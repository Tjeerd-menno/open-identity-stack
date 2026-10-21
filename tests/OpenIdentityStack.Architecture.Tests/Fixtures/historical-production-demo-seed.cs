// Copied from src/OpenIdentityStack.DbMigrator/Program.cs
// at 0943f58a7e59814996feee5a73ac224ef8ef9a7e (526cae99613eca0c9dee0ca2f2f3045b5327c49f^, PR #398).
static bool ShouldSeedDemoClients(
    IConfiguration configuration,
    IHostEnvironment hostEnvironment,
    bool seedDevData,
    bool seedCertificationProfile)
{
    if (seedCertificationProfile)
    {
        return false;
    }

    bool? configured = configuration.GetValue<bool?>("Seed:DemoClients");
    if (configured.HasValue)
    {
        return configured.Value;
    }

    return hostEnvironment.IsDevelopment()
        || hostEnvironment.IsEnvironment("Testing")
        || seedDevData;
}

// Same file, SeedIsotopesApiResourceClientAsync. ShouldSeedDemoClients returned this secret
// whenever Seed:DemoClients was set, including Production.
const string clientSecret = "isotopes-api-resource-secret";
