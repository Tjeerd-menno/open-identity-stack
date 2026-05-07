import { useNavigate } from 'react-router-dom';
import { useCreateProvider } from '../hooks';
import { ProviderForm } from '../components';
import { Button } from '@/components/ui/button';
import { ArrowLeft } from 'lucide-react';
import type { CreateProviderRequest } from '@/types';

export function CreateProviderPage() {
  const navigate = useNavigate();
  const createMutation = useCreateProvider();

  const handleSubmit = async (data: CreateProviderRequest) => {
    try {
      const provider = await createMutation.mutateAsync(data);
      navigate(`/providers/${provider.id}`);
    } catch (error) {
      console.error('Failed to create provider:', error);
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-4">
        <Button variant="ghost" size="icon" onClick={() => navigate('/providers')}>
          <ArrowLeft className="h-4 w-4" />
        </Button>
        <h1 className="text-3xl font-bold">Create Identity Provider</h1>
      </div>

      <div className="max-w-2xl">
        <ProviderForm onSubmit={handleSubmit} isLoading={createMutation.isPending} />
      </div>
    </div>
  );
}
