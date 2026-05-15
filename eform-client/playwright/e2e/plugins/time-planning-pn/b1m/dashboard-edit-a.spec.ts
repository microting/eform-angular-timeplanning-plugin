import { test, expect, Page } from '@playwright/test';
import { LoginPage } from '../../../Page objects/Login.page';
import { OFFGRID_TIMES } from '../../../../helpers/one-minute-times';

/**
 * b1m variant of `b/dashboard-edit-a.spec.ts`: exercises the planned-shift
 * edit + round-trip flow with off-grid (NOT 5-min-aligned) time literals.
 * Relies on the b1m `post-migration.sql` patch to flip
 * `AssignedSites.UseOneMinuteIntervals = 1` (so the Material timepicker
 * runs at `minutesGap=1` and the position-based picker can land on any
 * minute 0-59), and to flip ThirdShiftActive / FourthShiftActive /
 * FifthShiftActive = 1 (FU-A) so the workday-entity dialog renders
 * shift 3-5 inputs without an in-spec settings dance.
 *
 * FU-B history: this clone was dropped twice (PR #1545 → commit 35a22382)
 * because the legacy two-shift fill (shift1 + shift2 only) caused the
 * server-side Update path to compute over null shift3-5 fields and
 * silently fail (Save returned `success:false`, no `/plannings/index`
 * POST followed, `waitForResponse` hung). FU-A enables shifts 3-5 in
 * the seed; the remaining fix is to FILL all five shifts ascending so
 * the PUT body always carries every non-null shift cell — matching the
 * b1m multishift-shape pattern. PRs #1546 (currentUserAsync.Id) and
 * #1547 (model null-guard) shipped earlier server-side fixes; FU-A
 * (#1556) extended the seed; FU-B (this spec) drives the full five-
 * shift PUT body so all three fixes are exercised end-to-end.
 *
 * Scope: form-input round-trip on flag-on rows. Cumulative-flex math
 * stays on the legacy `b/` shard.
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

async function setPlannedShift(
  page: Page,
  shiftId: 1|2|3|4|5,
  start: string,
  end: string,
  breakStr: string,
) {
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

// All five shifts ascending, off-grid. Shifts 3-5 share a single break value
// with shifts 1-2 — the `break` field is intentionally a single canonical
// constant so adding new shifts doesn't mean adding new break literals (the
// workday-entity-dialog has one break input per shift, but the value is the
// same for every shift in this round-trip; what we're testing is the
// position-based picker round-trip, not break-per-shift uniqueness).
const allFivePlannedShifts = [
  { id: 1 as const, start: OFFGRID_TIMES.shift1Start, end: OFFGRID_TIMES.shift1End, break: OFFGRID_TIMES.break },
  { id: 2 as const, start: OFFGRID_TIMES.shift2Start, end: OFFGRID_TIMES.shift2End, break: OFFGRID_TIMES.break },
  { id: 3 as const, start: OFFGRID_TIMES.shift3Start, end: OFFGRID_TIMES.shift3End, break: OFFGRID_TIMES.break },
  { id: 4 as const, start: OFFGRID_TIMES.shift4Start, end: OFFGRID_TIMES.shift4End, break: OFFGRID_TIMES.break },
  { id: 5 as const, start: OFFGRID_TIMES.shift5Start, end: OFFGRID_TIMES.shift5End, break: OFFGRID_TIMES.break },
];

test.describe('Dashboard edit values (b1m, flag-on, 1-minute granularity)', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('http://localhost:4200');
    await new LoginPage(page).login();
  });

  test('persists off-grid planned shifts 1-5 through save + reopen', async ({ page }) => {
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

    // Fill all five shifts ascending. Shifts 3-5 inputs render because the
    // b1m post-migration patch (FU-A) sets ThirdShiftActive /
    // FourthShiftActive / FifthShiftActive = 1 on every active assigned
    // site, so the dialog template's `*ngIf="thirdShiftActive"` (etc.)
    // evaluates true on first render — no in-spec settings dance needed.
    for (const s of allFivePlannedShifts) {
      await setPlannedShift(page, s.id, s.start, s.end, s.break);
    }

    // Save. Wait for the button to lose `disabled` (the form's cross-shift
    // validators run async after each pick) before clicking — `force: true`
    // would let Playwright dispatch the event but the browser still drops
    // clicks on disabled <button>s, so the request never fires and the
    // waitForResponse below would just hang.
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

    // Re-open and assert every off-grid value round-tripped.
    await page.locator(cellId).scrollIntoViewIfNeeded();
    await page.locator(cellId).click();
    await expect(page.locator('#planHours')).toBeVisible();

    for (const s of allFivePlannedShifts) {
      await expect(
        page.locator(`[data-testid="plannedStartOfShift${s.id}"]`),
        `shift ${s.id} start should round-trip`,
      ).toHaveValue(s.start);
      await expect(
        page.locator(`[data-testid="plannedEndOfShift${s.id}"]`),
        `shift ${s.id} end should round-trip`,
      ).toHaveValue(s.end);
      await expect(
        page.locator(`[data-testid="plannedBreakOfShift${s.id}"]`),
        `shift ${s.id} break should round-trip`,
      ).toHaveValue(s.break);
    }

    await page.locator('#cancelButton').click();
  });
});
