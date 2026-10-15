using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using OpenIdentityStack.Analyzers;

namespace OpenIdentityStack.Architecture.Tests;

/// <summary>
/// OIS0001 fails the two-argument string.Equals overload and string.Contains(string).
/// ProvidersApi.cs at 9120ea8bee3804b6c06850840323d75fae4b5569 calls Contains("NotFound").
/// </summary>
public sealed class StringComparisonAnalyzerTests
{
    [Fact]
    public async Task HistoricalErrorCodeContains_FailsTheAnalyzer()
    {
        string repoRoot = FindRepositoryRoot();
        string source = File.ReadAllText(Path.Combine(
            repoRoot,
            "tests",
            "OpenIdentityStack.Architecture.Tests",
            "Fixtures",
            "historical-error-code-contains.cs"));
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source);

        diagnostics.ShouldContain(diagnostic =>
            diagnostic.Id == StringComparisonAnalyzer.DiagnosticId
            && diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture).Contains("string.Contains(string)", StringComparison.Ordinal)
            && diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture).Contains("StringComparison.Ordinal", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TwoArgumentEquals_FailsTheAnalyzer()
    {
        const string source = """
            class Sample
            {
                static bool Same(string left, string right) => string.Equals(left, right);
            }
            """;
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source);

        diagnostics.ShouldContain(diagnostic =>
            diagnostic.Id == StringComparisonAnalyzer.DiagnosticId
            && diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture).Contains("string.Equals(string, string)", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExplicitOrdinalAndCollectionContains_StayQuiet()
    {
        const string source = """
            using System;
            using System.Collections.Generic;
            class Sample
            {
                static bool Same(string left, string right) => string.Equals(left, right, StringComparison.Ordinal);
                static bool Email(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
                static bool Has(string code) => code.Contains("NotFound", StringComparison.Ordinal);
                static bool Letter(string code) => code.Contains('*');
                static bool Listed(List<string> values, string value) => values.Contains(value);
            }
            """;
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source);

        diagnostics.ShouldBeEmpty();
    }

    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source)
    {
        SyntaxTree tree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create(
            "history",
            [tree],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new StringComparisonAnalyzer());
        return await compilation.WithAnalyzers(analyzers).GetAnalyzerDiagnosticsAsync();
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "OpenIdentityStack.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find repository root.");
    }
}
