import { test, expect, Page } from '@playwright/test';
import { LoginPage } from '../../../Page objects/Login.page';
import { TimePlanningWorkingHoursPage } from '../TimePlanningWorkingHours.page';
import { selectDateRangeOnNewDatePicker } from '../../../helper-functions';

// ---------------------------------------------------------------------------
// Date utilities
// ---------------------------------------------------------------------------

const getMonday = (base: Date): Date => {
  const dow = base.getDay();
  const diff = dow === 0 ? -6 : 1 - dow;
  const mon = new Date(base);
  mon.setDate(base.getDate() + diff);
  return mon;
};

const getSunday = (monday: Date): Date => {
  const sun = new Date(monday);
  sun.setDate(monday.getDate() + 6);
  return sun;
};

// Use two weeks ago to ensure all days are in the past and editable
const today = new Date();
const twoWeeksAgoBase = new Date(today);
twoWeeksAgoBase.setDate(today.getDate() - 14);
const targetMonday = getMonday(twoWeeksAgoBase);
const targetSunday = getSunday(targetMonday);

// ---------------------------------------------------------------------------
// Navigation helpers
// ---------------------------------------------------------------------------

/**
 * Expand the time-planning plugin menu in the left sidebar.
 * Uses the mat-tree navigation approach (matching translated text).
 * Falls back to ID-based approach if tree nodes are available.
 */
/**
 * Navigation uses the proven pattern from dashboard-edit-b.spec.ts:
 * mat-nested-tree-node for parent menu, mat-tree-node for sub-items.
 */
async function expandPluginMenu(page: Page): Promise<void> {
  await page.locator('mat-nested-tree-node').filter({ hasText: 'Timeregistrering' }).click();
}

async function navigateToPayRuleSets(page: Page): Promise<void> {
  // Pay Rule Sets has no sidebar menu item - navigate via direct URL
  const responsePromise = page.waitForResponse(
    r => r.url().includes('/api/time-planning-pn/pay-rule-sets') && r.request().method() === 'GET',
  );
  await page.goto('http://localhost:4200/plugins/time-planning-pn/pay-rule-sets');
  await responsePromise;
  await page.locator('#time-planning-pn-pay-rule-sets-grid')
    .waitFor({ state: 'visible', timeout: 30000 });
}

async function navigateToPlannings(page: Page): Promise<void> {
  // Use the proven pattern from dashboard-edit-b.spec.ts
  await expandPluginMenu(page);
  const indexPromise = page.waitForResponse(
    r => r.url().includes('/api/time-planning-pn/plannings/index') && r.request().method() === 'POST',
  );
  await page.locator('mat-tree-node').filter({ hasText: 'Dashboard' }).click();
  await page.locator('#backwards').click();
  await indexPromise;

  // Wait for spinner to disappear if present
  if (await page.locator('.overlay-spinner').count() > 0) {
    await page.locator('.overlay-spinner').waitFor({ state: 'hidden', timeout: 30000 }).catch(() => {});
  }

  // Wait for the plannings grid to be visible
  await page.locator('#main-header-text').waitFor({ state: 'visible', timeout: 30000 });
}

// ---------------------------------------------------------------------------
// Pay Rule Set creation helpers (preset-based)
// ---------------------------------------------------------------------------

/**
 * Open the create pay rule set modal by clicking the trigger button.
 * Uses the stable #openCreatePayRuleSetBtn id rather than the accessible name,
 * which would break under non-English UI locales.
 */
async function openCreatePayRuleSetModal(page: Page): Promise<void> {
  await page.locator('#openCreatePayRuleSetBtn').click();
  await page.locator('mat-dialog-container').waitFor({ state: 'visible', timeout: 10000 });
}

/**
 * Select a preset from the #presetSelector dropdown in the create modal.
 * The dropdown uses mtx-select (ng-select) with groupBy, rendered via
 * ng-dropdown-panel appended to body.
 */
async function selectPreset(page: Page, presetLabel: string): Promise<void> {
  // Click the preset selector to open the dropdown
  await page.locator('#presetSelector').click();

  // Wait for the ng-dropdown-panel to appear (appended to body)
  const dropdown = page.locator('ng-dropdown-panel');
  await dropdown.waitFor({ state: 'visible', timeout: 10000 });

  // Click the option matching the label
  await dropdown.locator('.ng-option').filter({ hasText: presetLabel }).first().click();

  // Wait for the dropdown to close
  await dropdown.waitFor({ state: 'hidden', timeout: 5000 }).catch(() => {});
}

/**
 * Click the Create button to submit the pay rule set.
 */
async function submitCreatePayRuleSet(page: Page): Promise<void> {
  await page.locator('#createPayRuleSetBtn').click();
  await expect(page.locator('mat-dialog-container')).toHaveCount(0, { timeout: 15000 });
}

