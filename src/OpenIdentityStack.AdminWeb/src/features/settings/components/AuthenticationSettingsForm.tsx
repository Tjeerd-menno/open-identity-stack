import { useState } from 'react';
import { 
  useAuthenticationSettings, 
  useAuthenticationProviders, 
  useSetDefaultProvider, 
  useSetLocalFallback 
} from '../hooks';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { 
  Select, 
  SelectContent, 
  SelectItem, 
  SelectTrigger, 
  SelectValue 
} from '@/components/ui/select';
import { Checkbox } from '@/components/ui/checkbox';
import { Label } from '@/components/ui/label';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { LoadingSpinner } from '@/components/common/LoadingSpinner';
import { Badge } from '@/components/ui/badge';
import { AlertCircle, Settings, Info } from 'lucide-react';

export function AuthenticationSettingsForm() {
  const { data: settings, isLoading: settingsLoading, error: settingsError } = useAuthenticationSettings();
  const { data: providers, isLoading: providersLoading, error: providersError } = useAuthenticationProviders();
  const setDefaultProviderMutation = useSetDefaultProvider();
  const setLocalFallbackMutation = useSetLocalFallback();

  const [successMessage, setSuccessMessage] = useState<string | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const isLoading = settingsLoading || providersLoading;
  const error = settingsError || providersError;

  const handleDefaultProviderChange = async (providerId: string) => {
    setSuccessMessage(null);
    setErrorMessage(null);
    try {
      await setDefaultProviderMutation.mutateAsync({ providerId });
      setSuccessMessage('Default authentication provider updated successfully.');
    } catch {
      setErrorMessage('Failed to update default authentication provider.');
    }
  };

  const handleLocalFallbackChange = async (checked: boolean) => {
    setSuccessMessage(null);
    setErrorMessage(null);
    try {
      await setLocalFallbackMutation.mutateAsync({ enabled: checked });
      setSuccessMessage('Local fallback setting updated successfully.');
    } catch {
      setErrorMessage('Failed to update local fallback setting.');
    }
  };

  if (isLoading) {
    return (
      <div className="flex items-center justify-center min-h-[400px]">
        <LoadingSpinner size="lg" />
      </div>
    );
  }

  if (error) {
    return (
      <Alert variant="destructive" className="max-w-2xl mx-auto mt-8">
        <AlertCircle className="h-4 w-4" />
        <AlertTitle>Error</AlertTitle>
        <AlertDescription>
          Failed to load authentication settings. Please try again later.
        </AlertDescription>
      </Alert>
    );
  }

  const isExternalProviderDefault = settings && !settings.isLocalDefault;
  const activeProviders = providers?.filter(p => p.isActive) || [];

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-4">
        <Settings className="h-8 w-8" />
        <div>
          <h1 className="text-3xl font-bold">Authentication Settings</h1>
          <p className="text-muted-foreground">
            Configure default authentication provider and local fallback options
          </p>
        </div>
      </div>

      {successMessage && (
        <Alert className="border-green-200 bg-green-50 text-green-800">
          <Info className="h-4 w-4" />
          <AlertTitle>Success</AlertTitle>
          <AlertDescription>{successMessage}</AlertDescription>
        </Alert>
      )}

      {errorMessage && (
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertTitle>Error</AlertTitle>
          <AlertDescription>{errorMessage}</AlertDescription>
        </Alert>
      )}

      <div className="grid gap-6 md:grid-cols-2">
        {/* Default Provider Selection */}
        <Card>
          <CardHeader>
            <CardTitle>Default Authentication Provider</CardTitle>
            <CardDescription>
              Select which authentication method is presented as the primary login option.
              This affects the login page behavior for all users.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="default-provider">Default Provider</Label>
              <Select
                value={settings?.defaultProviderId || 'local'}
                onValueChange={handleDefaultProviderChange}
                disabled={setDefaultProviderMutation.isPending}
              >
                <SelectTrigger id="default-provider" data-testid="default-provider-select">
                  <SelectValue placeholder="Select a provider" />
                </SelectTrigger>
                <SelectContent>
                  {activeProviders.map((provider) => (
                    <SelectItem key={provider.id} value={provider.id}>
                      <div className="flex items-center gap-2">
                        <span>{provider.displayName}</span>
                        <Badge variant={provider.type === 'local' ? 'secondary' : 'outline'} className="text-xs">
                          {provider.type}
                        </Badge>
                      </div>
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="pt-2">
              <div className="text-sm text-muted-foreground">
                <strong>Current status:</strong>{' '}
                {settings?.isLocalDefault ? (
                  <Badge variant="secondary">Local Accounts (Default)</Badge>
                ) : (
                  <Badge variant="default">External Provider</Badge>
                )}
              </div>
            </div>
          </CardContent>
        </Card>

        {/* Local Fallback Configuration */}
        <Card>
          <CardHeader>
            <CardTitle>Admin Local Fallback</CardTitle>
            <CardDescription>
              When an external provider is the default, this setting allows IAM administrators
              to authenticate using local credentials as a fallback option.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="flex items-center space-x-3">
              <Checkbox
                id="local-fallback"
                data-testid="local-fallback-checkbox"
                checked={settings?.localFallbackEnabled ?? true}
                onCheckedChange={(checked) => handleLocalFallbackChange(checked === true)}
                disabled={setLocalFallbackMutation.isPending || settings?.isLocalDefault}
              />
              <div className="space-y-1">
                <Label htmlFor="local-fallback" className="cursor-pointer">
                  Enable local fallback for IAM admins
                </Label>
                <p className="text-sm text-muted-foreground">
                  Allows administrators to use local credentials when external provider is default
                </p>
              </div>
            </div>

            {settings?.isLocalDefault && (
              <Alert className="mt-4">
                <Info className="h-4 w-4" />
                <AlertDescription>
                  Local fallback setting is not applicable when Local Accounts is the default provider,
                  as all enabled local users can already authenticate using local credentials.
                </AlertDescription>
              </Alert>
            )}

            {isExternalProviderDefault && (
              <Alert className="mt-4">
                <AlertCircle className="h-4 w-4" />
                <AlertDescription>
                  <strong>Important:</strong> When an external provider is the default,
                  only IAM administrators can use local account authentication{' '}
                  {settings?.localFallbackEnabled ? '(currently enabled)' : '(currently disabled)'}.
                  Non-admin users must authenticate through the external provider.
                </AlertDescription>
              </Alert>
            )}
          </CardContent>
        </Card>
      </div>

      {/* Additional Information */}
      <Card>
        <CardHeader>
          <CardTitle>How Authentication Provider Selection Works</CardTitle>
        </CardHeader>
        <CardContent className="prose prose-sm max-w-none">
          <div className="space-y-4 text-sm text-muted-foreground">
            <div>
              <h4 className="font-medium text-foreground">Default Provider Selection</h4>
              <p>
                The default provider determines which login method is presented first on the authentication page.
                If set to Local Accounts, users see the standard username/password form.
                If set to an external provider, users are directed to that provider's login flow.
              </p>
            </div>
            <div>
              <h4 className="font-medium text-foreground">Admin-Only Local Fallback</h4>
              <p>
                When an external provider is the default, non-admin users cannot use local account authentication.
                This ensures consistent authentication through the designated provider.
                IAM administrators retain the ability to use local credentials for emergency access.
              </p>
            </div>
            <div>
              <h4 className="font-medium text-foreground">Login Page Behavior</h4>
              <p>
                The login page adapts based on these settings:
              </p>
              <ul className="list-disc pl-5 mt-2">
                <li>If Local Accounts is default: Standard login form is shown</li>
                <li>If External Provider is default: "Login with [Provider]" button is primary</li>
                <li>Admins see a "Login with Local Account" link when local fallback is enabled</li>
              </ul>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Last Updated */}
      {settings?.updatedAt && (
        <div className="text-sm text-muted-foreground text-right">
          Last updated: {new Date(settings.updatedAt).toLocaleString()}
        </div>
      )}
    </div>
  );
}
