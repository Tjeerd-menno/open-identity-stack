namespace OpenIdentityStack.Api.Diagnostics;

public sealed partial class DevelopmentRequestDiagnosticsMiddleware(
    RequestDelegate next,
    ILogger<DevelopmentRequestDiagnosticsMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            string method = context.Request.Method;
            string endpoint = context.GetEndpoint()?.DisplayName ?? "<unmatched>";
            bool authorizationPresent = context.Request.Headers.ContainsKey("Authorization");
            bool cookiePresent = context.Request.Headers.ContainsKey("Cookie");
            bool acceptPresent = context.Request.Headers.ContainsKey("Accept");
            bool contentTypePresent = context.Request.Headers.ContainsKey("Content-Type");
            RequestReceived(
                logger,
                method,
                endpoint,
                authorizationPresent,
                cookiePresent,
                acceptPresent,
                contentTypePresent);
        }

        await next(context);
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Request {Method} {Endpoint}; Authorization present: {AuthorizationPresent}; Cookie present: {CookiePresent}; Accept present: {AcceptPresent}; Content-Type present: {ContentTypePresent}")]
    private static partial void RequestReceived(
        ILogger logger,
        string method,
        string endpoint,
        bool authorizationPresent,
        bool cookiePresent,
        bool acceptPresent,
        bool contentTypePresent);
}
