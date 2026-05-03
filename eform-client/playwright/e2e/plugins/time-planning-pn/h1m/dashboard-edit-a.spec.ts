import { test, expect, Page } from '@playwright/test';
import { LoginPage } from '../../../Page objects/Login.page';
import { OFFGRID_TIMES_H1M } from '../../../../helpers/one-minute-times';

/**
 * h1m variant of `h/dashboard-edit-a.spec.ts`: the paid-out-flex decimal
 * round-trip suite, exercised under the `UseOneMinuteIntervals = true` code
 * path. The shard's `post-migration.sql` flips the flag on every active
 * assigned site, so the legacy in-spec setup from `h/` runs against the
 * `minutesGap = 1` timepicker.
 *
 * Where f1m exercises the SAVE-FAILURE / validation regression suite (no
 * save ever fires), h1m's tests SAVE successfully on every iteration — the
 * `afterEach` clicks `#saveButton` and waits for the PUT response. That
 * means the workday-entity dialog must be in a save-able state, and the
 * `plannedShiftDurationValidator` requires shift 1 to have a valid
 * (start < stop) pair before `#saveButton` becomes enabled.
 *
 * Multishift-shape pattern (shared with b1m / c1m / d1m / e1m): on the
 * FIRST test we fill all 5 shifts ascending with off-grid times from
 * `OFFGRID_TIMES_H1M`. Subsequent tests re-open the same `#cell0_0` dialog
 * — the shifts have already been persisted by the prior save, so the
 * dialog is valid as soon as it opens and we go straight to the
 * paid-out-flex assertion. A `firstTest` guard tracks the first run so we
 * only fill shifts once per session; PR 7's matrix entry is single-worker
 * (the playwright matrix runs each shard on its own MariaDB) so the
 * sequential-test ordering guarantees this works.
 *
 * Display assertion note: `#paidOutFlex` is a NUMERIC input — its
 * `toHaveValue` assertions stay as `'1.2'`, `'0'` etc. (no `HH:mm:ss`
 * formatting) since the field has nothing to do with time-of-day. The
 * shift inputs we fill in beforeEach use the `HH:mm` form (timepicker's
 * native display format).
 *
 * Stateful test ordering: the legacy `h/` spec has the same cross-test
 * dependency — test N reads back the value test N-1 saved. h1m preserves
 * that order exactly. If a single test fails the chain breaks; that's
 * intentional and matches `h/`.
 */

async function waitForSpinner(page: Page) {
  if (await page.locator('.overlay-spinner').count() > 0) {
    await page.locator('.overlay-spinner').waitFor({ state: 'hidden', timeout: 30000 });
  }
}

/**
 * Position-based clock-face picker. Identical helper to the b1m / c1m /
 * d1m / e1m / f1m specs — works uniformly for h=0 (break times) unlike
 * rotateZ-selector strategies that fail on the inner-ring `00` position.
 * Includes the f1m-vintage backdrop-hidden wait so consecutive picks
 * don't race on a still-fading overlay.
 */
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
  // Wait for the timepicker overlay to fully close before the next pick.
  await page.locator('.cdk-overlay-backdrop').waitFor({ state: 'hidden', timeout: 10000 }).catch(() => {});
  await page.waitForTimeout(500);
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
  { id: 1 as const, start: OFFGRID_TIMES_H1M.shift1Start, end: OFFGRID_TIMES_H1M.shift1End, break: OFFGRID_TIMES_H1M.break },
  { id: 2 as const, start: OFFGRID_TIMES_H1M.shift2Start, end: OFFGRID_TIMES_H1M.shift2End, break: OFFGRID_TIMES_H1M.break },
  { id: 3 as const, start: OFFGRID_TIMES_H1M.shift3Start, end: OFFGRID_TIMES_H1M.shift3End, break: OFFGRID_TIMES_H1M.break },
  { id: 4 as const, start: OFFGRID_TIMES_H1M.shift4Start, end: OFFGRID_TIMES_H1M.shift4End, break: OFFGRID_TIMES_H1M.break },
  { id: 5 as const, start: OFFGRID_TIMES_H1M.shift5Start, end: OFFGRID_TIMES_H1M.shift5End, break: OFFGRID_TIMES_H1M.break },
];

