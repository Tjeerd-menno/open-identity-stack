import { useState } from 'react';
import { FileKey, Key, XCircle } from 'lucide-react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { LoadingSpinner } from '@/components/common/LoadingSpinner';
import { ApplicationClientType, type Application, type ApplicationCredential } from '@/types';
import { useApplicationCredentials } from '../hooks/useApplicationCredentials';
import { useRevokeApplicationCredential } from '../hooks/useRevokeApplicationCredential';
import { AddApplicationCertificateDialog } from './AddApplicationCertificateDialog';
import { AddApplicationSecretDialog } from './AddApplicationSecretDialog';

interface ApplicationCredentialsProps {
  application: Application;
}

export function ApplicationCredentials({ application }: ApplicationCredentialsProps) {
  const [showAddSecret, setShowAddSecret] = useState(false);
  const [showAddCertificate, setShowAddCertificate] = useState(false);
  const credentials = useApplicationCredentials(application.id);
  const revokeCredential = useRevokeApplicationCredential();

  if (application.clientType === ApplicationClientType.Public) {
    return (
      <Card>
        <CardHeader>
          <CardTitle>Credentials</CardTitle>
          <CardDescription>Client authentication credentials</CardDescription>
        </CardHeader>
        <CardContent>
          <Alert>
            <AlertDescription>
              Public applications cannot use client secrets or certificates.
            </AlertDescription>
          </Alert>
        </CardContent>
      </Card>
    );
  }

  const handleRevoke = async (credentialId: string) => {
    await revokeCredential.mutateAsync({
      applicationId: application.id,
      credentialId,
    });
  };

  return (
    <Card>
      <CardHeader>
        <div className="flex items-center justify-between gap-4">
          <div>
            <CardTitle>Credentials</CardTitle>
            <CardDescription>
              Client secrets and certificates for this confidential application
            </CardDescription>
          </div>
          <div className="flex gap-2">
            <Button type="button" variant="outline" onClick={() => setShowAddSecret(true)}>
              <Key className="h-4 w-4 mr-2" />
              Add Secret
            </Button>
            <Button type="button" variant="outline" onClick={() => setShowAddCertificate(true)}>
              <FileKey className="h-4 w-4 mr-2" />
              Add Certificate
            </Button>
          </div>
        </div>
      </CardHeader>
      <CardContent>
        {credentials.isLoading ? (
          <div className="flex justify-center py-6">
            <LoadingSpinner />
          </div>
        ) : (
          <CredentialList
            credentials={credentials.data ?? []}
            onRevoke={handleRevoke}
            revokePending={revokeCredential.isPending}
          />
        )}
      </CardContent>

      <AddApplicationSecretDialog
        open={showAddSecret}
        onOpenChange={setShowAddSecret}
        applicationId={application.id}
      />
      <AddApplicationCertificateDialog
        open={showAddCertificate}
        onOpenChange={setShowAddCertificate}
        applicationId={application.id}
      />
    </Card>
  );
}

interface CredentialListProps {
  credentials: ApplicationCredential[];
  onRevoke: (credentialId: string) => Promise<void>;
  revokePending: boolean;
}

function CredentialList({ credentials, onRevoke, revokePending }: CredentialListProps) {
  if (credentials.length === 0) {
    return <p className="text-sm text-muted-foreground">No credentials configured.</p>;
  }

  return (
    <div className="space-y-3">
      {credentials.map((credential) => (
        <div
          key={credential.id}
          className="flex items-start justify-between gap-4 rounded-md border p-4"
        >
          <div className="space-y-2">
            <div className="flex items-center gap-2">
              <Badge variant={credential.revokedAt ? 'destructive' : 'secondary'}>
                {credential.type}
              </Badge>
              {credential.revokedAt && <Badge variant="outline">Revoked</Badge>}
            </div>
            <p className="font-medium">{credential.description ?? credential.subject ?? credential.id}</p>
            {credential.thumbprint && (
              <p className="font-mono text-xs text-muted-foreground">{credential.thumbprint}</p>
            )}
            {credential.subject && (
              <p className="text-sm text-muted-foreground">{credential.subject}</p>
            )}
            <p className="text-xs text-muted-foreground">
              Created {formatDate(credential.createdAt)}
              {credential.expiresAt ? ` · Expires ${formatDate(credential.expiresAt)}` : ''}
              {credential.lastUsedAt ? ` · Last used ${formatDate(credential.lastUsedAt)}` : ''}
            </p>
          </div>
          {!credential.revokedAt && (
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={() => void onRevoke(credential.id)}
              disabled={revokePending}
            >
              <XCircle className="h-4 w-4 mr-2" />
              Revoke
            </Button>
          )}
        </div>
      ))}
    </div>
  );
}

function formatDate(value: string) {
  return new Date(value).toLocaleString();
}
