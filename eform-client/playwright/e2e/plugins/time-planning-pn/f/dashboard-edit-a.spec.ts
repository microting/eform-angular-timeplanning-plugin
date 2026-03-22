import { test, expect } from '@playwright/test';
import { LoginPage } from '../../../Login.page';

async function waitForSpinner(page: import('@playwright/test').Page) {
  if (await page.locator('.overlay-spinner').count() > 0) {
    await page.locator('.overlay-spinner').waitFor({ state: 'hidden', timeout: 30000 });
  }
}

const setTimepickerValue = async (page: import('@playwright/test').Page, selector: string, hour: string, minute: string) => {
  const newSelector = `[data-testid="${selector}"]`;
  await page.locator(newSelector).click();

  // Clock face is 290x290px, center at (145, 145)
  const cx = 145;
  const cy = 145;

  // Wait for hour clock face
  const hourClockFace = page.locator('.clock-face');
  await hourClockFace.waitFor({ state: 'visible', timeout: 5000 });

  // Click hour using position relative to clock face element
  const hourNum = parseInt(hour);
  const hourAngle = (hourNum % 12) * 30;
  const isInner = hourNum === 0 || hourNum > 12;
  const hourR = isInner ? 60 : 100;
  const hourRad = hourAngle * Math.PI / 180;
  await hourClockFace.click({
    position: {
      x: Math.round(cx + hourR * Math.sin(hourRad)),
      y: Math.round(cy - hourR * Math.cos(hourRad)) + (Math.abs(Math.cos(hourRad)) < 0.01 ? 1 : 0)
    }
  });

  // Wait for minute face to render (hour component is destroyed, minute component created)
  await page.waitForTimeout(500);
  const minuteClockFace = page.locator('.clock-face');
  await minuteClockFace.waitFor({ state: 'visible', timeout: 5000 });

  // Click minute
  const minuteNum = parseInt(minute);
  const minuteAngle = minuteNum * 6;
  const minuteR = 100;
  const minuteRad = minuteAngle * Math.PI / 180;
  await minuteClockFace.click({
    position: {
      x: Math.round(cx + minuteR * Math.sin(minuteRad)),
      y: Math.round(cy - minuteR * Math.cos(minuteRad)) + (Math.abs(Math.cos(minuteRad)) < 0.01 ? 1 : 0)
    }
  });

  await page.waitForTimeout(500);
  await page.locator('.timepicker-button span').filter({ hasText: 'Ok' }).click();
};

// Get error message for a given input path
const assertInputError = async (page: import('@playwright/test').Page, errorTestId: string, expectedMessage: string) => {
  const errorLocator = page.locator(`[data-testid="${errorTestId}"]`).first();
  await errorLocator.waitFor({ state: 'visible', timeout: 15000 });
  await expect(errorLocator).toContainText(expectedMessage);
};

test.describe('Dashboard edit values', () => {
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
    await setTimepickerValue(page, 'plannedStartOfShift1', '10', '00');
    await setTimepickerValue(page, 'plannedEndOfShift1', '9', '00');
    await assertInputError(page, 'plannedEndOfShift1-Error', 'Stop må ikke være før start');
  });

  test.skip('should show an error when planned break is longer than the shift duration', async ({ page }) => {
    await setTimepickerValue(page, 'plannedStartOfShift1', '1', '00');
    await setTimepickerValue(page, 'plannedEndOfShift1', '10', '00');
    await setTimepickerValue(page, 'plannedBreakOfShift1', '9', '00');
    await assertInputError(page, 'plannedBreakOfShift1-Error', 'Pausen må ikke være lige så lang som eller længere end skiftets varighed');
  });

  test('should show an error when planned start and stop are the same', async ({ page }) => {
    await setTimepickerValue(page, 'plannedStartOfShift1', '9', '00');
    await setTimepickerValue(page, 'plannedEndOfShift1', '9', '00');
    await assertInputError(page, 'plannedEndOfShift1-Error', 'Start og stop kan ikke være det samme');
  });

  // --- Actual Shift Duration Validator ---
  test('should show an error when actual stop time is before start time', async ({ page }) => {
    await setTimepickerValue(page, 'start1StartedAt', '11', '00');
    await setTimepickerValue(page, 'stop1StoppedAt', '9', '00');
    await setTimepickerValue(page, 'pause1Id', '0', '00');
    await assertInputError(page, 'stop1StoppedAt-Error', 'Stop må ikke være før start');
  });

  test('should show an error when actual pause is longer than the shift duration', async ({ page }) => {
    await setTimepickerValue(page, 'start1StartedAt', '8', '00');
    await setTimepickerValue(page, 'stop1StoppedAt', '10', '00');
    await setTimepickerValue(page, 'pause1Id', '2', '00');
    await assertInputError(page, 'pause1Id-Error', 'Pausen må ikke være lige så lang som eller længere end skiftets varighed');
  });

  test('should show an error when actual start and stop are the same', async ({ page }) => {
    await setTimepickerValue(page, 'start1StartedAt', '9', '00');
    await setTimepickerValue(page, 'stop1StoppedAt', '9', '00');
    await setTimepickerValue(page, 'pause1Id', '0', '00');
    await assertInputError(page, 'stop1StoppedAt-Error', 'Start og stop kan ikke være det samme');
  });

  // --- Shift-Wise Validator ---
  test.skip('should show an error if planned Shift 2 starts before planned Shift 1 ends', async ({ page }) => {
    await setTimepickerValue(page, 'plannedStartOfShift1', '8', '00');
    await setTimepickerValue(page, 'plannedEndOfShift1', '12', '00');
    await setTimepickerValue(page, 'plannedStartOfShift2', '11', '00');
    await assertInputError(page, 'plannedStartOfShift2-Error', 'Start kan ikke være tidligere end stop for den forrige skift');
  });

  test.skip('should show an error if actual Shift 2 starts before actual Shift 1 ends', async ({ page }) => {
    await setTimepickerValue(page, 'start1StartedAt', '8', '00');
    await setTimepickerValue(page, 'stop1StoppedAt', '12', '00');
    await setTimepickerValue(page, 'start2StartedAt', '11', '00');
    await assertInputError(page, 'start2StartedAt-Error', 'Start kan ikke være tidligere end stop for den forrige skift');
  });

  test('should select midnight to some hours', async ({ page }) => {
    await setTimepickerValue(page, 'plannedStartOfShift1', '00', '00');
    await setTimepickerValue(page, 'plannedEndOfShift1', '2', '00');
    await setTimepickerValue(page, 'start1StartedAt', '00', '00');
    await setTimepickerValue(page, 'stop1StoppedAt', '2', '00');
    await expect(page.locator('#planHours')).toHaveValue('2');
    await expect(page.locator('#todaysFlex')).toHaveValue('0.00');
  });

  test('should select some hours to midnight', async ({ page }) => {
    await setTimepickerValue(page, 'plannedStartOfShift1', '2', '00');
    await setTimepickerValue(page, 'plannedEndOfShift1', '00', '00');
    await setTimepickerValue(page, 'start1StartedAt', '2', '00');
    await setTimepickerValue(page, 'stop1StoppedAt', '00', '00');
    await expect(page.locator('#planHours')).toHaveValue('22');
    await expect(page.locator('#todaysFlex')).toHaveValue('0.00');
  });
});
