# Security checklist

Use this checklist before moving from staging to production.

## Secrets and configuration

- Store secrets in a centralized secret manager.
- Never commit private keys, passwords, or connection strings.
- Use environment variables or volume-mounted secret stores in production.

## Certificates

- Use dedicated signing/encryption certificates for production.
- Rotate secrets and certificates on your normal schedule.
- Keep test/dev certificates separate from production material.

## Network posture

- Protect API/admin endpoints with ingress/auth controls.
- Restrict trusted origins and configure CORS correctly.
- Enable forwarded headers only when behind a proxy.

## Operational hardening

- Keep database backups enabled.
- Monitor API and AppHost startup/health logs.
- Verify role and permission changes through an admin workflow with approval.

