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

repo_root="$(git rev-parse --show-toplevel)"
cd "$repo_root"

base_temp_root="$(mktemp -d "${TMPDIR:-/tmp}/openapi-base-XXXXXX")"
cleanup() {
  rm -rf "$base_temp_root"
}
trap cleanup EXIT

mapfile -t current_specs < <(
  find specs -type f \( -name '*.yaml' -o -name '*.yml' \) -print 2>/dev/null |
    sed 's#\\#/#g' |
    while IFS= read -r path; do
      if [[ "$path" =~ ^specs/.+/contracts/.+\.ya?ml$ ]] && is_openapi_document "$path"; then
        printf '%s\n' "$path"
      fi
    done
)

if ! mapfile -t base_tree_entries < <(git ls-tree -r --name-only "$base_ref" specs); then
  echo "Unable to list OpenAPI specs from base ref '$base_ref'. Ensure the workflow fetched the base branch before running this script." >&2
  exit 1
fi

declare -a base_specs=()
for path in "${base_tree_entries[@]}"; do
  if [[ ! "$path" =~ ^specs/.+/contracts/.+\.ya?ml$ ]]; then
    continue
  fi

  base_path="$base_temp_root/$path"
  mkdir -p "$(dirname "$base_path")"

  if ! git show "${base_ref}:$path" > "$base_path"; then
    echo "Unable to read OpenAPI spec '$path' from base ref '$base_ref'." >&2
    exit 1
  fi

  if is_openapi_document "$base_path"; then
    base_specs+=("$path")
  fi
done

declare -A all_specs=()
for spec in "${current_specs[@]}" "${base_specs[@]}"; do
  if [[ -n "$spec" ]]; then
    all_specs["$spec"]=1
  fi
done

if [[ ${#all_specs[@]} -eq 0 ]]; then
  echo "No OpenAPI contract specs found under specs/**/contracts."
  exit 0
fi

has_failures=0
specs_root="$repo_root/specs"

while IFS= read -r spec_path; do
  current_path="$repo_root/$spec_path"
  base_path="$base_temp_root/$spec_path"

  current_exists=0
  base_exists=0
  [[ -f "$current_path" ]] && current_exists=1
  [[ -f "$base_path" ]] && base_exists=1

  if [[ $base_exists -eq 0 ]]; then
    echo "Skipping new OpenAPI spec '$spec_path'; no base version exists on $base_ref."
    continue
  fi

  if [[ $current_exists -eq 0 ]]; then
    echo "::error file=$spec_path::Breaking change: OpenAPI spec '$spec_path' exists on $base_ref but was removed in this branch. Spec removal is treated as a breaking API contract change."
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
    echo "Skipping '$spec_path'; API version changed from '$base_version' to '$current_version'."
    continue
  fi

  if [[ "${current_version%%.*}" -eq 0 ]]; then
    echo "Skipping '$spec_path'; API version '$current_version' is a pre-release (major < 1); breaking changes are allowed."
    continue
  fi

  echo "Checking '$spec_path' for breaking changes at API version '$current_version'."
  if ! docker run --rm \
    -v "${base_temp_root}:/base:ro" \
    -v "${specs_root}:/workspace/specs:ro" \
    "$oasdiff_image" \
    breaking --fail-on ERR --format githubactions "--allow-external-refs=$allow_external_refs" "/base/$spec_path" "/workspace/$spec_path"; then
    has_failures=1
  fi
done < <(printf '%s\n' "${!all_specs[@]}" | sort)

if [[ $has_failures -ne 0 ]]; then
  exit 1
fi
