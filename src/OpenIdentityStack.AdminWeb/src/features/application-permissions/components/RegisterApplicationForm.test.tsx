import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { RegisterApplicationForm } from './RegisterApplicationForm';

describe('RegisterApplicationForm', () => {
  it('submits an application permission manifest', async () => {
    const user = userEvent.setup();
    const onSubmit = vi.fn().mockResolvedValue(undefined);

    render(<RegisterApplicationForm onSubmit={onSubmit} />);

    fireEvent.change(screen.getByLabelText(/application id/i), { target: { value: 'patient-api' } });
    fireEvent.change(screen.getByLabelText(/application name/i), { target: { value: 'Patient API' } });
    fireEvent.change(screen.getByLabelText(/version/i), { target: { value: '1.0.0' } });
    fireEvent.change(screen.getByLabelText(/permission name 1/i), { target: { value: 'read:patients' } });
    fireEvent.change(screen.getByLabelText(/permission description 1/i), { target: { value: 'Allows reading patient data' } });
    fireEvent.change(screen.getByLabelText(/permission category 1/i), { target: { value: 'Patients' } });
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

    fireEvent.change(screen.getByLabelText(/well-known permissions endpoint/i), {
      target: { value: 'https://patient.example/.well-known/permissions' },
    });
    await user.click(screen.getByRole('button', { name: /import endpoint/i }));

    await waitFor(() => {
      expect(onImportEndpoint).toHaveBeenCalledWith('https://patient.example/.well-known/permissions');
    });
    expect(onSubmit).not.toHaveBeenCalled();
  });
});
