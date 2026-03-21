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
  const hourDegrees = 360 / 12 * (parseInt(hour) % 12);
  if (hourDegrees === 0) {
    // Hour 0 / 12 is at 720deg (inner ring)
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
  });

  test('should edit time planned in last week', async ({ page }) => {
    // Planned time
    await page.locator('#cell0_0').click();

    await setTimepickerValue(page, 'plannedStartOfShift1', '00', '00');
    await setTimepickerValue(page, 'plannedEndOfShift1', '1', '00');

    await page.locator('.timepicker-button span').filter({ hasText: 'Ok' }).click({ force: true });
    await expect(page.locator('[data-testid="plannedEndOfShift1"]')).toHaveValue('01:00');
    await expect(page.locator('#planHours')).toHaveValue('1');
    const savePromise = page.waitForResponse(
      r => r.url().includes('/api/time-planning-pn/plannings/') && r.request().method() === 'PUT'
    );
    await page.locator('#saveButton').click();
    await savePromise;
    await page.waitForTimeout(1000);
  });

  test('should edit time registration in last week', async ({ page }) => {
    // Registrar time
    await page.locator('#cell0_0').click();

    await setTimepickerValue(page, 'start1StartedAt', '00', '00');
    await setTimepickerValue(page, 'stop1StoppedAt', '1', '00');

    await page.locator('.timepicker-button span').filter({ hasText: 'Ok' }).click({ force: true });
    await page.waitForTimeout(1000);
    await expect(page.locator('[data-testid="stop1StoppedAt"]')).toHaveValue('01:00');
    const savePromise = page.waitForResponse(
      r => r.url().includes('/api/time-planning-pn/plannings/') && r.request().method() === 'PUT'
    );
    await page.locator('#saveButton').click();
    await savePromise;
    await page.waitForTimeout(1000);
  });

  test.afterEach(async ({ page }) => {
    await page.locator('#cell0_0').waitFor({ state: 'visible', timeout: 15000 });
    await page.locator('#cell0_0').scrollIntoViewIfNeeded();
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

    const savePromise = page.waitForResponse(
      r => r.url().includes('/api/time-planning-pn/plannings/') && r.request().method() === 'PUT'
    );
    await page.locator('#saveButton').click();
    await savePromise;
    await page.waitForTimeout(1000);
  });
});
