using MaxLib.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

#nullable enable

namespace MaxLib.WebServer
{
    [Serializable]
    public class MultipartRanges : HttpDataSource
    {
        static long joinGap = 0x40; //1kB
        /// <summary>
        /// If two ranges has a gap smaller then <see cref="JoinGap"/> than this two ranges
        /// are merged and transfered in one block. A maximum amount of <see cref="JoinGap"/>
        /// bytes need to send extra.
        /// </summary>
        public static long JoinGap
        {
            get => joinGap;
            set
            {
                if (value < 0) throw new ArgumentOutOfRangeException(nameof(JoinGap));
                joinGap = value;
            }
        }

        public override bool CanAcceptData => false;

        public override bool CanProvideData => true;

        [Serializable]
        struct Range
        {
            public long From, To;

            public string ToString(long total)
            {
                var sb = new StringBuilder();
                sb.Append("bytes ");
                sb.Append(From);
                sb.Append("-");
                sb.Append(To);
                sb.Append("/");
                sb.Append(total);
                return sb.ToString();
            }
        }

        readonly List<HttpDataSource> streams = new List<HttpDataSource>();
        readonly Stream baseStream;
        // readonly HttpDocument document;
        readonly HttpResponseHeader response;
        List<Range> ranges = new List<Range>();

        [Obsolete("Use MultipartRanges(Stream, HttpRequestHeader, HttpResponseHeader, string) instead. This will be removed in a future release.")]
        public MultipartRanges(Stream stream, HttpDocument document, string? mime)
            : this(stream, document.RequestHeader, document.ResponseHeader, mime)
        {

        }

        /// <summary>
        /// Creates a new <see cref="MultipartRanges"/> data source that can deliver parts
        /// of the given stream.
        /// </summary>
        /// <param name="stream">the stream with the full data. This streams needs to be seekable.</param>
        /// <param name="task">the progress task to work on</param>
        /// <param name="mime">the optional mime type of this data source</param>
        /// <exception cref="ArgumentNullException" />
        public MultipartRanges(Stream stream, WebProgressTask task, string? mime)
            : this(
                stream,
                task?.Request ?? throw new ArgumentNullException(nameof(task.Request)),
                task?.Response ?? throw new ArgumentNullException(nameof(task.Response)),
                mime
            )
        {

        }

        /// <summary>
        /// Creates a new <see cref="MultipartRanges"/> data source that can deliver parts
        /// of the given stream.
        /// </summary>
        /// <param name="stream">the stream with the full data. This streams needs to be seekable.</param>
        /// <param name="request">the request header</param>
        /// <param name="response">the response header</param>
        /// <param name="mime">the optional mime type of this data source</param>
        /// <exception cref="ArgumentNullException" />
        public MultipartRanges(Stream stream, HttpRequestHeader request, 
            HttpResponseHeader response, string? mime)
        {
            _ = request ?? throw new ArgumentNullException(nameof(request));
            this.response = response ?? throw new ArgumentNullException(nameof(response));
            baseStream = stream ?? throw new ArgumentNullException(nameof(stream));
            MimeType = string.IsNullOrWhiteSpace(mime) ? WebServer.MimeType.TextHtml : mime;

            response.HeaderParameter["Accept-Ranges"] = "bytes";
            if (request.HeaderParameter.ContainsKey("Range"))
            {
                ParseRanges(request.HeaderParameter["Range"]);
                var valid = ranges.Count > 0;
                foreach (var r in ranges)
                    if (r.From < 0 || r.From >= baseStream.Length || r.To < 0 || r.To >= baseStream.Length)
                        valid = false;
                if (!valid)
                {
                    response.StatusCode = HttpStateCode.RequestedRangeNotSatisfiable;
                    return;
                }
                FormatRanges();
                if (ranges.Count == 1)
                    SinglePart();
                else MultiPart();
            }
            else
            {
                streams.Add(new HttpStreamDataSource(stream)
                {
                    TransferCompleteData = true
                });
            }
        }

