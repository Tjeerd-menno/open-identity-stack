using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Extension methods for database initialization.
/// </summary>
public static class DatabaseExtensions
{
    /// <summary>
    /// The Testing environment name.
    /// </summary>
    public const string TestingEnvironmentName = "Testing";

    /// <summary>
    /// Ensures the database schema is created (development/testing only).
    /// This method is idempotent and safe to call multiple times.
    /// </summary>
    /// <typeparam name="TDbContext">The DbContext type.</typeparam>
    /// <param name="app">The web application.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task EnsureDatabaseCreatedAsync<TDbContext>(
        this WebApplication app,
        CancellationToken cancellationToken = default) where TDbContext : DbContext
    {
        if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment(TestingEnvironmentName))
        {
            return;
        }

        using IServiceScope scope = app.Services.CreateScope();
        TDbContext dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();
        ILogger<TDbContext>? logger = scope.ServiceProvider.GetService<ILogger<TDbContext>>();

#pragma warning disable CA1848, CA1873 // Startup code, not performance critical
        logger?.LogInformation("Initializing {DbContext} schema...", typeof(TDbContext).Name);
#pragma warning restore CA1848, CA1873

        try
        {
            await dbContext.Database.EnsureCreatedAsync(cancellationToken);
#pragma warning disable CA1848, CA1873
            logger?.LogInformation("{DbContext} schema initialized successfully.", typeof(TDbContext).Name);
#pragma warning restore CA1848, CA1873
        }
        catch (Exception ex)
        {
#pragma warning disable CA1848, CA1873
            logger?.LogError(ex, "Failed to initialize {DbContext} schema", typeof(TDbContext).Name);
#pragma warning restore CA1848, CA1873
            throw;
        }
    }
}
