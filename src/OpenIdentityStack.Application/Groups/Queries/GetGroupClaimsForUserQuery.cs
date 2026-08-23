using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Groups;

using SharedKernel;
namespace OpenIdentityStack.Application.Groups.Queries;

public sealed record GroupClaimDto(string Type, string Value, TokenTarget TokenTarget);

public sealed class GetGroupClaimsForUserQueryHandler : IGetGroupClaimsForUserQueryHandler
{
    private readonly IGroupRepository groupRepository;

    public GetGroupClaimsForUserQueryHandler(IGroupRepository groupRepository)
    {
        this.groupRepository = groupRepository;
    }

    /// <summary>
    /// Collects the claim mappings contributed by the groups a user belongs to.
    /// </summary>
    /// <remarks>
    /// Two behaviours are deliberate and covered by tests:
    /// <list type="bullet">
    /// <item>
    /// Groups are the user's direct memberships only. A parent group does not contribute its
    /// mappings to members of its children.
    /// </item>
    /// <item>
    /// When several groups map the same claim type to different values, every value is emitted
    /// and the claim becomes multi-valued. <see cref="GroupMapping"/> carries no precedence
    /// field, so there is nothing to resolve a conflict with, and discarding a value would
    /// silently drop an authorization input.
    /// </item>
    /// </list>
    /// </remarks>
    /// <param name="userId">The user whose group claims are being resolved.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<Result<IReadOnlyList<GroupClaimDto>>> HandleAsync(
        UserId userId,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Group> groups = await this.groupRepository.GetGroupsForUserAsync(userId, cancellationToken);

        var claims = new List<GroupClaimDto>();

        foreach (Group group in groups)
        {
            foreach (GroupMapping mapping in group.Mappings)
            {
                if (mapping.Type == MappingType.Claim && mapping.Value != null)
                {
                    claims.Add(new GroupClaimDto(mapping.Target, mapping.Value, mapping.TokenTarget));
                }
            }
        }

        return claims;
    }
}
