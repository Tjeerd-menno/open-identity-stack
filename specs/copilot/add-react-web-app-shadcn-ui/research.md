# Research: React Admin Web App with Shadcn UI

**Date**: 2026-01-18  
**Purpose**: Resolve technical unknowns and establish best practices for implementing the React-based admin web application.

---

## 1. React + Vite + TypeScript Setup

### Decision: Use the implemented Vite 8+ with React 19+ TypeScript application

**Rationale**: 
- Vite provides lightning-fast HMR (Hot Module Replacement)
- Built-in TypeScript support with minimal configuration
- Superior build performance compared to Webpack
- Excellent development experience with instant server start
- Native ES modules support

**Setup Approach**: The application now lives in `src/OpenIdentityStack.AdminWeb` and uses the checked-in Vite configuration, package manifest, runtime config script, and Aspire integration. Do not recreate the project from a template.

**Key Configuration**:

**vite.config.ts**:
```typescript
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: 'http://localhost:5000',  // API server
        changeOrigin: true
      }
    }
  },
  build: {
    rollupOptions: {
      output: {
        manualChunks: {
          'vendor': ['react', 'react-dom'],
          'router': ['react-router-dom'],
          'query': ['@tanstack/react-query']
        }
      }
    },
    minify: 'esbuild',
    sourcemap: false,  // Production
    target: 'es2020'
  }
})
```

**Environment Variables**:
- Prefix with `VITE_` for client exposure
- Use `import.meta.env.VITE_*` to access
- Create `.env`, `.env.development`, `.env.production`

**Code Splitting**:
- Route-based lazy loading with `React.lazy()` + `Suspense`
- Dynamic imports for heavy components
- Vite's automatic code splitting for optimal bundles

**Alternatives Considered**:
- ❌ **Create React App**: Deprecated, slow builds
- ❌ **Next.js**: Overkill for admin SPA, server-side rendering not needed
- ❌ **Webpack**: Complex configuration, slower dev experience

---

## 2. Shadcn UI Integration

### Decision: Use Shadcn UI with Tailwind CSS

**Rationale**:
- Built on Radix UI primitives (enterprise-grade accessibility)
- Components are copied into your codebase (full control, no version conflicts)
- Tailwind CSS provides utility-first styling
- Excellent TypeScript support
- Dark mode support built-in
- Perfect for admin dashboards (tables, forms, dialogs)

**Setup Steps**:

1. **Install Dependencies**:
```bash
npm install -D tailwindcss postcss autoprefixer
npx tailwindcss init -p
npm install class-variance-authority clsx tailwind-merge
npm install @radix-ui/react-icons
```

2. **Initialize Shadcn UI**:
```bash
npx shadcn@latest init
```
This creates `components.json` configuration file.

3. **Tailwind Configuration** (`tailwind.config.js`):
```javascript
module.exports = {
  darkMode: ["class"],
  content: [
    "./index.html",
    "./src/**/*.{js,ts,jsx,tsx}",
  ],
  theme: {
    extend: {
      colors: {
        border: "hsl(var(--border))",
        // ... Shadcn's design tokens
      },
    },
  },
  plugins: [require("tailwindcss-animate")],
}
```

4. **Add Components as Needed**:
```bash
npx shadcn@latest add button
npx shadcn@latest add table
npx shadcn@latest add dialog
npx shadcn@latest add form
npx shadcn@latest add input
# etc.
```

**Accessibility Features**:
- Radix UI primitives meet WCAG 2.1 AA standards
- Keyboard navigation built-in
- Screen reader support
- Focus management

**Common Admin Dashboard Components Needed**:
- Table (with sorting, filtering, pagination)
- Form (with validation)
- Dialog (for confirmations, create/edit modals)
- Dropdown Menu (for actions)
- Toast (for notifications)
- Command (for search/command palette)
- Avatar, Badge, Card, Tabs

**Alternatives Considered**:
- ❌ **Material UI**: Heavy bundle size, opinionated styling
- ❌ **Ant Design**: Good for admin apps but harder to customize
- ❌ **Chakra UI**: Good but Shadcn offers better customization
- ❌ **Headless UI**: Requires more manual styling work

---

## 3. OAuth2/OIDC Authentication for SPAs

### Decision: Use `oidc-client-ts` with React Context Wrapper

