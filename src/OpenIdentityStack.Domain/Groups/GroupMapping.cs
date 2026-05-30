using OpenIdentityStack.Domain.Common;

using SharedKernel;
namespace OpenIdentityStack.Domain.Groups;

public class GroupMapping : ValueObject
{
    public MappingType Type { get; }
    public string Target { get; }
    public string? Value { get; }
    public TokenTarget TokenTarget { get; }

    private GroupMapping(MappingType type, string target, string? value, TokenTarget tokenTarget)
    {
        this.Type = type;
        this.Target = target;
        this.Value = value;
        this.TokenTarget = tokenTarget;
    }

    public static Result<GroupMapping> Create(MappingType type, string target, string? value, TokenTarget tokenTarget)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return GroupMappingErrors.TargetRequired;
        }

        if (type == MappingType.Role && !string.IsNullOrEmpty(value))
        {
            return GroupMappingErrors.RoleMappingValueNotAllowed;
        }

        return new GroupMapping(type, target, value, tokenTarget);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return this.Type;
        yield return this.Target;
        yield return this.Value?.ToUpperInvariant() ?? string.Empty;
        yield return this.TokenTarget;
    }
}

public static class GroupMappingErrors
{
    public static readonly DomainError TargetRequired =
        DomainError.Validation("GroupMapping.TargetRequired", "Target cannot be empty.");

    public static readonly DomainError RoleMappingValueNotAllowed =
        DomainError.Validation("GroupMapping.RoleMappingValueNotAllowed", "Role mappings should not have a value.");
}
