# Hardening checklist

Use this checklist before calling a production rollout ready.

## Identity and key material

- production signing certificates are separate from development keys
- encryption certificates are separate from signing certificates
- certificate rotation ownership and timing are documented

## Configuration

- secrets are injected from a platform-managed store
- browser origins are explicitly configured
- forwarded headers are enabled only when required and scoped to trusted proxies or networks

## Access control

- the initial admin account is protected and auditable
- day-to-day operators do not all run as `super-admin`
- service accounts use least privilege and documented ownership

## Operations

- PostgreSQL backups exist and restores have been tested
- `/health` and `/alive` are monitored
- rollout, rollback, and secret rotation procedures are documented
