import { useParams, useNavigate } from 'react-router-dom';
import { useProvider, useDeleteProvider } from '../hooks';
import { providersApi } from '../api';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { LoadingSpinner } from '@/components/common/LoadingSpinner';
import { ConfirmDialog } from '@/components/common/ConfirmDialog';
import { ProviderStatus } from '@/types';
import { ArrowLeft, Edit, Trash2, Check, X } from 'lucide-react';
import { useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';

export function ProviderDetail() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [showDeleteDialog, setShowDeleteDialog] = useState(false);

  const { data: provider, isLoading } = useProvider(id!);
  const deleteMutation = useDeleteProvider();

  const toggleMutation = useMutation({
    mutationFn: async () => {
      if (!provider) return;
      if (provider.status === ProviderStatus.Active) {
        return providersApi.disableProvider(provider.id);
      } else {
        return providersApi.enableProvider(provider.id);
      }
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['provider', id] });
      queryClient.invalidateQueries({ queryKey: ['providers'] });
    }
  });

  const handleToggleStatus = async () => {
    if (!provider) return;
    await toggleMutation.mutateAsync();
  };

  const handleDelete = async () => {
    if (!id) return;
    await deleteMutation.mutateAsync(id);
    navigate('/providers');
  };

  if (isLoading) return <LoadingSpinner />;
  if (!provider) return <div>Provider not found</div>;

  const isActive = provider.status === ProviderStatus.Active;

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-4">
          <Button variant="ghost" size="icon" onClick={() => navigate('/providers')}>
            <ArrowLeft className="h-4 w-4" />
          </Button>
          <div>
            <h1 className="text-3xl font-bold">{provider.displayName}</h1>
            <p className="text-muted-foreground">{provider.name}</p>
          </div>
        </div>
        <div className="flex gap-2">
          <Button
            variant={isActive ? 'outline' : 'default'}
            onClick={handleToggleStatus}
            disabled={toggleMutation.isPending}
          >
            {isActive ? (
              <>
                <X className="mr-2 h-4 w-4" />
                Disable
              </>
            ) : (
              <>
                <Check className="mr-2 h-4 w-4" />
                Enable
              </>
            )}
          </Button>
          <Button variant="outline" onClick={() => navigate(`/providers/${id}/edit`)}>
            <Edit className="mr-2 h-4 w-4" />
            Edit
          </Button>
          <Button variant="destructive" onClick={() => setShowDeleteDialog(true)}>
            <Trash2 className="mr-2 h-4 w-4" />
            Delete
          </Button>
        </div>
      </div>

      <div className="grid gap-6 md:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>Provider Information</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <div>
              <div className="text-sm font-medium text-muted-foreground">Status</div>
              <div className="mt-1">
                <Badge variant={isActive ? 'default' : 'secondary'}>
                  {isActive ? 'Active' : 'Disabled'}
                </Badge>
              </div>
            </div>
            <div>
              <div className="text-sm font-medium text-muted-foreground">Authority</div>
              <div className="mt-1">{provider.authority || '-'}</div>
            </div>
            <div>
              <div className="text-sm font-medium text-muted-foreground">Client ID</div>
              <div className="mt-1">{provider.clientId || '-'}</div>
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Configuration</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <div>
              <div className="text-sm font-medium text-muted-foreground">Scopes</div>
              <div className="mt-1">
                {provider.scopes && provider.scopes.length > 0 ? (
                  <div className="flex flex-wrap gap-1">
                    {provider.scopes.map((scope: string) => (
                      <Badge key={scope} variant="outline">{scope}</Badge>
                    ))}
                  </div>
                ) : '-'}
              </div>
            </div>
            <div>
              <div className="text-sm font-medium text-muted-foreground">JIT Provisioning</div>
              <div className="mt-1">
                <Badge variant={provider.jitProvisioningEnabled ? 'default' : 'secondary'}>
                  {provider.jitProvisioningEnabled ? 'Enabled' : 'Disabled'}
                </Badge>
              </div>
            </div>
            <div>
              <div className="text-sm font-medium text-muted-foreground">Created At</div>
              <div className="mt-1">{new Date(provider.createdAt).toLocaleString()}</div>
            </div>
          </CardContent>
        </Card>
      </div>

      <ConfirmDialog
        open={showDeleteDialog}
        onOpenChange={setShowDeleteDialog}
        onConfirm={handleDelete}
        title="Delete Provider"
        description="Are you sure you want to delete this identity provider? This action cannot be undone and may affect users who authenticate through this provider."
        variant="destructive"
      />
    </div>
  );
}
