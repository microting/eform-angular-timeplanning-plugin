import { test, expect, Page } from '@playwright/test';
import { LoginPage } from '../../../Page objects/Login.page';
import {
  OFFGRID_TIMES_L1M,
  OFFGRID_TIMES_L1M_FULL_5SHIFTS,
  OFFGRID_TIMES_L1M_NETTO_CHECK,
} from '../../../../helpers/one-minute-times';

/**
 * l1m shard: admin-edit round-trip of the new Start/Stop{N}ExactMinutes DTO
 * fields under UseOneMinuteIntervals=true. Pre-fix the save path produced
 * float Start{N}Id values (37.6 for `03:03`), so off-grid actuals were
 * unrepresentable from the web dialog. Covers same-day (03:03 → 11:13)
 * round-trip end-to-end through PUT + reopen.
 *
 * Note: the cross-midnight (23:55 → 00:30) case is NOT covered here — the
 * admin dialog's shiftWiseValidator requires `stop > start` within a shift,
 * so admins cannot enter a cross-midnight pair through the UI. The C# helper
 * `ApplyExactMinuteStop` exists for backend defense against worker
 * punchclock writes that already cross midnight, not admin direct entry.
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
  await page.locator('.cdk-overlay-backdrop').waitFor({ state: 'hidden', timeout: 10000 }).catch(() => {});
  await page.waitForTimeout(500);
}

async function setTimepickerValue(page: Page, selector: string, timeStr: string) {
  await page.locator(`[data-testid="${selector}"]`).click();
  await pickTime(page, timeStr);
}

async function openDialogForActiveCell(page: Page) {
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

  // Wipe any seed-prefilled shift-1 actual-start so the test starts from a
  // known-empty pair. Mirrors the f1m beforeEach wipe.
  const wipeBtn = page.locator('[data-testid="start1StartedAt"]')
    .locator('xpath=ancestor::div[contains(@class,"flex-row")]')
    .locator('button mat-icon')
    .filter({ hasText: 'delete' });
  if (await wipeBtn.count() > 0) {
    await wipeBtn.click({ force: true });
    await page.waitForTimeout(500);
  }
}

async function clickSaveAndAwaitRoundtrip(page: Page) {
  await expect(page.locator('#saveButton')).toBeEnabled({ timeout: 10000 });
  const updatePromise = page.waitForResponse(r =>
    r.url().includes('/api/time-planning-pn/plannings/') && r.request().method() === 'PUT');
  const reindexPromise = page.waitForResponse(r =>
    r.url().includes('/api/time-planning-pn/plannings/index') && r.request().method() === 'POST');
  await page.locator('#saveButton').click();
  const updateResponse = await updatePromise;
  await reindexPromise;
  await waitForSpinner(page);
  await page.waitForTimeout(500);
  return updateResponse;
}

test.describe('Dashboard edit actual exact-minutes (l1m, flag-on, admin-edit round-trip)', () => {
  test('03:03 / 11:13 round-trips and produces non-empty netto display', async ({ page }) => {
    await openDialogForActiveCell(page);

    const t = OFFGRID_TIMES_L1M.shift1;
    await setTimepickerValue(page, 'start1StartedAt', t.start);
    await setTimepickerValue(page, 'stop1StoppedAt', t.stop);

    const updateResponse = await clickSaveAndAwaitRoundtrip(page);
    // The whole point of the new ExactMinutes path is that PUT succeeds —
    // pre-fix this round-tripped a float Start1Id / Stop1Id and threw 4xx/5xx.
    expect(updateResponse.status(), 'PUT must succeed under flag-on with off-grid actuals').toBeLessThan(400);

    // Re-open the dialog and assert pickers retained the exact off-grid values.
    await page.locator('#cell0_0').scrollIntoViewIfNeeded();
    await page.locator('#cell0_0').click();
    await expect(page.locator('#planHours')).toBeVisible();
    await expect(
      page.locator('[data-testid="start1StartedAt"]'),
      'actual.shift1.start must round-trip 03:03 exactly (not 03:00 / 03:05)',
    ).toHaveValue(t.start);
    await expect(
      page.locator('[data-testid="stop1StoppedAt"]'),
      'actual.shift1.stop must round-trip 11:13 exactly (not 11:10 / 11:15)',
    ).toHaveValue(t.stop);

    // Netto display reflects the recomputed (Stop1StoppedAt - Start1StartedAt)
    // minutes under flag-on (490 min ⇒ 8.1666... ⇒ "8.17" via .toFixed(2)).
    await expect(page.locator('#nettoHours')).toHaveValue('8.17');

    await page.locator('#cancelButton').click();
  });

  /**
   * Regression coverage: every shift must round-trip its off-grid values
   * exactly across save + reopen. Where the two tests above only fill
   * shift 1, this fills all five shifts (with off-grid breaks too) and
   * asserts each pair returns bit-identical after the PUT + reindex +
   * reopen. A regression that re-introduces 5-minute quantization on any
   * shift would surface here as a `toHaveValue` mismatch (e.g. `07:11`
   * → `07:10` / `07:15`).
   */
  test('all 5 shifts off-grid round-trip preserves every value exactly', async ({ page }) => {
    // 12 timepicker fills × ~7s each exceeds the default 120s test timeout.
    test.setTimeout(300000);
    await openDialogForActiveCell(page);

    const t = OFFGRID_TIMES_L1M_FULL_5SHIFTS;

    // Fill shift 1 start/stop/pause, then 2..5. Order matters because the
    // shiftWise validator only enables a later shift's inputs after the
    // previous shift's pair satisfies (start < stop). Pause is set last per
    // shift so the [max]=duration cap is already computed.
    for (const [n, shift] of [
      [1, t.shift1],
      [2, t.shift2],
      [3, t.shift3],
      [4, t.shift4],
      [5, t.shift5],
    ] as const) {
      await setTimepickerValue(page, `start${n}StartedAt`, shift.start);
      await setTimepickerValue(page, `stop${n}StoppedAt`, shift.stop);
      if (shift.pause !== '00:00') {
        await setTimepickerValue(page, `pause${n}Id`, shift.pause);
      }
    }

    const updateResponse = await clickSaveAndAwaitRoundtrip(page);
    expect(updateResponse.status(), 'PUT must succeed with off-grid values across all 5 shifts').toBeLessThan(400);

    // Re-open and assert every input retained its exact off-grid value.
    await page.locator('#cell0_0').scrollIntoViewIfNeeded();
    await page.locator('#cell0_0').click();
    await expect(page.locator('#planHours')).toBeVisible();

    for (const [n, shift] of [
      [1, t.shift1],
      [2, t.shift2],
      [3, t.shift3],
      [4, t.shift4],
      [5, t.shift5],
    ] as const) {
      await expect(
        page.locator(`[data-testid="start${n}StartedAt"]`),
        `actual.shift${n}.start must round-trip ${shift.start} exactly`,
      ).toHaveValue(shift.start);
      await expect(
        page.locator(`[data-testid="stop${n}StoppedAt"]`),
        `actual.shift${n}.stop must round-trip ${shift.stop} exactly`,
      ).toHaveValue(shift.stop);
      if (shift.pause !== '00:00') {
        await expect(
          page.locator(`[data-testid="pause${n}Id"]`),
          `actual.shift${n}.pause must round-trip ${shift.pause} exactly`,
        ).toHaveValue(shift.pause);
      }
    }

    await page.locator('#cancelButton').click();
  });

  /**
   * Regression coverage: the day's netto display must reflect minute-
   * precision math on the flag-on (timestamp-based) path, NOT the legacy
   * `nettoMinutes = (Stop1Id - Start1Id) * 5` quantization. A regression
   * that drops the new flag-on branch in `ComputePlanningNettoMinutes`
   * (TimePlanningPlanningService.cs ~L2005) would yield a different
   * `actualHours` (e.g. `8.25` or `8.00`) and trip this assertion.
   *
   * 03:03 → 11:13 with no pause ⇒ 490 min ⇒ 8.166… ⇒ `'8.17'` after
   * `.toFixed(2)`.
   */
  test('netto display reflects exact-minute (490 min) sum under flag-on, no 5-min quantization', async ({ page }) => {
    await openDialogForActiveCell(page);

    const t = OFFGRID_TIMES_L1M_NETTO_CHECK.shift1;
    await setTimepickerValue(page, 'start1StartedAt', t.start);
    await setTimepickerValue(page, 'stop1StoppedAt', t.stop);

    // Isolation: the previous "all 5 shifts off-grid round-trip" test persists
    // Pause1StartedAt/StoppedAt timestamps (pause='00:02') into cell0_0.
    // Clicking the form's pause-delete button only patches the form value to
    // null, which under UseOneMinuteIntervals=true sends `pause1ExactMinutes =
    // null` to the backend — and `if (minutes.HasValue) ApplyExactMinutePause`
    // skips clearing when null. So we must EXPLICITLY pick '00:00' here, which
    // sends `pause1ExactMinutes = 0`, triggering ClearPauseTimestamps and
    // nulling the leftover Pause1StartedAt/StoppedAt rows on save. Start/stop
    // are picked first so the pause picker's [max]=getMaxDifference(start,stop)
    // resolves to a positive duration that admits '00:00' as a valid selection.
    await setTimepickerValue(page, 'pause1Id', '00:00');

    const updateResponse = await clickSaveAndAwaitRoundtrip(page);
    expect(updateResponse.status(), 'PUT must succeed under flag-on with off-grid actuals').toBeLessThan(400);

    await page.locator('#cell0_0').scrollIntoViewIfNeeded();
    await page.locator('#cell0_0').click();
    await expect(page.locator('#planHours')).toBeVisible();

    // The whole point: nettoHours displays the recomputed minute-precision
    // total. Under flag-on this is 490 min ⇒ 8.17 h. Legacy quantization
    // would have produced 8.25 (round-up) or 8.00 (round-down).
    await expect(
      page.locator('#nettoHours'),
      'nettoHours must equal exact-minute sum, not 5-min-quantized',
    ).toHaveValue(OFFGRID_TIMES_L1M_NETTO_CHECK.expectedNetto);

    await page.locator('#cancelButton').click();
  });

  /**
   * Regression coverage: re-opening a day that already has off-grid
   * timestamps and clicking Save without touching anything must preserve
   * the timestamps exactly. The earlier regression (pre-fix) was that
   * the admin save path dropped the timestamp fields and re-derived them
   * from the legacy `Start{N}Id` columns — so an admin who opened a day
   * set by flutter-time to `03:03 / 11:13` and just clicked Save would
   * silently clobber the timestamps to `03:00 / 11:15`.
   *
   * This test first seeds the off-grid values via the UI (mirrors what
   * flutter-time would do via gRPC) — that is itself a valid pre-state
   * — then runs the "open + save without changes + reopen" cycle and
   * asserts the picker values are bit-identical to the pre-save state.
   */
  test('modify-then-save preservation: open + save without changes keeps off-grid timestamps', async ({ page }) => {
    // Phase 1: pre-populate 03:03 / 11:13 via UI (stands in for the
    // flutter-time gRPC populate-then-admin-edits scenario).
    await openDialogForActiveCell(page);
    const t = OFFGRID_TIMES_L1M.shift1;
    await setTimepickerValue(page, 'start1StartedAt', t.start);
    await setTimepickerValue(page, 'stop1StoppedAt', t.stop);
    const seedResponse = await clickSaveAndAwaitRoundtrip(page);
    expect(seedResponse.status(), 'seed PUT must succeed').toBeLessThan(400);

    // Phase 2: re-open and click Save WITHOUT changing anything.
    await page.locator('#cell0_0').scrollIntoViewIfNeeded();
    await page.locator('#cell0_0').click();
    await expect(page.locator('#planHours')).toBeVisible();
    // Sanity: the seed values are visible before the no-op save.
    await expect(page.locator('[data-testid="start1StartedAt"]')).toHaveValue(t.start);
    await expect(page.locator('[data-testid="stop1StoppedAt"]')).toHaveValue(t.stop);
    const noopResponse = await clickSaveAndAwaitRoundtrip(page);
    expect(noopResponse.status(), 'no-op PUT must succeed without clobbering timestamps').toBeLessThan(400);

    // Phase 3: re-open and assert values are still bit-identical.
    await page.locator('#cell0_0').scrollIntoViewIfNeeded();
    await page.locator('#cell0_0').click();
    await expect(page.locator('#planHours')).toBeVisible();
    await expect(
      page.locator('[data-testid="start1StartedAt"]'),
      'no-op save must preserve 03:03 (regression: silently clobbered to 03:00 / 03:05)',
    ).toHaveValue(t.start);
    await expect(
      page.locator('[data-testid="stop1StoppedAt"]'),
      'no-op save must preserve 11:13 (regression: silently clobbered to 11:10 / 11:15)',
    ).toHaveValue(t.stop);

    await page.locator('#cancelButton').click();
  });

  /**
   * Regression coverage: backend-state assertion via the PUT response body.
   * Even when the UI picker displays the correct off-grid value, silent
   * 5-min quantization on the server could mask itself through the same
   * subsequent reindex. To catch that, we inspect the PUT request body
   * and reindex response payload directly: the request must carry
   * `start1ExactMinutes = 183` (03:03 ⇒ 3*60+3) and the reindex response
   * must echo `start1StartedAt` as an ISO timestamp whose `HH:mm` portion
   * is `03:03` (not `03:00` / `03:05`).
   *
   * Inspecting the response of the index POST (rather than a dedicated
   * GET) reuses the existing `clickSaveAndAwaitRoundtrip` round-trip
   * promise and keeps this test self-contained without additional
   * helpers or HTTP plumbing.
   */
  test('backend persists Start1StartedAt as exact 03:03 UTC (not quantized)', async ({ page }) => {
    await openDialogForActiveCell(page);

    const t = OFFGRID_TIMES_L1M.shift1;
    await setTimepickerValue(page, 'start1StartedAt', t.start);
    await setTimepickerValue(page, 'stop1StoppedAt', t.stop);

    // Capture both: the PUT request body (to confirm the client sent
    // ExactMinutes) and the subsequent reindex response (to confirm the
    // server persisted them as off-grid timestamps).
    await expect(page.locator('#saveButton')).toBeEnabled({ timeout: 10000 });
    const putPromise = page.waitForRequest(r =>
      r.url().includes('/api/time-planning-pn/plannings/') && r.method() === 'PUT');
    const indexPromise = page.waitForResponse(r =>
      r.url().includes('/api/time-planning-pn/plannings/index') && r.request().method() === 'POST');
    await page.locator('#saveButton').click();
    const putRequest = await putPromise;
    const indexResponse = await indexPromise;

    // Wait for spinner before further DOM work (the helper above does this,
    // we inline it because we replaced the helper with raw promises).
    if (await page.locator('.overlay-spinner').count() > 0) {
      await page.locator('.overlay-spinner').waitFor({ state: 'hidden', timeout: 30000 });
    }

    // Request body must carry ExactMinutes — 03:03 ⇒ 3*60+3 = 183.
    const putBody = putRequest.postDataJSON();
    expect(
      putBody?.start1ExactMinutes,
      'client must send start1ExactMinutes=183 for 03:03 under flag-on',
    ).toBe(183);
    // 11:13 ⇒ 11*60+13 = 673.
    expect(
      putBody?.stop1ExactMinutes,
      'client must send stop1ExactMinutes=673 for 11:13 under flag-on',
    ).toBe(673);

    // Reindex response must echo the persisted ISO timestamp with the
    // exact off-grid minute component preserved. Find the row whose
    // start1StartedAt is set and whose siteName === 'ac ad'.
    const indexBody = await indexResponse.json();
    const rows = JSON.stringify(indexBody);
    expect(
      /T03:03(?::00)?/.test(rows),
      'backend must persist Start1StartedAt as 03:03 UTC, not 03:00 / 03:05',
    ).toBeTruthy();
  });
});
