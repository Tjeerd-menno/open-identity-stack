# Management Web Uses Opaque Access Tokens For Permissions

Management Web treats access tokens as opaque credentials and obtains its current-user permission snapshot from `GET /api/me`. This keeps Production access-token encryption enabled while avoiding frontend coupling to signed JWT payloads; the rejected alternatives were disabling encryption, moving permissions into ID tokens, or calling token introspection directly from the public SPA.
