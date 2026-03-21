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

  // Click hour on the timepicker face
  const hourDegrees = 360 / 12 * parseInt(hour);
  if (hourDegrees === 0) {
    await page.locator('[style="height: 85px; transform: rotateZ(720deg) translateX(-50%);"] > span').click();
  } else {
    await page.locator(`[style="transform: rotateZ(${hourDegrees}deg) translateX(-50%);"] > span`).click();
  }

  // Click minute on the timepicker face
  const minuteDegrees = 360 / 60 * parseInt(minute);
  if (minuteDegrees > 0) {
    await page.locator(`[style="transform: rotateZ(${minuteDegrees}deg) translateX(-50%);"] > span`).click({ force: true });
  }

  await page.waitForTimeout(1000);
  await page.locator('.timepicker-button span').filter({ hasText: 'Ok' }).click();
};

// Get error message for a given input path
const assertInputError = async (page: import('@playwright/test').Page, errorTestId: string, expectedMessage: string) => {
  await page.waitForTimeout(1000);
  await expect(
    page.locator(`[data-testid="${errorTestId}"]`)
  ).toBeVisible();
  await expect(
    page.locator(`[data-testid="${errorTestId}"]`)
  ).toContainText(expectedMessage);
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

  test('should show an error when planned break is longer than the shift duration', async ({ page }) => {
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
  test('should show an error if planned Shift 2 starts before planned Shift 1 ends', async ({ page }) => {
    await setTimepickerValue(page, 'plannedStartOfShift1', '8', '00');
    await setTimepickerValue(page, 'plannedEndOfShift1', '12', '00');
    await setTimepickerValue(page, 'plannedStartOfShift2', '11', '00');
    await assertInputError(page, 'plannedStartOfShift2-Error', 'Start kan ikke være tidligere end stop for den forrige skift');
  });

  test('should show an error if actual Shift 2 starts before actual Shift 1 ends', async ({ page }) => {
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
