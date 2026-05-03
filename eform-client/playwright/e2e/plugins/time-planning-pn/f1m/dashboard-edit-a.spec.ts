import { test, expect, Page } from '@playwright/test';
import { LoginPage } from '../../../Page objects/Login.page';
import { OFFGRID_TIMES_F1M } from '../../../../helpers/one-minute-times';

/**
 * f1m variant of the SAVE-FAILURE / validation regression suite cloned from
 * `f/dashboard-edit-a.spec.ts`. Where b1m / c1m / d1m / e1m clone the
 * positive-path multi-shift round-trip from the `b` shard (with five
 * ascending off-grid shifts), f1m exercises the NEGATIVE paths: each test
 * fills shift 1 with an invalid time pair (stop-before-start, same
 * start/stop, pause-longer-than-shift) and asserts the corresponding
 * validator surfaces its Danish error message.
 *
 * Why this matters for the flag-on matrix entry:
 *   The `workday-entity-dialog`'s `plannedShiftDurationValidator` and
 *   `actualShiftDurationValidator` run on raw `HH:mm` form values via
 *   `getMinutes()` — they don't go through the 5-min `convertTimeToMinutes`
 *   storage path. So the validators MUST trigger identical errors whether
 *   inputs land on a 5-min grid (`f`) or off-grid (`f1m`); a regression
 *   that makes the validator depend on the storage quantization would
 *   silently break the flag-on form. f1m guards that contract.
 *
 * Shared with b1m / c1m / d1m / e1m:
 *   • Same baseline seed (`420_eform-angular-time-planning-plugin.sql` and
 *     `420_SDK.sql` are copies of `a/`).
 *   • Same `post-migration.sql` flipping `UseOneMinuteIntervals = 1` for
 *     every active assigned site (the workflow's generic post-migration
 *     step picks this up automatically).
 *
 * What's new in f1m: a dedicated `OFFGRID_TIMES_F1M` block in the shared
 * helper that pairs each validation case with off-grid (non-multiple-of-5)
 * minutes which still preserve the same INVALID RELATIONSHIP that the
 * legacy `f`-shard test relied on. E.g. `'10:23' > '09:17'` trips
 * `invalidRange` exactly the same way `'10:00' > '09:00'` does.
 *
 * Math tests (positive path): the two midnight-wrap cases at the end use
 * an off-grid pair `00:00 ↔ 02:24` (144 min one way, 1296 min the other)
 * so the recomputed `planHours` lands on a clean fractional value
 * (`2.4` / `21.6`) rather than the integer `2` / `22` the legacy `f`
 * shard asserted. `todaysFlex` stays `'0.00'` because actual quantization
 * (still 5-min internally) round-trips symmetrically with the planned
 * value at this exact pair (see comment block on the test for the
 * arithmetic).
 *
 * NOT cloned from `f/`:
 *   • The two `test.skip(...)` cases for break-too-long-on-planned and
 *     shift2-overlapping-shift1 — kept as `.skip` here too so the file
 *     remains a structural mirror.
 */

async function waitForSpinner(page: Page) {
  if (await page.locator('.overlay-spinner').count() > 0) {
    await page.locator('.overlay-spinner').waitFor({ state: 'hidden', timeout: 30000 });
  }
}

/**
 * Position-based clock-face picker. Identical helper to the b1m / c1m /
 * d1m / e1m specs — works uniformly for h=0 (break and midnight times)
 * unlike rotateZ-selector strategies that fail on the inner-ring `00`
 * position.
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

/** Open the timepicker for `selector` and pick `timeStr`. */
async function setTimepickerValue(page: Page, selector: string, timeStr: string) {
  await page.locator(`[data-testid="${selector}"]`).click();
  await pickTime(page, timeStr);
}

/** Wait for and assert a Danish validator error on the given input. */
async function assertInputError(page: Page, errorTestId: string, expectedMessage: string) {
  const errorLocator = page.locator(`[data-testid="${errorTestId}"]`).first();
  await errorLocator.waitFor({ state: 'visible', timeout: 15000 });
  await expect(errorLocator).toContainText(expectedMessage);
}

