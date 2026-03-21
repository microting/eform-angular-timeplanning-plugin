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

    // clean out shifts
    for (const selector of ['plannedStartOfShift1', 'plannedEndOfShift1', 'start1StartedAt', 'stop1StoppedAt']) {
      const newSelector = `[data-testid="${selector}"]`;
      await page.locator(newSelector)
        .locator('xpath=ancestor::div[contains(@class,"flex-row")]')
        .locator('button mat-icon')
        .filter({ hasText: 'delete' })
        .click({ force: true });
      await page.waitForTimeout(500);
    }

    await setTimepickerValue(page, 'plannedStartOfShift1', '1', '00');
    await setTimepickerValue(page, 'plannedEndOfShift1', '10', '00');
    await page.waitForTimeout(500);
  });

  test('should add a comment', async ({ page }) => {
    await page.locator('#CommentOffice').clear();
    await page.locator('#CommentOffice').fill('test comment');
    const savePromise = page.waitForResponse(
      r => r.url().includes('/api/time-planning-pn/plannings/') && r.request().method() === 'PUT'
    );
    await page.locator('#saveButton').click();
    await savePromise;
  });

  test('should modify a comment', async ({ page }) => {
    await expect(page.locator('#CommentOffice')).toHaveValue('test comment');
    await page.locator('#CommentOffice').clear();
    await page.locator('#CommentOffice').fill('test comment updated');
    const savePromise = page.waitForResponse(
      r => r.url().includes('/api/time-planning-pn/plannings/') && r.request().method() === 'PUT'
    );
    await page.locator('#saveButton').click();
    await savePromise;
  });

  test('should remove a comment', async ({ page }) => {
    await expect(page.locator('#CommentOffice')).toHaveValue('test comment updated');
    await page.locator('#CommentOffice').clear();
    const savePromise = page.waitForResponse(
      r => r.url().includes('/api/time-planning-pn/plannings/') && r.request().method() === 'PUT'
    );
    await page.locator('#saveButton').click();
    await savePromise;
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

    const savePromise = page.waitForResponse(
      r => r.url().includes('/api/time-planning-pn/plannings/') && r.request().method() === 'PUT'
    );
    await page.locator('#saveButton').click();
    await savePromise;
    await page.waitForTimeout(1000);
  });
});
