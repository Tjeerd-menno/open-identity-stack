#!/usr/bin/env bash
# Requires kind, Docker, kubectl, curl, OpenSSL and Python 3 on an isolated Linux host.
# RC_API, RC_MIGRATOR and RC_WEB are full, digest-qualified release image references.
set -euo pipefail
umask 077
for tool in kind docker kubectl openssl curl python3; do command -v "$tool" >/dev/null; done
prefix=ghcr.io/tjeerd-menno/open-identity-stack
check_image() {
  local expected="$prefix-$1@sha256:"
  [[ "$2" == "$expected"* && "${2#"$expected"}" =~ ^[0-9a-f]{64}$ ]] || {
    echo "Expected a digest-qualified release image for $1" >&2
    exit 1
  }
}
check_image api "${RC_API:?}"
check_image db-migrator "${RC_MIGRATOR:?}"
check_image management-web "${RC_WEB:?}"

scripts=$(cd -- "$(dirname -- "$0")" && pwd)
work=$(mktemp -d)
cluster="ois-rehearsal-$(openssl rand -hex 8)"
export KUBECONFIG="$work/kubeconfig"
namespace=open-identity-stack
k() { kubectl --context "kind-$cluster" --namespace "$namespace" "$@"; }
cleanup() {
  local status=$?
  trap - EXIT
  if (( status != 0 )); then
    # Resource states and reasons are safe to export. Full pod logs can include tokens.
    k get pods,jobs 2>/dev/null || true
    k get events -o 'custom-columns=KIND:.involvedObject.kind,NAME:.involvedObject.name,REASON:.reason' \
      2>/dev/null | tail -20 || true
  fi
  if [[ -n "${api_forward:-}" ]]; then kill "$api_forward" 2>/dev/null || true; fi
  if [[ -n "${web_forward:-}" ]]; then kill "$web_forward" 2>/dev/null || true; fi
  kind delete cluster --name "$cluster" --kubeconfig "$KUBECONFIG"
  rm -rf -- "$work"
  exit "$status"
}
cd "$work"
trap cleanup EXIT
kind create cluster --name "$cluster" --kubeconfig "$KUBECONFIG" --wait 180s
k create namespace "$namespace"

# Three independent loopback listeners. The private client key stays on the runner.
export ISSUER=https://127.0.0.1:18443 EMERGENCY_ISSUER=https://127.0.0.1:19443
web_origin=http://127.0.0.1:18080
export REDIRECT_URI="$web_origin/auth/callback"
export ADMIN_EMAIL=operator@example.test
ADMIN_PASSWORD="Aa1!$(openssl rand -hex 32)"
export ADMIN_PASSWORD
export CA_CERT="$work/ca.crt" CLIENT_CERT="$work/operator.crt" CLIENT_KEY="$work/operator.key"

openssl req -x509 -newkey rsa:3072 -nodes -days 2 -subj '/CN=Rehearsal Root' \
  -addext 'basicConstraints=critical,CA:TRUE' -addext 'keyUsage=critical,keyCertSign,cRLSign' \
  -keyout ca.key -out ca.crt 2>/dev/null
issue_leaf() {
  local name="$1" usage="$2"
  openssl req -new -newkey rsa:2048 -nodes -subj "/CN=$name" \
    -keyout "$name.key" -out "$name.csr" 2>/dev/null
  openssl x509 -req -in "$name.csr" -CA ca.crt -CAkey ca.key -CAcreateserial -days 2 \
    -extfile <(printf 'basicConstraints=critical,CA:FALSE\n%s\n' "$usage") -out "$name.crt" 2>/dev/null
}
issue_leaf server $'keyUsage=digitalSignature,keyEncipherment\nextendedKeyUsage=serverAuth\nsubjectAltName=IP:127.0.0.1'
issue_leaf operator $'keyUsage=digitalSignature\nextendedKeyUsage=clientAuth'
issue_leaf signing 'keyUsage=critical,digitalSignature'
issue_leaf encryption 'keyUsage=critical,keyEncipherment'
k create secret generic oidc-certificates --from-file=signing.crt --from-file=signing.key \
  --from-file=encryption.crt --from-file=encryption.key
k create secret generic listener-certificates --from-file=ca.crt --from-file=server.key --from-file=server.crt
db_password=$(openssl rand -hex 32)
printf 'POSTGRES_USER=ois\nPOSTGRES_DB=ois\nPOSTGRES_PASSWORD=%s\n' "$db_password" >postgres.env
printf 'ConnectionStrings__openidentitystack=Host=postgres;Database=ois;Username=ois;Password=%s\nSecrets__EncryptionKey=%s\n' \
  "$db_password" "$(openssl rand -base64 32)" >app.env
