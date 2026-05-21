using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.ServicePermissions.Commands;
using OpenIdentityStack.Application.ServicePermissions.Dtos;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.ServicePermissions;
using SharedKernel;

namespace OpenIdentityStack.Application.Tests.ServicePermissions;

public sealed class ServicePermissionMaintenanceUseCasesTests
{
    private readonly IServicePermissionRegistryRepository repository;
    private readonly IServicePermissionAuthorizationService authorizationService;
    private readonly IRolePermissionDependencyReader dependencyReader;
    private readonly IServicePermissionAuditWriter auditWriter;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly ServicePermissionMaintenanceUseCases useCases;

    public ServicePermissionMaintenanceUseCasesTests()
    {
        this.repository = Substitute.For<IServicePermissionRegistryRepository>();
        this.authorizationService = Substitute.For<IServicePermissionAuthorizationService>();
        this.dependencyReader = Substitute.For<IRolePermissionDependencyReader>();
        this.auditWriter = Substitute.For<IServicePermissionAuditWriter>();
        this.dateTimeProvider = Substitute.For<IDateTimeProvider>();
        this.dateTimeProvider.UtcNow.Returns(new DateTimeOffset(2026, 5, 20, 12, 0, 0, TimeSpan.Zero));

        this.useCases = new ServicePermissionMaintenanceUseCases(
            this.repository,
            this.authorizationService,
            this.dependencyReader,
            this.auditWriter,
            this.dateTimeProvider);
    }

    [Fact]
    public async Task UpdateRegisteredService_WhenActorNotAuthorized_ReturnsForbidden()
    {
        // Arrange
        var command = new UpdateRegisteredServiceCommand(
            Guid.NewGuid(),
            "actor-123",
            "Updated Service",
            "Updated description",
            1);

        RegisteredService service = CreateTestService();
        this.repository.GetByIdAsync(new RegisteredServiceId(command.ServiceId), Arg.Any<CancellationToken>()).Returns(service);
        this.authorizationService.CanManageServiceAsync("actor-123", service.OwnerId, Arg.Any<CancellationToken>()).Returns(false);

        // Act
        Result<RegisteredServiceDto> result = await this.useCases.ExecuteAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldContain("Forbidden");
        await this.repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private static RegisteredService CreateTestService()
    {
        var permissions = new List<(string Key, string DisplayName, string? Description, string? IntendedUse, string? DocUrl)>
        {
            ("read", "Read", "Read permission", null, null)
        };

        Result<RegisteredService> result = RegisteredService.Register(
            "test-service",
            "Test Service",
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
