import { useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { ConfirmDialog } from '@/components/common/ConfirmDialog';
import { LoadingSpinner } from '@/components/common/LoadingSpinner';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { ApplicationClientType, ApplicationProfile } from '@/types';
import { ArrowLeft, Edit, Trash2 } from 'lucide-react';
import { useApplication } from '../hooks/useApplication';
import { useDeleteApplication } from '../hooks/useDeleteApplication';
import { useDisableApplication } from '../hooks/useDisableApplication';
import { useEnableApplication } from '../hooks/useEnableApplication';
import { ApplicationCredentials } from './ApplicationCredentials';
import { ApplicationStatusBadge } from './ApplicationStatusBadge';

const applicationProfileLabels: Record<ApplicationProfile, string> = {
  [ApplicationProfile.Web]: 'Web',
  [ApplicationProfile.SinglePage]: 'Single Page',
  [ApplicationProfile.Native]: 'Native',
  [ApplicationProfile.MachineToMachine]: 'Machine-to-machine',
  [ApplicationProfile.Device]: 'Device',
  [ApplicationProfile.Custom]: 'Custom',
};

const clientTypeLabels: Record<ApplicationClientType, string> = {
  [ApplicationClientType.Confidential]: 'Confidential',
  [ApplicationClientType.Public]: 'Public',
};

export function ApplicationDetail() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { data: application, isLoading } = useApplication(id!);
  const deleteApplication = useDeleteApplication();
  const disableApplication = useDisableApplication();
  const enableApplication = useEnableApplication();
  const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-12">
        <LoadingSpinner />
      </div>
    );
  }

  if (!application) {
    return (
      <div className="flex flex-col items-center justify-center py-12">
        <p className="text-muted-foreground">Application not found</p>
        <Button onClick={() => navigate('/applications')} className="mt-4">
          Back to Applications
        </Button>
      </div>
    );
  }

  const handleDelete = async () => {
    try {
      await deleteApplication.mutateAsync(id!);
      navigate('/applications');
    } catch (error) {
      console.error('Failed to delete application:', error);
    }
  };

  const handleDisable = async () => {
    try {
      await disableApplication.mutateAsync(id!);
    } catch (error) {
      console.error('Failed to disable application:', error);
    }
  };

  const handleEnable = async () => {
    try {
      await enableApplication.mutateAsync(id!);
    } catch (error) {
      console.error('Failed to enable application:', error);
    }
  };

  const formatDate = (dateString: string | null) => {
    if (!dateString) {
      return 'N/A';
    }

    return new Date(dateString).toLocaleString();
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-4">
          <Button
            variant="ghost"
            size="sm"
            onClick={() => navigate('/applications')}
          >
            <ArrowLeft className="h-4 w-4" />
          </Button>
          <div>
            <h1 className="text-3xl font-bold">{application.displayName}</h1>
            <p className="text-muted-foreground">{application.clientId}</p>
          </div>
        </div>
        <div className="flex gap-2">
          <Button onClick={() => navigate(`/applications/${id}/edit`)} variant="outline">
            <Edit className="h-4 w-4 mr-2" />
            Edit
          </Button>
          {application.status === 'Active' ? (
            <Button
              onClick={handleDisable}
              variant="outline"
              disabled={disableApplication.isPending}
            >
              Disable
            </Button>
          ) : (
            <Button
              onClick={handleEnable}
              variant="outline"
              disabled={enableApplication.isPending}
            >
              Enable
            </Button>
          )}
          <Button
            onClick={() => setShowDeleteConfirm(true)}
            variant="destructive"
          >
            <Trash2 className="h-4 w-4 mr-2" />
            Delete
          </Button>
        </div>
      </div>

      <div role="tablist" aria-label="Application detail sections" className="flex gap-2 border-b">
        <button
          type="button"
          role="tab"
          aria-selected="true"
          className="border-b-2 border-primary px-3 py-2 text-sm font-medium"
        >
          Overview
        </button>
        <button
          type="button"
          role="tab"
          aria-selected="true"
          className="border-b-2 border-primary px-3 py-2 text-sm font-medium"
        >
          OAuth Configuration
        </button>
        <button
          type="button"
          role="tab"
          aria-selected="true"
          className="border-b-2 border-primary px-3 py-2 text-sm font-medium"
        >
          Credentials
        </button>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Application Information</CardTitle>
          <CardDescription>Unified application details</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid grid-cols-2 gap-4">
            <div>
              <div className="text-sm font-medium text-muted-foreground">ID</div>
              <div className="mt-1 text-xs font-mono">{application.id}</div>
            </div>
            <div>
              <div className="text-sm font-medium text-muted-foreground">Status</div>
              <div className="mt-1">
                <ApplicationStatusBadge status={application.status} />
              </div>
            </div>
            <div>
              <div className="text-sm font-medium text-muted-foreground">Application Profile</div>
              <div className="mt-1">{applicationProfileLabels[application.profile]}</div>
            </div>
            <div>
              <div className="text-sm font-medium text-muted-foreground">Client Type</div>
              <div className="mt-1">{clientTypeLabels[application.clientType]}</div>
            </div>
            <div>
              <div className="text-sm font-medium text-muted-foreground">Require PKCE</div>
              <div className="mt-1">{application.requirePkce ? 'Yes' : 'No'}</div>
            </div>
            <div>
              <div className="text-sm font-medium text-muted-foreground">Require Consent</div>
              <div className="mt-1">{application.requireConsent ? 'Yes' : 'No'}</div>
            </div>
            <div>
              <div className="text-sm font-medium text-muted-foreground">Created</div>
              <div className="mt-1">{formatDate(application.createdAt)}</div>
            </div>
            {application.modifiedAt && (
              <div>
                <div className="text-sm font-medium text-muted-foreground">Modified</div>
                <div className="mt-1">{formatDate(application.modifiedAt)}</div>
              </div>
            )}
          </div>
          {application.description && (
            <div>
              <div className="text-sm font-medium text-muted-foreground">Description</div>
              <div className="mt-1">{application.description}</div>
            </div>
          )}
          {application.requiresMigrationReview && (
            <div className="rounded-md bg-yellow-50 px-3 py-2 text-sm text-yellow-800">
              Migration review required{application.migrationSource ? ` from ${application.migrationSource}` : ''}.
            </div>
          )}
        </CardContent>
      </Card>

      <ValueListCard
        title="Redirect URIs"
        description="Allowed redirect URIs after authentication"
        values={application.redirectUris}
        emptyMessage="No redirect URIs configured"
      />
      <ValueListCard
        title="Post Logout Redirect URIs"
        description="Allowed redirect URIs after logout"
        values={application.postLogoutRedirectUris}
        emptyMessage="No post logout redirect URIs configured"
      />
      <TagListCard
        title="Allowed Scopes"
        description="Scopes this application can request"
        values={application.allowedScopes}
        className="bg-blue-50 text-blue-700 ring-blue-700/10"
      />
      <TagListCard
        title="Allowed Grant Types"
        description="OAuth2 grant types this application can use"
        values={application.allowedGrantTypes}
        className="bg-green-50 text-green-700 ring-green-700/10"
      />

      <ApplicationCredentials application={application} />

      <ConfirmDialog
        open={showDeleteConfirm}
        onOpenChange={setShowDeleteConfirm}
        onConfirm={handleDelete}
        title="Delete Application"
        description="Are you sure you want to delete this application? This action cannot be undone and will immediately revoke protocol access."
      />
    </div>
  );
}

