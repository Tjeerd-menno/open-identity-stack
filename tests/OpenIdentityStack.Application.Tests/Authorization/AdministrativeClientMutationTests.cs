using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Applications;
using OpenIdentityStack.Application.Applications.Commands;
using OpenIdentityStack.Domain.Applications;
using SharedKernel;
using DomainApplication = OpenIdentityStack.Domain.Applications.Application;

namespace OpenIdentityStack.Application.Tests.Authorization;

public sealed class AdministrativeClientMutationTests
{
    [Theory]
    [InlineData("oauth")]
    [InlineData("enable")]
    [InlineData("secret")]
    [InlineData("certificate")]
    public async Task SensitiveClientChangesRequireApprovalBeforeMutation(string operation)
    {
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        DomainApplication client = DomainApplication.Create("approved-client", "Approved", null, ApplicationProfile.MachineToMachine,
            OAuthClientType.Confidential, ["client_credentials"], ["ois.admin"], [], [], false, false, clock).Value;
        if (operation == "enable") { client.Disable(clock); }
        IApplicationRepository repository = Substitute.For<IApplicationRepository>();
        repository.GetByIdAsync(client.Id, Arg.Any<CancellationToken>()).Returns(client);
        IApplicationProtocolProjection projection = Substitute.For<IApplicationProtocolProjection>();
        projection.UpsertAsync(Arg.Any<DomainApplication>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        IAdministrativeClientGuard guard = Substitute.For<IAdministrativeClientGuard>();
        guard.RequireAsync(client.Id, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(DomainError.Forbidden("AdministrativeApproval.HumanRequired", "Human approval is required.")));
        var lifecycle = new ApplicationLifecycleUseCases(repository, projection, Substitute.For<IPasswordHasher>(), clock, Substitute.For<IAuditLog>(), guard);
        var credentials = new ApplicationCredentialUseCases(repository, projection, Substitute.For<IPasswordHasher>(), clock,
            Substitute.For<IAuditLog>(), guard, new UnauthenticatedAdministrativeActorContext());
        bool failed = operation switch
        {
            "enable" => (await lifecycle.ExecuteAsync(new EnableApplicationCommand(client.Id))).IsFailure,
            "oauth" => (await lifecycle.ExecuteAsync(new ConfigureApplicationOAuthCommand(client.Id, ApplicationProfile.MachineToMachine,
                OAuthClientType.Confidential, ["client_credentials"], ["ois.admin", "business"], [], [], false, false))).IsFailure,
            "secret" => (await credentials.ExecuteAsync(new AddApplicationSecretCommand(client.Id, null, null, false))).IsFailure,
            _ => (await credentials.ExecuteAsync(new AddApplicationCertificateCommand(client.Id, "ABCDEF1234", null, null, null))).IsFailure,
        };
        failed.ShouldBeTrue();
        await guard.Received(1).RequireAsync(client.Id, Arg.Any<string>(), Arg.Any<CancellationToken>());
        client.AllowedScopes.ShouldBe(["ois.admin"]);
        client.Credentials.ShouldBeEmpty();
        if (operation == "enable") { client.Status.ShouldBe(ApplicationStatus.Disabled); }
        await repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await projection.DidNotReceiveWithAnyArgs().UpsertAsync(default!, default);
    }
}
