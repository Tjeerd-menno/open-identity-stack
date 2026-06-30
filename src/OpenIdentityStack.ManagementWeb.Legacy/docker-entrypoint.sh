#!/bin/sh
set -eu

cat <<EOF >/usr/share/nginx/html/runtime-config.js
window.__OIS_RUNTIME_CONFIG__ = {
  apiBaseUrl: "${VITE_API_BASE_URL:-}",
  oidcAuthority: "${VITE_OIDC_AUTHORITY:-}",
  oidcClientId: "${VITE_OIDC_CLIENT_ID:-}"
};
EOF