**Rationale**:
- Industry-standard OAuth2/OIDC client library
- Full PKCE support (required for SPAs)
- Automatic token refresh handling
- Works seamlessly with OpenIddict
- TypeScript-first design
- Well-maintained and battle-tested

**Implementation Strategy**:

**Library**: `oidc-client-ts` (with custom React hooks wrapper)

**Token Storage** (Security-Critical):
- ✅ **Access Token**: In-memory only (React state/context)
- **OIDC state**: session storage via `oidc-client-ts`; access tokens are exposed through the auth context and API interceptor
- ❌ **NOT in localStorage**: Vulnerable to XSS attacks (per NFR2.1)

**Flow**:
1. User clicks "Login" → redirects to OpenIddict `/authorize` endpoint
2. PKCE parameters generated (code_verifier, code_challenge)
3. User authenticates → authorization code returned
4. Frontend exchanges code for tokens at `/token` endpoint
5. **Access token**: Stored in memory (React context)
6. **OIDC state**: Client stores OIDC state in session storage and passes access tokens through the API interceptor
7. Access token expires (15-60 min) → silent refresh using cookie

**Configuration**:
```typescript
const oidcConfig: UserManagerSettings = {
  authority: import.meta.env.VITE_OIDC_AUTHORITY,  // OpenIddict server
  client_id: import.meta.env.VITE_OIDC_CLIENT_ID,  // "admin-web-client"
  redirect_uri: `${window.location.origin}/auth/callback`,
  post_logout_redirect_uri: `${window.location.origin}/`,
  response_type: 'code',  // Authorization Code Flow
  scope: 'openid profile email admin-api',  // Required scopes
  automaticSilentRenew: true,  // Auto-refresh before expiry
  silent_redirect_uri: `${window.location.origin}/auth/silent-callback`,
  
  // PKCE enabled by default in oidc-client-ts
  // code_challenge_method: 'S256'
}
```

**OpenIddict Server Configuration** (API side - for reference):
```csharp
// Client registration required in OpenIddict
.AddClient("admin-web-client", client =>
{
    client
        .AllowAuthorizationCodeFlow()
        .RequireProofKeyForCodeExchange()
        .SetRedirectUris([
            "http://localhost:5173/auth/callback",
            "http://localhost:5173/auth/silent-callback"
        ])
        .SetPostLogoutRedirectUris(["http://localhost:5173/"])
        .SetScopes(["openid", "profile", "email", "admin-api"])
        .SetClientType(ClientTypes.Public);  // No client secret for SPAs
});
```

**Security Best Practices**:
- ✅ Always use PKCE (Authorization Code Flow + PKCE)
- ✅ Short-lived access tokens (15-60 min)
- ✅ Refresh token rotation (OpenIddict supports this)
- ✅ SameSite=Strict for refresh token cookies
- ✅ HTTPS in production (mandatory)
- ✅ CORS whitelist (only allow admin web app origin)
- ✅ Token validation on every API call (backend)

**Protected Routes**:
```typescript
// Example: ProtectedRoute component
const ProtectedRoute = ({ children }: { children: ReactNode }) => {
  const { isAuthenticated, isLoading } = useAuth();
  
  if (isLoading) return <LoadingSpinner />;
  if (!isAuthenticated) return <Navigate to="/login" />;
  
  return <>{children}</>;
};
```

**Alternatives Considered**:
- ❌ **@auth/core (Auth.js)**: Less OIDC-specific, more abstraction
- ❌ **react-oidc-context**: Wrapper around oidc-client-ts, adds React hooks (viable alternative)
- ❌ **Implicit Flow**: Deprecated, insecure for SPAs

---

## 4. TanStack Query (React Query) v5

### Decision: Use TanStack Query v5 for API State Management

**Rationale**:
- Best-in-class data synchronization for React
- Automatic caching, background refetching, and request deduplication
- Optimistic updates for instant UX
- Built-in error handling and retry logic
- Perfect for admin CRUD operations
- Excellent TypeScript support

**Setup**:
```bash
npm install @tanstack/react-query @tanstack/react-query-devtools
```

**Configuration**:
```typescript
const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 5 * 60 * 1000,  // 5 minutes
      gcTime: 10 * 60 * 1000,    // 10 minutes (formerly cacheTime)
      retry: (failureCount, error) => {
        // Don't retry on 401/403 (auth errors)
        if (error.status === 401 || error.status === 403) return false;
        return failureCount < 3;
      },
      refetchOnWindowFocus: false,  // Reduce noise in admin apps
    },
    mutations: {
      retry: false,  // Don't auto-retry mutations
    },
  },
});
```

