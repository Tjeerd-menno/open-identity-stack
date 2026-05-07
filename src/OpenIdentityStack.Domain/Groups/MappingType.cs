namespace OpenIdentityStack.Domain.Groups;

/// <summary>
/// Specifies the type of group mapping.
/// </summary>
public enum MappingType
{
    /// <summary>
    /// Maps the group to a defined System Role.
    /// </summary>
    Role = 1,

    /// <summary>
    /// Maps the group to a raw Claim (type/value pair).
    /// </summary>
    Claim = 2
}
