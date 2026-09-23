using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using OpenIdentityStack.Api.Diagnostics;

namespace OpenIdentityStack.Api.UnitTests.Diagnostics;

public sealed class DevelopmentRequestDiagnosticsMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_NeverLogsCredentialBearingHeaderValues()
    {
        var logger = new CapturingLogger();
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "Bearer authorization-secret";
        context.Request.Headers.Cookie = "session=cookie-secret";
        context.Request.Headers["Set-Cookie"] = "session=set-cookie-secret";
        context.Request.Headers["Proxy-Authorization"] = "Basic proxy-secret";
        context.Request.Headers["X-Api-Key"] = "api-key-secret";
        context.Request.Headers.Accept = "application/json; value=accept-secret";
        context.Request.ContentType = "application/json; value=content-type-secret";
        var middleware = new DevelopmentRequestDiagnosticsMiddleware(_ => Task.CompletedTask, logger);

        await middleware.InvokeAsync(context);

        string message = logger.Messages.ShouldHaveSingleItem();
        foreach (string secret in new[] { "authorization-secret", "cookie-secret", "set-cookie-secret", "proxy-secret", "api-key-secret", "accept-secret", "content-type-secret" })
        {
            message.ShouldNotContain(secret);
        }
    }

    [Fact]
    public async Task InvokeAsync_LogsSafeRequestDiagnosticsAndRunsNext()
    {
        var logger = new CapturingLogger();
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.SetEndpoint(new Endpoint(null, new EndpointMetadataCollection(), "ImportApplicationPermissionManifest"));
        context.Request.Headers.Authorization = "Bearer secret";
        context.Request.Headers.Accept = "application/json";
        context.Request.ContentType = "application/json";
        bool nextCalled = false;
        var middleware = new DevelopmentRequestDiagnosticsMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, logger);

        await middleware.InvokeAsync(context);

        nextCalled.ShouldBeTrue();
        string message = logger.Messages.ShouldHaveSingleItem();
        message.ShouldContain("POST");
        message.ShouldContain("ImportApplicationPermissionManifest");
        message.ShouldContain("Authorization present: True");
        message.ShouldContain("Accept present: True");
        message.ShouldContain("Content-Type present: True");
    }

    private sealed class CapturingLogger : ILogger<DevelopmentRequestDiagnosticsMiddleware>
    {
        public List<string> Messages { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => new NoopScope();

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            this.Messages.Add(formatter(state, exception));
        }

        private sealed class NoopScope : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
