using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OpenIdentityStack.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for creating OpenIdentityStackDbContext during EF Core migrations.
/// This is used by the EF Core CLI tools when no startup project context is available.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<OpenIdentityStackDbContext>
{
    /// <summary>
    /// Creates a new instance of the DbContext for design-time operations.
    /// </summary>
    /// <param name="args">Command-line arguments (unused).</param>
    /// <returns>A configured DbContext instance.</returns>
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "This factory is only used by the EF Core CLI tooling at design time; it is never invoked in the AOT-published runtime binary.")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Same as IL2026: design-time only; not reachable from the AOT-published binary.")]
    public OpenIdentityStackDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<OpenIdentityStackDbContext>();

        // Use a local design-time connection string for migration generation.
        // The actual connection string will be provided at runtime via Aspire.
        const string connectionString = "Host=localhost;Database=openidentitystack;Username=postgres";

        optionsBuilder.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.MigrationsAssembly(typeof(OpenIdentityStackDbContext).Assembly.FullName);
        });

        // Register OpenIddict entity sets
        optionsBuilder.UseOpenIddict();

        return new OpenIdentityStackDbContext(optionsBuilder.Options);
    }
}
