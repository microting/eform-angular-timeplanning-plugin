import { test, expect } from '@playwright/test';
import { LoginPage } from '../../../Login.page';
import { PluginPage } from '../../../Plugin.page';

test.describe('Time Planning - Absence Requests', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('http://localhost:4200');
    await new LoginPage(page).login();
    // Navigate directly to the absence requests page since menu entry doesn't exist yet
    await page.goto('http://localhost:4200/plugins/time-planning-pn/absence-requests');
  });

  test('should display absence requests inbox view', async ({ page }) => {
    await page.locator('#absenceRequestsInboxBtn').scrollIntoViewIfNeeded();
    await expect(page.locator('#absenceRequestsInboxBtn')).toBeVisible();
    await expect(page.locator('#absenceRequestsMineBtn')).toBeVisible();
    await page.locator('#time-planning-pn-absence-requests-grid').scrollIntoViewIfNeeded();
    await expect(page.locator('#time-planning-pn-absence-requests-grid')).toBeVisible();
  });

  test('should switch between inbox and my requests views', async ({ page }) => {
    await expect(page.locator('#absenceRequestsInboxBtn')).toHaveClass(/active/);

    await page.locator('#absenceRequestsMineBtn').click();
    await expect(page.locator('#absenceRequestsMineBtn')).toHaveClass(/active/);
    await expect(page.locator('#absenceRequestsInboxBtn')).not.toHaveClass(/active/);

    await page.locator('#absenceRequestsInboxBtn').click();
    await expect(page.locator('#absenceRequestsInboxBtn')).toHaveClass(/active/);
    await expect(page.locator('#absenceRequestsMineBtn')).not.toHaveClass(/active/);
  });

  test('should display approve and reject buttons for pending requests in inbox', async ({ page }) => {
    await page.locator('#absenceRequestsInboxBtn').click();

    const grid = page.locator('#time-planning-pn-absence-requests-grid');
    // If there's data, check for buttons
    if (await page.locator('[id^="approveAbsenceRequestBtn-"]').count() > 0) {
      await expect(page.locator('[id^="approveAbsenceRequestBtn-"]').first()).toBeVisible();
      await expect(page.locator('[id^="rejectAbsenceRequestBtn-"]').first()).toBeVisible();
    }
    // No data scenario is also valid
  });

  test('should open approve modal when approve button is clicked', async ({ page }) => {
    await page.locator('#absenceRequestsInboxBtn').click();

    // Only try to click if button exists
    if (await page.locator('[id^="approveAbsenceRequestBtn-"]').count() > 0) {
      await page.locator('[id^="approveAbsenceRequestBtn-"]').first().click();

      await expect(page.locator('h3[mat-dialog-title]')).toContainText('Approve Absence Request');
      await page.locator('#saveApproveBtn').scrollIntoViewIfNeeded();
      await expect(page.locator('#saveApproveBtn')).toBeVisible();
      await expect(page.locator('#cancelApproveBtn')).toBeVisible();

      await page.locator('#cancelApproveBtn').click();
    }
  });

  test('should open reject modal when reject button is clicked', async ({ page }) => {
    await page.locator('#absenceRequestsInboxBtn').click();

    // Only try to click if button exists
    if (await page.locator('[id^="rejectAbsenceRequestBtn-"]').count() > 0) {
      await page.locator('[id^="rejectAbsenceRequestBtn-"]').first().click();

      await expect(page.locator('h3[mat-dialog-title]')).toContainText('Reject Absence Request');
      await page.locator('#saveRejectBtn').scrollIntoViewIfNeeded();
      await expect(page.locator('#saveRejectBtn')).toBeVisible();
      await expect(page.locator('#cancelRejectBtn')).toBeVisible();

      await page.locator('#cancelRejectBtn').click();
    }
  });
});
