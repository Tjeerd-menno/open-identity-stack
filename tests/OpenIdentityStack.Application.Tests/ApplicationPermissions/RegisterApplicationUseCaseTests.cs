using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.ApplicationPermissions.Commands;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.ApplicationPermissions;

namespace OpenIdentityStack.Application.Tests.ApplicationPermissions;

public sealed class RegisterApplicationUseCaseTests
{
    private readonly IApplicationPermissionRegistryRepository repository;
    private readonly IApplicationPermissionAuthorizationService authorizationService;
    private readonly IApplicationPermissionAuditWriter auditWriter;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly RegisterApplicationUseCase useCase;

    public RegisterApplicationUseCaseTests()
    {
        this.repository = Substitute.For<IApplicationPermissionRegistryRepository>();
        this.authorizationService = Substitute.For<IApplicationPermissionAuthorizationService>();
        this.auditWriter = Substitute.For<IApplicationPermissionAuditWriter>();
        this.dateTimeProvider = Substitute.For<IDateTimeProvider>();
        this.dateTimeProvider.UtcNow.Returns(new DateTimeOffset(2026, 1, 18, 12, 0, 0, TimeSpan.Zero));
        
        // By default, allow all operations for existing tests
        this.authorizationService.CanRegisterApplicationAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        
        this.useCase = new RegisterApplicationUseCase(this.repository, this.authorizationService, this.auditWriter, this.dateTimeProvider);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidCommand_RegistersApplication()
    {
        RegisterApplicationCommand command = ValidCommand();
        this.repository.ExistsByIdentifierAsync("orders-api", Arg.Any<CancellationToken>()).Returns(false);

        Result<RegisterApplicationResult> result = await this.useCase.ExecuteAsync(command);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ApplicationIdentifier.ShouldBe("orders-api");
        result.Value.PermissionsRegistered.ShouldBe(1);
        await this.repository.Received(1).AddAsync(Arg.Is<RegisteredApplication>(s => s.ApplicationIdentifier == "orders-api"), Arg.Any<CancellationToken>());
        await this.repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithPermissionManifest_RegistersApplicationManifest()
    {
        var command = new RegisterPermissionManifestCommand(
            new PermissionManifestApplication("patient-api", "Patient API", "1.0.0"),
            "actor-1",
            [
                new PermissionManifestEntry("patient:read", "Allows reading patient data", "Patients"),
                new PermissionManifestEntry("patient:write", "Allows modifying patient data", "Patients"),
            ]);
        this.repository.ExistsByIdentifierAsync("patient-api", Arg.Any<CancellationToken>()).Returns(false);

        Result<RegisterApplicationResult> result = await this.useCase.ExecuteAsync(command);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ApplicationIdentifier.ShouldBe("patient-api");
        result.Value.PermissionsRegistered.ShouldBe(2);
        await this.repository.Received(1).AddAsync(
            Arg.Is<RegisteredApplication>(application =>
                application.ApplicationIdentifier == "patient-api"
                && application.DisplayName == "Patient API"
                && application.Description == "1.0.0"
                && application.OwnerId == "actor-1"
                && application.Permissions[0].FullPermissionKey == "patient-api:patient:read"
                && application.Permissions[0].DisplayName == "patient:read"
                && application.Permissions[0].Description == "Allows reading patient data"
                && application.Permissions[0].Category == "Patients"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenIdentifierExists_ReturnsConflict()
    {
        RegisterApplicationCommand command = ValidCommand();
        this.repository.ExistsByIdentifierAsync("orders-api", Arg.Any<CancellationToken>()).Returns(true);

        Result<RegisterApplicationResult> result = await this.useCase.ExecuteAsync(command);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Conflict.RegisterApplication.IdentifierConflict");
        await this.repository.DidNotReceive().AddAsync(Arg.Any<RegisteredApplication>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithReservedIdentifier_ReturnsConflictBeforeRepositoryLookup()
    {
        RegisterApplicationCommand command = ValidCommand(applicationIdentifier: "users");

        Result<RegisterApplicationResult> result = await this.useCase.ExecuteAsync(command);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Conflict.RegisterApplication.IdentifierReserved");
        await this.repository.DidNotReceive().ExistsByIdentifierAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithoutPermissions_ReturnsValidationError()
    {
        RegisterApplicationCommand command = ValidCommand(permissions: []);

        Result<RegisterApplicationResult> result = await this.useCase.ExecuteAsync(command);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Validation.RegisterApplication.PermissionsRequired");
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidOwnerType_ReturnsValidationErrorBeforeRepositoryLookup()
    {
        RegisterApplicationCommand command = ValidCommand(ownerType: "invalid-owner");

        Result<RegisterApplicationResult> result = await this.useCase.ExecuteAsync(command);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Validation.RegisterApplication.OwnerTypeInvalid");
        await this.repository.DidNotReceive().ExistsByIdentifierAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private static RegisterApplicationCommand ValidCommand(
        string applicationIdentifier = "orders-api",
        string ownerType = "User",
        IReadOnlyList<RegisterApplicationPermissionInput>? permissions = null)
    {
        return new RegisterApplicationCommand(
            applicationIdentifier,
            "Orders API",
            "Manages orders",
            "owner-1",
            ownerType,
            "actor-1",
            permissions ?? [new RegisterApplicationPermissionInput("order:read", "Read orders")]);
    }
}
