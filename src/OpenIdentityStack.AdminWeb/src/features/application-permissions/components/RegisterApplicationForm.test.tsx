import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { RegisterApplicationForm } from './RegisterApplicationForm';

describe('RegisterApplicationForm', () => {
  it('submits an application permission manifest', async () => {
    const user = userEvent.setup();
    const onSubmit = vi.fn().mockResolvedValue(undefined);

    render(<RegisterApplicationForm onSubmit={onSubmit} />);

    await user.type(screen.getByLabelText(/application id/i), 'patient-api');
    await user.type(screen.getByLabelText(/^display name$/i), 'Patient API');
    await user.type(screen.getByLabelText(/^description$/i), 'Patient records');
    await user.type(screen.getByLabelText(/^version$/i), '1.0.0');
    await user.type(screen.getByLabelText(/^owner id$/i), 'owner-1');
    await user.type(screen.getByLabelText(/^manifest base url$/i), 'https://patient.example/api');
    await user.type(screen.getByLabelText(/^permission key 1$/i), 'patient:read');
    await user.type(screen.getByLabelText(/^permission display name 1$/i), 'Read patients');
    await user.type(screen.getByLabelText(/^permission description 1$/i), 'Allows reading patient data');
    await user.type(screen.getByLabelText(/^permission category 1$/i), 'Patients');
    await user.click(screen.getByRole('button', { name: /add application/i }));

    expect(onSubmit).toHaveBeenCalledWith({
      manifest: {
        schemaVersion: '1.0.0',
        application: {
          id: 'patient-api',
          displayName: 'Patient API',
          description: 'Patient records',
          version: '1.0.0',
        },
        permissions: [
          {
            key: 'patient:read',
            displayName: 'Read patients',
            description: 'Allows reading patient data',
            category: 'Patients',
          },
        ],
      },
      ownerId: 'owner-1',
      ownerType: 'user',
      manifestBaseUrl: 'https://patient.example/api',
    });
  });

  it('shows validation error and blocks duplicate permission keys', async () => {
    const user = userEvent.setup();
    const onSubmit = vi.fn().mockResolvedValue(undefined);

    render(<RegisterApplicationForm onSubmit={onSubmit} />);

    await user.type(screen.getByLabelText(/application id/i), 'patient-api');
    await user.type(screen.getByLabelText(/^display name$/i), 'Patient API');
    await user.type(screen.getByLabelText(/^version$/i), '1.0.0');
    await user.type(screen.getByLabelText(/^owner id$/i), 'owner-1');
    await user.type(screen.getByLabelText(/^permission key 1$/i), 'patient:read');
    await user.type(screen.getByLabelText(/^permission display name 1$/i), 'Read patients');
    await user.click(screen.getByRole('button', { name: /add permission/i }));
    await user.type(screen.getByLabelText(/^permission key 2$/i), 'patient:read');
    await user.type(screen.getByLabelText(/^permission display name 2$/i), 'Read patients again');
    await user.click(screen.getByRole('button', { name: /add application/i }));

    expect(await screen.findByText(/permission keys must be unique/i)).toBeInTheDocument();
    expect(onSubmit).not.toHaveBeenCalled();
  });

  it('submits a well-known permissions endpoint for import', async () => {
    const user = userEvent.setup();
    const onSubmit = vi.fn().mockResolvedValue(undefined);
    const onImportEndpoint = vi.fn().mockResolvedValue(undefined);

    render(<RegisterApplicationForm onSubmit={onSubmit} onImportEndpoint={onImportEndpoint} />);

    await user.type(screen.getByLabelText(/well-known permissions endpoint/i), 'https://patient.example/.well-known/permissions');
    await user.click(screen.getByRole('button', { name: /import endpoint/i }));

    expect(onImportEndpoint).toHaveBeenCalledWith('https://patient.example/.well-known/permissions');
    expect(onSubmit).not.toHaveBeenCalled();
  });
});
