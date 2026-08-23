namespace OpenIdentityStack.Contract.Tests.CurrentUser;

public sealed class CurrentUserEndpointContractTests
{
    [Fact]
    public void Contract_DefinesCurrentUserEndpointAndSecurity()
    {
        string contract = ReadContract();

        contract.ShouldContain("/api/me:");
        contract.ShouldContain("operationId: getCurrentUser");
        contract.ShouldContain("bearerAuth:");
        contract.ShouldContain("'401':");
    }

    [Fact]
    public void Contract_DefinesCurrentUserResponseShape()
    {
        string contract = ReadContract();

        contract.ShouldContain("CurrentUserResponse:");
        contract.ShouldContain("required:");
        contract.ShouldContain("- subject");
        contract.ShouldContain("- userName");
        contract.ShouldContain("- displayName");
        contract.ShouldContain("- email");
        contract.ShouldContain("- permissions");
    }

    private static string ReadContract()
    {
        string directory = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(directory))
        {
            string candidate = Path.Combine(directory, "contracts", "openapi", "008-current-user-permissions", "current-user.openapi.yaml");
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            string? parent = Directory.GetParent(directory)?.FullName;
            if (parent == directory)
            {
                break;
            }

            directory = parent ?? string.Empty;
        }

        throw new FileNotFoundException("Could not locate current-user OpenAPI contract.");
    }
}
