# Code Coverage Quick Reference

## Current Status (2026-02-02)

| Layer | Coverage | Trend |
|-------|----------|-------|
| **Domain** | 91.6% | ✅ Excellent |
| **Application** | 53.7% | ⚠️ Needs Improvement |
| **Infrastructure** | 11.1% | ❌ Critical |
| **Overall** | 30.1% | ⚠️ Below Target |

## Top Priority Areas (0% Coverage)

### Infrastructure Layer
1. **Client Management** (Complete feature, 0% coverage)
   - ClientRepository
   - CreateClientUseCase, UpdateClientUseCase, DeleteClientUseCase
   - GetClientQueryHandler, ListClientsQueryHandler

2. **Authentication Settings** (0% coverage)
   - AuthenticationSettingsRepository
   - GetAuthenticationSettingsQueryHandler
   - SetDefaultProviderUseCase, SetLocalFallbackUseCase

3. **Federation** (Partial, 0% in places)
   - CreateProviderUseCase
   - ListProvidersQueryHandler

### Application Layer
1. **Service Account Operations** (0% coverage)
   - UpdateServiceAccountUseCase
   - DeleteServiceAccountUseCase
   - Enable/DisableServiceAccountUseCase
   - GetServiceAccountQueryHandler, ListServiceAccountsQueryHandler

2. **Session Management** (0% coverage)
   - CreateSessionUseCase
   - Various query handlers

3. **Role Management** (0% coverage)
   - CreateRoleUseCase
   - ListRolesQueryHandler, GetUserRolesQueryHandler

## Quick Commands

```bash
# Generate coverage report
./scripts/coverage.sh              # Linux/macOS
./scripts/coverage.ps1             # Windows

# Run tests without coverage
dotnet test

# Run specific test project
dotnet test tests/OpenIdentityStack.Infrastructure.Tests
```

## Goals

| Timeframe | Target | Focus Areas |
|-----------|--------|-------------|
| 4 weeks | 65% | Infrastructure + Application critical paths |
| 8 weeks | 80% | Complete feature coverage + edge cases |
| Long-term | 85%+ | Maintain with new features |

## Resources

- [📊 Full Coverage Analysis](CODE_COVERAGE_ANALYSIS.md)
- [📖 Coverage Guide & Examples](CODE_COVERAGE_GUIDE.md)
- [🔧 Scripts](../scripts/)

## Test Execution Status

⚠️ **Note**: 496 tests are currently failing due to Aspire orchestrator connection issues in E2E and API integration tests. These are infrastructure issues, not code quality problems. The failing tests don't affect coverage metrics for Domain, Application, and Infrastructure unit tests.

**Working Tests**: 963 passed (Domain, Application, Infrastructure unit tests)  
**Failing Tests**: 496 (E2E and API integration tests - Aspire DCP issues)

---

Last Updated: 2026-02-02
