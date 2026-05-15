import { test, expect, Page } from '@playwright/test';
import { LoginPage } from '../../../Page objects/Login.page';
import { OFFGRID_TIMES_E1M } from '../../../../helpers/one-minute-times';

/**
 * e1m variant of the multi-shift round-trip regression guard, mirroring
 * `b1m/`, `c1m/` and `d1m/` but with off-grid times that land on the
 * small-hour outer-ring (01-10) hour positions of the timepicker clock face,
 * deliberately crossing the 12 boundary. b1m sweeps a narrower mid-morning
 * band (08-16), c1m straddles the outer→inner transition around 17-19, d1m
 * parks every shift inside the inner ring (13-23); e1m mirrors d1m on the
 * opposite side by sweeping the small-hour outer ring (01-11). The variant
 * matrix as a whole now exercises every quadrant of the 24-hour clock surface.
 *
 * Shared with b1m/c1m/d1m:
 *   • Same seed (`420_eform-angular-time-planning-plugin.sql` is a copy
 *     of `a/`).
 *   • Same `post-migration.sql` flipping `UseOneMinuteIntervals = 1` for
 *     every active assigned site.
 *   • Same multi-shift round-trip shape (fill all 5 shifts, save, reload,
 *     assert every value round-tripped).
 *
 * The e shard's original specs (`e/dashboard-edit-a.spec.ts`) are NOT
 * cloned here. That source spec only fills shift 1 with edge values
 * (01:00 / 00:00) — the partial-shift / midnight-wrap shape that hits the
 * still-unresolved `success:false` path inside
 * `TimePlanningPlanningService.Update` (see the lessons embedded in
 * PR #1545 / PR #1548 / PR #1549). Per the brainstorm map, e1m therefore
 * ships the same multishift-shape clone that b1m, c1m and d1m ship,
 * swapping in a different minute-grid neighborhood to keep the matrix
 * entry useful for the flag-on code path.
 *
 * Shift layout used by this test (every value is intentionally NOT a
 * multiple of 5 to push the timepicker through `minutesGap=1`; every
 * hour 01-11 lands on the outer ring, with shift 4 crossing the 7→9
 * span and shift 5 crossing the outer-ring upper boundary at 11):
 *   Shift 1: 01:03-02:17 break 00:31
 *   Shift 2: 02:29-04:41 break 00:31
 *   Shift 3: 04:52-06:58 break 00:31
 *   Shift 4: 07:09-09:21 break 00:31
 *   Shift 5: 09:34-11:46 break 00:31
 */

async function waitForSpinner(page: Page) {
  if (await page.locator('.overlay-spinner').count() > 0) {
    await page.locator('.overlay-spinner').waitFor({ state: 'hidden', timeout: 30000 });
  }
}

