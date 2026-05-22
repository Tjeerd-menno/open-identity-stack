import { useNavigate } from 'react-router-dom';
import { RegisterServiceForm } from '../components';
import { useRegisterService } from '../hooks';
import type { RegisterServiceRequest } from '@/types';

export function RegisterServicePage() {
  const navigate = useNavigate();
  const mutation = useRegisterService();

  const submit = async (request: RegisterServiceRequest) => {
    const service = await mutation.mutateAsync(request);
    navigate(`/service-permissions/${service.id}`);
  };

  return (
    <div className="container mx-auto py-6">
      <div className="mb-6">
        <h1 className="text-3xl font-bold">Register Service</h1>
        <p className="text-muted-foreground">Declare a service namespace and its initial assignable permissions.</p>
      </div>
      <RegisterServiceForm onSubmit={submit} isLoading={mutation.isPending} />
    </div>
  );
}
