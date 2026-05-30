# Application Permission Transaction Notes

This note documents the transaction boundary assumptions that have shown up in the application-permission registry workflow.

## Why This Exists

The registry workflow mixes:

- aggregate updates in `ApplicationPermissionMaintenanceUseCases`
- manifest apply and destructive change flows in `RegisterApplicationUseCase` (`IRegisterPermissionManifestUseCase`)
- permission assignment validation through `IPermissionAssignmentValidator`
- infrastructure-side projection and persistence work

That combination is sensitive to operation ordering during delete and manifest-apply paths.

## Working Rule

Use an explicit transaction coordination design for flows that can:

- remove permissions
- delete or disable registered applications
- trigger assignment cleanup
- coordinate repository writes with projection or follow-up persistence work

Do not rely on no-op transaction defaults for these paths. Treat missing transaction coordination as a design smell, not a convenience.

## Review Checklist

When changing these use cases, check:

- whether repository writes and assignment cleanup happen inside one coordinated boundary
- whether projection work can execute before the original command completes
- whether a delete or manifest sync can issue nested database work on the same connection
- whether the tests cover both the success path and the ordering-sensitive edge case

## Test Expectations

The best regression tests for this area are:

- application-layer tests that verify the transaction runner is part of the path
- infrastructure tests for the concrete transaction runner
- E2E tests that keep destructive flows visible when backend ordering breaks
