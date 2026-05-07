using System.Net;
using OpenIdentityStack.Api.Tests.Fixtures;

namespace OpenIdentityStack.Api.Tests.Views;

/// <summary>
/// Integration tests for the AccessDenied Razor view.
/// These tests verify the AccessDenied view renders correctly with expected content.
/// </summary>
[Collection("AppHostFixtureCollection")]
public class AccessDeniedViewTests
{
    private readonly AppHostFixture _fixture;

    public AccessDeniedViewTests(AppHostFixture fixture)
    {
        this._fixture = fixture;
    }

    [Fact]
    public async Task AccessDenied_Get_ReturnsSuccessStatusCode()
    {
        // Act
        HttpResponseMessage response = await this._fixture.HttpClient!.GetAsync("/Account/AccessDenied");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AccessDenied_Get_ReturnsHtmlContentType()
    {
        // Act
        HttpResponseMessage response = await this._fixture.HttpClient!.GetAsync("/Account/AccessDenied");

        // Assert
        response.Content.Headers.ContentType?.MediaType.ShouldBe("text/html");
    }

    [Fact]
    public async Task AccessDenied_Get_ContainsAccessDeniedMessage()
    {
        // Act
        HttpResponseMessage response = await this._fixture.HttpClient!.GetAsync("/Account/AccessDenied");
        string content = await response.Content.ReadAsStringAsync();

        // Assert
        content.ShouldContain("Access Denied", Case.Insensitive);
    }

    [Fact]
    public async Task AccessDenied_Get_ContainsExplanationText()
    {
        // Act
        HttpResponseMessage response = await this._fixture.HttpClient!.GetAsync("/Account/AccessDenied");
        string content = await response.Content.ReadAsStringAsync();

        // Assert
        // The page should explain why access was denied
        content.ShouldContain("permission", Case.Insensitive);
    }

    [Fact]
    public async Task AccessDenied_Get_ContainsHomeLink()
    {
        // Act
        HttpResponseMessage response = await this._fixture.HttpClient!.GetAsync("/Account/AccessDenied");
        string content = await response.Content.ReadAsStringAsync();

        // Assert
        // The page should have a link back to home
        content.ShouldContain("href=\"/\"", Case.Insensitive);
    }

    [Fact]
    public async Task AccessDenied_Get_ContainsReturnToHomeText()
    {
        // Act
        HttpResponseMessage response = await this._fixture.HttpClient!.GetAsync("/Account/AccessDenied");
        string content = await response.Content.ReadAsStringAsync();

        // Assert
        // The page should have text suggesting to return home
        content.ShouldContain("home", Case.Insensitive);
    }

    [Fact]
    public async Task AccessDenied_Get_IsValidHtml()
    {
        // Act
        HttpResponseMessage response = await this._fixture.HttpClient!.GetAsync("/Account/AccessDenied");
        string content = await response.Content.ReadAsStringAsync();

        // Assert - Basic HTML structure
        content.ShouldContain("<html", Case.Insensitive);
        content.ShouldContain("</html>", Case.Insensitive);
    }

    [Fact]
    public async Task AccessDenied_Get_DoesNotContainStackTrace()
    {
        // Act
        HttpResponseMessage response = await this._fixture.HttpClient!.GetAsync("/Account/AccessDenied");
        string content = await response.Content.ReadAsStringAsync();

        // Assert - Should not expose stack traces or detailed error info
        content.ShouldNotContain("Exception", Case.Insensitive);
        content.ShouldNotContain("StackTrace", Case.Insensitive);
    }
}
