# Self-hosting the OIDF conformance suite against a local stack

Research note for [issue #306](https://github.com/Tjeerd-menno/open-identity-stack/issues/306).
Researched 2026-07-31 against the conformance suite repository at commit `daf33d6` (master) and
the OpenID Foundation's own certification pages.

**Verdict up front:** the local-first strategy holds. A conformance suite running in a container
can test an issuer on the host or on a private hostname, on a non-standard port, with a self-signed
certificate. The OpenID Foundation's own CI does exactly this. The map does not need rerouting.
The one thing that is *not* available locally is the certification submission itself — see
[§6](#6-are-self-hosted-logs-acceptable-for-an-oidf-submission).

---

## 1. How to run the suite locally

### The quick-start path (no source build)

The suite wiki documents three options; the relevant one is
"[Quick start: running a prebuilt version with Docker](https://gitlab.com/openid/conformance-suite/-/wikis/Developers/Build-&-Run)",
which the wiki describes as "the fastest way to try the conformance suite — it uses the prebuilt
images published to the GitLab container registry and does **not** require cloning the source or
building anything locally."

Steps, verbatim from the wiki:

1. Install Docker (with the Compose plugin).
2. Add `localhost.emobix.co.uk` to your hosts file pointing at `127.0.0.1`. On macOS/Linux edit
   `/etc/hosts`; on Windows edit `C:\Windows\System32\drivers\etc\hosts`. This is required because
   the default `BASE_URL` uses that hostname.
3. `curl -O https://gitlab.com/openid/conformance-suite/-/raw/master/docker-compose-prebuilt.yml`
4. `docker compose -f docker-compose-prebuilt.yml up`
5. Visit <https://localhost.emobix.co.uk:8443/>.
6. `docker compose -f docker-compose-prebuilt.yml down` to stop. MongoDB data persists in
   `./mongo/data` relative to the YAML file.

### What the stack actually is

`docker-compose-prebuilt.yml` (fetched from master) defines exactly three services:

| Service   | Image                                                          | Notes |
|-----------|----------------------------------------------------------------|-------|
| `mongodb` | `${MONGODB_IMAGE:-mongo:6.0.13}`                               | bind-mounts `./mongo/data` |
| `nginx`   | `registry.gitlab.com/openid/conformance-suite/nginx:${IMAGE_TAG:-latest}` | publishes `8443:8443`, TLS terminator |
| `server`  | `registry.gitlab.com/openid/conformance-suite:${IMAGE_TAG:-latest}`       | the Java suite |

Overridable environment variables, per the wiki table:

| Variable          | Default                               | Purpose |
|-------------------|---------------------------------------|---------|
| `IMAGE_TAG`       | `latest`                              | tag for both `server` and `nginx` |
| `MONGODB_IMAGE`   | `mongo:6.0.13`                        | MongoDB image reference |
| `BASE_URL`        | `https://localhost.emobix.co.uk:8443` | base URL the suite advertises to external clients |
| `JAVA_EXTRA_ARGS` | `--fintechlabs.devmode=true`          | extra JVM arguments |

`IMAGE_TAG` pins a release: `IMAGE_TAG=<tag> docker compose -f docker-compose-prebuilt.yml up`.
Tags are listed in the [container registry](https://gitlab.com/openid/conformance-suite/container_registry).
Pinning matters for us — the OIDF checks that the plan version is "close enough" to the current
suite version at submission time (see §6).

### Prerequisites and resources

- **Docker (or Podman) with Compose.** No Java, Maven or Node needed on the quick-start path.
- **Java, only if building from source:** the wiki says `java -version` must show "17 or later";
  the repo's `pom.xml` sets `<java.version>21</java.version>` and both `Dockerfile` and
  `server-dev/Dockerfile` use `eclipse-temurin:21`. Treat 21 as the real requirement.
- **Memory/CPU:** not stated for the Docker path. The best primary datapoint is the OIDF's own Helm
  chart defaults (`chart/values.yaml`): suite server `limits.memory: 2Gi`, `requests.cpu: 100m`;
  MongoDB `limits.memory: 1Gi`, `requests.cpu: 100m`, `persistence.size: 5Gi`; PVC `storage: 10G`.
  Budget ~3–4 GB of RAM for the three containers and a few GB of disk.
- **Startup time:** not documented anywhere I could find. The quick-start path is a container image
  pull plus a Spring Boot start, so expect minutes on first run and seconds thereafter. The wiki
  *does* warn that the alternative Devenv path "might take 30 minutes or so as it will compile
  mongodb" — that is the Nix development path, not the path we want.

### Windows / Podman caveats

- **MongoDB bind mount.** The wiki's Windows section states the compose file "has to be a bit
  different in Windows, with regards MongoDB — the default bind-mount path `./mongo/data` doesn't
  work reliably, so a named volume is used instead", and supplies a Windows-specific compose
  snippet using `volumes: mongodata:/data/db`. Apply the same change to
  `docker-compose-prebuilt.yml`.
- **Hosts file.** `C:\Windows\System32\drivers\etc\hosts` needs `127.0.0.1 localhost.emobix.co.uk`
  (requires an elevated editor).
- **Podman + compose.** The suite ships plain Compose v2 files with no Docker-specific features
  beyond `build`, `ports`, `volumes` and `depends_on`, so `podman compose -f
  docker-compose-prebuilt.yml up` should work. Not verified empirically — verify before relying on
  it.
- **Podman `host-gateway` on Windows.** See §3; this is the one place Podman on Windows differs
  materially from Docker.

---

## 2. Authentication of the local suite

**The local stack runs effectively unauthenticated, with an auto-logged-in admin.**

`docker-compose-prebuilt.yml` sets `SPRING_PROFILES_ACTIVE: ${SPRING_PROFILES_ACTIVE:-dev}`.
The `dev` Spring profile (`src/main/resources/application-dev.properties`) contains:

```properties
fintechlabs.devmode=true
```

`src/main/java/net/openid/conformance/security/DummyUserFilter.java` acts on that flag. Its class
javadoc says: "DummyUserFilter is used to inject an authenticated `OIDCAuthenticationToken` into the
Security Context. This should **NEVER** be used in production ... to fake out the OIDC
authentication mechanism of Spring into thinking a user has already logged in." The filter grants
`ROLE_USER` **and** `ROLE_ADMIN` by default (`fintechlabs.makeDummyUserAdminInDevMode` defaults to
`true`) and presents as `DEVMODE@developer.com` / sub `developer`.

Consequences:

- No Google or GitLab login is needed locally. The hosted suite requires one
  ("Login using a Google or GitLab account" — [OIDF OP testing instructions](https://openid.net/certification/connect_op_testing/)).
- The Google/GitLab client-id/secret env vars in the compose file are placeholders
  (`google-client` / `google-secret`) and are never exercised in devmode.
- **The suite's UI and API are wide open to anything that can reach port 8443.** Do not expose it
  beyond the developer machine.
- API automation needs no token in devmode: `scripts/run-test-plan.py` sets `token = None` when
  `CONFORMANCE_DEV_MODE` is in the environment, and only reads `CONFORMANCE_TOKEN` otherwise.

---

## 3. The load-bearing question: can a containerised suite test a host / private-hostname issuer?

**Yes. Unambiguously yes, and this is a first-class supported configuration, not a hack.**

### The decisive evidence

The OIDF's own CI runs the OIDCC test plans against a provider on a **private, non-resolvable
hostname on a non-standard port with a self-signed certificate**. From
`.gitlab-ci/local-provider-oidcc-conformance-config.json`:

```json
{
    "description": "oidc-provider OIDC",
    "server": {
        "discoveryUrl": "https://oidcc-provider:3000/.well-known/openid-configuration"
    },
    ...
}
```

`oidcc-provider` is a container hostname; `:3000` is not 443; the certificate is generated by
`.gitlab-ci/generate-provider-cert.sh` as a 10-year self-signed cert with no SAN at all
(`-subj '/CN=oidcc-provider'`). `docker-compose-localtest.yml` wires this together and even
documents the host-side variant in a comment:

```yaml
  oidcc-provider:
    # To start just the provider to run tests against for dev use:
    # docker-compose -f docker-compose-localtest.yml up oidcc-provider
    # and add an entry to /etc/hosts '127.0.0.1 oidcc-provider'
```

### What the suite actually constrains

Only two things, both in `src/main/java/net/openid/conformance/condition/client/`:

1. `CheckDiscEndpointDiscoveryUrl.java` — `server.discoveryUrl` must parse as a URL, must end in
   `/.well-known/openid-configuration`, and its protocol must be `https`. Nothing about the host
   being public or the port being 443.
2. `CheckDiscEndpointIssuer.java` — the `issuer` in the discovery document must equal the discovery
   URL with `.well-known/openid-configuration` stripped (trailing slash tolerated), or the test
   fails with: *"issuer listed in the discovery document is not consistent with the location the
   discovery document was retrieved from. These must match to prevent impersonation attacks."*

There is also `CheckDiscEndpointAllEndpointsAreHttps` (all advertised endpoints must be `https`)
and `condition/util/IssuerUrlValidation.java`, which requires an https scheme, a host component, no
fragment/query/userinfo and a port ≤ 65535. **No condition rejects localhost, RFC 1918 addresses,
private hostnames or non-standard ports.**

### The networking arrangement this implies

The binding constraint is not reachability, it is *name identity*: the hostname the suite container
uses to reach OpenIdentityStack must be **the same string** the OP publishes as its `issuer`,
because of `CheckDiscEndpointIssuer`. So pick one hostname and make it resolve correctly in three
places.

Recommended arrangement on Windows 11 + Podman:

1. **Choose a hostname**, e.g. `oidp.local`. Do not use `localhost` — inside the container that
   points at the container itself.
2. **Windows hosts file:** `127.0.0.1 oidp.local` — so the developer's browser reaches
   OpenIdentityStack during the authorization step.
3. **OpenIdentityStack:** issuer `https://oidp.local:<port>`, and it must listen on all interfaces
   (`ASPNETCORE_URLS=https://0.0.0.0:<port>` or equivalent Aspire endpoint config), not just the
   loopback, so the container can connect.
4. **Suite container:** map the same name to the host, e.g. in the compose file

   ```yaml
   server:
     extra_hosts:
       - "oidp.local:host-gateway"
   ```

   Podman supports the `host-gateway` magic value for `--add-host`, which Compose `extra_hosts`
   maps onto.

Traffic in the other direction (OP → suite) also works and *is* needed: the Basic OP certification
plan includes `OIDCCRequestUriUnsignedSupportedCorrectlyOrRejectedAsUnsupported`
(`src/main/java/net/openid/conformance/openid/OIDCCBasicTestPlan.java`), and `request_uri` requires
the OP to fetch a document from the suite. Because both processes are on the same machine and the
suite publishes 8443, the OP can reach `https://localhost.emobix.co.uk:8443/...` directly — but
OpenIdentityStack's HTTP client must then tolerate the suite's self-signed nginx certificate (see
§4). The test module's name indicates that cleanly rejecting `request_uri` as unsupported is also an
acceptable outcome, so this is only load-bearing if we advertise `request_uri_parameter_supported`.

### The Podman-on-Windows caveat — read this before assuming it just works

From the [`podman-run` man page](https://docs.podman.io/en/latest/markdown/podman-run.1.html), on
`--add-host ... :host-gateway`:

> Instead of an IP address, the special flag `host-gateway` can be given. This resolves to an IP
> address the container can use to connect to the host. The IP address chosen depends on your
> network setup, thus there's no guarantee that Podman can determine the host-gateway address
> automatically, which will then cause Podman to fail with an error message. You can overwrite this
> IP address using the `host_containers_internal_ip` option in containers.conf.

and, specifically for our platform:

> If Podman is running in a virtual machine using `podman machine` (this includes Mac and Windows
> hosts), Podman will silently skip adding the internal hostnames to `/etc/hosts`, unless an IP
> address was configured manually; the internal hostnames are resolved by the gvproxy DNS resolver
> instead.

[`containers.conf(5)`](https://github.com/containers/common/blob/main/docs/containers.conf.5.md)
adds: "This config doesn't affect the actual network setup, it just tells Podman the IP address it
should expect. Configuring an IP address here doesn't ensure that the container can actually reach
the host using this IP address."

**Practical reading:**
- `host.containers.internal` should resolve inside the container on Windows (gvproxy DNS), but
  `--add-host name:host-gateway` may fail outright unless `host_containers_internal_ip` is set in
  `containers.conf`.
- **Uncertain, verify empirically:** whether the address gvproxy hands back reaches a service bound
  on the *Windows* host as opposed to the podman-machine WSL VM. The primary docs do not settle
  this.
- **Robust fallback that sidesteps the whole question:** point `extra_hosts` at the Windows box's
  actual LAN IP — `- "oidp.local:192.168.x.y"` — instead of `host-gateway`. This is explicit,
  works on Docker and Podman alike, and needs no gvproxy behaviour. Its only cost is that the IP
  must be refreshed when the machine changes network.
- **Alternative that avoids host networking entirely:** run OpenIdentityStack itself as a container
  on the same Podman network as the suite, exactly as `docker-compose-localtest.yml` does with
  `oidcc-provider`. Then the hostname is the compose service name and nothing else is needed. This
  is the closest analogue to the OIDF's own arrangement and is the lowest-risk option if the
  host-gateway route gives trouble.

---

## 4. TLS demanded of the issuer under test

**Requirement: `https`. Not: a publicly trusted certificate.**

The scheme is enforced (see §3: `CheckDiscEndpointDiscoveryUrl`, `CheckDiscEndpointAllEndpointsAreHttps`,
`IssuerUrlValidation`). Certificate *validity* is not.

The outbound HTTP client the test conditions use is built in
`src/main/java/net/openid/conformance/condition/AbstractCondition.java` (`createHttpClient`). It
installs an `X509TrustManager` whose `checkServerTrusted` and `checkClientTrusted` methods are
empty, initialises the `SSLContext` with it, and sets `NoopHostnameVerifier.INSTANCE` on the socket
factory. In other words:

- **Any certificate is accepted** — self-signed, expired, wrong CA.
- **Hostname is not verified** — no SAN required, CN mismatch irrelevant.
- **No trust has to be established inside the container.** There is no keystore to seed, no CA to
  mount, no `keytool` step. Grepping the whole repository for `keytool`, `cacerts` or `trustStore`
  returns nothing.

This is corroborated by the OIDF's own CI provider certificate: `openssl req -x509 -nodes -days 3650
-key local-provider.key -out local-provider-oidcc.crt -subj '/CN=oidcc-provider'` — self-signed, CN
only, no SAN — used with no truststore import anywhere in the repo.

Only one nuance is worth flagging. The suite can drive browser interaction itself (HtmlUnit, via
`frontchannel/BrowserControl.java`) when the test configuration contains a `browser` block, as the
CI config does. I could **not** find an explicit `setUseInsecureSSL(true)` call in
`BrowserControl.java`, so I cannot point at the line that makes the *scripted browser* tolerate the
self-signed cert — but the OIDF CI demonstrably runs scripted browser interaction against
`https://oidcc-provider:3000` with that certificate and no truststore import, so empirically it
does. Treat the mechanism as unconfirmed and the outcome as confirmed.

For **manual** browser interaction (the default for OIDCC OP plans without a `browser` block) the
certificate must be acceptable to the *developer's own browser* on Windows. Two certificates are in
play there:

- **OpenIdentityStack's.** The ASP.NET Core dev certificate (`dotnet dev-certs https`) has
  `localhost` in its SAN, so browsing to `https://oidp.local:<port>` will warn. Either click through,
  or issue a cert with the chosen hostname in the SAN and trust it in the Windows store.
- **The suite's own nginx.** `nginx/Dockerfile` generates a self-signed cert with
  `-subj "/CN=localhost"` — so `https://localhost.emobix.co.uk:8443/` warns too. The wiki
  acknowledges this: "This may result in a certificate error depending on your browser."

---

## 5. Creating and driving test plans, and exporting results

The self-hosted instance is the *same application* as the hosted one, so plan creation and export
behave identically.

**Via the UI.** Create a plan, pick "OpenID Connect Core: Basic Certification Profile Authorization
server test" from the "Test an OpenID Provider" section, fill in the JSON configuration, press
"Start Test Plan". The plan name in the API is `oidcc-basic-certification-test-plan`
(`@PublishTestPlan` annotation on `openid/OIDCCBasicTestPlan.java`).

**Via the API.** `scripts/run-test-plan.py` plus the `scripts/conformance.py` helper library drive
the same REST API. The OIDF explicitly recommends this: *"A python script and library are available
to allow the conformance suite to be used in a continuous integration system; it is highly
recommended that authorization server developers integrate this into their development pipeline"*
([About the Conformance Suite](https://openid.net/certification/about-conformance-suite/)). There is
a [step-by-step tutorial repo](https://gitlab.com/openid/conformance-suite-automated-testing-tutorial)
covering plan creation, configuration, automating interactive steps, and producing a certification
package.

Relevant environment variables (`scripts/run-test-plan.py`):

- `CONFORMANCE_SERVER` — base URL of the suite; defaults to `https://localhost.emobix.co.uk:8443/`.
- `CONFORMANCE_DEV_MODE` — when set, no API token is used and TLS verification of the suite itself
  is skipped (`verify_ssl = not dev_mode and not 'DISABLE_SSL_VERIFY' in os.environ`).
- `CONFORMANCE_TOKEN` — only needed against an authenticated (hosted) instance.

**Export.** All the export endpoints live in `src/main/java/net/openid/conformance/logging/LogApi.java`
and are present in any instance:

| Endpoint | Purpose |
|---|---|
| `GET /api/log/export/{id}` | single test log as zip |
| `GET /api/log/exporthtml/{id}` | single test log as HTML+JSON zip |
| `GET /api/plan/export/{id}` | whole plan as zip |
| `GET /api/plan/exporthtml/{id}` | whole plan as HTML+JSON zip |
| `POST /api/plan/{id}/certificationpackage` | *"Prepare certification package for a test plan. Also publishes the plan and marks it as immutable."* |

The certification-package endpoint refuses to produce anything if any test failed or is incomplete;
it publishes the plan and calls `changeTestPlanImmutableStatus(id, TRUE)` before exporting. So yes —
a self-hosted instance can produce a byte-identical-in-shape certification package, including the
immutability marking the OIDF checks for. Whether they will *accept* one is §6.

---

## 6. Are self-hosted logs acceptable for an OIDF submission?

**Short answer: no — plan on the final, submitted run happening on `www.certification.openid.net`.
Self-hosted runs are for iteration.**

I could not find a sentence that says "self-hosted logs are rejected" in so many words. What I did
find are three requirements that a self-hosted run cannot satisfy without misrepresentation. The
decisive one is from [Submission of Results for OP
Certification](https://openid.net/certification/op_submission/):

> 'Conformance Test Suite Software' field must contain the string "www.certification.openid.net"
> and the conformance suite version number, for example "www.certification.openid.net version
> 4.1.18".

Supporting evidence:

- The same page: *"The certification package must be created by using the 'Publish for
  certification' button."*
- [Conformance Testing for OpenID Connect OPs](https://openid.net/certification/connect_op_testing/):
  *"Open https://www.certification.openid.net/ ... Login using a Google or GitLab account"*, and the
  registered redirect URI must be
  `https://www.certification.openid.net/test/a/<ALIAS>/callback`. A self-hosted run would produce
  `https://localhost.emobix.co.uk:8443/test/a/<ALIAS>/callback` throughout the logs, which is
  visibly not the hosted instance.
- The OIDF's internal [Certification Check List](https://gitlab.com/openid/conformance-suite/-/wikis/Certification-Check-List)
  instructs reviewers to *"Verify that the test plan is immutable"* and to check that the package
  filename matches the rules, noting that a non-conforming name *"indicates that the requestor did
  not use the 'Certification Package' button, which might also indicate that the plan hasn't been
  made immutable."* It also says to *"check that the plan version is 'close enough' to the current
  conformance suite version"*.

Conversely, the OIDF is unambiguous that self-hosting for *testing* is supported and encouraged
([About the Conformance Suite](https://openid.net/certification/about-conformance-suite/)):

> There is no cost to utilize the conformance suite to test OpenID deployments and is available for
> all to utilize at any time. A fee is required for OpenID certifications. ... The conformance suite
> can be installed locally inside docker, see:
> https://gitlab.com/openid/conformance-suite/wikis/Developers/Build-&-Run

**Uncertainty, stated plainly:** no primary source explicitly prohibits submitting self-hosted logs.
The prohibition is implied by the mandatory `www.certification.openid.net` attestation string, the
mandatory hosted redirect URI, and a reviewer checklist calibrated to hosted-suite artefacts. If we
want this settled definitively, the OIDF answers at `certification@oidf.org`. But the practical
conclusion is not in doubt: **the public run is strictly necessary for certification**, and this
research does not change the map's destination — it only makes the road to it cheaper.

---

## 7. What this means for OpenIdentityStack

1. Stand up the prebuilt stack with a Windows-adjusted `docker-compose-prebuilt.yml` (named volume
   for Mongo), pinned via `IMAGE_TAG` to a specific release.
2. Pick a stable hostname (`oidp.local` or similar), put it in the Windows hosts file, make
   OpenIdentityStack's issuer use it, and bind the app to `0.0.0.0`.
3. Map that hostname into the suite container. Prefer `extra_hosts: ["oidp.local:<host LAN IP>"]`
   over `host-gateway` on Podman/Windows until `host-gateway` is verified working.
4. Do not build a real certificate for this. The suite trusts everything; only the developer's
   browser cares, and clicking through is acceptable.
5. Iterate with `oidcc-basic-certification-test-plan` locally until clean, then run the identical
   plan against the public certification environment on `www.certification.openid.net` and produce
   the submission package there.

## Sources

All accessed 2026-07-31.

- Conformance suite repository, master @ `daf33d6` — <https://gitlab.com/openid/conformance-suite>
  - `docker-compose-prebuilt.yml`, `docker-compose.yml`, `docker-compose-localtest.yml`
  - `Dockerfile`, `server-dev/Dockerfile`, `nginx/Dockerfile`, `pom.xml`, `chart/values.yaml`
  - `src/main/resources/application.properties`, `application-dev.properties`
  - `src/main/java/net/openid/conformance/security/DummyUserFilter.java`
  - `src/main/java/net/openid/conformance/condition/AbstractCondition.java`
  - `src/main/java/net/openid/conformance/condition/client/CheckDiscEndpointDiscoveryUrl.java`,
    `CheckDiscEndpointIssuer.java`
  - `src/main/java/net/openid/conformance/condition/util/IssuerUrlValidation.java`
  - `src/main/java/net/openid/conformance/openid/OIDCCBasicTestPlan.java`
  - `src/main/java/net/openid/conformance/logging/LogApi.java`
  - `src/main/java/net/openid/conformance/frontchannel/BrowserControl.java`
  - `scripts/run-test-plan.py`, `scripts/conformance.py`
  - `.gitlab-ci/local-provider-oidcc-conformance-config.json`, `.gitlab-ci/generate-provider-cert.sh`
- Conformance suite wiki — <https://gitlab.com/openid/conformance-suite/-/wikis/Developers/Build-&-Run>
  and <https://gitlab.com/openid/conformance-suite/-/wikis/Certification-Check-List>
- Automated testing tutorial — <https://gitlab.com/openid/conformance-suite-automated-testing-tutorial>
- OpenID Foundation — <https://openid.net/certification/about-conformance-suite/>,
  <https://openid.net/certification/connect_op_testing/>, <https://openid.net/certification/op_submission/>
- Podman — <https://docs.podman.io/en/latest/markdown/podman-run.1.html>,
  <https://github.com/containers/common/blob/main/docs/containers.conf.5.md>
