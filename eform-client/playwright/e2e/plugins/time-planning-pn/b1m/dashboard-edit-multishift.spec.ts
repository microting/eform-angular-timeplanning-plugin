import { test, expect, Page } from '@playwright/test';
import { LoginPage } from '../../../Page objects/Login.page';

/**
 * b1m variant of `b/dashboard-edit-multishift.spec.ts`: the same multi-shift
 * (3-5) round-trip regression guard, but with off-grid times that exercise
 * the `UseOneMinuteIntervals = true` code path. The shard's
 * `post-migration.sql` flips the flag on every active assigned site, so the
 * legacy in-spec `UseOneMinuteIntervals` toggle test from `b/` is dropped
 * here (its purpose lives on in the original `b/` shard) and the dormant
 * Phase 4 fixture-TODO `test.skip` is also removed (the post-migration
 * patch is the fixture).
 *
 * Shift layout used by this test (every value is intentionally NOT a
 * multiple of 5 to push the timepicker through `minutesGap=1`):
 *   Shift 1: 01:01-02:02 break 00:03
 *   Shift 2: 03:07-04:11 break 00:13
 *   Shift 3: 05:17-06:19 break 00:23
 *   Shift 4: 07:29-08:31 break 00:37
 *   Shift 5: 09:41-10:43 break 00:47
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

// All times are non-multiples of 5; hour 0 avoided per legacy comment.
const allFiveShifts = [
  { id: 1 as const, start: '01:01', end: '02:02', break: '00:03' },
  { id: 2 as const, start: '03:07', end: '04:11', break: '00:13' },
  { id: 3 as const, start: '05:17', end: '06:19', break: '00:23' },
  { id: 4 as const, start: '07:29', end: '08:31', break: '00:37' },
  { id: 5 as const, start: '09:41', end: '10:43', break: '00:47' },
];

test.describe('Dashboard — multi-shift (3-5) round-trip regression guard (b1m, flag-on)', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('http://localhost:4200');
    await new LoginPage(page).login();
  });

  test('persists all 5 planned shifts at 1-minute granularity through save + reload', async ({ page }) => {
    await page.locator('mat-nested-tree-node').filter({ hasText: 'Timeregistrering' }).click();
    const indexPromise = page.waitForResponse(r =>
      r.url().includes('/api/time-planning-pn/plannings/index') && r.request().method() === 'POST');
    await page.locator('mat-tree-node').filter({ hasText: 'Dashboard' }).click();
    await indexPromise;
    await waitForSpinner(page);

    // Shifts 3-5 are only rendered in the workday-entity dialog when the
    // assigned site has thirdShiftActive / fourthShiftActive / fifthShiftActive
    // flipped on. The post-migration patch only sets `UseOneMinuteIntervals`
    // — the multi-shift flags still need the UI dance below.
    for (const id of ['thirdShiftActive', 'fourthShiftActive', 'fifthShiftActive']) {
      await page.locator('#firstColumn3').click();
      await expect(page.locator('mat-dialog-container')).toBeVisible({ timeout: 10000 });

      const cb = page.locator(`#${id} input[type="checkbox"]`);
      await cb.waitFor({ state: 'attached', timeout: 10000 });
      if (!(await cb.isChecked())) {
        await page.locator(`#${id}`).click({ force: true });
      }
      await expect(cb).toBeChecked();

      const assignSitePromise = page.waitForResponse(
        r => r.url().includes('/api/time-planning-pn/settings/assigned-site') && r.request().method() === 'PUT');
      await page.locator('#saveButton').click({ force: true });
      await assignSitePromise;
      await waitForSpinner(page);
      await expect(page.locator('mat-dialog-container')).toHaveCount(0, { timeout: 10000 });
    }

    const cellId = '#cell3_0';
    await page.locator(cellId).scrollIntoViewIfNeeded();
    await page.locator(cellId).click();
    await expect(page.locator('#planHours')).toBeVisible();

    // Fill all 5 shifts at 1-minute granularity.
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

    // Re-open the same cell and assert every shift round-tripped — same
    // regression guard as `b/`, just with off-grid values.
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

  /**
   * Same editing-policy regression guard as `b/`: this test has no time
   * inputs so the flag-on context is incidental; included here so the b1m
   * multishift spec mirrors the legacy file structure.
   *
   * Stabilization (FU-E) — same family of fixes as FU-D applied to `b/`:
   * `#firstColumn3` triggers `getAssignedSite()` and only then opens the
   * dialog. On slow CI the dialog can become visible before the GET
   * commits, so the gated `*ngIf` content (editing-policy radios bound to
   * `data.entryMethod`) is rendered with a stale form snapshot. The
   * post-save reopen is the same race surface — the freshly-saved
   * `acceptPlanned` value only binds after the second GET commits, so the
   * default 5s `toBeChecked` retry can race the bind. Repro: stable run
   * 25302103911 attempt 1 — slug `b-3e2c2-n-acceptPlanned-is-selected` —
   * `expect(locator).toBeChecked failed — element(s) not found` on the
   * post-reopen radio at line ~217. Attempt 2 passed.
   *
   * Fix is timing-only: gate every `#firstColumn3` click on the assigned-
   * sites GET, swap `locator.waitFor({attached})` for
   * `expect().toBeAttached()`, and bump per-assertion timeouts on the
   * round-trip checks so reactive bindings have room to hydrate.
   */
  test('editing-policy stays visible and persists when acceptPlanned is selected', async ({ page }) => {
    await page.locator('mat-nested-tree-node').filter({ hasText: 'Timeregistrering' }).click();
    const indexPromise = page.waitForResponse(
      r => r.url().includes('/api/time-planning-pn/plannings/index') && r.request().method() === 'POST',
      { timeout: 30000 });
    await page.locator('mat-tree-node').filter({ hasText: 'Dashboard' }).click();
    await indexPromise;
    await waitForSpinner(page);

    // Await the GET that hydrates the dialog model BEFORE the dialog even
    // opens. `onFirstColumnClick` fires getAssignedSite() and only then
    // calls dialog.open(...), so this response gates whether the *ngIf-
    // gated editing-policy radios bind to the persisted entry-method
    // value. Without this gate the dialog can become visible before the
    // GET commits and the radios render against a stale snapshot.
    const getAssignedSitePromise = page.waitForResponse(
      r => r.url().includes('/api/time-planning-pn/settings/assigned-sites')
        && r.url().includes('siteId=')
        && r.request().method() === 'GET',
      { timeout: 30000 });
    await page.locator('#firstColumn3').click();
    await getAssignedSitePromise;
    await expect(page.locator('mat-dialog-container')).toBeVisible({ timeout: 30000 });

    const personalCb = page.locator('#allowPersonalTimeRegistration input[type="checkbox"]');
    // expect.toBeAttached() retries continuously and emits a richer error
    // log than locator.waitFor() — same observable contract, better flake
    // diagnostics.
    await expect(personalCb).toBeAttached({ timeout: 30000 });
    if (!(await personalCb.isChecked())) {
      await page.locator('#allowPersonalTimeRegistration').click({ force: true });
    }
    await expect(personalCb).toBeChecked({ timeout: 10000 });

    const acceptPlannedRadio = page.locator('mat-radio-button[value="acceptPlanned"]');
    await acceptPlannedRadio.scrollIntoViewIfNeeded();
    await acceptPlannedRadio.locator('label').first().click({ force: true });

    await expect(page.locator('mat-radio-button[value="untilPayroll"]')).toBeVisible({ timeout: 15000 });
    await expect(page.locator('mat-radio-button[value="twoDaysRolling"]')).toBeVisible({ timeout: 15000 });

    await page.locator('mat-radio-button[value="untilPayroll"]').locator('label').first()
      .click({ force: true });

    const assignSitePromise = page.waitForResponse(
      r => r.url().includes('/api/time-planning-pn/settings/assigned-site') && r.request().method() === 'PUT',
      { timeout: 30000 });
    await page.locator('#saveButton').click({ force: true });
    await assignSitePromise;
    await waitForSpinner(page);
    await expect(page.locator('mat-dialog-container')).toHaveCount(0, { timeout: 15000 });

    // Re-open the dialog and assert both choices round-tripped. Wait for
    // the freshly-fetched assigned-site GET so the radios bind to the
    // persisted values before we assert (prior failure: line ~217
    // `toBeChecked` saw the pre-bind radio in a 5s window).
    const getAssignedSitePromise2 = page.waitForResponse(
      r => r.url().includes('/api/time-planning-pn/settings/assigned-sites')
        && r.url().includes('siteId=')
        && r.request().method() === 'GET',
      { timeout: 30000 });
    await page.locator('#firstColumn3').click();
    await getAssignedSitePromise2;
    await expect(page.locator('mat-dialog-container')).toBeVisible({ timeout: 30000 });

    await expect(
      page.locator('mat-radio-button[value="acceptPlanned"] input[type="radio"]'),
    ).toBeChecked({ timeout: 15000 });

    await expect(page.locator('mat-radio-button[value="untilPayroll"]')).toBeVisible({ timeout: 15000 });
    await expect(
      page.locator('mat-radio-button[value="untilPayroll"] input[type="radio"]'),
    ).toBeChecked({ timeout: 15000 });

    await page.locator('#cancelButton').click();
  });
});
