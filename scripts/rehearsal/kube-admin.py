"""Exercise a published issuer on loopback with an isolated emergency operator."""

import base64
import hashlib
import json
import os
import secrets
import ssl
from html.parser import HTMLParser
from http.cookiejar import CookieJar
from urllib.error import HTTPError
from urllib.parse import parse_qs, urlencode, urlsplit
from urllib.request import (
    HTTPCookieProcessor, HTTPRedirectHandler, HTTPSHandler, ProxyHandler, Request,
    build_opener,
)


class NoRedirect(HTTPRedirectHandler):
    def redirect_request(self, *args):
        return None


class LoginForm(HTMLParser):
    verification_token = None

    def handle_starttag(self, tag, attrs):
        fields = dict(attrs)
        if tag == "input" and fields.get("name") == "__RequestVerificationToken":
            self.verification_token = fields.get("value")


def issuer(name):
    value = os.environ[name].rstrip("/")
    parts = urlsplit(value)
    if (parts.scheme != "https" or parts.hostname not in ("127.0.0.1", "localhost", "::1")
            or parts.username or parts.password or parts.path or parts.query or parts.fragment):
        raise ValueError(f"{name} must identify a loopback HTTPS listener")
    return value


def browser(cookies, emergency=False):
    context = ssl.create_default_context(cafile=os.environ.get("CA_CERT"))
    if emergency:
        context.load_cert_chain(os.environ["CLIENT_CERT"], os.environ["CLIENT_KEY"])
    return build_opener(ProxyHandler({}), HTTPSHandler(context=context),
                        HTTPCookieProcessor(cookies), NoRedirect())


def send(client, url, expected, *, fields=None, token=None):
    headers = {"Authorization": "Bearer " + token} if token else {}
    data = urlencode(fields).encode() if fields is not None else None
    try:
        result = client.open(Request(url, data=data, headers=headers), timeout=20)
    except HTTPError as error:
        result = error
    with result:
        if result.code != expected:
            # OAuth parameters and response bodies may contain credentials.
            raise RuntimeError(f"{urlsplit(url).path} returned HTTP {result.code}, expected {expected}")
        return result.headers, result.read()


def login():
    public = issuer("ISSUER")
    recovery = issuer("EMERGENCY_ISSUER")
    if public == recovery or urlsplit(public).hostname != urlsplit(recovery).hostname:
        raise ValueError("Recovery and public listeners must share a hostname on distinct ports")
    cookies = CookieJar()
    operator = browser(cookies, emergency=True)
    ordinary = browser(cookies)
    _, body = send(ordinary, public + "/.well-known/openid-configuration", 200)
    if json.loads(body)["issuer"].rstrip("/") != public:
        raise RuntimeError("Discovery returned a different issuer")

    _, body = send(operator, recovery + "/Account/Login", 200)
    form = LoginForm()
    form.feed(body.decode())
    if not form.verification_token:
        raise RuntimeError("Login page missing its antiforgery token")

    client_id = "management-web-client"
    callback = os.environ["REDIRECT_URI"]
    verifier = secrets.token_urlsafe(48)
    challenge = base64.urlsafe_b64encode(hashlib.sha256(verifier.encode()).digest()).decode().rstrip("=")
    state = secrets.token_urlsafe(32)
    path = "/connect/authorize?" + urlencode({
        "client_id": client_id, "redirect_uri": callback, "response_type": "code",
        "scope": "openid profile email ois.admin offline_access", "state": state,
        "code_challenge_method": "S256", "code_challenge": challenge,
    })
    send(operator, recovery + "/Account/Login", 302, fields={
        "Email": os.environ["ADMIN_EMAIL"], "Password": os.environ["ADMIN_PASSWORD"],
        "__RequestVerificationToken": form.verification_token, "returnUrl": path,
    })
    headers, _ = send(ordinary, public + path, 302)
    location = urlsplit(headers.get("Location", ""))
    if location._replace(query="", fragment="").geturl() != callback:
        raise RuntimeError("Authorization did not return the registered redirect URI")
    query = parse_qs(location.query)
    if query.get("state") != [state] or len(query.get("code", [])) != 1:
        raise RuntimeError("Authorization did not return a code and matching state")
    _, body = send(ordinary, public + "/connect/token", 200, fields={
        "client_id": client_id, "redirect_uri": callback, "grant_type": "authorization_code",
        "code": query["code"][0], "code_verifier": verifier,
    })
    tokens = json.loads(body)
    if not all(tokens.get(key) for key in ("access_token", "refresh_token", "id_token")):
        raise RuntimeError("Code exchange omitted an expected token")
    return tokens


def verify_admin():
    tokens = login()
    client = browser(CookieJar())
    _, body = send(client, issuer("ISSUER") + "/api/admin/users", 200, token=tokens["access_token"])
    if not any(user["email"] == os.environ["ADMIN_EMAIL"] for user in json.loads(body)["items"]):
        raise RuntimeError("Authorized users list did not include the fixture administrator")
    print("PASS: discovery issuer, certificate/password login, Management Web PKCE, authorized user list")


if __name__ == "__main__":
    verify_admin()