test.describe('Dashboard edit values (f1m, flag-on, off-grid validation pairs)', () => {
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
    await page.locator('#cell0_0').click();

    // The `a/`-style seed pre-fills shift-1 planned + actual-1 start, so we
    // wipe them via the row-level delete icon to start each test from a
    // known-empty pair (mirrors the legacy `f` shard's beforeEach exactly).
    for (const selector of ['plannedStartOfShift1', 'start1StartedAt']) {
      const newSelector = `[data-testid="${selector}"]`;
      await page.locator(newSelector)
        .locator('xpath=ancestor::div[contains(@class,"flex-row")]')
        .locator('button mat-icon')
        .filter({ hasText: 'delete' })
        .click({ force: true });
      await page.waitForTimeout(500);
    }
  });

  // --- Planned Shift Duration Validator ---
  test('should show an error when planned stop time is before start time', async ({ page }) => {
    const t = OFFGRID_TIMES_F1M.plannedStopBefore;
    await setTimepickerValue(page, 'plannedStartOfShift1', t.start);
    await setTimepickerValue(page, 'plannedEndOfShift1', t.stop);
    await assertInputError(page, 'plannedEndOfShift1-Error', 'Stop må ikke være før start');
  });

  test.skip('should show an error when planned break is longer than the shift duration', async ({ page }) => {
    // Skipped in the legacy `f` shard too — preserved here as `.skip` so the
    // file remains a 1:1 structural mirror of `f/dashboard-edit-a.spec.ts`.
    await setTimepickerValue(page, 'plannedStartOfShift1', '01:03');
    await setTimepickerValue(page, 'plannedEndOfShift1', '10:17');
    await setTimepickerValue(page, 'plannedBreakOfShift1', '09:43');
    await assertInputError(page, 'plannedBreakOfShift1-Error', 'Pausen må ikke være lige så lang som eller længere end skiftets varighed');
  });

  test('should show an error when planned start and stop are the same', async ({ page }) => {
    const t = OFFGRID_TIMES_F1M.plannedSameTime;
    await setTimepickerValue(page, 'plannedStartOfShift1', t.start);
    await setTimepickerValue(page, 'plannedEndOfShift1', t.stop);
    await assertInputError(page, 'plannedEndOfShift1-Error', 'Start og stop kan ikke være det samme');
  });

  // --- Actual Shift Duration Validator ---
  test('should show an error when actual stop time is before start time', async ({ page }) => {
    const t = OFFGRID_TIMES_F1M.actualStopBefore;
    await setTimepickerValue(page, 'start1StartedAt', t.start);
    await setTimepickerValue(page, 'stop1StoppedAt', t.stop);
    await setTimepickerValue(page, 'pause1Id', t.pause);
    await assertInputError(page, 'stop1StoppedAt-Error', 'Stop må ikke være før start');
  });

  test('should show an error when actual pause is longer than the shift duration', async ({ page }) => {
    // 10:31 - 08:13 = 138 min shift duration; 02:47 = 167 min pause.
    // 167 ≥ 138 ⇒ `breakTooLong` validator fires the same as the legacy
    // f-shard pair (10:00-08:00 / 02:00 = 120 ≥ 120).
    const t = OFFGRID_TIMES_F1M.actualPauseTooLong;
    await setTimepickerValue(page, 'start1StartedAt', t.start);
    await setTimepickerValue(page, 'stop1StoppedAt', t.stop);
    await setTimepickerValue(page, 'pause1Id', t.pause);
    await assertInputError(page, 'pause1Id-Error', 'Pausen må ikke være lige så lang som eller længere end skiftets varighed');
  });

  test('should show an error when actual start and stop are the same', async ({ page }) => {
    const t = OFFGRID_TIMES_F1M.actualSameTime;
    await setTimepickerValue(page, 'start1StartedAt', t.start);
    await setTimepickerValue(page, 'stop1StoppedAt', t.stop);
    await setTimepickerValue(page, 'pause1Id', t.pause);
    await assertInputError(page, 'stop1StoppedAt-Error', 'Start og stop kan ikke være det samme');
  });

  // --- Shift-Wise Validator ---
  test.skip('should show an error if planned Shift 2 starts before planned Shift 1 ends', async ({ page }) => {
    // Skipped in the legacy `f` shard too — preserved here as `.skip` so the
    // file remains a 1:1 structural mirror.
    await setTimepickerValue(page, 'plannedStartOfShift1', '08:13');
    await setTimepickerValue(page, 'plannedEndOfShift1', '12:17');
    await setTimepickerValue(page, 'plannedStartOfShift2', '11:29');
    await assertInputError(page, 'plannedStartOfShift2-Error', 'Start kan ikke være tidligere end stop for den forrige skift');
  });

  test.skip('should show an error if actual Shift 2 starts before actual Shift 1 ends', async ({ page }) => {
    // Skipped in the legacy `f` shard too — preserved here as `.skip` so the
    // file remains a 1:1 structural mirror.
    await setTimepickerValue(page, 'start1StartedAt', '08:13');
    await setTimepickerValue(page, 'stop1StoppedAt', '12:17');
    await setTimepickerValue(page, 'start2StartedAt', '11:29');
    await assertInputError(page, 'start2StartedAt-Error', 'Start kan ikke være tidligere end stop for den forrige skift');
  });

  // --- Positive-path math tests (midnight-wrap with off-grid endpoints) ---
  test('should select midnight to some hours', async ({ page }) => {
    // 00:00 → 02:24 ⇒ planned 144 min ⇒ planHours = 2.4.
    // Actual 00:00 → 02:24 ⇒ start1Id = (0/5)+1 = 1; stop1Id = (144/5)+1 = 29.8;
    // actualMin = (29.8 - 1) * 5 = 144 ⇒ actualHours = 2.4.
    // todaysFlex = actualHours - planHours = 2.4 - 2.4 = 0.00.
    const t = OFFGRID_TIMES_F1M.midnightToHours;
    await setTimepickerValue(page, 'plannedStartOfShift1', t.start);
    await setTimepickerValue(page, 'plannedEndOfShift1', t.stop);
    await setTimepickerValue(page, 'start1StartedAt', t.start);
    await setTimepickerValue(page, 'stop1StoppedAt', t.stop);
    await expect(page.locator('#planHours')).toHaveValue(OFFGRID_TIMES_F1M.midnightToHoursPlan);
    await expect(page.locator('#todaysFlex')).toHaveValue(OFFGRID_TIMES_F1M.zeroFlex);
  });

  test('should select some hours to midnight', async ({ page }) => {
    // 02:24 → 00:00 ⇒ midnight-wrap: planned (1440 - 144) + 0 = 1296 min ⇒ planHours = 21.6.
    // Actual 02:24 → 00:00 ⇒ start1Id = (144/5)+1 = 29.8; stop1Id = 289 (isStop && result===0);
    // actualMin = (289 - 29.8) * 5 = 1296 ⇒ actualHours = 21.6.
    // todaysFlex = 21.6 - 21.6 = 0.00.
    const t = OFFGRID_TIMES_F1M.hoursToMidnight;
    await setTimepickerValue(page, 'plannedStartOfShift1', t.start);
    await setTimepickerValue(page, 'plannedEndOfShift1', t.stop);
    await setTimepickerValue(page, 'start1StartedAt', t.start);
    await setTimepickerValue(page, 'stop1StoppedAt', t.stop);
    await expect(page.locator('#planHours')).toHaveValue(OFFGRID_TIMES_F1M.hoursToMidnightPlan);
    await expect(page.locator('#todaysFlex')).toHaveValue(OFFGRID_TIMES_F1M.zeroFlex);
  });
});
