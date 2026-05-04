import { test, expect, Page } from '@playwright/test';
import { LoginPage } from '../../../Page objects/Login.page';
import { OFFGRID_TIMES } from '../../../../helpers/one-minute-times';

/**
 * b1m variant of `b/dashboard-edit-b.spec.ts`: edits the ACTUAL stamps
 * (start1StartedAt / stop1StoppedAt / pause1Id) at minute granularity and
 * round-trips them. Phase 4 added second-precision DISPLAY for actual stamps
 * when the row's site has `useOneMinuteIntervals = true` — the dashboard
 * cell renders `formatStamp(...)` and `getStopTimeDisplayWithSeconds(...)`
 * with `HH:mm:ss` format on flag-on rows.
 *
 * The form-input values themselves stay `HH:mm` (Phase 4 didn't widen the
 * time-picker form binding), so input-value assertions remain `HH:mm`.
 * The cell-text assertion below is the bit that exercises the second-
 * precision display path.
 *
 * FU-B history: this clone was dropped (PR #1545 → commit 6acff720)
 * because `waitForResponse('/plannings/index')` timed out on save —
 * server-side Update returned `success:false` so no `/plannings/index`
 * POST followed. PRs #1546 (currentUserAsync.Id) and #1547 (model
 * null-guard) shipped server-side fixes; FU-A (#1556) extended the
 * b1m seed to flip ThirdShiftActive / FourthShiftActive /
 * FifthShiftActive = 1 so the dialog renders shift 3-5 inputs without
 * an in-spec settings dance. The remaining hypothesis was that the
 * legacy two-shift fill caused the server-side compute to operate on
 * null shift3-5 fields and silently fail — FU-B (this clone) fills
 * all five planned shifts ascending before setting actual stamps so
 * the PUT body always carries every non-null shift cell.
 */

async function waitForSpinner(page: Page) {
  if (await page.locator('.overlay-spinner').count() > 0) {
    await page.locator('.overlay-spinner').waitFor({ state: 'hidden', timeout: 30000 });
  }
}

