import { screen, waitFor } from '@testing-library/react';
import { Route, Routes, useLocation } from 'react-router';
import { renderManagementWeb } from '@/test/render';
import { AuthCallbackRoute } from './auth-callback';

function CurrentPath() {
  const location = useLocation();
  return <span data-testid="current-path">{location.pathname}</span>;
}

describe('AuthCallbackRoute', () => {
  it('keeps the callback URL available for the auth provider to process', async () => {
    renderManagementWeb(
      <Routes>
        <Route
          path="/auth/callback"
          element={
            <>
              <AuthCallbackRoute />
              <CurrentPath />
            </>
          }
        />
        <Route path="/" element={<CurrentPath />} />
      </Routes>,
      { initialEntries: ['/auth/callback?code=test-code&state=test-state'] }
    );

    expect(screen.getByText('Processing authentication...')).toBeInTheDocument();

    await waitFor(() => {
      expect(screen.getByTestId('current-path')).toHaveTextContent('/auth/callback');
    });
  });
});
