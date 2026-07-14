import { test, expect, Page } from '@playwright/test';
import { LoginPage } from '../../../Page objects/Login.page';

/**
 * b shard: version-history ("Aktivitetslog") modal + human-readable value
 * formatting.
 *
 * The workday edit dialog exposes an admin-only history icon-button which
 * opens VersionHistoryModalComponent — a day-grouped timeline where the
 * backend diffs PlanRegistrationVersion rows into FieldChange lists and the
 * frontend renders values human-readably:
 *   - Planned(Start|End|Break)OfShift1-5 / Pause1-5OverrideMinutes are stored
 *     as integer minutes and must render HH:mm (420 -> "07:00", 900 -> "15:00",
 *     60 -> "01:00") — never the raw integer.
 *   - Each event line reads "<label> ændret: <from> → <to>" plus an
 *     "af <actor>" line (CI runs the UI in Danish, like the sibling specs
 *     asserting 'Timeregistrering' / '(x timer)').
 *
 * Ordering inside shard b (workers=1, alphabetical file order):
 * this file runs after dashboard-edit-pause-override.spec.ts and before
 * time-planning-settings.spec.ts. The settings spec does not read dashboard
 * cell state, so the plan edits made here (on last week's #cell3_1) do not
 * feed any downstream assertion in the shard's cumulative chain.
 *
 * The spec is self-contained: it performs two saves with values it fully
 * controls, so the newest version's diff rows are deterministic regardless
 * of what earlier specs left on the cell:
 *   edit 1: planned shift 1 = 07:00 - 15:00 / break 01:00
 *   edit 2: planned shift 1 = 08:00 - 16:00 / break 00:30
 * => newest version must show 07:00 → 08:00, 15:00 → 16:00, 01:00 → 00:30.
 */

async function waitForSpinner(page: Page) {
  if (await page.locator('.overlay-spinner').count() > 0) {
    await page.locator('.overlay-spinner').waitFor({ state: 'hidden', timeout: 30000 });
  }
}

// Planned-time clock-face driver — mirrors the rotateZ selector logic used by
// dashboard-edit-a.spec.ts for the planned{Start,End,Break}OfShiftN pickers
// (proven in CI for exactly these testids). Hour 0 lives on the inner ring as
// the 85px-high element rotated 720deg.
async function pickPlannedTime(page: Page, testid: string, timeStr: string) {
  const hours = parseInt(timeStr.split(':')[0], 10);
  const minutes = parseInt(timeStr.split(':')[1], 10);
  const degrees = 360 / 12 * hours;
  const minuteDegrees = 360 / 60 * minutes;

  await page.locator(`[data-testid="${testid}"]`).click();
  if (degrees > 360) {
    await page.locator('[style="height: 85px; transform: rotateZ(' + degrees + 'deg) translateX(-50%);"] > span').click();
  } else if (!degrees || isNaN(degrees) || degrees === 0) {
    await page.locator('[style="height: 85px; transform: rotateZ(720deg) translateX(-50%);"] > span').click();
  } else {
    await page.locator('[style="transform: rotateZ(' + degrees + 'deg) translateX(-50%);"] > span').click();
  }
  if (minuteDegrees > 0) {
    await page.locator('[style="transform: rotateZ(' + minuteDegrees + 'deg) translateX(-50%);"] > span').click({ force: true });
  }
  if (minuteDegrees === 0) {
    // The minute face must be clicked even for :00 — skipping it keeps the
    // field's residual minutes (e.g. 07:50 left by dashboard-edit-b would
    // survive as "07:50" instead of "07:00"). "00" sits at rotateZ(360deg);
    // mirrors dashboard-edit-b.spec.ts's proven zero-minute handling.
    await page.locator('[style="transform: rotateZ(360deg) translateX(-50%);"] > span').click();
  }
  await page.locator('.timepicker-button span').filter({ hasText: 'Ok' }).click();
  await expect(page.locator(`[data-testid="${testid}"]`)).toHaveValue(timeStr);
}

async function openDashboardLastWeek(page: Page) {
  await page.locator('mat-nested-tree-node').filter({ hasText: 'Timeregistrering' }).click();
  const indexUpdatePromise = page.waitForResponse(
    r => r.url().includes('/api/time-planning-pn/plannings/index') && r.request().method() === 'POST'
  );
  await page.locator('mat-tree-node').filter({ hasText: 'Dashboard' }).click();
  await page.locator('#backwards').click();
  await indexUpdatePromise;
  await waitForSpinner(page);
}

async function openCell(page: Page, cellId: string) {
  await page.locator(cellId).waitFor({ state: 'visible', timeout: 15000 });
  await page.locator(cellId).scrollIntoViewIfNeeded();
  await page.locator(cellId).click();
  await page.locator('#planHours').waitFor({ state: 'visible', timeout: 15000 });
}

