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

    public static GroupMapping Create(MappingType type, string target, string? value, TokenTarget tokenTarget)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            throw new ArgumentException("Target cannot be empty", nameof(target));
        }

        if (type == MappingType.Role && !string.IsNullOrEmpty(value))
        {
            throw new ArgumentException("Role mappings should not have a value", nameof(value));
        }

        return new GroupMapping(type, target, value, tokenTarget);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return this.Type;
        yield return this.Target;
        yield return this.Value?.ToUpperInvariant() ?? string.Empty; // Case insensitive value comparison usually? Or keep sensitive. Let's keep sensitive for now unless spec says otherwise, but strings often null. Common to use object.
        yield return this.TokenTarget;
    }
}
