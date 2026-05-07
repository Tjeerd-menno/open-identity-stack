/**
 * EditProviderPage Component
 * 
 * Page for editing an existing identity provider
 */

import { useNavigate, useParams } from 'react-router-dom';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { LoadingSpinner } from '@/components/common/LoadingSpinner';
import { EditProviderForm } from '../components/EditProviderForm';
import { useProvider } from '../hooks/useProvider';
import { useUpdateProvider } from '../hooks/useUpdateProvider';
import { ArrowLeft } from 'lucide-react';
import type { UpdateProviderRequest } from '@/types';

export function EditProviderPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { data: provider, isLoading } = useProvider(id!);
  const updateProvider = useUpdateProvider();

  const handleSubmit = async (data: UpdateProviderRequest) => {
    try {
      await updateProvider.mutateAsync({ id: id!, data });
      navigate(`/providers/${id}`);
    } catch (error) {
      console.error('Failed to update provider:', error);
      throw error;
    }
  };

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-12">
        <LoadingSpinner />
      </div>
    );
  }

  if (!provider) {
    return (
      <div className="space-y-4">
        <h1 className="text-3xl font-bold">Provider Not Found</h1>
        <Button onClick={() => navigate('/providers')}>
          <ArrowLeft className="h-4 w-4 mr-2" />
          Back to Providers
        </Button>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-4">
        <Button
          variant="ghost"
          size="sm"
          onClick={() => navigate(`/providers/${id}`)}
        >
          <ArrowLeft className="h-4 w-4" />
        </Button>
        <div>
          <h1 className="text-3xl font-bold">Edit Provider</h1>
          <p className="text-muted-foreground">{provider.displayName}</p>
        </div>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Provider Details</CardTitle>
          <CardDescription>
            Update identity provider configuration
          </CardDescription>
        </CardHeader>
        <CardContent>
          <EditProviderForm
            provider={provider}
            onSubmit={handleSubmit}
            isLoading={updateProvider.isPending}
          />
        </CardContent>
      </Card>
    </div>
  );
}
