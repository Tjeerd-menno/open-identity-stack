"""Exercise published services using the normal browser, client and administration endpoints."""

import json
import os
import runpy
import secrets
import ssl
import sys
from pathlib import Path
from urllib.error import HTTPError
from urllib.parse import urlencode
from urllib.request import Request, urlopen

MACHINE_CLIENT = "release-rehearsal-worker"


def directory():
    return Path(os.environ["REHEARSAL_DIR"])


def browser_tokens():
    return runpy.run_path(str(Path(__file__).with_name("kube-admin.py")))["login"]()


def request(path, method="GET", payload=None, token=None, form=False):
    headers = {}
    if token:
        headers["Authorization"] = f"Bearer {token}"
    data = None
    if payload is not None:
        headers["Content-Type"] = "application/x-www-form-urlencoded" if form else "application/json"
        data = (urlencode(payload) if form else json.dumps(payload)).encode()
    req = Request(os.environ["ISSUER"] + path, data=data, method=method, headers=headers)
    try:
        with urlopen(req, context=ssl.create_default_context(cafile=os.environ["CA_CERT"]), timeout=15) as response:
            body = response.read()
            return json.loads(body) if body else None
    except HTTPError as exc:
        # Responses can include tokens or query strings. Report only a bounded error identifier.
        try:
            body = json.load(exc)
            code = body.get("error") or body.get("code") or ""
            if not isinstance(code, str) or not all(c.isalnum() or c in "._-" for c in code):
                code = ""
        except (ValueError, AttributeError):
            code = ""
        raise RuntimeError(f"{method} {path.split('?')[0]} returned {exc.code} {code}") from None


def machine_tokens():
    return request("/connect/token", "POST", {
        "grant_type": "client_credentials", "client_id": MACHINE_CLIENT,
        "client_secret": (directory() / "machine.secret").read_text(), "scope": "ois.admin",
    }, form=True)


def redeem_refresh(refresh):
    return request("/connect/token", "POST", {
        "grant_type": "refresh_token", "refresh_token": refresh, "client_id": "management-web-client",
    }, form=True)


def populate(token):
    request("/api/admin/users", "POST", {
        "email": "existing-user@example.test", "displayName": "Existing release rehearsal user",
        "password": "Aa2!" + secrets.token_urlsafe(36),
    }, token)
    root = "/api/admin/applications"
    app = request(root, "POST", {
        "clientId": MACHINE_CLIENT, "displayName": "Release rehearsal worker", "profile": "MachineToMachine",
        "clientType": "Confidential", "allowedGrantTypes": ["client_credentials"], "allowedScopes": ["ois.admin"],
        "redirectUris": [], "postLogoutRedirectUris": [], "requireConsent": False, "requirePkce": False,
    }, token)
    base = f"{root}/{app['id']}"
    secret = request(base + "/credentials/client-secrets", "POST",
                     {"description": "Disposable release drill", "revokeExisting": False}, token)
    (directory() / "machine.secret").write_text(secret["clientSecret"])
    request(base + "/administrative-access", "PUT", {
        "delegatedPermissions": [], "applicationPermissions": ["users:read"],
        "expectedRevision": None, "acknowledgeAdministrativeAccess": True,
    }, token)


def registration_snapshot(token):
    root = "/api/admin/applications"
    rows = {r["clientId"]: r for r in request(root, token=token)["items"]}
    issuer = os.environ["ISSUER"]
    metadata = request("/.well-known/openid-configuration")
    if metadata["issuer"].rstrip("/") != issuer or not metadata["jwks_uri"].startswith(issuer + "/"):
        raise RuntimeError("Unexpected discovery issuer or signing-key endpoint")
    keys = request(metadata["jwks_uri"][len(issuer):])["keys"]
    published = sorted(key["kid"] for key in keys if key.get("use", "sig") == "sig")
    if not published:
        raise RuntimeError("No public signing key in discovery")
    users = request("/api/admin/users", token=token)["items"]
    [existing] = [user for user in users if user["email"] == "existing-user@example.test"]
    result = {
        "administrator": request("/api/me", token=token),
        "existingUser": {prop: existing[prop] for prop in ("id", "email", "displayName", "status")},
        "issuer": metadata["issuer"], "publishedSigningKeyIds": published,
    }
    for client in ("management-web-client", MACHINE_CLIENT):
        app_url = root + "/" + rows[client]["id"]
        app = request(app_url, token=token)
        result[client] = {key: app[key] for key in (
            "id", "clientId", "status", "redirectUris", "clientType", "allowedScopes", "allowedGrantTypes", "requirePkce")}
        result[client]["grant"] = request(app_url + "/administrative-access", token=token)
    return result


def verify(stage):
    tokens = browser_tokens()
    token = tokens["access_token"]
    if stage == "populate":
        populate(token)
    snapshot = registration_snapshot(token)
    reference = directory() / "registrations.json"
    if stage == "populate":
        reference.write_text(json.dumps(snapshot))
    elif snapshot != json.loads(reference.read_text()):
        raise RuntimeError("Registered client, user, grant, issuer or published signing key changed")
    refreshed = redeem_refresh(tokens["refresh_token"])
    if request("/api/me", token=refreshed["access_token"]) != snapshot["administrator"]:
        raise RuntimeError("Refresh changed administrator identity or privileges")
    recovery_probe = directory() / "recovery-refresh.secret"
    if stage == "populate":
        recovery_probe.write_text(refreshed["refresh_token"])
    elif stage == "recovery":
        recovered = redeem_refresh(recovery_probe.read_text())
        if request("/api/me", token=recovered["access_token"]) != snapshot["administrator"]:
            raise RuntimeError("Pre-backup refresh did not recover the original user and permissions")
        print("PASS recovery: pre-backup browser refresh token retained its session and privileges", flush=True)
    machine = machine_tokens()["access_token"]
    if request("/api/me", token=machine)["permissions"] != ["users:read"]:
        raise RuntimeError("Unexpected machine permission ceiling")
    request("/api/admin/users", token=machine)
    print(f"PASS {stage}: retained issuer and JWKS, administrator sign-in, PKCE, refresh, user, registered clients and grants, machine token and authorized API read", flush=True)
    if stage == "upgrade":
        # Obtain a second session and revoke it using the independent administrator session.
        uid = snapshot["administrator"]["subject"]
        endpoint = f"/api/admin/sessions?userId={uid}&status=Active&pageSize=100"
        before = {r["id"] for r in request(endpoint, token=token)["items"]}
        disposable = browser_tokens()
        after = {r["id"] for r in request(endpoint, token=token)["items"]}
        added = after - before
        if len(added) != 1:
            raise RuntimeError("Could not uniquely identify the disposable session")
        resource = "/api/admin/sessions/" + added.pop()
        request(resource, "DELETE", token=token)
        if request(resource, token=token)["status"] != "Revoked":
            raise RuntimeError("Revocation did not persist")
        try:
            redeem_refresh(disposable["refresh_token"])
        except RuntimeError as error:
            if "400 invalid_grant" not in str(error):
                raise
        else:
            raise RuntimeError("Revoked session could refresh")
        print("PASS upgrade: revoked session persisted and its refresh token returned invalid_grant", flush=True)


if __name__ == "__main__":
    verify(sys.argv[1])