async function saveAndAwait(page: Page) {
  const updatePromise = page.waitForResponse(r =>
    r.url().includes('/api/time-planning-pn/plannings/') && r.request().method() === 'PUT');
  const reindexPromise = page.waitForResponse(r =>
    r.url().includes('/api/time-planning-pn/plannings/index') && r.request().method() === 'POST');
  await page.locator('#saveButton').click();
  const updateResponse = await updatePromise;
  await reindexPromise;
  await waitForSpinner(page);
  await page.waitForTimeout(1000);
  return updateResponse;
}

test.describe('Dashboard version-history (Aktivitetslog) modal', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('http://localhost:4200');
    await new LoginPage(page).login();
  });

  test('planned-time edits render as HH:mm timeline events, never raw minutes', async ({ page }) => {
    const cellId = '#cell3_1';

    await openDashboardLastWeek(page);

    // Edit 1: establish a fully known planned shift 1 (07:00 - 15:00 / 01:00).
    await openCell(page, cellId);
    await pickPlannedTime(page, 'plannedStartOfShift1', '07:00');
    await pickPlannedTime(page, 'plannedEndOfShift1', '15:00');
    await pickPlannedTime(page, 'plannedBreakOfShift1', '01:00');
    const firstSave = await saveAndAwait(page);
    expect(firstSave.status(), 'first PUT must succeed').toBeLessThan(400);

    // Edit 2: change every controlled field so the newest version carries a
    // deterministic diff for start, end and break.
    await openCell(page, cellId);
    await pickPlannedTime(page, 'plannedStartOfShift1', '08:00');
    await pickPlannedTime(page, 'plannedEndOfShift1', '16:00');
    await pickPlannedTime(page, 'plannedBreakOfShift1', '00:30');
    const secondSave = await saveAndAwait(page);
    expect(secondSave.status(), 'second PUT must succeed').toBeLessThan(400);

    // Re-open the day dialog and open the admin-only history modal.
    await openCell(page, cellId);
    const historyPromise = page.waitForResponse(r =>
      r.url().includes('/version-history') && r.request().method() === 'GET');
    await page
      .locator('mat-dialog-container button')
      .filter({ has: page.locator('mat-icon', { hasText: 'history' }) })
      .click();
    await historyPromise;

    const modal = page.locator('app-version-history-modal');
    await expect(modal).toBeVisible();

    // Header: Danish title, intro line and hint line.
    await expect(modal.locator('.history-title')).toHaveText('Aktivitetslog');
    await expect(modal.locator('.history-intro')).toContainText('Ændringer for');
    await expect(modal.locator('.history-hint')).toContainText('Nyeste øverst');

    // Day-grouped timeline: at least one day heading with events under it.
    await expect(modal.locator('.history-day-heading').first()).toBeVisible();
    const events = modal.locator('.history-event');
    expect(await events.count()).toBeGreaterThan(0);

    // Each event shows an HH:mm timestamp.
    await expect(modal.locator('.history-time').first()).toHaveText(/^\d{2}:\d{2}$/);

    // Newest-first: the first row matching each label is the edit-2 diff.
    // Values are stored as integer minutes (480/420 etc.) but must render HH:mm.
    const startRow = events.filter({ hasText: 'Planlagt start på 1. vagt' }).first();
    await expect(startRow).toContainText('ændret');
    await expect(startRow).toContainText('07:00 → 08:00');

    const endRow = events.filter({ hasText: 'Planlagt afslutning af 1. vagt' }).first();
    await expect(endRow).toContainText('15:00 → 16:00');

    const breakRow = events.filter({ hasText: 'Planlagt pause på 1. vagt' }).first();
    await expect(breakRow).toContainText('01:00 → 00:30');
    // 01:00 is stored as 60 minutes — the raw integer must not leak into the
    // row (anchored to the break row to avoid false positives elsewhere).
    await expect(breakRow).not.toContainText(/\b60\b/);

    // Raw minute integers for the values used above must not appear anywhere
    // in the modal (07:00 = 420, 15:00 = 900): formatting must have run for
    // every version in the timeline, including ones from earlier b-shard specs.
    await expect(modal).not.toContainText(/\b420\b/);
    await expect(modal).not.toContainText(/\b900\b/);

    // Actor line: "af <user>" under the newest event.
    await expect(modal.locator('.history-actor').first()).toContainText('af ');

    // Close the history modal, then the workday dialog.
    await modal.locator('.history-close').click();
    await expect(modal).toBeHidden();
    await page.locator('#cancelButton').click();
  });
});
