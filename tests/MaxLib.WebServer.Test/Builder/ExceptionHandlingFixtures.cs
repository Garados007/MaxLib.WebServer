using MaxLib.WebServer;
using MaxLib.WebServer.Builder;

#nullable enable

namespace MaxLib.WebServer.Test.Builder
{
    // A synchronous (non-Task) handler that throws HttpException - the documented pattern for
    // cancelling a request with a specific status code. MethodBase.Invoke wraps this in a
    // TargetInvocationException unless the caller passes BindingFlags.DoNotWrapExceptions.
    [Path("/sync-throw")]
    public class SyncHttpExceptionService
    {
        [Path("/sync-throw")]
        public HttpDataSource Foo()
        {
            throw new HttpException(HttpStateCode.Forbidden);
        }
    }

    // A handler with a typed parameter, used to exercise a conversion failure (e.g. a
    // non-numeric query value bound to an int) at the parameter-binding layer.
    [Path("/conv-fail")]
    public class ConversionFailureService
    {
        [Path("/conv-fail")]
        public HttpDataSource Foo([Get] int id)
        {
            return new HttpStringDataSource($"id={id}");
        }
    }
}
