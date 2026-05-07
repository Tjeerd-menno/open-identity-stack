/**
 * ServiceAccountDetail Component
 * 
 * Displays detailed information about a service account
 */

import { useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useServiceAccount } from '../hooks/useServiceAccount';
import { useDeleteServiceAccount } from '../hooks/useDeleteServiceAccount';
import { useEnableServiceAccount } from '../hooks/useEnableServiceAccount';
import { useDisableServiceAccount } from '../hooks/useDisableServiceAccount';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { LoadingSpinner } from '@/components/common/LoadingSpinner';
import { ConfirmDialog } from '@/components/common/ConfirmDialog';
import { ServiceAccountStatusBadge } from './ServiceAccountStatusBadge';
import { RotateSecretDialog } from './RotateSecretDialog';
import { AddCertificateDialog } from './AddCertificateDialog';
import { ArrowLeft, Edit, Trash2, Key, FileKey, Ban, CheckCircle } from 'lucide-react';

export function ServiceAccountDetail() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { data: account, isLoading } = useServiceAccount(id!);
  const deleteServiceAccount = useDeleteServiceAccount();
  const enableServiceAccount = useEnableServiceAccount();
  const disableServiceAccount = useDisableServiceAccount();
  const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);
  const [showRotateSecret, setShowRotateSecret] = useState(false);
  const [showAddCertificate, setShowAddCertificate] = useState(false);
  const [showDisableConfirm, setShowDisableConfirm] = useState(false);

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-12">
        <LoadingSpinner />
      </div>
    );
  }

  if (!account) {
    return (
      <div className="flex flex-col items-center justify-center py-12">
        <p className="text-muted-foreground">Service account not found</p>
        <Button onClick={() => navigate('/service-accounts')} className="mt-4">
          Back to Service Accounts
        </Button>
      </div>
    );
  }

  const handleDelete = async () => {
    try {
      await deleteServiceAccount.mutateAsync(id!);
      navigate('/service-accounts');
    } catch (error) {
      console.error('Failed to delete service account:', error);
    }
  };

  const handleEnable = async () => {
    try {
      await enableServiceAccount.mutateAsync(id!);
    } catch (error) {
      console.error('Failed to enable service account:', error);
    }
  };

  const handleDisable = async () => {
    try {
      await disableServiceAccount.mutateAsync(id!);
      setShowDisableConfirm(false);
    } catch (error) {
      console.error('Failed to disable service account:', error);
    }
  };

  const formatDate = (dateString: string | null) => {
    if (!dateString) return 'N/A';
    return new Date(dateString).toLocaleString();
  };

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-4">
          <Button
            variant="ghost"
            size="sm"
            onClick={() => navigate('/service-accounts')}
          >
            <ArrowLeft className="h-4 w-4" />
          </Button>
          <div>
            <h1 className="text-3xl font-bold">{account.displayName}</h1>
            <p className="text-muted-foreground">{account.clientId}</p>
          </div>
        </div>
        <div className="flex gap-2">
          {account.status === 'Active' ? (
            <Button onClick={() => setShowDisableConfirm(true)} variant="outline">
              <Ban className="h-4 w-4 mr-2" />
              Disable
            </Button>
          ) : (
            <Button onClick={handleEnable} variant="outline">
              <CheckCircle className="h-4 w-4 mr-2" />
              Enable
            </Button>
          )}
          <Button onClick={() => setShowRotateSecret(true)} variant="outline">
            <Key className="h-4 w-4 mr-2" />
            Rotate Secret
          </Button>
          <Button onClick={() => setShowAddCertificate(true)} variant="outline">
            <FileKey className="h-4 w-4 mr-2" />
            Add Certificate
          </Button>
          <Button onClick={() => navigate(`/service-accounts/${id}/edit`)} variant="outline">
            <Edit className="h-4 w-4 mr-2" />
            Edit
          </Button>
          <Button 
            onClick={() => setShowDeleteConfirm(true)}
            variant="destructive"
          >
            <Trash2 className="h-4 w-4 mr-2" />
            Delete
          </Button>
        </div>
      </div>

      {/* Account Information Card */}
      <Card>
        <CardHeader>
          <CardTitle>Service Account Information</CardTitle>
          <CardDescription>Basic service account details</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid grid-cols-2 gap-4">
            <div>
              <div className="text-sm font-medium text-muted-foreground">ID</div>
              <div className="mt-1 text-xs font-mono">{account.id}</div>
            </div>
            <div>
              <div className="text-sm font-medium text-muted-foreground">Status</div>
              <div className="mt-1">
                <ServiceAccountStatusBadge status={account.status} />
              </div>
            </div>
            <div>
              <div className="text-sm font-medium text-muted-foreground">Credentials</div>
              <div className="mt-1">{account.credentialCount}</div>
            </div>
            <div>
              <div className="text-sm font-medium text-muted-foreground">Certificates</div>
              <div className="mt-1">{account.certificateCount}</div>
            </div>
            <div>
              <div className="text-sm font-medium text-muted-foreground">Created</div>
              <div className="mt-1">{formatDate(account.createdAt)}</div>
            </div>
            {account.modifiedAt && (
              <div>
                <div className="text-sm font-medium text-muted-foreground">Modified</div>
                <div className="mt-1">{formatDate(account.modifiedAt)}</div>
              </div>
            )}
          </div>
        </CardContent>
      </Card>

      {/* Scopes Card */}
      <Card>
        <CardHeader>
          <CardTitle>Allowed Scopes</CardTitle>
          <CardDescription>Scopes this service account can request</CardDescription>
        </CardHeader>
        <CardContent>
          <div className="flex flex-wrap gap-2">
            {account.allowedScopes.map((scope) => (
              <span
                key={scope}
                className="inline-flex items-center rounded-md bg-blue-50 px-2 py-1 text-xs font-medium text-blue-700 ring-1 ring-inset ring-blue-700/10"
              >
                {scope}
              </span>
            ))}
          </div>
        </CardContent>
      </Card>

      {/* Grant Types Card */}
      <Card>
        <CardHeader>
          <CardTitle>Allowed Grant Types</CardTitle>
          <CardDescription>OAuth2 grant types this service account can use</CardDescription>
        </CardHeader>
        <CardContent>
          <div className="flex flex-wrap gap-2">
            {account.allowedGrantTypes.map((grantType) => (
              <span
                key={grantType}
                className="inline-flex items-center rounded-md bg-green-50 px-2 py-1 text-xs font-medium text-green-700 ring-1 ring-inset ring-green-700/10"
              >
                {grantType}
              </span>
            ))}
          </div>
        </CardContent>
      </Card>

      {/* Delete Confirmation Dialog */}
      <ConfirmDialog
        open={showDeleteConfirm}
        onOpenChange={setShowDeleteConfirm}
        onConfirm={handleDelete}
        title="Delete Service Account"
        description="Are you sure you want to delete this service account? This action cannot be undone and will immediately revoke all access."
      />

      {/* Disable Confirmation Dialog */}
      <ConfirmDialog
        open={showDisableConfirm}
        onOpenChange={setShowDisableConfirm}
        onConfirm={handleDisable}
        title="Disable Service Account"
        description="Are you sure you want to disable this service account? It will no longer be able to authenticate."
      />

      {/* Rotate Secret Dialog */}
      <RotateSecretDialog
        open={showRotateSecret}
        onOpenChange={setShowRotateSecret}
        serviceAccountId={id!}
      />

      {/* Add Certificate Dialog */}
      <AddCertificateDialog
        open={showAddCertificate}
        onOpenChange={setShowAddCertificate}
        serviceAccountId={id!}
      />
    </div>
  );
}
