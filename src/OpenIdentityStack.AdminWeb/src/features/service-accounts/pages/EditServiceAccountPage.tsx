/**
 * EditServiceAccountPage Component
 * 
 * Page for editing an existing service account
 */

import { useNavigate, useParams } from 'react-router-dom';
import { Button } from '@/components/ui/button';
import { LoadingSpinner } from '@/components/common/LoadingSpinner';
import { ServiceAccountForm } from '../components/ServiceAccountForm';
import { useServiceAccount } from '../hooks/useServiceAccount';
import { useUpdateServiceAccount } from '../hooks/useUpdateServiceAccount';
import { ArrowLeft } from 'lucide-react';
import type { UpdateServiceAccountRequest } from '@/types';

export function EditServiceAccountPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { data: serviceAccount, isLoading } = useServiceAccount(id!);
  const updateServiceAccount = useUpdateServiceAccount();

  const handleSubmit = async (data: UpdateServiceAccountRequest) => {
    try {
      await updateServiceAccount.mutateAsync({ serviceAccountId: id!, data });
      navigate(`/service-accounts/${id}`);
    } catch (error) {
      console.error('Failed to update service account:', error);
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

  if (!serviceAccount) {
    return (
      <div className="space-y-4">
        <h1 className="text-3xl font-bold">Service Account Not Found</h1>
        <Button onClick={() => navigate('/service-accounts')}>
          <ArrowLeft className="h-4 w-4 mr-2" />
          Back to Service Accounts
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
          onClick={() => navigate(`/service-accounts/${id}`)}
        >
          <ArrowLeft className="h-4 w-4" />
        </Button>
        <div>
          <h1 className="text-3xl font-bold">Edit Service Account</h1>
          <p className="text-muted-foreground">{serviceAccount.clientId}</p>
        </div>
      </div>

      <ServiceAccountForm
        serviceAccount={serviceAccount}
        onSubmit={handleSubmit}
        isLoading={updateServiceAccount.isPending}
      />
    </div>
  );
}
