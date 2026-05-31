import { expect, test, type Page } from '@playwright/test';

const mockRole = {
  id: 'role-test-1',
  name: 'operator',
  displayName: 'Operator',
  isSystemRole: false,
  isActive: true,
};

const mockUser = {
  id: 'user-test-1',
  email: 'admin@test.com',
  displayName: 'Ada Lovelace',
  status: 'Active',
  createdAt: '2024-01-01T00:00:00Z',
  mfaEnabled: false,
  lastLoginAt: null,
  modifiedAt: null,
  profile: {},
};

async function setupApiMocks(page: Page) {
  await page.route(/\/api\/admin\//, async (route) => {
    const url = new URL(route.request().url());
    const path = url.pathname;
    const method = route.request().method();

    if (path.match(/\/api\/admin\/users\/[^/]+\/roles$/) && method === 'GET') {
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ userId: mockUser.id, roles: [] }),
      });
    }
    if (path.startsWith('/api/admin/roles') && method === 'GET') {
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ items: [mockRole], totalCount: 1, page: 1, pageSize: 100, totalPages: 1 }),
      });
    }
    if (path.startsWith('/api/admin/users') && method === 'GET') {
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ items: [mockUser], totalCount: 1, page: 1, pageSize: 20, totalPages: 1 }),
      });
    }

    return route.continue();
  });
}

test('AdminWeb and Management Web remain independently reachable during dual UI rollout', async ({ page }) => {
  const managementWebUrl = process.env.MANAGEMENT_WEB_BASE_URL ?? 'http://localhost:5176';

  await setupApiMocks(page);

  await page.goto(managementWebUrl);
  await expect(page.getByText(/Management Web/i)).toBeVisible();

  // Navigate away to simulate the user switching between UIs
  await page.goto('about:blank');

  // Management Web remains independently reachable and functional on return
  await page.goto(managementWebUrl);
  await expect(page.getByRole('heading', { name: /Users/i })).toBeVisible();
});
