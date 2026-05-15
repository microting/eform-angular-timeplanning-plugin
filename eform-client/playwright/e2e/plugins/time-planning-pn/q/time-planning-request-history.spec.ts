import { test, expect } from '@playwright/test';
import { LoginPage } from '../../../Page objects/Login.page';

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
    await page.locator('#requestHistoryTypeFilter').selectOption('Handover');
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
    await page.locator('#requestHistoryTypeFilter').selectOption('Absence');
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
    await page.locator('#requestHistoryStatusFilter').selectOption('Pending');
    await page.locator('#applyFiltersBtn').click();

    // Wait for loading to complete
    if (await page.locator('.overlay-spinner').count() > 0) {
      await page.locator('.overlay-spinner').waitFor({ state: 'hidden', timeout: 30000 });
    }

    // Verify the grid is still visible after filtering
    await expect(page.locator('#time-planning-pn-request-history-grid')).toBeVisible();

    // If there are rows, verify only Pending status rows are shown
    const pendingBadges = page.locator('.status-pending');
    const approvedBadges = page.locator('.status-approved');
    const rejectedBadges = page.locator('.status-rejected');
    if (await pendingBadges.count() > 0) {
      expect(await approvedBadges.count()).toBe(0);
      expect(await rejectedBadges.count()).toBe(0);
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
    await page.locator('#requestHistoryTypeFilter').selectOption('Handover');
    await page.locator('#applyFiltersBtn').click();

    if (await page.locator('.overlay-spinner').count() > 0) {
      await page.locator('.overlay-spinner').waitFor({ state: 'hidden', timeout: 30000 });
    }

    // Then reset to All
    await page.locator('#requestHistoryTypeFilter').selectOption('');
    await page.locator('#applyFiltersBtn').click();

    if (await page.locator('.overlay-spinner').count() > 0) {
      await page.locator('.overlay-spinner').waitFor({ state: 'hidden', timeout: 30000 });
    }

    // Grid should still be visible
    await expect(page.locator('#time-planning-pn-request-history-grid')).toBeVisible();
  });
});
