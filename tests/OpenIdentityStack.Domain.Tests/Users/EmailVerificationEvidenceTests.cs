using OpenIdentityStack.Domain.Federation;
using OpenIdentityStack.Domain.Users;
using SharedKernel;

namespace OpenIdentityStack.Domain.Tests.Users;

public sealed class EmailVerificationEvidenceTests
{
    [Theory]
    [InlineData(false, true, "person@example.com", false)]
    [InlineData(true, false, "person@example.com", false)]
    [InlineData(true, true, "other@example.com", false)]
    [InlineData(true, true, "person@example.com", true)]
    public void ProviderEvidenceRequiresTrustPositiveAssertionAndCurrentAddress(bool trusted, bool asserted, string email, bool expected)
    {
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        User user = User.CreateFederated("person@example.com", "Person", clock).Value;
        UpstreamProvider provider = UpstreamProvider.Create("provider", "Provider", "https://issuer.example", "client").Value;
        provider.SetEmailVerificationTrust(trusted);

        user.RecordProviderEmailVerification(provider, "https://issuer.example", email, asserted, clock.UtcNow);

        user.EmailVerified.ShouldBe(expected);
        user.CanAuthenticate().ShouldBeTrue();
        if (expected)
        {
            user.EmailVerificationEvidence.Single().ProviderId.ShouldBe(provider.Id.Value);
            user.EmailVerificationEvidence.Single().Issuer.ShouldBe("https://issuer.example");
        }
    }

    [Fact]
    public void RecordingNewOrRepeatedEvidenceDoesNotChangeProviderPolicyVersion()
    {
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        User user = User.CreateFederated("person@example.com", "Person", clock).Value;
        UpstreamProvider provider = UpstreamProvider.Create("provider", "Provider", "https://issuer.example", "client").Value;
        provider.SetEmailVerificationTrust(true);
        Guid policyVersion = provider.EmailTrustVersion;
        user.RecordProviderEmailVerification(provider, "https://issuer.example", user.Email, true, clock.UtcNow);
        provider.EmailTrustVersion.ShouldBe(policyVersion);
        user.RecordProviderEmailVerification(provider, "https://issuer.example", user.Email, true, clock.UtcNow);
        provider.EmailTrustVersion.ShouldBe(policyVersion);
        user.EmailVerificationEvidence.Count.ShouldBe(1);
    }

    [Fact]
    public void WithdrawalPreservesIndependentVerificationAndProviderProvenance()
    {
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        User user = User.CreateLocal("person@example.com", "Person", "hash", clock).Value;
        user.VerifyEmail(clock).IsSuccess.ShouldBeTrue();
        UpstreamProvider provider = UpstreamProvider.Create("provider", "Provider", "https://issuer.example", "client").Value;
        provider.SetEmailVerificationTrust(true);
        user.RecordProviderEmailVerification(provider, "https://issuer.example", user.Email, true, clock.UtcNow);

        user.WithdrawProviderEmailVerification(provider.Id.Value, clock.UtcNow);

        user.EmailVerified.ShouldBeTrue();
        user.EmailVerificationEvidence.Count.ShouldBe(2);
        user.EmailVerificationEvidence.Single(e => e.ProviderId.HasValue).WithdrawnAt.ShouldNotBeNull();
    }
}
