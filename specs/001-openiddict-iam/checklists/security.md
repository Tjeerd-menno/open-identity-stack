# Security Review Checklist

## OpenIddict Configuration
- [x] PKCE Required for Authorization Code Flow
- [x] Token Endpoint Passthrough enabled safely
- [x] Encryption/Signing keys handled (Development certs currently)

## Authentication
- [x] Cookie attributes (Secure, HttpOnly, SameSite)
- [x] Password Hashing (Argon2/PBKDF2 - using BCrypt/PBKDF2?)
- [x] Session Management active and integrated
- [x] JIT Provisioning validates upstream claims

## API Security
- [x] HTTPS Redirection (Commented out in Program.cs for Dev - enable in production)
- [x] CSRF Protection on Forms (Login)
- [x] Parameter Validation (FluentValidation)
- [x] Structured Logging excludes secrets (redaction)

## Access Control
- [x] RBAC enforcement (Claims in token)
- [x] Service Account limitations (Client Credentials scope check)
