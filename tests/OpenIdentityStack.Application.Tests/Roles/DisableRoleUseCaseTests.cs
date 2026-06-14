using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Roles.Commands;
using OpenIdentityStack.Application.Roles.Queries;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Roles;

using SharedKernel;
namespace OpenIdentityStack.Application.Tests.Roles;

public sealed class DisableRoleUseCaseTests
{
    private readonly IRoleRepository roleRepository;
    private readonly DisableRoleUseCase useCase;

    public DisableRoleUseCaseTests()
    {
        this.roleRepository = Substitute.For<IRoleRepository>();
        this.useCase = new DisableRoleUseCase(this.roleRepository);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRoleNotFound_ReturnsNotFound()
    {
        var command = new DisableRoleCommand(RoleId.Create());
        this.roleRepository.GetByIdAsync(command.RoleId, Arg.Any<CancellationToken>()).Returns((Role?)null);

        Result<RoleDto> result = await this.useCase.ExecuteAsync(command);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(RoleErrors.NotFound);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSystemRole_ReturnsDomainErrorAndDoesNotPersist()
    {
        Role role = Role.CreateSystemRole("super-admin", "Super Admin", "System role").Value;
        var command = new DisableRoleCommand(role.Id);
        this.roleRepository.GetByIdAsync(role.Id, Arg.Any<CancellationToken>()).Returns(role);

        Result<RoleDto> result = await this.useCase.ExecuteAsync(command);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Validation.CannotDisableSystemRole");
        await this.roleRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenActiveRole_DisablesAndPersists()
    {
        Role role = Role.Create("custom", "Custom role").Value;
        var command = new DisableRoleCommand(role.Id);
        this.roleRepository.GetByIdAsync(role.Id, Arg.Any<CancellationToken>()).Returns(role);

        Result<RoleDto> result = await this.useCase.ExecuteAsync(command);

        result.IsSuccess.ShouldBeTrue();
        result.Value.IsActive.ShouldBeFalse();
        await this.roleRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
