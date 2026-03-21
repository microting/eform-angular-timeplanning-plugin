import { test, expect } from '@playwright/test';
import { LoginPage } from '../../../Login.page';
import { PluginPage } from '../../../Plugin.page';

test.describe('Enable Backend Config plugin', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('http://localhost:4200');
    await new LoginPage(page).login();
    await new PluginPage(page).Navbar.goToPluginsPage();
  });

  test('should enabled Time registration plugin', async ({ page }) => {
    const pluginName = 'Microting Time Planning Plugin';

    // Open action menu for the plugin
    await page.locator('.mat-mdc-row').filter({ hasText: pluginName }).first()
      .locator('#actionMenu').click();
    await page.waitForTimeout(500);

    // Click the status button inside the menu to enable the plugin
    await page.locator('#plugin-status-button0').scrollIntoViewIfNeeded();
    await expect(page.locator('#plugin-status-button0')).toBeVisible();
    await page.locator('#plugin-status-button0').click();
    await page.waitForTimeout(500);

    // Confirm activation in the modal if present
    if (await page.locator('#pluginOKBtn').count() > 0) {
      await page.locator('#pluginOKBtn').click();
      await page.waitForTimeout(100000); // Wait for plugin activation
    }

    await page.goto('http://localhost:4200');
    await new LoginPage(page).login();
    await new PluginPage(page).Navbar.goToPluginsPage();

    // Verify the plugin is enabled by checking the status
    // Re-open the action menu to check the status
    await page.locator('.mat-mdc-row').filter({ hasText: pluginName }).first()
      .locator('#actionMenu').click();
    await page.waitForTimeout(500);

    await expect(
      page.locator('#plugin-status-button0').locator('mat-icon')
    ).toContainText('toggle_on'); // plugin is enabled
  });
});
