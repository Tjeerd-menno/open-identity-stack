#!/usr/bin/env bash
# Start the test-only suite and OP on a disposable Linux runner. Plan configuration
# and all private runtime material stay in the runner's temporary directory.
set -euo pipefail
umask 077
mode=${1:?expected candidate, source or stop}
state="${RUNNER_TEMP:?}/oidf"
root=$(cd "$(dirname "$0")/../.." && pwd)
compose=(docker compose --project-name "oidf-${GITHUB_RUN_ID:?}-${GITHUB_RUN_ATTEMPT:?}"
    --env-file "$state/stack.env" -f "$state/compose.yml" -f "$root/conformance/ci/compose.yml")

if [[ $mode == stop ]]; then
    if [[ -f $state/stack.env ]]; then "${compose[@]}" down --volumes --remove-orphans; fi
    exit 0
fi
if [[ $mode != candidate && $mode != source ]]; then echo 'Expected candidate, source or stop' >&2; exit 2; fi
if [[ ${RUNNER_ENVIRONMENT:-} != github-hosted ]]; then
    echo 'Run this stack on a disposable GitHub-hosted runner.' >&2
    exit 2
fi

mkdir "$state"
cp "$root/conformance/local/docker-compose-local.yml" "$state/compose.yml"
# Public DNS is unnecessary. Both the machine running Chromium and the suite's
# container network aliases must resolve each name to the matching local service.
printf '%s\n' '127.0.0.1 oidc.localtest.me localhost.emobix.co.uk' | sudo tee -a /etc/hosts >/dev/null
suite_version=release-v5.3.4
suite_commit=23353b36957d6466010de767c812d2c91ed37638
suite_image=registry.gitlab.com/openid/conformance-suite@sha256:dcc4bd1fb7e9c00f048a67373fe2bec5ecf9feb45a6b5d55e237e5c30e5312c6
nginx_image=registry.gitlab.com/openid/conformance-suite/nginx@sha256:9a7850826fcc8690cd43373528f50e9551ba33bf8e4a40b67b13267b0aca3675
if [[ $mode == candidate ]]; then
    revision=0a48a4513e0ed7d1c4917f454fa1d1f21063d4ab
    api=ghcr.io/tjeerd-menno/open-identity-stack-api@sha256:bfae54349b272ed0db0df0493d0ecd27ef7e90e483b4618bb4a5f221b4b0c57b
    migrator=ghcr.io/tjeerd-menno/open-identity-stack-db-migrator@sha256:c6c00bdaf1dad18c18c0a3c9196191eb20fc8a320cf7d37a67f9dccd18350dc2
    for image in "$api" "$migrator"; do
        docker pull "$image"
        if [[ $(docker image inspect --format '{{ index .Config.Labels "org.opencontainers.image.revision" }}' "$image") != "$revision" ]]; then
            echo "Release image revision mismatch for $image" >&2
            exit 1
        fi
    done
    api_ref=$api
    migrator_ref=$migrator
else
    revision=$(git -C "$root" rev-parse HEAD)
    api="oidf-api:$revision"
    migrator="oidf-migrator:$revision"
    docker build -f "$root/src/OpenIdentityStack.Api/Dockerfile" -t "$api" "$root"
    docker build -f "$root/src/OpenIdentityStack.DbMigrator/Dockerfile" -t "$migrator" "$root"
    api_ref=$(docker image inspect --format '{{.Id}}' "$api")
    migrator_ref=$(docker image inspect --format '{{.Id}}' "$migrator")
fi

cert_password=$(openssl rand -hex 24)
alice="Aa1!$(openssl rand -hex 24)"
export CODE_CLIENT_SECRET POST_CLIENT_SECRET SECOND_CLIENT_SECRET
CODE_CLIENT_SECRET=$(openssl rand -hex 32)
POST_CLIENT_SECRET=$(openssl rand -hex 32)
SECOND_CLIENT_SECRET=$(openssl rand -hex 32)
for secret in "$cert_password" "$alice" "$CODE_CLIENT_SECRET" "$POST_CLIENT_SECRET" "$SECOND_CLIENT_SECRET"; do
    echo "::add-mask::$secret"
