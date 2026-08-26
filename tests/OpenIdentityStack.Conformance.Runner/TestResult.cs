namespace OpenIdentityStack.Conformance.Runner;

internal sealed record TestResult(
    string Module,
    string? TestId,
    string? Status,
    string? Result,
    IReadOnlyList<string> BrowserNotes);
