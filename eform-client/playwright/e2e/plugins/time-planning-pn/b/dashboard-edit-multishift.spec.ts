import { test, expect, Page } from '@playwright/test';
import { LoginPage } from '../../../Page objects/Login.page';

/**
 * Regression guard for the multi-shift (3-5) save + render pipeline.
 *
 * Prior bug: the C# `Update()` method only copied shift 1-2 from the request
 * model onto the PlanRegistration entity — shifts 3-5 were silently dropped.
 * A round-trip that fills all 5 shifts in the workday-entity dialog and
 * re-reads them from the table cell + dialog is the minimum guard against
 * that regression ever coming back.
 *
 * Shift layout used by this test:
 *   Shift 1: 00:00-01:00 break 00:05
 *   Shift 2: 02:00-03:00 break 00:10
 *   Shift 3: 04:00-05:00 break 00:15
 *   Shift 4: 06:00-07:00 break 00:20
 *   Shift 5: 07:00-08:00 break 00:25
 */

async function waitForSpinner(page: Page) {
  if (await page.locator('.overlay-spinner').count() > 0) {
    await page.locator('.overlay-spinner').waitFor({ state: 'hidden', timeout: 30000 });
  }
}

async function pickTime(page: Page, timeStr: string) {
  // Position-based clock-face clicks (same approach as time-planning-settings.spec.ts).
  // Works uniformly for h=0 (break times), unlike rotateZ-selector strategies.
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
}

async function setShift(page: Page, shiftId: 1|2|3|4|5, start: string, end: string, breakStr: string) {
  await page.locator(`[data-testid="plannedStartOfShift${shiftId}"]`).click();
  await pickTime(page, start);
  await expect(page.locator(`[data-testid="plannedStartOfShift${shiftId}"]`)).toHaveValue(start);

  await page.locator(`[data-testid="plannedEndOfShift${shiftId}"]`).click();
  await pickTime(page, end);
  await expect(page.locator(`[data-testid="plannedEndOfShift${shiftId}"]`)).toHaveValue(end);

  await page.locator(`[data-testid="plannedBreakOfShift${shiftId}"]`).click();
  await pickTime(page, breakStr);
  await expect(page.locator(`[data-testid="plannedBreakOfShift${shiftId}"]`)).toHaveValue(breakStr);
}

// Times chosen to avoid hour==0 (the Material timepicker's "12" selector
// sits at a non-rotateZ position that breaks the degree-math helper above).
const allFiveShifts = [
  { id: 1 as const, start: '01:00', end: '02:00', break: '00:05' },
  { id: 2 as const, start: '03:00', end: '04:00', break: '00:10' },
  { id: 3 as const, start: '05:00', end: '06:00', break: '00:15' },
  { id: 4 as const, start: '07:00', end: '08:00', break: '00:20' },
  { id: 5 as const, start: '09:00', end: '10:00', break: '00:25' },
];

test.describe('Dashboard — multi-shift (3-5) round-trip regression guard', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('http://localhost:4200');
    await new LoginPage(page).login();
  });

  test('persists all 5 planned shifts through save + reload', async ({ page }) => {
    await page.locator('mat-nested-tree-node').filter({ hasText: 'Timeregistrering' }).click();
    const indexPromise = page.waitForResponse(r =>
      r.url().includes('/api/time-planning-pn/plannings/index') && r.request().method() === 'POST');
    await page.locator('mat-tree-node').filter({ hasText: 'Dashboard' }).click();
    await indexPromise;
    await waitForSpinner(page);

    // Shifts 3-5 are only rendered in the workday-entity dialog when the
    // assigned site has thirdShiftActive / fourthShiftActive / fifthShiftActive
    // flipped on (see workday-entity-dialog.component.ts:354-363). CI seed
    // defaults them all to false, so we open the assigned-site dialog first
    // and enable the three cascading checkboxes before editing the day.
    await page.locator('#firstColumn0').click();
    await expect(page.locator('mat-dialog-container')).toBeVisible({ timeout: 10000 });

    for (const id of ['#thirdShiftActive', '#fourthShiftActive', '#fifthShiftActive']) {
      const cb = page.locator(`${id} input[type="checkbox"]`);
      await cb.waitFor({ state: 'attached', timeout: 10000 });
      if (!(await cb.isChecked())) {
        await page.locator(id).click({ force: true });
      }
      await expect(cb).toBeChecked();
    }

    const assignSitePromise = page.waitForResponse(
      r => r.url().includes('/api/time-planning-pn/settings/assigned-site') && r.request().method() === 'PUT');
    await page.locator('#saveButton').click({ force: true });
    await assignSitePromise;
    await waitForSpinner(page);
    await expect(page.locator('mat-dialog-container')).toHaveCount(0, { timeout: 10000 });

    // Pick any visible day cell — column 3 (worker index), first date in the range.
    const cellId = '#cell3_0';
    await page.locator(cellId).scrollIntoViewIfNeeded();
    await page.locator(cellId).click();
    await expect(page.locator('#planHours')).toBeVisible();

    // Fill all 5 shifts.
    for (const s of allFiveShifts) {
      await setShift(page, s.id, s.start, s.end, s.break);
    }

    // Save.
    const updatePromise = page.waitForResponse(r =>
      r.url().includes('/api/time-planning-pn/plannings/') && r.request().method() === 'PUT');
    const reindexPromise = page.waitForResponse(r =>
      r.url().includes('/api/time-planning-pn/plannings/index') && r.request().method() === 'POST');
    await page.locator('#saveButton').click();
    await updatePromise;
    await reindexPromise;
    await waitForSpinner(page);
    await page.waitForTimeout(500);

    // Re-open the same cell and assert every shift round-tripped —
    // this is the bit that failed before the fix: shifts 3-5 came back as 00:00.
    await page.locator(cellId).scrollIntoViewIfNeeded();
    await page.locator(cellId).click();
    await expect(page.locator('#planHours')).toBeVisible();

    for (const s of allFiveShifts) {
      await expect(
        page.locator(`[data-testid="plannedStartOfShift${s.id}"]`),
        `shift ${s.id} start should round-trip`
      ).toHaveValue(s.start);
      await expect(
        page.locator(`[data-testid="plannedEndOfShift${s.id}"]`),
        `shift ${s.id} end should round-trip`
      ).toHaveValue(s.end);
      await expect(
        page.locator(`[data-testid="plannedBreakOfShift${s.id}"]`),
        `shift ${s.id} break should round-trip`
      ).toHaveValue(s.break);
    }

    await page.locator('#cancelButton').click();
  });
});
