using System;
using System.IO;

namespace MaxLib.WebServer.Benchmark
{
    public class BenchmarkTask : IDisposable
    {
        public WebProgressTask Task { get; }

        public MemoryStream Input { get; }

        public MemoryStream Output { get; }

        public void WriteToInput(string text)
        {
            Input.Write(System.Text.Encoding.UTF8.GetBytes(text));
        }

        public BenchmarkTask()
        {
            Input = new MemoryStream();
            Output = new MemoryStream();
            var stream = new IO.CombineStream(Input, Output);
            Task = new WebProgressTask
            {
                Connection = new HttpConnection
                {
                    NetworkStream = stream,
                },
                NetworkStream = stream,
            };
        }

        public void Dispose()
        {
            Input.Dispose();
            Output.Dispose();
            Task.Dispose();
        }
    }
}