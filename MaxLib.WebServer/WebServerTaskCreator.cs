using System;
using System.IO;
using System.Threading.Tasks;

namespace MaxLib.WebServer
{
    public class WebServerTaskCreator
    {
        public WebProgressTask Task { get; private set; }

        public ServerStage TerminationStage { get; set; }

        public WebServerTaskCreator()
        {
            Task = new WebProgressTask()
            {
                CurrentStage = ServerStage.FIRST_STAGE,
                Document = new HttpDocument()
                {
                    RequestHeader = new HttpRequestHeader(),
                    ResponseHeader = new HttpResponseHeader(),
                },
                Connection = new HttpConnection
                {
                    Ip = "127.0.0.1",
                    LastWorkTime = -1,
                    ConnectionKey = new byte[0],
                },
                NetworkStream = new MemoryStream()
            };
            TerminationStage = ServerStage.FINAL_STAGE;
        }

        public async Task Start(Server server)
        {
            Task.Server = server;
            await server.ExecuteTaskChain(Task, TerminationStage);
            Task.Server = null;
        }

        public void SetProtocolHeader(string url, string method = "GET", string protocol = "HTTP/1.1")
        {
            Task.Document.RequestHeader.ProtocolMethod = method;
            Task.Document.RequestHeader.Url = url;
            Task.Document.RequestHeader.HttpProtocol = protocol;
        }

        public void SetHeaderParameter(string key, string value)
        {
            if (Task.Document.RequestHeader.HeaderParameter.ContainsKey(key))
                Task.Document.RequestHeader.HeaderParameter[key] = value;
            else Task.Document.RequestHeader.HeaderParameter.Add(key, value);
        }

        public void SetPost(string post, string mime)
        {
            Task.Document.RequestHeader.Post.SetPost(post, mime);
        }

        public void SetAccept(string[] acceptTypes = null, string[] encoding = null)
        {
            if (acceptTypes != null) Task.Document.RequestHeader.FieldAccept.AddRange(acceptTypes);
            if (encoding != null) Task.Document.RequestHeader.FieldAcceptEncoding.AddRange(acceptTypes);
        }

        public void SetHost(string host)
        {
            Task.Document.RequestHeader.Host = host;
        }

        public void SetCookie(string cookieString)
        {
            Task.Document.RequestHeader.Cookie.SetRequestCookieString(cookieString);
        }

        public void SetStream(Stream stream)
        {
            Task.NetworkStream = stream;
        }

        public void SetStream(Stream input, Stream output)
        {
            Task.NetworkStream = new BidirectionalStream(input, output);
        }

        public class BidirectionalStream : Stream
        {
            public Stream Input { get; private set; }

            public Stream Output { get; private set; }

            public BidirectionalStream(Stream input, Stream output)
            {
                if (!input.CanRead) throw new ArgumentException("input is not readable");
                if (!output.CanWrite) throw new ArgumentException("output is not writeable");
                Input = input;
                Output = output;
            }

            public override bool CanRead
            {
                get
                {
                    return true;
                }
            }

            public override bool CanSeek
            {
                get
                {
                    return false;
                }
            }

            public override bool CanWrite
            {
                get
                {
                    return true;
                }
            }

            public override long Length
            {
                get
                {
                    throw new NotSupportedException();
                }
            }

            public override long Position
            {
                get => throw new NotSupportedException();

                set => throw new NotSupportedException();
            }

            public override void Flush()
            {
                Output.Flush();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                return Input.Read(buffer, offset, count);
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                Output.Write(buffer, offset, count);
            }
        }
    }
}
