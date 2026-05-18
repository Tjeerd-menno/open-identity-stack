# Troubleshooting

Use this playbook when deployment or login flow does not behave as expected.

## AppHost or startup failures

- **Container or service fails immediately**
  1. Confirm local ports are not already in use.
  2. Confirm `.NET` and Node prerequisites are installed.
  3. Review AppHost terminal output for stack trace details.
- **Long startup or hanging migrations**
  1. Verify PostgreSQL container is healthy and accepting connections.
  2. Retry with `OPENIDENTITYSTACK_DISABLE_DATA_VOLUME=true` if migration state is corrupted.

## DB and migration issues

- **Migration did not run**
  - Confirm DbMigrator is in the composition.
  - Check startup order and database credentials.
  - Retry after recreating database credentials.

## Auth and certificate issues

- **Token validation or sign-in fails**
  - Confirm signing/encryption material is configured.
  - Verify token issuer/audience settings in your deployment.
- **Certificate password errors**
  - Re-check secret names and value encoding.

## Reverse proxy and CORS

- If calls work in local mode but fail behind load balancer / ingress:
  - check `ForwardedHeaders` handling
  - confirm `AllowedCorsOrigins` includes the admin/API origins
  - confirm TLS termination path and headers are preserved

## Client registration and service accounts

- Confirm client IDs and secrets are set in the expected environment.
- Recheck callback URLs and grant types for each client.
- Validate the secret is current and not expired.

## Escalation checklist

When opening an issue, include:

- deployment target and compose path used
- exact error output block
- environment variables marked secret-safe
- steps already performed from this document

