/**
 * SessionDetail Component
 * 
 * Displays detailed information about a session
 */

import { useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useSession } from '../hooks/useSession';
import { useRevokeSession } from '../hooks/useRevokeSession';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { LoadingSpinner } from '@/components/common/LoadingSpinner';
import { ConfirmDialog } from '@/components/common/ConfirmDialog';
import { SessionStatusBadge } from './SessionStatusBadge';
import { ArrowLeft, Ban } from 'lucide-react';

export function SessionDetail() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { data: session, isLoading } = useSession(id!);
  const revokeSession = useRevokeSession();
  const [showRevokeConfirm, setShowRevokeConfirm] = useState(false);

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-12">
        <LoadingSpinner />
      </div>
    );
  }

  if (!session) {
    return (
      <div className="flex flex-col items-center justify-center py-12">
        <p className="text-muted-foreground">Session not found</p>
        <Button onClick={() => navigate('/sessions')} className="mt-4">
          Back to Sessions
        </Button>
      </div>
    );
  }

  const handleRevoke = async () => {
    try {
      await revokeSession.mutateAsync(id!);
      navigate('/sessions');
    } catch (error) {
      console.error('Failed to revoke session:', error);
    }
  };

  const formatDate = (dateString: string) => {
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
            onClick={() => navigate('/sessions')}
          >
            <ArrowLeft className="h-4 w-4" />
          </Button>
          <div>
            <h1 className="text-3xl font-bold">Session Details</h1>
            <p className="text-muted-foreground font-mono text-sm">{session.id}</p>
          </div>
        </div>
        {session.status === 'Active' && (
          <Button 
            onClick={() => setShowRevokeConfirm(true)}
            variant="destructive"
          >
            <Ban className="h-4 w-4 mr-2" />
            Revoke Session
          </Button>
        )}
      </div>

      {/* Session Information Card */}
      <Card>
        <CardHeader>
          <CardTitle>Session Information</CardTitle>
          <CardDescription>Basic session details</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid grid-cols-2 gap-4">
            <div>
              <div className="text-sm font-medium text-muted-foreground">Session ID</div>
              <div className="mt-1 text-xs font-mono">{session.id}</div>
            </div>
            <div>
              <div className="text-sm font-medium text-muted-foreground">Status</div>
              <div className="mt-1">
                <SessionStatusBadge status={session.status} />
              </div>
            </div>
            <div>
              <div className="text-sm font-medium text-muted-foreground">User ID</div>
              <div className="mt-1 text-xs font-mono">{session.userId}</div>
            </div>
            <div>
              <div className="text-sm font-medium text-muted-foreground">Client Count</div>
              <div className="mt-1">{session.clientCount}</div>
            </div>
            <div>
              <div className="text-sm font-medium text-muted-foreground">IP Address</div>
              <div className="mt-1">{session.ipAddress}</div>
            </div>
            <div>
              <div className="text-sm font-medium text-muted-foreground">User Agent</div>
              <div className="mt-1 text-sm break-all">{session.userAgent}</div>
            </div>
            <div>
              <div className="text-sm font-medium text-muted-foreground">Created</div>
              <div className="mt-1">{formatDate(session.createdAt)}</div>
            </div>
            <div>
              <div className="text-sm font-medium text-muted-foreground">Last Activity</div>
              <div className="mt-1">{formatDate(session.lastActivityAt)}</div>
            </div>
            <div>
              <div className="text-sm font-medium text-muted-foreground">Expires</div>
              <div className="mt-1">{formatDate(session.expiresAt)}</div>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Revoke Confirmation Dialog */}
      <ConfirmDialog
        open={showRevokeConfirm}
        onOpenChange={setShowRevokeConfirm}
        onConfirm={handleRevoke}
        title="Revoke Session"
        description="Are you sure you want to revoke this session? The user will be immediately logged out from all clients associated with this session."
      />
    </div>
  );
}
