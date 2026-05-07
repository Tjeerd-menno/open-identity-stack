#!/bin/sh
set -eu

: "${VITE_API_BASE_URL:=http://localhost:5000}"
: "${VITE_OIDC_AUTHORITY:=http://localhost:5000}"
: "${VITE_OIDC_CLIENT_ID:=admin-web-client}"
: "${VITE_OIDC_REDIRECT_URI:=http://localhost:3000/auth/callback}"
: "${VITE_OIDC_POST_LOGOUT_REDIRECT_URI:=http://localhost:3000/}"
: "${VITE_OIDC_SILENT_REDIRECT_URI:=http://localhost:3000/auth/silent-callback}"
: "${VITE_OIDC_SCOPE:=openid profile email api}"
: "${VITE_APP_NAME:=OpenIdentityStack Admin}"
: "${VITE_APP_VERSION:=1.0.0}"

export VITE_API_BASE_URL VITE_OIDC_AUTHORITY VITE_OIDC_CLIENT_ID VITE_OIDC_REDIRECT_URI
export VITE_OIDC_POST_LOGOUT_REDIRECT_URI VITE_OIDC_SILENT_REDIRECT_URI VITE_OIDC_SCOPE
export VITE_APP_NAME VITE_APP_VERSION

envsubst < /usr/share/nginx/html/config.template.js > /usr/share/nginx/html/config.js