import { useState } from 'react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Checkbox } from '@/components/ui/checkbox';
import { useAddApplicationSecret } from '../hooks/useAddApplicationSecret';
import { ApplicationSecretDisplay } from './ApplicationSecretDisplay';

interface AddApplicationSecretDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  applicationId: string;
}

export function AddApplicationSecretDialog({
  open,
  onOpenChange,
  applicationId,
}: AddApplicationSecretDialogProps) {
  const addSecret = useAddApplicationSecret();
  const [description, setDescription] = useState('');
  const [expiresAt, setExpiresAt] = useState('');
  const [revokeExisting, setRevokeExisting] = useState(false);
  const [clientSecret, setClientSecret] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const reset = () => {
    setDescription('');
    setExpiresAt('');
    setRevokeExisting(false);
    setClientSecret(null);
    setError(null);
  };

  const handleOpenChange = (nextOpen: boolean) => {
    if (!nextOpen) {
      reset();
    }

    onOpenChange(nextOpen);
  };

  const handleSubmit = async () => {
    setError(null);
    try {
      const response = await addSecret.mutateAsync({
        applicationId,
        data: {
          description: description || null,
          expiresAt: expiresAt || null,
          revokeExisting,
        },
      });
      setClientSecret(response.clientSecret);
    } catch (unknownError) {
      setError(unknownError instanceof Error ? unknownError.message : 'Failed to add secret.');
    }
  };

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Add Secret</DialogTitle>
          <DialogDescription>
            Create a client secret for this confidential application.
          </DialogDescription>
        </DialogHeader>

        {clientSecret ? (
          <ApplicationSecretDisplay clientSecret={clientSecret} />
        ) : (
          <div className="space-y-4">
            {error && (
              <Alert variant="destructive">
                <AlertDescription>{error}</AlertDescription>
              </Alert>
            )}
            <div className="space-y-2">
              <Label htmlFor="application-secret-description">Description</Label>
              <Input
                id="application-secret-description"
                value={description}
                onChange={(event) => setDescription(event.target.value)}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="application-secret-expires-at">Expires at</Label>
              <Input
                id="application-secret-expires-at"
                type="datetime-local"
                value={expiresAt}
                onChange={(event) => setExpiresAt(event.target.value)}
              />
            </div>
            <div className="flex items-center gap-2">
              <Checkbox
                id="application-secret-revoke-existing"
                checked={revokeExisting}
                onCheckedChange={(checked) => setRevokeExisting(checked === true)}
              />
              <Label htmlFor="application-secret-revoke-existing">Revoke existing secrets</Label>
            </div>
            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => handleOpenChange(false)}>
                Cancel
              </Button>
              <Button type="button" onClick={handleSubmit} disabled={addSecret.isPending}>
                Add Secret
              </Button>
            </DialogFooter>
          </div>
        )}

        {clientSecret && (
          <DialogFooter>
            <Button type="button" onClick={() => handleOpenChange(false)}>
              Done
            </Button>
          </DialogFooter>
        )}
      </DialogContent>
    </Dialog>
  );
}
