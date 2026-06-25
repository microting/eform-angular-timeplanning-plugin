import { test, expect, Page } from '@playwright/test';
import { LoginPage } from '../../../Page objects/Login.page';

/**
 * b shard (flag-off, 5-minute grid): P0 round-trip for the admin pause override
 * (Approach C). This is the round-trip that was previously MISSING — earlier
 * specs only verified the pause picker accepted a value, never that the edited
 * pause (and the resulting netto) SURVIVED save + reopen through the new
 * Pause{N}OverrideMinutes channel.
 *
 * Flow: open a last-week cell, set a known start/stop, set the pause to a known
 * value, save, re-open, and assert the pause picker AND #nettoHours reflect the
 * edited value. The override is non-destructive server-side, so the displayed
 * pause must equal the override the admin typed.
 */

const formatDate = (date: Date): string => {
  const day = date.getDate();
  const month = date.getMonth() + 1;
  const year = date.getFullYear();
  return `${day}.${month}.${year}`;
};

const getMonday = (baseDate: Date): Date => {
  const dayOfWeek = baseDate.getDay();
  const diffToMonday = dayOfWeek === 0 ? -6 : 1 - dayOfWeek;
  const monday = new Date(baseDate);
  monday.setDate(baseDate.getDate() + diffToMonday);
  return monday;
};

const today = new Date();
const lastWeekBase = new Date(today);
lastWeekBase.setDate(today.getDate() - 7);
const lastWeekMonday = getMonday(lastWeekBase);

async function waitForSpinner(page: Page) {
  if (await page.locator('.overlay-spinner').count() > 0) {
    await page.locator('.overlay-spinner').waitFor({ state: 'hidden', timeout: 30000 });
  }
}

// Position-based clock-face picker driver. The brittle
// `[style="transform: rotateZ(...deg) translateX(-50%);"] > span` selector is
// overlapped and times out; the repo standard (and the passing l1m flag-on
// counterpart) computes the hand angle and clicks `.clock-face` at the
// resulting coordinate. This drives the same way for both the start/stop time
// pickers and the pause picker. Math mirrors
// l1m/dashboard-edit-pause-override.spec.ts.
async function pickFiveMinute(page: Page, testid: string, timeStr: string) {
  await page.locator(`[data-testid="${testid}"]`).click();
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

async function openCell(page: Page, cellId: string) {
  await page.locator('mat-nested-tree-node').filter({ hasText: 'Timeregistrering' }).click();
  const indexUpdatePromise = page.waitForResponse(
    r => r.url().includes('/api/time-planning-pn/plannings/index') && r.request().method() === 'POST'
  );
  await page.locator('mat-tree-node').filter({ hasText: 'Dashboard' }).click();
  await page.locator('#backwards').click();
  await indexUpdatePromise;
  await waitForSpinner(page);
  await page.locator(cellId).waitFor({ state: 'visible', timeout: 15000 });
  await page.locator(cellId).scrollIntoViewIfNeeded();
  await page.locator(cellId).click();
  await page.locator('#planHours').waitFor({ state: 'visible', timeout: 15000 });
}

async function saveAndAwait(page: Page) {
  const updatePromise = page.waitForResponse(r =>
    r.url().includes('/api/time-planning-pn/plannings/') && r.request().method() === 'PUT');
  const reindexPromise = page.waitForResponse(r =>
    r.url().includes('/api/time-planning-pn/plannings/index') && r.request().method() === 'POST');
  await page.locator('#saveButton').click();
  const updateResponse = await updatePromise;
  await reindexPromise;
  await waitForSpinner(page);
  await page.waitForTimeout(1000);
  return updateResponse;
}

test.describe('Dashboard pause-override round-trip (b, flag-off)', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('http://localhost:4200');
    await new LoginPage(page).login();
  });

  test('edited pause survives save + reopen via Pause{N}OverrideMinutes', async ({ page }) => {
    const cellId = '#cell3_0';

    await openCell(page, cellId);

    // Known shift-1 actual: 08:00 - 16:00 with a 00:30 pause.
    await pickFiveMinute(page, 'start1StartedAt', '08:00');
    await expect(page.locator('[data-testid="start1StartedAt"]')).toHaveValue('08:00');
    await pickFiveMinute(page, 'stop1StoppedAt', '16:00');
    await expect(page.locator('[data-testid="stop1StoppedAt"]')).toHaveValue('16:00');
    await pickFiveMinute(page, 'pause1Id', '00:30');
    await expect(page.locator('[data-testid="pause1Id"]')).toHaveValue('00:30');

    // Netto before save: 8h span - 0.5h pause = 7.50.
    await expect(page.locator('#nettoHours')).toHaveValue('7.50');

    const res = await saveAndAwait(page);
    expect(res.status(), 'PUT must succeed').toBeLessThan(400);

    // Re-open and assert the override-derived pause and netto round-tripped.
    await page.locator(cellId).scrollIntoViewIfNeeded();
    await page.locator(cellId).click();
    await expect(page.locator('#planHours')).toBeVisible();
    await expect(
      page.locator('[data-testid="pause1Id"]'),
      'pause must round-trip 00:30 via the override channel',
    ).toHaveValue('00:30');
    await expect(page.locator('#nettoHours')).toHaveValue('7.50');

    await page.locator('#cancelButton').click();
  });
});
