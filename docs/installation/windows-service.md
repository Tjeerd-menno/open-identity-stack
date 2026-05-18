# Windows service deployment (optional)

If your infrastructure requires a Windows service mode, package artifacts are published from the release pipeline.

## What is included

- Published API binaries for `win-x64`
- Install/uninstall scripts in `deploy/windows-service`

## Install flow

1. Download the Windows service zip from a release.
2. Run the provided install script.
3. Configure connection settings and service account details.
4. Start and verify service health.

## Validation

- API service starts and exposes expected endpoints.
- Admin UI or reverse proxy target resolves to API.
- Logs are captured and rotated according to host policy.

If this path is not required for your environment, prefer container deployment.

