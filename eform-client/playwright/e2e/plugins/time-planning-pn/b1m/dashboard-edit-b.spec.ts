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

    // Set the actual stamps for shift 1 with off-grid times.
    await page.locator('[data-testid="start1StartedAt"]').click();
    await pickTime(page, OFFGRID_TIMES.shift1Start);
    await expect(page.locator('[data-testid="start1StartedAt"]')).toHaveValue(OFFGRID_TIMES.shift1Start);

    await page.locator('[data-testid="stop1StoppedAt"]').click();
    await pickTime(page, OFFGRID_TIMES.shift1End);
    await expect(page.locator('[data-testid="stop1StoppedAt"]')).toHaveValue(OFFGRID_TIMES.shift1End);

    // Save.
    const updatePromise = page.waitForResponse(r =>
      r.url().includes('/api/time-planning-pn/plannings/') && r.request().method() === 'PUT');
    const reindexPromise = page.waitForResponse(r =>
      r.url().includes('/api/time-planning-pn/plannings/index') && r.request().method() === 'POST');
    await page.locator('#saveButton').click({ force: true });
    await updatePromise;
    await reindexPromise;
    await waitForSpinner(page);
    await page.waitForTimeout(1000);

    // Phase 4 second-precision DISPLAY assertion: cell text must include
    // HH:mm:ss for both the start and the stop stamp. The user picked
    // 08:01 / 16:08, the form-binding stores `:00` seconds, the dashboard
    // renders via formatStamp(...) which returns `HH:mm:ss` on flag-on rows.
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

    await page.locator('#cancelButton').click();
  });
});
