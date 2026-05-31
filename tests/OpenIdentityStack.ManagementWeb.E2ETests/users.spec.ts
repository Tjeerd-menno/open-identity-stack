import { expect, test, type Page } from '@playwright/test';

const TEST_USER_ID = 'user-test-1';
const TEST_ROLE_ID = 'role-test-1';

const mockUser = {
  id: TEST_USER_ID,
  email: 'admin@test.com',
  displayName: 'Ada Lovelace',
  status: 'Active',
  createdAt: '2024-01-01T00:00:00Z',
  mfaEnabled: false,
  lastLoginAt: null,
  modifiedAt: null,
  profile: {},
};

const mockRole = {
  id: TEST_ROLE_ID,
  name: 'operator',
  displayName: 'Operator',
  isSystemRole: false,
  isActive: true,
};

async function setupApiMocks(page: Page) {
  await page.route(/\/api\/admin\//, async (route) => {
    const url = new URL(route.request().url());
    const path = url.pathname;
    const method = route.request().method();

    if (path.match(/\/api\/admin\/users\/[^/]+\/roles\/[^/]+/) && method === 'POST') {
      return route.fulfill({ status: 204 });
    }
    if (path.match(/\/api\/admin\/users\/[^/]+\/roles$/) && method === 'GET') {
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ userId: TEST_USER_ID, roles: [] }),
      });
    }
    if (path.match(/\/api\/admin\/users\/[^/]+\/disable$/) && method === 'POST') {
      return route.fulfill({ status: 204 });
    }
    if (path.match(/\/api\/admin\/users\/[^/]+$/) && method === 'PUT') {
      return route.fulfill({ status: 204 });
    }
    if (path.match(/\/api\/admin\/users\/[^/]+$/) && method === 'GET') {
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(mockUser),
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

test('operator can complete the Management Web users workflow', async ({ page }) => {
  await setupApiMocks(page);
  await page.goto('/users');

  await expect(page.getByRole('heading', { name: 'Users' })).toBeVisible();
  await expect(page.getByRole('navigation', { name: 'Management navigation' })).toBeVisible();
  await expect(page.getByText(/Operate user accounts/i)).toBeVisible();

  await page.getByRole('button', { name: /Ada Lovelace/i }).first().click();
  await expect(page.getByRole('region', { name: /User details/i })).toBeVisible();
  await page.getByLabel(/Display name/i).fill('Updated operator');
  await page.getByRole('button', { name: /Save changes/i }).click();
  await page.getByRole('button', { name: /Disable user/i }).click();
  await page.getByLabel(/Assign role/i).selectOption({ index: 1 });
  await page.getByRole('button', { name: /Assign selected role/i }).click();
});
