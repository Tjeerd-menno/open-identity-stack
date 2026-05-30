/**
 * CreateClientPage Component
 * 
 * Page for creating a new client
 */

import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useCreateClient } from '../hooks/useCreateClient';
import { ClientForm, type CreateClientFormData } from '../components/ClientForm';
import { SecretDisplay } from '@/features/service-accounts/components/SecretDisplay';
import { Button } from '@/components/ui/button';
import { ArrowLeft } from 'lucide-react';
import type { CreateClientRequest, ClientType } from '@/types';

export function CreateClientPage() {
  const navigate = useNavigate();
  const createClient = useCreateClient();
  const [createdClient, setCreatedClient] = useState<{ clientSecret: string | null } | null>(null);

  const handleSubmit = async (data: CreateClientFormData) => {
    try {
      const request: CreateClientRequest = {
        clientId: data.clientId,
        displayName: data.displayName,
        clientType: data.clientType as ClientType,
        description: data.description || null,
        redirectUris: data.redirectUris,
        postLogoutRedirectUris: data.postLogoutRedirectUris,
        allowedScopes: data.allowedScopes,
        allowedGrantTypes: data.allowedGrantTypes,
        requirePkce: data.requirePkce,
        requireConsent: data.requireConsent,
      };

      const response = await createClient.mutateAsync(request);
      setCreatedClient({ clientSecret: response.clientSecret });
    } catch (error) {
      console.error('Failed to create client:', error);
    }
  };

  if (createdClient?.clientSecret) {
    return (
      <div className="container mx-auto py-6 max-w-2xl">
        <div className="space-y-6">
          <div className="flex items-center gap-4">
            <Button
              variant="ghost"
              size="sm"
              onClick={() => navigate('/clients')}
            >
              <ArrowLeft className="h-4 w-4" />
            </Button>
            <div>
              <h1 className="text-3xl font-bold">Client Created</h1>
              <p className="text-muted-foreground">
                Save the client secret below - it will only be shown once
              </p>
            </div>
          </div>

          <SecretDisplay 
            secret={createdClient.clientSecret}
            title="Client Secret"
            label="Save this client secret"
            description="You will not be able to see this client secret again. Store it securely."
          />

          <div className="flex justify-end">
            <Button onClick={() => navigate('/clients')}>
              Done
            </Button>
          </div>
        </div>
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
            onClick={() => navigate('/clients')}
          >
            <ArrowLeft className="h-4 w-4" />
          </Button>
          <div>
            <h1 className="text-3xl font-bold">Create Client</h1>
            <p className="text-muted-foreground">
              Create a new OAuth 2.0 / OpenID Connect client
            </p>
          </div>
        </div>

        <ClientForm
          onSubmit={(data) => handleSubmit(data as CreateClientFormData)}
          isLoading={createClient.isPending}
        />
      </div>
    </div>
  );
}
