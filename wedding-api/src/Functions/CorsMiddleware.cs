using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;

namespace WeddingApi.Functions;

public sealed class CorsMiddleware : IFunctionsWorkerMiddleware
{
    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var req = await context.GetHttpRequestDataAsync();
        if (req is null)
        {
            await next(context);
            return;
        }

        // Handle CORS preflight.
        if (string.Equals(req.Method, "OPTIONS", StringComparison.OrdinalIgnoreCase))
        {
            var res = req.CreateResponse(HttpStatusCode.NoContent);
            if (Cors.TryApply(req, res))
            {
                res.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");

                var requestedHeaders = Cors.TryGetHeader(req, "Access-Control-Request-Headers");
                res.Headers.Add(
                    "Access-Control-Allow-Headers",
                    string.IsNullOrWhiteSpace(requestedHeaders) ? "Content-Type" : requestedHeaders);
            }

            context.GetInvocationResult().Value = res;
            return;
        }

        await next(context);
    }
}
