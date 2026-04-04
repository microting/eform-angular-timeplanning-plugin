import { test, expect } from '@playwright/test';
import { LoginPage } from '../../../Page objects/Login.page';

async function waitForSpinner(page: import('@playwright/test').Page) {
  if (await page.locator('.overlay-spinner').count() > 0) {
    await page.locator('.overlay-spinner').waitFor({ state: 'hidden', timeout: 30000 });
  }
}

test.describe('Dashboard edit values', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('http://localhost:4200');
    await new LoginPage(page).login();
    await page.locator('mat-nested-tree-node').filter({ hasText: 'Timeregistrering' }).click();

    const indexUpdatePromise = page.waitForResponse(
      r => r.url().includes('/api/time-planning-pn/plannings/index') && r.request().method() === 'POST'
    );

    await page.locator('mat-tree-node').filter({ hasText: 'Dashboard' }).click();
    await indexUpdatePromise;
    await waitForSpinner(page);

    await page.locator('#workingHoursSite').click();
    await page.locator('.ng-option').filter({ hasText: 'ac ad' }).click();
    await page.locator('#cell0_0').click();
    await page.waitForTimeout(500);
  });

  test('should set paid out flex value', async ({ page }) => {
    await page.locator('#paidOutFlex').clear();
    await page.locator('#paidOutFlex').fill('1.2');
  });

  test('should accepts decimal values with dot', async ({ page }) => {
    await expect(page.locator('#paidOutFlex')).toHaveValue('1.2');
    await page.locator('#paidOutFlex').clear();
    await page.locator('#paidOutFlex').fill('1,2');
  });

  test('should accepts decimal values with comma', async ({ page }) => {
    await expect(page.locator('#paidOutFlex')).toHaveValue('1.2');
    await page.locator('#paidOutFlex').clear();
    await page.locator('#paidOutFlex').fill('1,2');
  });

  test('should accepts whole numbers', async ({ page }) => {
    await expect(page.locator('#paidOutFlex')).toHaveValue('1.2');
    await page.locator('#paidOutFlex').clear();
    await page.locator('#paidOutFlex').fill('0');
  });

  test.afterEach(async ({ page }) => {
    const savePromise = page.waitForResponse(
      r => r.url().includes('/api/time-planning-pn/plannings/') && r.request().method() === 'PUT'
    );
    await page.locator('#saveButton').click();
    await savePromise;
    await page.waitForTimeout(1000);
  });
});
