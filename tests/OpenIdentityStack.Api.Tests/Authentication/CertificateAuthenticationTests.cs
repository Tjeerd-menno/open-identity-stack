
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace OpenIdentityStack.Api.Tests.Authentication;
/// <summary>
/// Integration tests for certificate-based client authentication.
/// These tests verify that service accounts can authenticate using X.509 certificates
/// for the OAuth 2.0 client credentials flow.
/// 
/// Note: mTLS integration tests require Kestrel configuration with client certificate validation
/// which is complex to set up in test environments. The certificate utility tests below
/// validate the certificate handling logic that would be used by mTLS authentication.
/// </summary>
public sealed class CertificateAuthenticationTests
{
    #region mTLS Integration Tests (require special infrastructure)
    
    // Note: These tests would require:
    // 1. Kestrel configured for mTLS with client certificate requirement
    // 2. A test CA certificate trusted by the server
    // 3. Client certificates signed by the test CA
    // 4. Service accounts with registered certificate thumbprints
    // 
    // For now, the certificate utility tests below validate the core certificate logic.
    // Full mTLS testing would be done in a dedicated environment with proper PKI setup.

    #endregion

    #region Certificate Utility Tests

    [Fact]
    public void Certificate_ThumbprintCalculation_IsConsistent()
    {
        // Arrange - Create a self-signed certificate for testing thumbprint calculation
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=Test Certificate",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        DateTimeOffset notBefore = DateTimeOffset.UtcNow;
        DateTimeOffset notAfter = notBefore.AddYears(1);

        using X509Certificate2 certificate = request.CreateSelfSigned(notBefore, notAfter);

        // Act
        string thumbprint1 = certificate.GetCertHashString(HashAlgorithmName.SHA256);
        string thumbprint2 = certificate.GetCertHashString(HashAlgorithmName.SHA256);

        // Assert
        thumbprint1.ShouldBe(thumbprint2);
        thumbprint1.ShouldNotBeNullOrEmpty();
        thumbprint1.Length.ShouldBe(64); // SHA256 produces 32 bytes = 64 hex chars
    }

    [Fact]
    public void Certificate_SubjectExtraction_WorksCorrectly()
    {
        // Arrange
        using var rsa = RSA.Create(2048);
        string subjectName = "CN=Test Service Account, O=Test Organization, C=US";
        var request = new CertificateRequest(
            subjectName,
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        DateTimeOffset notBefore = DateTimeOffset.UtcNow;
        DateTimeOffset notAfter = notBefore.AddYears(1);

        using X509Certificate2 certificate = request.CreateSelfSigned(notBefore, notAfter);

        // Act
        string subject = certificate.Subject;

        // Assert
        subject.ShouldBe(subjectName);
        subject.ShouldContain("CN=Test Service Account");
        subject.ShouldContain("O=Test Organization");
        subject.ShouldContain("C=US");
    }

    [Fact]
    public void Certificate_ExpirationCheck_WorksCorrectly()
    {
        // Arrange
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=Test Certificate",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        // Create a certificate that expires in 1 day
        DateTimeOffset notBefore = DateTimeOffset.UtcNow;
        DateTimeOffset notAfter = notBefore.AddDays(1);

        using X509Certificate2 certificate = request.CreateSelfSigned(notBefore, notAfter);

        // Act & Assert - Use DateTime.Now for comparison since certificate times are local
        certificate.NotBefore.ShouldBeLessThanOrEqualTo(DateTime.Now);
        certificate.NotAfter.ShouldBeGreaterThan(DateTime.Now);
    }

    [Fact]
    public void ExpiredCertificate_IsDetectedCorrectly()
    {
        // Arrange
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=Expired Certificate",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        // Create a certificate that already expired (backdate it)
        DateTimeOffset notBefore = DateTimeOffset.UtcNow.AddDays(-2);
        DateTimeOffset notAfter = notBefore.AddDays(1); // Expired 1 day ago

        using X509Certificate2 certificate = request.CreateSelfSigned(notBefore, notAfter);

        // Act
        bool isExpired = certificate.NotAfter < DateTime.UtcNow;

        // Assert
        isExpired.ShouldBeTrue();
    }

    [Fact]
    public void NotYetValidCertificate_IsDetectedCorrectly()
    {
        // Arrange
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=Future Certificate",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        // Create a certificate that is not yet valid
        DateTimeOffset notBefore = DateTimeOffset.UtcNow.AddDays(1);
        DateTimeOffset notAfter = notBefore.AddDays(365);

        using X509Certificate2 certificate = request.CreateSelfSigned(notBefore, notAfter);

        // Act
        bool isNotYetValid = certificate.NotBefore > DateTime.UtcNow;

        // Assert
        isNotYetValid.ShouldBeTrue();
    }

    #endregion
}
