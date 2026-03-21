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

  // Wait for clock face to appear
  const clockFace = page.locator('.clock-face');
  await clockFace.waitFor({ state: 'visible', timeout: 5000 });
  const box = await clockFace.boundingBox();
  if (!box) throw new Error('Clock face not found');
  const centerX = box.x + box.width / 2;
  const centerY = box.y + box.height / 2;

  // Click hour on clock face using coordinates
  // For 24h format: hours 1-12 outer ring (~105px), 0 and 13-23 inner ring (~70px)
  const hourNum = parseInt(hour);
  const hourAngle = (hourNum % 12) * 30;
  const isInnerRing = hourNum === 0 || hourNum > 12;
  const hourRadius = isInnerRing ? 70 : 105;
  const hourRad = hourAngle * Math.PI / 180;
  await page.mouse.click(
    centerX + hourRadius * Math.sin(hourRad),
    centerY - hourRadius * Math.cos(hourRad)
  );

  await page.waitForTimeout(500);

  // Click minute on clock face using coordinates
  const minuteNum = parseInt(minute);
  const minuteAngle = minuteNum * 6;
  const minuteRadius = 105;
  const minuteRad = minuteAngle * Math.PI / 180;
  await page.mouse.click(
    centerX + minuteRadius * Math.sin(minuteRad),
    centerY - minuteRadius * Math.cos(minuteRad)
  );

  await page.waitForTimeout(500);
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

    // Ensure any lingering overlay is dismissed
    await page.keyboard.press('Escape');
    await page.waitForTimeout(1000);

    // Wait for save button to become enabled
    await page.locator('#saveButton:not([disabled])').waitFor({ timeout: 15000 });

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
    // Dismiss any lingering overlay
    await page.keyboard.press('Escape');
    await page.locator('.cdk-overlay-backdrop').waitFor({ state: 'hidden', timeout: 5000 }).catch(() => {});
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
