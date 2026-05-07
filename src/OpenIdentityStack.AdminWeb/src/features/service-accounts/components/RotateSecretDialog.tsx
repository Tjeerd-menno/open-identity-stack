/**
 * RotateSecretDialog Component
 * 
 * Dialog for rotating service account secret
 */

import { useState } from 'react';
import { useRotateSecret } from '../hooks/useRotateSecret';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { SecretDisplay } from './SecretDisplay';
import { AlertCircle } from 'lucide-react';
import { Alert, AlertDescription } from '@/components/ui/alert';

interface RotateSecretDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  serviceAccountId: string;
}

export function RotateSecretDialog({ open, onOpenChange, serviceAccountId }: RotateSecretDialogProps) {
  const rotateSecret = useRotateSecret();
  const [newSecret, setNewSecret] = useState<string | null>(null);

  const handleRotate = async () => {
    try {
      const response = await rotateSecret.mutateAsync({ serviceAccountId, data: {} });
      setNewSecret(response.newSecret);
    } catch (error) {
      console.error('Failed to rotate secret:', error);
    }
  };

  const handleClose = () => {
    setNewSecret(null);
    onOpenChange(false);
  };

  return (
    <Dialog open={open} onOpenChange={handleClose}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Rotate Service Account Secret</DialogTitle>
          <DialogDescription>
            Generate a new secret for this service account. The old secret will be immediately invalidated.
          </DialogDescription>
        </DialogHeader>

        {newSecret ? (
          <SecretDisplay secret={newSecret} />
        ) : (
          <>
            <Alert>
              <AlertCircle className="h-4 w-4" />
              <AlertDescription>
                This will immediately invalidate the current secret. Make sure you update all applications using this service account.
              </AlertDescription>
            </Alert>

            <DialogFooter>
              <Button variant="outline" onClick={handleClose}>
                Cancel
              </Button>
              <Button onClick={handleRotate} disabled={rotateSecret.isPending}>
                Rotate Secret
              </Button>
            </DialogFooter>
          </>
        )}

        {newSecret && (
          <DialogFooter>
            <Button onClick={handleClose}>Done</Button>
          </DialogFooter>
        )}
      </DialogContent>
    </Dialog>
  );
}
