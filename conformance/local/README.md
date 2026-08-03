# Local conformance loop

Runs the OIDF conformance suite and OpenIdentityStack side by side as containers, so conformance failures can be found and fixed in minutes instead of a deploy cycle.

**This is for iteration only.** OIDF certification submission requires a run against the hosted suite at `certification.openid.net` — self-hosted logs are not submissible.

## The hostname problem, and how this solves it

The suite requires the OP's published `issuer` to equal the discovery URL minus the `/.well-known/openid-configuration` suffix. So one hostname must resolve **identically** in three places: your browser, inside the suite container, and in the OP's issuer string.

This setup avoids any hosts-file edit or administrator privileges:

- **`oidc.localtest.me` resolves publicly to `127.0.0.1`**, so your browser reaches the provider through the published port.
- The provider carries a **network alias** of that same name, so the suite container resolves it to the provider container directly.
- Port `3000` is identical on both paths, so a single issuer string — `https://oidc.localtest.me:3000/` — is correct for both.

`localhost.emobix.co.uk` also resolves publicly to `127.0.0.1`, so the suite's own default `BASE_URL` needs no hosts entry either.

## Security

The suite runs with `SPRING_PROFILES_ACTIVE=dev`, which injects an auto-authenticated `ROLE_ADMIN` user. **It is completely unauthenticated.** Never expose port 8443 beyond this machine. The suite's own javadoc says this profile must never be used in production.

The provider's TLS certificate is self-signed. That is fine: the suite installs a trust-all `X509TrustManager` and `NoopHostnameVerifier`, so certificate validity is irrelevant to it. Your browser will warn once and needs an exception.

## Bringing it up from cold

```bash
podman machine start
```

Build both images for your **host** architecture — these run locally, not on the arm64 cluster:

```bash
podman build --platform linux/amd64 -f src/OpenIdentityStack.Api/Dockerfile -t oidcp-api:local .
podman build --platform linux/amd64 -f src/OpenIdentityStack.DbMigrator/Dockerfile -t oidcp-migrator:local .
```

Generate the provider certificate, in this directory. On Git Bash, `MSYS_NO_PATHCONV=1` is required or the subject is mangled into a Windows path:

```bash
MSYS_NO_PATHCONV=1 openssl req -x509 -newkey rsa:2048 -nodes -keyout provider.key -out provider.crt -days 825 -subj "/CN=oidc.localtest.me" -addext "subjectAltName=DNS:oidc.localtest.me,DNS:localhost,IP:127.0.0.1"
```

```bash
MSYS_NO_PATHCONV=1 openssl pkcs12 -export -out provider.pfx -inkey provider.key -in provider.crt -passout pass:conformance
```

The encryption key must decode to exactly 32 bytes or the API fails at startup with `Secrets:EncryptionKey must be a base64-encoded 256-bit key`:

```bash
export SECRETS_ENCRYPTION_KEY=$(openssl rand -base64 32)
```

```bash
podman compose -f docker-compose-local.yml up -d
```

## Verifying

```bash
curl -sk -o /dev/null -w "%{http_code}\n" https://oidc.localtest.me:3000/.well-known/openid-configuration
```

```bash
podman exec oidcc-local-server-1 curl -sk -o /dev/null -w "%{http_code}\n" https://oidc.localtest.me:3000/.well-known/openid-configuration
```

Both must return `200` — the first is the browser path, the second the suite's. The published `issuer` must read exactly `https://oidc.localtest.me:3000/`.

The suite UI is at <https://localhost.emobix.co.uk:8443/>. Both `oidcc-basic-certification-test-plan` and `oidcc-config-certification-test-plan` are available (85 plans total).

## Seeded test identities

| | |
|---|---|
| Users | `alice@example.test` / `Alice!Conformance1`, `bob@example.test` / `Bob!Conformance1` |
| Clients | `oidf-code-client`, `oidf-code-client-post`, `oidf-code-client-takeover` |
| Alias | `local-conformance` |

Client secrets are in the compose file. They are local-only throwaway values and must never be reused for the public certification environment.

## The suite plan configuration

`plan-config.json` is **not** tracked — the repository ignores that filename everywhere, because in static-client mode it carries client secrets. Create it in this directory before driving a plan:

```bash
cat > plan-config.json <<'JSON'
{
  "alias": "local-conformance",
  "description": "OpenIdentityStack local conformance loop (throwaway local-only secrets)",
  "server": {
    "discoveryUrl": "https://oidc.localtest.me:3000/.well-known/openid-configuration",
    "login_hint": "alice@example.test"
  },
  "client": {
    "client_id": "oidf-code-client",
    "client_secret": "code-client-secret"
  },
  "client_secret_post": {
    "client_id": "oidf-code-client-post",
    "client_secret": "code-client-post-secret"
  },
  "client2": {
    "client_id": "oidf-code-client-takeover",
    "client_secret": "code-client-takeover-secret"
  }
}
JSON
```

The three client secrets must match `Seed__Certification__Clients__*` in the compose file, and `login_hint` must be a seeded user.

## Notes

- The migrator container exits `0` when finished; that is expected, not a failure.
- The migrator needs both the OpenIddict certificates **and** `OpenIddict__Issuer`, even though it issues no tokens — it constructs the full OpenIddict server, which refuses to start without them outside Development/Testing.
- MongoDB uses a **named volume** rather than the upstream `./mongo/data` bind mount, which is unreliable on Windows.
- `podman compose` delegates to `docker-compose` v2 and works without modification.

## Tearing down

```bash
podman compose -f docker-compose-local.yml down -v
```
