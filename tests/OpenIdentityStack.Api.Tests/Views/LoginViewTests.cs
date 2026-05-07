using System.Net;
using OpenIdentityStack.Api.Tests.Fixtures;

namespace OpenIdentityStack.Api.Tests.Views;

/// <summary>
/// Integration tests for the Login Razor view.
/// These tests verify the Login view renders correctly with expected content.
/// </summary>
[Collection("AppHostFixtureCollection")]
public class LoginViewTests
{
    private readonly AppHostFixture _fixture;

    public LoginViewTests(AppHostFixture fixture)
    {
        this._fixture = fixture;
    }

    [Fact]
    public async Task Login_Get_ReturnsSuccessStatusCode()
    {
        // Act
        HttpResponseMessage response = await this._fixture.HttpClient!.GetAsync("/Account/Login");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Login_Get_ReturnsHtmlContentType()
    {
        // Act
        HttpResponseMessage response = await this._fixture.HttpClient!.GetAsync("/Account/Login");

        // Assert
        response.Content.Headers.ContentType?.MediaType.ShouldBe("text/html");
    }

    [Fact]
    public async Task Login_Get_ContainsLoginForm()
    {
        // Act
        HttpResponseMessage response = await this._fixture.HttpClient!.GetAsync("/Account/Login");
        string content = await response.Content.ReadAsStringAsync();

        // Assert
        content.ShouldContain("<form");
        content.ShouldContain("method=\"post\"", Case.Insensitive);
    }

    [Fact]
    public async Task Login_Get_ContainsEmailInputField()
    {
        // Act
        HttpResponseMessage response = await this._fixture.HttpClient!.GetAsync("/Account/Login");
        string content = await response.Content.ReadAsStringAsync();

        // Assert
        content.ShouldContain("Email", Case.Insensitive);
        content.ShouldContain("type=\"email\"", Case.Insensitive);
    }

    [Fact]
    public async Task Login_Get_ContainsPasswordInputField()
    {
        // Act
        HttpResponseMessage response = await this._fixture.HttpClient!.GetAsync("/Account/Login");
        string content = await response.Content.ReadAsStringAsync();

        // Assert
        content.ShouldContain("Password", Case.Insensitive);
        content.ShouldContain("type=\"password\"", Case.Insensitive);
    }

    [Fact]
    public async Task Login_Get_ContainsRememberMeCheckbox()
    {
        // Act
        HttpResponseMessage response = await this._fixture.HttpClient!.GetAsync("/Account/Login");
        string content = await response.Content.ReadAsStringAsync();

        // Assert
        content.ShouldContain("RememberMe", Case.Insensitive);
        content.ShouldContain("type=\"checkbox\"", Case.Insensitive);
    }

    [Fact]
    public async Task Login_Get_ContainsSubmitButton()
    {
        // Act
        HttpResponseMessage response = await this._fixture.HttpClient!.GetAsync("/Account/Login");
        string content = await response.Content.ReadAsStringAsync();

        // Assert
        content.ShouldContain("type=\"submit\"", Case.Insensitive);
    }

    [Fact]
    public async Task Login_Get_ContainsAntiForgeryToken()
    {
        // Act
        HttpResponseMessage response = await this._fixture.HttpClient!.GetAsync("/Account/Login");
        string content = await response.Content.ReadAsStringAsync();

        // Assert
        // Anti-forgery token is rendered as a hidden input with name __RequestVerificationToken
        content.ShouldContain("__RequestVerificationToken", Case.Insensitive);
    }

    [Fact]
    public async Task Login_Get_WithReturnUrl_PreservesReturnUrl()
    {
        // Arrange
        string returnUrl = "/dashboard";

        // Act
        HttpResponseMessage response = await this._fixture.HttpClient!.GetAsync($"/Account/Login?returnUrl={Uri.EscapeDataString(returnUrl)}");
        string content = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        // The form action should contain the returnUrl
        content.ShouldContain(returnUrl);
    }

    [Fact]
    public async Task Login_Get_ContainsLoginTitle()
    {
        // Act
        HttpResponseMessage response = await this._fixture.HttpClient!.GetAsync("/Account/Login");
        string content = await response.Content.ReadAsStringAsync();

        // Assert
        // The page should have a title or heading indicating it's a login page
        content.ShouldContain("Login", Case.Insensitive);
    }

    [Fact]
    public async Task Login_Get_ContainsValidationSummaryPlaceholder()
    {
        // Act
        HttpResponseMessage response = await this._fixture.HttpClient!.GetAsync("/Account/Login");
        string content = await response.Content.ReadAsStringAsync();

        // Assert
        // Validation summary is typically rendered as a div with asp-validation-summary
        content.ShouldContain("validation-summary", Case.Insensitive);
    }
}
