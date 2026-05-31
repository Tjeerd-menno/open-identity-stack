# Dual admin UI strategy

OpenIdentityStack will run and actively develop two management frontends in parallel: `OpenIdentityStack.AdminWeb` and `OpenIdentityStack.ManagementWeb` (Mantine-first). We chose this to ship a higher-quality Management Web quickly without blocking ongoing AdminWeb delivery, while accepting that parity is best-effort rather than a strict release gate.
