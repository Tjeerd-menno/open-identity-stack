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
import { useAddApplicationCertificate } from '../hooks/useAddApplicationCertificate';

interface AddApplicationCertificateDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  applicationId: string;
}

export function AddApplicationCertificateDialog({
  open,
  onOpenChange,
  applicationId,
}: AddApplicationCertificateDialogProps) {
  const addCertificate = useAddApplicationCertificate();
  const [thumbprint, setThumbprint] = useState('');
  const [subject, setSubject] = useState('');
  const [description, setDescription] = useState('');
  const [expiresAt, setExpiresAt] = useState('');
  const [error, setError] = useState<string | null>(null);

  const reset = () => {
    setThumbprint('');
    setSubject('');
    setDescription('');
    setExpiresAt('');
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
      await addCertificate.mutateAsync({
        applicationId,
        data: {
          thumbprint,
          subject: subject || null,
          description: description || null,
          expiresAt: expiresAt || null,
        },
      });
      handleOpenChange(false);
    } catch (unknownError) {
      setError(unknownError instanceof Error ? unknownError.message : 'Failed to add certificate.');
    }
  };

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Add Certificate</DialogTitle>
          <DialogDescription>
            Add an X.509 certificate credential for this application.
          </DialogDescription>
        </DialogHeader>
        <div className="space-y-4">
          {error && (
            <Alert variant="destructive">
              <AlertDescription>{error}</AlertDescription>
            </Alert>
          )}
          <div className="space-y-2">
            <Label htmlFor="application-certificate-thumbprint">Thumbprint</Label>
            <Input
              id="application-certificate-thumbprint"
              value={thumbprint}
              onChange={(event) => setThumbprint(event.target.value)}
            />
          </div>
          <div className="space-y-2">
            <Label htmlFor="application-certificate-subject">Subject</Label>
            <Input
              id="application-certificate-subject"
              value={subject}
              onChange={(event) => setSubject(event.target.value)}
            />
          </div>
          <div className="space-y-2">
            <Label htmlFor="application-certificate-description">Description</Label>
            <Input
              id="application-certificate-description"
              value={description}
              onChange={(event) => setDescription(event.target.value)}
            />
          </div>
          <div className="space-y-2">
            <Label htmlFor="application-certificate-expires-at">Expires at</Label>
            <Input
              id="application-certificate-expires-at"
              type="datetime-local"
              value={expiresAt}
              onChange={(event) => setExpiresAt(event.target.value)}
            />
          </div>
          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => handleOpenChange(false)}>
              Cancel
            </Button>
            <Button type="button" onClick={handleSubmit} disabled={addCertificate.isPending}>
              Add Certificate
            </Button>
          </DialogFooter>
        </div>
      </DialogContent>
    </Dialog>
  );
}