**Patterns for CRUD Operations**:

**1. Query (Read)**:
```typescript
// List users
const useUsers = (page: number) => {
  return useQuery({
    queryKey: ['users', page],
    queryFn: () => api.getUsers({ page, pageSize: 20 }),
  });
};

// Get single user
const useUser = (userId: string) => {
  return useQuery({
    queryKey: ['user', userId],
    queryFn: () => api.getUser(userId),
    enabled: !!userId,  // Only fetch if userId exists
  });
};
```

**2. Mutation (Create/Update/Delete)**:
```typescript
const useCreateUser = () => {
  const queryClient = useQueryClient();
  
  return useMutation({
    mutationFn: (data: CreateUserRequest) => api.createUser(data),
    onSuccess: (newUser) => {
      // Invalidate and refetch users list
      queryClient.invalidateQueries({ queryKey: ['users'] });
      // Optionally add to cache directly
      queryClient.setQueryData(['user', newUser.id], newUser);
    },
  });
};
```

**3. Optimistic Updates**:
```typescript
const useUpdateUser = () => {
  const queryClient = useQueryClient();
  
  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: UpdateUserRequest }) => 
      api.updateUser(id, data),
    
    // Optimistic update
    onMutate: async ({ id, data }) => {
      await queryClient.cancelQueries({ queryKey: ['user', id] });
      const previousUser = queryClient.getQueryData(['user', id]);
      
      // Set optimistic data
      queryClient.setQueryData(['user', id], (old) => ({ ...old, ...data }));
      
      return { previousUser };  // Rollback context
    },
    
    // Rollback on error
    onError: (err, variables, context) => {
      queryClient.setQueryData(['user', variables.id], context?.previousUser);
    },
    
    // Always refetch after success/error
    onSettled: (data, error, variables) => {
      queryClient.invalidateQueries({ queryKey: ['user', variables.id] });
    },
  });
};
```

**4. Pagination Strategy**:
```typescript
// Server-side pagination
const [page, setPage] = useState(1);

const { data: usersPage, isLoading } = useQuery({
  queryKey: ['users', page],
  queryFn: () => api.getUsers({ page, pageSize: 20 }),
  keepPreviousData: true,  // Keep old data while fetching new page
});

// Infinite scroll (alternative)
const { data, fetchNextPage, hasNextPage } = useInfiniteQuery({
  queryKey: ['users'],
  queryFn: ({ pageParam = 1 }) => api.getUsers({ page: pageParam }),
  getNextPageParam: (lastPage) => lastPage.nextPage ?? undefined,
});
```

**5. Error Handling**:
```typescript
const { data, error, isError } = useQuery({
  queryKey: ['users'],
  queryFn: api.getUsers,
});

if (isError) {
  // Display user-friendly error
  toast.error(`Failed to load users: ${error.message}`);
}
```

**6. Integration with Auth**:
```typescript
// Axios interceptor to inject access token
axios.interceptors.request.use((config) => {
  const token = authContext.getAccessToken();
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

// Handle 401 globally
axios.interceptors.response.use(
  (response) => response,
  async (error) => {
    if (error.response?.status === 401) {
      await authContext.logout();
      window.location.href = '/login';
    }
    return Promise.reject(error);
  }
);
```

**Alternatives Considered**:
- ❌ **Redux Toolkit Query**: More boilerplate, tightly coupled to Redux
- ❌ **SWR**: Good but less feature-rich than TanStack Query
- ❌ **Custom fetch hooks**: Reinventing the wheel, error-prone

---

## 5. .NET Aspire Integration

### Decision: Use `AddNpmApp()` Resource Type

**Rationale**:
- .NET Aspire 13.3.x has native support for Node.js applications
- Seamless integration with existing AppHost orchestration
- Automatic environment variable passing
- Service discovery for API URL resolution
- Unified dashboard for all services

