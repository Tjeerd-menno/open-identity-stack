using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Roles.Commands;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Roles;

using SharedKernel;
namespace OpenIdentityStack.Application.Tests.Roles;

public sealed class DeleteRoleUseCaseTests
{
    private readonly IRoleRepository roleRepository;
    private readonly DeleteRoleUseCase useCase;

    public DeleteRoleUseCaseTests()
    {
        this.roleRepository = Substitute.For<IRoleRepository>();
        this.useCase = new DeleteRoleUseCase(this.roleRepository);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRoleNotFound_ReturnsNotFound()
    {
        var command = new DeleteRoleCommand(RoleId.Create());
        this.roleRepository.GetByIdAsync(command.RoleId, Arg.Any<CancellationToken>()).Returns((Role?)null);

        Result result = await this.useCase.ExecuteAsync(command);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(RoleErrors.NotFound);
        this.roleRepository.DidNotReceive().Remove(Arg.Any<Role>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenSystemRole_ReturnsValidationErrorAndDoesNotRemove()
    {
        Role role = Role.CreateSystemRole("super-admin", "Super Admin", "System role").Value;
        var command = new DeleteRoleCommand(role.Id);
        this.roleRepository.GetByIdAsync(role.Id, Arg.Any<CancellationToken>()).Returns(role);

        Result result = await this.useCase.ExecuteAsync(command);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(RoleErrors.CannotDeleteSystemRole);
        this.roleRepository.DidNotReceive().Remove(Arg.Any<Role>());
        await this.roleRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenRoleExists_RemovesAndPersists()
    {
        Role role = Role.Create("custom", "Custom role").Value;
        var command = new DeleteRoleCommand(role.Id);
        this.roleRepository.GetByIdAsync(role.Id, Arg.Any<CancellationToken>()).Returns(role);

        Result result = await this.useCase.ExecuteAsync(command);

        result.IsSuccess.ShouldBeTrue();
        this.roleRepository.Received(1).Remove(role);
        await this.roleRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
