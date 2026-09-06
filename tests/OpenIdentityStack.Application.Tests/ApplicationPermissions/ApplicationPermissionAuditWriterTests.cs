using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.ApplicationPermissions;

namespace OpenIdentityStack.Application.Tests.ApplicationPermissions;

public sealed class ApplicationPermissionAuditWriterTests
{
    [Theory]
    [InlineData("system", "client:system", "client:system")]
    [InlineData("federation", "system", "federation")]
    [InlineData("human-id", "human-id", "human-id")]
    public async Task AuditUsesTypedRequestActorWithoutChangingAuthorizationIdentity(string suppliedActor, string contextActor, string expectedActor)
    {
        IAuditLog audit = Substitute.For<IAuditLog>();
        IAdministrativeActorContext actor = Substitute.For<IAdministrativeActorContext>();
        actor.AuditActorId.Returns(contextActor);
        var writer = new ApplicationPermissionAuditWriter(audit, actor);

        await writer.WriteAsync("Registry.Changed", suppliedActor, "application", "success");

        await audit.Received(1).LogAsync(expectedActor, "Registry.Changed", "RegisteredApplication", "application", "success", Arg.Any<CancellationToken>());
    }
}
