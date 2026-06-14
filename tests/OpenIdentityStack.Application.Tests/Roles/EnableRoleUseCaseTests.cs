using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Roles.Commands;
using OpenIdentityStack.Application.Roles.Queries;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Roles;

using SharedKernel;
namespace OpenIdentityStack.Application.Tests.Roles;

public sealed class EnableRoleUseCaseTests
{
    private readonly IRoleRepository roleRepository;
    private readonly EnableRoleUseCase useCase;

    public EnableRoleUseCaseTests()
    {
        this.roleRepository = Substitute.For<IRoleRepository>();
        this.useCase = new EnableRoleUseCase(this.roleRepository);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRoleNotFound_ReturnsNotFound()
    {
        var command = new EnableRoleCommand(RoleId.Create());
        this.roleRepository.GetByIdAsync(command.RoleId, Arg.Any<CancellationToken>()).Returns((Role?)null);

        Result<RoleDto> result = await this.useCase.ExecuteAsync(command);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(RoleErrors.NotFound);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAlreadyActive_ReturnsDomainErrorAndDoesNotPersist()
    {
        Role role = Role.Create("custom", "Custom role").Value;
        var command = new EnableRoleCommand(role.Id);
        this.roleRepository.GetByIdAsync(role.Id, Arg.Any<CancellationToken>()).Returns(role);

        Result<RoleDto> result = await this.useCase.ExecuteAsync(command);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Validation.AlreadyActive");
        await this.roleRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenDisabledRole_EnablesAndPersists()
    {
        Role role = Role.Create("custom", "Custom role").Value;
        role.Disable();
        var command = new EnableRoleCommand(role.Id);
        this.roleRepository.GetByIdAsync(role.Id, Arg.Any<CancellationToken>()).Returns(role);

        Result<RoleDto> result = await this.useCase.ExecuteAsync(command);

        result.IsSuccess.ShouldBeTrue();
        result.Value.IsActive.ShouldBeTrue();
        await this.roleRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