        void ParseRanges(string code)
        {
            code = code.Trim();
            if (!code.StartsWith("bytes")) return;
            code = code[5..].TrimStart();
            if (!code.StartsWith("=")) return;
            code = code[1..].TrimStart();
            foreach (var part in code.Split(','))
            {
                var t = part.Split('-');
                if (t.Length == 2 && 
                    long.TryParse(t[0].Trim(), out long from))
                {
                    if (long.TryParse(t[1].Trim(), out long to))
                        ranges.Add(new Range { From = from, To = to });
                    else ranges.Add(new Range { From = from, To = baseStream.Length - 1 });
                }
                else
                {
                    ranges.Clear();
                    return;
                }
            }
        }

        void FormatRanges()
        {
            if (ranges.Count < 2) return;
            ranges.Sort((r1, r2) => r1.From.CompareTo(r2.To));
            var nr = new List<Range>(ranges.Count);
            Range? last = null;
            for (int i = 0; i < ranges.Count; ++i)
                if (last != null)
                {
                    if (last.Value.To < ranges[i].From - 1 - JoinGap)
                    {
                        nr.Add(last.Value);
                        last = ranges[i];
                    }
                    else last = new Range
                    {
                        From = last.Value.From,
                        To = Math.Max(last.Value.To, ranges[i].To)
                    };
                }
                else last = ranges[i];
            if (last != null) nr.Add(last.Value);
            ranges = nr;
        }

        void SinglePart()
        {
            response.StatusCode = HttpStateCode.PartialContent;
            var h = response.HeaderParameter;
            h["Content-Range"] = ranges[0].ToString(baseStream.Length);
            streams.Add(new HttpStreamDataSource(baseStream)
            {
                RangeStart = ranges[0].From,
                RangeEnd = ranges[0].To,
                TransferCompleteData = false
            });
        }

        void MultiPart()
        {
            var b = new byte[8];
            new Random().NextBytes(b);
            var boundary = BitConverter.ToString(b).Replace("-", "");
            base.MimeType = WebServer.MimeType.MultipartByteranges + "; boundary=" + boundary;
            response.StatusCode = HttpStateCode.PartialContent;
            var sb = new StringBuilder();
            foreach (var r in ranges)
            {
                sb.Append("--");
                sb.AppendLine(boundary);
                if (MimeType != null)
                {
                    sb.Append("Content-Type: ");
                    sb.AppendLine(MimeType);
                }
                sb.Append("Content-Range: ");
                sb.AppendLine(r.ToString(baseStream.Length));
                sb.AppendLine();
                streams.Add(new HttpStringDataSource(sb.ToString())
                {
                    TransferCompleteData = true
                });
                streams.Add(new HttpStreamDataSource(baseStream)
                {
                    RangeStart = r.From,
                    RangeEnd = r.To,
                    TransferCompleteData = false
                });
                sb.Clear();
            }
            sb.Append("--");
            sb.Append(boundary);
            sb.Append("--");
            streams.Add(new HttpStringDataSource(sb.ToString())
            {
                TransferCompleteData = true
            });
        }

        public override long? Length()
        {
            long sum = 0;
            foreach (var stream in streams)
            {
                var length = stream.Length();
                if (length == null)
                    return null;
                sum += length.Value;
            }
            return sum;
        }

        public override void Dispose()
        {
            baseStream.Dispose();
            foreach (var s in streams) s.Dispose();
        }

        protected override async Task<long> WriteStreamInternal(Stream stream, long start, long? stop)
        {
            using (var skip = new SkipableStream(stream, start))
            {
                long total = 0;
                foreach (var s in streams)
                {
                    if (stop != null && total >= stop.Value)
                        return total;
                    var end = stop == null ? null : (long?)(stop.Value - total);
                    var size = s.Length();
                    if (size == null)
                    {
                        total += await s.WriteStream(skip, 0, end);
                    }
                    else
                    {
                        if (size.Value < skip.SkipBytes)
                        {
                            skip.Skip(size.Value);
                            continue;
                        }
                        var leftSkip = skip.SkipBytes;
                        skip.Skip(skip.SkipBytes);
                        total += await s.WriteStream(skip, leftSkip, end);
                    }
                }
                return total;
            }
        }

        protected override Task<long> ReadStreamInternal(Stream stream, long? length)
            => throw new NotSupportedException();
    }
}
