using OpenIdentityStack.Domain.ServicePermissions;

namespace OpenIdentityStack.Domain.Tests.ServicePermissions;

public sealed class DelegatedMaintainerTests
{
    private readonly RegisteredServiceId serviceId = RegisteredServiceId.Create();
    private readonly DateTimeOffset now = new(2026, 1, 20, 9, 30, 0, TimeSpan.Zero);

    [Fact]
    public void Create_WithValidInputs_SetsAllProperties()
    {
        var dateTimeProvider = new TestDateTimeProvider(this.now);

        DelegatedMaintainer maintainer = DelegatedMaintainer.Create(
            this.serviceId,
            "principal-1",
            OwnerType.User,
            "admin-1",
            dateTimeProvider);

        maintainer.Id.ShouldNotBe(DelegatedMaintainerId.Empty);
        maintainer.RegisteredServiceId.ShouldBe(this.serviceId);
        maintainer.PrincipalId.ShouldBe("principal-1");
        maintainer.PrincipalType.ShouldBe(OwnerType.User);
        maintainer.GrantedBy.ShouldBe("admin-1");
        maintainer.GrantedAt.ShouldBe(this.now);
        maintainer.CreatedAt.ShouldBe(this.now);
    }

    [Fact]
    public void Create_CalledTwice_GeneratesUniqueIds()
    {
        var dateTimeProvider = new TestDateTimeProvider(this.now);

        DelegatedMaintainer first = DelegatedMaintainer.Create(
            this.serviceId,
            "principal-1",
            OwnerType.Group,
            "admin-1",
            dateTimeProvider);
        DelegatedMaintainer second = DelegatedMaintainer.Create(
            this.serviceId,
            "principal-1",
            OwnerType.Group,
            "admin-1",
            dateTimeProvider);

        first.Id.ShouldNotBe(second.Id);
    }

    private sealed class TestDateTimeProvider(DateTimeOffset now) : IDateTimeProvider
    {
        public DateTimeOffset UtcNow => now;

        public DateTimeOffset Now => now;
    }
}
