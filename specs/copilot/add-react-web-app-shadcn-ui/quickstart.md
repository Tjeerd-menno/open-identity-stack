# Quickstart Guide: React Admin Web App

**Date**: 2026-01-18  
**Purpose**: Developer guide for setting up, building, and running the React admin web application.

---

## Prerequisites

Before you begin, ensure you have:

- **Node.js**: v22.x LTS or higher
- **npm**: v10.x or higher (comes with Node.js)
- **.NET SDK**: 10.0.100 or higher (for running the full stack)
- **Git**: For cloning and version control
- **IDE**: VS Code, WebStorm, or similar (recommended: VS Code with extensions)

### Recommended VS Code Extensions

```json
{
  "recommendations": [
    "dbaeumer.vscode-eslint",
    "esbenp.prettier-vscode",
    "bradlc.vscode-tailwindcss",
    "ms-dotnettools.csharp",
    "ms-dotnettools.csdevkit"
  ]
}
```

---

## Project Structure

```
src/OpenIdentityStack.AdminWeb/
├── public/                     # Static assets
├── src/
│   ├── features/               # Feature modules
│   │   ├── auth/               # Authentication
│   │   ├── users/              # User management
│   │   ├── roles/              # Role management
│   │   ├── groups/             # Group management
│   │   ├── service-accounts/   # Service account management
│   │   ├── sessions/           # Session management
│   │   └── providers/          # Provider management
│   ├── components/             # Shared UI components
│   │   ├── ui/                 # Shadcn UI components
│   │   ├── layout/             # Layout components
│   │   └── common/             # Common reusable components
│   ├── lib/                    # Utilities
│   │   ├── api/                # API client
│   │   ├── auth/               # Auth utilities
│   │   └── utils.ts            # Helper functions
│   ├── hooks/                  # Global custom hooks
│   ├── routes/                 # Route definitions
│   ├── types/                  # TypeScript types
│   ├── App.tsx                 # Root component
│   └── main.tsx                # Entry point
├── tests/                      # Test files
│   ├── unit/                   # Vitest unit tests
│   └── e2e/                    # Playwright E2E tests
├── .env.example                # Environment template
├── package.json                # Dependencies
├── tsconfig.json               # TypeScript config
├── vite.config.ts              # Vite config
└── tailwind.config.js          # Tailwind config
```

---

## Setup Instructions

### 1. Initial Setup

```bash
# Navigate to the project root
cd /path/to/open-identity-stack

# Navigate to the admin web app directory
cd src/OpenIdentityStack.AdminWeb

# Install dependencies
npm install
```

### 2. Environment Configuration

Create a `.env` file from the template:

```bash
cp .env.example .env
```

Edit `.env` with your configuration:

```env
# API Configuration
VITE_API_BASE_URL=http://localhost:5000

# OpenIddict OIDC Configuration
VITE_OIDC_AUTHORITY=https://localhost:5001
VITE_OIDC_CLIENT_ID=admin-web-client
VITE_OIDC_REDIRECT_URI=http://localhost:5173/auth/callback
VITE_OIDC_POST_LOGOUT_REDIRECT_URI=http://localhost:5173/
VITE_OIDC_SILENT_REDIRECT_URI=http://localhost:5173/auth/silent-callback
VITE_OIDC_SCOPE=openid profile email admin-api

# App Configuration
VITE_APP_NAME=OpenIdentityStack Admin
VITE_APP_VERSION=1.0.0
```

### 3. Shadcn UI Setup

The Shadcn UI components should already be initialized, but if you need to add more:

```bash
# Add individual components
npx shadcn@latest add button
npx shadcn@latest add table
npx shadcn@latest add dialog
npx shadcn@latest add form
npx shadcn@latest add input
npx shadcn@latest add select
npx shadcn@latest add toast
npx shadcn@latest add dropdown-menu
npx shadcn@latest add avatar
npx shadcn@latest add badge
npx shadcn@latest add card
npx shadcn@latest add tabs
```

---

## Development

### Running the Admin Web App (Standalone)

```bash
# Start the Vite dev server
npm run dev

# Opens at http://localhost:5173
```

**Note**: Running standalone requires the API to be running separately.

### Running with .NET Aspire (Recommended)

```bash
# Navigate to the AppHost project
cd /path/to/open-identity-stack/src/OpenIdentityStack.AppHost

# Run the entire stack (API + DB + AdminWeb)
dotnet run

# Or use the Aspire dashboard
# Opens at http://localhost:15888 (Aspire Dashboard)
# Admin Web: http://localhost:5173
# API: http://localhost:5000
```

