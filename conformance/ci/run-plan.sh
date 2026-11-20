#!/usr/bin/env bash
# Drive the repository's fixed interoperability selection against the isolated CI stack.
set -euo pipefail
umask 077
case "${1:-}" in
    basic)
        plan=oidcc-basic-certification-test-plan
        variant='{"server_metadata":"discovery","client_registration":"static_client"}'
        modules=oidcc-server,oidcc-ensure-request-with-valid-pkce-succeeds,oidcc-userinfo-get
        ;;
    discovery)
        plan=oidcc-config-certification-test-plan
        variant='{}'
        modules=oidcc-discovery-endpoint-verification
        ;;
    *) echo 'Expected basic or discovery' >&2; exit 2 ;;
esac

work="${RUNNER_TEMP:?}/oidf"
source "$work/runner.env"
mkdir -p "$work/evidence"
log="$work/$1.log"
results="$work/$1-results.json"
driver=tests/OpenIdentityStack.Conformance.Runner/bin/Release/net11.0/OpenIdentityStack.Conformance.Runner.dll
runner_status=0
# Browser notes and suite exceptions can include redirect parameters. Only the
# redacted validator output and its fixed diagnostic fields belong in the job log.
dotnet "$driver" --config "$work/plan-config.json" --plan "$plan" --variant "$variant" \
    --only "$modules" --out "$results" >"$log" 2>&1 || runner_status=$?
plan_id=$(sed -nE 's/^planId : ([a-zA-Z0-9_-]+)\r?$/\1/p' "$log")
if [[ -z $plan_id || ! -s $results ]]; then
    printf '%s produced no complete results file (runner exit %s).\n' "$plan" "$runner_status" >&2
    # Print diagnostic types/status codes, never response bodies or requested URLs.
    grep -Eo '([A-Za-z]+\.)+[A-Za-z]+Exception|status code does not indicate success: [0-9]{3}|START-ERROR|STALLED|--only names [0-9]+ module\(s\)' "$log" | sort -u || true
    exit 1
fi
jq --arg plan "$plan" --arg id "$plan_id" --argjson variant "$variant" \
   '{PlanId:$id,PlanName:$plan,Variant:$variant,Results:.}' "$results" > "$work/$1-envelope.json"
tag=()
if [[ ${OIDC_TARGET:?} == candidate ]]; then tag=(--tag v1.0.0-rc.1); fi
python3 conformance/ci/evidence.py --input "$work/$1-envelope.json" --plan "$plan" \
    --only "$modules" --images "$work/images.json" "${tag[@]}" --out "$work/evidence/$1.json"
exit "$runner_status"
