using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Roles.Commands;
using OpenIdentityStack.Application.Roles.Queries;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Roles;

using SharedKernel;
namespace OpenIdentityStack.Application.Tests.Roles;

public sealed class UpdateRoleUseCaseTests
{
    private readonly IRoleRepository roleRepository;
    private readonly IPermissionAssignmentValidator permissionAssignmentValidator;
    private readonly UpdateRoleUseCase useCase;

    public UpdateRoleUseCaseTests()
    {
        this.roleRepository = Substitute.For<IRoleRepository>();
        this.permissionAssignmentValidator = Substitute.For<IPermissionAssignmentValidator>();
        this.permissionAssignmentValidator
            .ValidateAssignableAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        this.useCase = new UpdateRoleUseCase(this.roleRepository, this.permissionAssignmentValidator);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRoleNotFound_ReturnsNotFound()
    {
        var command = new UpdateRoleCommand(RoleId.Create(), "Display", "Desc", null);
        this.roleRepository.GetByIdAsync(command.RoleId, Arg.Any<CancellationToken>()).Returns((Role?)null);

        Result<RoleDto> result = await this.useCase.ExecuteAsync(command);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(RoleErrors.NotFound);
        await this.roleRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_UpdatesDescriptionAndDisplayNameAndPersists()
    {
        Role role = Role.Create("admin", "Admin role").Value;
        var command = new UpdateRoleCommand(role.Id, "New Display", "New Desc", null);
        this.roleRepository.GetByIdAsync(role.Id, Arg.Any<CancellationToken>()).Returns(role);

        Result<RoleDto> result = await this.useCase.ExecuteAsync(command);

        result.IsSuccess.ShouldBeTrue();
        result.Value.DisplayName.ShouldBe("New Display");
        result.Value.Description.ShouldBe("New Desc");
        await this.roleRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenDisplayNameNull_LeavesDisplayNameUnchanged()
    {
        Role role = Role.Create("admin", "Admin", "Admin role").Value;
        string originalDisplayName = role.DisplayName;
        var command = new UpdateRoleCommand(role.Id, null, "New Desc", null);
        this.roleRepository.GetByIdAsync(role.Id, Arg.Any<CancellationToken>()).Returns(role);

        Result<RoleDto> result = await this.useCase.ExecuteAsync(command);

        result.IsSuccess.ShouldBeTrue();
        result.Value.DisplayName.ShouldBe(originalDisplayName);
    }

    [Fact]
    public async Task ExecuteAsync_OnlyValidatesNewPermissions()
    {
        Role role = Role.Create("admin", "Admin role").Value;
        role.SetPermissions(["app:resource:read"]);
        var command = new UpdateRoleCommand(
            role.Id,
            null,
            null,
            ["app:resource:read", "app:resource:write"]);
        this.roleRepository.GetByIdAsync(role.Id, Arg.Any<CancellationToken>()).Returns(role);

        Result<RoleDto> result = await this.useCase.ExecuteAsync(command);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Permissions.ShouldBe(["app:resource:read", "app:resource:write"]);
        await this.permissionAssignmentValidator.Received(1).ValidateAssignableAsync(
            Arg.Is<IEnumerable<string>>(p => p.Count() == 1 && p.Single() == "app:resource:write"),
            false,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenPermissionValidationFails_ReturnsErrorAndDoesNotPersist()
    {
        Role role = Role.Create("admin", "Admin role").Value;
        var command = new UpdateRoleCommand(role.Id, null, null, ["users:*"]);
        this.roleRepository.GetByIdAsync(role.Id, Arg.Any<CancellationToken>()).Returns(role);
        this.permissionAssignmentValidator
            .ValidateAssignableAsync(Arg.Any<IEnumerable<string>>(), false, Arg.Any<CancellationToken>())
            .Returns(DomainError.Conflict("RolePermissions.BroadGrantAcknowledgementRequired", "ack required"));

        Result<RoleDto> result = await this.useCase.ExecuteAsync(command);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldStartWith("Conflict.");
        await this.roleRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
