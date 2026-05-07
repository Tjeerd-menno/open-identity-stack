namespace OpenIdentityStack.Application.Abstractions;

/// <summary>
/// Protects secrets before persistence and unprotects them for runtime use.
/// </summary>
public interface ISecretProtector
{
    string Protect(string secret);

    string? Unprotect(string? protectedSecret);
}
