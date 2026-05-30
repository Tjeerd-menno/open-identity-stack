using OpenIdentityStack.Domain.Common;

using SharedKernel;
namespace OpenIdentityStack.Domain.Groups;

public sealed class Group : AggregateRoot<GroupId>
{
    private readonly List<GroupMembership> memberships = [];
    private readonly List<GroupMapping> mappings = [];
    private readonly List<Group> childGroups = [];

    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public GroupId? ParentGroupId { get; private set; }
    
    // Navigation properties
    public Group? ParentGroup { get; private set; }
    public IReadOnlyCollection<Group> ChildGroups => this.childGroups.AsReadOnly();
    public IReadOnlyCollection<GroupMembership> Memberships => this.memberships.AsReadOnly();
    public IReadOnlyCollection<GroupMapping> Mappings => this.mappings.AsReadOnly();

    private Group(
        GroupId id,
        string name,
        string? description,
        DateTimeOffset createdAt) : base(id)
    {
        this.Name = name;
        this.Description = description;
        this.CreatedAt = createdAt;
        this.ModifiedAt = createdAt;
    }

    // Required for EF Core
    private Group() 
    {
        this.Name = string.Empty;
    }

    public static Result<Group> Create(
        string name,
        string? description,
        IDateTimeProvider dateTimeProvider)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return GroupErrors.NameRequired;
        }

        if (name.Length > 128)
        {
            return GroupErrors.NameTooLong;
        }

        if (!string.IsNullOrEmpty(description) && description.Length > 512)
        {
            return GroupErrors.DescriptionTooLong;
        }

        var group = new Group(
            GroupId.Create(),
            name.Trim(),
            description?.Trim(),
            dateTimeProvider.UtcNow);

        group.RaiseDomainEvent(new GroupDomainEvents.GroupCreated(
            group.Id,
            group.Name,
            dateTimeProvider.UtcNow));

        return group;
    }

    public Result Update(string name, string? description, IDateTimeProvider dateTimeProvider)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return GroupErrors.NameRequired;
        }

        if (name.Length > 128)
        {
            return GroupErrors.NameTooLong;
        }

        if (!string.IsNullOrEmpty(description) && description.Length > 512)
        {
            return GroupErrors.DescriptionTooLong;
        }

        this.Name = name.Trim();
        this.Description = description?.Trim();
        this.ModifiedAt = dateTimeProvider.UtcNow;

        this.RaiseDomainEvent(new GroupDomainEvents.GroupUpdated(
            this.Id,
            dateTimeProvider.UtcNow));

        return Result.Success();
    }

    public Result SetParent(Group? parent, IDateTimeProvider dateTimeProvider)
    {
        // Basic circular check (self-reference)
        if (parent != null && parent.Id == this.Id)
        {
            return GroupErrors.CircularReference;
        }

        // Deep circular check - traverse up the parent chain
        if (parent != null && HasCircularReference(parent))
        {
            return GroupErrors.CircularReference;
        }

        this.ParentGroup = parent;
        this.ParentGroupId = parent?.Id;
        this.ModifiedAt = dateTimeProvider.UtcNow;
        return Result.Success();
    }

    /// <summary>
    /// Checks if setting the given parent would create a circular reference.
    /// </summary>
    private bool HasCircularReference(Group potentialParent)
    {
        // Traverse up the parent chain to check if we find this group
        Group? current = potentialParent;
        while (current != null)
        {
            if (current.Id == this.Id)
            {
                return true; // Circular reference detected
            }
            current = current.ParentGroup;
        }
        return false;
    }

    public Result AddMember(UserId userId, UserId assignedBy, IDateTimeProvider dateTimeProvider)
    {
        if (userId == UserId.Empty)
        {
            return GroupErrors.UserIdRequired;
        }

        // Idempotent: adding an existing member is a successful no-op.
        if (this.memberships.Any(m => m.UserId == userId))
        {
            return Result.Success();
        }

        this.memberships.Add(new GroupMembership(this.Id, userId, dateTimeProvider.UtcNow, assignedBy));
        this.ModifiedAt = dateTimeProvider.UtcNow;
        return Result.Success();
    }

    public Result RemoveMember(UserId userId, IDateTimeProvider dateTimeProvider)
    {
        GroupMembership? membership = this.memberships.FirstOrDefault(m => m.UserId == userId);
        if (membership != null)
        {
            this.memberships.Remove(membership);
            this.ModifiedAt = dateTimeProvider.UtcNow;
        }

        return Result.Success();
    }

    public Result AddMapping(MappingType type, string target, string? value, TokenTarget tokenTarget, IDateTimeProvider dateTimeProvider)
    {
        Result<GroupMapping> mappingResult = GroupMapping.Create(type, target, value, tokenTarget);
        if (mappingResult.IsFailure)
        {
            return mappingResult.Error;
        }

        GroupMapping mapping = mappingResult.Value;
        if (!this.mappings.Contains(mapping))
        {
            this.mappings.Add(mapping);
            this.ModifiedAt = dateTimeProvider.UtcNow;
        }

        return Result.Success();
    }

    public Result RemoveMapping(GroupMapping mapping, IDateTimeProvider dateTimeProvider)
    {
        if (this.mappings.Remove(mapping))
        {
            this.ModifiedAt = dateTimeProvider.UtcNow;
        }

        return Result.Success();
    }
}

public static class GroupErrors
{
    public static readonly DomainError NameRequired = new("Group.NameRequired", "Group name is required.");
    public static readonly DomainError NameTooLong = new("Group.NameTooLong", "Group name cannot exceed 128 characters.");
    public static readonly DomainError DescriptionTooLong = new("Group.DescriptionTooLong", "Description cannot exceed 512 characters.");
    public static readonly DomainError CircularReference = new("Group.CircularReference", "Circular parent group reference detected.");
    public static readonly DomainError UserIdRequired = new("Group.UserIdRequired", "User id is required.");
}
