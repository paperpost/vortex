using Grpc.Core;
using Grpc.Core.Interceptors;
using Serilog;
using System.Diagnostics;
using System.Threading.Tasks;

namespace vortex.Common
{
  public class gRPCLogger : Interceptor
  {
    private const string MessageTemplate =
      "{RequestMethod} responded {StatusCode} in {Elapsed:0.0000} ms";

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(TRequest request, ServerCallContext context, UnaryServerMethod<TRequest, TResponse> continuation)
    {
      var sw = Stopwatch.StartNew();

      Log.Debug("Started request to "+context.Method);

      var response = await base.UnaryServerHandler(request, context, continuation);

      sw.Stop();
      Log.Information(MessageTemplate,
            context.Method,
            context.Status.StatusCode,
            sw.Elapsed.TotalMilliseconds);

      return response;
    }
  }
}