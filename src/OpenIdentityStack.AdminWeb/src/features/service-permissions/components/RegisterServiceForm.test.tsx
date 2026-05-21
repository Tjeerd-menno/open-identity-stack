import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { RegisterServiceForm } from './RegisterServiceForm';

describe('RegisterServiceForm', () => {
  it('submits service metadata and initial permission', async () => {
    const user = userEvent.setup();
    const onSubmit = vi.fn().mockResolvedValue(undefined);

    render(<RegisterServiceForm onSubmit={onSubmit} />);

    await user.type(screen.getByLabelText(/service identifier/i), 'inventory-api');
    await user.type(screen.getByLabelText('Display Name'), 'Inventory API');
    await user.type(screen.getByLabelText(/owner id/i), 'owner-1');
    await user.type(screen.getByLabelText(/permission key 1/i), 'read');
    await user.type(screen.getByLabelText(/permission display name 1/i), 'Read inventory');
    await user.click(screen.getByRole('button', { name: /register service/i }));

    expect(onSubmit).toHaveBeenCalledWith(expect.objectContaining({
      serviceIdentifier: 'inventory-api',
      displayName: 'Inventory API',
      ownerId: 'owner-1',
      ownerType: 'User',
      permissions: [
        expect.objectContaining({
          permissionKey: 'read',
          displayName: 'Read inventory',
        }),
      ],
    }));
  });
});
