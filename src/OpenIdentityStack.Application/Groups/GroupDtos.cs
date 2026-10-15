using OpenIdentityStack.Domain.Groups;

using SharedKernel;
namespace OpenIdentityStack.Application.Groups;

public record CreateGroupRequest(
    string Name,
    string? Description,
    Guid? ParentGroupId);

public record UpdateGroupRequest(
    string? Name,
    string? Description);

public record GroupResponse(
    Guid Id,
    string Name,
    string? Description,
    Guid? ParentGroupId,
    DateTime CreatedAt);

public record GroupListResponse(
    IEnumerable<GroupResponse> Items,
    string? NextPageToken);

public record AddGroupMemberRequest(
    Guid UserId);

public record CreateGroupMappingRequest(
    MappingType MappingType,
    Guid? TargetRoleId,
    string? ClaimType,
    string? ClaimValue,
    TokenTarget TokenTarget,
    int? Priority);

public record GroupMappingResponse(
    Guid Id,
    MappingType MappingType,
    Guid? TargetRoleId,
    string? ClaimType,
    string? ClaimValue,
    TokenTarget TokenTarget);

/// <summary>
/// One group mapping prepared for the admin list, with role mappings shown by role name.
/// </summary>
public sealed record ListedGroupMapping(Guid Id, MappingType MappingType, string Value);
