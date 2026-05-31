# Dual-host management UI topology

OpenIdentityStack will expose AdminWeb and Management Web on separate hostnames per environment, with separate OIDC client registrations, while both UIs remain active during transition. We chose this to isolate routing and authentication concerns per UI, reduce coupling between releases, and allow independent deploy/rollback behavior.
