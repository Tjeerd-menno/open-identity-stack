/**
 * ClientDetail Component
 * 
 * Displays detailed information about a client
 */

import { useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useClient } from '../hooks/useClient';
import { useDeleteClient } from '../hooks/useDeleteClient';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { LoadingSpinner } from '@/components/common/LoadingSpinner';
import { ConfirmDialog } from '@/components/common/ConfirmDialog';
import { ArrowLeft, Edit, Trash2 } from 'lucide-react';
import type { ClientType } from '@/types';

export function ClientDetail() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { data: client, isLoading } = useClient(id!);
  const deleteClient = useDeleteClient();
  const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-12">
        <LoadingSpinner />
      </div>
    );
  }

  if (!client) {
    return (
      <div className="flex flex-col items-center justify-center py-12">
        <p className="text-muted-foreground">Client not found</p>
        <Button onClick={() => navigate('/clients')} className="mt-4">
          Back to Clients
        </Button>
      </div>
    );
  }

  const handleDelete = async () => {
    try {
      await deleteClient.mutateAsync(id!);
      navigate('/clients');
    } catch (error) {
      console.error('Failed to delete client:', error);
    }
  };

  const formatDate = (dateString: string | null) => {
    if (!dateString) return 'N/A';
    return new Date(dateString).toLocaleString();
  };

  const getClientTypeLabel = (clientType: ClientType): string => {
    return clientType === 0 ? 'Confidential' : 'Public';
  };

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-4">
          <Button
            variant="ghost"
            size="sm"
            onClick={() => navigate('/clients')}
          >
            <ArrowLeft className="h-4 w-4" />
          </Button>
          <div>
            <h1 className="text-3xl font-bold">{client.displayName}</h1>
            <p className="text-muted-foreground">{client.clientIdValue}</p>
          </div>
        </div>
        <div className="flex gap-2">
          <Button onClick={() => navigate(`/clients/${id}/edit`)} variant="outline">
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

      {/* Client Information Card */}
      <Card>
        <CardHeader>
          <CardTitle>Client Information</CardTitle>
          <CardDescription>Basic client details</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid grid-cols-2 gap-4">
            <div>
              <div className="text-sm font-medium text-muted-foreground">ID</div>
              <div className="mt-1 text-xs font-mono">{client.id}</div>
            </div>
            <div>
              <div className="text-sm font-medium text-muted-foreground">Client Type</div>
              <div className="mt-1">{getClientTypeLabel(client.clientType)}</div>
            </div>
            <div>
              <div className="text-sm font-medium text-muted-foreground">Require PKCE</div>
              <div className="mt-1">{client.requirePkce ? 'Yes' : 'No'}</div>
            </div>
            <div>
              <div className="text-sm font-medium text-muted-foreground">Require Consent</div>
              <div className="mt-1">{client.requireConsent ? 'Yes' : 'No'}</div>
            </div>
            <div>
              <div className="text-sm font-medium text-muted-foreground">Created</div>
              <div className="mt-1">{formatDate(client.createdAt)}</div>
            </div>
            {client.modifiedAt && (
              <div>
                <div className="text-sm font-medium text-muted-foreground">Modified</div>
                <div className="mt-1">{formatDate(client.modifiedAt)}</div>
              </div>
            )}
          </div>
          {client.description && (
            <div>
              <div className="text-sm font-medium text-muted-foreground">Description</div>
              <div className="mt-1">{client.description}</div>
            </div>
          )}
        </CardContent>
      </Card>

      {/* Redirect URIs Card */}
      <Card>
        <CardHeader>
          <CardTitle>Redirect URIs</CardTitle>
          <CardDescription>Allowed redirect URIs after authentication</CardDescription>
        </CardHeader>
        <CardContent>
          <div className="space-y-2">
            {client.redirectUris.length > 0 ? (
              client.redirectUris.map((uri, index) => (
                <div
                  key={index}
                  className="rounded-md bg-muted px-3 py-2 text-sm font-mono"
                >
                  {uri}
                </div>
              ))
            ) : (
              <p className="text-sm text-muted-foreground">No redirect URIs configured</p>
            )}
          </div>
        </CardContent>
      </Card>

      {/* Post Logout Redirect URIs Card */}
      <Card>
        <CardHeader>
          <CardTitle>Post Logout Redirect URIs</CardTitle>
          <CardDescription>Allowed redirect URIs after logout</CardDescription>
        </CardHeader>
        <CardContent>
          <div className="space-y-2">
            {client.postLogoutRedirectUris.length > 0 ? (
              client.postLogoutRedirectUris.map((uri, index) => (
                <div
                  key={index}
                  className="rounded-md bg-muted px-3 py-2 text-sm font-mono"
                >
                  {uri}
                </div>
              ))
            ) : (
              <p className="text-sm text-muted-foreground">No post logout redirect URIs configured</p>
            )}
          </div>
        </CardContent>
      </Card>

      {/* Scopes Card */}
      <Card>
        <CardHeader>
          <CardTitle>Allowed Scopes</CardTitle>
          <CardDescription>Scopes this client can request</CardDescription>
        </CardHeader>
        <CardContent>
          <div className="flex flex-wrap gap-2">
            {client.allowedScopes.map((scope) => (
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
          <CardDescription>OAuth2 grant types this client can use</CardDescription>
        </CardHeader>
        <CardContent>
          <div className="flex flex-wrap gap-2">
            {client.allowedGrantTypes.map((grantType) => (
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
        title="Delete Client"
        description="Are you sure you want to delete this client? This action cannot be undone and will immediately revoke all access."
      />
    </div>
  );
}
