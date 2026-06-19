# Implementation Plan: Management Web Sole Frontend

**Branch**: `007-management-web` | **Date**: 2026-06-18

## Summary

Management Web is now the only supported OpenIdentityStack frontend. The repository, local runtime, test infrastructure, and documentation must all reflect a single-frontend architecture centered on `src/OpenIdentityStack.ManagementWeb`.

## Current Architecture

- `src/OpenIdentityStack.ManagementWeb` is the only browser UI started by `src/OpenIdentityStack.AppHost`.
- `src/OpenIdentityStack.DbMigrator` seeds the `management-web-client` OIDC public client for local and test flows.
- `tests/OpenIdentityStack.ManagementWeb.E2ETests` is the only frontend E2E test project.
- Legacy Clients and Service Accounts stay removed from frontend navigation in favor of the unified Applications model.

## Operational Rules

- Local AppHost port for Management Web is `5175`.
- Backend-only local runs can disable the frontend with `OPENIDENTITYSTACK_ENABLE_MANAGEMENTWEB=false`.
- All interactive operator documentation should refer to Management Web only.

## Cleanup Requirements

1. Remove runtime and solution references to `AdminWeb`.
2. Remove legacy frontend source and legacy frontend E2E test assets from active repository use.
3. Update docs, repo guidance, and configuration references so they describe a ManagementWeb-only architecture.
4. Verify that focused build and test commands pass without relying on AdminWeb.
