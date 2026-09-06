using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using OpenIdentityStack.Api.Authorization;
using OpenIdentityStack.Application.Abstractions;

namespace OpenIdentityStack.Api.Tests.Authorization;

public sealed class AdministrativeApprovalOutcomeMiddlewareTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AuditFailurePreservesHandlerOutcome(bool handlerFails)
    {
        var original = new InvalidOperationException("handler failure");
        IAdministrativeApproval approval = Substitute.For<IAdministrativeApproval>();
        approval.RecordOutcomeAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("audit unavailable")));
        var context = new DefaultHttpContext();
        var middleware = new AdministrativeApprovalOutcomeMiddleware(_ =>
        {
            if (handlerFails) { throw original; }
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return Task.CompletedTask;
        }, NullLogger<AdministrativeApprovalOutcomeMiddleware>.Instance);

        if (handlerFails)
        {
            (await Should.ThrowAsync<InvalidOperationException>(() => middleware.InvokeAsync(context, approval))).ShouldBeSameAs(original);
        }
        else
        {
            await middleware.InvokeAsync(context, approval);
            context.Response.StatusCode.ShouldBe(StatusCodes.Status204NoContent);
        }
        await approval.Received(1).RecordOutcomeAsync(!handlerFails, CancellationToken.None);
    }
}
