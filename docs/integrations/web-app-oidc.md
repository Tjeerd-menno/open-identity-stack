# Web app OIDC integration

Use this path for browser-based applications that sign in users interactively.

## Recommended flow

Authorization code with PKCE is the default fit for modern web applications.

## What you need

- a registered client
- exact redirect URIs
- post-logout redirect URIs
- the public authority URL

## Concrete example

For a public authority hosted at `https://identity.example.com`, the key protocol URLs look like this:

```text
https://identity.example.com/.well-known/openid-configuration
https://identity.example.com/connect/authorize
https://identity.example.com/connect/token
https://identity.example.com/connect/userinfo
https://identity.example.com/connect/logout
```

Example client values:

```text
client_id: admin-portal
redirect_uri: https://admin.example.com/auth/callback
post_logout_redirect_uri: https://admin.example.com/
```

## Verification checklist

1. the application can load the discovery document
2. the redirect URI matches exactly
3. login returns to the app successfully
4. logout returns to the expected page
5. the application accepts refreshed tokens or re-authenticates as expected
