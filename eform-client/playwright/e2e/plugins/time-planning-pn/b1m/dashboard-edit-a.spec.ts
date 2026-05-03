import { test, expect, Page } from '@playwright/test';
import { LoginPage } from '../../../Page objects/Login.page';
import { OFFGRID_TIMES } from '../../../../helpers/one-minute-times';

/**
 * b1m variant of `b/dashboard-edit-a.spec.ts`: exercises the same planned-shift
 * edit + round-trip flow but with off-grid (NOT 5-min-aligned) time literals.
 * Relies on the post-migration patch (`b1m/post-migration.sql`) to flip
 * `AssignedSites.UseOneMinuteIntervals = 1`, which in turn pushes the Material
 * timepicker into `minutesGap=1` so any minute 0-59 is selectable.
 *
 * Scope is intentionally narrower than the legacy `b/` shard: the legacy spec
 * is tightly coupled to seed-driven cumulative flex math that recomputes per
 * cell. Variant focus here is the picker + form-input round-trip on flag-on
 * rows; the cumulative-math assertions stay on the `b/` shard.
 */

async function waitForSpinner(page: Page) {
  if (await page.locator('.overlay-spinner').count() > 0) {
    await page.locator('.overlay-spinner').waitFor({ state: 'hidden', timeout: 30000 });
  }
}

async function pickTime(page: Page, timeStr: string) {
  // Position-based clock-face clicks (cloned from b/dashboard-edit-multishift's
  // pickTime helper). Works at minute granularity because the position math
  // is `minuteAngle = m * 6`, no rotateZ-selector dependency.
  const [hourStr, minuteStr] = timeStr.split(':');
  const h = parseInt(hourStr, 10);
  const m = parseInt(minuteStr, 10);

  const cx = 145, cy = 145;

  const hourFace = page.locator('.clock-face');
  await hourFace.first().waitFor({ state: 'visible', timeout: 5000 });
  const hourAngle = (h % 12) * 30;
  const hourR = (h === 0 || h > 12) ? 60 : 100;
  const hourRad = hourAngle * Math.PI / 180;
  await hourFace.first().click({
    position: {
      x: Math.round(cx + hourR * Math.sin(hourRad)),
      y: Math.round(cy - hourR * Math.cos(hourRad)) + (Math.abs(Math.cos(hourRad)) < 0.01 ? 1 : 0),
    },
  });

  await page.waitForTimeout(500);
  const minuteFace = page.locator('.clock-face');
  await minuteFace.first().waitFor({ state: 'visible', timeout: 5000 });
  const minuteAngle = m * 6;
  const minuteR = 100;
  const minuteRad = minuteAngle * Math.PI / 180;
  await minuteFace.first().click({
    position: {
      x: Math.round(cx + minuteR * Math.sin(minuteRad)),
      y: Math.round(cy - minuteR * Math.cos(minuteRad)) + (Math.abs(Math.cos(minuteRad)) < 0.01 ? 1 : 0),
    },
  });

  await page.waitForTimeout(500);
  await page.locator('.timepicker-button span').filter({ hasText: 'Ok' }).click();
}

test.describe('Dashboard edit values (b1m, flag-on, 1-minute granularity)', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('http://localhost:4200');
    await new LoginPage(page).login();
  });

  test('persists off-grid planned shift1 + shift2 through save + reopen', async ({ page }) => {
    await page.locator('mat-nested-tree-node').filter({ hasText: 'Timeregistrering' }).click();
    const indexPromise = page.waitForResponse(r =>
      r.url().includes('/api/time-planning-pn/plannings/index') && r.request().method() === 'POST');
    await page.locator('mat-tree-node').filter({ hasText: 'Dashboard' }).click();
    await indexPromise;
    await waitForSpinner(page);

    const cellId = '#cell3_0';
    await page.locator(cellId).scrollIntoViewIfNeeded();
    await page.locator(cellId).click();
    await expect(page.locator('#planHours')).toBeVisible();

    // Set shift 1 with off-grid start, end, break.
    await page.locator('[data-testid="plannedStartOfShift1"]').click();
    await pickTime(page, OFFGRID_TIMES.shift1Start);
    await expect(page.locator('[data-testid="plannedStartOfShift1"]')).toHaveValue(OFFGRID_TIMES.shift1Start);

    await page.locator('[data-testid="plannedEndOfShift1"]').click();
    await pickTime(page, OFFGRID_TIMES.shift1End);
    await expect(page.locator('[data-testid="plannedEndOfShift1"]')).toHaveValue(OFFGRID_TIMES.shift1End);

    await page.locator('[data-testid="plannedBreakOfShift1"]').click();
    await pickTime(page, OFFGRID_TIMES.break);
    await expect(page.locator('[data-testid="plannedBreakOfShift1"]')).toHaveValue(OFFGRID_TIMES.break);

    // Set shift 2 with off-grid start + end (no break to keep the assertion
    // stack short — shift1 already covers the break path).
    await page.locator('[data-testid="plannedStartOfShift2"]').click();
    await pickTime(page, OFFGRID_TIMES.shift2Start);
    await expect(page.locator('[data-testid="plannedStartOfShift2"]')).toHaveValue(OFFGRID_TIMES.shift2Start);

    await page.locator('[data-testid="plannedEndOfShift2"]').click();
    await pickTime(page, OFFGRID_TIMES.shift2End);
    await expect(page.locator('[data-testid="plannedEndOfShift2"]')).toHaveValue(OFFGRID_TIMES.shift2End);

    // Save.
    const updatePromise = page.waitForResponse(r =>
      r.url().includes('/api/time-planning-pn/plannings/') && r.request().method() === 'PUT');
    const reindexPromise = page.waitForResponse(r =>
      r.url().includes('/api/time-planning-pn/plannings/index') && r.request().method() === 'POST');
    await page.locator('#saveButton').click();
    await updatePromise;
    await reindexPromise;
    await waitForSpinner(page);
    await page.waitForTimeout(500);

    // Re-open and assert every off-grid value round-tripped.
    await page.locator(cellId).scrollIntoViewIfNeeded();
    await page.locator(cellId).click();
    await expect(page.locator('#planHours')).toBeVisible();

    await expect(page.locator('[data-testid="plannedStartOfShift1"]')).toHaveValue(OFFGRID_TIMES.shift1Start);
    await expect(page.locator('[data-testid="plannedEndOfShift1"]')).toHaveValue(OFFGRID_TIMES.shift1End);
    await expect(page.locator('[data-testid="plannedBreakOfShift1"]')).toHaveValue(OFFGRID_TIMES.break);
    await expect(page.locator('[data-testid="plannedStartOfShift2"]')).toHaveValue(OFFGRID_TIMES.shift2Start);
    await expect(page.locator('[data-testid="plannedEndOfShift2"]')).toHaveValue(OFFGRID_TIMES.shift2End);

    await page.locator('#cancelButton').click();
  });
});