k create secret generic postgres --from-env-file=postgres.env
k create secret generic open-identity-stack-app --from-env-file=app.env
cat >seed.env <<EOF
Seed__AdministrativeAccess__BootstrapManagementWeb=true
Seed__AdminUser__Enabled=true
Seed__AdminUser__Email=$ADMIN_EMAIL
Seed__AdminUser__Password=$ADMIN_PASSWORD
EOF
k create secret generic open-identity-stack-admin-seed --from-env-file=seed.env
cat >authority.env <<EOF
DOTNET_ENVIRONMENT=Production
ASPNETCORE_ENVIRONMENT=Production
OpenIddict__Issuer=$ISSUER/
OpenIddict__Clients__ManagementWeb__RedirectUris__0=$REDIRECT_URI
OpenIddict__Clients__ManagementWeb__RedirectUris__1=$web_origin/auth/silent-callback
OpenIddict__Clients__ManagementWeb__PostLogoutRedirectUris__0=$web_origin/
AllowedCorsOrigins=$web_origin
OpenIddict__Certificates__Signing__CertificatePath=/run/oidc/signing.crt
OpenIddict__Certificates__Signing__PrivateKeyPath=/run/oidc/signing.key
OpenIddict__Certificates__Encryption__CertificatePath=/run/oidc/encryption.crt
OpenIddict__Certificates__Encryption__PrivateKeyPath=/run/oidc/encryption.key
EOF
k create configmap authority --from-env-file=authority.env
k apply -f "$scripts/kube-db.yaml"
k rollout status deployment/postgres --timeout=240s
sed "s|MIGRATOR_IMAGE|$RC_MIGRATOR|" "$scripts/kube-migrator.yaml" | k apply -f -
k wait --for=condition=Complete job/open-identity-stack-db-migrator --timeout=370s
k delete secret open-identity-stack-admin-seed
echo "PASS: production migrator completed on fresh PostgreSQL"

sql="SELECT \"EntityId\" FROM \"AuditLogEntries\" WHERE \"Action\" = 'User.BootstrapCreated';"
admin_id=$(k exec deployment/postgres -- psql -XAt -v ON_ERROR_STOP=1 -U ois -d ois -c "$sql")
[[ "$admin_id" =~ ^[0-9a-fA-F]{8}(-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}$ ]]
fingerprint=$(openssl x509 -in operator.crt -noout -fingerprint -sha256 | cut -d= -f2 | tr -d :)
cat >listeners.env <<EOF
AdministrativeMfa__Emergency__UserId=$admin_id
AdministrativeMfa__Emergency__CertificateSha256=$fingerprint
Kestrel__Certificates__Default__Path=/run/tls/server.crt
Kestrel__Certificates__Default__KeyPath=/run/tls/server.key
Kestrel__Endpoints__Public__Url=https://+:8443
Kestrel__Endpoints__Operator__Url=https://+:9443
Kestrel__Endpoints__Operator__ClientCertificateMode=AllowCertificate
SSL_CERT_FILE=/run/tls/ca.crt
EOF
k create configmap listeners --from-env-file=listeners.env
k create configmap web --from-literal="VITE_API_BASE_URL=$ISSUER" \
  --from-literal="VITE_OIDC_AUTHORITY=$ISSUER" --from-literal=VITE_OIDC_CLIENT_ID=management-web-client
sed -e "s|API_IMAGE|$RC_API|" -e "s|WEB_IMAGE|$RC_WEB|" "$scripts/kube-services.yaml" | k apply -f -
k rollout status deployment/open-identity-stack-api --timeout=300s
k rollout status deployment/open-identity-stack-managementweb --timeout=300s
k port-forward deployment/open-identity-stack-api --address=127.0.0.1 18443:8443 19443:9443 >api-forward.log 2>&1 &
api_forward=$!
k port-forward deployment/open-identity-stack-managementweb --address=127.0.0.1 18080:80 >web-forward.log 2>&1 &
web_forward=$!
curl -fsS --noproxy '*' --cacert ca.crt --retry 15 --retry-connrefused --max-time 10 \
  "$ISSUER/health" | grep -Fxq Healthy
curl -fsS --noproxy '*' --retry 15 --retry-connrefused --max-time 10 \
  "$web_origin/runtime-config.js" | grep -Fq "$ISSUER"
curl -fsS --noproxy '*' "$web_origin/" | grep -Fq 'id="root"'
echo "PASS: API readiness, Management Web entry page and issuer configuration"
python3 "$scripts/kube-admin.py"
audit_count=$(k exec deployment/postgres -- psql -XAt -v ON_ERROR_STOP=1 -U ois -d ois -c \
  "SELECT count(*) FROM \"AuditLogEntries\" WHERE \"Action\" = 'EmergencyAdministrator.Authenticated' AND \"EntityId\" = '$admin_id';")
[[ "$audit_count" == 1 ]]
echo "PASS: emergency authentication audit matches the bootstrapped operator"
k get pods -o 'custom-columns=WORKLOAD:.metadata.name,IMAGE:.spec.containers[0].image,IMAGE_ID:.status.containerStatuses[0].imageID'
