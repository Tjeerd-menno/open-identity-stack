/**
 * CreateServiceAccountPage Component
 * 
 * Page for creating a new service account
 */

import { useNavigate } from 'react-router-dom';
import { useCreateServiceAccount } from '../hooks/useCreateServiceAccount';
import { ServiceAccountForm } from '../components/ServiceAccountForm';
import { SecretDisplay } from '../components/SecretDisplay';
import { Button } from '@/components/ui/button';
import { ArrowLeft } from 'lucide-react';
import type { CreateServiceAccountRequest } from '@/types';
import { useState } from 'react';

export function CreateServiceAccountPage() {
  const navigate = useNavigate();
  const createServiceAccount = useCreateServiceAccount();
  const [newSecret, setNewSecret] = useState<string | null>(null);

  const handleSubmit = async (data: CreateServiceAccountRequest) => {
    try {
      const response = await createServiceAccount.mutateAsync(data);
      setNewSecret(response.initialSecret);
    } catch (error) {
      console.error('Failed to create service account:', error);
    }
  };

  if (newSecret) {
    return (
      <div className="container mx-auto py-6 max-w-2xl">
        <div className="space-y-6">
          <div className="flex items-center gap-4">
            <Button
              variant="ghost"
              size="sm"
              onClick={() => navigate('/service-accounts')}
            >
              <ArrowLeft className="h-4 w-4" />
            </Button>
            <div>
              <h1 className="text-3xl font-bold">Service Account Created</h1>
              <p className="text-muted-foreground">
                Save the secret below - it will only be shown once
              </p>
            </div>
          </div>

          <SecretDisplay secret={newSecret} />

          <div className="flex justify-end">
            <Button onClick={() => navigate('/service-accounts')}>
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
            onClick={() => navigate('/service-accounts')}
          >
            <ArrowLeft className="h-4 w-4" />
          </Button>
          <div>
            <h1 className="text-3xl font-bold">Create Service Account</h1>
            <p className="text-muted-foreground">
              Create a new service account for API access
            </p>
          </div>
        </div>

        <ServiceAccountForm
          onSubmit={handleSubmit}
          isLoading={createServiceAccount.isPending}
        />
      </div>
    </div>
  );
}
