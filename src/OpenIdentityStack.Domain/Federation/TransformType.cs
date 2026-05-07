namespace OpenIdentityStack.Domain.Federation;

/// <summary>
/// Represents the type of claim transformation to apply.
/// </summary>
public enum TransformType
{
    /// <summary>
    /// Direct mapping without transformation.
    /// </summary>
    Direct = 0,

    /// <summary>
    /// Lowercase transformation.
    /// </summary>
    Lowercase = 1,

    /// <summary>
    /// Uppercase transformation.
    /// </summary>
    Uppercase = 2,

    /// <summary>
    /// Extract email prefix (before @).
    /// </summary>
    EmailPrefix = 3,

    /// <summary>
    /// Extract email domain (after @).
    /// </summary>
    EmailDomain = 4,

    /// <summary>
    /// Regular expression replacement.
    /// </summary>
    Regex = 5,
}
