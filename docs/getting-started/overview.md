# Getting started

OpenIdentityStack is built for teams that want to run their own identity service while still keeping familiar OAuth 2.0 and OpenID Connect workflows.

## What you get

- an OpenIddict-based authorization server
- authorization code with PKCE, client credentials, and refresh token support
- local accounts plus external provider federation
- an admin web UI for day-to-day operations
- roles, groups, permissions, delegated administration, and service accounts
- database-backed data protection and controlled migrations

## Good fit scenarios

OpenIdentityStack is a good fit when you need:

- one identity authority for multiple internal or customer-facing applications
- first-party control over clients, redirects, scopes, and sessions
- administrative workflows for users, roles, groups, and service accounts
- production deployment in your own Kubernetes, container, or Windows-hosted environment

## When to pause and evaluate carefully

OpenIdentityStack may be a poor fit if you want:

- a fully managed SaaS identity tenant with no operational ownership
- product-specific authorization logic to live entirely inside the identity server
- an API gateway, service mesh, or edge platform

## Start with the right path

- For a local walkthrough, use [Quick start](quick-start.md).
- For a decision-based onboarding flow, use [Guided setup](guided-setup.md).
- For a production-first rollout, continue to [Production deployment](../installation/production.md).
- For application onboarding, continue to [Integrations](../integrations/index.md).
