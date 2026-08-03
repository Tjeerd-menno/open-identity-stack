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

The suite runs with `SPRING_PROFILES_ACTIVE=dev`, which injects an auto-authenticated `ROLE_ADMIN` user. **It is completely unauthenticated.** The suite's own javadoc says this profile must never be used in production. Both published ports are therefore bound to `127.0.0.1` in the compose file rather than to all interfaces; do not widen them.

The provider's TLS certificate is self-signed. That is fine: the suite installs a trust-all `X509TrustManager` and `NoopHostnameVerifier`, so certificate validity is irrelevant to it. Your browser will warn once and needs an exception.

## Bringing it up from cold

```bash
podman machine start
```

### From the repository root

The image builds need the repository as their build context, so run these two from the root — not from this directory.

Both images run on this machine, so build them for your **host** architecture. The Dockerfiles honour `--platform` through `$TARGETPLATFORM`; omitting it gives you the host's own, which is what you want. Do not copy the arm64 platform flag used for the cluster builds.

```bash
podman build -f src/OpenIdentityStack.Api/Dockerfile -t oidcp-api:local .
```

```bash
podman build -f src/OpenIdentityStack.DbMigrator/Dockerfile -t oidcp-migrator:local .
```

### From this directory

```bash
cd conformance/local
```

Generate the provider certificate. On Git Bash, `MSYS_NO_PATHCONV=1` is required or the subject is mangled into a Windows path:

```bash
MSYS_NO_PATHCONV=1 openssl req -x509 -newkey rsa:2048 -nodes -keyout provider.key -out provider.crt -days 825 -subj "/CN=oidc.localtest.me" -addext "subjectAltName=DNS:oidc.localtest.me,DNS:localhost,IP:127.0.0.1"
```

```bash
MSYS_NO_PATHCONV=1 openssl pkcs12 -export -out provider.pfx -inkey provider.key -in provider.crt -passout pass:conformance
```

Both images run as `USER app`, and under rootless Podman your host UID maps to container root — so a default-umask `0600` PFX is unreadable to the runtime user and both containers fail to start. Make it readable:

```bash
chmod 0644 provider.pfx
```

That is deliberate for this file only. It is a throwaway self-signed certificate whose password is already published in the compose file below, and it is never the certificate used for the hosted certification run. `provider.key` can stay `0600`; nothing mounts it.

On an SELinux-enforcing host the Unix mode is not sufficient on its own, but no extra step is needed: the compose file mounts the PFX with `:ro,z` so Podman relabels it for container access.

The encryption key must decode to exactly 32 bytes or the API fails at startup with `Secrets:EncryptionKey must be a base64-encoded 256-bit key`:

```bash
export SECRETS_ENCRYPTION_KEY=$(openssl rand -base64 32)
```

```bash
podman compose -f docker-compose-local.yml up -d
```

The suite and nginx images default to the `latest` tag. To pin a known-good suite build — worth doing when bisecting a behaviour change, since upstream moves `latest` — set `CONFORMANCE_SUITE_TAG` before bringing the stack up.

## Verifying

```bash
curl -sk -o /dev/null -w "%{http_code}\n" https://oidc.localtest.me:3000/.well-known/openid-configuration
```

```bash
podman compose -f docker-compose-local.yml exec server curl -sk -o /dev/null -w "%{http_code}\n" https://oidc.localtest.me:3000/.well-known/openid-configuration
```

Both must return `200` — the first is the browser path, the second the suite's. The published `issuer` must read exactly `https://oidc.localtest.me:3000/`.

### If the browser path fails but the suite path succeeds

On Windows, the Podman machine is a WSL VM, and after a machine restart WSL's localhost relay stops forwarding published ports — the listeners sit in the rootless network namespace where the relay cannot see them. The symptom is precise: the in-container check returns `200` while the host `curl` cannot connect at all. This is a WSL issue and not the port binding; a container published on `0.0.0.0` is equally unreachable.

Tunnel the two ports through the machine's own sshd, in a separate shell:

```bash
podman machine inspect podman-machine-default --format '{{.SSHConfig.Port}}'
```

```bash
ssh -i "$USERPROFILE/.local/share/containers/podman/machine/machine" -p <port> -N -L 127.0.0.1:3000:127.0.0.1:3000 -L 127.0.0.1:8443:127.0.0.1:8443 user@127.0.0.1
```

Both `oidc.localtest.me` and `localhost.emobix.co.uk` resolve to `127.0.0.1`, so one tunnel serves the provider and the suite UI alike.

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

- The migrator container exits `0` when finished; that is expected, not a failure. That exit is load-bearing — the provider waits on it via `service_completed_successfully`, so the API cannot serve discovery before migrations and seeding are done. If the provider never starts, read the migrator's logs first.
- The migrator needs both the OpenIddict certificates **and** `OpenIddict__Issuer`, even though it issues no tokens — it constructs the full OpenIddict server, which refuses to start without them outside Development/Testing.
- MongoDB uses a **named volume** rather than the upstream `./mongo/data` bind mount, which is unreliable on Windows.
- `podman compose` delegates to `docker-compose` v2 and works without modification.

## Tearing down

```bash
podman compose -f docker-compose-local.yml down -v
```
