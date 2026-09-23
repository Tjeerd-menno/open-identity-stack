namespace OpenIdentityStack.Api.Authentication;

public sealed record ConsentViewModel(
    string ActionUrl,
    string ClientName,
    IReadOnlyList<string> Scopes,
    IReadOnlyList<string> Claims,
    IReadOnlyList<KeyValuePair<string, string>> RequestParameters,
    string Ticket);