**Advantages**:
- Automatic environment variable injection
- Service discovery
- Unified logging and monitoring
- PostgreSQL database automatically started

### Development Scripts

```json
{
  "scripts": {
    "dev": "vite",                        // Start dev server
    "build": "tsc && vite build",         // Build for production
    "preview": "vite preview",            // Preview production build
    "lint": "eslint src --ext ts,tsx",    // Lint code
    "lint:fix": "eslint src --ext ts,tsx --fix",  // Fix lint issues
    "format": "prettier --write \"src/**/*.{ts,tsx,css}\"",  // Format code
    "test": "vitest",                     // Run unit tests
    "test:ui": "vitest --ui",             // Run tests with UI
    "test:coverage": "vitest --coverage", // Generate coverage report
    "test:e2e": "playwright test",        // Run E2E tests
    "type-check": "tsc --noEmit"          // Type check without building
  }
}
```

---

## Building for Production

```bash
# Build optimized production bundle
npm run build

# Output directory: dist/
# Contains: index.html, assets/ (JS, CSS, images)
```

### Production Build Optimization

The Vite build process includes:
- **Code Splitting**: Route-based chunks
- **Minification**: Terser/esbuild minification
- **Tree Shaking**: Remove unused code
- **Asset Optimization**: Image compression, CSS purging
- **Source Maps**: Disabled for production

### Serving Production Build Locally

```bash
# Preview production build
npm run preview

# Serves at http://localhost:4173
```

---

## Testing

### Unit Tests (Vitest + React Testing Library)

```bash
# Run all unit tests
npm test

# Run tests in watch mode
npm test -- --watch

# Run tests with UI
npm run test:ui

# Generate coverage report
npm run test:coverage
```

**Example Test**:
```typescript
// src/features/users/components/__tests__/UserList.test.tsx
import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import UserList from '../UserList';

describe('UserList', () => {
  it('renders user list with data', () => {
    const users = [
      { id: '1', email: 'user@example.com', displayName: 'User', status: 'Active', createdAt: '2026-01-18' }
    ];
    
    render(<UserList users={users} />);
    
    expect(screen.getByText('user@example.com')).toBeInTheDocument();
  });
});
```

### E2E Tests (Playwright)

```bash
# Install Playwright browsers (first time only)
npx playwright install

# Run E2E tests
npm run test:e2e

# Run E2E tests in UI mode
npx playwright test --ui

# Generate E2E report
npx playwright show-report
```

**Example E2E Test**:
```typescript
// tests/e2e/auth.spec.ts
import { test, expect } from '@playwright/test';

test('admin can login', async ({ page }) => {
  await page.goto('http://localhost:5173');
  
  await page.click('text=Login');
  await page.fill('input[name="email"]', 'admin@example.com');
  await page.fill('input[name="password"]', 'Admin123!@456');
  await page.click('button[type="submit"]');
  
  await expect(page).toHaveURL(/.*dashboard/);
  await expect(page.locator('text=Welcome')).toBeVisible();
});
```

---

## Code Quality

### Linting

```bash
# Check for lint errors
npm run lint

# Fix auto-fixable lint errors
npm run lint:fix
```

**ESLint Configuration** (`.eslintrc.cjs`):
```javascript
module.exports = {
  extends: [
    'eslint:recommended',
    'plugin:@typescript-eslint/recommended',
    'plugin:react/recommended',
    'plugin:react-hooks/recommended',
  ],
  rules: {
    'react/react-in-jsx-scope': 'off',  // Not needed in React 18
    '@typescript-eslint/no-unused-vars': ['error', { argsIgnorePattern: '^_' }],
  },
};
```

### Formatting

```bash
# Format all files
npm run format

# Check formatting without writing
npx prettier --check "src/**/*.{ts,tsx,css}"
```

**Prettier Configuration** (`.prettierrc`):
```json
{
  "semi": true,
  "singleQuote": true,
  "trailingComma": "es5",
  "tabWidth": 2,
  "printWidth": 100
}
```

### Type Checking

```bash
# Run TypeScript type checker
npm run type-check
```

---

## Debugging

### VS Code Debug Configuration

Create `.vscode/launch.json`:

```json
{
  "version": "0.2.0",
  "configurations": [
    {
      "type": "chrome",
      "request": "launch",
      "name": "Launch Chrome against localhost",
      "url": "http://localhost:5173",
      "webRoot": "${workspaceFolder}/src/OpenIdentityStack.AdminWeb/src",
      "sourceMaps": true
    }
  ]
}
```

### Browser DevTools

