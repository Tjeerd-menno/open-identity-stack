# OpenIdentityStack Production Configuration

OpenIdentityStack intentionally refuses to start in non-development environments without production key material.

Required settings:

```json
{
  "OpenIddict": {
    "Certificates": {
      "Signing": {
        "Path": "/run/secrets/openid-signing.pfx",
        "Password": "<secret>"
      },
      "Encryption": {
        "Path": "/run/secrets/openid-encryption.pfx",
        "Password": "<secret>"
      }
    }
  },
  "Secrets": {
    "EncryptionKey": "<base64-encoded-32-byte-key>"
  },
  "AllowedCorsOrigins": "https://admin.example.com",
  "ForwardedHeaders": {
    "Enabled": true
  }
}
```

`OpenIddict:Certificates:*:Base64` can be used instead of `Path` when the deployment platform supplies certificates as environment variables.

The Aspire AppHost now composes only PostgreSQL, the OpenIdentityStack migrator, the OpenIdentityStack API, and the Management Web app. Demo isotope resources and demo OAuth clients are not part of the production composition.

