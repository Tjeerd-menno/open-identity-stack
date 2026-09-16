
using System.Text.RegularExpressions;

using OpenIdentityStack.Domain.Common;

using SharedKernel;
namespace OpenIdentityStack.Domain.Federation;
/// <summary>
/// Represents a mapping from an upstream claim to a local claim.
/// </summary>
public sealed class ClaimMapping : IEquatable<ClaimMapping>
{
    private static readonly TimeSpan regexMatchTimeout = TimeSpan.FromMilliseconds(250);

    private readonly Regex? transformRegex;

    private ClaimMapping(
        string sourceClaim,
        string targetClaim,
        TransformType transformType,
        string? transformPattern)
    {
        this.SourceClaim = sourceClaim;
        this.TargetClaim = targetClaim;
        this.TransformType = transformType;
        this.TransformPattern = transformPattern;
        this.transformRegex = transformType == TransformType.Regex && !string.IsNullOrEmpty(transformPattern)
            ? CreateRegex(transformPattern)
            : null;
    }

    /// <summary>
    /// Gets the source claim name from the upstream provider.
    /// </summary>
    public string SourceClaim { get; }

    /// <summary>
    /// Gets the target claim name for the local token.
    /// </summary>
    public string TargetClaim { get; }

    /// <summary>
    /// Gets the type of transformation to apply.
    /// </summary>
    public TransformType TransformType { get; }

    /// <summary>
    /// Gets the pattern for regex transformations.
    /// </summary>
    public string? TransformPattern { get; }

    /// <summary>
    /// Creates a new claim mapping.
    /// </summary>
    /// <param name="sourceClaim">The source claim name.</param>
    /// <param name="targetClaim">The target claim name.</param>
    /// <param name="transformType">The transformation type.</param>
    /// <param name="transformPattern">Optional pattern for regex transforms.</param>
    /// <returns>Result containing the claim mapping or an error.</returns>
    public static Result<ClaimMapping> Create(
        string sourceClaim,
        string targetClaim,
        TransformType transformType,
        string? transformPattern = null)
    {
        if (string.IsNullOrWhiteSpace(sourceClaim))
        {
            return ClaimMappingErrors.SourceClaimRequired;
        }

        if (string.IsNullOrWhiteSpace(targetClaim))
        {
            return ClaimMappingErrors.TargetClaimRequired;
        }

        if (transformType == TransformType.Regex && string.IsNullOrWhiteSpace(transformPattern))
        {
            return ClaimMappingErrors.TransformPatternRequired;
        }

        return new ClaimMapping(
            sourceClaim.Trim(),
            targetClaim.Trim(),
            transformType,
            transformPattern?.Trim());
    }

    /// <summary>
    /// Transforms a claim value according to the mapping configuration.
    /// </summary>
    /// <param name="value">The source claim value.</param>
    /// <returns>The transformed claim value.</returns>
    public string Transform(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        try
        {
            int atIndex = value.IndexOf('@');

            return this.TransformType switch
            {
                TransformType.Direct => value,
                TransformType.Lowercase => value.ToLowerInvariant(),
                TransformType.Uppercase => value.ToUpperInvariant(),
                TransformType.EmailPrefix => atIndex < 0 ? value : value[..atIndex],
                TransformType.EmailDomain => GetEmailDomain(value, atIndex),
                TransformType.Regex when this.transformRegex is not null => this.transformRegex.Replace(value, string.Empty),
                _ => value,
            };
        }
        catch (ArgumentException)
        {
            return value;
        }
        catch (RegexMatchTimeoutException)
        {
            return value;
        }
    }

    private static Regex? CreateRegex(string pattern)
    {
        try
        {
            return new Regex(pattern, RegexOptions.None, regexMatchTimeout);
        }
        catch (ArgumentException)
        {
            // An unusable pattern is treated as a pass-through mapping, matching the historical
            // behaviour of catching the same exception from Regex.Replace on every transform.
            return null;
        }
    }

    private static string GetEmailDomain(string value, int atIndex)
    {
        if (atIndex < 0)
        {
            return value;
        }

        int domainEnd = value.IndexOf('@', atIndex + 1);
        return domainEnd < 0 ? value[(atIndex + 1)..] : value[(atIndex + 1)..domainEnd];
    }

    /// <inheritdoc />
    public bool Equals(ClaimMapping? other)
    {
        if (other is null)
        {
            return false;
        }

        return this.SourceClaim == other.SourceClaim;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => this.Equals(obj as ClaimMapping);

    /// <inheritdoc />
    public override int GetHashCode() => this.SourceClaim.GetHashCode();
}

/// <summary>
/// Claim mapping related errors.
/// </summary>
public static class ClaimMappingErrors
{
    public static readonly DomainError SourceClaimRequired =
        DomainError.Validation("ClaimMapping.SourceClaimRequired", "Source claim is required.");

    public static readonly DomainError TargetClaimRequired =
        DomainError.Validation("ClaimMapping.TargetClaimRequired", "Target claim is required.");

    public static readonly DomainError TransformPatternRequired =
        DomainError.Validation("ClaimMapping.TransformPatternRequired", "Transform pattern is required for regex transformations.");
}