- **React DevTools**: Install browser extension
- **TanStack Query DevTools**: Automatically included in dev mode
- **Redux DevTools**: If using Redux (not recommended with TanStack Query)

---

## Common Tasks

### Adding a New Feature Module

1. Create feature directory:
```bash
mkdir -p src/features/my-feature
cd src/features/my-feature
mkdir components hooks api
```

2. Create component:
```typescript
// src/features/my-feature/components/MyFeature.tsx
export default function MyFeature() {
  return <div>My Feature</div>;
}
```

3. Create API hook:
```typescript
// src/features/my-feature/hooks/useMyFeature.ts
import { useQuery } from '@tanstack/react-query';
import { api } from '@/lib/api';

export function useMyFeature() {
  return useQuery({
    queryKey: ['myFeature'],
    queryFn: () => api.get('/my-feature'),
  });
}
```

4. Add route:
```typescript
// src/routes/index.tsx
import MyFeature from '@/features/my-feature/components/MyFeature';

export const routes = [
  { path: '/my-feature', element: <MyFeature /> },
];
```

### Adding a Shadcn UI Component

```bash
# List available components
npx shadcn@latest add

# Add specific component
npx shadcn@latest add accordion

# Component added to: src/components/ui/accordion.tsx
```

### Environment-Specific Configuration

```bash
# Development (default)
npm run dev

# Production build
npm run build
NODE_ENV=production npm run preview

# Staging (custom .env.staging)
npm run build -- --mode staging
```

---

## Troubleshooting

### Issue: CORS Errors

**Solution**: Ensure API CORS policy allows the admin web app origin:

```csharp
// OpenIdentityStack.Api/Program.cs
builder.Services.AddCors(options =>
{
    options.AddPolicy("AdminWeb", policy =>
    {
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

app.UseCors("AdminWeb");
```

### Issue: Authentication Fails

**Solution**: Check OpenIddict client registration:

1. Verify `VITE_OIDC_CLIENT_ID` matches registered client
2. Ensure redirect URIs are registered in OpenIddict
3. Check PKCE is enabled for the client

### Issue: Vite Dev Server Won't Start

**Solution**:
```bash
# Clear node_modules and reinstall
rm -rf node_modules package-lock.json
npm install

# Check port 5173 is not in use
lsof -i :5173
kill -9 <PID>
```

### Issue: TypeScript Errors

**Solution**:
```bash
# Regenerate TypeScript cache
rm -rf node_modules/.vite
npm run type-check
```

---

## Deployment

### Production Deployment (Static Hosting)

```bash
# Build for production
npm run build

# Deploy dist/ folder to:
# - Nginx/Apache
# - AWS S3 + CloudFront
# - Vercel/Netlify
# - Azure Static Web Apps
```

### Nginx Configuration

```nginx
server {
    listen 80;
    server_name admin.example.com;
    root /var/www/admin-web/dist;
    index index.html;

    location / {
        try_files $uri $uri/ /index.html;
    }

    # API proxy (optional)
    location /api/ {
        proxy_pass http://localhost:5000/api/;
        proxy_set_header Host $host;
    }
}
```

### Docker Deployment

```dockerfile
# Dockerfile
FROM node:22-alpine AS build

WORKDIR /app
COPY package*.json ./
RUN npm ci

COPY . .
RUN npm run build

FROM nginx:alpine
COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
```

---

## Performance Optimization

### Code Splitting

```typescript
// Lazy load routes
import { lazy } from 'react';

const Users = lazy(() => import('@/features/users/components/UserList'));
const Roles = lazy(() => import('@/features/roles/components/RoleList'));
```

### Bundle Analysis

```bash
# Analyze bundle size
npm run build -- --mode analyze

# View bundle report in browser
```

### Caching Strategy

```typescript
// TanStack Query configuration
const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 5 * 60 * 1000,  // 5 minutes
      gcTime: 10 * 60 * 1000,    // 10 minutes
    },
  },
});
```

---

## Resources

- **Vite Documentation**: https://vitejs.dev/
- **React Documentation**: https://react.dev/
- **Shadcn UI**: https://ui.shadcn.com/
- **TanStack Query**: https://tanstack.com/query/latest
- **TypeScript**: https://www.typescriptlang.org/
- **Tailwind CSS**: https://tailwindcss.com/
- **Vitest**: https://vitest.dev/
- **Playwright**: https://playwright.dev/

---

## Getting Help

- **Repository Issues**: https://github.com/your-org/open-identity-stack/issues
- **Team Slack**: #admin-web-dev
- **Documentation**: `/docs/admin-web/`

---

**Last Updated**: 2026-01-18  
**Version**: 1.0.0
