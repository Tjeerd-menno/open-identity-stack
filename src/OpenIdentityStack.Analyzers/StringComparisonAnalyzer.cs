using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace OpenIdentityStack.Analyzers;

/// <summary>
/// Rejects string.Equals and string.Contains overloads that do not take StringComparison.
/// The message names StringComparison.Ordinal and the normalized column.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class StringComparisonAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "OIS0001";

    private static readonly DiagnosticDescriptor rule = new(
        DiagnosticId,
        "String comparison must pass StringComparison",
        "'{0}' compares strings without StringComparison. Pass StringComparison.Ordinal, or compare the normalized column.",
        "Globalization",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "string.Equals and string.Contains without StringComparison hide an unclear comparison. Pass StringComparison.Ordinal, or compare a normalized column. OrdinalIgnoreCase stays legal when the call passes it explicitly.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context)
    {
        if (context.Operation is not IInvocationOperation invocation)
        {
            return;
        }

        IMethodSymbol target = invocation.TargetMethod;
        INamedTypeSymbol? containingType = target.ContainingType;
        if (containingType?.SpecialType != SpecialType.System_String)
        {
            return;
        }

        if (target.Name is not ("Equals" or "Contains"))
        {
            return;
        }

        if (HasStringComparisonParameter(target) || IsObjectEquals(target) || IsCharContains(target))
        {
            return;
        }

        string overload = $"string.{target.Name}({DescribeParameters(target)})";
        context.ReportDiagnostic(Diagnostic.Create(rule, invocation.Syntax.GetLocation(), overload));
    }

    private static bool HasStringComparisonParameter(IMethodSymbol target)
    {
        foreach (IParameterSymbol parameter in target.Parameters)
        {
            if (parameter.Type.Name == "StringComparison"
                && parameter.Type.ContainingNamespace?.ToDisplayString() == "System")
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsObjectEquals(IMethodSymbol target)
    {
        return target.Name == "Equals"
            && !target.IsStatic
            && target.Parameters.Length == 1
            && target.Parameters[0].Type.SpecialType == SpecialType.System_Object;
    }

    private static bool IsCharContains(IMethodSymbol target)
    {
        return target.Name == "Contains"
            && target.Parameters.Length == 1
            && target.Parameters[0].Type.SpecialType == SpecialType.System_Char;
    }

    private static string DescribeParameters(IMethodSymbol target)
    {
        if (target.Parameters.Length == 0)
        {
            return string.Empty;
        }

        string[] names = new string[target.Parameters.Length];
        for (int index = 0; index < target.Parameters.Length; index++)
        {
            ITypeSymbol type = target.Parameters[index].Type;
            names[index] = type.SpecialType == SpecialType.System_String
                ? "string"
                : type.ToDisplayString();
        }

        return string.Join(", ", names);
    }
}
