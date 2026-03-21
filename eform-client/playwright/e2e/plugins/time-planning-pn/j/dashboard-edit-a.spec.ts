import { test, expect } from '@playwright/test';
import { LoginPage } from '../../../Login.page';
import { PluginPage } from '../../../Plugin.page';

async function waitForSpinner(page: import('@playwright/test').Page) {
  if (await page.locator('.overlay-spinner').count() > 0) {
    await page.locator('.overlay-spinner').waitFor({ state: 'hidden', timeout: 30000 });
  }
}

test.describe('Dashboard edit values', () => {
  let storedValues: Record<string, string> = {};

  test.beforeEach(async ({ page }) => {
    await page.goto('http://localhost:4200');
    await new LoginPage(page).login();

    await new PluginPage(page).Navbar.goToPluginsPage();
    await page.locator('#actionMenu').scrollIntoViewIfNeeded();
    await expect(page.locator('#actionMenu')).toBeVisible();
    await page.locator('#actionMenu').click({ force: true });

    const settingsGetPromise = page.waitForResponse(
      r => r.url().includes('/api/time-planning-pn/settings') && r.request().method() === 'GET'
    );
    await page.locator('#plugin-settings-link0').click();
    await settingsGetPromise;

    // Check autoBreakCalculationActiveToggle state and enable if needed
    const toggleBtn = page.locator('#autoBreakCalculationActiveToggle button[role="switch"]');
    const isChecked = await toggleBtn.getAttribute('aria-checked');
    if (isChecked !== 'true') {
      await toggleBtn.scrollIntoViewIfNeeded();
      await toggleBtn.click({ force: true });
    }

    // Confirm it's ON
    await expect(toggleBtn).toHaveAttribute('aria-checked', 'true');

    await page.locator('#saveSettings').scrollIntoViewIfNeeded();
    await expect(page.locator('#saveSettings')).toBeVisible();
    await page.locator('#saveSettings').click({ force: true });
  });

  test('should validate the values from global gets set', async ({ page }) => {
    await page.locator('mat-nested-tree-node').filter({ hasText: 'Timeregistrering' }).click();

    const indexUpdatePromise = page.waitForResponse(
      r => r.url().includes('/api/time-planning-pn/plannings/index') && r.request().method() === 'POST'
    );

    await page.locator('mat-tree-node').filter({ hasText: 'Dashboard' }).click();
    await indexUpdatePromise;
    await waitForSpinner(page);

    const indexUpdatePromise2 = page.waitForResponse(
      r => r.url().includes('/api/time-planning-pn/plannings/index') && r.request().method() === 'POST'
    );
    await page.locator('#workingHoursSite').click();
    await page.locator('.ng-option').filter({ hasText: 'ac ad' }).click();
    await indexUpdatePromise2;
    await waitForSpinner(page);

    await page.locator('#firstColumn0').click();

    // Wait for dialog to appear
    await page.locator('mat-dialog-container').scrollIntoViewIfNeeded();
    await expect(page.locator('mat-dialog-container')).toBeVisible({ timeout: 10000 });

    // Ensure the checkbox is active
    const checkbox = page.locator('#autoBreakCalculationActive-input');
    await checkbox.scrollIntoViewIfNeeded();
    const isChecked = await checkbox.isChecked();
    if (!isChecked) {
      await checkbox.click({ force: true });
    }
    await expect(checkbox).toBeChecked();

    // Click the "Auto break calculation settings" tab
    await page.locator('.mat-mdc-tab').filter({ hasText: 'Auto break calculation settings' })
      .scrollIntoViewIfNeeded();
    await expect(
      page.locator('.mat-mdc-tab').filter({ hasText: 'Auto break calculation settings' })
    ).toBeVisible();
    await page.locator('.mat-mdc-tab').filter({ hasText: 'Auto break calculation settings' })
      .click({ force: true });
    await page.waitForTimeout(2000);

    // Click all refresh buttons (Monday-Sunday)
    const loadDefaultsBtns = page.locator('button[id$="LoadDefaults"]');
    await expect(loadDefaultsBtns.first()).toBeVisible({ timeout: 10000 });
    const btnCount = await loadDefaultsBtns.count();
    expect(btnCount).toBeGreaterThanOrEqual(1);
    for (let i = 0; i < btnCount; i++) {
      await loadDefaultsBtns.nth(i).scrollIntoViewIfNeeded();
      await expect(loadDefaultsBtns.nth(i)).toBeVisible();
      await loadDefaultsBtns.nth(i).click({ force: true });
      await page.waitForTimeout(500);
    }

    // Capture all current input values
    const activeTabBody = page.locator('mat-dialog-container mat-tab-body[aria-hidden="false"]');
    await activeTabBody.scrollIntoViewIfNeeded();
    await expect(activeTabBody).toBeVisible({ timeout: 10000 });

    const readonlyInputs = activeTabBody.locator('input[readonly="true"]');
    await expect(readonlyInputs).toHaveCount(await readonlyInputs.count());
    const inputCount = await readonlyInputs.count();
    expect(inputCount).toBeGreaterThanOrEqual(3);

    storedValues = {};
    for (let i = 0; i < inputCount; i++) {
      const id = await readonlyInputs.nth(i).getAttribute('id') || '';
      const val = await readonlyInputs.nth(i).inputValue();
      storedValues[id] = val;
    }

    const assignSitePromise = page.waitForResponse(
      r => r.url().includes('/api/time-planning-pn/settings/assigned-site') && r.request().method() === 'PUT'
    );
    await page.locator('#saveButton').scrollIntoViewIfNeeded();
    await page.locator('#saveButton').click({ force: true });
    await assignSitePromise;

    const indexUpdatePromise3 = page.waitForResponse(
      r => r.url().includes('/api/time-planning-pn/plannings/index') && r.request().method() === 'POST'
    );
    await indexUpdatePromise3;
    await waitForSpinner(page);

    // Verify dialog is closed
    await expect(page.locator('mat-dialog-container')).toHaveCount(0, { timeout: 5000 });
    await page.waitForTimeout(500);

    await expect(page.locator('mat-dialog-container')).toHaveCount(0, { timeout: 5000 });
    await page.waitForTimeout(500);

    // Reopen dialog
    await page.locator('#firstColumn0').click();

    await expect(page.locator('mat-dialog-container')).toBeVisible({ timeout: 5000 });

    // Click the "Auto break calculation settings" tab again
    await page.locator('.mat-mdc-tab').filter({ hasText: 'Auto break calculation settings' })
      .scrollIntoViewIfNeeded();
    await expect(
      page.locator('.mat-mdc-tab').filter({ hasText: 'Auto break calculation settings' })
    ).toBeVisible();
    await page.locator('.mat-mdc-tab').filter({ hasText: 'Auto break calculation settings' })
      .click({ force: true });

    // Verify tab content is visible
    await expect(page.locator('#mat-tab-group-1-content-1')).toBeVisible();
    await expect(page.locator('#mat-tab-group-1-content-1')).not.toHaveAttribute('aria-hidden', 'true');

    // Verify all input values match the stored ones
    const verifyInputs = page.locator('#mat-tab-group-1-content-1 input[readonly="true"]');
    const verifyCount = await verifyInputs.count();
    for (let i = 0; i < verifyCount; i++) {
      const id = await verifyInputs.nth(i).getAttribute('id') || '';
      const val = await verifyInputs.nth(i).inputValue();
      expect(val).toBe(storedValues[id]);
    }

    await page.locator('#saveButton').scrollIntoViewIfNeeded();
    await page.locator('#saveButton').click({ force: true });
    await expect(page.locator('mat-dialog-container')).toHaveCount(0, { timeout: 5000 });
    await page.waitForTimeout(500);
  });
});
