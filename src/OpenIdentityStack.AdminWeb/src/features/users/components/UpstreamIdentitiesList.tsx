/**
 * UpstreamIdentitiesList Component
 * 
 * Displays list of upstream identities (federated logins) linked to a user
 */

import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { useUpstreamIdentities } from '../hooks/useUpstreamIdentities';
import { useUnlinkIdentity } from '../hooks/useUnlinkIdentity';
import { X, Plus, ExternalLink } from 'lucide-react';
import { LoadingSpinner } from '@/components/common/LoadingSpinner';

interface UpstreamIdentitiesListProps {
  userId: string;
}

export function UpstreamIdentitiesList({ userId }: UpstreamIdentitiesListProps) {
  const { data: identities, isLoading } = useUpstreamIdentities(userId);
  const unlinkIdentity = useUnlinkIdentity();
  const [, setShowLinkDialog] = useState(false);

  const handleUnlink = async (providerId: string) => {
    try {
      await unlinkIdentity.mutateAsync({ userId, providerId });
    } catch (error) {
      console.error('Failed to unlink identity:', error);
    }
  };

  const formatDate = (dateString: string) => {
    return new Date(dateString).toLocaleDateString();
  };

  if (isLoading) {
    return <LoadingSpinner />;
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h3 className="text-lg font-semibold">Linked Accounts</h3>
        <Button 
          onClick={() => setShowLinkDialog(true)}
          size="sm"
        >
          <Plus className="h-4 w-4 mr-1" />
          Link Account
        </Button>
      </div>

      {!identities || identities.length === 0 ? (
        <p className="text-sm text-muted-foreground">No linked accounts</p>
      ) : (
        <div className="space-y-2">
          {identities.map((identity) => (
            <div
              key={`${identity.providerId}-${identity.subjectId}`}
              className="flex items-center justify-between p-3 border rounded-md"
              data-testid="identity-item"
            >
              <div className="flex-1">
                <div className="flex items-center gap-2">
                  <ExternalLink className="h-4 w-4 text-muted-foreground" />
                  <span className="font-medium">{identity.providerName}</span>
                </div>
                <div className="text-sm text-muted-foreground mt-1">
                  {identity.email || identity.subjectId}
                </div>
                <div className="text-xs text-muted-foreground mt-1">
                  Linked {formatDate(identity.linkedAt)}
                  {identity.lastLoginAt && 
                    ` • Last login ${formatDate(identity.lastLoginAt)}`
                  }
                </div>
              </div>
              <Button
                variant="ghost"
                size="sm"
                onClick={() => handleUnlink(identity.providerId)}
                aria-label={`Unlink ${identity.providerName}`}
              >
                <X className="h-4 w-4" />
              </Button>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
