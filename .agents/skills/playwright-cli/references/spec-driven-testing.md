# Spec-driven testing for .NET (plan → generate → heal)

End-to-end workflow for authoring and maintaining Playwright tests in .NET using xUnit v3 and Aspire integration testing. The three sections below can be used independently:

- **Planning** — explore the app via live manual testing, produce a spec file describing what to test.
- **Generate** — turn a spec into xUnit test files using C# Playwright. Update the spec if it's vague or stale.
- **Heal** — diagnose failing tests, fix the code, reconcile the spec with reality.

All three lean on Aspire's test infrastructure: the fixture manages app startup, database seeding, and browser lifecycle. See [PLAYWRIGHT_PATTERNS.md](#references) for selector and waiting best practices from the AdminWeb E2E suite.

---

## 1. Planning

Goal: produce a spec file (e.g. `specs/<feature>.plan.md`) that enumerates the scenarios to test. **Always** write the spec to a file.

### 1.1 Prerequisite: Aspire test fixture

Check the project has an Aspire AppHost fixture before anything else:

```bash
# Confirm the fixture exists and has dependencies
test -f tests/OpenIdentityStack.AdminWeb.E2ETests/Fixtures/AdminWebAppHostFixture.cs
dotnet --version  # .NET 10 or later
```

If no fixture exists, create one using [AdminWebAppHostFixture.cs](../../../tests/OpenIdentityStack.AdminWeb.E2ETests/Fixtures/AdminWebAppHostFixture.cs) as a template. Key patterns:

- Decorate with `[AssemblyFixture(typeof(YourFixture))]` so it initializes once per test run.
- Inherit `IAsyncLifetime` for async setup/teardown.
- Start the Aspire app, seed test data, initialize Playwright Chromium, and expose URLs.
- Call `EnsureChromiumRuntimeIsAvailable()` — fail fast if browsers aren't installed.

### 1.2 Prerequisite: seed scenario

A **seed scenario** is a minimal test method that lands the page in the state every scenario starts from: navigation to the app, any required login, feature flags, etc. Scenarios assume a fresh start *after* the seed.

Minimum viable seed (in a new test class):

```csharp
// tests/OpenIdentityStack.AdminWeb.E2ETests/Features/SeedTests.cs
using Microsoft.Playwright;
using OpenIdentityStack.AdminWeb.E2ETests.Fixtures;

namespace OpenIdentityStack.AdminWeb.E2ETests.Features;

/// <summary>
/// Seed test: navigates to the app and waits for the authenticated shell.
/// Serves as the starting point for all scenario exploration.
/// </summary>
public class SeedTests : IAsyncLifetime
{
    private readonly AdminWebAppHostFixture fixture;
    private IBrowserContext? context;
    private IPage? page;

    public SeedTests(AdminWebAppHostFixture fixture)
    {
        this.fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        context = await fixture.CreateBrowserContextAsync();
        page = await context.NewPageAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (page != null) await page.CloseAsync();
        if (context != null) await context.CloseAsync();
    }

    [Fact]
    public async Task Seed_NavigateToApp()
    {
        // Seed test: just navigate and wait for app shell
        // Exploration will happen manually from here
        string url = fixture.AdminWebUrl ?? throw new InvalidOperationException("AdminWeb URL is null");
        await page!.GotoAsync(url, new() { WaitUntil = WaitUntilState.DOMContentLoaded });
        await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
    }
}
```

### 1.3 Explore the app manually

1. **Start the Aspire stack:**
   ```bash
   dotnet run --project src/OpenIdentityStack.AppHost
   ```
   The dashboard opens automatically. Wait for the `api` and `postgres` resources to be healthy.

2. **Run the seed test:**
   ```bash
   dotnet test --project tests/OpenIdentityStack.AdminWeb.E2ETests/OpenIdentityStack.AdminWeb.E2ETests.csproj -- --filter "SeedTests.Seed_NavigateToApp"
   ```
   This starts the browser and navigates to the app (it will pass immediately).

3. **While the Aspire app is running, open a browser manually:**
   - Navigate to the AdminWeb URL (visible in the Aspire dashboard).
   - Interact with the UI to map out flows, forms, modals, and navigation.
   - Test edge cases: empty states, validation errors, long input, boundary values.
   - Check persistence: reload the page, verify session state, check URL fragments.
   - Note which interactions trigger network requests and state changes.

4. **Stop the test when done:**
   ```bash
   # Ctrl+C to stop the Aspire stack
   ```

Map out:

- **Interactive surfaces:** forms, buttons, lists, filters, modals, dialogs.
- **Primary user journeys:** happy path flows end-to-end.
- **Edge cases:** empty states, validation errors, very long input, boundary values.
- **Persistence:** reload, session/local storage, URL fragments, back/forward behaviour.
- **Navigation:** which controls change the URL, breadcrumbs, sidebar links.

