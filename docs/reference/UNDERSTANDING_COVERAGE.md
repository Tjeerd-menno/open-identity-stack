# Understanding Code Coverage Reports

This guide helps you interpret and act on code coverage reports generated for the OpenIdentityStack solution.

## Coverage Report Structure

When you run `./scripts/coverage.sh` or `./scripts/coverage.ps1`, several files are generated:

```
coverage-report/
├── index.html              # Main coverage dashboard (open this in browser)
├── Summary.txt             # Text summary of coverage by class
├── Summary.md              # Markdown summary
├── badge_linecoverage.svg  # Coverage badge image
├── badge_branchcoverage.svg
└── OpenIdentityStack.*.html     # Per-class coverage details
```

## Reading the Dashboard (index.html)

### Main Metrics

| Metric | What It Means | Good Target |
|--------|---------------|-------------|
| **Line Coverage** | % of code lines executed during tests | 80%+ |
| **Branch Coverage** | % of decision branches tested (if/else) | 70%+ |
| **Method Coverage** | % of methods called at least once | 75%+ |

### Color Coding

- 🟢 **Green (80-100%)** - Excellent coverage
- 🟡 **Yellow (60-79%)** - Acceptable but improvable
- 🟠 **Orange (40-59%)** - Needs attention
- 🔴 **Red (0-39%)** - Critical gap

## Understanding Coverage Percentages

### Line Coverage

```csharp
public Result<User> CreateUser(string email, string name)
{
    if (string.IsNullOrEmpty(email))        // Line 1 ✅
        return UserErrors.EmailRequired;    // Line 2 ✅
    
    if (string.IsNullOrEmpty(name))         // Line 3 ❌ Not covered
        return UserErrors.NameRequired;     // Line 4 ❌ Not covered
    
    return User.Create(email, name);        // Line 5 ✅
}
```

**Line Coverage: 60%** (3 of 5 lines covered)

The test only validates the email check, missing the name validation path.

### Branch Coverage

```csharp
public string GetStatus(bool isActive, bool isVerified)
{
    if (isActive && isVerified)             // Branch point
        return "Active";                     // Branch 1 ✅
    else
        return "Inactive";                   // Branch 2 ✅
}
```

**Branch Coverage: 100%** (both paths tested)

To achieve 100% branch coverage, you need tests for:
- `isActive=true, isVerified=true` → "Active"
- `isActive=false, isVerified=false` → "Inactive"
- `isActive=true, isVerified=false` → "Inactive"
- `isActive=false, isVerified=true` → "Inactive"

## Common Patterns in Coverage Reports

### Pattern 1: High Method Coverage, Low Line Coverage

```
Method Coverage: 95%
Line Coverage: 45%
```

**Meaning:** Most methods are called, but many code paths within methods aren't tested.

**Action:** Focus on testing all branches and paths within existing test methods.

### Pattern 2: High Line Coverage, Low Branch Coverage

```
Line Coverage: 85%
Branch Coverage: 40%
```

**Meaning:** Most code is executed, but error/edge cases aren't tested.

**Action:** Add tests for:
- Error conditions
- Validation failures
- Edge cases (null, empty, boundary values)

### Pattern 3: 0% Coverage on Entire Feature

```
ClientRepository: 0%
CreateClientUseCase: 0%
UpdateClientUseCase: 0%
```

**Meaning:** Feature is completely untested.

**Action:** This is the highest priority. Start with basic happy-path tests, then expand to edge cases.

## Drilling Down into Low Coverage

### Step 1: Find the Class

1. Open `coverage-report/index.html`
2. Click on assembly (e.g., "OpenIdentityStack.Infrastructure")
3. Find classes with low coverage (red/orange)

### Step 2: View Details

Click on a class name to see line-by-line coverage:

```csharp
1  ✅ public class ClientRepository : IClientRepository
2  ✅ {
3  ✅     private readonly OpenIdentityStackDbContext _context;
4
5  ✅     public ClientRepository(OpenIdentityStackDbContext context)
6  ✅     {
7  ✅         _context = context;
8  ✅     }
9
10 ✅     public async Task<Client?> GetByIdAsync(ClientId id, CancellationToken ct)
11 ✅     {
12 ✅         return await _context.Clients
13 ✅             .FirstOrDefaultAsync(c => c.Id == id, ct);
14 ✅     }
15
16 ❌     public async Task<Client?> GetByNameAsync(string name, CancellationToken ct)
17 ❌     {
18 ❌         return await _context.Clients
19 ❌             .FirstOrDefaultAsync(c => c.Name == name, ct);
20 ❌     }
21 }
```

**Analysis:**
- `GetByIdAsync` is fully tested ✅
- `GetByNameAsync` has no tests ❌

**Action:** Add test for `GetByNameAsync`.

### Step 3: Identify Missing Tests

Look for patterns:
- ❌ All lines red → No tests exist
- ❌ Red lines in if/else → Missing branch tests
- ❌ Red lines in error handling → Missing error case tests
- ❌ Red lines in loops → Missing iteration tests

## What Coverage Doesn't Tell You

### ⚠️ High Coverage ≠ Good Tests

