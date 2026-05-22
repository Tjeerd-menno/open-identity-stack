using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.ServicePermissions.Commands;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.ServicePermissions;

namespace OpenIdentityStack.Application.Tests.ServicePermissions;

public sealed class RegisterServiceUseCaseTests
{
    private readonly IServicePermissionRegistryRepository repository;
    private readonly IServicePermissionAuthorizationService authorizationService;
    private readonly IServicePermissionAuditWriter auditWriter;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly RegisterServiceUseCase useCase;

    public RegisterServiceUseCaseTests()
    {
        this.repository = Substitute.For<IServicePermissionRegistryRepository>();
        this.authorizationService = Substitute.For<IServicePermissionAuthorizationService>();
        this.auditWriter = Substitute.For<IServicePermissionAuditWriter>();
        this.dateTimeProvider = Substitute.For<IDateTimeProvider>();
        this.dateTimeProvider.UtcNow.Returns(new DateTimeOffset(2026, 1, 18, 12, 0, 0, TimeSpan.Zero));
        
        // By default, allow all operations for existing tests
        this.authorizationService.CanRegisterServiceAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        
        this.useCase = new RegisterServiceUseCase(this.repository, this.authorizationService, this.auditWriter, this.dateTimeProvider);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidCommand_RegistersService()
    {
        RegisterServiceCommand command = ValidCommand();
        this.repository.ExistsByIdentifierAsync("orders-api", Arg.Any<CancellationToken>()).Returns(false);

        Result<RegisterServiceResult> result = await this.useCase.ExecuteAsync(command);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ServiceIdentifier.ShouldBe("orders-api");
        result.Value.PermissionsRegistered.ShouldBe(1);
        await this.repository.Received(1).AddAsync(Arg.Is<RegisteredService>(s => s.ServiceIdentifier == "orders-api"), Arg.Any<CancellationToken>());
        await this.repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenIdentifierExists_ReturnsConflict()
    {
        RegisterServiceCommand command = ValidCommand();
        this.repository.ExistsByIdentifierAsync("orders-api", Arg.Any<CancellationToken>()).Returns(true);

        Result<RegisterServiceResult> result = await this.useCase.ExecuteAsync(command);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Conflict.RegisterService.IdentifierConflict");
        await this.repository.DidNotReceive().AddAsync(Arg.Any<RegisteredService>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithReservedIdentifier_ReturnsConflictBeforeRepositoryLookup()
    {
        RegisterServiceCommand command = ValidCommand(serviceIdentifier: "users");

        Result<RegisterServiceResult> result = await this.useCase.ExecuteAsync(command);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Conflict.RegisterService.IdentifierReserved");
        await this.repository.DidNotReceive().ExistsByIdentifierAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithoutPermissions_ReturnsValidationError()
    {
        RegisterServiceCommand command = ValidCommand(permissions: []);

        Result<RegisterServiceResult> result = await this.useCase.ExecuteAsync(command);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Validation.RegisterService.PermissionsRequired");
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidOwnerType_ReturnsValidationErrorBeforeRepositoryLookup()
    {
        RegisterServiceCommand command = ValidCommand(ownerType: "invalid-owner");

        Result<RegisterServiceResult> result = await this.useCase.ExecuteAsync(command);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Validation.RegisterService.OwnerTypeInvalid");
        await this.repository.DidNotReceive().ExistsByIdentifierAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private static RegisterServiceCommand ValidCommand(
        string serviceIdentifier = "orders-api",
        string ownerType = "User",
        IReadOnlyList<RegisterServicePermissionInput>? permissions = null)
    {
        return new RegisterServiceCommand(
            serviceIdentifier,
            "Orders API",
            "Manages orders",
            "owner-1",
            ownerType,
            "actor-1",
            permissions ?? [new RegisterServicePermissionInput("read-orders", "Read orders")]);
    }
}
