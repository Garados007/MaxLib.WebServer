using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace MaxLib.WebServer.Sessions
{
    public class MemorySessionService : SessionServiceBase
    {
        public Dictionary<string, Session> Sessions { get; }
            = new Dictionary<string, Session>();

        private readonly object sessionsLock = new();

        protected override ValueTask<Session> Get(string key)
        {
            _ = key ?? throw new ArgumentNullException(nameof(key));
            Session? value;
            lock (sessionsLock)
            {
                if (Sessions.TryGetValue(key, out value) && value.LastUsed + MaxAge < DateTime.UtcNow)
                    // expired: never resurrect stale data under an old id, and drop it here
                    // instead of paying for a full sweep on every single request
                    value = null;
                if (value is null)
                    Sessions[key] = value = new Session();
                value.LastUsed = DateTime.UtcNow;
            }
            return new ValueTask<Session>(value);
        }

        protected override ValueTask<bool> IsKeyAvailable(string key)
        {
            lock (sessionsLock)
                return new ValueTask<bool>(!Sessions.ContainsKey(key));
        }

        /// <summary>
        /// The margin <see cref="RunAutomaticSweep" /> adds after the oldest surviving
        /// session's expiry when it schedules its next run, so a session isn't swept the
        /// instant it expires and the timer isn't rescheduled to a near-immediate delay.
        /// Default: one hour.
        /// </summary>
        public TimeSpan AutomaticSweepMargin { get; set; } = TimeSpan.FromHours(1);

        private Timer? sweepTimer;

        /// <summary>
        /// Removes every session whose <see cref="Session.LastUsed" /> plus <see
        /// cref="SessionServiceBase.MaxAge" /> is in the past. This is the only way expired
        /// sessions are ever evicted from <see cref="Sessions" /> in bulk - <see
        /// cref="Get(string)" /> only ever discards the single expired entry it happens to
        /// look up, to keep individual requests cheap. Call this yourself on whatever
        /// schedule suits your application, or use <see cref="RunAutomaticSweep" /> to have
        /// this service schedule it for you.
        /// </summary>
        /// <returns>
        /// The <see cref="Session.LastUsed" /> of the least-recently-used session that
        /// survived the sweep, or null if no session remains - so a caller scheduling the
        /// next sweep doesn't need a second pass over <see cref="Sessions" /> to find it.
        /// </returns>
        public DateTime? Sweep()
        {
            var now = DateTime.UtcNow;
            DateTime? oldestSurviving = null;
            lock (sessionsLock)
            {
                List<string>? expired = null;
                foreach (var (key, session) in Sessions)
                {
                    if (session.LastUsed + MaxAge < now)
                        (expired ??= new List<string>()).Add(key);
                    else if (oldestSurviving is null || session.LastUsed < oldestSurviving)
                        oldestSurviving = session.LastUsed;
                }
                if (expired != null)
                    foreach (var key in expired)
                        Sessions.Remove(key);
            }
            return oldestSurviving;
        }

        /// <summary>
        /// Starts (or restarts) a background timer that calls <see cref="Sweep" /> for you,
        /// so <see cref="Sessions" /> doesn't grow without bound. Entirely optional: <see
        /// cref="Sweep" /> is public specifically so you can drive it from your own
        /// timer/background-job infrastructure instead, e.g. if your application already has
        /// one and you'd rather not run a second. Each run reschedules itself for shortly
        /// after the next session is due to expire (see <see cref="AutomaticSweepMargin" />)
        /// instead of polling on a fixed interval. Stop it with <see
        /// cref="StopAutomaticSweep" /> (also done by <see cref="Dispose" />).
        /// </summary>
        public void StartAutomaticSweep()
        {
            lock (sessionsLock)
            {
                sweepTimer?.Dispose();
                sweepTimer = new Timer(RunAutomaticSweep, null, TimeSpan.Zero, Timeout.InfiniteTimeSpan);
            }
        }

        /// <summary>
        /// Stops the background timer started by <see cref="StartAutomaticSweep" />, if
        /// running. Does not remove any session <see cref="Sweep" /> already evicted.
        /// </summary>
        public void StopAutomaticSweep()
        {
            lock (sessionsLock)
            {
                sweepTimer?.Dispose();
                sweepTimer = null;
            }
        }

        /// <summary>
        /// Runs one <see cref="Sweep" /> and reschedules the timer <see
        /// cref="StartAutomaticSweep" /> started for shortly after the next session is due to
        /// expire, using the oldest surviving session <see cref="Sweep" /> reports back -
        /// avoiding a second pass over <see cref="Sessions" /> just to find it. Exposed as
        /// public so a host that prefers to drive its own scheduling can still reuse this
        /// exact reschedule logic instead of reimplementing it.
        /// </summary>
        public void RunAutomaticSweep(object? state = null)
        {
            var oldestSurviving = Sweep();
            var delay = oldestSurviving is DateTime oldest
                ? oldest + MaxAge + AutomaticSweepMargin - DateTime.UtcNow
                : AutomaticSweepMargin;
            if (delay < TimeSpan.Zero)
                delay = TimeSpan.Zero;
            lock (sessionsLock)
                sweepTimer?.Change(delay, Timeout.InfiniteTimeSpan);
        }

        public override void Dispose()
        {
            StopAutomaticSweep();
            base.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
