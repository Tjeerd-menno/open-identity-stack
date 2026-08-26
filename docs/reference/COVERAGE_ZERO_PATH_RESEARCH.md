# Zero-Coverage Path Research

Date: 2026-08-23

## Question

Which of the current SonarQube zero-coverage paths are high-value, directly testable seams for focused unit tests?

## Findings

- SonarQube reports 439 files in project `Tjeerd-menno_open-identity-stack`; the first 20 files sorted by overall coverage are at 0%.
- The zero-coverage files include the shared Admin API contract modules under `src/frontend-packages/admin-api-client/src` and the Management Web adapter at `src/OpenIdentityStack.ManagementWeb/src/lib/api.ts`.
- The Admin API contract modules are thin, public adapters over the `AdminApiClient` interface. Their observable behavior is the HTTP method, route, query parameters, request body, and response normalization.
- Existing client tests cover the transport implementation in `src/frontend-packages/admin-api-client/src/index.test.ts`, the current-user contract in `src/frontend-packages/admin-api-client/src/current-user.test.ts`, and permission matching. They do not directly exercise the application, application-permission, audit, group, provider, role, session, settings, or user contract mappings.
- The most valuable normalization branches are:
  - group members mapping `id` to `userId` when the API omits `userId`;
  - user roles accepting either a bare array or `{ roles }` response;
  - upstream identities accepting either a bare array or `{ items }` response;
  - upstream identity linking accepting `subjectId` or falling back to `subject`;
  - application-permission mutation responses accepting either `id` or `applicationId`;
  - application-permission maintainer removal encoding query parameters and principal IDs.

## Selected test seam

Tests will use a fake `AdminApiClient` and assert the public contract methods' calls. This is the agreed seam because it verifies the adapter behavior without coupling tests to private helpers, React components, or the transport implementation.

## Primary sources

- SonarQube project coverage API: `component_tree` for `Tjeerd-menno_open-identity-stack`, queried on 2026-08-23.
- SonarQube line source API: `sources/lines` for the selected contract modules, queried on 2026-08-23.
- [Admin API client transport](https://github.com/Tjeerd-menno/open-identity-stack/blob/main/src/frontend-packages/admin-api-client/src/index.ts)
- [Applications contract](https://github.com/Tjeerd-menno/open-identity-stack/blob/main/src/frontend-packages/admin-api-client/src/applications.ts)
- [Application permissions contract](https://github.com/Tjeerd-menno/open-identity-stack/blob/main/src/frontend-packages/admin-api-client/src/application-permissions.ts)
- [Groups contract](https://github.com/Tjeerd-menno/open-identity-stack/blob/main/src/frontend-packages/admin-api-client/src/groups.ts)
- [Users contract](https://github.com/Tjeerd-menno/open-identity-stack/blob/main/src/frontend-packages/admin-api-client/src/users.ts)
- [Remaining contract modules](https://github.com/Tjeerd-menno/open-identity-stack/tree/main/src/frontend-packages/admin-api-client/src)
- [Existing transport tests](https://github.com/Tjeerd-menno/open-identity-stack/blob/main/src/frontend-packages/admin-api-client/src/index.test.ts)
- [Repository testing strategy](TESTING-STRATEGY.md)
- [Repository domain vocabulary](https://github.com/Tjeerd-menno/open-identity-stack/blob/main/CONTEXT.md)
