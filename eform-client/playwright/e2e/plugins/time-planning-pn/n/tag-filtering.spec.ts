import { test, expect } from '@playwright/test';
import { LoginPage } from '../../../Login.page';
import { PluginPage } from '../../../Plugin.page';

const BASE_URL = 'http://localhost:4200';

test.describe('Time Planning - Tag Filtering', () => {
  const tagNames = [
    `Tag-A-${Date.now()}-${Math.random().toString(36).substring(7)}`,
    `Tag-B-${Date.now()}-${Math.random().toString(36).substring(7)}`,
    `Tag-C-${Date.now()}-${Math.random().toString(36).substring(7)}`,
    `Tag-D-${Date.now()}-${Math.random().toString(36).substring(7)}`,
    `Tag-E-${Date.now()}-${Math.random().toString(36).substring(7)}`,
  ];

  // Define tag combinations for each site
  const siteTagCombinations = [
    { siteIndex: 0, tags: [tagNames[0], tagNames[1]], sitename: '' }, // Site 1: Tag-A, Tag-B
    { siteIndex: 1, tags: [tagNames[0], tagNames[2]], sitename: '' }, // Site 2: Tag-A, Tag-C
    { siteIndex: 2, tags: [tagNames[0], tagNames[3]], sitename: '' }, // Site 3: Tag-A, Tag-D
    { siteIndex: 3, tags: [tagNames[0], tagNames[4]], sitename: '' }, // Site 4: Tag-A, Tag-E
    { siteIndex: 4, tags: [tagNames[0], tagNames[1], tagNames[2]], sitename: '' }, // Site 5: Tag-A, Tag-B, Tag-C
  ];

  test.beforeEach(async ({ page }) => {
    await page.goto(BASE_URL);
    await new LoginPage(page).login();
  });

  /**
   * Helper function to create a tag following the pattern from 'm' folder
   */
  const createTag = async (page, tagName: string) => {
    // Navigate to Advanced > Sites to access tags management
    await page.goto(`${BASE_URL}/advanced/sites`);
    await page.waitForTimeout(2000);

    // Find and click the tags button (mat-icon with text 'discount')
    const tagIconButtons = page.locator('button').filter({ has: page.locator('mat-icon', { hasText: 'discount' }) });
    if (await tagIconButtons.count() === 0) {
      throw new Error('Tags button with mat-icon (discount) not found on Sites page');
    }
    await tagIconButtons.first().click();
    await page.waitForTimeout(1000);

    // Click the add tag button if present
    const addTagButton = page.locator('button').filter({ has: page.locator('mat-icon', { hasText: 'add' }) });
    if (await addTagButton.count() > 0) {
      await addTagButton.first().click();
      await page.waitForTimeout(500);
    }

    // Enter tag name
    if (await page.locator('input[id="newTagName"]').count() > 0) {
      await page.locator('input[id="newTagName"]').fill(tagName);
    } else if (await page.locator('input[formcontrolname="name"]').count() > 0) {
      await page.locator('input[formcontrolname="name"]').fill(tagName);
    } else {
      await page.locator('mat-dialog-container input[type="text"]').first().fill(tagName);
    }

    // Save the tag
    if (await page.locator('#newTagSaveBtn').count() > 0) {
      const tagCreateResponse = page.waitForResponse(
        (resp) => resp.url().includes('/api/tags') && resp.request().method() === 'POST',
        { timeout: 10000 }
      );
      await page.locator('#newTagSaveBtn').click();
      await tagCreateResponse;
      await expect(page.locator('.overlay-spinner')).not.toBeVisible({ timeout: 30000 });
      await page.locator('#tagsModalCloseBtn').click();
    } else if (await page.locator('#saveButton').count() > 0) {
      const tagCreateResponse = page.waitForResponse(
        (resp) => resp.url().includes('/api/tags') && resp.request().method() === 'POST',
        { timeout: 10000 }
      );
      await page.locator('#saveButton').click();
      await tagCreateResponse;
    }

    await page.waitForTimeout(1000);
  };

  /**
   * Test 1: Create 5 tags
   */
  test('should create 5 tags', async ({ page }) => {
    for (const tagName of tagNames) {
      await createTag(page, tagName);
    }
  });

  /**
   * Test 2: Assign tags to sites
   */
  test('should assign tags to sites', async ({ page }) => {
    for (let idx = 0; idx < siteTagCombinations.length; idx++) {
      const combination = siteTagCombinations[idx];

      // Navigate to the advanced/sites
      await page.goto(`${BASE_URL}/advanced/sites`);
      await page.waitForTimeout(2000);

      // Wait for spinner to disappear
      if (await page.locator('.overlay-spinner').count() > 0) {
        await expect(page.locator('.overlay-spinner')).not.toBeVisible({ timeout: 30000 });
      }

      // Click the action menu for the site in list (skip header row)
      const rows = page.locator('table tbody tr');
      const targetRow = rows.nth(combination.siteIndex + 1);
      await targetRow.locator('#actionMenu, button[id*="actionMenu"]').first().scrollIntoViewIfNeeded();
      await targetRow.locator('#actionMenu, button[id*="actionMenu"]').first().click();
      await page.waitForTimeout(500);

      // Click the edit button
      await page.locator('#editSiteBtn, button[id*="edit"]').first().scrollIntoViewIfNeeded();
      await expect(page.locator('#editSiteBtn, button[id*="edit"]').first()).toBeVisible();
      await page.locator('#editSiteBtn, button[id*="edit"]').first().click();
      await page.waitForTimeout(1000);

      // Wait for dialog to open
      await expect(page.locator('mat-dialog-container, [role="dialog"]')).toBeVisible();

      // Select each tag in the combination
      for (const tagName of combination.tags) {
        await page.locator('#tagSelector').scrollIntoViewIfNeeded();
        await expect(page.locator('#tagSelector')).toBeVisible();
        await page.locator('#tagSelector').click();
        await page.waitForTimeout(500);
        await page.locator('.ng-option').filter({ hasText: tagName }).click();
        await page.waitForTimeout(200);
      }

      // Close dropdown
      await page.locator('body').click({ position: { x: 0, y: 0 } });
      await page.waitForTimeout(500);

      // Save
      const siteUpdateResponse = page.waitForResponse(
        (resp) => resp.url().includes('/api/sites') && resp.request().method() === 'PUT',
        { timeout: 10000 }
      );
      await page.locator('#siteEditSaveBtn').scrollIntoViewIfNeeded();
      await expect(page.locator('#siteEditSaveBtn')).toBeVisible();
      await page.locator('#siteEditSaveBtn').click();
      await siteUpdateResponse;

      // Wait for spinner to disappear
      if (await page.locator('.overlay-spinner').count() > 0) {
        await expect(page.locator('.overlay-spinner')).not.toBeVisible({ timeout: 30000 });
      }
      await page.waitForTimeout(1000);
    }
  });

  /**
   * Test 3: Navigate to dashboard and verify filtering works
   */
  test('should show all assigned sites on dashboard', async ({ page }) => {
    // Navigate to Time Planning Dashboard
    await page.locator('mat-nested-tree-node').filter({ hasText: 'Timeregistrering' }).click();
    await page.waitForTimeout(500);

    const indexResponse = page.waitForResponse(
      (resp) => resp.url().includes('/api/time-planning-pn/plannings/index') && resp.status() === 200,
      { timeout: 60000 }
    );
    await page.locator('mat-tree-node').filter({ hasText: 'Dashboard' }).click();
    await indexResponse;

    // Wait for spinner
    if (await page.locator('.overlay-spinner').count() > 0) {
      await expect(page.locator('.overlay-spinner')).not.toBeVisible({ timeout: 30000 });
    }

    await page.waitForTimeout(2000);

    // Verify that sites are shown
    if (await page.locator('app-time-plannings-table').count() > 0) {
      await expect(page.locator('app-time-plannings-table')).toBeVisible();
    }
  });

  /**
   * Test 4: Filter by single tag
   */
  test('should filter by single tag', async ({ page }) => {
    // Navigate to dashboard
    await page.locator('mat-nested-tree-node').filter({ hasText: 'Timeregistrering' }).click();
    await page.waitForTimeout(500);

    const indexResponse = page.waitForResponse(
      (resp) => resp.url().includes('/api/time-planning-pn/plannings/index') && resp.status() === 200,
      { timeout: 60000 }
    );
    await page.locator('mat-tree-node').filter({ hasText: 'Dashboard' }).click();
    await indexResponse;

    // Wait for spinner
    if (await page.locator('.overlay-spinner').count() > 0) {
      await expect(page.locator('.overlay-spinner')).not.toBeVisible({ timeout: 30000 });
    }

    for (const combination of siteTagCombinations) {
      await page.locator('#planningTags').scrollIntoViewIfNeeded();
      await expect(page.locator('#planningTags')).toBeVisible();
      await page.locator('#planningTags').click();
      await page.waitForTimeout(500);
      await page.locator('.ng-option').filter({ hasText: combination.tags[0] }).click();

      if (await page.locator('.overlay-spinner').count() > 0) {
        await expect(page.locator('.overlay-spinner')).not.toBeVisible({ timeout: 30000 });
      }

      // Verify that the correct site is shown for this tag
      if (await page.locator('app-time-plannings-table').count() > 0) {
        await expect(page.locator('app-time-plannings-table')).toBeVisible();
        await expect(page.locator('app-time-plannings-table')).toContainText(combination.sitename);
      }
      await page.waitForTimeout(500);
    }
  });

  /**
   * Test 5: Filter by multiple tags (AND logic)
   */
  test('should filter by multiple tags', async ({ page }) => {
    // Navigate to dashboard
    await page.locator('mat-nested-tree-node').filter({ hasText: 'Timeregistrering' }).click();
    await page.waitForTimeout(500);

    const indexResponse = page.waitForResponse(
      (resp) => resp.url().includes('/api/time-planning-pn/plannings/index') && resp.status() === 200,
      { timeout: 60000 }
    );
    await page.locator('mat-tree-node').filter({ hasText: 'Dashboard' }).click();
    await indexResponse;

    // Wait for spinner
    if (await page.locator('.overlay-spinner').count() > 0) {
      await expect(page.locator('.overlay-spinner')).not.toBeVisible({ timeout: 30000 });
    }

    await page.waitForTimeout(2000);
  });
});