async function pickTime(page: Page, timeStr: string) {
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

// Same five-shift block the b1m/dashboard-edit-a clone uses — keeps the
// PUT body fully populated so the server-side Update path never operates
// on null shift3-5 fields. The actual stamps below are set on shift 1
// only (mirrors the legacy `b/dashboard-edit-b.spec.ts` convention; the
// HH:mm:ss display assertion targets `firstShiftActual3_0`).
const allFivePlannedShifts = [
  { id: 1 as const, start: OFFGRID_TIMES.shift1Start, end: OFFGRID_TIMES.shift1End, break: OFFGRID_TIMES.break },
  { id: 2 as const, start: OFFGRID_TIMES.shift2Start, end: OFFGRID_TIMES.shift2End, break: OFFGRID_TIMES.break },
  { id: 3 as const, start: OFFGRID_TIMES.shift3Start, end: OFFGRID_TIMES.shift3End, break: OFFGRID_TIMES.break },
  { id: 4 as const, start: OFFGRID_TIMES.shift4Start, end: OFFGRID_TIMES.shift4End, break: OFFGRID_TIMES.break },
  { id: 5 as const, start: OFFGRID_TIMES.shift5Start, end: OFFGRID_TIMES.shift5End, break: OFFGRID_TIMES.break },
];

test.describe('Dashboard edit actual stamps (b1m, flag-on, 1-minute granularity)', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('http://localhost:4200');
    await new LoginPage(page).login();
  });

  test('persists off-grid actual start/stop and renders HH:mm:ss in dashboard cell', async ({ page }) => {
    await page.locator('mat-nested-tree-node').filter({ hasText: 'Timeregistrering' }).click();
    const indexPromise = page.waitForResponse(r =>
      r.url().includes('/api/time-planning-pn/plannings/index') && r.request().method() === 'POST');
    await page.locator('mat-tree-node').filter({ hasText: 'Dashboard' }).click();
    // Step backwards into last-week so the cell falls in the past
    // (firstShiftActual is rendered for any day that has a start1StartedAt
    // value, but past dates render the cumulative flex too — keeping the
    // assertion stack aligned with the legacy `b` shard's range).
    await page.locator('#backwards').click();
    await indexPromise;
    await waitForSpinner(page);

    const cellId = '#cell3_0';
    await page.locator(cellId).scrollIntoViewIfNeeded();
    await page.locator(cellId).click();
    await expect(page.locator('#planHours')).toBeVisible();
    await page.waitForTimeout(500);

    // Fill all five planned shifts ascending so the PUT body always
    // carries non-null shift3-5 fields. Shifts 3-5 inputs render because
    // the b1m post-migration patch (FU-A) sets ThirdShiftActive /
    // FourthShiftActive / FifthShiftActive = 1 on every active assigned
    // site.
    for (const s of allFivePlannedShifts) {
      await setPlannedShift(page, s.id, s.start, s.end, s.break);
    }

    // Set the actual stamps for shift 1 with off-grid times. Pause IS set
    // here (matching the legacy `b/dashboard-edit-b.spec.ts` convention) —
    // the server's UpdatePlanning path NREs on null `pause1Id` when the row
    // has off-grid actual stamps, but that's an orthogonal Phase 0-4 path
    // and out of scope for this PR. Setting pause sidesteps it entirely.
    await page.locator('[data-testid="start1StartedAt"]').click();
    await pickTime(page, OFFGRID_TIMES.shift1Start);
    await expect(page.locator('[data-testid="start1StartedAt"]')).toHaveValue(OFFGRID_TIMES.shift1Start);

    await page.locator('[data-testid="stop1StoppedAt"]').click();
    await pickTime(page, OFFGRID_TIMES.shift1End);
    await expect(page.locator('[data-testid="stop1StoppedAt"]')).toHaveValue(OFFGRID_TIMES.shift1End);

    await page.locator('[data-testid="pause1Id"]').click();
    await pickTime(page, OFFGRID_TIMES.break);
    await expect(page.locator('[data-testid="pause1Id"]')).toHaveValue(OFFGRID_TIMES.break);

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
    await page.waitForTimeout(1000);

    // Phase 4 second-precision DISPLAY assertion: cell text must include
    // HH:mm:ss for both the start and the stop stamp. The user picked
    // shift1Start / shift1End, the form-binding stores `:00` seconds, the
    // dashboard renders via formatStamp(...) which returns `HH:mm:ss` on
    // flag-on rows.
    const firstShiftActualLocator = page.locator('#firstShiftActual3_0');
    await firstShiftActualLocator.scrollIntoViewIfNeeded();
    await expect(firstShiftActualLocator).toContainText(`${OFFGRID_TIMES.shift1Start}:00`, { timeout: 15000 });
    await expect(firstShiftActualLocator).toContainText(`${OFFGRID_TIMES.shift1End}:00`);
    // Negative guard — the legacy 5-min path would render bare HH:mm.
    // (We can't simply assert ".not.toContainText('08:01 ')" because the
    // HH:mm:ss form is a strict superset; instead verify seconds exist.)

    // Re-open the cell and assert form-input values round-tripped at HH:mm.
    await page.locator(cellId).scrollIntoViewIfNeeded();
    await page.locator(cellId).click();
    await expect(page.locator('#planHours')).toBeVisible();

    await expect(page.locator('[data-testid="start1StartedAt"]')).toHaveValue(OFFGRID_TIMES.shift1Start);
    await expect(page.locator('[data-testid="stop1StoppedAt"]')).toHaveValue(OFFGRID_TIMES.shift1End);
    await expect(page.locator('[data-testid="pause1Id"]')).toHaveValue(OFFGRID_TIMES.break);

    // Planned shifts 1-5 must have round-tripped too — the FU-B premise
    // is that filling all five before setting actual stamps keeps the
    // PUT body fully populated, so the round-trip should preserve every
    // shift cell.
    for (const s of allFivePlannedShifts) {
      await expect(
        page.locator(`[data-testid="plannedStartOfShift${s.id}"]`),
        `shift ${s.id} planned start should round-trip`,
      ).toHaveValue(s.start);
      await expect(
        page.locator(`[data-testid="plannedEndOfShift${s.id}"]`),
        `shift ${s.id} planned end should round-trip`,
      ).toHaveValue(s.end);
    }

    await page.locator('#cancelButton').click();
  });
});
