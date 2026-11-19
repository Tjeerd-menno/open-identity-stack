using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

namespace OpenIdentityStack.Api.Authorization;

/// <summary>Deployment-owned trust for delegated administrative authentication.</summary>
public sealed class AdministrativeMfaPolicy
{
    public const string ClaimType = "ois_admin_mfa";
    private readonly Dictionary<Guid, TrustedProvider> providers = [];
    private readonly HashSet<string> federatedEvidence = new(StringComparer.Ordinal);
    private readonly string? emergencyPin;
    private readonly TimeProvider clock;

    public AdministrativeMfaPolicy(IHostEnvironment environment, IConfiguration configuration, TimeProvider clock)
    {
        this.clock = clock;
        IConfigurationSection policy = configuration.GetSection("AdministrativeMfa");
        this.IsRequired = (!environment.IsDevelopment() && !environment.IsEnvironment("Testing")) || policy.GetValue<bool>("Enforce");
        foreach (IConfigurationSection provider in policy.GetSection("TrustedProviders").GetChildren())
        {
            string? issuer = provider["Issuer"];
            string? acr = provider["Acr"];
            if (!Guid.TryParse(provider.Key, out Guid id) || id == Guid.Empty ||
                !Uri.TryCreate(issuer, UriKind.Absolute, out Uri? uri) || uri.Scheme != Uri.UriSchemeHttps ||
                !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) ||
                string.IsNullOrWhiteSpace(acr))
            {
                throw new InvalidOperationException("AdministrativeMfa requires a provider ID, an HTTPS issuer, and an exact ACR.");
            }

            // A change to any part of the trusted tuple invalidates previously issued evidence.
            string proof = "oidc:" + Convert.ToHexString(SHA256.HashData(
                JsonSerializer.SerializeToUtf8Bytes(new[] { id.ToString("D"), issuer, acr })));
            this.providers.Add(id, new TrustedProvider(issuer, acr, proof));
            this.federatedEvidence.Add(proof);
        }

        string? account = policy["Emergency:UserId"];
        string? pin = policy["Emergency:CertificateSha256"];
        if (account is null && pin is null) { return; }
        if (!Guid.TryParse(account, out Guid userId) || userId == Guid.Empty ||
            pin is not { Length: 64 } || !pin.All(char.IsAsciiHexDigit))
        {
            throw new InvalidOperationException("AdministrativeMfa emergency access requires a user ID and a SHA-256 certificate fingerprint.");
        }

        this.EmergencyUserId = userId;
        this.emergencyPin = pin.ToUpperInvariant();
    }

    public bool IsRequired { get; }
    public Guid? EmergencyUserId { get; }

    public string? FederatedEvidence(Guid providerId, string issuer, string acr) =>
        this.providers.TryGetValue(providerId, out TrustedProvider? provider) &&
        provider.Issuer == issuer && provider.Acr == acr ? provider.Proof : null;

    public string? EmergencyEvidence(Guid userId, X509Certificate2 certificate)
    {
        DateTimeOffset now = this.clock.GetUtcNow();
        DateTimeOffset expiresAt = certificate.NotAfter.ToUniversalTime();
        if (userId != this.EmergencyUserId || now < certificate.NotBefore.ToUniversalTime() || now >= expiresAt ||
            certificate.GetCertHashString(HashAlgorithmName.SHA256) != this.emergencyPin)
        {
            return null;
        }

        return FormattableString.Invariant($"cert:{this.emergencyPin}:{expiresAt.ToUnixTimeSeconds()}");
    }

    public bool AllowsHuman(ClaimsPrincipal principal, Guid subjectId)
    {
        if (!this.IsRequired) { return true; }
        string? proof = principal.FindSingleClaim(ClaimType)?.Value;
        if (proof is null) { return false; }
        if (this.federatedEvidence.Contains(proof)) { return true; }
        if (subjectId != this.EmergencyUserId) { return false; }

        string prefix = $"cert:{this.emergencyPin}:";
        return proof.StartsWith(prefix, StringComparison.Ordinal) &&
            long.TryParse(proof.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out long expiration) &&
            expiration > this.clock.GetUtcNow().ToUnixTimeSeconds();
    }

    private sealed record TrustedProvider(string Issuer, string Acr, string Proof);
}
