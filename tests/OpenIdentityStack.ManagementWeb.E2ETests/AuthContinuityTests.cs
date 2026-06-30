using Microsoft.Playwright;
using OpenIdentityStack.ManagementWeb.E2ETests.Fixtures;

namespace OpenIdentityStack.ManagementWeb.E2ETests;

/// <summary>
/// Verifies the ManagementWeb shell stays reachable across reloads while the real OIDC
/// session (established in the base class sign-in) persists in session storage.
/// </summary>
public sealed class AuthContinuityTests : ManagementWebPageTest
{
    public AuthContinuityTests(ManagementWebAppHostFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task ManagementWebShellRemainsReachableAcrossReloads()
    {
        await GotoAsync("/");
        await Page.GetByRole(AriaRole.Heading, new() { Name = "Overview", Exact = true }).WaitForAsync();
        await Page.GetByRole(AriaRole.Navigation, new() { Name = "Management navigation", Exact = true }).WaitForAsync();

        await Page.ReloadAsync();

        await Page.GetByRole(AriaRole.Heading, new() { Name = "Overview", Exact = true }).WaitForAsync();
        await Page.GetByText("OpenIdentity").First.WaitForAsync();
    }
}
