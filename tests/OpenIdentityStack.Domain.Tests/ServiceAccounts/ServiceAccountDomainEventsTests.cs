using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.ServiceAccounts;

using SharedKernel;
namespace OpenIdentityStack.Domain.Tests.ServiceAccounts;

public sealed class ServiceAccountDomainEventsTests
{
    [Fact]
    public void ServiceAccountCreated_SetsProperties()
    {
        DateTimeOffset occurredAt = DateTimeOffset.UtcNow;
        var serviceAccountId = ServiceAccountId.Create();

        var evt = new ServiceAccountDomainEvents.ServiceAccountCreated(
            serviceAccountId,
            "client-id",
            "Service Account",
            occurredAt);

        evt.ServiceAccountId.ShouldBe(serviceAccountId);
        evt.ClientId.ShouldBe("client-id");
        evt.DisplayName.ShouldBe("Service Account");
        evt.OccurredAt.ShouldBe(occurredAt);
        evt.EventId.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void ServiceAccountDisabled_SetsProperties()
    {
        DateTimeOffset occurredAt = DateTimeOffset.UtcNow;
        var serviceAccountId = ServiceAccountId.Create();

        var evt = new ServiceAccountDomainEvents.ServiceAccountDisabled(serviceAccountId, occurredAt);

        evt.ServiceAccountId.ShouldBe(serviceAccountId);
        evt.OccurredAt.ShouldBe(occurredAt);
        evt.EventId.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void ServiceAccountEnabled_SetsProperties()
    {
        DateTimeOffset occurredAt = DateTimeOffset.UtcNow;
        var serviceAccountId = ServiceAccountId.Create();

        var evt = new ServiceAccountDomainEvents.ServiceAccountEnabled(serviceAccountId, occurredAt);

        evt.ServiceAccountId.ShouldBe(serviceAccountId);
        evt.OccurredAt.ShouldBe(occurredAt);
        evt.EventId.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void CredentialAdded_SetsProperties()
    {
        DateTimeOffset occurredAt = DateTimeOffset.UtcNow;
        var serviceAccountId = ServiceAccountId.Create();
        var credentialId = Guid.NewGuid();
        DateTimeOffset expiresAt = occurredAt.AddDays(30);

        var evt = new ServiceAccountDomainEvents.CredentialAdded(
            serviceAccountId,
            credentialId,
            "rotation",
            expiresAt,
            occurredAt);

        evt.ServiceAccountId.ShouldBe(serviceAccountId);
        evt.CredentialId.ShouldBe(credentialId);
        evt.Description.ShouldBe("rotation");
        evt.ExpiresAt.ShouldBe(expiresAt);
        evt.OccurredAt.ShouldBe(occurredAt);
        evt.EventId.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void CredentialRevoked_SetsProperties()
    {
        DateTimeOffset occurredAt = DateTimeOffset.UtcNow;
        var serviceAccountId = ServiceAccountId.Create();
        var credentialId = Guid.NewGuid();

        var evt = new ServiceAccountDomainEvents.CredentialRevoked(serviceAccountId, credentialId, occurredAt);

        evt.ServiceAccountId.ShouldBe(serviceAccountId);
        evt.CredentialId.ShouldBe(credentialId);
        evt.OccurredAt.ShouldBe(occurredAt);
        evt.EventId.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void CertificateAdded_SetsProperties()
    {
        DateTimeOffset occurredAt = DateTimeOffset.UtcNow;
        var serviceAccountId = ServiceAccountId.Create();
        var certificateId = Guid.NewGuid();
        DateTimeOffset expiresAt = occurredAt.AddDays(90);

        var evt = new ServiceAccountDomainEvents.CertificateAdded(
            serviceAccountId,
            certificateId,
            "thumbprint",
            "CN=test",
            expiresAt,
            occurredAt);

        evt.ServiceAccountId.ShouldBe(serviceAccountId);
        evt.CertificateId.ShouldBe(certificateId);
        evt.Thumbprint.ShouldBe("thumbprint");
        evt.Subject.ShouldBe("CN=test");
        evt.ExpiresAt.ShouldBe(expiresAt);
        evt.OccurredAt.ShouldBe(occurredAt);
        evt.EventId.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void CertificateRevoked_SetsProperties()
    {
        DateTimeOffset occurredAt = DateTimeOffset.UtcNow;
        var serviceAccountId = ServiceAccountId.Create();
        var certificateId = Guid.NewGuid();

        var evt = new ServiceAccountDomainEvents.CertificateRevoked(serviceAccountId, certificateId, occurredAt);

        evt.ServiceAccountId.ShouldBe(serviceAccountId);
        evt.CertificateId.ShouldBe(certificateId);
        evt.OccurredAt.ShouldBe(occurredAt);
        evt.EventId.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void ServiceAccountUpdated_SetsProperties()
    {
        DateTimeOffset occurredAt = DateTimeOffset.UtcNow;
        var serviceAccountId = ServiceAccountId.Create();

        var evt = new ServiceAccountDomainEvents.ServiceAccountUpdated(serviceAccountId, occurredAt);

        evt.ServiceAccountId.ShouldBe(serviceAccountId);
        evt.OccurredAt.ShouldBe(occurredAt);
        evt.EventId.ShouldNotBe(Guid.Empty);
    }
}
