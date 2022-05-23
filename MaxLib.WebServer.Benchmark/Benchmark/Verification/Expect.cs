using System.Collections.Generic;

namespace MaxLib.WebServer.Benchmark.Verification
{
    public class Expect
    {
        public HttpStateCode? HttpStateCode { get; set; }

        public Dictionary<string, string?>? Header { get; set; }

        public string? TextResponse { get; set; }

        protected void Assert<T>(string path, T expected, T actual)
        {
            if (Equals(expected, actual))
                throw new VerificationException(path, expected, actual);
        }

        public void Verify(BenchmarkTask task)
        {
            if (HttpStateCode != null)
                Assert($"HttpStateCode", HttpStateCode.Value, task.Task.Response.StatusCode);
            if (Header != null)
                foreach (var (key, value) in Header)
                {
                    Assert($"Header[{key}]", value, task.Task.Response.GetHeader(key));
                }
            if (TextResponse != null)
            {
                var actual = System.Text.Encoding.UTF8.GetString(task.Output.ToArray());
                Assert("TextResponse", TextResponse, actual);
            }
        }
    }
}