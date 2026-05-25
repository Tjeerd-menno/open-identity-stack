import { createContext, type ReactNode } from 'react';

export interface AuthenticatedUser {
  sub: string;
  email: string;
  name: string;
  permissions: string[];
}

export interface AuthState {
  isAuthenticated: boolean;
  isLoading: boolean;
  isLoggingOut: boolean;
  user: AuthenticatedUser | null;
  accessToken: string | null;
  error: string | null;
}

export interface AuthContextValue extends AuthState {
  login: () => Promise<void>;
  logout: () => Promise<void>;
  signinCallback: () => Promise<void>;
  signinSilentCallback: () => Promise<void>;
  getAccessToken: () => Promise<string | null>;
}

export interface AuthProviderProps {
  children: ReactNode;
}

export const AuthContext = createContext<AuthContextValue | undefined>(undefined);
