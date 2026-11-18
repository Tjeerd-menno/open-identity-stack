# Admin API release train and 1.x compatibility

## Decision

Beginning with 1.0.0, the documented Admin API is a public contract for external administrative tooling. Management Web, the shared frontend client, and the API may still ship together. External callers can upgrade within 1.x without adopting those components or updating their requests in lockstep.

The 1.0 release records a baseline of the Admin API operations exported by the server. Pull requests compare the candidate API with the baseline. Reviewers also check changes in authorization, error semantics, and other behavior that an OpenAPI comparison cannot observe. Compatible additions become supported when released. Release notes announce deprecations and their replacements. Removal of a supported interface requires a later major release. The operator and integrator policy also covers security corrections. [1.x compatibility contract](../reference/compatibility.md)

## Maintaining the OpenAPI baseline

The first snapshot lives at `contracts/openapi/baselines/1.0.0/admin-api.json`. When a 1.x release publishes changes to the Admin API, capture the generated document in a new `<release>/admin-api.json` directory alongside it. Keep previous snapshots intact. PR validation compares the runtime document to every 1.x snapshot, including when an OpenAPI document's version changes.

To reproduce the check after building the solution, put oasdiff 1.15.0 on your path and run these commands from the repository root in Bash. Set the output to an existing directory outside the snapshots.

```bash
export OIS_ADMIN_OPENAPI_OUTPUT="${TMPDIR:-/tmp}/admin-api.json"
dotnet test --test-modules "tests/OpenIdentityStack.Api.Tests/bin/Debug/net11.0/OpenIdentityStack.Api.Tests.dll" --no-build --no-restore -- --filter-class OpenIdentityStack.Api.Tests.Admin.AdminOpenApiExportTests
python3 scripts/ci/check-admin-api-compatibility.py --base-ref origin/main "$OIS_ADMIN_OPENAPI_OUTPUT"
```

## History

Before product release 1.0, a coordinated release of the Admin API, shared frontend client, and Management Web could introduce non-versioned breaking changes. That decision enabled the pre-release migration from Clients and Service Accounts to Applications. Individual OpenAPI documents also carried their own `info.version` during development. The product's major version governs the 1.x compatibility promise. [Applications migration](../applications-migration.md)
