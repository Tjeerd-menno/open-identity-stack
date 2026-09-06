import type { AdminApiClient } from './index';

export type AdministrativeAccess = {
  approved: boolean;
  delegatedPermissions: string[];
  applicationPermissions: string[];
  revision: number | null;
};

export type AdministrativeAccessConfiguration = {
  delegatedPermissions: string[];
  applicationPermissions: string[];
  expectedRevision: number | null;
  acknowledgeAdministrativeAccess?: boolean;
};

export function createAdministrativeAccessContract(client: AdminApiClient) {
  return {
    get: (applicationId: string) => client.get<AdministrativeAccess>(`/api/admin/applications/${applicationId}/administrative-access`),
    save: (applicationId: string, data: AdministrativeAccessConfiguration) =>
      client.put<AdministrativeAccess>(`/api/admin/applications/${applicationId}/administrative-access`, data),
  };
}
