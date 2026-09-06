using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Users.Queries;
using OpenIdentityStack.Domain.Federation;
using OpenIdentityStack.Domain.Users;
using SharedKernel;

namespace OpenIdentityStack.Application.Tests.Users;

public sealed class IdentityMigrationInventoryQueryHandlerTests
{
    [Theory]
    [InlineData(UserStatus.Active)]
    [InlineData(UserStatus.PendingVerification)]
    [InlineData(UserStatus.Disabled)]
    public async Task PasswordPresence_RemainsOnlyACandidateAndDoesNotClearMigrationBlock(UserStatus status)
    {
        IUserRepository users = Substitute.For<IUserRepository>();
        IUpstreamProviderRepository providers = Substitute.For<IUpstreamProviderRepository>();
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        User user = User.CreateLocal("candidate@example.com", "Candidate", "password-hash", clock).Value;
        if (status != UserStatus.PendingVerification) { user.VerifyEmail(clock).IsSuccess.ShouldBeTrue(); }
        if (status == UserStatus.Disabled) { user.Disable("Disabled", clock).IsSuccess.ShouldBeTrue(); }
        user.LinkUpstreamIdentity(UpstreamProviderId.Create(), "legacy", "legacy-subject", null);
        users.ListWithUpstreamIdentitiesAsync(1, 20, null, Arg.Any<CancellationToken>()).Returns(((IReadOnlyList<User>)[user], 1));
        providers.GetActiveProvidersAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<UpstreamProvider>());
        var handler = new IdentityMigrationInventoryQueryHandler(users, providers);
        Result<IdentityMigrationInventoryResult> result = await handler.ExecuteAsync(new IdentityMigrationInventoryQuery());
        IdentityMigrationUser item = result.Value.Items.Single();
        item.HasPasswordCredential.ShouldBeTrue();
        item.MigrationBlocked.ShouldBeTrue();
        item.RecoveryRequired.ShouldBe(status != UserStatus.Active);
        item.Identities.Single().AssociationEvidence.ShouldBe("Unknown");
    }

    [Fact]
    public async Task OnlyIndependentProvenActiveProviderIsListedAsFederationCandidate()
    {
        IUserRepository users = Substitute.For<IUserRepository>();
        IUpstreamProviderRepository providers = Substitute.For<IUpstreamProviderRepository>();
        UpstreamProvider provider = UpstreamProvider.Create("proven", "Proven", "https://issuer.example", "client").Value;
        provider.BindIssuer("https://issuer.example", provider.Authority);
        User user = User.ProvisionFederated("candidate@example.com", "Candidate", provider.Id, provider.Name, "proven-subject", "https://issuer.example").Value;
        user.LinkUpstreamIdentity(UpstreamProviderId.Create(), "legacy", "legacy-subject", null, "https://issuer.example");
        users.ListWithUpstreamIdentitiesAsync(1, 20, null, Arg.Any<CancellationToken>()).Returns(((IReadOnlyList<User>)[user], 1));
        providers.GetActiveProvidersAsync(Arg.Any<CancellationToken>()).Returns(new[] { provider });
        var handler = new IdentityMigrationInventoryQueryHandler(users, providers);
        IdentityMigrationUser item = (await handler.ExecuteAsync(new IdentityMigrationInventoryQuery())).Value.Items.Single();
        item.MigrationBlocked.ShouldBeTrue();
        item.CandidateFederationProviderIds.ShouldBe(new[] { provider.Id.Value });
        item.RecoveryRequired.ShouldBeFalse();
        providers.GetActiveProvidersAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<UpstreamProvider>());
        item = (await handler.ExecuteAsync(new IdentityMigrationInventoryQuery())).Value.Items.Single();
        item.CandidateFederationProviderIds.ShouldBeEmpty();
        item.RecoveryRequired.ShouldBeTrue();
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 101)]
    [InlineData(int.MaxValue, 100)]
    public async Task InvalidPaginationIsRejectedBeforeReadingInventory(int page, int pageSize)
    {
        IUserRepository users = Substitute.For<IUserRepository>();
        var handler = new IdentityMigrationInventoryQueryHandler(users, Substitute.For<IUpstreamProviderRepository>());
        (await handler.ExecuteAsync(new IdentityMigrationInventoryQuery(page, pageSize))).IsFailure.ShouldBeTrue();
        await users.DidNotReceive().ListWithUpstreamIdentitiesAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<UpstreamProviderId?>(), Arg.Any<CancellationToken>());
    }
}
