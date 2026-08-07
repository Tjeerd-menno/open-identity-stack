#!/usr/bin/env bash

set -euo pipefail

base_ref=""
oasdiff_image="tufin/oasdiff:v1.15.0"
allow_external_refs="false"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --base-ref)
      base_ref="${2:-}"
      shift 2
      ;;
    --oasdiff-image)
      oasdiff_image="${2:-}"
      shift 2
      ;;
    --allow-external-refs)
      allow_external_refs="true"
      shift
      ;;
    *)
      echo "Unknown argument: $1" >&2
      exit 1
      ;;
  esac
done

if [[ -z "$base_ref" ]]; then
  echo "--base-ref is required" >&2
  exit 1
fi

get_openapi_version() {
  local path="$1"

  awk '
    /^info:[[:space:]]*$/ { in_info=1; next }
    in_info && /^[^[:space:]]/ { exit }
    in_info && /^[[:space:]]+version:[[:space:]]*/ {
      line = $0
      sub(/^[[:space:]]+version:[[:space:]]*/, "", line)
      sub(/[[:space:]]*(#.*)?$/, "", line)
      gsub(/^["'"'"']|["'"'"']$/, "", line)
      print line
      found = 1
      exit
    }
    END {
      if (!found) {
        exit 1
      }
    }
  ' "$path"
}

is_openapi_document() {
  local path="$1"
  grep -Eq '^openapi:[[:space:]]*' "$path"
}

get_contract_key() {
  local path="$1"

  if [[ "$path" =~ ^specs/(.+)/contracts/(.+\.ya?ml)$ ]]; then
    printf '%s/%s\n' "${BASH_REMATCH[1]}" "${BASH_REMATCH[2]}"
    return 0
  fi

  if [[ "$path" =~ ^contracts/openapi/(.+\.ya?ml)$ ]]; then
    printf '%s\n' "${BASH_REMATCH[1]}"
    return 0
  fi

  return 1
}

repo_root="$(git rev-parse --show-toplevel)"
cd "$repo_root"

base_temp_root="$(mktemp -d "${TMPDIR:-/tmp}/openapi-base-XXXXXX")"
cleanup() {
  rm -rf "$base_temp_root"
}
trap cleanup EXIT

declare -A current_specs_by_key=()
while IFS= read -r path; do
  if key="$(get_contract_key "$path")" && is_openapi_document "$path"; then
    current_specs_by_key["$key"]="$path"
  fi
done < <(
  find specs contracts/openapi -type f \( -name '*.yaml' -o -name '*.yml' \) -print 2>/dev/null |
    sed 's#\\#/#g'
)

if ! mapfile -t base_tree_entries < <(git ls-tree -r --name-only "$base_ref" specs contracts/openapi 2>/dev/null); then
  echo "Unable to list OpenAPI specs from base ref '$base_ref'. Ensure the workflow fetched the base branch before running this script." >&2
  exit 1
fi

declare -A base_specs_by_key=()
for path in "${base_tree_entries[@]}"; do
  if ! key="$(get_contract_key "$path")"; then
    continue
  fi

  base_path="$base_temp_root/$key"
  mkdir -p "$(dirname "$base_path")"

  if ! git show "${base_ref}:$path" > "$base_path"; then
    echo "Unable to read OpenAPI spec '$path' from base ref '$base_ref'." >&2
    exit 1
  fi

  if is_openapi_document "$base_path"; then
    base_specs_by_key["$key"]="$path"
  fi
done

declare -A all_specs=()
for key in "${!current_specs_by_key[@]}" "${!base_specs_by_key[@]}"; do
  all_specs["$key"]=1
done

if [[ ${#all_specs[@]} -eq 0 ]]; then
  echo "No OpenAPI contract specs found under contracts/openapi or specs/**/contracts."
  exit 0
fi

has_failures=0

while IFS= read -r spec_key; do
  current_spec_path="${current_specs_by_key[$spec_key]:-}"
  base_spec_path="${base_specs_by_key[$spec_key]:-}"
  current_path="$repo_root/$current_spec_path"
  base_path="$base_temp_root/$spec_key"

  current_exists=0
  base_exists=0
  [[ -f "$current_path" ]] && current_exists=1
  [[ -f "$base_path" ]] && base_exists=1

  if [[ $base_exists -eq 0 ]]; then
    echo "Skipping new OpenAPI spec '$current_spec_path'; no base version exists on $base_ref."
    continue
  fi

  if [[ $current_exists -eq 0 ]]; then
    echo "::error file=$base_spec_path::Breaking change: OpenAPI spec '$base_spec_path' exists on $base_ref but was removed in this branch. Spec removal is treated as a breaking API contract change."
    has_failures=1
    continue
  fi

  if ! base_version="$(get_openapi_version "$base_path")"; then
    echo "OpenAPI document '$base_path' is missing info.version." >&2
    exit 1
  fi

  if ! current_version="$(get_openapi_version "$current_path")"; then
    echo "OpenAPI document '$current_path' is missing info.version." >&2
    exit 1
  fi

  if [[ "$base_version" != "$current_version" ]]; then
    echo "Skipping '$current_spec_path'; API version changed from '$base_version' to '$current_version'."
    continue
  fi

  if [[ "${current_version%%.*}" -eq 0 ]]; then
    echo "Skipping '$current_spec_path'; API version '$current_version' is a pre-release (major < 1); breaking changes are allowed."
    continue
  fi

  echo "Checking '$current_spec_path' for breaking changes at API version '$current_version'."
  if ! docker run --rm \
    -v "${base_temp_root}:/base:ro" \
    -v "${repo_root}:/workspace:ro" \
    "$oasdiff_image" \
    breaking --fail-on ERR --format githubactions "--allow-external-refs=$allow_external_refs" "/base/$spec_key" "/workspace/$current_spec_path"; then
    has_failures=1
  fi
done < <(printf '%s\n' "${!all_specs[@]}" | sort)

if [[ $has_failures -ne 0 ]]; then
  exit 1
fi
