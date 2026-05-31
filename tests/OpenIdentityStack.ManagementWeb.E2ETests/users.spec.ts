import { expect, test } from '@playwright/test';

test('operator can complete the Management Web users workflow', async ({ page }) => {
  await page.goto('/users');

  await expect(page.getByRole('heading', { name: 'Users' })).toBeVisible();
  await expect(page.getByRole('navigation', { name: 'Management navigation' })).toBeVisible();
  await expect(page.getByText(/Operate user accounts/i)).toBeVisible();

  await page.getByRole('button', { name: /Ada Lovelace|admin/i }).first().click();
  await expect(page.getByRole('region', { name: /User details/i })).toBeVisible();
  await page.getByLabel(/Display name/i).fill('Updated operator');
  await page.getByRole('button', { name: /Save changes/i }).click();
  await page.getByRole('button', { name: /Disable user/i }).click();
  await page.getByLabel(/Assign role/i).selectOption({ index: 1 });
  await page.getByRole('button', { name: /Assign selected role/i }).click();
});
