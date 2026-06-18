# GHCR publishing

OpenIdentityStack publishes Linux container images to GitHub Container Registry from the `Release` workflow.

## Images

The workflow publishes these images:

| Component | Image |
| --- | --- |
| API | `ghcr.io/tjeerd-menno/open-identity-stack-api:<version>` |
| DbMigrator | `ghcr.io/tjeerd-menno/open-identity-stack-db-migrator:<version>` |
| Management Web | `ghcr.io/tjeerd-menno/open-identity-stack-management-web:<version>` |

Each image also receives a `sha-<commit>` tag. The workflow emits build provenance attestations for the published images.

## Triggers

The workflow runs on:

- SemVer-like tags matching `v*.*.*`.
- Manual dispatch with a required `version` input.

Use immutable SemVer or prerelease tags, for example:

```bash
git tag v0.1.0-rc.1
git push origin v0.1.0-rc.1
```

Or run the workflow manually and provide `v0.1.0-rc.1` as the version.

## Permissions

The workflow uses `GITHUB_TOKEN` with:

- `packages: write` for GHCR pushes.
- `contents: write` for GitHub release assets.
- `attestations: write` and `id-token: write` for provenance attestations.

Repository package settings must allow GitHub Actions to publish packages.
