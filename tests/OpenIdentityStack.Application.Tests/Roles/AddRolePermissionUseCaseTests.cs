using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Roles.Commands;
using OpenIdentityStack.Application.Roles.Queries;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Roles;

using SharedKernel;
namespace OpenIdentityStack.Application.Tests.Roles;

public sealed class AddRolePermissionUseCaseTests
{
    private readonly IRoleRepository roleRepository;
    private readonly IPermissionAssignmentValidator permissionAssignmentValidator;
    private readonly AddRolePermissionUseCase useCase;

    public AddRolePermissionUseCaseTests()
    {
        this.roleRepository = Substitute.For<IRoleRepository>();
        this.permissionAssignmentValidator = Substitute.For<IPermissionAssignmentValidator>();
        this.permissionAssignmentValidator
            .ValidateAssignableAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        this.useCase = new AddRolePermissionUseCase(this.roleRepository, this.permissionAssignmentValidator);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRoleNotFound_ReturnsNotFound()
    {
        var command = new AddRolePermissionCommand(RoleId.Create(), "app:resource:read");
        this.roleRepository.GetByIdAsync(command.RoleId, Arg.Any<CancellationToken>()).Returns((Role?)null);

        Result<RoleDto> result = await this.useCase.ExecuteAsync(command);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(RoleErrors.NotFound);
    }

    [Fact]
    public async Task ExecuteAsync_AddsPermissionAndPersists()
    {
        Role role = Role.Create("admin", "Admin role").Value;
        var command = new AddRolePermissionCommand(role.Id, "app:resource:read");
        this.roleRepository.GetByIdAsync(role.Id, Arg.Any<CancellationToken>()).Returns(role);

        Result<RoleDto> result = await this.useCase.ExecuteAsync(command);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Permissions.ShouldContain("app:resource:read");
        await this.roleRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenPermissionAlreadyExists_ReturnsConflictAndDoesNotPersist()
    {
        Role role = Role.Create("admin", "Admin role").Value;
        role.SetPermissions(["app:resource:read"]);
        var command = new AddRolePermissionCommand(role.Id, "app:resource:read");
        this.roleRepository.GetByIdAsync(role.Id, Arg.Any<CancellationToken>()).Returns(role);

        Result<RoleDto> result = await this.useCase.ExecuteAsync(command);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Conflict.PermissionAlreadyExists");
        await this.roleRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenValidationFails_ReturnsErrorAndDoesNotPersist()
    {
        Role role = Role.Create("admin", "Admin role").Value;
        var command = new AddRolePermissionCommand(role.Id, "users:*");
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
