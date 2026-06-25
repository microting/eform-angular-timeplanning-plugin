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

// 5-minute clock-face picker driver (mirrors dashboard-edit-b.spec.ts).
async function pickFiveMinute(page: Page, testid: string, timeStr: string) {
  await page.locator(`[data-testid="${testid}"]`).click();
  const hours = parseInt(timeStr.split(':')[0], 10);
  const minutes = parseInt(timeStr.split(':')[1], 10);
  const degrees = (360 / 12) * hours;
  const minuteDegrees = (360 / 60) * minutes;
  if (degrees > 360) {
    await page.locator(`[style="height: 85px; transform: rotateZ(${degrees}deg) translateX(-50%);"] > span`).click();
  } else if (degrees === 0) {
    await page.locator('[style="height: 85px; transform: rotateZ(720deg) translateX(-50%);"] > span').click();
  } else {
    await page.locator(`[style="transform: rotateZ(${degrees}deg) translateX(-50%);"] > span`).click();
  }
  if (minuteDegrees > 0) {
    await page.locator(`[style="transform: rotateZ(${minuteDegrees}deg) translateX(-50%);"] > span`).click({ force: true });
  } else {
    await page.locator('[style="transform: rotateZ(360deg) translateX(-50%);"] > span').click();
  }
  await page.locator('.timepicker-button span').filter({ hasText: 'Ok' }).click();
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
