using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Authorization;
using OpenIdentityStack.Application.Roles.Queries;
using OpenIdentityStack.Application.Users.Queries;
using OpenIdentityStack.Domain.Users;
using SharedKernel;

namespace OpenIdentityStack.Application.Tests.Authorization;

public sealed class AdministrativeApprovalTests
{
    private readonly IAdministrativeActorContext context = Substitute.For<IAdministrativeActorContext>();
    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly IGetUserEffectiveRolesQueryHandler roles = Substitute.For<IGetUserEffectiveRolesQueryHandler>();
    private readonly IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
    private readonly IAdministrativeApprovalAudit audit = Substitute.For<IAdministrativeApprovalAudit>();
    private readonly User user;
    private readonly AdministrativeApproval approval;
    private static readonly DateTimeOffset now = new(2026, 9, 5, 12, 0, 0, TimeSpan.Zero);

    public AdministrativeApprovalTests()
    {
        this.clock.UtcNow.Returns(now);
        this.user = User.CreateFederated("operator@example.com", "Operator", this.clock).Value;
        this.context.Current.Returns(new AdministrativeActor(this.user.Id, now, true, true));
        this.users.GetByIdAsync(this.user.Id, Arg.Any<CancellationToken>()).Returns(this.user);
        this.SetPermissions(["*"]);
        this.approval = new AdministrativeApproval(this.context, this.users, this.roles, this.clock, this.audit);
    }

    [Theory]
    [InlineData(-301, true, true, false)]
    [InlineData(-300, true, true, true)]
    [InlineData(0, true, true, true)]
    [InlineData(1, true, true, false)]
    [InlineData(0, false, true, false)]
    [InlineData(0, true, false, false)]
    public async Task RequiresFreshAcknowledgedHuman(int seconds, bool human, bool acknowledged, bool allowed)
    {
        this.context.Current.Returns(new AdministrativeActor(this.user.Id, now.AddSeconds(seconds), human, acknowledged));
        Result result = await this.approval.RequireAsync("Role.GrantUnrestricted", "role-id");
        result.IsSuccess.ShouldBe(allowed);
    }

    [Fact]
    public async Task MissingAuthenticationTimeRequiresReauthentication()
    {
        this.context.Current.Returns(new AdministrativeActor(this.user.Id, null, true, true));
        Result result = await this.approval.RequireAsync("Role.GrantUnrestricted", "role-id");
        result.Error.Code.ShouldContain("ReauthenticationRequired");
    }

    [Fact]
    public async Task MissingActorDeniesApproval()
    {
        this.context.Current.Returns((AdministrativeActor?)null);
        (await this.approval.RequireAsync("Role.GrantUnrestricted", "role-id")).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task DisabledActorDeniesApproval()
    {
        this.user.Disable("Administrative action", this.clock);
        (await this.approval.RequireAsync("Role.GrantUnrestricted", "role-id")).IsFailure.ShouldBeTrue();
    }

    [Theory]
    [InlineData("roles:write")]
    [InlineData("roles:*")]
    public async Task CurrentPermissionsMustContainExplicitAllGrant(string permission)
    {
        this.SetPermissions([permission]);
        (await this.approval.RequireAsync("Role.GrantUnrestricted", "role-id")).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task RechecksPersistedAuthorityAfterWithdrawal()
    {
        (await this.approval.RequireAsync("Role.GrantUnrestricted", "one")).IsSuccess.ShouldBeTrue();
        this.SetPermissions([]);
        (await this.approval.RequireAsync("Role.GrantUnrestricted", "two")).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task AuditsApprovedIntentAndCommittedOutcomeSeparately()
    {
        (await this.approval.RequireAsync("Role.GrantUnrestricted", "role-id")).IsSuccess.ShouldBeTrue();
        await this.audit.Received(1).LogAsync(this.user.Id.Value.ToString(), "AdministrativeApproval.IntentApproved",
            "Role.GrantUnrestricted", "role-id", Arg.Any<string>(), Arg.Any<CancellationToken>());
        await this.approval.RecordOutcomeAsync(true);
        await this.audit.Received(1).LogAsync(this.user.Id.Value.ToString(), "AdministrativeApproval.MutationSucceeded",
            "Role.GrantUnrestricted", "role-id", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApprovalFailsWhenIntentAuditCannotPersist()
    {
        this.audit.LogAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.FromException(new InvalidOperationException("Audit unavailable")));
        await Should.ThrowAsync<InvalidOperationException>(() => this.approval.RequireAsync("Role.GrantUnrestricted", "role-id"));
    }

    [Theory]
    [InlineData("users:write", true)]
    [InlineData("*", false)]
    public async Task UserAccessChangesRequireApprovalOnlyForUnrestrictedTargets(string permission, bool allowed)
    {
        var targetId = UserId.Create();
        IReadOnlyList<RoleDto> targetRoles = [new RoleDto(Guid.NewGuid(), "target", "Target", null, false, true, [permission])];
        this.roles.HandleAsync(targetId, Arg.Any<CancellationToken>()).Returns((Result<IReadOnlyList<RoleDto>>)targetRoles.ToList());
        this.context.Current.Returns((AdministrativeActor?)null);
        (await this.approval.RequireForUserAccessAsync(targetId, "User.Enable")).IsSuccess.ShouldBe(allowed);
    }

    private void SetPermissions(IReadOnlyList<string> permissions)
    {
        IReadOnlyList<RoleDto> current = [new RoleDto(Guid.NewGuid(), "admin", "Role", null, false, true, permissions)];
        this.roles.HandleAsync(this.user.Id, Arg.Any<CancellationToken>()).Returns((Result<IReadOnlyList<RoleDto>>)current.ToList());
    }
}
