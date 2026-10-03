namespace MaxLib.WebServer.Builder.Debugger.Example
{
    /// <summary>
    /// A path variable and a class-level path prefix, done correctly.
    /// </summary>
    [Path("/greet", Prefix = true)]
    public class GreetingService : Service
    {
        [Path("/greet/{name}")]
        public HttpDataSource Hello([Var] string name)
            => new HttpStringDataSource($"Hello, {name}!");
    }

    public record MathResult(int A, int B, int Sum);

    /// <summary>
    /// GET query parameters with automatic string-to-int conversion, and a JSON API endpoint.
    /// </summary>
    [Path("/math", Prefix = true)]
    public class MathService : Service
    {
        [Path("/math/add")]
        public HttpDataSource Add([Get] int a, [Get] int b)
            => new HttpStringDataSource($"{a} + {b} = {a + b}");

        [Path("/math/add-json")]
        [return: JsonDataConverter]
        public MathResult AddJson([Get] int a, [Get] int b)
            => new(a, b, a + b);
    }

    /// <summary>
    /// A POST endpoint reading an "application/x-www-form-urlencoded" body.
    /// </summary>
    public class EchoPostService : Service
    {
        [Path("/echo-post")]
        [Method("POST")]
        public HttpDataSource Echo([UrlEncodedPost] string message)
            => new HttpStringDataSource($"You said: {message}");
    }

    /// <summary>
    /// Framework types (here: the raw request header) can be injected into a method parameter
    /// without any attribute - the generator resolves them as "core parameters".
    /// </summary>
    public class RequestInfoService : Service
    {
        [Path("/request-info")]
        public HttpDataSource Info(HttpRequestHeader request)
            => new HttpStringDataSource(
                $"{request.ProtocolMethod} {request.Location.Url} from {request.Host}"
            );
    }

    /// <summary>
    /// Two unrelated services both claim the same path. Whichever has the higher priority always
    /// wins, and the other one never runs - open the debugger's request simulator on
    /// "/priority-demo" to see this play out and which candidate was never reached.
    /// </summary>
    public class PriorityDemoHighService : Service
    {
        [Path("/priority-demo")]
        [Priority(WebServicePriority.High)]
        public HttpDataSource M() => new HttpStringDataSource("Handled by the HIGH priority service.");
    }

    public class PriorityDemoLowService : Service
    {
        [Path("/priority-demo")]
        [Priority(WebServicePriority.Low)]
        public HttpDataSource M() => new HttpStringDataSource("Handled by the LOW priority service.");
    }
}