async function pickTime(page: Page, timeStr: string) {
  // Position-based clock-face clicks. Works uniformly for h=0 (break
  // times), unlike rotateZ-selector strategies. Identical helper to b1m/c1m/d1m.
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

async function setShift(page: Page, shiftId: 1|2|3|4|5, start: string, end: string, breakStr: string) {
  await page.locator(`[data-testid="plannedStartOfShift${shiftId}"]`).click();
  await pickTime(page, start);
  await expect(page.locator(`[data-testid="plannedStartOfShift${shiftId}"]`)).toHaveValue(start);

  await page.locator(`[data-testid="plannedEndOfShift${shiftId}"]`).click();
  await pickTime(page, end);
  await expect(page.locator(`[data-testid="plannedEndOfShift${shiftId}"]`)).toHaveValue(end);

  await page.locator(`[data-testid="plannedBreakOfShift${shiftId}"]`).click();
  await pickTime(page, breakStr);
  await expect(page.locator(`[data-testid="plannedBreakOfShift${shiftId}"]`)).toHaveValue(breakStr);
}

const allFiveShifts = [
  { id: 1 as const, start: OFFGRID_TIMES_E1M.shift1Start, end: OFFGRID_TIMES_E1M.shift1End, break: OFFGRID_TIMES_E1M.break },
  { id: 2 as const, start: OFFGRID_TIMES_E1M.shift2Start, end: OFFGRID_TIMES_E1M.shift2End, break: OFFGRID_TIMES_E1M.break },
  { id: 3 as const, start: OFFGRID_TIMES_E1M.shift3Start, end: OFFGRID_TIMES_E1M.shift3End, break: OFFGRID_TIMES_E1M.break },
  { id: 4 as const, start: OFFGRID_TIMES_E1M.shift4Start, end: OFFGRID_TIMES_E1M.shift4End, break: OFFGRID_TIMES_E1M.break },
  { id: 5 as const, start: OFFGRID_TIMES_E1M.shift5Start, end: OFFGRID_TIMES_E1M.shift5End, break: OFFGRID_TIMES_E1M.break },
];

test.describe('Dashboard — multi-shift (3-5) round-trip regression guard (e1m, flag-on, outer-ring 01-11)', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('http://localhost:4200');
    await new LoginPage(page).login();
  });

  test('persists all 5 planned shifts at 1-minute granularity through save + reload (small-hour shift block)', async ({ page }) => {
    await page.locator('mat-nested-tree-node').filter({ hasText: 'Timeregistrering' }).click();
    const indexPromise = page.waitForResponse(r =>
      r.url().includes('/api/time-planning-pn/plannings/index') && r.request().method() === 'POST');
    await page.locator('mat-tree-node').filter({ hasText: 'Dashboard' }).click();
    await indexPromise;
    await waitForSpinner(page);

    // Shifts 3-5 are only rendered in the workday-entity dialog when the
    // assigned site has thirdShiftActive / fourthShiftActive / fifthShiftActive
    // flipped on. The post-migration patch only sets `UseOneMinuteIntervals`
    // — the multi-shift flags still need the UI dance below.
    for (const id of ['thirdShiftActive', 'fourthShiftActive', 'fifthShiftActive']) {
      await page.locator('#firstColumn3').click();
      await expect(page.locator('mat-dialog-container')).toBeVisible({ timeout: 10000 });

      const cb = page.locator(`#${id} input[type="checkbox"]`);
      await cb.waitFor({ state: 'attached', timeout: 10000 });
      if (!(await cb.isChecked())) {
        await page.locator(`#${id}`).click({ force: true });
      }
      await expect(cb).toBeChecked();

      // PR #1545 lesson: assert saveButton is enabled before clicking. Angular
      // drops clicks on disabled regardless of Playwright's actionability bypass.
      await expect(page.locator('#saveButton')).toBeEnabled({ timeout: 10000 });
      const assignSitePromise = page.waitForResponse(
        r => r.url().includes('/api/time-planning-pn/settings/assigned-site') && r.request().method() === 'PUT');
      await page.locator('#saveButton').click();
      await assignSitePromise;
      await waitForSpinner(page);
      await expect(page.locator('mat-dialog-container')).toHaveCount(0, { timeout: 10000 });
    }

    const cellId = '#cell3_0';
    await page.locator(cellId).scrollIntoViewIfNeeded();
    await page.locator(cellId).click();
    await expect(page.locator('#planHours')).toBeVisible();

    // Fill all 5 shifts at 1-minute granularity.
    for (const s of allFiveShifts) {
      await setShift(page, s.id, s.start, s.end, s.break);
    }

    // PR #1545 lesson: assert saveButton is enabled before clicking.
    await expect(page.locator('#saveButton')).toBeEnabled({ timeout: 10000 });
    const updatePromise = page.waitForResponse(r =>
      r.url().includes('/api/time-planning-pn/plannings/') && r.request().method() === 'PUT');
    const reindexPromise = page.waitForResponse(r =>
      r.url().includes('/api/time-planning-pn/plannings/index') && r.request().method() === 'POST');
    await page.locator('#saveButton').click();
    await updatePromise;
    await reindexPromise;
    await waitForSpinner(page);
    await page.waitForTimeout(500);

    // Re-open the same cell and assert every shift round-tripped.
    await page.locator(cellId).scrollIntoViewIfNeeded();
    await page.locator(cellId).click();
    await expect(page.locator('#planHours')).toBeVisible();

    for (const s of allFiveShifts) {
      await expect(
        page.locator(`[data-testid="plannedStartOfShift${s.id}"]`),
        `shift ${s.id} start should round-trip`
      ).toHaveValue(s.start);
      await expect(
        page.locator(`[data-testid="plannedEndOfShift${s.id}"]`),
        `shift ${s.id} end should round-trip`
      ).toHaveValue(s.end);
      await expect(
        page.locator(`[data-testid="plannedBreakOfShift${s.id}"]`),
        `shift ${s.id} break should round-trip`
      ).toHaveValue(s.break);
    }

    await page.locator('#cancelButton').click();
  });
});
