namespace OpenIdentityStack.Application.Authorization;

/// <summary>OP-issued subject classification; this namespace cannot be supplied by group claims.</summary>
public static class TokenSubjectClaims
{
    public const string Kind = "ois.subject_kind";
    public const string Application = "application";
}
