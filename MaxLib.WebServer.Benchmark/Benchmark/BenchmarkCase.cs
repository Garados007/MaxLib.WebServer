using System;

namespace MaxLib.WebServer.Benchmark
{
    /// <summary>
    /// A simple benchmark test case
    /// </summary>
    public abstract class BenchmarkCase : IDisposable
    {
        public virtual void Dispose()
        {

        }

        /// <summary>
        /// This function runs before each test and prepares the next test
        /// </summary>
        /// <param name="task">the target where the preparation has to be done</param>
        public abstract void Setup(BenchmarkTask task);

        /// <summary>
        /// Verifies the result if everything went well. If something happened this will call any
        /// <see cref="Exception" />.
        /// </summary>
        /// <param name="task">the target which has to be checked</param>
        public abstract void Verify(BenchmarkTask task);
    }
}