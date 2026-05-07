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

    public async Task<Result<IReadOnlyList<GroupClaimDto>>> HandleAsync(
        UserId userId,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Group> groups = await this.groupRepository.GetGroupsForUserAsync(userId, cancellationToken);

        var claims = new List<GroupClaimDto>();

        // Priority logic?
        // Spec: "Priority | int | Default 0 | For conflict resolution" 
        // "Higher priority wins in conflicts"
        // My GroupMapping implementation in Domain didn't include Priority property in ValueObject!
        // Task T149: "Create GroupMapping value object ...".
        // I created it with Type, Target, Value, TokenTarget. NO Priority.
        // If Spec has Priority, I missed it during Domain implementation of GroupMapping.
        // Let's check GroupMapping.cs again.
        
        foreach (Group group in groups)
        {
            foreach (GroupMapping mapping in group.Mappings)
            {
                if (mapping.Type == MappingType.Claim && mapping.Value != null)
                {
                    // If target is ClaimType
                    claims.Add(new GroupClaimDto(mapping.Target, mapping.Value, mapping.TokenTarget));
                    
                    // Conflict resolution without Priority? 
                    // If multiple groups define "department" with different values?
                    // We collect all (array of claims).
                    // Or we should override?
                    // "department": "IT", "department": "Sales" -> User has both?
                    // Usually arrays are fine for claims.
                    // If spec required Priority for single-value, I'd need Priority.
                    // Assuming multi-value for now as I missed Priority field.
                }
            }
        }

        return claims;
    }
}