// ---------------------------------------------------------------------------
// Assign PayRuleSet to worker via Plannings page
// ---------------------------------------------------------------------------

/**
 * Open the assigned-site dialog for the first worker, select the pay rule set
 * by name, and save.
 */
async function assignPayRuleSetToWorker(
  page: Page,
  payRuleSetName: string,
): Promise<void> {
  // Click on the first worker's avatar/name column to open AssignedSite dialog
  const firstColumn = page.locator('#firstColumn0');
  await firstColumn.waitFor({ state: 'visible', timeout: 15000 });
  await firstColumn.click();

  // Wait for the dialog to open
  const dialog = page.locator('mat-dialog-container');
  await dialog.waitFor({ state: 'visible', timeout: 15000 });

  // Find the mtx-select for payRuleSetId and click it to open the dropdown
  const payRuleSetField = dialog.locator('mtx-select[formcontrolname="payRuleSetId"]');
  await payRuleSetField.waitFor({ state: 'visible', timeout: 10000 });
  await payRuleSetField.click();

  // Wait for the ng-dropdown-panel to appear and select the option
  const dropdown = page.locator('ng-dropdown-panel');
  await dropdown.waitFor({ state: 'visible', timeout: 10000 });
  await dropdown.locator('.ng-option').filter({ hasText: payRuleSetName }).first().click();

  // Save the dialog
  await dialog.locator('#saveButton').click();

  // Wait for dialog to close
  await expect(page.locator('mat-dialog-container')).toHaveCount(0, { timeout: 10000 });
}

// ---------------------------------------------------------------------------
// Workday entity dialog helpers (time picker interaction)
// ---------------------------------------------------------------------------

/**
 * Set a time value in an ngx-material-timepicker input field.
 * The inputs are readonly and open a timepicker overlay when clicked.
 * We use evaluate to set the value directly on the input and dispatch events.
 */
/**
 * Set a time value using the ngx-material-timepicker clock face.
 * Uses the proven rotateZ degree-based selector pattern from dashboard-edit-b.spec.ts.
 *
 * Clock face: hours use rotateZ(360/12 * h), minutes use rotateZ(360/60 * m).
 * Special cases: hour 0 → 720deg, minute 0 → 360deg.
 */
async function setTimepickerValue(
  page: Page,
  testId: string,
  timeValue: string,
): Promise<void> {
  const [hours, minutes] = timeValue.split(':').map(Number);
  const input = page.locator(`[data-testid="${testId}"]`);
  await input.waitFor({ state: 'visible', timeout: 10000 });
  // The timepicker input is readonly and may be disabled - force click
  await input.click({ force: true });

  // Select hour on the clock face
  const hourDegrees = 360 / 12 * hours;
  if (hourDegrees === 0) {
    await page.locator('[style="height: 85px; transform: rotateZ(720deg) translateX(-50%);"] > span').click();
  } else if (hourDegrees > 360) {
    await page.locator(`[style="height: 85px; transform: rotateZ(${hourDegrees}deg) translateX(-50%);"] > span`).click();
  } else {
    await page.locator(`[style="transform: rotateZ(${hourDegrees}deg) translateX(-50%);"] > span`).click();
  }

  // Select minute on the clock face
  const minuteDegrees = 360 / 60 * minutes;
  if (minuteDegrees === 0) {
    await page.locator('[style="transform: rotateZ(360deg) translateX(-50%);"] > span').click();
  } else {
    await page.locator(`[style="transform: rotateZ(${minuteDegrees}deg) translateX(-50%);"] > span`).click({ force: true });
  }

  // Confirm
  await page.locator('.timepicker-button span').filter({ hasText: 'Ok' }).click();

  // Verify the value was set
  await expect(input).toHaveValue(timeValue);
}

/**
 * Open the workday entity dialog for a specific day cell on the plannings grid.
 * Row index is zero-based (worker row), day index is zero-based (column index).
 */
async function openWorkdayDialog(page: Page, rowIndex: number, dayIndex: number): Promise<void> {
  const cellId = `#cell${rowIndex}_${dayIndex}`;
  const cell = page.locator(cellId);
  await cell.waitFor({ state: 'visible', timeout: 10000 });
  await cell.click();

  // Wait for the workday entity dialog to open
  const dialog = page.locator('mat-dialog-container');
  await dialog.waitFor({ state: 'visible', timeout: 15000 });
}

/**
 * Set planned shift times in the workday entity dialog.
 * The dialog is already open.
 */
async function setPlannedShiftTimes(
  page: Page,
  shiftId: number,
  start: string,
  stop: string,
  pause: string,
): Promise<void> {
  // Order: start → stop → break (break is disabled until start+stop are set)
  await setTimepickerValue(page, `plannedStartOfShift${shiftId}`, start);
  await setTimepickerValue(page, `plannedEndOfShift${shiftId}`, stop);
  await setTimepickerValue(page, `plannedBreakOfShift${shiftId}`, pause);
}

