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
    await user.type(screen.getByLabelText(/application name/i), 'Patient API');
    await user.type(screen.getByLabelText(/version/i), '1.0.0');
    await user.type(screen.getByLabelText(/permission name 1/i), 'read:patients');
    await user.type(screen.getByLabelText(/permission description 1/i), 'Allows reading patient data');
    await user.type(screen.getByLabelText(/permission category 1/i), 'Patients');
    await user.click(screen.getByRole('button', { name: /add application/i }));

    expect(onSubmit).toHaveBeenCalledWith({
      application: {
        id: 'patient-api',
        name: 'Patient API',
        version: '1.0.0',
      },
      permissions: [
        {
          name: 'read:patients',
          description: 'Allows reading patient data',
          category: 'Patients',
        },
      ],
    });
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
