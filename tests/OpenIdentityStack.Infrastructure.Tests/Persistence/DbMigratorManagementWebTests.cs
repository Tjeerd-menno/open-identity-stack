using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using OpenIddict.EntityFrameworkCore.Models;
using OpenIdentityStack.Application;
using OpenIdentityStack.Application.Resources;
using OpenIdentityStack.Domain.Resources;
using OpenIdentityStack.Infrastructure.Persistence;

namespace OpenIdentityStack.Infrastructure.Tests.Persistence;

public sealed class DbMigratorManagementWebTests
{
    [Fact]
    public async Task RealMigratorPreparesManagementRegistrationAndOnlyExplicitBootstrapGrantsDelegatedAccess()
    {
        string? server = Environment.GetEnvironmentVariable("OIS_FEDERATION_TEST_POSTGRES");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(server), "The DbMigrator executable requires PostgreSQL.");
        string connection = new NpgsqlConnectionStringBuilder(server)
        {
            Database = $"ois_migrator_test_{Guid.NewGuid():N}", Pooling = false,
        }.ConnectionString;
        DbContextOptions<OpenIdentityStackDbContext> options = new DbContextOptionsBuilder<OpenIdentityStackDbContext>().UseNpgsql(connection).UseOpenIddict().Options;
        await using var db = new OpenIdentityStackDbContext(options);
        try
        {
            await RunMigratorAsync(connection, bootstrap: false);
            Domain.Applications.Application client = (await db.Applications.SingleOrDefaultAsync(application => application.ClientId == "management-web-client"))!;
            client.ShouldNotBeNull();
            client.AllowedScopes.ShouldContain(ProtectedResource.AdministrativeScope);
            (await db.Set<OpenIddictEntityFrameworkCoreApplication>().CountAsync(application => application.ClientId == client.ClientId)).ShouldBe(1);
            (await db.ClientResourceGrants.CountAsync(grant => grant.ClientApplicationId == client.Id)).ShouldBe(0);

            await RunMigratorAsync(connection, bootstrap: true);
            ClientResourceGrant grant = await db.ClientResourceGrants.SingleAsync(value => value.ClientApplicationId == client.Id);
            grant.ResourceId.ShouldBe(ProtectedResource.AdministrativeResourceId);
            grant.DelegatedPermissions.ShouldBe(["*"]);
            grant.ApplicationPermissions.ShouldBeEmpty();
            long revision = grant.Revision;

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddApplication();
            services.AddInfrastructure(connection, new ConfigurationBuilder().Build(), "Testing");
            await using ServiceProvider provider = services.BuildServiceProvider();
            await using AsyncServiceScope scope = provider.CreateAsyncScope();
            Domain.Users.User user = await db.Users.SingleAsync(value => value.Email == "migrator-test@example.test");
            SharedKernel.Result<ResourceTokenProjection> projected = await scope.ServiceProvider.GetRequiredService<IResourcePermissionService>().ProjectAsync(
                new("management-web-client", ["openid", "profile", "email", "ois.admin"], [], user.Id));
            projected.IsSuccess.ShouldBeTrue();
            projected.Value.Permissions.ShouldContain("users:read");

            await RunMigratorAsync(connection, bootstrap: true);
            db.ChangeTracker.Clear();
            (await db.Applications.CountAsync(application => application.ClientId == client.ClientId)).ShouldBe(1);
            grant = await db.ClientResourceGrants.SingleAsync(value => value.ClientApplicationId == client.Id);
            grant.Revision.ShouldBe(revision);
            grant.Configure([], []).IsSuccess.ShouldBeTrue();
            await db.SaveChangesAsync();
            await RunMigratorAsync(connection, bootstrap: true);
            db.ChangeTracker.Clear();
            (await db.ClientResourceGrants.SingleAsync(value => value.ClientApplicationId == client.Id)).DelegatedPermissions.ShouldBeEmpty();
        }
        finally
        {
            // Only the randomly named database created by this test is removed.
            await db.Database.EnsureDeletedAsync();
        }
    }

    private static async Task RunMigratorAsync(string connection, bool bootstrap)
    {
        DirectoryInfo? root = new(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "OpenIdentityStack.slnx"))) { root = root.Parent; }
        root.ShouldNotBeNull();
        string configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        string project = Path.Combine(root.FullName, "src", "OpenIdentityStack.DbMigrator");
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = project, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true,
        };
        start.ArgumentList.Add(Path.Combine(project, "bin", configuration, "net10.0", "OpenIdentityStack.DbMigrator.dll"));
        start.Environment["ConnectionStrings__openidentitystack"] = connection;
        start.Environment["DOTNET_ENVIRONMENT"] = "Testing";
        start.Environment["Seed__DemoClients"] = "false";
        start.Environment["Seed__AdminUser__Enabled"] = "true";
        start.Environment["Seed__AdminUser__Email"] = "migrator-test@example.test";
        start.Environment["Seed__AdminUser__Password"] = "MigratorFixture123!";
        start.Environment["Seed__AdministrativeAccess__BootstrapManagementWeb"] = bootstrap.ToString();
        using Process process = Process.Start(start)!;
        // Drain output without attaching startup logs or test credentials to assertion failures.
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        await Task.WhenAll(output, error);
        process.ExitCode.ShouldBe(0, "The actual DbMigrator executable must complete successfully.");
    }
}
