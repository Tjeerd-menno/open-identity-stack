using OpenIdentityStack.Domain.Groups;

namespace OpenIdentityStack.Application.Groups.Commands;

/// <summary>
/// Command to add a mapping to a group.
/// </summary>
/// <param name="GroupId">The group ID.</param>
/// <param name="Type">The mapping type (Role or Claim).</param>
/// <param name="Target">The target name (Role name or Claim type).</param>
/// <param name="Value">The value (if claim type).</param>
/// <param name="TokenTarget">Where the claim should appear.</param>
public sealed record AddGroupMappingCommand(
    GroupId GroupId,
    MappingType Type,
    string Target,
    string? Value,
    TokenTarget TokenTarget);
