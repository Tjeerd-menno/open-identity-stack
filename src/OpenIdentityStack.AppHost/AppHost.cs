IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

// Use a persistent data volume only when not explicitly disabled (e.g., test runs)
bool disableDataVolume = string.Equals(
    Environment.GetEnvironmentVariable("OPENIDENTITYSTACK_DISABLE_DATA_VOLUME"),
    "true",
    StringComparison.OrdinalIgnoreCase);
bool enableAdminWeb = !string.Equals(
    Environment.GetEnvironmentVariable("OPENIDENTITYSTACK_ENABLE_ADMINWEB"),
    "false",
    StringComparison.OrdinalIgnoreCase);
bool enableManagementWeb = !string.Equals(
    Environment.GetEnvironmentVariable("OPENIDENTITYSTACK_ENABLE_MANAGEMENTWEB"),
    "false",
    StringComparison.OrdinalIgnoreCase);

IResourceBuilder<ParameterResource> defaultAdminPassword = builder.AddParameter("default-admin-password", secret: true);
IResourceBuilder<ParameterResource> postgresPassword = disableDataVolume
    ? builder.AddParameter("postgres-password", secret: true)
    : builder.AddParameter("postgres-password", "postgres", publishValueAsDefault: false, secret: true);

// Add PostgreSQL with pgAdmin for development
IResourceBuilder<PostgresServerResource> postgres = builder.AddPostgres("postgres", password: postgresPassword);

if (!disableDataVolume)
{
    postgres = postgres.WithDataVolume();
}

IResourceBuilder<PostgresDatabaseResource> openIdentityStackDb = postgres.AddDatabase("openidentitystack");

// Propagate the AppHost environment name to child projects so they use the correct
// OpenIddict configuration (dev/ephemeral certificates in Development/Testing, real
// certificates in Production/Staging).
string aspNetCoreEnvironment = builder.Environment.EnvironmentName;

// Run database migrations/seeding outside the APIs
// The DbMigrator is a generic Host app (not ASP.NET Core), so it reads DOTNET_ENVIRONMENT
// rather than ASPNETCORE_ENVIRONMENT to determine IHostEnvironment.EnvironmentName.
IResourceBuilder<ProjectResource> openIdModuleMigrator = builder.AddProject<Projects.OpenIdentityStack_DbMigrator>("openidentitystack-db-migrator")
    .WithReference(openIdentityStackDb)
    .WithEnvironment("DOTNET_ENVIRONMENT", aspNetCoreEnvironment)
    .WithEnvironment("Seed__DevelopmentData", "true")
    .WithEnvironment("Seed__DefaultAdmin__Password", defaultAdminPassword)
    .WaitFor(openIdentityStackDb);

// Add the API project with PostgreSQL dependency
// Use fixed external ports for consistent OAuth configuration
IResourceBuilder<ProjectResource> api = builder.AddProject<Projects.OpenIdentityStack_Api>("api")
    .WithReference(openIdentityStackDb)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", aspNetCoreEnvironment)
    .WaitFor(openIdentityStackDb)
    .WaitForCompletion(openIdModuleMigrator);

if (enableAdminWeb)
{
    // Add the Admin Web App (React + Vite)
    // Use a fixed port in local development, but allow dynamic ports for test runs.
#pragma warning disable IDE0008 // Var improves fluency with Aspire builder APIs.
    var adminWeb = builder.AddJavaScriptApp("adminweb", "../OpenIdentityStack.AdminWeb")
        .WithReference(api)
        .WithExternalHttpEndpoints()
        .WithEnvironment("VITE_OIDC_AUTHORITY", api.GetEndpoint("http"))
        .WithEnvironment("VITE_API_BASE_URL", api.GetEndpoint("http"))
        .PublishAsDockerFile();

    if (disableDataVolume)
    {
        adminWeb.WithHttpEndpoint(env: "PORT");
    }
    else
    {
        adminWeb.WithHttpEndpoint(port: 5175, env: "PORT");
    }

#pragma warning restore IDE0008
}

if (enableManagementWeb)
{
    // Add the Management Web App (React + Vite + Mantine) as a side-by-side UI.
    // Use a fixed local port for stable OAuth callback registration, but dynamic
    // endpoints during test runs to avoid port contention.
#pragma warning disable IDE0008 // Var improves fluency with Aspire builder APIs.
    var managementWeb = builder.AddJavaScriptApp("managementweb", "../OpenIdentityStack.ManagementWeb")
        .WithReference(api)
        .WithExternalHttpEndpoints()
        .WithEnvironment("VITE_OIDC_AUTHORITY", api.GetEndpoint("http"))
        .WithEnvironment("VITE_API_BASE_URL", api.GetEndpoint("http"))
        .WithEnvironment("VITE_OIDC_CLIENT_ID", "management-web-client")
        .PublishAsDockerFile();

    if (disableDataVolume)
    {
        managementWeb.WithHttpEndpoint(env: "PORT");
    }
    else
    {
        managementWeb.WithHttpEndpoint(port: 5176, env: "PORT");
    }

#pragma warning restore IDE0008
}

await builder.Build().RunAsync();
