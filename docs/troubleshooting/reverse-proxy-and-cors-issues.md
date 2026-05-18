# Reverse proxy and CORS issues

## Symptoms

- local runs work but production browser calls fail
- redirects use the wrong scheme or host
- the admin web cannot call the API

## Likely causes

- forwarded headers are disabled or incompletely trusted
- `AllowedCorsOrigins` is missing the real browser origin
- ingress is rewriting scheme or host unexpectedly

## Checks

1. confirm `ForwardedHeaders__Enabled`
2. review trusted proxies or networks
3. confirm `AllowedCorsOrigins` contains the exact admin web origin
4. verify the public authority URL and host headers seen by the API

## Fixes

- enable and scope forwarded headers correctly
- correct the browser origin list
- repair ingress host, scheme, or header forwarding

## When to escalate

Escalate when browser and proxy traces disagree with the API logs after forwarding and CORS settings are corrected.
