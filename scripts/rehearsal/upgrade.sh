#!/usr/bin/env bash
# Run only on a disposable Linux Docker host. All product images must be pinned to published digests.
set -Eeuo pipefail
umask 077
mode=${1:-local}
[[ $mode == local || $mode == fresh || $mode == export || $mode == restore ]] || exit 2
for image in RC_API RC_MIGRATOR RC_WEB BASE_API BASE_MIGRATOR BASE_WEB; do
    [[ ${!image:?Missing product image} =~ @sha256:[[:xdigit:]]{64}$ ]] || { echo "Missing digest for $image"; exit 1; }
done
script_dir=$(cd "$(dirname "$0")" && pwd)
work=$(mktemp -d)
prefix="ois-release-$(openssl rand -hex 8)"
api="$prefix-api"
web="$prefix-web"
db="$prefix-db"
network="$prefix-primary"
material="$work/primary"
issuer=https://localhost:18443
export ISSUER="$issuer" OPERATOR_ISSUER=https://localhost:18444 EMERGENCY_ISSUER=https://localhost:18444
export ADMIN_EMAIL=operator@example.test REDIRECT_URI=https://localhost:18445/auth/callback
cleanup() {
    docker rm -fv "$api" "$web" "$db" >/dev/null 2>&1 || true
    docker network rm "$prefix-primary" "$prefix-restored" >/dev/null 2>&1 || true
    rm -rf -- "$work"
}
trap cleanup EXIT
trap 'echo "FAILED at upgrade.sh line $LINENO, phase ${phase:-setup}" >&2' ERR
mkdir -p "$material" "$work/backup"
export REHEARSAL_DIR="$material"

make_certs() {
    openssl req -x509 -newkey rsa:3072 -nodes -days 2 -subj /CN=Drill-CA \
        -addext 'basicConstraints=critical,CA:true' -addext 'keyUsage=critical,keyCertSign,cRLSign' \
        -keyout "$work/ca.key" -out "$material/ca.crt" >/dev/null 2>&1
    for name in tls operator; do
        extension=serverAuth
        [[ $name == operator ]] && extension=clientAuth
        openssl req -new -newkey rsa:2048 -nodes -subj "/CN=$name" \
            -keyout "$material/$name.key" -out "$work/$name.csr" >/dev/null 2>&1
        openssl x509 -req -days 2 -in "$work/$name.csr" -CA "$material/ca.crt" -CAkey "$work/ca.key" \
            -CAserial "$work/ca.srl" -CAcreateserial -out "$material/$name.crt" \
            -extfile <(printf 'extendedKeyUsage=%s\nsubjectAltName=DNS:localhost\n' "$extension") >/dev/null 2>&1
    done
    for name in signing encryption; do
        usage=digitalSignature
        [[ $name == encryption ]] && usage=keyEncipherment
        openssl req -x509 -newkey rsa:3072 -nodes -days 2 -subj "/CN=drill-$name" -addext "keyUsage=$usage" \
            -keyout "$material/$name.key" -out "$material/$name.crt" >/dev/null 2>&1
    done
}

set_client_env() {
    export REHEARSAL_DIR="$material" CA_CERT="$material/ca.crt"
    export CLIENT_KEY="$material/operator.key" CLIENT_CERT="$material/operator.crt"
    ADMIN_PASSWORD=$(cat "$material/admin.secret")
    export ADMIN_PASSWORD
}

