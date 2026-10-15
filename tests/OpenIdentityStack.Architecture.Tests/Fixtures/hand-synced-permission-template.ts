// Scanned by ManagementWeb_ImportsGeneratedPermissionConstants.
// A template literal is a hand-synced permission, the same as a quoted one.
export function templateLiteralReadsUsers(
  permissions: string[],
  hasPermission: (granted: string[], required: string) => boolean,
): boolean {
  return hasPermission(permissions, `users:read`);
}
