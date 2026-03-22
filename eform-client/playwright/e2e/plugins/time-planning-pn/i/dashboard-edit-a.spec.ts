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
    // Planned time — use outer ring hours only (1-12) for reliable clock interaction
    await page.locator('#cell0_0').click();

    await setTimepickerValue(page, 'plannedStartOfShift1', '2', '00');
    await expect(page.locator('[data-testid="plannedStartOfShift1"]')).toHaveValue('02:00', { timeout: 5000 });
    await setTimepickerValue(page, 'plannedEndOfShift1', '10', '00');
    await expect(page.locator('[data-testid="plannedEndOfShift1"]')).toHaveValue('10:00', { timeout: 5000 });

    // Ensure any lingering overlay is dismissed
    await page.keyboard.press('Escape');
    await page.locator('.cdk-overlay-backdrop').waitFor({ state: 'hidden', timeout: 5000 }).catch(() => {});
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