make_environment() {
    openssl rand -hex 24 > "$material/db.secret"
    printf 'Aa2!%s\n' "$(openssl rand -hex 24)" > "$material/admin.secret"
    cat > "$material/common.env" <<EOF
DOTNET_ENVIRONMENT=Production
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__openidentitystack=Host=postgres;Database=ois;Username=postgres;Password=$(cat "$material/db.secret")
Secrets__EncryptionKey=$(openssl rand -base64 32)
OpenIddict__Issuer=$issuer/
OpenIddict__Clients__ManagementWeb__RedirectUris__0=$REDIRECT_URI
OpenIddict__Clients__ManagementWeb__PostLogoutRedirectUris__0=https://localhost:18445/
Logging__LogLevel__Default=Warning
EOF
    cat > "$material/api.env" <<EOF
Kestrel__Endpoints__Public__Url=https://+:8443
Kestrel__Endpoints__Operator__Url=https://+:8444
Kestrel__Endpoints__Operator__ClientCertificateMode=AllowCertificate
Kestrel__Certificates__Default__Path=/drill/tls.crt
Kestrel__Certificates__Default__KeyPath=/drill/tls.key
OpenIddict__Certificates__Signing__CertificatePath=/drill/signing.crt
OpenIddict__Certificates__Signing__PrivateKeyPath=/drill/signing.key
OpenIddict__Certificates__Encryption__CertificatePath=/drill/encryption.crt
OpenIddict__Certificates__Encryption__PrivateKeyPath=/drill/encryption.key
SSL_CERT_FILE=/drill/ca.crt
EOF
}

sql() { docker exec "$db" psql -X -At -v ON_ERROR_STOP=1 -U postgres -d ois -c "$1"; }
start_postgres() {
    docker network create "$network" >/dev/null
    local db_env="$work/postgres.env"
    printf 'POSTGRES_DB=ois\nPOSTGRES_PASSWORD=%s\n' "$(cat "$material/db.secret")" > "$db_env"
    docker run -d --name "$db" --network "$network" --network-alias postgres --env-file "$db_env" postgres:17 >/dev/null
    for _ in {1..60}; do
        if docker exec "$db" pg_isready -h 127.0.0.1 -U postgres -d ois >/dev/null; then return 0; fi
        sleep 1
    done
    echo "PostgreSQL did not become ready"; return 1
}

migrate() {
    local image="$1" settings=(--env-file "$material/common.env")
    for purpose in signing encryption; do
        for extension in crt key; do
            chmod g+r "$material/$purpose.$extension"
            settings+=(--mount "type=bind,source=$material/$purpose.$extension,target=/drill/$purpose.$extension,readonly")
        done
        settings+=(-e "OpenIddict__Certificates__${purpose^}__CertificatePath=/drill/$purpose.crt"
            -e "OpenIddict__Certificates__${purpose^}__PrivateKeyPath=/drill/$purpose.key")
    done
    if [[ ${2:-} == bootstrap ]]; then
        printf 'Seed__AdminUser__Password=%s\n' "$ADMIN_PASSWORD" > "$work/seed.env"
        settings+=(--env-file "$work/seed.env" -e Seed__AdminUser__Enabled=true -e "Seed__AdminUser__Email=$ADMIN_EMAIL"
            -e Seed__AdministrativeAccess__BootstrapManagementWeb=true)
    fi
    if ! docker run --rm --network "$network" --group-add "$(id -g)" "${settings[@]}" "$image" >"$work/migrator.log" 2>&1; then
        echo "Migrator failed for $image; database remains offline."
        grep -Eo '(InvalidOperationException|[A-Za-z]+Exception|[0-9A-Z]{5}:)' "$work/migrator.log" | sort -u | head -15 || true
        grep -Eo 'at (OpenIdentityStack|Program|Npgsql)[A-Za-z0-9.<>_$]+' "$work/migrator.log" | head -12 || true
        grep -Eo 'Bootstrap password does not satisfy the password policy|OpenIddict signing and encryption certificates must be configured|The Management Web registration does not match|controlled administration bootstrap requires' "$work/migrator.log" | head -2 || true
        return 1
    fi
}