### 1.4 Write the spec file

Save under `specs/<feature>.plan.md`. Use this structure:

```markdown
# <Feature> Test Plan

## Application Overview

<One paragraph describing what the feature does and why it matters. Reference the AdminWeb URL and any prerequisite setup.>

## Test Scenarios

### 1. <Group Name>

**Fixture:** `AdminWebAppHostFixture`

**Class:** `<GroupName>Tests`

#### 1.1. <kebab-case-scenario-name>

**File:** `tests/OpenIdentityStack.AdminWeb.E2ETests/Features/<kebab-case-scenario-name>.cs`

**Test Method:** `<PascalCaseScenarioName>`

**Steps:**
  1. <Concrete user step>
    - expect: <observable outcome>
    - expect: <another observable outcome>
  2. <Next step>
    - expect: <outcome>

#### 1.2. <next-scenario>
...

### 2. <Next Group>

**Fixture:** `AdminWebAppHostFixture`

**Class:** `<NextGroupName>Tests`
...
```

Guidelines:

- Each scenario is independent and starts from a fresh authenticated shell — never chain scenarios.
- Scenario names are kebab-case; test method names are PascalCase (`should-add-user` → `ShouldAddUser()`).
- Cover happy path, edge cases, validation, negative flows, and persistence.
- Write steps at the user level ("Type 'admin@example.com' into the email field"), not the API level ("call `Fill()`").
- Put observable outcomes in `- expect:` bullets; each becomes an assertion during generation.
- Reference the fixture and class names so generation stays consistent.

---

## 2. Generate

Goal: take a spec file and produce xUnit test files. Optionally update the spec if it has drifted.

### 2.1 Inputs

- **Spec file**, e.g. `specs/user-management.plan.md`.
- **Target**: either a single scenario (e.g. `1.2`), a whole group (`1`), or all.
- **Fixture class**, read from the `**Fixture:**` line of the scenario's group.
- **Aspire AppHost** running locally with database migrations applied.

### 2.2 Generate one scenario

For each target scenario, in sequence (do not parallelize — all tests share the same fixture and database):

1. **Start the Aspire stack:**
   ```bash
   dotnet run --project src/OpenIdentityStack.AppHost
   ```

2. **Manually walk through the scenario steps** with the browser open:
   - Navigate to the URL stated in the spec.
   - Perform each step and observe the outcome.
   - If a step is vague ("click the button" — which button?), references an element that no longer exists, or contradicts the app's actual behaviour, use your judgement: update the spec to match what the app really does, then keep going. Editing the spec mid-generation is expected.

