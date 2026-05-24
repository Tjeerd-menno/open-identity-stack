import { useNavigate } from 'react-router-dom';
import { RegisterApplicationForm } from '../components';
import { useImportPermissionManifestFromEndpoint, useRegisterPermissionManifest } from '../hooks';
import type { PermissionManifestRequest } from '@/types';

export function RegisterApplicationPage() {
  const navigate = useNavigate();
  const registerMutation = useRegisterPermissionManifest();
  const importMutation = useImportPermissionManifestFromEndpoint();

  const submit = async (request: PermissionManifestRequest) => {
    const application = await registerMutation.mutateAsync(request);
    navigate(`/application-permissions/${application.id}`);
  };

  const importEndpoint = async (endpoint: string) => {
    const application = await importMutation.mutateAsync(endpoint);
    navigate(`/application-permissions/${application.id}`);
  };

  return (
    <div className="container mx-auto py-6">
      <div className="mb-6">
        <h1 className="text-3xl font-bold">Add Application</h1>
      </div>
      <RegisterApplicationForm
        onSubmit={submit}
        onImportEndpoint={importEndpoint}
        isLoading={registerMutation.isPending}
        isImporting={importMutation.isPending}
      />
    </div>
  );
}
