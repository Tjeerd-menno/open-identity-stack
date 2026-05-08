
using OpenIdentityStack.Domain.Common;

using SharedKernel;
namespace OpenIdentityStack.Domain.Federation;
/// <summary>
/// Represents a mapping from an upstream claim to a local claim.
/// </summary>
public sealed class ClaimMapping : IEquatable<ClaimMapping>
{
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

        return this.TransformType switch
        {
            TransformType.Direct => value,
            TransformType.Lowercase => value.ToLowerInvariant(),
            TransformType.Uppercase => value.ToUpperInvariant(),
            TransformType.EmailPrefix => value.Contains('@') ? value.Split('@')[0] : value,
            TransformType.EmailDomain => value.Contains('@') ? value.Split('@')[1] : value,
            TransformType.Regex when !string.IsNullOrEmpty(this.TransformPattern) =>
                System.Text.RegularExpressions.Regex.Replace(
                    value,
                    this.TransformPattern,
                    string.Empty,
                    System.Text.RegularExpressions.RegexOptions.None,
                    TimeSpan.FromMilliseconds(250)),
            _ => value,
        };
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
