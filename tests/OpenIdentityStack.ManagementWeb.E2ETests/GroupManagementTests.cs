using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Text.Json.Nodes;
using Microsoft.Playwright;
using OpenIdentityStack.ManagementWeb.E2ETests.Fixtures;

namespace OpenIdentityStack.ManagementWeb.E2ETests;

/// <summary>E2E coverage for the ManagementWeb Groups list and group detail (real admin API).</summary>
public sealed class GroupManagementTests : ManagementWebPageTest
{
    private Guid _groupId;
    private string _groupName = "";
    private string _memberName = "";
    private string _candidateName = "";

    public GroupManagementTests(ManagementWebAppHostFixture fixture) : base(fixture)
    {
    }

    protected override async Task SeedAsync()
    {
        _groupName = $"Engineering {Unique}";
        _memberName = $"Ada Lovelace {Unique}";
        _candidateName = $"Grace Hopper {Unique}";

        JsonNode group = await ApiPostAsync("/api/admin/groups", new
        {
            name = _groupName,
            description = "Platform engineering",
        });
        _groupId = Guid.Parse(group["id"]!.GetValue<string>());

        Guid adaId = await Fixture.SeedUserAsync($"ada.member.{Unique}@northwind.io", _memberName, "Password123!@456");
        await Fixture.SeedUserAsync($"grace.candidate.{Unique}@northwind.io", _candidateName, "Password123!@456");
        HttpResponseMessage addMember = await Api.PostAsync($"/api/admin/groups/{_groupId}/members/{adaId}", content: null);
        addMember.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task OperatorCanBrowseGroupsAndOpenDetail()
    {
        await GotoAsync($"/groups/{_groupId}");
        await Page.GetByRole(AriaRole.Heading, new() { Name = _groupName, Exact = true }).WaitForAsync();
        await Page.GetByRole(AriaRole.Tab, new() { NameRegex = new Regex("Members", RegexOptions.IgnoreCase) }).WaitForAsync();
        await Page.GetByText(_memberName).WaitForAsync();
    }

    [Fact]
    public async Task OperatorCanCreateAGroup()
    {
        await GotoAsync("/groups");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Create group", Exact = true }).ClickAsync();

        ILocator dialog = Page.GetByRole(AriaRole.Dialog);
        string name = $"Design {Unique}";
        await dialog.GetByLabel("Group name").FillAsync(name);
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Create group", Exact = true }).ClickAsync();

        await Page.GetByText(new Regex($"Created {Regex.Escape(name)}", RegexOptions.IgnoreCase)).WaitForAsync();
    }

    [Fact]
    public async Task OperatorCanAddAMemberToAGroup()
    {
        await GotoAsync($"/groups/{_groupId}");
        await Page.GetByRole(AriaRole.Heading, new() { Name = _groupName, Exact = true }).WaitForAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Add members", Exact = true }).ClickAsync();

        ILocator dialog = Page.GetByRole(AriaRole.Dialog);
        await dialog.GetByPlaceholder("Search users…").FillAsync(_candidateName);
        await dialog.GetByText(_candidateName).WaitForAsync();
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Add", Exact = true }).First.ClickAsync();

        await Page.GetByText(new Regex("Member added", RegexOptions.IgnoreCase)).WaitForAsync();
    }

    [Fact]
    public async Task OperatorCanRemoveAMemberFromAGroup()
    {
        await GotoAsync($"/groups/{_groupId}");
        await Page.GetByRole(AriaRole.Heading, new() { Name = _groupName, Exact = true }).WaitForAsync();
        await Page.GetByText(_memberName).WaitForAsync();

        ILocator row = Page.Locator("tbody tr", new() { Has = Page.GetByText(_memberName) }).First;
        await row.GetByRole(AriaRole.Button, new() { Name = "Row actions" }).ClickAsync();
        await Page.GetByRole(AriaRole.Menuitem, new() { Name = "Remove from group", Exact = true }).ClickAsync();

        await ExpectTextAsync("Member removed");
    }

    [Fact]
    public async Task OperatorCanAddAMappingToAGroup()
    {
        await GotoAsync($"/groups/{_groupId}");
        await Page.GetByRole(AriaRole.Tab, new() { NameRegex = new Regex("Mappings", RegexOptions.IgnoreCase) }).ClickAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Add mapping", Exact = true }).ClickAsync();

        ILocator dialog = Page.GetByRole(AriaRole.Dialog);
        await dialog.GetByLabel("Value").FillAsync("platform-admin");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Add mapping", Exact = true }).ClickAsync();

        await Page.GetByText(new Regex("Mapping added", RegexOptions.IgnoreCase)).WaitForAsync();
    }

    [Fact]
    public async Task OperatorCanRemoveAMappingFromAGroup()
    {
        // Seed a claim mapping through the real API so the Mappings tab has one to remove.
        // The endpoint returns no body, so POST directly rather than through ApiPostAsync.
        string mappingValue = $"removable-{Unique}";
        HttpResponseMessage addMapping = await Api.PostAsJsonAsync(
            $"/api/admin/groups/{_groupId}/mappings",
            new { type = "Claim", value = mappingValue });
        addMapping.EnsureSuccessStatusCode();

        await GotoAsync($"/groups/{_groupId}");
        await Page.GetByRole(AriaRole.Tab, new() { NameRegex = new Regex("Mappings", RegexOptions.IgnoreCase) }).ClickAsync();
        await Page.GetByText(mappingValue, new() { Exact = true }).WaitForAsync();

        await Page.GetByRole(AriaRole.Button, new() { Name = "Remove", Exact = true }).First.ClickAsync();
        await Page.GetByText(new Regex("Mapping removed", RegexOptions.IgnoreCase)).WaitForAsync();
        await Page.GetByText(mappingValue, new() { Exact = true }).WaitForAsync(new() { State = WaitForSelectorState.Detached });
    }

    [Fact]
    public async Task OperatorCanEditGroupSettings()
    {
        await GotoAsync($"/groups/{_groupId}");
        await Page.GetByRole(AriaRole.Tab, new() { Name = "Settings", Exact = true }).ClickAsync();

        await Page.GetByLabel("Description").FillAsync("Platform engineering and tooling");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Save changes", Exact = true }).ClickAsync();

        await Page.GetByText(new Regex("Group updated", RegexOptions.IgnoreCase)).WaitForAsync();
    }

    [Fact]
    public async Task OperatorCanDeleteAGroup()
    {
        // Seed a throwaway group so the shared group survives for other tests.
        JsonNode group = await ApiPostAsync("/api/admin/groups", new
        {
            name = $"Disposable {Unique}",
            description = (string?)null,
        });
        var disposableId = Guid.Parse(group["id"]!.GetValue<string>());

        await GotoAsync($"/groups/{disposableId}");
        await Page.GetByRole(AriaRole.Tab, new() { Name = "Settings", Exact = true }).ClickAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Delete group", Exact = true }).ClickAsync();

        await Page.GetByText(new Regex("Group deleted", RegexOptions.IgnoreCase)).WaitForAsync();
        await Page.WaitForURLAsync(new Regex(@"/groups/?$"));
    }
}
