import { test, expect, Page } from '@playwright/test';
import { LoginPage } from '../../../Page objects/Login.page';

// The Type/Status filters are mtx-select (ng-select) dropdowns. Select an
// option by its index so the helper stays independent of the UI language.
// Type options:   0=All, 1=Handover, 2=Absence
// Status options: 0=All, 1=Pending, 2=Approved, 3=Accepted, 4=Rejected, 5=Cancelled, 6=Expired
async function selectMtxOption(page: Page, selectId: string, optionIndex: number) {
  await page.locator(`#${selectId}`).click();
  await page.locator('.ng-dropdown-panel').waitFor({ state: 'visible', timeout: 10000 });
  await page.locator('.ng-dropdown-panel .ng-option').nth(optionIndex).click();
}

test.describe('Time Planning - Request History', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('http://localhost:4200');
    await new LoginPage(page).login();
    // Navigate directly to the request history page
    await page.goto('http://localhost:4200/plugins/time-planning-pn/request-history');
    // Wait for any spinner to disappear
    if (await page.locator('.overlay-spinner').count() > 0) {
      await page.locator('.overlay-spinner').waitFor({ state: 'hidden', timeout: 30000 });
    }
  });

  test('should load the request history page without stalling', async ({ page }) => {
    // Verify the filter bar is visible
    await page.locator('#time-planning-pn-request-history-filters').scrollIntoViewIfNeeded();
    await expect(page.locator('#time-planning-pn-request-history-filters')).toBeVisible();

    // Verify the grid is visible
    await page.locator('#time-planning-pn-request-history-grid').scrollIntoViewIfNeeded();
    await expect(page.locator('#time-planning-pn-request-history-grid')).toBeVisible();
  });

  test('should display all filter controls', async ({ page }) => {
    // Type filter dropdown
    await expect(page.locator('#requestHistoryTypeFilter')).toBeVisible();

    // Status filter dropdown
    await expect(page.locator('#requestHistoryStatusFilter')).toBeVisible();

    // Date from input
    await expect(page.locator('#requestHistoryFromDate')).toBeVisible();

    // Date to input
    await expect(page.locator('#requestHistoryToDate')).toBeVisible();

    // Apply button
    await expect(page.locator('#applyFiltersBtn')).toBeVisible();
  });

  test('should filter by type when selecting Handover', async ({ page }) => {
    // Select "Handover" in the type filter
    await selectMtxOption(page, 'requestHistoryTypeFilter', 1);
    await page.locator('#applyFiltersBtn').click();

    // Wait for loading to complete
    if (await page.locator('.overlay-spinner').count() > 0) {
      await page.locator('.overlay-spinner').waitFor({ state: 'hidden', timeout: 30000 });
    }

    // Verify the grid is still visible after filtering
    await expect(page.locator('#time-planning-pn-request-history-grid')).toBeVisible();

    // If there are rows, all should be of type Handover
    const typeHandoverBadges = page.locator('.type-handover');
    const typeAbsenceBadges = page.locator('.type-absence');
    if (await typeHandoverBadges.count() > 0) {
      // There should be no Absence rows when filtering by Handover
      expect(await typeAbsenceBadges.count()).toBe(0);
    }
  });

  test('should filter by type when selecting Absence', async ({ page }) => {
    // Select "Absence" in the type filter
    await selectMtxOption(page, 'requestHistoryTypeFilter', 2);
    await page.locator('#applyFiltersBtn').click();

    // Wait for loading to complete
    if (await page.locator('.overlay-spinner').count() > 0) {
      await page.locator('.overlay-spinner').waitFor({ state: 'hidden', timeout: 30000 });
    }

    // Verify the grid is still visible after filtering
    await expect(page.locator('#time-planning-pn-request-history-grid')).toBeVisible();

    // If there are rows, all should be of type Absence
    const typeAbsenceBadges = page.locator('.type-absence');
    const typeHandoverBadges = page.locator('.type-handover');
    if (await typeAbsenceBadges.count() > 0) {
      // There should be no Handover rows when filtering by Absence
      expect(await typeHandoverBadges.count()).toBe(0);
    }
  });

  test('should filter by status when selecting Pending', async ({ page }) => {
    // Select "Pending" in the status filter
    await selectMtxOption(page, 'requestHistoryStatusFilter', 1);
    await page.locator('#applyFiltersBtn').click();

    // Wait for loading to complete
    if (await page.locator('.overlay-spinner').count() > 0) {
      await page.locator('.overlay-spinner').waitFor({ state: 'hidden', timeout: 30000 });
    }

    // Verify the grid is still visible after filtering
    await expect(page.locator('#time-planning-pn-request-history-grid')).toBeVisible();

    // Status renders as mat-chip badges: Pending -> tag-warning, Approved/Accepted
    // -> tag-success, Rejected -> tag-error. When filtering by Pending, no
    // success/error badges should remain.
    const pendingBadges = page.locator('mat-chip.tag-warning');
    const successBadges = page.locator('mat-chip.tag-success');
    const errorBadges = page.locator('mat-chip.tag-error');
    if (await pendingBadges.count() > 0) {
      expect(await successBadges.count()).toBe(0);
      expect(await errorBadges.count()).toBe(0);
    }
  });

  test('should handle empty state gracefully', async ({ page }) => {
    // The grid should be visible even with no data
    await expect(page.locator('#time-planning-pn-request-history-grid')).toBeVisible();

    // If no rows, the noResultText should be present or the grid should simply be empty
    // (mtx-grid shows noResultText when data is empty)
  });

  test('should reset filters when selecting All for type', async ({ page }) => {
    // First set a filter
    await selectMtxOption(page, 'requestHistoryTypeFilter', 1);
    await page.locator('#applyFiltersBtn').click();

    if (await page.locator('.overlay-spinner').count() > 0) {
      await page.locator('.overlay-spinner').waitFor({ state: 'hidden', timeout: 30000 });
    }

    // Then reset to All
    await selectMtxOption(page, 'requestHistoryTypeFilter', 0);
    await page.locator('#applyFiltersBtn').click();

    if (await page.locator('.overlay-spinner').count() > 0) {
      await page.locator('.overlay-spinner').waitFor({ state: 'hidden', timeout: 30000 });
    }

    // Grid should still be visible
    await expect(page.locator('#time-planning-pn-request-history-grid')).toBeVisible();
  });
});