interface ValueListCardProps {
  title: string;
  description: string;
  values: string[];
  emptyMessage: string;
}

function ValueListCard({ title, description, values, emptyMessage }: ValueListCardProps) {
  return (
    <Card>
      <CardHeader>
        <CardTitle>{title}</CardTitle>
        <CardDescription>{description}</CardDescription>
      </CardHeader>
      <CardContent>
        <div className="space-y-2">
          {values.length > 0 ? (
            values.map((value) => (
              <div
                key={value}
                className="rounded-md bg-muted px-3 py-2 text-sm font-mono"
              >
                {value}
              </div>
            ))
          ) : (
            <p className="text-sm text-muted-foreground">{emptyMessage}</p>
          )}
        </div>
      </CardContent>
    </Card>
  );
}

interface TagListCardProps {
  title: string;
  description: string;
  values: string[];
  className: string;
}

function TagListCard({ title, description, values, className }: TagListCardProps) {
  return (
    <Card>
      <CardHeader>
        <CardTitle>{title}</CardTitle>
        <CardDescription>{description}</CardDescription>
      </CardHeader>
      <CardContent>
        <div className="flex flex-wrap gap-2">
          {values.map((value) => (
            <span
              key={value}
              className={`inline-flex items-center rounded-md px-2 py-1 text-xs font-medium ring-1 ring-inset ${className}`}
            >
              {value}
            </span>
          ))}
        </div>
      </CardContent>
    </Card>
  );
}
