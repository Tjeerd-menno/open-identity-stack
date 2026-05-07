import { useState, useEffect } from 'react';
import { Checkbox } from '@/components/ui/checkbox';
import { Label } from '@/components/ui/label';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';

interface PermissionSelectorProps {
  /**
   * Currently selected permissions
   */
  selectedPermissions: string[];
  
  /**
   * Callback when permissions change
   */
  onChange: (permissions: string[]) => void;
  
  /**
   * Whether the selector is disabled
   */
  disabled?: boolean;
}

/**
 * Permission selector component with grouped checkboxes
 * 
 * Groups permissions by resource (users, roles, groups, etc.)
 * and displays them in an organized grid layout.
 */
export function PermissionSelector({
  selectedPermissions,
  onChange,
  disabled = false,
}: PermissionSelectorProps) {
  const [selected, setSelected] = useState<Set<string>>(
    new Set(selectedPermissions)
  );

  // Sync with external changes
  useEffect(() => {
    setSelected(new Set(selectedPermissions));
  }, [selectedPermissions]);

  const handleToggle = (permission: string, checked: boolean) => {
    const newSelected = new Set(selected);
    
    if (checked) {
      newSelected.add(permission);
    } else {
      newSelected.delete(permission);
    }
    
    setSelected(newSelected);
    onChange(Array.from(newSelected));
  };

  // Group permissions by resource
  const permissionGroups = {
    Users: [
      { value: 'users:read', label: 'Read Users' },
      { value: 'users:create', label: 'Create Users' },
      { value: 'users:update', label: 'Update Users' },
      { value: 'users:delete', label: 'Delete Users' },
    ],
    Roles: [
      { value: 'roles:read', label: 'Read Roles' },
      { value: 'roles:create', label: 'Create Roles' },
      { value: 'roles:update', label: 'Update Roles' },
      { value: 'roles:delete', label: 'Delete Roles' },
    ],
    Groups: [
      { value: 'groups:read', label: 'Read Groups' },
      { value: 'groups:create', label: 'Create Groups' },
      { value: 'groups:update', label: 'Update Groups' },
      { value: 'groups:delete', label: 'Delete Groups' },
    ],
    'Service Accounts': [
      { value: 'service-accounts:read', label: 'Read Service Accounts' },
      { value: 'service-accounts:create', label: 'Create Service Accounts' },
      { value: 'service-accounts:update', label: 'Update Service Accounts' },
      { value: 'service-accounts:delete', label: 'Delete Service Accounts' },
    ],
    Sessions: [
      { value: 'sessions:read', label: 'Read Sessions' },
      { value: 'sessions:revoke', label: 'Revoke Sessions' },
    ],
    Providers: [
      { value: 'providers:read', label: 'Read Providers' },
      { value: 'providers:create', label: 'Create Providers' },
      { value: 'providers:update', label: 'Update Providers' },
      { value: 'providers:delete', label: 'Delete Providers' },
    ],
  };

  return (
    <div className="space-y-4" data-testid="permission-selector">
      {Object.entries(permissionGroups).map(([groupName, permissions]) => (
        <Card key={groupName} data-permission-group={groupName}>
          <CardHeader className="pb-3">
            <CardTitle className="text-sm font-medium">{groupName}</CardTitle>
            <CardDescription className="text-xs">
              Manage {groupName.toLowerCase()} permissions
            </CardDescription>
          </CardHeader>
          <CardContent>
            <div className="grid grid-cols-2 gap-4">
              {permissions.map((permission) => (
                <div key={permission.value} className="flex items-center space-x-2">
                  <Checkbox
                    id={permission.value}
                    checked={selected.has(permission.value)}
                    onCheckedChange={(checked) =>
                      handleToggle(permission.value, checked as boolean)
                    }
                    disabled={disabled}
                    aria-label={permission.label}
                  />
                  <Label
                    htmlFor={permission.value}
                    className="text-sm font-normal cursor-pointer"
                  >
                    {permission.label}
                  </Label>
                </div>
              ))}
            </div>
          </CardContent>
        </Card>
      ))}
    </div>
  );
}
