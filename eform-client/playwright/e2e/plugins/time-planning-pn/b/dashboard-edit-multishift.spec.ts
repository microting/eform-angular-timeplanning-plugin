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
      // Wait for the GET that hydrates the dialog model BEFORE the dialog
      // even opens. `onFirstColumnClick` fires getAssignedSite() and only
      // then calls dialog.open(...), so this response gates whether the
      // *ngIf-gated checkbox (fourthShiftActive needs data.thirdShiftActive,
      // fifthShiftActive needs data.fourthShiftActive) is ever rendered.
      // Previously the test asserted on `mat-dialog-container` visible and
      // then hard-waited 10s for the gated input to attach; on slow CI the
      // dialog appeared before the GET committed and the *ngIf evaluated
      // false, blowing the wait. Awaiting the GET here is the deterministic
      // gate.
      const getAssignedSitePromise = page.waitForResponse(
        r => r.url().includes('/api/time-planning-pn/settings/assigned-sites')
          && r.url().includes('siteId=')
          && r.request().method() === 'GET',
        { timeout: 30000 });
      await page.locator('#firstColumn3').click();
      await getAssignedSitePromise;
      await expect(page.locator('mat-dialog-container')).toBeVisible({ timeout: 30000 });

      const cb = page.locator(`#${id} input[type="checkbox"]`);
      // expect.toBeAttached() retries continuously and emits a richer error
      // log than locator.waitFor() — same observable contract, better flake
      // diagnostics.
      await expect(cb).toBeAttached({ timeout: 30000 });
      if (!(await cb.isChecked())) {
        await page.locator(`#${id}`).click({ force: true });
      }
      await expect(cb).toBeChecked({ timeout: 10000 });

      const assignSitePromise = page.waitForResponse(
        r => r.url().includes('/api/time-planning-pn/settings/assigned-site') && r.request().method() === 'PUT',
        { timeout: 30000 });
      await page.locator('#saveButton').click({ force: true });
      await assignSitePromise;
      await waitForSpinner(page);
      await expect(page.locator('mat-dialog-container')).toHaveCount(0, { timeout: 15000 });
    }

    // Day cell id is `cell{rowIndex}_{colField}` — row 3 matches the worker
    // whose assigned-site row (#firstColumn3) we just configured above.
    const cellId = '#cell3_0';
    await page.locator(cellId).scrollIntoViewIfNeeded();
    await page.locator(cellId).click();
    await expect(page.locator('#planHours')).toBeVisible({ timeout: 15000 });

    // Fill all 5 shifts.
    for (const s of allFiveShifts) {
      await setShift(page, s.id, s.start, s.end, s.break);
    }

    // Save.
    const updatePromise = page.waitForResponse(
      r => r.url().includes('/api/time-planning-pn/plannings/') && r.request().method() === 'PUT',
      { timeout: 30000 });
    const reindexPromise = page.waitForResponse(
      r => r.url().includes('/api/time-planning-pn/plannings/index') && r.request().method() === 'POST',
      { timeout: 30000 });
    await page.locator('#saveButton').click();
    await updatePromise;
    await reindexPromise;
    await waitForSpinner(page);
    await page.waitForTimeout(500);

    // Re-open the same cell and assert every shift round-tripped —
    // this is the bit that failed before the fix: shifts 3-5 came back as 00:00.
    await page.locator(cellId).scrollIntoViewIfNeeded();
    await page.locator(cellId).click();
    await expect(page.locator('#planHours')).toBeVisible({ timeout: 15000 });

    // Per-assertion 30s timeouts — the dialog opens before the form's
    // reactive bindings finish hydrating from the just-saved planning,
    // so the 5s default toHaveValue retry can race the bind. The
    // assertion contract is unchanged; only the retry budget grew.
    for (const s of allFiveShifts) {
      await expect(
        page.locator(`[data-testid="plannedStartOfShift${s.id}"]`),
        `shift ${s.id} start should round-trip`
      ).toHaveValue(s.start, { timeout: 30000 });
      await expect(
        page.locator(`[data-testid="plannedEndOfShift${s.id}"]`),
        `shift ${s.id} end should round-trip`
      ).toHaveValue(s.end, { timeout: 30000 });
      await expect(
        page.locator(`[data-testid="plannedBreakOfShift${s.id}"]`),
        `shift ${s.id} break should round-trip`
      ).toHaveValue(s.break, { timeout: 30000 });
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
    // clobber each other across the same shard). Await the
    // getAssignedSite GET so the dialog opens with hydrated data — the
    // gated *ngIf inputs only attach once `data.*` is bound.
    const getAssignedSitePromise = page.waitForResponse(
      r => r.url().includes('/api/time-planning-pn/settings/assigned-sites')
        && r.url().includes('siteId=')
        && r.request().method() === 'GET',
      { timeout: 30000 });
    await page.locator('#firstColumn3').click();
    await getAssignedSitePromise;
    await expect(page.locator('mat-dialog-container')).toBeVisible({ timeout: 30000 });

    // The entry-method + editing-policy radios are gated behind
    // allowPersonalTimeRegistration. Make sure it's enabled (idempotent).
    const personalCb = page.locator('#allowPersonalTimeRegistration input[type="checkbox"]');
    await expect(personalCb).toBeAttached({ timeout: 30000 });
    if (!(await personalCb.isChecked())) {
      await page.locator('#allowPersonalTimeRegistration').scrollIntoViewIfNeeded();
      await page.locator('#allowPersonalTimeRegistration').click({ force: true });
    }
    await expect(personalCb).toBeChecked({ timeout: 10000 });

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
      r => r.url().includes('/api/time-planning-pn/settings/assigned-site') && r.request().method() === 'PUT',
      { timeout: 30000 });
    await page.locator('#saveButton').click({ force: true });
    await assignSitePromise;
    await waitForSpinner(page);
    await expect(page.locator('mat-dialog-container')).toHaveCount(0, { timeout: 15000 });

    // Re-open the dialog and assert both choices round-tripped. Wait for
    // the freshly-fetched assigned-site GET so the radios bind to the
    // persisted values before we assert.
    const getAssignedSitePromise2 = page.waitForResponse(
      r => r.url().includes('/api/time-planning-pn/settings/assigned-sites')
        && r.url().includes('siteId=')
        && r.request().method() === 'GET',
      { timeout: 30000 });
    await page.locator('#firstColumn3').click();
    await getAssignedSitePromise2;
    await expect(page.locator('mat-dialog-container')).toBeVisible({ timeout: 30000 });

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
   * Every site runs on 1-minute intervals (#1740): the "Use 1-minute
   * intervals" checkbox and the "Advanced settings" section that held only it
   * are gone, and TimeSettingService.UpdateAssignedSite ignores the flag the
   * client sends. The CI seed logs in as admin@admin.com, the first-user who
   * used to see the section, so its absence here is the real assertion.
   */
  test('the 1-minute intervals checkbox is gone from the assigned-site dialog', async ({ page }) => {
    await page.locator('mat-nested-tree-node').filter({ hasText: 'Timeregistrering' }).click();
    const indexPromise = page.waitForResponse(r =>
      r.url().includes('/api/time-planning-pn/plannings/index') && r.request().method() === 'POST',
      { timeout: 30000 });
    await page.locator('mat-tree-node').filter({ hasText: 'Dashboard' }).click();
    await indexPromise;
    await waitForSpinner(page);

    const getAssignedSitePromise = page.waitForResponse(
      r => r.url().includes('/api/time-planning-pn/settings/assigned-sites')
        && r.url().includes('siteId=')
        && r.request().method() === 'GET',
      { timeout: 30000 });
    await page.locator('#firstColumn3').click();
    await getAssignedSitePromise;
    const dialog = page.locator('mat-dialog-container');
    await expect(dialog).toBeVisible({ timeout: 30000 });
    // The hydrated dialog is rendered (so the absence checks below are not
    // vacuous): the save button is the last control of the same form.
    await expect(dialog.locator('#saveButton')).toBeVisible({ timeout: 15000 });

    await expect(dialog.locator('#useOneMinuteIntervals')).toHaveCount(0, { timeout: 15000 });
    // The CI user's UI is Danish: the da.ts texts of 'Use 1-minute intervals'
    // and 'Advanced settings'.
    await expect(dialog.getByText('Brug 1-minutters intervaller')).toHaveCount(0, { timeout: 15000 });
    await expect(dialog.getByText('Avancerede indstillinger')).toHaveCount(0, { timeout: 15000 });

    await dialog.locator('#cancelButton').click();
    await expect(dialog).toHaveCount(0, { timeout: 15000 });
  });

  /**
   * Plannings-table display contract: the cell always renders the actual
   * shift stamp as `HH:mm`, even when `AssignedSite.UseOneMinuteIntervals`
   * is on. Seconds are never shown regardless of the flag.
   *
   * Server-side seeding of `AssignedSite.UseOneMinuteIntervals = true`
   * plus a planning row with `Start1StartedAt = 2026-05-15 07:03:53`
   * requires DB fixture work the CI playwright shard doesn't yet wire up
   * (the tests here log in as admin and rely on the default seed).
   * Captured here as a TODO so the assertion shape survives any future
   * fixture work; the jest unit test on `formatStamp(...)` covers the
   * format-helper contract for the merge-blocking path.
   */
  test.skip('plannings-table renders HH:mm for actual stamp when site flag is on', async ({ page }) => {
    // TODO(fixture): seed AssignedSite.UseOneMinuteIntervals = true for the
    // worker referenced by #cell3_0 AND a PlanRegistration row with
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
    //   await expect(firstShiftActual).toContainText('07:03');
    //   // Negative guard — seconds are never displayed even when the flag is on.
    //   await expect(firstShiftActual).not.toContainText('07:03:53');
    //
    // Until the fixture lands the unit test
    //   `formatStamp — uses HH:mm format when row.useOneMinuteIntervals is true`
    // covers the format-helper contract (eform-client/src/app/plugins/modules/time-planning-pn/
    // components/plannings/time-plannings-table/time-plannings-table.component.spec.ts).
  });
});
