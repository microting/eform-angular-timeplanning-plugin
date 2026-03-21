import { test, expect } from '@playwright/test';
import { LoginPage } from '../../../Login.page';
import { PluginPage } from '../../../Plugin.page';

test.describe('Time Planning - Leave policies', () => {
  const testSiteSearchText = 'c d';

  const leavePolicies: { labelInFlags: string; expectedTooltip: string }[] = [
    { labelInFlags: 'Fridag',              expectedTooltip: 'Fridag' },
    { labelInFlags: 'Ferie',               expectedTooltip: 'Ferie' },
    { labelInFlags: 'Syg',                 expectedTooltip: 'Syg' },
    { labelInFlags: 'Kursus',              expectedTooltip: 'Kursus' },
    { labelInFlags: 'Orlov',               expectedTooltip: 'Orlov' },
    { labelInFlags: 'Barns 1. sygedag',    expectedTooltip: 'Barns 1. sygedag' },
    { labelInFlags: 'Barns 2. sygedag',    expectedTooltip: 'Barns 2. sygedag' },
    { labelInFlags: 'Ferie fridag',        expectedTooltip: 'Ferie fridag' },
    { labelInFlags: 'Helligdag',           expectedTooltip: 'Helligdag' },
    { labelInFlags: 'Afspadsering',        expectedTooltip: 'Afspadsering' },
    { labelInFlags: 'Barselsorlov',        expectedTooltip: 'Barselsorlov' },
  ];

  test.beforeEach(async ({ page }) => {
    await page.goto('http://localhost:4200');
    await new LoginPage(page).login();

    const pluginPage = new PluginPage(page);
    await pluginPage.Navbar.goToPluginsPage();
    await page.locator('#actionMenu')
      .scrollIntoViewIfNeeded();
    await expect(page.locator('#actionMenu')).toBeVisible();
    await page.locator('#actionMenu').click({ force: true });

    const planningsIndexResponse = page.waitForResponse(
      (resp) => resp.url().includes('/api/time-planning-pn/plannings/index') && resp.status() === 200
    );
    await page.locator('#plugin-settings-link0').click({ force: true });

    await page.locator('mat-nested-tree-node').filter({ hasText: 'Timeregistrering' }).click();
    await page.locator('mat-tree-node').filter({ hasText: 'Dashboard' }).click();
    await planningsIndexResponse;
    await expect(page.locator('.overlay-spinner')).not.toBeVisible({ timeout: 30000 });

    await page.locator('mat-toolbar > div > button .mat-mdc-button-persistent-ripple')
      .first()
      .locator('..')
      .click();

    await page.locator('#workingHoursSite').locator('input').clear();
    await page.locator('#workingHoursSite').locator('input').fill(testSiteSearchText);

    const planningsIndexResponse2 = page.waitForResponse(
      (resp) => resp.url().includes('/api/time-planning-pn/plannings/index') && resp.status() === 200
    );
    await page.locator('.ng-option.ng-option-marked').click();
    await planningsIndexResponse2;
    await expect(page.locator('.overlay-spinner')).not.toBeVisible({ timeout: 30000 });
  });

  test('should set and persist all leave policies in dashboard planning table', async ({ page }) => {
    const dayCellSelector = '#cell0_0';

    for (const { labelInFlags, expectedTooltip } of leavePolicies) {
      await page.locator(dayCellSelector).click({ force: true });

      await page.locator('#flags').scrollIntoViewIfNeeded();

      // Uncheck all checked checkboxes
      const checkboxes = page.locator('#flags mat-checkbox');
      const count = await checkboxes.count();
      for (let i = 0; i < count; i++) {
        const checkbox = checkboxes.nth(i);
        const input = checkbox.locator('input[type="checkbox"]');
        if (await input.isChecked()) {
          await checkbox.click({ force: true });
        }
      }

      // Click the target leave policy checkbox
      await page.locator('#flags mat-checkbox')
        .filter({ has: page.getByText(labelInFlags, { exact: true }) })
        .locator('.mdc-label')
        .scrollIntoViewIfNeeded();
      await page.locator('#flags mat-checkbox')
        .filter({ has: page.getByText(labelInFlags, { exact: true }) })
        .locator('.mdc-label')
        .click({ force: true });

      // Save and wait for responses
      const [saveResponse, indexResponse] = await Promise.all([
        page.waitForResponse(
          (resp) => resp.url().includes('/api/time-planning-pn/plannings/') && resp.request().method() === 'PUT' && resp.status() === 200,
          { timeout: 60000 }
        ),
        page.waitForResponse(
          (resp) => resp.url().includes('/api/time-planning-pn/plannings/index') && resp.status() === 200,
          { timeout: 60000 }
        ),
        (async () => {
          await page.locator('#saveButton').scrollIntoViewIfNeeded();
          await page.locator('#saveButton').click({ force: true });
        })(),
      ]);

      await expect(page.locator('.overlay-spinner')).not.toBeVisible({ timeout: 30000 });
      await page.waitForTimeout(1000);

      // Hover over the icon to trigger tooltip
      const dayCell = page.locator(dayCellSelector);
      await dayCell.scrollIntoViewIfNeeded();
      const tooltipIcon = dayCell.locator('.plan-icons mat-icon.mat-mdc-tooltip-trigger').first();
      await tooltipIcon.scrollIntoViewIfNeeded();
      await tooltipIcon.hover();
      await page.waitForTimeout(500);

      // Verify tooltip text - use the actual visible Material tooltip surface, not the hidden aria-describedby element
      const tooltip = page.locator('.cdk-overlay-container .mat-mdc-tooltip-surface').filter({ hasText: expectedTooltip });
      await expect(tooltip).toBeVisible({ timeout: 10000 });

      await page.waitForTimeout(1000);
    }
  });
});