test.describe('Dashboard edit values (h1m, flag-on, paid-out-flex)', () => {
  // Multishift-shape: only fill the 5 shifts ONCE, on the first test. The
  // workday-entity row is then persisted on save and subsequent test
  // re-opens of `#cell0_0` see a valid form straight away.
  let firstTest = true;

  test.beforeEach(async ({ page }) => {
    await page.goto('http://localhost:4200');
    await new LoginPage(page).login();
    await page.locator('mat-nested-tree-node').filter({ hasText: 'Timeregistrering' }).click();

    const indexUpdatePromise = page.waitForResponse(
      r => r.url().includes('/api/time-planning-pn/plannings/index') && r.request().method() === 'POST'
    );

    await page.locator('mat-tree-node').filter({ hasText: 'Dashboard' }).click();
    await indexUpdatePromise;
    await waitForSpinner(page);

    await page.locator('#workingHoursSite').click();
    await page.locator('.ng-option').filter({ hasText: 'ac ad' }).click();
    // Wait for site-filter re-index to finish before opening the cell — the
    // legacy h spec races a 500ms timeout here, but the (reverted) g1m
    // shard showed that the site-filter trigger detaches the cell row mid-
    // click on flag-on. Wait for the spinner and a subsequent index POST
    // before reaching for `#cell0_0`.
    await waitForSpinner(page);
    await page.waitForTimeout(500);

    await page.locator('#cell0_0').click();
    // Dialog open is async — the workday-entity dialog renders the shift
    // inputs only after `planHours` becomes visible, so gate on that.
    await expect(page.locator('#planHours')).toBeVisible({ timeout: 10000 });

    if (firstTest) {
      // Fill all 5 shifts at 1-minute granularity so `#saveButton` becomes
      // enabled and stays enabled for every subsequent test (the saved
      // shifts round-trip into the dialog on the next #cell0_0 click).
      for (const s of allFiveShifts) {
        await setShift(page, s.id, s.start, s.end, s.break);
      }
      firstTest = false;
    } else {
      // Sanity check that the prior save round-tripped — if shift 1 lost
      // its values, the saveButton would be disabled and the afterEach
      // would silently hang on the PUT promise.
      await expect(
        page.locator('[data-testid="plannedStartOfShift1"]'),
      ).toHaveValue(OFFGRID_TIMES_H1M.shift1Start);
    }
  });

  test('should set paid out flex value', async ({ page }) => {
    await page.locator('#paidOutFlex').clear();
    await page.locator('#paidOutFlex').fill('1.2');
  });

  test('should accepts decimal values with dot', async ({ page }) => {
    await expect(page.locator('#paidOutFlex')).toHaveValue('1.2');
    await page.locator('#paidOutFlex').clear();
    await page.locator('#paidOutFlex').fill('1,2');
  });

  test('should accepts decimal values with comma', async ({ page }) => {
    await expect(page.locator('#paidOutFlex')).toHaveValue('1.2');
    await page.locator('#paidOutFlex').clear();
    await page.locator('#paidOutFlex').fill('1,2');
  });

  test('should accepts whole numbers', async ({ page }) => {
    await expect(page.locator('#paidOutFlex')).toHaveValue('1.2');
    await page.locator('#paidOutFlex').clear();
    await page.locator('#paidOutFlex').fill('0');
  });

  test.afterEach(async ({ page }) => {
    // Wait for saveButton to become enabled BEFORE clicking — never use
    // `force: true`. The plannedShiftDurationValidator only resolves the
    // disabled state asynchronously after the last input blurs.
    await expect(page.locator('#saveButton')).toBeEnabled({ timeout: 10000 });
    const savePromise = page.waitForResponse(
      r => r.url().includes('/api/time-planning-pn/plannings/') && r.request().method() === 'PUT'
    );
    await page.locator('#saveButton').click();
    await savePromise;
    await page.waitForTimeout(1000);
  });
});
