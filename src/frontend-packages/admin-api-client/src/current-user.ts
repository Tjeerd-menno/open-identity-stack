import type { AdminApiClient } from './index';

export type CurrentUserResponse = {
  subject: string;
  userName: string;
  displayName: string;
  email: string | null;
  permissions: string[];
};

export type CurrentUserContract = {
  getCurrentUser: () => Promise<CurrentUserResponse>;
};

export function createCurrentUserContract(client: AdminApiClient): CurrentUserContract {
  return {
    getCurrentUser: () => client.get<CurrentUserResponse>('/api/me'),
  };
}
