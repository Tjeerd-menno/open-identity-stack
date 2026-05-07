/**
 * T056: Create SilentCallback component for token refresh
 * 
 * Silent callback page for automatic token renewal.
 * Loaded in a hidden iframe for seamless token refresh.
 */

import React, { useEffect } from 'react';
import { useAuth } from '../hooks/useAuth';

/**
 * Silent callback component
 * 
 * This component is loaded in a hidden iframe during automatic token renewal.
 * It processes the silent signin callback and communicates back to the parent window.
 * 
 * The OIDC library (oidc-client-ts) handles the communication automatically.
 * This page should be minimal and fast-loading.
 * 
 * Flow:
 * 1. Access token is about to expire
 * 2. UserManager loads this page in a hidden iframe
 * 3. Page processes the callback and extracts new tokens
 * 4. Communicates back to parent window
 * 5. Parent window updates auth state with new tokens
 */
export const SilentCallback: React.FC = () => {
  const { signinSilentCallback } = useAuth();

  useEffect(() => {
    const handleSilentCallback = async () => {
      try {
        // Process the silent callback
        // This extracts the tokens from the URL and communicates with the parent window
        await signinSilentCallback();
      } catch (err) {
        console.error('Silent callback error:', err);
        // Error is logged but not displayed as this page is in an iframe
      }
    };

    handleSilentCallback();
  }, [signinSilentCallback]);

  // This page is loaded in an iframe, so it doesn't need any visible content
  // However, we'll show a minimal message for debugging purposes
  return (
    <div style={{ display: 'none' }}>
      <p>Processing silent authentication...</p>
    </div>
  );
};
