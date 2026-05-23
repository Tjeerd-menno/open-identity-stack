import { useState } from 'react';
import { Checkbox } from '@/components/ui/checkbox';
import { Label } from '@/components/ui/label';
import { useAssignablePermissionCatalog } from '@/features/application-permissions/hooks';
import { cn } from '@/lib/utils';

type PermissionOption = {
  value: string;
  label: string;
  description?: string | null;
  category?: string | null;
};

type PermissionTab = {
  id: string;
  label: string;
  groups: Record<string, PermissionOption[]>;
};

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
  const { data: catalog } = useAssignablePermissionCatalog({ page: 1, pageSize: 100 });
  const [activeTab, setActiveTab] = useState('built-in');
  const selected = new Set(selectedPermissions);

  const handleToggle = (permission: string, checked: boolean) => {
    const newSelected = new Set(selectedPermissions);
    
    if (checked) {
      newSelected.add(permission);
    } else {
      newSelected.delete(permission);
    }
    onChange(Array.from(newSelected));
  };

  const builtInGroups: Record<string, PermissionOption[]> = {
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

  const applicationTabs = new Map<string, PermissionTab>();
  for (const permission of catalog?.items ?? []) {
    const applicationId = permission.applicationId ?? 'registered-applications';
    const applicationName = permission.applicationName ?? 'Registered Applications';
    const category = permission.category ?? 'Permissions';
    const existing = applicationTabs.get(applicationId) ?? {
      id: `application-${applicationId}`,
      label: applicationName,
      groups: {},
    };
    existing.groups[category] ??= [];
    existing.groups[category].push({
      value: permission.fullPermissionKey,
      label: permission.displayName,
      description: permission.description,
      category,
    });
    applicationTabs.set(applicationId, existing);
  }

  const tabs: PermissionTab[] = [
    { id: 'built-in', label: 'Built-in', groups: builtInGroups },
    ...Array.from(applicationTabs.values()).sort((left, right) => left.label.localeCompare(right.label)),
  ];
  const currentTab = tabs.find((tab) => tab.id === activeTab) ?? tabs[0];

  return (
    <div className="space-y-4" data-testid="permission-selector">
      <div className="flex flex-wrap gap-2 border-b" role="tablist" aria-label="Permission applications">
        {tabs.map((tab) => (
          <button
            key={tab.id}
            type="button"
            role="tab"
            aria-selected={currentTab.id === tab.id}
            aria-controls={`permission-panel-${tab.id}`}
            id={`permission-tab-${tab.id}`}
            className={cn(
              'border-b-2 px-3 py-2 text-sm font-medium transition-colors',
              currentTab.id === tab.id
                ? 'border-primary text-foreground'
                : 'border-transparent text-muted-foreground hover:text-foreground'
            )}
            onClick={() => setActiveTab(tab.id)}
          >
            {tab.label}
          </button>
        ))}
      </div>

      <div
        role="tabpanel"
        id={`permission-panel-${currentTab.id}`}
        aria-labelledby={`permission-tab-${currentTab.id}`}
        className="space-y-5"
      >
        {Object.entries(currentTab.groups).map(([groupName, permissions]) => (
          <section key={groupName} data-permission-group={groupName} className="space-y-3">
            <div>
              <h3 className="text-sm font-medium">{groupName}</h3>
            </div>
            <div className="grid gap-3 md:grid-cols-2">
              {permissions.map((permission) => {
                const descriptionId = permission.description ? `${permission.value}-description` : undefined;
                return (
                  <div key={permission.value} className="flex items-start gap-2 rounded-md border p-3">
                    <Checkbox
                      id={permission.value}
                      checked={selected.has(permission.value)}
                      onCheckedChange={(checked) =>
                        handleToggle(permission.value, checked as boolean)
                      }
                      disabled={disabled}
                      aria-label={permission.label}
                      aria-describedby={descriptionId}
                    />
                    <div className="min-w-0 space-y-1">
                      <Label
                        htmlFor={permission.value}
                        className="cursor-pointer break-words text-sm font-normal"
                      >
                        {permission.label}
                      </Label>
                      {permission.description && (
                        <p id={descriptionId} className="text-xs text-muted-foreground">
                          {permission.description}
                        </p>
                      )}
                    </div>
                  </div>
                );
              })}
            </div>
          </section>
        ))}
      </div>
    </div>
  );
}
