import { test, expect } from '@playwright/test';
import { LoginPage } from '../../../Page objects/Login.page';
import { PluginPage } from '../../../Page objects/Plugin.page';

test.describe('Payroll settings', () => {
  test.describe.configure({ timeout: 120000 });

  test('should show payroll integration section and save DanLøn', async ({ page }) => {
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
    await page.waitForTimeout(2000);

    // Verify the payroll integration card is visible
    const payrollCard = page.locator('mat-card').filter({ hasText: /Payroll integration|Lønintegration/ });
    await payrollCard.scrollIntoViewIfNeeded();
    await expect(payrollCard).toBeVisible();
    await expect(page.locator('#payrollSystemSelect')).toBeVisible();
    await expect(page.locator('#payrollCutoffDay')).toBeVisible();
    await expect(page.locator('#savePayrollSettings')).toBeVisible();

    // Select DanLøn via mtx-select dropdown
    const payrollSystemSelect = page.locator('#payrollSystemSelect');
    await payrollSystemSelect.click();
    const dropdown = page.locator('ng-dropdown-panel');
    await dropdown.waitFor({ state: 'visible', timeout: 10000 });
    await dropdown.locator('.ng-option').filter({ hasText: 'DanLøn' }).first().click();

    // Set cutoff day to 15
    const cutoffDayInput = page.locator('#payrollCutoffDay');
    await cutoffDayInput.click();
    await cutoffDayInput.fill('15');

    // Save payroll settings
    const [updateResp] = await Promise.all([
      page.waitForResponse(r => r.url().includes('/api/time-planning-pn/payroll/settings') && r.request().method() === 'PUT'),
      page.locator('#savePayrollSettings').click(),
    ]);
    expect(updateResp.status()).toBe(200);
    await page.waitForTimeout(1000);

    // Reload and verify persistence
    await page.goto('http://localhost:4200');
    await new LoginPage(page).login();

    const settingsGetPromise2 = page.waitForResponse(
      r => r.url().includes('/api/time-planning-pn/settings') && r.request().method() === 'GET'
    );
    await new PluginPage(page).Navbar.goToPluginsPage();
    await page.locator('#actionMenu').scrollIntoViewIfNeeded();
    await expect(page.locator('#actionMenu')).toBeVisible();
    await page.locator('#actionMenu').click({ force: true });
    await page.locator('#plugin-settings-link0').click();
    await settingsGetPromise2;
    await page.waitForTimeout(2000);

    // Verify DanLøn is still selected and cutoff day is 15
    const payrollCard2 = page.locator('mat-card').filter({ hasText: /Payroll integration|Lønintegration/ });
    await payrollCard2.scrollIntoViewIfNeeded();
    await expect(payrollCard2.locator('#payrollSystemSelect')).toContainText('DanLøn');
    await expect(page.locator('#payrollCutoffDay')).toHaveValue('15');
  });
});