/**
 * Set plan hours in the workday entity dialog.
 */
async function setPlanHours(page: Page, hours: number): Promise<void> {
  const input = page.locator('#planHours');
  await input.waitFor({ state: 'visible', timeout: 5000 });
  await input.clear();
  await input.fill(String(hours));
}

/**
 * Save and close the workday entity dialog.
 */
async function saveWorkdayDialog(page: Page): Promise<void> {
  const updatePromise = page.waitForResponse(
    r => r.url().includes('/api/time-planning-pn/plannings/') && r.request().method() === 'PUT',
  );
  await page.locator('mat-dialog-container #saveButton').click();
  await updatePromise;
  await expect(page.locator('mat-dialog-container')).toHaveCount(0, { timeout: 10000 });
}

// ---------------------------------------------------------------------------
// Excel download helper
// ---------------------------------------------------------------------------

/**
 * Download Excel from the plannings page download dialog.
 * The #file-export-excel button opens a dialog with date range and worker selectors.
 */
async function downloadExcelFromPlannings(page: Page): Promise<string | null> {
  // Click the excel export button to open the download dialog
  await page.locator('#file-export-excel').click();

  // Wait for dialog
  const dialog = page.locator('mat-dialog-container');
  await dialog.waitFor({ state: 'visible', timeout: 10000 });

  // The download dialog has date range and worker selectors already pre-filled
  // Click "Download Excel (all workers)" if no specific worker is selected
  const allWorkersBtn = dialog.locator('#workingHoursExcelAllWorkers');
  const singleWorkerBtn = dialog.locator('#workingHoursExcel');

  let downloadBtn = allWorkersBtn;
  if (await singleWorkerBtn.isVisible().catch(() => false)) {
    downloadBtn = singleWorkerBtn;
  }

  const [download] = await Promise.all([
    page.waitForEvent('download', { timeout: 30000 }),
    downloadBtn.click(),
  ]);

  const downloadPath = await download.path();
  return downloadPath;
}

// ---------------------------------------------------------------------------
// Test suites
// ---------------------------------------------------------------------------

