import { useState } from 'react';
import { AlertTriangle, Copy } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';

interface ApplicationSecretDisplayProps {
  clientSecret: string;
}

export function ApplicationSecretDisplay({ clientSecret }: ApplicationSecretDisplayProps) {
  const [copied, setCopied] = useState(false);

  const copySecret = async () => {
    await navigator.clipboard.writeText(clientSecret);
    setCopied(true);
  };

  return (
    <Alert className="border-yellow-200 bg-yellow-50">
      <AlertTriangle className="h-4 w-4 text-yellow-600" />
      <AlertTitle className="text-yellow-900">Client secret created</AlertTitle>
      <AlertDescription className="mt-3 space-y-3 text-yellow-800">
        <p>You will not be able to see this secret again. Store it securely now.</p>
        <div className="flex items-center gap-2 rounded-md border border-yellow-300 bg-white p-3 font-mono text-sm">
          <span className="flex-1 break-all">{clientSecret}</span>
          <Button
            type="button"
            variant="ghost"
            size="sm"
            onClick={copySecret}
            aria-label="Copy client secret"
          >
            <Copy className="h-4 w-4" />
          </Button>
        </div>
        {copied && <p className="text-xs">Copied to clipboard</p>}
      </AlertDescription>
    </Alert>
  );
}
