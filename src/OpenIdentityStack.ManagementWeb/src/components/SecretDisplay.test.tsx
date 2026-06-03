import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { renderManagementWeb } from '@/test/render';
import { SecretDisplay } from './SecretDisplay';

describe('SecretDisplay', () => {
  it('hides and reveals a one-time secret', async () => {
    renderManagementWeb(<SecretDisplay label="Client secret" value="secret-value" />);

    expect(screen.getByText('••••••••••••••••')).toBeInTheDocument();
    expect(screen.queryByText('secret-value')).not.toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: /show client secret/i }));

    expect(screen.getByText('secret-value')).toBeInTheDocument();
  });

  it('copies the secret value', async () => {
    const writeText = vi.fn().mockResolvedValue(undefined);
    Object.assign(navigator, { clipboard: { writeText } });

    renderManagementWeb(<SecretDisplay label="Client secret" value="secret-value" />);

    await userEvent.click(screen.getByRole('button', { name: /copy client secret/i }));

    expect(writeText).toHaveBeenCalledWith('secret-value');
  });
});