done
openssl req -x509 -newkey rsa:3072 -nodes -days 3 -subj '/CN=oidc.localtest.me' \
    -addext 'subjectAltName=DNS:oidc.localtest.me' -keyout "$state/provider.key" -out "$state/provider.crt" 2>/dev/null
PROVIDER_PFX_PASSWORD="$cert_password" openssl pkcs12 -export -out "$state/provider.pfx" \
    -inkey "$state/provider.key" -in "$state/provider.crt" -passout env:PROVIDER_PFX_PASSWORD
# The bind mount is consumed by the unprivileged app user. Its host parent is private.
chmod 0644 "$state/provider.pfx"

cat >"$state/stack.env" <<EOF
SUITE_IMAGE=$suite_image
SUITE_NGINX_IMAGE=$nginx_image
API_IMAGE=$api
MIGRATOR_IMAGE=$migrator
PROVIDER_CERT_PASSWORD=$cert_password
SECRETS_ENCRYPTION_KEY=$(openssl rand -base64 32)
POSTGRES_PASSWORD=$(openssl rand -hex 24)
CONFORMANCE_PASSWORD=$alice
CONFORMANCE_BOB_PASSWORD=Aa1!$(openssl rand -hex 24)
CODE_CLIENT_SECRET=$CODE_CLIENT_SECRET
POST_CLIENT_SECRET=$POST_CLIENT_SECRET
SECOND_CLIENT_SECRET=$SECOND_CLIENT_SECRET
EOF
printf 'export CONFORMANCE_PASSWORD=%q\n' "$alice" >"$state/runner.env"
jq -n '{alias:"local-conformance",description:"OpenIdentityStack CI",
        server:{discoveryUrl:"https://oidc.localtest.me:3000/.well-known/openid-configuration",login_hint:"alice@example.test"},
        client:{client_id:"oidf-code-client",client_secret:env.CODE_CLIENT_SECRET},
        client_secret_post:{client_id:"oidf-code-client-post",client_secret:env.POST_CLIENT_SECRET},
        client2:{client_id:"oidf-code-client-takeover",client_secret:env.SECOND_CLIENT_SECRET}}' >"$state/plan-config.json"
jq -n --arg mode "$mode" --arg revision "$revision" --arg api "$api_ref" --arg migrator "$migrator_ref" \
    --arg suiteVersion "$suite_version" --arg suiteCommit "$suite_commit" --arg suiteImage "$suite_image" \
    --arg nginxImage "$nginx_image" '{$mode,$revision,$api,$migrator,$suiteVersion,$suiteCommit,$suiteImage,$nginxImage}' \
    >"$state/images.json"

"${compose[@]}" config --quiet
"${compose[@]}" up --detach
suite=https://localhost.emobix.co.uk:8443
issuer=https://oidc.localtest.me:3000
deadline=$((SECONDS + 240))
while ((SECONDS < deadline)); do
    if curl --noproxy '*' -fkSs --max-time 4 "$suite/api/plan/available" -o /dev/null 2>/dev/null &&
       curl --noproxy '*' -fSs --cacert "$state/provider.crt" --max-time 4 "$issuer/.well-known/openid-configuration" 2>/dev/null |
           jq -e --arg issuer "$issuer/" '.issuer == $issuer' >/dev/null; then
        echo "Ready: $mode $revision with OIDF $suite_version"
        exit 0
    fi
    sleep 2
done
"${compose[@]}" ps -a --format 'table {{.Service}}\t{{.State}}\t{{.ExitCode}}'
echo 'Loopback suite and issuer did not become ready.' >&2
exit 1
