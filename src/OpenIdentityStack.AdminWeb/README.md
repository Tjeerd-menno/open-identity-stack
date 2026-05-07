# OpenIdentityStack Admin Web Application

A modern React-based administrative interface for managing the OpenIdentityStack IAM system. Built with React 19, TypeScript, Vite, and Shadcn UI.

## Features

### Core Admin Modules
- **Dashboard**: Overview of system statistics and quick actions
- **User Management**: Create, edit, delete users; assign roles and groups; manage upstream identities
- **Role Management**: Define roles with granular permissions across all resources
- **Group Management**: Organize users into groups with role and claim mappings
- **Service Account Management**: Manage OAuth2 service accounts with credentials and certificates
- **Session Management**: Monitor and revoke active user sessions
- **Provider Management**: Configure external identity providers (OIDC, OAuth2, SAML2)

### Security
- **OAuth2/OIDC Authentication**: Secure authorization code flow with PKCE
- **Permission-Based UI**: UI elements adapt based on user permissions
- **Secure Credential Handling**: Secrets shown only once, never re-displayed
- **Token Management**: Automatic token refresh and 401 handling

## Quick Start

```bash
# Install dependencies
npm install

# Start development server
npm run dev

# Build for production
npm run build
```

## Configuration

Create `.env` file:

```env
VITE_API_URL=http://localhost:5000
VITE_API_BASE_PATH=/api/admin
VITE_OIDC_AUTHORITY=http://localhost:5000
VITE_OIDC_CLIENT_ID=admin-web
VITE_OIDC_REDIRECT_URI=http://localhost:5173/auth/callback
```

See `.env.example` for all configuration options.

## Development

```bash
npm run dev          # Start dev server
npm run build        # Build for production
npm run preview      # Preview production build
npm run lint         # Run linter
npm run test         # Run tests
```

## Documentation

For complete documentation, see `/specs/copilot/add-react-web-app-shadcn-ui/` in the repository root.

## License

See main OpenIdentityStack repository for license information.
