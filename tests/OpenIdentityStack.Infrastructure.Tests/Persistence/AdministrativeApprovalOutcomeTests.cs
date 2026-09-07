using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Authorization;
using OpenIdentityStack.Application.Roles.Commands;
using OpenIdentityStack.Application.Roles.Queries;
using OpenIdentityStack.Application.Users.Queries;
using OpenIdentityStack.Domain.Users;
using OpenIdentityStack.Infrastructure.Persistence;
using OpenIdentityStack.Infrastructure.Persistence.Roles;
using OpenIdentityStack.Infrastructure.Tests.Common;
using SharedKernel;

namespace OpenIdentityStack.Infrastructure.Tests.Persistence;

public sealed class AdministrativeApprovalOutcomeTests(AdministrativeAuthorityTestFixture fixture) : IClassFixture<AdministrativeAuthorityTestFixture>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProtectedRoleCreationPreservesCommitResultDuringOutcomeAuditOutage(bool failIntent)
    {
        await using OpenIdentityStackDbContext db = fixture.CreateDbContext();
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        clock.UtcNow.Returns(now);
        User human = User.CreateFederated("operator@example.test", "Operator", clock).Value;
        IAdministrativeActorContext actor = Substitute.For<IAdministrativeActorContext>();
        actor.Current.Returns(new AdministrativeActor(human.Id, now, true, true));
        IUserRepository users = Substitute.For<IUserRepository>();
        users.GetByIdAsync(human.Id, Arg.Any<CancellationToken>()).Returns(human);
        IGetUserEffectiveRolesQueryHandler roles = Substitute.For<IGetUserEffectiveRolesQueryHandler>();
        roles.HandleAsync(human.Id, Arg.Any<CancellationToken>()).Returns(
            (Result<IReadOnlyList<RoleDto>>)new List<RoleDto> { new(Guid.NewGuid(), "admin", "Admin", null, false, true, ["*"]) });
        IAdministrativeApprovalAudit audit = Substitute.For<IAdministrativeApprovalAudit>();
        string failingAction = failIntent ? "AdministrativeApproval.IntentApproved" : "AdministrativeApproval.MutationSucceeded";
        audit.LogAsync(Arg.Any<string>(), failingAction, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("Simulated audit outage")));
        ILogger<AdministrativeApproval> logger = Substitute.For<ILogger<AdministrativeApproval>>();
        logger.IsEnabled(LogLevel.Critical).Returns(true);
        var approval = new AdministrativeApproval(actor, users, roles, clock, audit, new AdministrativeAuthoritySnapshot(db), logger);
        IPermissionAssignmentValidator validator = Substitute.For<IPermissionAssignmentValidator>();
        validator.ValidateAssignableAsync(Arg.Any<IEnumerable<string>>(), true, Arg.Any<CancellationToken>()).Returns(Result.Success());
        var useCase = new CreateRoleUseCase(new RoleRepository(db), validator, approval);
        string name = $"approved-{Guid.NewGuid():N}";
        var command = new CreateRoleCommand(name, "Approved role", null, ["*"], true);

        if (failIntent)
        {
            await Should.ThrowAsync<InvalidOperationException>(() => useCase.ExecuteAsync(command));
        }
        else
        {
            Result<CreateRoleResponse> result = await useCase.ExecuteAsync(command);
            result.IsSuccess.ShouldBeTrue();
            // A middleware retry must retain the already-known committed outcome.
            await approval.RecordOutcomeAsync(false);
            await audit.DidNotReceive().LogAsync(Arg.Any<string>(), "AdministrativeApproval.MutationNotConfirmed",
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
            NSubstitute.Core.ICall[] diagnostics = logger.ReceivedCalls().Where(call => call.GetMethodInfo().Name == "Log").ToArray();
            diagnostics.Length.ShouldBe(2);
            foreach (NSubstitute.Core.ICall diagnostic in diagnostics)
            {
                diagnostic.GetArguments()[0].ShouldBe(LogLevel.Critical);
                diagnostic.GetArguments()[2]!.ToString()!.ShouldContain("Mutation succeeded: True");
                diagnostic.GetArguments()[2]!.ToString()!.ShouldNotContain("Simulated audit outage");
                diagnostic.GetArguments()[3].ShouldBeNull();
            }
            audit.LogAsync(Arg.Any<string>(), failingAction, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);
            await approval.RecordOutcomeAsync(false);
            await approval.RecordOutcomeAsync(false);
            await audit.Received(3).LogAsync(Arg.Any<string>(), "AdministrativeApproval.MutationSucceeded",
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        await using OpenIdentityStackDbContext read = fixture.CreateDbContext();
        OpenIdentityStack.Domain.Roles.Role? persisted = await read.Roles.SingleOrDefaultAsync(role => role.Name == name);
        (persisted is not null).ShouldBe(!failIntent);
        if (persisted is not null) { persisted.Permissions.ShouldContain("*"); }
    }
}
