import { api } from './api';

export async function getAllRoles() {
  const firstPage = await api.roles.getRoles({ page: 1, pageSize: 100 });
  const roles = [...firstPage.items];
  for (let page = 2; page <= firstPage.totalPages; page++) {
    const response = await api.roles.getRoles({ page, pageSize: 100 });
    roles.push(...response.items);
  }
  return roles;
}
