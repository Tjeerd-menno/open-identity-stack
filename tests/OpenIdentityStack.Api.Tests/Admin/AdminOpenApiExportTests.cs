using Microsoft.OpenApi;
using OpenIdentityStack.Api.Tests.Fixtures;

namespace OpenIdentityStack.Api.Tests.Admin;

public sealed class AdminOpenApiExportTests(AppHostFixture fixture)
{
    [Fact]
    public async Task Document_DescribesOnlyAdminApiOperations()
    {
        OpenApiDocument document = await fixture.GetAdminOpenApiDocumentAsync();

        Assert.Contains("/api/admin/applications", document.Paths.Keys);
        Assert.Contains("/api/admin/users", document.Paths.Keys);
        Assert.All(document.Paths.Keys, path =>
            Assert.True(path == "/api/admin" || path.StartsWith("/api/admin/", StringComparison.Ordinal), path));

        string? outputPath = Environment.GetEnvironmentVariable("OIS_ADMIN_OPENAPI_OUTPUT");
        if (!string.IsNullOrEmpty(outputPath))
        {
            await using FileStream output = File.Create(outputPath);
            await document.SerializeAsJsonAsync(output, OpenApiSpecVersion.OpenApi3_0, TestContext.Current.CancellationToken);
        }
    }
}
