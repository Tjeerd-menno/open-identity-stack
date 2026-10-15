// Generated from endpoint RequireAuthorization for single-permission CRUD screens. Do not edit.
// Screens that combine several permissions stay explicit.
import {
  applicationPermissionsRead,
  applicationsRead,
  auditLogsRead,
  groupsRead,
  providersRead,
  rolesRead,
  sessionsRead,
  systemManageSettings,
  usersRead,
} from "./generated-permissions";

export const crudRoutePolicies = {
  applicationDetail: applicationsRead,
  applicationPermissionDetail: applicationPermissionsRead,
  applicationPermissions: applicationPermissionsRead,
  applications: applicationsRead,
  auditEntries: auditLogsRead,
  authenticationSettings: systemManageSettings,
  groupDetail: groupsRead,
  groups: groupsRead,
  providerDetail: providersRead,
  providers: providersRead,
  roleDetail: rolesRead,
  roles: rolesRead,
  sessionDetail: sessionsRead,
  sessions: sessionsRead,
  userDetail: usersRead,
  users: usersRead,
} as const;