**AppHost.cs Modification**:
```csharp
var builder = DistributedApplication.CreateBuilder(args);

// PostgreSQL (existing)
var postgres = builder.AddPostgres("postgres");
if (!string.Equals(Environment.GetEnvironmentVariable("OPENIDENTITYSTACK_DISABLE_DATA_VOLUME"), 
    "true", StringComparison.OrdinalIgnoreCase))
{
    postgres = postgres.WithDataVolume();
}

var db = postgres.AddDatabase("openidentitystack");

// .NET API (existing)
var api = builder.AddProject<Projects.OpenIdentityStack_Api>("api")
    .WithReference(db)
    .WaitFor(db)
    .WithHttpEndpoint(port: 5000, name: "http");

// React Admin Web App (NEW)
var adminWeb = builder.AddNpmApp("admin-web", "../OpenIdentityStack.AdminWeb")
    .WithReference(api)  // Service discovery
    .WithNpmCommand("dev")  // Use "build" + "preview" for production
    .WithHttpEndpoint(port: 5173, env: "VITE_PORT")
    .WithEnvironment("VITE_API_BASE_URL", () => 
        api.GetEndpoint("http").Url ?? "http://localhost:5000")
    .WithEnvironment("VITE_OIDC_AUTHORITY", "https://localhost:5001")
    .WithEnvironment("VITE_OIDC_CLIENT_ID", "admin-web-client")
    .WithEnvironment("VITE_OIDC_REDIRECT_URI", "http://localhost:5173/auth/callback")
    .PublishAsDockerFile();  // Optional: containerize for production

await builder.Build().RunAsync();
```

**Environment Variable Passing**:
- Use `.WithEnvironment(key, value)` for static values
- Use `.WithEnvironment(key, () => dynamicValue)` for runtime resolution
- Vite accesses via `import.meta.env.VITE_*`

**Development Mode**:
- `.WithNpmCommand("dev")` → runs `npm run dev`
- Vite dev server with HMR on port 5173
- Hot reload on code changes

**Production Mode**:
```csharp
if (builder.Environment.IsProduction())
{
    adminWeb
        .WithNpmCommand("build")   // Build static assets
        .WithNpmCommand("preview")  // Serve built app
        .WithEnvironment("NODE_ENV", "production");
}
```

**CORS Configuration** (API side):
```csharp
// Program.cs in OpenIdentityStack.Api
builder.Services.AddCors(options =>
{
    options.AddPolicy("AdminWeb", policy =>
    {
        policy
            .WithOrigins("http://localhost:5173", "https://localhost:5174")
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();  // Required for cookies
    });
});

app.UseCors("AdminWeb");
```

**Health Checks**:
```csharp
adminWeb.WithHealthCheck();  // Aspire pings /health endpoint
```

In Vite app, implement health endpoint:
```typescript
// vite.config.ts
export default defineConfig({
  plugins: [
    react(),
    {
      name: 'health-check',
      configureServer(server) {
        server.middlewares.use((req, res, next) => {
          if (req.url === '/health') {
            res.statusCode = 200;
            res.end('healthy');
          } else {
            next();
          }
        });
      },
    },
  ],
});
```

**Aspire Dashboard Benefits**:
- View all services (API, DB, AdminWeb) in one place
- Monitor logs from admin web app
- Inspect environment variables
- Check health status

**Alternatives Considered**:
- ❌ **Standalone Node.js server**: No integration with Aspire orchestration
- ❌ **Manual Docker Compose**: More configuration, less DX
- ❌ **Separate hosting**: Misses Aspire's unified development experience

---

## Summary of Key Decisions

| Component | Technology | Rationale |
|-----------|-----------|-----------|
| **Build Tool** | Vite 8+ | Fast HMR, optimal DX, modern ES modules |
| **UI Library** | Shadcn UI + Tailwind CSS | Accessible, customizable, admin-ready components |
| **Authentication** | oidc-client-ts | PKCE support, OpenIddict compatibility, secure |
| **API State** | TanStack Query v5 | Best-in-class data sync, caching, optimistic updates |
| **Orchestration** | .NET Aspire AddNpmApp() | Native integration, unified dashboard, service discovery |

---

## Next Steps (Phase 1)

1. ✅ **Research Complete** → All technical unknowns resolved
2. 🔄 **Data Model**: Define TypeScript types for API responses (users, roles, etc.)
3. 🔄 **API Contracts**: Document existing admin API endpoints (OpenAPI specs)
4. 🔄 **Quickstart Guide**: Developer setup instructions for the React app

---

**Document Status**: ✅ Complete  
**Phase 0 Output**: All NEEDS CLARIFICATION items resolved
