# Sourced by upgrade.sh with a healthy isolated candidate and its recovered configuration.
rehearse_rotation() (
    set -euo pipefail
    local probe
    probe="$(dirname "${BASH_SOURCE[0]}")/rotation.py"
    /usr/bin/python3 "$probe" baseline
    for purpose in signing encryption; do
        openssl req -x509 -newkey rsa:3072 -nodes -sha256 -days 7 \
            -subj "/CN=ois-rc1-next-$purpose" \
            -keyout "$material/$purpose.next.key" -out "$material/$purpose.next.crt" 2>/dev/null
        chmod --reference="$material/$purpose.key" "$material/$purpose.next.key"
        chmod --reference="$material/$purpose.crt" "$material/$purpose.next.crt"
        mv "$material/$purpose.next.key" "$material/$purpose.key"
        mv "$material/$purpose.next.crt" "$material/$purpose.crt"
        restart_api "$RC_API"
        /usr/bin/python3 "$probe" "$purpose"
    done
)
