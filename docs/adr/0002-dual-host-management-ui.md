# Management Web frontend topology

OpenIdentityStack exposes Management Web as the only browser management UI. It keeps its dedicated OIDC client registration and endpoint configuration so the frontend remains independently deployable from the API without preserving a second operator UI host.
