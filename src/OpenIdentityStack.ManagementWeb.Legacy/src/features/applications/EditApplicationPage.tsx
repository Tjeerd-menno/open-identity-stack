import { Alert, Button, Group, Loader, Stack, Text, Title } from '@mantine/core';
import { useState } from 'react';
import { useNavigate, useParams } from 'react-router';
import { getApiErrorMessage } from '@/lib/admin-api';
import { ApplicationForm, type UpdateApplicationFormData } from './ApplicationForm';
import { useApplication, useUpdateApplication } from './applications-hooks';

export function EditApplicationPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const application = useApplication(id);
  const updateApplication = useUpdateApplication(id ?? '');
  const [error, setError] = useState<string | null>(null);

  if (application.isLoading) {
    return <Loader aria-label="Loading application" />;
  }

  if (application.isError) {
    return <Alert color="red">{getApiErrorMessage(application.error)}</Alert>;
  }

  if (!application.data || !id) {
    return <Alert>Application not found.</Alert>;
  }

  const handleSubmit = async (data: UpdateApplicationFormData) => {
    setError(null);
    try {
      await updateApplication.mutateAsync({
        displayName: data.displayName,
        description: data.description || null,
      });
      navigate(`/applications/${id}`);
    } catch (unknownError) {
      setError(getApiErrorMessage(unknownError));
      throw unknownError;
    }
  };

  return (
    <Stack gap="lg">
      <Group align="flex-start">
        <Button variant="default" onClick={() => navigate(`/applications/${id}`)}>Back</Button>
        <div>
          <Title order={1}>Edit application</Title>
          <Text c="dimmed">{application.data.clientId}</Text>
        </div>
      </Group>
      {error && <Text c="red">{error}</Text>}
      <ApplicationForm
        application={application.data}
        onSubmit={handleSubmit}
        isLoading={updateApplication.isPending}
      />
    </Stack>
  );
}
