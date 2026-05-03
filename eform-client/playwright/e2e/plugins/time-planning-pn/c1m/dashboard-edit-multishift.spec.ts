import { test, expect, Page } from '@playwright/test';
import { LoginPage } from '../../../Page objects/Login.page';
import { OFFGRID_TIMES_C1M } from '../../../helpers/one-minute-times';

/**
 * c1m variant of the multi-shift round-trip regression guard, mirroring
 * `b1m/dashboard-edit-multishift.spec.ts` but with off-grid times that
 * land on the lower-half hour positions of the clock face (afternoon /
 * early-evening). Both b1m and c1m share the seed and the post-migration
 * patch (`UPDATE AssignedSites SET UseOneMinuteIntervals = 1 ...`); the
 * point of duplicating the round-trip in a second shard is to exercise
 * a different minute-grid neighborhood and to keep the c-shard playwright
 * matrix entry useful for the flag-on code path.
 *
 * The original c-shard specs (`c/dashboard-edit-a.spec.ts`,
 * `c/time-planning-glsa-3f-pay-rules.spec.ts`) are NOT cloned here:
 *
 *   • `c/dashboard-edit-a.spec.ts` exercises the text-mode `#planText`
 *     parser, never opens the timepicker overlay, and therefore does
 *     not benefit from the flag-on minute-granularity rendering. It
 *     also fills only shifts 1+2 — the partial-shift shape that hits
 *     the still-unresolved `success:false` path in
 *     `TimePlanningPlanningService.Update` (see PR #1545's lessons).
 *   • `c/time-planning-glsa-3f-pay-rules.spec.ts` is a pay-rule preset
 *     CRUD spec; it explicitly skips shift entry (line ~348: "Shift
 *     entry via timepicker is already covered by dashboard-edit-b.spec.ts").
 *     Nothing in those scenarios depends on the minute granularity.
 *
 * Shift layout used by this test (every value is intentionally NOT a
 * multiple of 5 to push the timepicker through `minutesGap=1`):
 *   Shift 1: 08:01-11:13 break 00:27
 *   Shift 2: 12:17-14:23 break 00:27
 *   Shift 3: 14:35-15:42 break 00:27
 *   Shift 4: 15:55-17:08 break 00:27
 *   Shift 5: 17:21-19:33 break 00:27
 */

async function waitForSpinner(page: Page) {
  if (await page.locator('.overlay-spinner').count() > 0) {
    await page.locator('.overlay-spinner').waitFor({ state: 'hidden', timeout: 30000 });
  }
}

async function pickTime(page: Page, timeStr: string) {
  // Position-based clock-face clicks. Works uniformly for h=0 (break
  // times), unlike rotateZ-selector strategies. Identical helper to b1m.
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
  { id: 1 as const, start: OFFGRID_TIMES_C1M.shift1Start, end: OFFGRID_TIMES_C1M.shift1End, break: OFFGRID_TIMES_C1M.break },
  { id: 2 as const, start: OFFGRID_TIMES_C1M.shift2Start, end: OFFGRID_TIMES_C1M.shift2End, break: OFFGRID_TIMES_C1M.break },
  { id: 3 as const, start: OFFGRID_TIMES_C1M.shift3Start, end: OFFGRID_TIMES_C1M.shift3End, break: OFFGRID_TIMES_C1M.break },
  { id: 4 as const, start: OFFGRID_TIMES_C1M.shift4Start, end: OFFGRID_TIMES_C1M.shift4End, break: OFFGRID_TIMES_C1M.break },
  { id: 5 as const, start: OFFGRID_TIMES_C1M.shift5Start, end: OFFGRID_TIMES_C1M.shift5End, break: OFFGRID_TIMES_C1M.break },
];

test.describe('Dashboard — multi-shift (3-5) round-trip regression guard (c1m, flag-on, afternoon)', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('http://localhost:4200');
    await new LoginPage(page).login();
  });

  test('persists all 5 planned shifts at 1-minute granularity through save + reload (afternoon shift block)', async ({ page }) => {
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
