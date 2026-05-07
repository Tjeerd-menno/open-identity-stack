using OpenIdentityStack.Domain.Common;

using SharedKernel;
namespace OpenIdentityStack.Domain.Groups;

public class GroupMembership
{
    public UserId UserId { get; private set; }
    public GroupId GroupId { get; private set; }
    public DateTimeOffset AssignedAt { get; private set; }
    public UserId AssignedBy { get; private set; }

    internal GroupMembership(GroupId groupId, UserId userId, DateTimeOffset assignedAt, UserId assignedBy)
    {
        this.GroupId = groupId;
        this.UserId = userId;
        this.AssignedAt = assignedAt;
        this.AssignedBy = assignedBy;
    }

    // Constructor required for EF Core
    private GroupMembership() 
    {
        this.UserId = UserId.Empty;
        this.GroupId = GroupId.Empty;
        this.AssignedBy = UserId.Empty;
    }
}
