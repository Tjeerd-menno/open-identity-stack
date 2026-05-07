/**
 * T054: Create Login page component
 * 
 * Login page that initiates OAuth2/OIDC authentication flow.
 */

import React, { useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../hooks/useAuth';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { LoadingSpinner } from '@/components/common/LoadingSpinner';

/**
 * Login page component
 * 
 * Displays a login button that initiates the OAuth2/OIDC flow.
 * Redirects to OpenIddict authorization endpoint with PKCE.
 * 
 * Features:
 * - OAuth2/OIDC login flow
 * - PKCE for secure authentication
 * - Error handling and display
 * - Loading state during redirect
 */
export const Login: React.FC = () => {
  const { login, isLoading, error, isAuthenticated } = useAuth();
  const navigate = useNavigate();

  useEffect(() => {
    if (isAuthenticated) {
      navigate('/', { replace: true });
      return;
    }

    if (!isLoading) {
      void login();
    }
  }, [isAuthenticated, isLoading, login, navigate]);

  return (
    <div className="flex min-h-screen items-center justify-center bg-gray-50">
      <Card className="w-[400px]">
        <CardHeader className="space-y-1">
          <div className="flex items-center justify-center mb-4">
            <div className="h-12 w-12 rounded-lg bg-primary flex items-center justify-center">
              <span className="text-2xl text-primary-foreground font-bold">OM</span>
            </div>
          </div>
          <CardTitle className="text-2xl text-center">OpenIdentityStack Admin</CardTitle>
          <CardDescription className="text-center">
            Redirecting to sign-in...
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          {error && (
            <div className="p-3 bg-red-50 border border-red-200 rounded-md">
              <p className="text-sm text-red-800">{error}</p>
            </div>
          )}

          <Button
            disabled
            className="w-full"
            size="lg"
          >
            <>
              <LoadingSpinner size="sm" className="mr-2" />
              Redirecting...
            </>
          </Button>

          <div className="text-center text-sm text-gray-500">
            <p>Secure OAuth2/OIDC authentication</p>
          </div>
        </CardContent>
      </Card>
    </div>
  );
};
