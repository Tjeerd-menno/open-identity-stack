namespace OpenIdentityStack.Domain.Users;

/// <summary>
/// The OpenID Connect standard <c>address</c> claim, as defined by OIDC Core section 5.1.1.
/// </summary>
/// <remarks>
/// <see cref="Formatted"/> is stored rather than derived from the other components:
/// international address formatting has no correct general rule, so only the source of
/// the data knows how the address should be laid out for display.
/// </remarks>
public sealed record Address(
    string? Formatted = null,
    string? StreetAddress = null,
    string? Locality = null,
    string? Region = null,
    string? PostalCode = null,
    string? Country = null)
{
    /// <summary>
    /// Gets a value indicating whether every component is absent, in which case the
    /// address must not be emitted as a claim at all.
    /// </summary>
    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(this.Formatted)
        && string.IsNullOrWhiteSpace(this.StreetAddress)
        && string.IsNullOrWhiteSpace(this.Locality)
        && string.IsNullOrWhiteSpace(this.Region)
        && string.IsNullOrWhiteSpace(this.PostalCode)
        && string.IsNullOrWhiteSpace(this.Country);
}
