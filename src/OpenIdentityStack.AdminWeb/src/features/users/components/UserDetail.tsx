/**
 * UserDetail Component
 * 
 * Displays detailed information about a user including roles, groups, and upstream identities
 */

import { useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useUser } from '../hooks/useUser';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { LoadingSpinner } from '@/components/common/LoadingSpinner';
import { UserStatusBadge } from './UserStatusBadge';
import { UserRolesList } from './UserRolesList';
import { UserGroupsList } from './UserGroupsList';
import { UpstreamIdentitiesList } from './UpstreamIdentitiesList';
import { ResetPasswordDialog } from './ResetPasswordDialog';
import { ArrowLeft, Edit, ExternalLink, Key } from 'lucide-react';
import type { UserProfile } from '@/types';

type ProfileField = {
  label: string;
  value: string | null;
  href?: string | null;
};

const emptyProfile: UserProfile = {
  givenName: null,
  familyName: null,
  middleName: null,
  nickname: null,
  preferredUsername: null,
  profile: null,
  picture: null,
  website: null,
  gender: null,
  birthdate: null,
  zoneInfo: null,
  locale: null,
};

function getProfileFields(profile: UserProfile): ProfileField[] {
  return [
    { label: 'Given Name', value: profile.givenName },
    { label: 'Family Name', value: profile.familyName },
    { label: 'Middle Name', value: profile.middleName },
    { label: 'Nickname', value: profile.nickname },
    { label: 'Preferred Username', value: profile.preferredUsername },
    { label: 'Gender', value: profile.gender },
    { label: 'Birthdate', value: profile.birthdate },
    { label: 'Time Zone', value: profile.zoneInfo },
    { label: 'Locale', value: profile.locale },
    { label: 'Profile URL', value: profile.profile, href: profile.profile },
    { label: 'Picture URL', value: profile.picture, href: profile.picture },
    { label: 'Website URL', value: profile.website, href: profile.website },
  ];
}

function ProfileValue({ field }: { field: ProfileField }) {
  if (!field.value) {
    return <span className="text-muted-foreground">Not provided</span>;
  }

  if (field.href) {
    return (
      <a
        className="inline-flex max-w-full items-center gap-1 truncate text-primary hover:underline"
        href={field.href}
        rel="noreferrer"
        target="_blank"
      >
        <span className="truncate">{field.value}</span>
        <ExternalLink className="h-3 w-3 shrink-0" />
      </a>
    );
  }

  return <span>{field.value}</span>;
}

export function UserDetail() {
  const { userId } = useParams<{ userId: string }>();
  const navigate = useNavigate();
  const { data: user, isLoading } = useUser(userId!);
  const [showResetPassword, setShowResetPassword] = useState(false);

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-12">
        <LoadingSpinner />
      </div>
    );
  }

  if (!user) {
    return (
      <div className="space-y-4">
        <h1 className="text-3xl font-bold">User Not Found</h1>
        <Button onClick={() => navigate('/users')}>
          <ArrowLeft className="h-4 w-4 mr-2" />
          Back to Users
        </Button>
      </div>
    );
  }

  const formatDate = (dateString: string | null) => {
    if (!dateString) return 'Never';
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
            onClick={() => navigate('/users')}
          >
            <ArrowLeft className="h-4 w-4" />
          </Button>
          <div>
            <h1 className="text-3xl font-bold">{user.displayName}</h1>
            <p className="text-muted-foreground">{user.email}</p>
          </div>
        </div>
        <div className="flex gap-2">
          <Button onClick={() => setShowResetPassword(true)} variant="outline">
            <Key className="h-4 w-4 mr-2" />
            Reset Password
          </Button>
          <Button onClick={() => navigate(`/users/${userId}/edit`)}>
            <Edit className="h-4 w-4 mr-2" />
            Edit User
          </Button>
        </div>
      </div>

      {/* User Information Card */}
      <Card>
        <CardHeader>
          <CardTitle>User Information</CardTitle>
          <CardDescription>Basic user account details</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid grid-cols-2 gap-4">
            <div>
              <div className="text-sm font-medium text-muted-foreground">Status</div>
              <div className="mt-1">
                <UserStatusBadge status={user.status} />
              </div>
            </div>
            <div>
              <div className="text-sm font-medium text-muted-foreground">MFA Enabled</div>
              <div className="mt-1">{user.mfaEnabled ? 'Yes' : 'No'}</div>
            </div>
            <div>
              <div className="text-sm font-medium text-muted-foreground">Last Login</div>
              <div className="mt-1">{formatDate(user.lastLoginAt)}</div>
            </div>
            <div>
              <div className="text-sm font-medium text-muted-foreground">Created</div>
              <div className="mt-1">{formatDate(user.createdAt)}</div>
            </div>
            <div>
              <div className="text-sm font-medium text-muted-foreground">Modified</div>
              <div className="mt-1">{formatDate(user.modifiedAt)}</div>
            </div>
            <div>
              <div className="text-sm font-medium text-muted-foreground">User ID</div>
              <div className="mt-1 text-xs font-mono">{user.id}</div>
            </div>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>OIDC Profile</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
            {getProfileFields(user.profile ?? emptyProfile).map((field) => (
              <div key={field.label} className="min-w-0">
                <div className="text-sm font-medium text-muted-foreground">{field.label}</div>
                <div className="mt-1 min-w-0 text-sm">
                  <ProfileValue field={field} />
                </div>
              </div>
            ))}
          </div>
        </CardContent>
      </Card>

      {/* Roles Card */}
      <Card>
        <CardHeader>
          <CardTitle>Roles & Permissions</CardTitle>
          <CardDescription>Roles assigned to this user</CardDescription>
        </CardHeader>
        <CardContent>
          <UserRolesList userId={userId!} />
        </CardContent>
      </Card>

      {/* Groups Card */}
      <Card>
        <CardHeader>
          <CardTitle>Group Membership</CardTitle>
          <CardDescription>Groups this user belongs to</CardDescription>
        </CardHeader>
        <CardContent>
          <UserGroupsList userId={userId!} />
        </CardContent>
      </Card>

      {/* Upstream Identities Card */}
      <Card>
        <CardHeader>
          <CardTitle>Linked Accounts</CardTitle>
          <CardDescription>External identity providers linked to this user</CardDescription>
        </CardHeader>
        <CardContent>
          <UpstreamIdentitiesList userId={userId!} />
        </CardContent>
      </Card>

      {/* Reset Password Dialog */}
      <ResetPasswordDialog
        userId={userId!}
        userEmail={user.email}
        open={showResetPassword}
        onOpenChange={setShowResetPassword}
        onSuccess={() => {
          // Show success message
          console.log('Password reset successfully');
        }}
      />
    </div>
  );
}
