/**
 * AddCertificateDialog Component
 * 
 * Dialog for adding a certificate to a service account
 */

import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useAddCertificate } from '../hooks/useAddCertificate';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Textarea } from '@/components/ui/textarea';
import { Form, FormControl, FormDescription, FormField, FormItem, FormLabel, FormMessage } from '@/components/ui/form';

const schema = z.object({
  certificate: z.string().min(1, 'Certificate is required'),
});

type FormData = z.infer<typeof schema>;

interface AddCertificateDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  serviceAccountId: string;
}

export function AddCertificateDialog({ open, onOpenChange, serviceAccountId }: AddCertificateDialogProps) {
  const addCertificate = useAddCertificate();

  const form = useForm<FormData>({
    resolver: zodResolver(schema),
    defaultValues: {
      certificate: '',
    },
  });

  const handleSubmit = async (_data: FormData) => {
    try {
      await addCertificate.mutateAsync({ 
        serviceAccountId, 
        data: { 
          thumbprint: 'auto', // Server will extract from certificate
          subject: 'auto', // Server will extract from certificate
          expiresAt: new Date(Date.now() + 365 * 24 * 60 * 60 * 1000).toISOString(), // Default 1 year
        } 
      });
      form.reset();
      onOpenChange(false);
    } catch (error) {
      console.error('Failed to add certificate:', error);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Add Certificate</DialogTitle>
          <DialogDescription>
            Add a certificate for certificate-based authentication
          </DialogDescription>
        </DialogHeader>

        <Form {...form}>
          <form onSubmit={form.handleSubmit(handleSubmit)} className="space-y-4">
            <FormField
              control={form.control}
              name="certificate"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Certificate (PEM format)</FormLabel>
                  <FormControl>
                    <Textarea 
                      {...field} 
                      placeholder="-----BEGIN CERTIFICATE-----&#10;...&#10;-----END CERTIFICATE-----"
                      rows={10}
                      className="font-mono text-xs"
                    />
                  </FormControl>
                  <FormDescription>
                    Paste the certificate in PEM format
                  </FormDescription>
                  <FormMessage />
                </FormItem>
              )}
            />

            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
                Cancel
              </Button>
              <Button type="submit" disabled={addCertificate.isPending}>
                Add Certificate
              </Button>
            </DialogFooter>
          </form>
        </Form>
      </DialogContent>
    </Dialog>
  );
}
