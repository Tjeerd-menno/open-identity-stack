using OpenIdentityStack.Application.ApplicationPermissions.Validators;

namespace OpenIdentityStack.Application.Tests.ApplicationPermissions;

public sealed class ApplicationPermissionManifestValidatorTests
{
    [Fact]
    public void Validate_WithValidManifest_ReturnsSuccess()
    {
        Result result = ApplicationPermissionManifestValidator.Validate(ValidManifest(), "https://orders.example.com/api");

        result.IsSuccess.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("1")]
    [InlineData("1.1.0")]
    public void Validate_WithUnsupportedSchemaVersion_ReturnsValidationError(string schemaVersion)
    {
        PermissionManifestDocument manifest = ValidManifest() with { SchemaVersion = schemaVersion };

        Result result = ApplicationPermissionManifestValidator.Validate(manifest, null);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Validation.PermissionManifest.SchemaVersionUnsupported");
    }

    [Theory]
    [InlineData("1.0")]
    [InlineData("01.0.0")]
    [InlineData("1.0.0+build.1")]
    public void Validate_WithInvalidApplicationVersion_ReturnsValidationError(string version)
    {
        PermissionManifestDocument manifest = ValidManifest() with
        {
            Application = ValidManifest().Application with { Version = version },
        };

        Result result = ApplicationPermissionManifestValidator.Validate(manifest, null);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Validation.PermissionManifest.ApplicationVersionInvalid");
    }

    [Theory]
    [InlineData("Orders")]
    [InlineData("or")]
    [InlineData("orders_api")]
    public void Validate_WithInvalidApplicationId_ReturnsValidationError(string applicationId)
    {
        PermissionManifestDocument manifest = ValidManifest() with
        {
            Application = ValidManifest().Application with { Id = applicationId },
        };

        Result result = ApplicationPermissionManifestValidator.Validate(manifest, null);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Validation.PermissionManifest.ApplicationIdInvalid");
    }

    [Theory]
    [InlineData("order")]
    [InlineData("order:*")]
    [InlineData("order:item:read")]
    [InlineData("Order:Read")]
    public void Validate_WithInvalidPermissionKey_ReturnsValidationError(string permissionKey)
    {
        PermissionManifestDocument manifest = ValidManifest() with
        {
            Permissions =
            [
                new PermissionManifestPermissionDeclaration(permissionKey, "Read order", null, null),
            ],
        };

        Result result = ApplicationPermissionManifestValidator.Validate(manifest, null);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Validation.PermissionManifest.PermissionKeyInvalid");
    }

    [Fact]
    public void Validate_WithDuplicatePermissionKeys_ReturnsValidationError()
    {
        PermissionManifestDocument manifest = ValidManifest() with
        {
            Permissions =
            [
                new PermissionManifestPermissionDeclaration("order:read", "Read order", null, null),
                new PermissionManifestPermissionDeclaration("order:read", "Read order duplicate", null, null),
            ],
        };

        Result result = ApplicationPermissionManifestValidator.Validate(manifest, null);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Validation.PermissionManifest.DuplicatePermissionKeys");
    }

    [Theory]
    [InlineData("https://orders.example.com/api?x=1")]
    [InlineData("https://orders.example.com/api#fragment")]
    [InlineData("ftp://orders.example.com/api")]
    public void Validate_WithInvalidManifestBaseUrl_ReturnsValidationError(string manifestBaseUrl)
    {
        Result result = ApplicationPermissionManifestValidator.Validate(ValidManifest(), manifestBaseUrl);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Validation.PermissionManifest.ManifestBaseUrlInvalid");
    }

    private static PermissionManifestDocument ValidManifest()
    {
        return new PermissionManifestDocument(
            "1.0.0",
            new PermissionManifestApplicationDeclaration("orders-api", "Orders API", "Order management", "1.2.0-beta.1"),
            [new PermissionManifestPermissionDeclaration("order:read", "Read order", "Allows reading orders", "Orders")]);
    }
}
