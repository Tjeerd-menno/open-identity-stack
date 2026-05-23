using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.ApplicationPermissions.Commands;
using OpenIdentityStack.Application.ApplicationPermissions.Dtos;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.ApplicationPermissions;
using SharedKernel;

namespace OpenIdentityStack.Application.Tests.ApplicationPermissions;

public sealed class ApplicationPermissionMaintenanceUseCasesTests
{
    private readonly IApplicationPermissionRegistryRepository repository;
    private readonly IApplicationPermissionAuthorizationService authorizationService;
    private readonly IRolePermissionDependencyReader dependencyReader;
    private readonly IApplicationPermissionAuditWriter auditWriter;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly ApplicationPermissionMaintenanceUseCases useCases;

    public ApplicationPermissionMaintenanceUseCasesTests()
    {
        this.repository = Substitute.For<IApplicationPermissionRegistryRepository>();
        this.authorizationService = Substitute.For<IApplicationPermissionAuthorizationService>();
        this.dependencyReader = Substitute.For<IRolePermissionDependencyReader>();
        this.auditWriter = Substitute.For<IApplicationPermissionAuditWriter>();
        this.dateTimeProvider = Substitute.For<IDateTimeProvider>();
        this.dateTimeProvider.UtcNow.Returns(new DateTimeOffset(2026, 5, 20, 12, 0, 0, TimeSpan.Zero));

        this.useCases = new ApplicationPermissionMaintenanceUseCases(
            this.repository,
            this.authorizationService,
            this.dependencyReader,
            this.auditWriter,
            this.dateTimeProvider);
    }

    [Fact]
    public async Task UpdateRegisteredApplication_WhenActorNotAuthorized_ReturnsForbidden()
    {
        // Arrange
        var command = new UpdateRegisteredApplicationCommand(
            Guid.NewGuid(),
            "actor-123",
            "Updated Application",
            "Updated description",
            1);

        RegisteredApplication application = CreateTestApplication();
        this.repository.GetByIdAsync(new RegisteredApplicationId(command.ApplicationId), Arg.Any<CancellationToken>()).Returns(application);
        this.authorizationService.CanManageApplicationAsync("actor-123", application.OwnerId, Arg.Any<CancellationToken>()).Returns(false);

        // Act
        Result<RegisteredApplicationDto> result = await this.useCases.ExecuteAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldContain("Forbidden");
        await this.repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private static RegisteredApplication CreateTestApplication()
    {
        var permissions = new List<(string Key, string DisplayName, string? Description, string? Category)>
        {
            ("read", "Read", "Read permission", null)
        };

        Result<RegisteredApplication> result = RegisteredApplication.Register(
            "test-application",
            "Test Application",
            "Test description",
            "owner-123",
            OwnerType.User,
            permissions,
            "creator-123",
            new TestDateTimeProvider());

        return result.Value;
    }

    private sealed class TestDateTimeProvider : IDateTimeProvider
    {
        public DateTimeOffset UtcNow => new(2026, 5, 20, 12, 0, 0, TimeSpan.Zero);
        public DateTimeOffset Now => new(2026, 5, 20, 12, 0, 0, TimeSpan.Zero);
    }
}
