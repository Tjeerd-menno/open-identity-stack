using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Roles.Commands;
using OpenIdentityStack.Application.Roles.Queries;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Roles;

using SharedKernel;
namespace OpenIdentityStack.Application.Tests.Roles;

public sealed class RemoveRolePermissionUseCaseTests
{
    private readonly IRoleRepository roleRepository;
    private readonly RemoveRolePermissionUseCase useCase;

    public RemoveRolePermissionUseCaseTests()
    {
        this.roleRepository = Substitute.For<IRoleRepository>();
        this.useCase = new RemoveRolePermissionUseCase(this.roleRepository);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRoleNotFound_ReturnsNotFound()
    {
        var command = new RemoveRolePermissionCommand(RoleId.Create(), "app:resource:read");
        this.roleRepository.GetByIdAsync(command.RoleId, Arg.Any<CancellationToken>()).Returns((Role?)null);

        Result<RoleDto> result = await this.useCase.ExecuteAsync(command);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(RoleErrors.NotFound);
    }

    [Fact]
    public async Task ExecuteAsync_WhenPermissionNotOnRole_ReturnsNotFoundAndDoesNotPersist()
    {
        Role role = Role.Create("admin", "Admin role").Value;
        var command = new RemoveRolePermissionCommand(role.Id, "app:resource:read");
        this.roleRepository.GetByIdAsync(role.Id, Arg.Any<CancellationToken>()).Returns(role);

        Result<RoleDto> result = await this.useCase.ExecuteAsync(command);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("NotFound.PermissionNotFound");
        await this.roleRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenPermissionExists_RemovesAndPersists()
    {
        Role role = Role.Create("admin", "Admin role").Value;
        role.SetPermissions(["app:resource:read", "app:resource:write"]);
        var command = new RemoveRolePermissionCommand(role.Id, "app:resource:read");
        this.roleRepository.GetByIdAsync(role.Id, Arg.Any<CancellationToken>()).Returns(role);

        Result<RoleDto> result = await this.useCase.ExecuteAsync(command);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Permissions.ShouldBe(["app:resource:write"]);
        await this.roleRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
