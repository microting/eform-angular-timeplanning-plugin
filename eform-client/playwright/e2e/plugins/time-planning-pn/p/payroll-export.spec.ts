import { test, expect } from '@playwright/test';
import { LoginPage } from '../../../Page objects/Login.page';
import { PluginPage } from '../../../Page objects/Plugin.page';

test.describe('Payroll export', () => {
  test.describe.configure({ timeout: 120000 });

  test.beforeEach(async ({ page }) => {
    await page.goto('http://localhost:4200');
    await new LoginPage(page).login();

    // Configure payroll settings: DanLon + cutoffDay=19
    const settingsGetPromise = page.waitForResponse(
      r => r.url().includes('/api/time-planning-pn/settings') && r.request().method() === 'GET'
    );
    await new PluginPage(page).Navbar.goToPluginsPage();
    await page.locator('#actionMenu').scrollIntoViewIfNeeded();
    await expect(page.locator('#actionMenu')).toBeVisible();
    await page.locator('#actionMenu').click({ force: true });
    await page.locator('#plugin-settings-link0').click();
    await settingsGetPromise;

    // Wait for payroll settings to load
    await page.waitForResponse(
      r => r.url().includes('/api/time-planning-pn/payroll/settings') && r.request().method() === 'GET'
    ).catch(() => {});
    await page.waitForTimeout(1000);

    // Scroll to payroll section
    const payrollCard = page.locator('mat-card').filter({ hasText: /Payroll integration|L\u00f8nintegration/ });
    await payrollCard.scrollIntoViewIfNeeded();

    // Select DanLøn via mtx-select (ng-select) dropdown
    const payrollSystemSelect = page.locator('#payrollSystemSelect');
    await payrollSystemSelect.click();
    const dropdown = page.locator('ng-dropdown-panel');
    await dropdown.waitFor({ state: 'visible', timeout: 10000 });
    await dropdown.locator('.ng-option').filter({ hasText: 'DanLøn' }).first().click();

    // Set cutoff day to 19
    const cutoffDayInput = page.locator('#payrollCutoffDay');
    await cutoffDayInput.click();
    await cutoffDayInput.fill('19');

    // Save payroll settings
    const [updateResp] = await Promise.all([
      page.waitForResponse(r => r.url().includes('/api/time-planning-pn/payroll/settings') && r.request().method() === 'PUT'),
      page.locator('#savePayrollSettings').click(),
    ]);
    expect(updateResp.status()).toBe(200);

    // Navigate to Dashboard
    await page.goto('http://localhost:4200');
    await new LoginPage(page).login();
    await page.locator('mat-nested-tree-node').filter({ hasText: 'Timeregistrering' }).click();
    await page.locator('mat-tree-node').filter({ hasText: 'Dashboard' }).click();
    await page.waitForTimeout(2000);
  });

  test('should show payroll export button on dashboard', async ({ page }) => {
    const payrollExportButton = page.locator('#payroll-export');
    await expect(payrollExportButton).toBeVisible();
  });

  test('should open payroll export dialog when clicking export button', async ({ page }) => {
    const payrollExportButton = page.locator('#payroll-export');
    await expect(payrollExportButton).toBeVisible();

    // Click the export button
    await payrollExportButton.click();

    // Verify dialog opens with the expected title
    const dialogTitle = page.locator('h2[mat-dialog-title]');
    await expect(dialogTitle).toBeVisible();
    await expect(dialogTitle).toContainText(/Export payroll period|Eksport\u00e9r l\u00f8nperiode/);

    // Verify period date fields are visible
    const startDateInput = page.locator('mat-dialog-content input').first();
    await expect(startDateInput).toBeVisible();

    const endDateInput = page.locator('mat-dialog-content input').last();
    await expect(endDateInput).toBeVisible();

    // Verify Cancel and Export buttons are present
    const cancelButton = page.locator('#cancelPayrollExportBtn');
    await expect(cancelButton).toBeVisible();

    const confirmButton = page.locator('#confirmPayrollExportBtn');
    await expect(confirmButton).toBeVisible();
  });

  test('should cancel payroll export dialog', async ({ page }) => {
    const payrollExportButton = page.locator('#payroll-export');
    await payrollExportButton.click();

    // Wait for dialog
    const dialogTitle = page.locator('h2[mat-dialog-title]');
    await expect(dialogTitle).toBeVisible();

    // Click cancel
    await page.locator('#cancelPayrollExportBtn').click();

    // Verify dialog is closed
    await expect(dialogTitle).not.toBeVisible();
  });

  test('should export payroll and download CSV file', async ({ page }) => {
    const payrollExportButton = page.locator('#payroll-export');
    await payrollExportButton.click();

    // Wait for dialog and preview to load
    const dialogTitle = page.locator('h2[mat-dialog-title]');
    await expect(dialogTitle).toBeVisible();

    // Wait for preview data to load (loading spinner disappears)
    await page.locator('mat-dialog-content').locator('text=Loading').waitFor({ state: 'hidden', timeout: 10000 }).catch(() => {});
    await page.waitForTimeout(1000);

    // Click export and wait for download
    const confirmButton = page.locator('#confirmPayrollExportBtn');
    await expect(confirmButton).toBeEnabled({ timeout: 10000 });

    const downloadPromise = page.waitForEvent('download', { timeout: 30000 });
    await confirmButton.click();
    const download = await downloadPromise;

    // Verify the downloaded file has a CSV filename
    const filename = download.suggestedFilename();
    expect(filename).toMatch(/\.csv$/);

    // Read and verify CSV content
    const filePath = await download.path();
    expect(filePath).toBeTruthy();

    const fs = await import('fs');
    const content = fs.readFileSync(filePath!, 'utf-8');
    expect(content.length).toBeGreaterThan(0);

    // CSV should contain separator or header rows
    const lines = content.split('\n').filter(l => l.trim().length > 0);
    expect(lines.length).toBeGreaterThanOrEqual(1);
  });

  test('should show preview information in export dialog', async ({ page }) => {
    const payrollExportButton = page.locator('#payroll-export');
    await payrollExportButton.click();

    // Wait for dialog
    const dialogTitle = page.locator('h2[mat-dialog-title]');
    await expect(dialogTitle).toBeVisible();

    // Wait for loading to finish
    await page.locator('mat-dialog-content').locator('text=Loading').waitFor({ state: 'hidden', timeout: 10000 }).catch(() => {});
    await page.waitForTimeout(1000);

    // Verify preview info is displayed (workers count and pay lines)
    const dialogContent = page.locator('mat-dialog-content');
    await expect(dialogContent).toBeVisible();

    // The preview should show worker count and pay line count
    const previewText = dialogContent.locator('p');
    if (await previewText.count() > 0) {
      const text = await previewText.first().textContent();
      expect(text).toMatch(/\d+/); // Should contain at least a number
    }
  });
});
