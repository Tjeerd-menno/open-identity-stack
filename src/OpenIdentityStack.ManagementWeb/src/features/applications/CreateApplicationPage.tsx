import { Stack, Text, Title } from '@mantine/core';
import { useState } from 'react';
import { useNavigate } from 'react-router';
import { BackLink } from '@/components/DetailPrimitives';
import { getApiErrorMessage } from '@/lib/admin-api';
import { ApplicationForm, type CreateApplicationFormData } from './ApplicationForm';
import { useCreateApplication } from './applications-hooks';

export function CreateApplicationPage() {
  const navigate = useNavigate();
  const createApplication = useCreateApplication();
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (data: CreateApplicationFormData) => {
    setError(null);
    try {
      const response = await createApplication.mutateAsync(data);
      navigate(`/applications/${response.id}`);
    } catch (unknownError) {
      setError(getApiErrorMessage(unknownError));
      throw unknownError;
    }
  };

  return (
    <Stack gap="lg">
      <BackLink label="Back to Applications" to="/applications" />
      <div>
        <Title order={1}>Create application</Title>
        <Text c="dimmed">Create a unified OAuth 2.0 / OpenID Connect application.</Text>
      </div>
      {error && <Text c="red">{error}</Text>}
      <ApplicationForm onSubmit={handleSubmit} isLoading={createApplication.isPending} />
    </Stack>
  );
}
