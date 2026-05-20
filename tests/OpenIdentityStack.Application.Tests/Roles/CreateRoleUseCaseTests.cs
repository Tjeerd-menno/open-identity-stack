using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Roles.Commands;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Roles;

namespace OpenIdentityStack.Application.Tests.Roles;

public sealed class CreateRoleUseCaseTests
{
    private readonly IRoleRepository roleRepository;
    private readonly CreateRoleUseCase useCase;

    public CreateRoleUseCaseTests()
    {
        this.roleRepository = Substitute.For<IRoleRepository>();
        this.useCase = new CreateRoleUseCase(this.roleRepository);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidCommand_NormalizesRoleAndPersistsIt()
    {
        var command = new CreateRoleCommand(
            " Administrators ",
            " Administrators ",
            "Can administer the system",
            ["users:read", "USERS:WRITE"]);
        Role? addedRole = null;
        this.roleRepository.ExistsByNameAsync("administrators", Arg.Any<CancellationToken>())
            .Returns(false);
        this.roleRepository.AddAsync(Arg.Do<Role>(role => addedRole = role), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        Result<CreateRoleResponse> result = await this.useCase.ExecuteAsync(command);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Name.ShouldBe("administrators");
        result.Value.DisplayName.ShouldBe("Administrators");
        result.Value.Description.ShouldBe("Can administer the system");
        result.Value.IsActive.ShouldBeTrue();
        addedRole.ShouldNotBeNull();
        addedRole.Permissions.ShouldBe(["users:read", "users:write"]);
        await this.roleRepository.Received(1).AddAsync(addedRole, Arg.Any<CancellationToken>());
        await this.roleRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenRoleNameIsInvalid_ReturnsValidationErrorAndDoesNotCheckDuplicates()
    {
        var command = new CreateRoleCommand(" ", null, null, null);

        Result<CreateRoleResponse> result = await this.useCase.ExecuteAsync(command);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Validation.NameRequired");
        await this.roleRepository.DidNotReceive().ExistsByNameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await this.roleRepository.DidNotReceive().AddAsync(Arg.Any<Role>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenRoleNameExists_ReturnsConflictAndDoesNotPersist()
    {
        var command = new CreateRoleCommand("Admin", "Admin", null, null);
        this.roleRepository.ExistsByNameAsync("admin", Arg.Any<CancellationToken>())
            .Returns(true);

        Result<CreateRoleResponse> result = await this.useCase.ExecuteAsync(command);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Conflict.NameAlreadyExists");
        await this.roleRepository.DidNotReceive().AddAsync(Arg.Any<Role>(), Arg.Any<CancellationToken>());
        await this.roleRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithoutDisplayNameOrDescription_UsesRoleDefaults()
    {
        var command = new CreateRoleCommand("Support", null, null, null);

        Result<CreateRoleResponse> result = await this.useCase.ExecuteAsync(command);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Name.ShouldBe("support");
        result.Value.DisplayName.ShouldBe("Support");
        result.Value.Description.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task ExecuteAsync_PassesCancellationToken()
    {
        var command = new CreateRoleCommand("Support", null, null, null);
        using var cts = new CancellationTokenSource();

        await this.useCase.ExecuteAsync(command, cts.Token);

        await this.roleRepository.Received(1).ExistsByNameAsync("support", cts.Token);
        await this.roleRepository.Received(1).AddAsync(Arg.Any<Role>(), cts.Token);
        await this.roleRepository.Received(1).SaveChangesAsync(cts.Token);
    }
}
