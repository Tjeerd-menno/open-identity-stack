# OpenIdentityStack Documentation

OpenIdentityStack is an OAuth 2.0 and OpenID Connect identity platform built for teams that need reliable user administration, client management, and identity federation in production.

This site is written for people deploying and operating the platform:

- product owners
- platform teams
- application operators
- administrators

If you are working on the codebase, check the **Reference** section for implementation-level docs.

## Start here

- [Guided setup](getting-started/guided-setup.md)
- [Quick start](getting-started/quick-start.md)
- [Local installation](installation/local.md)
- [Production deployment](installation/production.md)
- [Troubleshooting](troubleshooting.md)

## What this documentation covers

- What you can use OpenIdentityStack for
- Local installation and first run
- Production deployment paths
- Common failures and recommended fixes
- Security and hardening checks

## Contributing to docs

To preview documentation locally:

```bash
python -m pip install -r docs/requirements.txt
mkdocs serve
```

Keep changes focused and user-facing. Internal architecture notes and engineering investigations
live under [Reference](reference/index.md).

## First release-tag publish run

The first release-tag publish already ran successfully on tag **`v0.1.2`**.

- Docs publish flow: https://github.com/Tjeerd-menno/open-identity-stack/actions/runs/26046271059  
- Release artifact flow: https://github.com/Tjeerd-menno/open-identity-stack/actions/runs/26046270132

You can now treat `v0.1.2` as the baseline release tag for:

- First-pass production rollout validation
- Documentation verification
- Release artifact handoff checks

