import { test, expect, Page } from '@playwright/test';
import { LoginPage } from '../../../Page objects/Login.page';

/**
 * l1m shard (flag-on, UseOneMinuteIntervals=true): P0 round-trip for the admin
 * pause override (Approach C) on a one-minute site.
 *
 * Pre-Approach-C, the flag-on edit "worked" only because the save path
 * DESTROYED the worker's recorded pause sub-slots (ApplyExactMinutePause /
 * ClearPauseTimestamps). The override replaces that destructive collapse: the
 * admin's typed pause total now rides on Pause{N}OverrideMinutes and the worker
 * sub-slots are preserved server-side. This test asserts the admin-typed pause
 * survives save + reopen exactly (1-minute precision), proving the override —
 * not the legacy Pause{N}Id and not a slot rewrite — is the authoritative
 * channel.
 */

async function waitForSpinner(page: Page) {
  if (await page.locator('.overlay-spinner').count() > 0) {
    await page.locator('.overlay-spinner').waitFor({ state: 'hidden', timeout: 30000 });
  }
}

// Position-based 1-minute clock-face picker (mirrors l1m/dashboard-edit-actual-exact).
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
  await page.locator('#planHours').waitFor({ state: 'visible', timeout: 15000 });
}

async function reopenCell(page: Page) {
  await page.locator('#cell0_0').scrollIntoViewIfNeeded();
  await page.locator('#cell0_0').click();
  await expect(page.locator('#planHours')).toBeVisible();
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

test.describe('Dashboard pause-override round-trip (l1m, flag-on)', () => {
  test('admin-typed pause total round-trips at 1-minute precision', async ({ page }) => {
    test.setTimeout(180000);
    await openDialogForActiveCell(page);

    // Establish a known shift-1 span and set a deliberately off-grid pause
    // total (00:23) so the assertion can only pass if the exact override
    // round-tripped (not a 5-min-quantized value).
    await setTimepickerValue(page, 'start1StartedAt', '08:01');
    await setTimepickerValue(page, 'stop1StoppedAt', '16:11');
    await setTimepickerValue(page, 'pause1Id', '00:23');
    await expect(page.locator('[data-testid="pause1Id"]')).toHaveValue('00:23');

    const res = await clickSaveAndAwaitRoundtrip(page);
    expect(res.status(), 'PUT must succeed under flag-on with an override pause').toBeLessThan(400);

    await reopenCell(page);
    await expect(
      page.locator('[data-testid="pause1Id"]'),
      'pause must round-trip 00:23 exactly via Pause1OverrideMinutes (not 00:20 / 00:25)',
    ).toHaveValue('00:23');

    await page.locator('#cancelButton').click();
  });
});
