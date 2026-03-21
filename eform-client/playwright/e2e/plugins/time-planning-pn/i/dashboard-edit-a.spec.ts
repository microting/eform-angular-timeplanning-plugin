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
  // Wait for the timepicker overlay to fully close
  await page.locator('.cdk-overlay-backdrop').waitFor({ state: 'hidden', timeout: 10000 }).catch(() => {});
  await page.waitForTimeout(500);
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
    // Planned time — use 12h-clock-compatible values (8:00 to 4:00 = 8h shift)
    await page.locator('#cell0_0').click();

    await setTimepickerValue(page, 'plannedStartOfShift1', '8', '00');
    await setTimepickerValue(page, 'plannedEndOfShift1', '4', '00');

    await page.waitForTimeout(1000);

    const savePromise = page.waitForResponse(
      r => r.url().includes('/api/time-planning-pn/plannings/') && r.request().method() === 'PUT'
    );
    await page.locator('#saveButton').click();
    await savePromise;

    await page.locator('#cell0_0').click();

    // Read the exact value from #flexToDate and dynamically calculate paidOutFlex
    const flexToDateEl = page.locator('#flexToDate');
    const rawVal = await flexToDateEl.evaluate((el: HTMLInputElement | HTMLElement) => {
      return (el as HTMLInputElement).value !== undefined ? (el as HTMLInputElement).value : el.textContent;
    });
    const cleaned = (rawVal || '').trim().replace(',', '.');
    const flexToDate = parseFloat(cleaned || '0');
    // Read planHours to know the actual shift duration the system calculated
    const planHoursEl = page.locator('#planHours');
    const planHoursRaw = await planHoursEl.evaluate((el: HTMLInputElement | HTMLElement) => {
      return (el as HTMLInputElement).value !== undefined ? (el as HTMLInputElement).value : el.textContent;
    });
    const planHours = parseFloat((planHoursRaw || '0').trim().replace(',', '.'));
    const actualValue = (flexToDate - planHours).toFixed(2);

    await page.locator('#paidOutFlex').scrollIntoViewIfNeeded();
    await expect(page.locator('#paidOutFlex')).toBeVisible();
    await page.locator('#paidOutFlex').clear();
    await page.locator('#paidOutFlex').fill(actualValue);

    const savePromise2 = page.waitForResponse(
      r => r.url().includes('/api/time-planning-pn/plannings/') && r.request().method() === 'PUT'
    );
    await page.locator('#saveButton').click({ force: true });
    await savePromise2;

    await page.locator('#cell0_0').click();
    await page.waitForTimeout(1000);

    await expect(page.locator('#flexIncludingToday')).toHaveValue('0.00');

    await page.locator('#saveButton').click();
  });

  test.afterEach(async ({ page }) => {
    await page.locator('#cell0_0').click();

    for (const selector of ['plannedStartOfShift1', 'plannedEndOfShift1', 'start1StartedAt', 'stop1StoppedAt']) {
      const newSelector = `[data-testid="${selector}"]`;
      await page.locator(newSelector)
        .locator('xpath=ancestor::div[contains(@class,"flex-row")]')
        .locator('button mat-icon')
        .filter({ hasText: 'delete' })
        .click({ force: true });
      await page.waitForTimeout(500);
    }

    await page.locator('#paidOutFlex').scrollIntoViewIfNeeded();
    await expect(page.locator('#paidOutFlex')).toBeVisible();
    await page.locator('#paidOutFlex').clear();
    await page.locator('#paidOutFlex').fill('0');

    const savePromise = page.waitForResponse(
      r => r.url().includes('/api/time-planning-pn/plannings/') && r.request().method() === 'PUT'
    );
    await page.locator('#saveButton').click();
    await savePromise;

    const savePromise2 = page.waitForResponse(
      r => r.url().includes('/api/time-planning-pn/plannings/') && r.request().method() === 'PUT'
    );
    await page.locator('#saveButton').click();
    await savePromise2;
    await page.waitForTimeout(1000);
  });
});
