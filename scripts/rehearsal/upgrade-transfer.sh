#!/usr/bin/env bash
# Exchange only a public recipient certificate and an authenticated, encrypted backup between runner VMs.
set -Eeuo pipefail
umask 077
transfer="${RUNNER_TEMP:?}/ois-transfer"
mkdir -p "$transfer"
case "${1:-}" in
    keygen)
        openssl req -new -x509 -newkey rsa:4096 -nodes -days 1 -subj /CN=disposable-recovery-host \
            -addext 'keyUsage=critical,keyEncipherment' -keyout "$transfer/recipient.key" \
            -out "$transfer/recipient.crt" 2>/dev/null
        exit 0 ;;
    wait-key)
        artifact="ois-drill-key-${GITHUB_RUN_ATTEMPT:?}"
        peer=recovery ;;
    wait-backup)
        artifact="ois-drill-backup-${GITHUB_RUN_ATTEMPT:?}"
        peer=upgrade-recovery-rotation ;;
    *) exit 2 ;;
esac

# Scope both artifacts and completion checks to this exact workflow run and attempt.
run="repos/${GITHUB_REPOSITORY:?}/actions/runs/${GITHUB_RUN_ID:?}"
deadline=$((SECONDS + 1500))
while ((SECONDS < deadline)); do
    id=$(gh api "$run/artifacts?name=$artifact" --jq '.artifacts[] | select(.expired == false) | .id')
    if [[ $id =~ ^[0-9]+$ ]]; then
        gh run download "$GITHUB_RUN_ID" --repo "$GITHUB_REPOSITORY" --name "$artifact" --dir "$transfer"
        echo "Retrieved $artifact from the paired runner"
        exit 0
    fi
    status=$(gh api "$run/attempts/$GITHUB_RUN_ATTEMPT/jobs" --jq ".jobs[] | select(.name == \"$peer\") | .status")
    if [[ $status == completed ]]; then
        echo "The $peer job finished without publishing $artifact"
        exit 1
    fi
    sleep 10
done
echo "Timed out waiting for $artifact"
exit 1