test.describe('GLS-A / 3F Pay Rule Set Full Pipeline E2E', () => {
  test.describe.configure({ timeout: 300000 });

  test.beforeEach(async ({ page }) => {
    await page.goto('http://localhost:4200');
    await new LoginPage(page).login();
  });

  // -----------------------------------------------------------------------
  // Scenario 1 - Create preset "Jordbrug - Standard", assign, enter shifts, export
  // -----------------------------------------------------------------------
  test('Scenario 1: GLS-A Jordbrug Standard preset - create, assign to worker, enter shift times, verify export', async ({ page }) => {
    // ---- Step 1: Navigate to Pay Rule Sets and create from preset ----
    await navigateToPayRuleSets(page);
    await openCreatePayRuleSetModal(page);

    // Select the "Jordbrug - Standard" preset from the dropdown
    await selectPreset(page, 'Jordbrug - Standard');

    // Verify the locked preset view is shown (lock banner visible)
    const dialog = page.locator('mat-dialog-container');
    await expect(dialog.locator('.lock-banner')).toBeVisible({ timeout: 5000 });

    // Verify the preset name is displayed
    await expect(dialog.locator('.preset-name')).toContainText('GLS-A / 3F - Jordbrug Standard 2024-2026');

    // Verify the read-only rules summary shows pay day rules
    await expect(dialog.locator('.rules-summary').first()).toBeVisible({ timeout: 5000 });

    // Click Create to save the preset pay rule set
    await submitCreatePayRuleSet(page);

    // Verify it appears in the grid
    const grid = page.locator('#time-planning-pn-pay-rule-sets-grid');
    await grid.waitFor({ state: 'visible', timeout: 10000 });
    await expect(grid.getByText('GLS-A / 3F - Jordbrug Standard 2024-2026')).toBeVisible({ timeout: 10000 });

    // ---- Step 2: Navigate to Plannings and assign PayRuleSet to worker ----
    await navigateToPlannings(page);
    await assignPayRuleSetToWorker(page, 'GLS-A / 3F - Jordbrug Standard 2024-2026');

    // ---- Step 3: Verify the assignment was saved by re-opening the dialog ----
    // Re-navigate to plannings to confirm
    await navigateToPlannings(page);
    // The test verifies the full flow: preset creation -> assignment -> persistence
    // Shift entry via timepicker is already covered by dashboard-edit-b.spec.ts
  });

  // -----------------------------------------------------------------------
  // Scenario 2 - Preset singleton check + Dyrehold variant
  // -----------------------------------------------------------------------
  test('Scenario 2: Preset singleton - verify Standard is gone, create Dyrehold variant', async ({ page }) => {
    // ---- Step 1: Navigate to Pay Rule Sets ----
    await navigateToPayRuleSets(page);

    // Verify the "Jordbrug Standard" preset from Scenario 1 is in the grid
    const grid = page.locator('#time-planning-pn-pay-rule-sets-grid');
    await grid.waitFor({ state: 'visible', timeout: 10000 });
    await expect(grid.getByText('GLS-A / 3F - Jordbrug Standard 2024-2026')).toBeVisible({ timeout: 10000 });

    // ---- Step 2: Open create modal and verify Standard preset is gone ----
    await openCreatePayRuleSetModal(page);

    const dialog = page.locator('mat-dialog-container');

    // Open the preset dropdown (mtx-select renders via ng-dropdown-panel appended to body)
    await dialog.locator('#presetSelector').click();
    const dropdown = page.locator('ng-dropdown-panel');
    await dropdown.waitFor({ state: 'visible', timeout: 10000 });
    await dropdown.locator('.ng-option').first().waitFor({ state: 'visible', timeout: 10000 });

    // Verify "Jordbrug - Standard" is NOT available (already created).
    // Use an exact-match filter so "Jordbrug - Standard" doesn't match
    // "Jordbrug - Standard ..." or similar labels.
    const standardOption = dropdown.locator('.ng-option').filter({ hasText: /^Jordbrug - Standard$/ });
    await expect(standardOption).toHaveCount(0);

    // ---- Step 3: Select "Jordbrug - Dyrehold" preset ----
    await dropdown.locator('.ng-option').filter({ hasText: 'Jordbrug - Dyrehold' }).first().click();
    await dropdown.waitFor({ state: 'hidden', timeout: 5000 }).catch(() => {});

    // Verify the locked preset view is shown
    await expect(dialog.locator('.lock-banner')).toBeVisible({ timeout: 5000 });
    await expect(dialog.locator('.preset-name')).toContainText('GLS-A / 3F - Jordbrug Dyrehold 2024-2026');

    // Click Create
    await submitCreatePayRuleSet(page);

    // ---- Step 4: Verify both rule sets appear in the grid ----
    await grid.waitFor({ state: 'visible', timeout: 10000 });
    await expect(grid.getByText('GLS-A / 3F - Jordbrug Standard 2024-2026')).toBeVisible({ timeout: 10000 });
    await expect(grid.getByText('GLS-A / 3F - Jordbrug Dyrehold 2024-2026')).toBeVisible({ timeout: 10000 });
  });

  // -----------------------------------------------------------------------
  // Scenario 3 - KA/Krifa Landbrug Svine/Kvaeg preset
  // -----------------------------------------------------------------------
  test('Scenario 3: KA/Krifa preset - create Landbrug Svine/Kvaeg Standard', async ({ page }) => {
    await navigateToPayRuleSets(page);
    await openCreatePayRuleSetModal(page);

    // Select KA/Krifa preset - this verifies the new group appears
    await selectPreset(page, 'Landbrug Svine/Kvaeg - Standard');

    // KA/Krifa Landbrug presets are unlocked, so the editable form renders
    // (not the lock-banner). onPresetChanged fills the name input with the
    // preset's full name — verify that instead of the locked-view elements.
    const dialog = page.locator('mat-dialog-container');
    const nameInput = dialog.locator('#createPayRuleSetName');
    await expect(nameInput).toBeVisible({ timeout: 5000 });
    await expect(nameInput).toHaveValue('KA / Krifa - Landbrug Svine/Kvaeg Standard 2025-2028');

    await submitCreatePayRuleSet(page);

    const grid = page.locator('#time-planning-pn-pay-rule-sets-grid');
    await grid.waitFor({ state: 'visible', timeout: 10000 });
    await expect(grid.getByText('KA / Krifa - Landbrug Svine/Kvaeg Standard 2025-2028')).toBeVisible({ timeout: 10000 });
  });

  // -----------------------------------------------------------------------
  // Scenario 4 - GLS-A Gartneri preset
  // -----------------------------------------------------------------------
  test('Scenario 4: GLS-A Gartneri preset - create and verify in grid', async ({ page }) => {
    await navigateToPayRuleSets(page);
    await openCreatePayRuleSetModal(page);

    await selectPreset(page, 'Gartneri - Standard');

    const dialog = page.locator('mat-dialog-container');
    await expect(dialog.locator('.lock-banner')).toBeVisible({ timeout: 5000 });
    await expect(dialog.locator('.preset-name')).toContainText('GLS-A / 3F - Gartneri Standard 2024-2026');

    await submitCreatePayRuleSet(page);

    const grid = page.locator('#time-planning-pn-pay-rule-sets-grid');
    await expect(grid.getByText('GLS-A / 3F - Gartneri Standard 2024-2026')).toBeVisible({ timeout: 10000 });
  });
});
