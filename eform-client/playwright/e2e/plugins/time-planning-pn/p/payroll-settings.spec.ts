import { test, expect } from '@playwright/test';
import { LoginPage } from '../../../Page objects/Login.page';
import { PluginPage } from '../../../Page objects/Plugin.page';

test.describe('Payroll settings', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('http://localhost:4200');
    await new LoginPage(page).login();

    // Navigate to settings page
    const settingsGetPromise = page.waitForResponse(
      r => r.url().includes('/api/time-planning-pn/settings') && r.request().method() === 'GET'
    );
    await new PluginPage(page).Navbar.goToPluginsPage();
    await page.locator('#actionMenu').scrollIntoViewIfNeeded();
    await expect(page.locator('#actionMenu')).toBeVisible();
    await page.locator('#actionMenu').click({ force: true });
    await page.locator('#plugin-settings-link0').click();
    await settingsGetPromise;
  });

  test('should show payroll integration section on settings page', async ({ page }) => {
    // Scroll to the payroll integration card and verify it is visible
    const payrollCard = page.locator('mat-card').filter({ hasText: /Payroll integration|L\u00f8nintegration/ });
    await payrollCard.scrollIntoViewIfNeeded();
    await expect(payrollCard).toBeVisible();

    // Verify the payroll system select is present
    const payrollSystemSelect = page.locator('#payrollSystemSelect');
    await expect(payrollSystemSelect).toBeVisible();

    // Verify the cutoff day input is present
    const cutoffDayInput = page.locator('#payrollCutoffDay');
    await expect(cutoffDayInput).toBeVisible();

    // Verify the save button is present
    const saveButton = page.locator('#savePayrollSettings');
    await expect(saveButton).toBeVisible();
  });

  test('should save DanL\u00f8n selection and persist', async ({ page }) => {
    // Scroll to the payroll section
    const payrollCard = page.locator('mat-card').filter({ hasText: /Payroll integration|L\u00f8nintegration/ });
    await payrollCard.scrollIntoViewIfNeeded();

    // Open the payroll system dropdown and select DanL\u00f8n (value: 1)
    const payrollSystemSelect = page.locator('#payrollSystemSelect');
    await payrollSystemSelect.click();
    await page.locator('ngx-mat-select-search, .cdk-overlay-container').waitFor({ state: 'visible', timeout: 3000 }).catch(() => {});
    await page.locator('.cdk-overlay-container').locator('mat-option, ngx-dropdown-panel .ngx-option, mtx-option').filter({ hasText: 'DanL\u00f8n' }).click();

    // Save payroll settings
    const [updateResp] = await Promise.all([
      page.waitForResponse(r => r.url().includes('/api/time-planning-pn/payroll/settings') && r.request().method() === 'PUT'),
      page.locator('#savePayrollSettings').click(),
    ]);
    expect(updateResp.status()).toBe(200);

    // Reload page and navigate back to settings
    await page.goto('http://localhost:4200');
    await new LoginPage(page).login();
    await new PluginPage(page).Navbar.goToPluginsPage();

    const settingsGetPromise2 = page.waitForResponse(
      r => r.url().includes('/api/time-planning-pn/settings') && r.request().method() === 'GET'
    );
    await page.locator('#actionMenu').scrollIntoViewIfNeeded();
    await expect(page.locator('#actionMenu')).toBeVisible();
    await page.locator('#actionMenu').click({ force: true });
    await page.locator('#plugin-settings-link0').click();
    await settingsGetPromise2;

    // Wait for payroll settings to load
    await page.waitForResponse(
      r => r.url().includes('/api/time-planning-pn/payroll/settings') && r.request().method() === 'GET'
    ).catch(() => {});
    await page.waitForTimeout(1000);

    // Verify DanL\u00f8n is still selected
    const payrollCard2 = page.locator('mat-card').filter({ hasText: /Payroll integration|L\u00f8nintegration/ });
    await payrollCard2.scrollIntoViewIfNeeded();
    await expect(payrollCard2.locator('#payrollSystemSelect')).toContainText('DanL\u00f8n');
  });

  test('should save custom cutoff day and persist', async ({ page }) => {
    // Scroll to the payroll section
    const payrollCard = page.locator('mat-card').filter({ hasText: /Payroll integration|L\u00f8nintegration/ });
    await payrollCard.scrollIntoViewIfNeeded();

    // Change cutoff day to 15
    const cutoffDayInput = page.locator('#payrollCutoffDay');
    await cutoffDayInput.click();
    await cutoffDayInput.fill('15');

    // Save payroll settings
    const [updateResp] = await Promise.all([
      page.waitForResponse(r => r.url().includes('/api/time-planning-pn/payroll/settings') && r.request().method() === 'PUT'),
      page.locator('#savePayrollSettings').click(),
    ]);
    expect(updateResp.status()).toBe(200);

    // Reload page and navigate back to settings
    await page.goto('http://localhost:4200');
    await new LoginPage(page).login();
    await new PluginPage(page).Navbar.goToPluginsPage();

    const settingsGetPromise2 = page.waitForResponse(
      r => r.url().includes('/api/time-planning-pn/settings') && r.request().method() === 'GET'
    );
    await page.locator('#actionMenu').scrollIntoViewIfNeeded();
    await expect(page.locator('#actionMenu')).toBeVisible();
    await page.locator('#actionMenu').click({ force: true });
    await page.locator('#plugin-settings-link0').click();
    await settingsGetPromise2;

    // Wait for payroll settings to load
    await page.waitForResponse(
      r => r.url().includes('/api/time-planning-pn/payroll/settings') && r.request().method() === 'GET'
    ).catch(() => {});
    await page.waitForTimeout(1000);

    // Verify cutoff day is persisted as 15
    const payrollCard2 = page.locator('mat-card').filter({ hasText: /Payroll integration|L\u00f8nintegration/ });
    await payrollCard2.scrollIntoViewIfNeeded();
    await expect(page.locator('#payrollCutoffDay')).toHaveValue('15');
  });
});
