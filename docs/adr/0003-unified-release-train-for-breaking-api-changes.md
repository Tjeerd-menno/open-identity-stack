# Unified release train for breaking admin API changes

OpenIdentityStack allows non-versioned breaking changes on the Admin API, but only when API, shared frontend client, AdminWeb, and Management Web are released in a coordinated release train. We chose this to preserve delivery speed without API version proliferation, while explicitly accepting tighter cross-component release coupling.