3. **For each user-visible action, identify the corresponding Playwright locator:**
   - Use **role-based locators** for accessibility: `page.GetByRole(AriaRole.Button, new() { Name = "Submit" })`
   - For text inputs: `page.GetByLabel("Email")` or `page.GetByRole(AriaRole.Textbox, ...)`
   - For Radix/composite widgets: `page.GetByRole(AriaRole.Combobox, ...)` + inspect `data-state` attributes.
   - Avoid brittle selectors (class names, data-testid). See [PLAYWRIGHT_PATTERNS.md](#references) for examples.

4. **For each `- expect:` bullet, write an assertion using Shouldly:**
   ```csharp
   await expect(page.GetByRole(AriaRole.Heading)).ToContainTextAsync("Welcome");
   // or
   var text = await page.GetByRole(AriaRole.Heading).TextContentAsync();
   text.ShouldContain("Welcome");
   ```

5. **Collect the generated code** and write the test file at the path given in the spec:

```csharp
// spec: specs/user-management.plan.md
// fixture: AdminWebAppHostFixture
// class: UserManagementTests

using Microsoft.Playwright;
using OpenIdentityStack.AdminWeb.E2ETests.Fixtures;
using OpenIdentityStack.AdminWeb.E2ETests.Helpers;
using Shouldly;

namespace OpenIdentityStack.AdminWeb.E2ETests.Features;

/// <summary>
/// E2E tests for user creation and management.
/// Task references: T001, T002
/// </summary>
public class UserManagementTests : IAsyncLifetime
{
    private readonly AdminWebAppHostFixture fixture;
    private IBrowserContext? context;
    private IPage? page;

    public UserManagementTests(AdminWebAppHostFixture fixture)
    {
        this.fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        context = await fixture.CreateBrowserContextAsync();
        page = await context.NewPageAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (page != null) await page.CloseAsync();
        if (context != null) await context.CloseAsync();
    }

    [Fact]
    public async Task ShouldAddUser()
    {
        // 1. Log in as test admin
        await TestHelpers.LoginAsTestAdminAsync(page!, fixture.AdminWebUrl!);

        // 2. Navigate to Users page
        await page!.GetByRole(AriaRole.Link, new() { Name = "Users" }).ClickAsync();
        await page.WaitForURLAsync("**/users", new() { Timeout = 10000 });

        // 3. Click "Add User" button
        await page.GetByRole(AriaRole.Button, new() { Name = "Add User" }).ClickAsync();

        // 4. Fill in user details
        await page.GetByLabel("Email").FillAsync("newuser@example.com");
        await page.GetByLabel("Display Name").FillAsync("New User");

        // 5. Submit the form
        await page.GetByRole(AriaRole.Button, new() { Name = "Create" }).ClickAsync();

        // Expect: confirmation toast appears
        await page.WaitForSelectorAsync("text=User created successfully", new() { Timeout = 5000 });

        // Expect: user appears in the list
        await page.WaitForURLAsync("**/users", new() { Timeout = 10000 });
        var userRow = page.GetByText("newuser@example.com");
        await userRow.IsVisibleAsync().ShouldBeAsync(true);
    }
}
```

Rules:

- **One test class per spec group.** Use the group name from the spec (e.g. `UserManagementTests`).
- **One test method per scenario.** Test names are PascalCase and match the spec's kebab-case scenario name.
- Prefix each numbered step with a `// N. <step text>` comment before its actions.
- Use `IAsyncLifetime` for setup/teardown. Initialize context and page in `InitializeAsync()`, dispose in `DisposeAsync()`.
- Import `Microsoft.Playwright`, `Shouldly`, and helpers from `OpenIdentityStack.AdminWeb.E2ETests.Helpers`.
- Import the fixture class and decorate with `[Fact]` or `[Theory]` from xUnit.
- **Waiting:** Use `WaitForURLAsync()`, `WaitForSelectorAsync()`, `WaitForLoadStateAsync()` — never `Task.Delay()` unless it's a small local fallback (e.g. React render settle).
- **No sleeps as workarounds.** If the test flakes, the issue is likely in the locator, the wait condition, or the backend boundary. Debug with [section 3](#3-heal).

### 2.3 Generate multiple scenarios

Loop 2.2 over the targeted scenarios one at a time. Between scenarios:

- Stop the current test by pressing Ctrl+C in the terminal.
- Stop the Aspire app (Ctrl+C again).
- Verify the database is in a clean state: restart the Aspire app so migrations re-run and seed data refreshes.
- Start the next scenario.

Do not parallelize scenario generation: all tests share the same fixture and database, so serial generation ensures isolation.

### 2.4 Run generated tests

After generation, run the new tests once:

```bash
dotnet test --project tests/OpenIdentityStack.AdminWeb.E2ETests/OpenIdentityStack.AdminWeb.E2ETests.csproj -- --filter "UserManagementTests.ShouldAddUser"
```

Any failure goes to Section 3. If all pass:

```bash
dotnet test --project tests/OpenIdentityStack.AdminWeb.E2ETests/OpenIdentityStack.AdminWeb.E2ETests.csproj -- --filter "UserManagementTests"
```

---

## 3. Heal

Goal: fix failing tests, and update the spec if the app's intended behaviour changed.

### 3.1 Find failing tests

```bash
dotnet test --project tests/OpenIdentityStack.AdminWeb.E2ETests/OpenIdentityStack.AdminWeb.E2ETests.csproj
```

Note the failing test class and method name (e.g. `UserManagementTests.ShouldAddUser`). Process failures one at a time.

### 3.2 Debug one failure

1. **Start the Aspire app:**
   ```bash
   dotnet run --project src/OpenIdentityStack.AppHost
   ```
   Wait for it to be healthy.

2. **Run the failing test:**
   ```bash
   dotnet test --project tests/OpenIdentityStack.AdminWeb.E2ETests/OpenIdentityStack.AdminWeb.E2ETests.csproj -- --filter "UserManagementTests.ShouldAddUser"
   ```

3. **Open the test file and add tracing / debugging:**
   - Add `System.Console.WriteLine()` statements before assertions to log the actual values.
   - Use `page.Screenshot()` to capture the page state at key points:
     ```csharp
     await page.ScreenshotAsync(new() { Path = "debug_screenshot.png" });
     ```
   - Check the Aspire dashboard and API logs for errors or unexpected behaviour.
   - Inspect network requests using browser DevTools (if running headful, set `Headless = false` in the fixture).

4. **Common failure causes:**
   - **Locator not found:** Element name, role, or ARIA label changed in the app. Inspect the live app to find the new selector.
   - **Timeout on wait:** Expected network request didn't fire or took too long. Check the backend for errors or add a shorter `Timeout`.
   - **Assertion text mismatch:** App's text changed (e.g. validation message, button label). Update the test to match the new text.
   - **Test data leaking:** A previous test left data behind. Ensure the fixture's seed clears stale data or uses unique identifiers.
   - **Timing (flakiness):** Transition or async load is slower than expected. Add a stronger wait condition (e.g. wait for the specific table row, not just the page).

### 3.3 Apply the fix

Edit the test file:

- Update the locator (role name, label, ARIA attributes).
- Add a stronger wait condition (wait for the specific outcome, not just the page load).
- Update the assertion text to match the app's current output.
- If a step was completely wrong, update both the test and the spec (see 3.4).

Do not:
- Add `Task.Delay()` as a fix (it masks timing issues and makes tests slow).
- Use `networkidle` (it's fragile and over-waits).
- Skip the test or mark it as `Skip` without documenting the blocker.

Rerun the single test to confirm it passes:

```bash
dotnet test --project tests/OpenIdentityStack.AdminWeb.E2ETests/OpenIdentityStack.AdminWeb.E2ETests.csproj -- --filter "UserManagementTests.ShouldAddUser"
```

### 3.4 Reconcile with the spec

Open the spec referenced by the comment at the top of the test file (e.g. `specs/user-management.plan.md`) and locate the scenario that matches the test.

- **Fix was purely technical** (locator drift, better wait condition, assertion text update) and the spec's user-level behaviour still matches the app → leave the spec alone.
- **Fix changed user-visible steps, inputs, order, or expected outcomes** that the spec describes → update the spec to match reality. Keep the scenario id and file path stable; only the step / expect lines change.
- **Unclear whether the app change is intentional** (spec is stale) **or a regression** (test was right, app is wrong) → **stop and ask the user**. Provide:
  - the scenario id (e.g. `1.2`),
  - the spec lines that no longer match,
  - the observed app behaviour (quote the actual vs. expected, or describe what's on screen).

Only after the user answers, either update the spec (intentional change) or file/flag the test as covering a bug (regression).

### 3.5 Iteration and giving up

- Fix failures one at a time; rerun after each.
- If after thorough investigation you are confident the test is correct but the app is wrong *and* the user has confirmed it's a bug: mark the test with a comment referencing the issue:
  ```csharp
  [Fact(Skip = "Regression: https://github.com/...")]
  public async Task ShouldAddUser()
  {
      // Test is correct. App bug tracked in issue.
  }
  ```
  Never silently skip or delete a test.

---

## Best Practices

### Waiting

- **Always wait on a concrete condition:** `WaitForURLAsync()`, `WaitForSelectorAsync()`, `WaitForLoadStateAsync()`, or `page.IsVisibleAsync()`.
- **Match the exact request:** When waiting for a list reload after a search, wait for the request with the search term in the URL/payload, not just `NetworkIdle`.
- **Keep local `Task.Delay()` small:** Only use short delays (e.g. 100–500ms) for React settle time *after* a stronger wait has already completed.

### Selectors

- **Prefer role-based locators:** `GetByRole(AriaRole.Button, new() { Name = "..." })` — accessible and stable.
- **Use `GetByLabel()` for form inputs:** `page.GetByLabel("Email")` is more readable than counting tabindexes.
- **Avoid data-testid, class names, IDs:** These are brittle and change frequently. Only use as a last resort.
- **Scope duplicate names to containers:** If two dialogs have a "Delete" button, use `dialog.GetByRole(AriaRole.Button, new() { Name = "Delete" })` to scope it.

### Test Structure

- **One scenario per test method.** Use `[Fact]` for deterministic scenarios, `[Theory]` for parameterized variations.
- **Always seed in `InitializeAsync()` and dispose in `DisposeAsync()`:** The fixture manages the app lifecycle; your test manages the browser context/page.
- **No test chaining.** Each test starts from a clean state (fresh browser context, seeded database). Do not rely on test A setting up state for test B.
- **Assertion style:** Use Shouldly for readability:
  ```csharp
  var text = await heading.TextContentAsync();
  text.ShouldContain("Welcome");
  ```

---

## References

| Topic | File |
|---|---|
| Selector and waiting patterns | [PLAYWRIGHT_PATTERNS.md](#../../../tests/OpenIdentityStack.AdminWeb.E2ETests/PLAYWRIGHT_PATTERNS.md) |
| AdminWeb E2E fixture | [AdminWebAppHostFixture.cs](#../../../tests/OpenIdentityStack.AdminWeb.E2ETests/Fixtures/AdminWebAppHostFixture.cs) |
| Test helpers (login, navigation) | [TestHelpers.cs](#../../../tests/OpenIdentityStack.AdminWeb.E2ETests/Helpers/TestHelpers.cs) |
| Example test file | [AuthenticationFlowTests.cs](#../../../tests/OpenIdentityStack.AdminWeb.E2ETests/AuthenticationFlowTests.cs) |
| Aspire testing docs | https://learn.microsoft.com/en-us/dotnet/aspire/testing |
| Microsoft.Playwright API | https://playwright.dev/dotnet/docs/intro |
| xUnit v3 docs | https://xunit.net/ |
