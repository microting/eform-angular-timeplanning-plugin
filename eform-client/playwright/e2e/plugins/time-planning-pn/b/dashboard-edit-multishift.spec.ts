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
    // defaults them all to false. The assigned-site dialog also gates the
    // 4th/5th checkboxes behind `data.thirdShiftActive` / `data.fourthShiftActive`
    // — those bindings reflect the snapshot passed into the dialog, so each
    // new checkbox only materialises after a save + reopen cycle.
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

    // Day cell id is `cell{rowIndex}_{colField}` — row 3 matches the worker
    // whose assigned-site row (#firstColumn3) we just configured above.
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

  /**
   * Regression guard for the assigned-site dialog "edit past registrations"
   * radio group: it must remain visible and editable when entry method is
   * acceptPlanned. Prior bug: an *ngIf clause in the template hid the entire
   * editing-policy section under acceptPlanned mode, even though the server
   * persists allowEditOfRegistrations independently of allowAcceptOfPlannedHours.
   */
  test('editing-policy stays visible and persists when acceptPlanned is selected', async ({ page }) => {
    await page.locator('mat-nested-tree-node').filter({ hasText: 'Timeregistrering' }).click();
    const indexPromise = page.waitForResponse(r =>
      r.url().includes('/api/time-planning-pn/plannings/index') && r.request().method() === 'POST');
    await page.locator('mat-tree-node').filter({ hasText: 'Dashboard' }).click();
    await indexPromise;
    await waitForSpinner(page);

    // Open the assigned-site dialog for the third worker (matches the
    // multishift test's #firstColumn3 convention so the two tests don't
    // clobber each other across the same shard).
    await page.locator('#firstColumn3').click();
    await expect(page.locator('mat-dialog-container')).toBeVisible({ timeout: 10000 });

    // The entry-method + editing-policy radios are gated behind
    // allowPersonalTimeRegistration. Make sure it's enabled (idempotent).
    const personalCb = page.locator('#allowPersonalTimeRegistration input[type="checkbox"]');
    await personalCb.waitFor({ state: 'attached', timeout: 10000 });
    if (!(await personalCb.isChecked())) {
      await page.locator('#allowPersonalTimeRegistration').click({ force: true });
    }
    await expect(personalCb).toBeChecked();

    // Click the acceptPlanned radio — pick the inner clickable label/input
    // because the Material radio button host wraps a hidden input.
    const acceptPlannedRadio = page.locator('mat-radio-button[value="acceptPlanned"]');
    await acceptPlannedRadio.scrollIntoViewIfNeeded();
    await acceptPlannedRadio.locator('label').first().click({ force: true });

    // Assert the editing-policy section is in the DOM. The two radio groups
    // each render their own mat-radio-group; the second one carries the
    // editing-policy values (locked / untilPayroll / twoDaysRolling).
    await expect(page.locator('mat-radio-button[value="untilPayroll"]')).toBeVisible({ timeout: 5000 });
    await expect(page.locator('mat-radio-button[value="twoDaysRolling"]')).toBeVisible();

    // Pick "Yes, until the last payroll run" (untilPayroll).
    await page.locator('mat-radio-button[value="untilPayroll"]').locator('label').first()
      .click({ force: true });

    // Save and wait for the PUT to land + the dashboard re-index.
    const assignSitePromise = page.waitForResponse(
      r => r.url().includes('/api/time-planning-pn/settings/assigned-site') && r.request().method() === 'PUT');
    await page.locator('#saveButton').click({ force: true });
    await assignSitePromise;
    await waitForSpinner(page);
    await expect(page.locator('mat-dialog-container')).toHaveCount(0, { timeout: 10000 });

    // Re-open the dialog and assert both choices round-tripped.
    await page.locator('#firstColumn3').click();
    await expect(page.locator('mat-dialog-container')).toBeVisible({ timeout: 10000 });

    // acceptPlanned still selected.
    await expect(
      page.locator('mat-radio-button[value="acceptPlanned"] input[type="radio"]'),
    ).toBeChecked();

    // Editing-policy section is still rendered (the previously-broken case)…
    await expect(page.locator('mat-radio-button[value="untilPayroll"]')).toBeVisible();
    // …and untilPayroll is the persisted choice.
    await expect(
      page.locator('mat-radio-button[value="untilPayroll"] input[type="radio"]'),
    ).toBeChecked();

    await page.locator('#cancelButton').click();
  });

  /**
   * Regression guard for the "Use 1-minute intervals" first-user toggle in
   * the Advanced settings section. The flag rides on AssignedSite end-to-end
   * (entity → DTO → write mapping → angular model) and is persisted by
   * TimeSettingService.UpdateAssignedSite. This test only verifies the UI
   * plumbing — the toggle is dormant (no business-logic consumers yet).
   *
   * Gating: !data.resigned && (selectCurrentUserIsFirstUser$ | async).
   * The CI seed logs in as admin@admin.com (LoginConstants.username), which
   * is the first-user, so the toggle is visible in this test context.
   */
  test('first-user can toggle Use 1-minute intervals; persists across save+reopen', async ({ page }) => {
    await page.locator('mat-nested-tree-node').filter({ hasText: 'Timeregistrering' }).click();
    const indexPromise = page.waitForResponse(r =>
      r.url().includes('/api/time-planning-pn/plannings/index') && r.request().method() === 'POST');
    await page.locator('mat-tree-node').filter({ hasText: 'Dashboard' }).click();
    await indexPromise;
    await waitForSpinner(page);

    // Open the assigned-site dialog for the third worker — same convention
    // as the other tests on this shard so they don't clobber each other.
    await page.locator('#firstColumn3').click();
    await expect(page.locator('mat-dialog-container')).toBeVisible({ timeout: 10000 });

    // The toggle is gated behind first-user; the CI fixture logs in as
    // first-user, so it must be visible.
    await expect(page.locator('#useOneMinuteIntervals')).toBeVisible({ timeout: 10000 });

    const cb = page.locator('#useOneMinuteIntervals input[type="checkbox"]');
    await cb.waitFor({ state: 'attached', timeout: 10000 });

    // Capture the starting state, flip it, save, re-open, assert it persisted.
    const wasChecked = await cb.isChecked();
    await page.locator('#useOneMinuteIntervals').click({ force: true });
    if (wasChecked) {
      await expect(cb).not.toBeChecked();
    } else {
      await expect(cb).toBeChecked();
    }

    const assignSitePromise = page.waitForResponse(
      r => r.url().includes('/api/time-planning-pn/settings/assigned-site') && r.request().method() === 'PUT');
    await page.locator('#saveButton').click({ force: true });
    await assignSitePromise;
    await waitForSpinner(page);
    await expect(page.locator('mat-dialog-container')).toHaveCount(0, { timeout: 10000 });

    // Re-open and assert the new value round-tripped.
    await page.locator('#firstColumn3').click();
    await expect(page.locator('mat-dialog-container')).toBeVisible({ timeout: 10000 });

    const cbReopened = page.locator('#useOneMinuteIntervals input[type="checkbox"]');
    await cbReopened.waitFor({ state: 'attached', timeout: 10000 });
    if (wasChecked) {
      await expect(cbReopened).not.toBeChecked();
    } else {
      await expect(cbReopened).toBeChecked();
    }

    await page.locator('#cancelButton').click();
  });

  /**
   * Phase 4 — second-precision DISPLAY: when a row's site has
   * `useOneMinuteIntervals = true` AND the planning has a precise
   * `start1StartedAt` stamp (e.g. 07:03:53), the plannings-table cell must
   * render the stamp at HH:mm:ss instead of the legacy HH:mm.
   *
   * Server-side seeding `AssignedSite.UseOneMinuteIntervals = true` plus
   * a planning with `Start1StartedAt = 2026-05-15 07:03:53` requires DB
   * fixture work the CI playwright shard doesn't yet wire up (the tests
   * here log in as admin and rely on the default seed). Captured here as
   * a TODO so the assertion shape survives any future fixture work; the
   * Phase 4 jest unit test on `formatStamp(...)` covers the contract for
   * the merge-blocking path.
   */
  test.skip('plannings-table renders HH:mm:ss for actual stamp when site flag is on', async ({ page }) => {
    // TODO(phase 4 fixture): seed AssignedSite.UseOneMinuteIntervals = true
    // for the worker referenced by #cell3_0 AND a PlanRegistration row with
    // Start1StartedAt = '2026-05-15T07:03:53Z' on a date that lands inside
    // the dashboard's default visible range.
    //
    // Then the assertion shape is:
    //
    //   await page.locator('mat-nested-tree-node').filter({ hasText: 'Timeregistrering' }).click();
    //   await page.locator('mat-tree-node').filter({ hasText: 'Dashboard' }).click();
    //   await waitForSpinner(page);
    //
    //   const cellId = '#cell3_0';
    //   await page.locator(cellId).scrollIntoViewIfNeeded();
    //
    //   // The first-shift actual line is rendered with id firstShiftActual{rowIdx}_{colField}.
    //   const firstShiftActual = page.locator('[id^="firstShiftActual"]').first();
    //   await expect(firstShiftActual).toContainText('07:03:53');
    //   // Negative guard — the legacy 5-min path would render '07:00' / '07:05' instead.
    //   await expect(firstShiftActual).not.toContainText(/07:0[05]\s/);
    //
    // Until the fixture lands the unit test
    //   `formatStamp (Phase 4) — uses HH:mm:ss format when row.useOneMinuteIntervals is true`
    // covers the format-helper contract (eform-client/src/app/plugins/modules/time-planning-pn/
    // components/plannings/time-plannings-table/time-plannings-table.component.spec.ts).
  });
});
