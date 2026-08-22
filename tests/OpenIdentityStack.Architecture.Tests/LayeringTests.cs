using System.Reflection;
using OpenIdentityStack.Application.Groups.Queries;
using OpenIdentityStack.Domain.Common;

namespace OpenIdentityStack.Architecture.Tests;

/// <summary>
/// Enforces the inward-pointing dependency rule from the project principles: Domain holds
/// business rules and depends on nothing but SharedKernel; Application depends on Domain and
/// declares ports; adapter concerns (EF Core, ASP.NET Core, OpenIddict) live in Infrastructure
/// and Api.
///
/// The layering has been correct so far, held only by what each .csproj happens to reference.
/// These tests make a violation fail the build instead of passing review unnoticed.
/// </summary>
public sealed class LayeringTests
{
    /// <summary>
    /// Assembly name prefixes that identify an adapter concern. A reference to any of these from
    /// Domain or Application means an outer-layer dependency has leaked inward.
    /// </summary>
    private static readonly string[] AdapterAssemblyPrefixes =
    [
        "Microsoft.EntityFrameworkCore",
        "Microsoft.AspNetCore",
        "OpenIddict",
        "Npgsql",
        "Quartz",
        "Aspire.",
        "Scalar.",
    ];

    [Fact]
    public void Domain_DoesNotDependOnAdapterConcerns()
    {
        List<string> violations = AdapterReferences(typeof(SessionId).Assembly);

        violations.ShouldBeEmpty(
            $"Domain must not reference adapter assemblies, but references: {string.Join(", ", violations)}");
    }

    [Fact]
    public void Domain_DoesNotDependOnOuterLayers()
    {
        List<string> violations = SolutionReferences(typeof(SessionId).Assembly)
            .Where(name => name is not "SharedKernel")
            .ToList();

        violations.ShouldBeEmpty(
            $"Domain may only reference SharedKernel, but also references: {string.Join(", ", violations)}");
    }

    [Fact]
    public void Application_DoesNotDependOnAdapterConcerns()
    {
        List<string> violations = AdapterReferences(typeof(GroupClaimDto).Assembly);

        violations.ShouldBeEmpty(
            $"Application must not reference adapter assemblies, but references: {string.Join(", ", violations)}");
    }

    [Fact]
    public void Application_DoesNotDependOnInfrastructureOrApi()
    {
        List<string> violations = SolutionReferences(typeof(GroupClaimDto).Assembly)
            .Where(name => name is not "SharedKernel" and not "OpenIdentityStack.Domain")
            .ToList();

        violations.ShouldBeEmpty(
            "Application defines ports that Infrastructure implements, so it may only reference "
            + $"Domain and SharedKernel, but also references: {string.Join(", ", violations)}");
    }

    [Fact]
    public void SharedKernel_DependsOnNothingElseInTheSolution()
    {
        Assembly sharedKernel = typeof(IDateTimeProvider).Assembly;

        List<string> violations = [.. SolutionReferences(sharedKernel), .. AdapterReferences(sharedKernel)];

        violations.ShouldBeEmpty(
            $"SharedKernel sits at the centre and must reference nothing, but references: {string.Join(", ", violations)}");
    }

    private static List<string> AdapterReferences(Assembly assembly) =>
        ReferencedAssemblyNames(assembly)
            .Where(name => AdapterAssemblyPrefixes.Any(
                prefix => name.StartsWith(prefix, StringComparison.Ordinal)))
            .ToList();

    private static List<string> SolutionReferences(Assembly assembly) =>
        ReferencedAssemblyNames(assembly)
            .Where(name => name.StartsWith("OpenIdentityStack.", StringComparison.Ordinal)
                || name == "SharedKernel")
            .ToList();

    private static List<string> ReferencedAssemblyNames(Assembly assembly) =>
        assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name => name.Length > 0)
            .ToList();
}
