"""Probe a single-key transition using the relying party's OIDC discovery and PKCE flow."""

import base64
import json
import os
from pathlib import Path
import ssl
import sys
import time
import urllib.error
import urllib.parse
import urllib.request

from cryptography.hazmat.primitives import hashes
from cryptography.hazmat.primitives.asymmetric import padding, rsa
from upgrade_flows import browser_tokens


def decode(value):
    return base64.urlsafe_b64decode(value + "=" * (-len(value) % 4))


def require(condition, message):
    if not condition:
        raise RuntimeError(message)


def main(stage):
    issuer = os.environ["ISSUER"].rstrip("/")
    material = Path(os.environ["REHEARSAL_DIR"])
    tls = ssl.create_default_context(cafile=str(material / "ca.crt"))

    def get(url):
        require(url.startswith(issuer + "/"), "Discovery endpoint is outside the rehearsal authority")
        with urllib.request.urlopen(url, context=tls, timeout=15) as response:
            return json.load(response)

    metadata = get(issuer + "/.well-known/openid-configuration")
    require(metadata["issuer"].rstrip("/") == issuer, "Unexpected discovery issuer")
    keys = get(metadata["jwks_uri"])["keys"]
    by_id = {key["kid"]: key for key in keys if key.get("use", "sig") == "sig"}
    tokens = browser_tokens()
    header, payload, signature = tokens["id_token"].split(".")
    info = json.loads(decode(header))
    claims = json.loads(decode(payload))
    key = by_id[info["kid"]]
    require(info["alg"] == "RS256" and key["kty"] == "RSA", "Unexpected signing algorithm")
    rsa.RSAPublicNumbers(
        int.from_bytes(decode(key["e"]), "big"), int.from_bytes(decode(key["n"]), "big")
    ).public_key().verify(decode(signature), (header + "." + payload).encode(),
                          padding.PKCS1v15(), hashes.SHA256())
    audience = claims["aud"] if isinstance(claims["aud"], list) else [claims["aud"]]
    require(claims["iss"] == metadata["issuer"] and claims["exp"] > time.time()
            and "management-web-client" in audience, "ID token issuer, expiry or audience mismatch")
    access = tokens["access_token"].split(".")
    require(len(access) == 5, "Expected an encrypted Production access token")
    enc_kid = json.loads(decode(access[0]))["kid"]

    def admin_read(value):
        req = urllib.request.Request(issuer + "/api/admin/users",
                                     headers={"Authorization": "Bearer " + value})
        try:
            with urllib.request.urlopen(req, context=tls, timeout=15) as result:
                require("items" in json.load(result), "Unexpected users response")
                return result.status
        except urllib.error.HTTPError as error:
            if error.code == 401:
                return error.code
            raise RuntimeError(f"Unexpected admin response HTTP {error.code}") from None

    def refresh(value):
        data = urllib.parse.urlencode(dict(grant_type="refresh_token", client_id="management-web-client",
                                          refresh_token=value)).encode()
        endpoint = metadata["token_endpoint"]
        require(endpoint.startswith(issuer + "/"), "Unexpected token endpoint")
        try:
            with urllib.request.urlopen(endpoint, data=data, context=tls, timeout=15) as result:
                require(bool(json.load(result)["access_token"]), "Refresh response missing token")
                return "accepted"
        except urllib.error.HTTPError as error:
            if error.code == 400 and json.load(error).get("error") == "invalid_grant":
                return "invalid_grant"
            raise RuntimeError(f"Unexpected refresh response HTTP {error.code}") from None

    # Keep a separate, never-redeemed grant for the subsequent transition.
    require(admin_read(tokens["access_token"]) == 200, "New access token rejected by Admin API")
    require(refresh(tokens["refresh_token"]) == "accepted", "Fresh grant failed to refresh")
    state_file = material.parent / "rotation-state.json"
    if stage != "baseline":
        previous = json.loads(state_file.read_text())
        if stage == "signing":
            require(previous["sig"] != info["kid"] and previous["sig"] not in by_id
                    and previous["enc"] == enc_kid, "Signing key replacement mismatch")
        elif stage == "encryption":
            require(previous["sig"] == info["kid"] and previous["enc"] != enc_kid,
                    "Encryption key replacement mismatch")
        else:
            raise ValueError("Unknown rotation stage")
        previous_refresh = refresh(previous["refresh"])
        previous_read = admin_read(previous["access"])
        print(f"{stage} replacement: previous access HTTP {previous_read}, previous unused refresh = {previous_refresh}")
        require(previous_read == 401 and previous_refresh == "invalid_grant",
                f"Previously issued credentials behaved differently from the RC1 {stage} runbook")
    if stage != "encryption":
        fd = os.open(state_file, os.O_WRONLY | os.O_CREAT | os.O_TRUNC, 0o600)
        held = browser_tokens()
        require(admin_read(held["access_token"]) == 200, "Pre-transition token rejected")
        with os.fdopen(fd, "w") as state:
            json.dump(dict(sig=info["kid"], enc=enc_kid, refresh=held["refresh_token"], access=held["access_token"]), state)
    else:
        state_file.unlink()
    print(f"{stage}: verified discovery, ID-token signature and claims, encrypted access token and fresh refresh")


if __name__ == "__main__":
    main(sys.argv[1])
