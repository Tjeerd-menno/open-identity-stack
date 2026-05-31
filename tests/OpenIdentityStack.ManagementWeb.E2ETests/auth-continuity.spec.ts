import { expect, test } from '@playwright/test';

test('AdminWeb and Management Web remain independently reachable during dual UI rollout', async ({ page }) => {
  const adminWebUrl = process.env.ADMIN_WEB_BASE_URL ?? 'http://localhost:5175';
  const managementWebUrl = process.env.MANAGEMENT_WEB_BASE_URL ?? 'http://localhost:5176';

  await page.goto(managementWebUrl);
  await expect(page.getByText(/Management Web/i)).toBeVisible();

  await page.goto(adminWebUrl);
  await expect(page).toHaveURL(/localhost:5175|admin/i);

  await page.goto(managementWebUrl);
  await expect(page.getByRole('heading', { name: /Users/i })).toBeVisible();
});
