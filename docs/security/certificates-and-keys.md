# Certificates and keys

Production token issuance depends on stable signing and encryption material.

## What the platform needs

- a signing certificate and private key
- an encryption certificate and private key
- a rotation process that keeps clients and the authority in sync

## Current deployment pattern

In Kubernetes, mount the signing and encryption certificates and their private keys from Secrets into both the API and DbMigrator. Operators can use cert-manager to issue the certificates. Configure each certificate path and its matching private key path.

## Rotation guidance

The candidate loads one signing certificate and one encryption certificate at process startup. Plan a coordinated maintenance window. Replicas holding different keys during a rolling deployment can disagree on which credentials they can process.

1. Record the issuer, discovery JWKS URI and current signing key ID. Use an isolated relying party to obtain an ID token and a separately held, unused refresh token. Confirm a fresh code exchange and refresh work before the change.
2. Drain traffic to every API replica. Replace the signing certificate and its matching private key, keeping the encryption key stable. Restart all replicas. Fetch discovery and JWKS again. Check that the new ID token signature verifies with the advertised key and that its issuer, audience and expiry match. A relying party fetching fresh JWKS loses the old verification key. Consumers with a cached old key may continue to accept an unexpired old signature. Coordinate cache refresh with relying parties.
3. Repeat with a new unused refresh token, replacing only the encryption certificate and private key. An encrypted credential issued under the retired key requires the old private key for decryption. Plan for affected applications to obtain new credentials and for users to sign in again. Confirm the new access token is accepted by its API, and test both a fresh and a previously issued refresh credential.
4. Reopen traffic after the issuer, relying parties and protected APIs agree on the active configuration. Retain previous key material in controlled recovery storage according to your incident and retention policy. Switching back can invalidate credentials issued since the change.

The RC1 Production rehearsal exercised both replacements independently. After each restart, the Admin API answered HTTP 401 for a previously accepted access token and the token endpoint returned `invalid_grant` for a previously unused refresh token. Fresh PKCE, ID-token signature validation, an authorized API read, and refresh succeeded after each change. Schedule reauthentication for clients holding old credentials. [Rotation rehearsal](https://github.com/Tjeerd-menno/open-identity-stack/actions/runs/44597379060/job/135819216448)
