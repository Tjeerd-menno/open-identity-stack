/**
 * EditClientPage Component
 * 
 * Page for editing an existing client
 */

import { useNavigate, useParams } from 'react-router-dom';
import { Button } from '@/components/ui/button';
import { LoadingSpinner } from '@/components/common/LoadingSpinner';
import { ClientForm } from '../components/ClientForm';
import { useClient } from '../hooks/useClient';
import { useUpdateClient } from '../hooks/useUpdateClient';
import { ArrowLeft } from 'lucide-react';
import type { UpdateClientRequest } from '@/types';

export function EditClientPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { data: client, isLoading } = useClient(id!);
  const updateClient = useUpdateClient();

  const handleSubmit = async (data: UpdateClientRequest) => {
    try {
      await updateClient.mutateAsync({ 
        clientId: id!, 
        displayName: data.displayName,
        description: data.description 
      });
      navigate(`/clients/${id}`);
    } catch (error) {
      console.error('Failed to update client:', error);
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

  if (!client) {
    return (
      <div className="space-y-4">
        <h1 className="text-3xl font-bold">Client Not Found</h1>
        <Button onClick={() => navigate('/clients')}>
          <ArrowLeft className="h-4 w-4 mr-2" />
          Back to Clients
        </Button>
      </div>
    );
  }

  return (
    <div className="container mx-auto py-6 max-w-4xl">
      <div className="space-y-6">
        <div className="flex items-center gap-4">
          <Button
            variant="ghost"
            size="sm"
            onClick={() => navigate(`/clients/${id}`)}
          >
            <ArrowLeft className="h-4 w-4" />
          </Button>
          <div>
            <h1 className="text-3xl font-bold">Edit Client</h1>
            <p className="text-muted-foreground">{client.clientIdValue}</p>
          </div>
        </div>

        <ClientForm
          client={client}
          onSubmit={handleSubmit}
          isLoading={updateClient.isPending}
        />
      </div>
    </div>
  );
}
