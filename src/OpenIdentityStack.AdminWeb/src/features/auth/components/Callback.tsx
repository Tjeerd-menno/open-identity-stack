/**
 * T055: Create Callback page component
 * 
 * Callback page that handles the OAuth2/OIDC redirect after authentication.
 * Exchanges authorization code for tokens.
 */

import React, { useEffect, useState, useRef } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../hooks/useAuth';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { LoadingSpinner } from '@/components/common/LoadingSpinner';

/**
 * Callback page component
 * 
 * Handles the OAuth2/OIDC callback after user authentication.
 * 
 * Flow:
 * 1. Receives authorization code from OpenIddict
 * 2. Exchanges code for access and refresh tokens
 * 3. Stores tokens securely
 * 4. Redirects to the originally requested page or home
 * 
 * Security:
 * - PKCE code verification
 * - State parameter validation
 * - Token exchange via secure backend endpoint
 */
export const Callback: React.FC = () => {
  const { signinCallback } = useAuth();
  const navigate = useNavigate();
  const [error, setError] = useState<string | null>(null);
  
  // Guard against double execution in React 18 Strict Mode
  // Authorization codes can only be redeemed once, so we must prevent duplicate calls
  const callbackProcessed = useRef(false);

  useEffect(() => {
    const handleCallback = async () => {
      // Prevent double execution in React Strict Mode
      if (callbackProcessed.current) {
        return;
      }
      callbackProcessed.current = true;
      
      try {
        // Process the callback and exchange code for tokens
        await signinCallback();
        
        // Redirect to home page or the page user was trying to access
        // The OIDC library stores the original URL in state
        navigate('/', { replace: true });
      } catch (err) {
        console.error('Callback error:', err);
        setError(
          err instanceof Error 
            ? err.message 
            : 'Failed to complete sign-in. Please try again.'
        );
      }
    };

    handleCallback();
  }, [signinCallback, navigate]);

  if (error) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-gray-50">
        <Card className="w-[400px]">
          <CardHeader>
            <CardTitle className="text-red-600">Sign-In Failed</CardTitle>
            <CardDescription>
              There was a problem completing your sign-in.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="p-3 bg-red-50 border border-red-200 rounded-md">
              <p className="text-sm text-red-800">{error}</p>
            </div>
            <button
              onClick={() => navigate('/login')}
              className="w-full px-4 py-2 bg-primary text-primary-foreground rounded-md hover:bg-primary/90"
            >
              Back to Sign In
            </button>
          </CardContent>
        </Card>
      </div>
    );
  }

  return (
    <div className="flex min-h-screen items-center justify-center bg-gray-50">
      <Card className="w-[400px]">
        <CardHeader>
          <CardTitle>Completing Sign-In</CardTitle>
          <CardDescription>
            Please wait while we complete your authentication...
          </CardDescription>
        </CardHeader>
        <CardContent className="flex justify-center py-8">
          <LoadingSpinner size="lg" />
        </CardContent>
      </Card>
    </div>
  );
};