```csharp
// Bad test with 100% coverage
[Fact]
public void CreateUser_ShouldNotThrow()
{
    var result = User.Create("test@test.com", "Test User");
    // Achieves 100% coverage but validates nothing!
}

// Good test with 100% coverage
[Fact]
public void CreateUser_WithValidData_ShouldSucceed()
{
    var result = User.Create("test@test.com", "Test User");
    
    result.IsSuccess.ShouldBeTrue();
    result.Value.Email.ShouldBe("test@test.com");
    result.Value.DisplayName.ShouldBe("Test User");
}
```

### Coverage Doesn't Measure

- ❌ **Test Quality** - Tests could pass without asserting anything
- ❌ **Test Maintainability** - Brittle tests can break easily
- ❌ **Integration Testing** - Components working together
- ❌ **Performance** - Code might be slow
- ❌ **Security** - Vulnerabilities aren't caught

## Improving Coverage Strategically

### Priority Framework

1. **Critical Business Logic** (Highest Priority)
   - Payment processing
   - Authentication/Authorization
   - Data validation

2. **Core Features** (High Priority)
   - CRUD operations
   - User workflows
   - API endpoints

3. **Edge Cases** (Medium Priority)
   - Error handling
   - Boundary conditions
   - Null/empty scenarios

4. **Nice to Have** (Lower Priority)
   - Logging
   - Configuration
   - Helper utilities

### Don't Over-Test

Some code doesn't need 100% coverage:

- **Generated Code** - EF migrations, auto-generated DTOs
- **Simple Properties** - Auto-properties with no logic
- **Framework Code** - Don't test ASP.NET Core, EF Core itself
- **Obsolete Code** - Marked for deletion

## Taking Action

### For Classes with 0% Coverage

1. **Start Simple:** Basic happy-path test
2. **Add Error Cases:** Null, empty, invalid inputs
3. **Add Edge Cases:** Boundaries, special characters
4. **Add Integration:** Test with dependencies

### For Classes with Partial Coverage

1. **View Coverage Details:** Find red lines
2. **Identify Patterns:** Missing branches? Error paths?
3. **Add Targeted Tests:** Cover specific gaps
4. **Verify:** Re-run coverage to confirm

### Example: Improving ClientRepository from 40% to 90%

**Initial Coverage (40%):**
```
✅ GetByIdAsync - tested
❌ GetByNameAsync - not tested
❌ ListAsync - not tested
❌ UpdateAsync - not tested
❌ DeleteAsync - not tested
```

**Step 1:** Add GetByNameAsync test (+12% coverage)
**Step 2:** Add ListAsync test (+12% coverage)
**Step 3:** Add UpdateAsync test (+13% coverage)
**Step 4:** Add DeleteAsync test (+13% coverage)
**Final Coverage:** 90%

## Using Coverage in Code Reviews

### Before Merging

1. ✅ Run coverage on PR branch
2. ✅ Check coverage didn't decrease
3. ✅ Verify new code has >80% coverage
4. ✅ Review coverage report for critical paths

### Coverage Gates

Set up automated checks:

```yaml
# Example: Fail PR if coverage drops below 75%
if coverage < 75%:
  fail_build()
```

## Tools and Commands

### Generate Coverage Report
```bash
./scripts/coverage.sh              # Linux/macOS
./scripts/coverage.ps1             # Windows
```

### View Coverage for Specific Project
```bash
dotnet test tests/OpenIdentityStack.Infrastructure.Tests \
  --coverage \
  --coverage-output-format cobertura
```

### Compare Coverage Over Time
```bash
# Baseline
./scripts/coverage.sh > baseline.txt

# After changes
./scripts/coverage.sh > after.txt

# Compare
diff baseline.txt after.txt
```

## Best Practices

### ✅ Do

- Focus on business-critical code first
- Test behavior, not implementation
- Write meaningful assertions
- Cover happy path AND error paths
- Use coverage to find gaps, not as only metric

### ❌ Don't

- Chase 100% coverage at all costs
- Write tests just to increase numbers
- Test framework code or generated code
- Ignore test quality for coverage quantity
- Skip testing because coverage is "good enough"

## Troubleshooting

### "Coverage shows 0% but I have tests"

**Cause:** Tests aren't calling the code, or coverage tool not configured.

**Fix:**
1. Verify tests actually run: `dotnet test --verbosity normal`
2. Check test fixture setup
3. Ensure code is actually being called

### "Coverage report not generating"

**Cause:** Coverage tool not installed or path issues.

**Fix:**
```bash
dotnet tool install --global dotnet-reportgenerator-globaltool
dotnet tool update --global dotnet-reportgenerator-globaltool
```

### "Coverage decreased after refactoring"

**Cause:** Code was reorganized and tests didn't move.

**Fix:**
1. Find which classes lost coverage
2. Update tests to target new structure
3. Add tests for newly exposed code

## Resources

- [CODE_COVERAGE_ANALYSIS.md](CODE_COVERAGE_ANALYSIS.md) - Detailed analysis
- [CODE_COVERAGE_GUIDE.md](CODE_COVERAGE_GUIDE.md) - Testing patterns
- [CODE_COVERAGE_SUMMARY.md](CODE_COVERAGE_SUMMARY.md) - Quick reference
- [ReportGenerator Documentation](https://reportgenerator.io/)
- [Cobertura Format Specification](https://cobertura.github.io/cobertura/)

---

**Remember:** Coverage is a tool to find untested code, not a goal in itself. Focus on writing meaningful tests that verify behavior and catch bugs.
