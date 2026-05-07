/**
 * SecretDisplay Component
 * 
 * Displays a one-time secret with clear warning and copy functionality
 */

import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Copy, AlertTriangle, Eye, EyeOff } from 'lucide-react';
import { cn } from '@/lib/utils';

interface SecretDisplayProps {
  secret: string;
  title?: string;
  label?: string;
  description?: string;
}

export function SecretDisplay({
  secret,
  title = 'Secret Key',
  label = 'Save this secret',
  description = 'You will not be able to see this secret again. Store it securely.',
}: SecretDisplayProps) {
  const [copied, setCopied] = useState(false);
  const [visible, setVisible] = useState(false);

  const handleCopy = async () => {
    await navigator.clipboard.writeText(secret);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  return (
    <Alert className="border-yellow-200 bg-yellow-50">
      <AlertTriangle className="h-4 w-4 text-yellow-600" />
      <AlertTitle className="text-yellow-900">{title}</AlertTitle>
      <AlertDescription className="mt-4 space-y-3 text-yellow-800">
        <p className="font-semibold">{label}</p>
        <p className="text-sm">{description}</p>

        <div className="mt-4 space-y-2">
          <div className="flex items-center gap-2 rounded-md border border-yellow-300 bg-white p-3 font-mono text-sm">
            <span
              data-testid="secret"
              className={cn(
                'flex-1 break-all',
                !visible && 'blur-sm select-none'
              )}
            >
              {secret}
            </span>
            <Button
              type="button"
              variant="ghost"
              size="sm"
              onClick={() => setVisible(!visible)}
              className="h-8 w-8 p-0"
              title={visible ? 'Hide secret' : 'Show secret'}
            >
              {visible ? (
                <EyeOff className="h-4 w-4" />
              ) : (
                <Eye className="h-4 w-4" />
              )}
            </Button>
            <Button
              type="button"
              variant="ghost"
              size="sm"
              onClick={handleCopy}
              className="h-8 w-8 p-0"
              title={copied ? 'Copied!' : 'Copy secret'}
            >
              <Copy className="h-4 w-4" />
            </Button>
          </div>
          <p className="text-xs text-yellow-700">
            {copied ? '✓ Copied to clipboard' : 'Click the copy icon to copy'}
          </p>
        </div>
      </AlertDescription>
    </Alert>
  );
}
