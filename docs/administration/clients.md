# Client applications

Client records represent the applications that trust OpenIdentityStack for sign-in or token issuance.

## Typical tasks

- register a new web or API client
- configure redirect URIs
- configure logout redirect URIs
- rotate shared secrets for confidential clients
- remove unused clients

## Things to verify every time

- the redirect URIs match the real application URLs exactly
- the grant type matches the application profile
- production and non-production clients are kept separate
- secret rotation is scheduled and documented

## Admin web bootstrap

The current Kubernetes migrator example seeds redirect URIs for the admin web client. Use the same discipline for your own application clients so deployments and client records stay in sync.
