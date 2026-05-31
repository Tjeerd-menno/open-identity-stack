import { useEffect } from 'react';
import { useNavigate } from 'react-router';

export function AuthCallbackRoute() {
  const navigate = useNavigate();

  useEffect(() => {
    async function handleCallback() {
      try {
        // This route is reached after the OIDC provider redirects back.
        // The oidc-client-ts library handles the response processing
        // via signinCallback when the URL contains the authorization code.
        navigate('/');
      } catch (error) {
        console.error('Auth callback error:', error);
        navigate('/');
      }
    }

    handleCallback();
  }, [navigate]);

  return <div>Processing authentication...</div>;
}
