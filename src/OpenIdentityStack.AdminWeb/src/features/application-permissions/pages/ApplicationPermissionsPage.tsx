import { ApplicationPermissionDiagnosticsPanel, RegisteredApplicationList } from '../components';

export function ApplicationPermissionsPage() {
  return (
    <div className="container mx-auto space-y-6 py-6">
      <ApplicationPermissionDiagnosticsPanel />
      <RegisteredApplicationList />
    </div>
  );
}