start_api() {
    local image="$1" args=() uid pin
    uid=$(sql "SELECT \"EntityId\" FROM \"AuditLogEntries\" WHERE \"Action\"='User.BootstrapCreated' AND \"Details\" LIKE 'Created initial administrator%'")
    [[ $uid =~ ^[a-fA-F0-9-]{36}$ ]] || { echo "Expected one bootstrap administrator audit identity"; return 1; }
    pin=$(openssl x509 -in "$material/operator.crt" -noout -fingerprint -sha256 | cut -d= -f2 | tr -d :)
    # Mount only the server material consumed by the API. Private operator and database credentials stay on the host.
    for file in ca.crt tls.crt tls.key signing.crt signing.key encryption.crt encryption.key; do
        chmod g+r "$material/$file"
        args+=(--mount "type=bind,source=$material/$file,target=/drill/$file,readonly")
    done
    docker run -d --name "$api" --network "$network" --group-add "$(id -g)" \
        -p 127.0.0.1:18443:8443 -p 127.0.0.1:18444:8444 "${args[@]}" \
        --env-file "$material/common.env" --env-file "$material/api.env" \
        -e "AdministrativeMfa__Emergency__UserId=$uid" -e "AdministrativeMfa__Emergency__CertificateSha256=$pin" "$image" >/dev/null
    for _ in {1..60}; do
        if curl -fsS --max-time 3 --cacert "$CA_CERT" "$issuer/health" >/dev/null 2>&1; then return 0; fi
        [[ $(docker inspect -f '{{.State.Running}}' "$api") == true ]] || break
        sleep 1
    done
    echo "API did not become ready"
    docker logs "$api" 2>&1 | grep -Eo '[A-Za-z.]+Exception' | sort -u | head -15 || true
    return 1
}
restart_api() { docker rm -f "$api" >/dev/null && start_api "$1"; }

start_web() {
    local settings=()
    if [[ $1 == "$BASE_WEB" ]]; then
        # v0.8.0's unprivileged entrypoint writes a root-owned image file. Provide that one writable file.
        install -m 0660 /dev/null "$work/v080-runtime-config.js"
        settings+=(--group-add "$(id -g)" --mount
            "type=bind,source=$work/v080-runtime-config.js,target=/usr/share/nginx/html/runtime-config.js")
    fi
    docker run -d --name "$web" --network "$network" "${settings[@]}" -p 127.0.0.1:18446:80 \
        -e "VITE_API_BASE_URL=$issuer" -e "VITE_OIDC_AUTHORITY=$issuer" -e VITE_OIDC_CLIENT_ID=management-web-client "$1" >/dev/null
    for _ in {1..45}; do
        if curl -fsS --max-time 3 http://localhost:18446/runtime-config.js -o "$work/web-runtime.js" 2>/dev/null &&
                grep -Fq "$issuer" "$work/web-runtime.js"; then return 0; fi
        [[ $(docker inspect -f '{{.State.Running}}' "$web") == true ]] || break
        sleep 1
    done
    echo "Management Web did not serve the expected runtime issuer"
    docker logs "$web" 2>&1 | grep -Fo 'runtime-config.js: Permission denied' | head -1 || true
    return 1
}

dp_hash() {
    sql 'SELECT "Xml" FROM "DataProtectionKeys" ORDER BY "Id"' | sha256sum | cut -d' ' -f1
}

recover() {
    phase=recovery
    network="$prefix-restored"
    material="$work/restored"
    mkdir "$material"
    (cd "$work/backup" && sha256sum -c checksums)
    tar -xf "$work/backup/credentials.tar" -C "$material"
    set_client_env
    start_postgres
    docker exec -i "$db" pg_restore --exit-on-error --no-owner --no-acl -U postgres -d ois < "$work/backup/ois.dump"
    [[ $(dp_hash) == "$(cat "$work/backup/dp.sha256")" ]]
    migrate "$BASE_MIGRATOR"
    start_api "$BASE_API"
    start_web "$BASE_WEB"
    python3 "$script_dir/upgrade_flows.py" recovery
    echo "PASS recovery: v0.8.0 restored to a fresh database volume, network and service instances with the original issuer and Data Protection keys"
}

