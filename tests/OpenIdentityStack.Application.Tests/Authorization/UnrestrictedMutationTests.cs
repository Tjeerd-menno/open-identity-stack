using Microsoft.Extensions.Logging.Abstractions;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Authorization;
using OpenIdentityStack.Application.Groups.Commands;
using OpenIdentityStack.Application.Roles.Commands;
using OpenIdentityStack.Domain.Groups;
using OpenIdentityStack.Domain.Roles;
using OpenIdentityStack.Domain.Users;
using SharedKernel;

namespace OpenIdentityStack.Application.Tests.Authorization;

public sealed class UnrestrictedMutationTests
{
    [Theory]
    [InlineData("create")]
    [InlineData("update")]
    [InlineData("set")]
    [InlineData("add")]
    [InlineData("enable")]
    [InlineData("assign")]
    [InlineData("group-mapping")]
    [InlineData("group-member")]
    public async Task EveryUnrestrictedGrantPathRequiresApprovalBeforeMutation(string operation)
    {
        IRoleRepository roles = Substitute.For<IRoleRepository>();
        IGroupRepository groups = Substitute.For<IGroupRepository>();
        IUserRepository users = Substitute.For<IUserRepository>();
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        IAdministrativeApproval approval = Substitute.For<IAdministrativeApproval>();
        approval.RequireAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(DomainError.Forbidden("ApprovalDenied", "Denied")));
        IPermissionAssignmentValidator validator = Substitute.For<IPermissionAssignmentValidator>();
        validator.ValidateAssignableAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        Role role = Role.Create("unrestricted", "Unrestricted", null).Value;
        if (operation is "enable" or "assign" or "group-mapping" or "group-member") { role.SetPermissions(["*"]); }
        if (operation == "enable") { role.Disable(); }
        roles.GetByIdAsync(role.Id, Arg.Any<CancellationToken>()).Returns(role);
        roles.GetByNameAsync(role.Name, Arg.Any<CancellationToken>()).Returns(role);
        User user = User.CreateFederated("user@example.com", "User", clock).Value;
        users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        Group group = Group.Create("group", null, clock).Value;
        if (operation == "group-member") { group.AddMapping(MappingType.Role, role.Id.Value.ToString(), null, TokenTarget.Both, clock); }
        groups.GetByIdAsync(group.Id, Arg.Any<CancellationToken>()).Returns(group);
        var policy = new UnrestrictedGrantPolicy(roles);

        bool denied = operation switch
        {
            "create" => (await new CreateRoleUseCase(roles, validator, approval).ExecuteAsync(new CreateRoleCommand("new", null, null, ["*"], true))).IsFailure,
            "update" => (await new UpdateRoleUseCase(roles, validator, approval).ExecuteAsync(new UpdateRoleCommand(role.Id, "Changed", "Changed", ["*"], true))).IsFailure,
            "set" => (await new SetRolePermissionsUseCase(roles, validator, approval).ExecuteAsync(new SetRolePermissionsCommand(role.Id, ["*"], true))).IsFailure,
            "add" => (await new AddRolePermissionUseCase(roles, validator, approval).ExecuteAsync(new AddRolePermissionCommand(role.Id, "*", true))).IsFailure,
            "enable" => (await new EnableRoleUseCase(roles, approval).ExecuteAsync(new EnableRoleCommand(role.Id))).IsFailure,
            "assign" => (await new AssignRoleUseCase(users, roles, clock, Substitute.For<IAuditLog>(), NullLogger<AssignRoleUseCase>.Instance, approval).ExecuteAsync(new AssignRoleCommand(user.Id, role.Id, "actor"))).IsFailure,
            "group-mapping" => (await new AddGroupMappingUseCase(groups, clock, approval, policy).ExecuteAsync(new AddGroupMappingCommand(group.Id, MappingType.Role, role.Id.Value.ToString(), null, TokenTarget.Both))).IsFailure,
            "group-member" => (await new AddUserToGroupUseCase(groups, users, clock, approval, policy).ExecuteAsync(new AddUserToGroupCommand(group.Id, user.Id, user.Id))).IsFailure,
            _ => throw new InvalidOperationException(),
        };

        denied.ShouldBeTrue();
        await approval.Received(1).RequireAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
        await roles.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await groups.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        role.DisplayName.ShouldBe("Unrestricted");
        group.Memberships.ShouldBeEmpty();
    }
}