if [[ $mode == restore ]]; then
    phase=decrypt
    transfer="${RUNNER_TEMP:?}/ois-transfer"
    openssl cms -decrypt -binary -inform DER -in "$transfer/backup.cms" \
        -recip "$transfer/recipient.crt" -inkey "$transfer/recipient.key" -out "$work/received.tar"
    tar -xf "$work/received.tar" -C "$work/backup" -- ois.dump credentials.tar dp.sha256 checksums
    recover
    exit 0
fi

phase=prepare
make_certs
make_environment
set_client_env
start_postgres
if [[ $mode == fresh ]]; then
    phase=fresh
    migrate "$RC_MIGRATOR" bootstrap
    start_api "$RC_API"
    start_web "$RC_WEB"
    python3 "$script_dir/kube-admin.py"
    echo "PASS clean install: Production API, migrator and Management Web from release digests"
    phase=rotation
    source "$script_dir/rotation.sh"
    rehearse_rotation
    exit 0
fi
phase=baseline
migrate "$BASE_MIGRATOR" bootstrap
start_api "$BASE_API"
start_web "$BASE_WEB"
python3 "$script_dir/upgrade_flows.py" populate

phase=maintenance
# A quiescent database and the matching credential bundle form one recovery point.
docker stop "$web" "$api" >/dev/null
docker rm "$web" "$api" >/dev/null
docker exec "$db" pg_dump -Fc --no-owner --no-acl -U postgres ois > "$work/backup/ois.dump"
[[ $(sql 'SELECT COUNT(*) FROM "DataProtectionKeys"') -gt 0 ]]
dp_hash > "$work/backup/dp.sha256"
tar -cf "$work/backup/credentials.tar" -C "$material" .
(cd "$work/backup" && sha256sum ois.dump credentials.tar > checksums)
echo "PASS backup: offline PostgreSQL dump, nonempty Data Protection key ring and matching deployment material captured"
if [[ $mode == export ]]; then
    transfer="${RUNNER_TEMP:?}/ois-transfer"
    # Only the authenticated ciphertext may cross runners. A fresh recipient privately owns the decryption key.
    tar -cf "$work/outbound.tar" -C "$work/backup" ois.dump credentials.tar dp.sha256 checksums
    openssl cms -encrypt -binary -aes-256-gcm -outform DER -in "$work/outbound.tar" \
        -out "$transfer/backup.cms.tmp" "$transfer/recipient.crt"
    mv "$transfer/backup.cms.tmp" "$transfer/backup.cms"
    echo "PASS backup: encrypted for the fresh recovery host"
fi

phase=upgrade
# Run each gate in its own subshell so a failed candidate cannot prevent rehearsal of the recovery point.
set +e
(
    set -e
    migrate "$RC_MIGRATOR"
    [[ $(dp_hash) == "$(cat "$work/backup/dp.sha256")" ]]
    start_api "$RC_API"
    start_web "$RC_WEB"
    python3 "$script_dir/upgrade_flows.py" upgrade
)
upgrade_status=$?
set -e
if [[ $upgrade_status -ne 0 ]]; then echo "Upgrade failed. Keep traffic drained and restore the paired v0.8.0 backup on a clean target."; fi

phase=rotation
rotation_status=1
if curl -fsS --max-time 3 --cacert "$CA_CERT" "$issuer/health" >/dev/null 2>&1; then
    set +e
    (set -e; source "$script_dir/rotation.sh"; rehearse_rotation)
    rotation_status=$?
    set -e
else
    echo "BLOCKED rotation: candidate unavailable"
fi

if [[ $mode == local ]]; then
    docker rm -fv "$api" "$web" >/dev/null 2>&1 || true
    docker rm -fv "$db" >/dev/null
    docker network rm "$network" >/dev/null
    recover
fi
[[ $upgrade_status == 0 && $rotation_status == 0 ]]
