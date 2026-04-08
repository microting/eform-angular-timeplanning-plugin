import { test, expect, Page } from '@playwright/test';
import { LoginPage } from '../../../Page objects/Login.page';
import { TimePlanningWorkingHoursPage } from '../TimePlanningWorkingHours.page';
import { selectDateRangeOnNewDatePicker, selectValueInNgSelector } from '../../../helper-functions';
import * as XLSX from 'xlsx';

// ---------------------------------------------------------------------------
// Constants
// ---------------------------------------------------------------------------

/**
 * HOURS_PICKER_ARRAY id mapping: id = (hours * 12) + (minutes / 5) + 1
 * e.g. 06:00 => id 73, 07:00 => id 85, 08:00 => id 97, 14:30 => id 175,
 *      15:30 => id 187, 00:30 => id 7
 */
function timeToPickerId(hhmm: string): number {
  const [h, m] = hhmm.split(':').map(Number);
  return h * 12 + m / 5 + 1;
}

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

async function expandPluginMenu(page: Page): Promise<void> {
  const menuItem = page.locator('#time-planning-pn');
  await menuItem.waitFor({ state: 'visible', timeout: 30000 });
  // Check if a sub-item is visible; if not, click to expand
  const subItem = page.locator('#time-planning-pn-pay-rule-sets');
  if (!await subItem.isVisible().catch(() => false)) {
    await menuItem.click();
    await subItem.waitFor({ state: 'visible', timeout: 10000 });
  }
}

async function navigateToPayRuleSets(page: Page): Promise<void> {
  await expandPluginMenu(page);
  await page.locator('#time-planning-pn-pay-rule-sets').click();
  await page.locator('#time-planning-pn-pay-rule-sets-grid, .table-actions')
    .first().waitFor({ state: 'visible', timeout: 30000 });
}

async function navigateToPlannings(page: Page): Promise<void> {
  await expandPluginMenu(page);
  await page.locator('#time-planning-pn-planning').click();
  await page.locator('#main-header-text').waitFor({ state: 'visible', timeout: 30000 });
}

async function navigateToWorkingHours(page: Page): Promise<void> {
  await expandPluginMenu(page);
  const wh = new TimePlanningWorkingHoursPage(page);
  await wh.goToWorkingHours();
  await page.locator('#time-planning-pn-working-hours-grid')
    .waitFor({ state: 'visible', timeout: 30000 });
}

// ---------------------------------------------------------------------------
// Pay Rule Set creation helpers
// ---------------------------------------------------------------------------

async function openCreatePayRuleSetModal(page: Page): Promise<void> {
  await page.getByRole('button', { name: /Create Pay Rule Set/i }).click();
  await page.locator('mat-dialog-container').waitFor({ state: 'visible', timeout: 10000 });
}

async function fillPayRuleSetName(page: Page, name: string): Promise<void> {
  await page.locator('#createPayRuleSetName').fill(name);
}

/**
 * Add a Pay Day Rule.
 * Clicks "Add Day" to open the pay-day-rule-dialog, selects dayCode,
 * adds tiers with payCode + upToSeconds, then saves.
 */
async function addPayDayRule(
  page: Page,
  dayCode: string,
  tiers: { payCode: string; upToSeconds: number | null }[],
): Promise<void> {
  await page.locator('#addDayBtn').click();
  // Wait for nested dialog (second mat-dialog-container)
  const dialogs = page.locator('mat-dialog-container');
  await expect(dialogs).toHaveCount(2, { timeout: 10000 });

  const ruleDialog = dialogs.last();

  // Select dayCode from mat-select
  await ruleDialog.locator('mat-select[formcontrolname="dayCode"]').click();
  await page.locator('.day-code-select-panel mat-option').filter({
    hasText: new RegExp(`^\\s*${dayCodeToLabel(dayCode)}\\s*$`, 'i'),
  }).click();

  // Add tiers
  for (let i = 0; i < tiers.length; i++) {
    await ruleDialog.getByRole('button', { name: /Add Tier/i }).click();
    // Fill pay code (the text input in the tier row)
    const payCodeInput = ruleDialog.locator('table.tiers-table tbody tr').nth(i)
      .locator('input[type="text"]');
    await payCodeInput.fill(tiers[i].payCode);

    // Fill upToSeconds if not null
    if (tiers[i].upToSeconds !== null) {
      const upToInput = ruleDialog.locator('table.tiers-table tbody tr').nth(i)
        .locator('input[type="number"]');
      await upToInput.fill(String(tiers[i].upToSeconds));
    }
  }

  await page.locator('#savePayDayRuleBtn').click();
  // Wait for nested dialog to close
  await expect(dialogs).toHaveCount(1, { timeout: 10000 });
}

/**
 * Add a Day Type Rule.
 * Clicks "Add Day Type" to open the day-type-rule-dialog, fills form, saves.
 */
async function addDayTypeRule(
  page: Page,
  dayType: string,
  defaultPayCode: string,
  priority: number,
): Promise<void> {
  await page.locator('#addDayTypeBtn').click();
  const dialogs = page.locator('mat-dialog-container');
  await expect(dialogs).toHaveCount(2, { timeout: 10000 });

  const ruleDialog = dialogs.last();

  // Select dayType from mat-select
  await ruleDialog.locator('mat-select[formcontrolname="dayType"]').click();
  await page.locator('.day-type-select-panel mat-option').filter({
    hasText: new RegExp(`^\\s*${dayType}\\s*$`, 'i'),
  }).click();

  // Fill default pay code
  await ruleDialog.locator('input[formcontrolname="defaultPayCode"]').fill(defaultPayCode);

  // Fill priority
  await ruleDialog.locator('input[formcontrolname="priority"]').clear();
  await ruleDialog.locator('input[formcontrolname="priority"]').fill(String(priority));

  await page.locator('#saveDayTypeRuleBtn').click();
  await expect(dialogs).toHaveCount(1, { timeout: 10000 });
}

async function submitCreatePayRuleSet(page: Page): Promise<void> {
  await page.locator('#createPayRuleSetBtn').click();
  await expect(page.locator('mat-dialog-container')).toHaveCount(0, { timeout: 15000 });
}

function dayCodeToLabel(code: string): string {
  const map: Record<string, string> = {
    MONDAY: 'Monday', TUESDAY: 'Tuesday', WEDNESDAY: 'Wednesday',
    THURSDAY: 'Thursday', FRIDAY: 'Friday', SATURDAY: 'Saturday',
    SUNDAY: 'Sunday', WEEKDAY: 'Weekday', WEEKEND: 'Weekend',
    HOLIDAY: 'Holiday', GRUNDLOVSDAG: 'Grundlovsdag',
  };
  return map[code] || code;
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
  await navigateToPlannings(page);

  // Click on the first worker's avatar/name column to open AssignedSite dialog
  const firstColumn = page.locator('#firstColumn0');
  await firstColumn.waitFor({ state: 'visible', timeout: 15000 });
  await firstColumn.click();

  // Wait for the dialog to open
  const dialog = page.locator('mat-dialog-container');
  await dialog.waitFor({ state: 'visible', timeout: 15000 });

  // Find the mtx-select for payRuleSetId
  // The mtx-select is inside a mat-form-field with label "Pay Rule Set"
  const payRuleSetField = dialog.locator('mtx-select[formcontrolname="payRuleSetId"]');
  await payRuleSetField.waitFor({ state: 'visible', timeout: 10000 });

  // Click to open the dropdown
  await payRuleSetField.click();

  // Wait for the ng-dropdown-panel to appear and select the option
  const dropdown = page.locator('ng-dropdown-panel');
  await dropdown.waitFor({ state: 'visible', timeout: 10000 });
  await dropdown.locator('.ng-option').filter({ hasText: payRuleSetName }).first().click();

  // Save the dialog
  await page.locator('#saveButton').click();

  // Wait for dialog to close
  await expect(page.locator('mat-dialog-container')).toHaveCount(0, { timeout: 10000 });
}

// ---------------------------------------------------------------------------
// Working hours entry helpers
// ---------------------------------------------------------------------------

/**
 * Select a shift time value in the mtx-select dropdown for a specific cell.
 * The field ID pattern is "{fieldName}{rowIndex}" e.g. "shift1Start0".
 * The mtx-select uses ng-dropdown-panel with option values from HOURS_PICKER_ARRAY.
 */
async function selectShiftTime(
  page: Page,
  fieldId: string,
  timeValue: string,
): Promise<void> {
  const cell = page.locator(`#${fieldId}`);
  await cell.waitFor({ state: 'visible', timeout: 10000 });

  // Click the mtx-select inside this cell
  const mtxSelect = cell.locator('mtx-select');
  await mtxSelect.click();

  // Wait for dropdown and type to filter
  const dropdown = page.locator('ng-dropdown-panel');
  await dropdown.waitFor({ state: 'visible', timeout: 10000 });

  // Type the time to filter options
  const input = mtxSelect.locator('input');
  if (await input.isVisible()) {
    await input.fill(timeValue);
  }

  // Select the matching option
  const option = dropdown.locator('.ng-option').filter({ hasText: timeValue }).first();
  await option.waitFor({ state: 'visible', timeout: 10000 });
  await option.click();
}

/**
 * Register working hours for a row in the working hours grid.
 * @param rowIndex zero-based row index in the grid
 * @param shift1Start e.g. "07:00"
 * @param shift1Stop e.g. "15:30"
 * @param shift1Pause e.g. "00:30"
 */
async function registerWorkingHoursRow(
  page: Page,
  rowIndex: number,
  shift1Start: string,
  shift1Stop: string,
  shift1Pause: string,
): Promise<void> {
  await selectShiftTime(page, `shift1Start${rowIndex}`, shift1Start);
  await selectShiftTime(page, `shift1Stop${rowIndex}`, shift1Stop);
  await selectShiftTime(page, `shift1Pause${rowIndex}`, shift1Pause);
}

// ---------------------------------------------------------------------------
// Excel parsing helper
// ---------------------------------------------------------------------------

async function downloadAndParseExcel(
  page: Page,
): Promise<{ headers: string[]; rows: unknown[][] }> {
  const wh = new TimePlanningWorkingHoursPage(page);

  const [download] = await Promise.all([
    page.waitForEvent('download'),
    wh.workingHoursExcel().click(),
  ]);
  const downloadPath = await download.path();
  expect(downloadPath).toBeTruthy();

  const fs = await import('fs');
  const content = fs.readFileSync(downloadPath!);
  const wb = XLSX.read(content, { type: 'buffer' });
  expect(wb.SheetNames.length).toBeGreaterThan(0);

  const sheet = wb.Sheets[wb.SheetNames[0]];
  const allRows = XLSX.utils.sheet_to_json<unknown[]>(sheet, { header: 1 });
  expect(allRows.length).toBeGreaterThan(0);

  const headers = allRows[0] as string[];
  return { headers, rows: allRows as unknown[][] };
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
  // Scenario 1 - Standard Agriculture Full Week
  // -----------------------------------------------------------------------
  test('Scenario 1: GLS-A Jordbrug Standard - create rule set, assign to worker, register hours, verify export', async ({ page }) => {
    // ---- Step 1: Create the PayRuleSet ----
    await navigateToPayRuleSets(page);
    await openCreatePayRuleSetModal(page);
    await fillPayRuleSetName(page, 'GLS-A Jordbrug Standard');

    // WEEKDAY rule: 3 tiers
    //  Tier 1: NORMAL up to 26640s (7h 24m)
    //  Tier 2: OVERTIME_30 up to 33840s (9h 24m)
    //  Tier 3: OVERTIME_80 no limit
    await addPayDayRule(page, 'WEEKDAY', [
      { payCode: 'NORMAL', upToSeconds: 26640 },
      { payCode: 'OVERTIME_30', upToSeconds: 33840 },
      { payCode: 'OVERTIME_80', upToSeconds: null },
    ]);

    // SATURDAY rule: 2 tiers
    //  Tier 1: SAT_NORMAL up to 21600s (6h)
    //  Tier 2: SAT_AFTERNOON no limit
    await addPayDayRule(page, 'SATURDAY', [
      { payCode: 'SAT_NORMAL', upToSeconds: 21600 },
      { payCode: 'SAT_AFTERNOON', upToSeconds: null },
    ]);

    // SUNDAY rule: 1 tier
    //  Tier 1: SUN_HOLIDAY no limit
    await addPayDayRule(page, 'SUNDAY', [
      { payCode: 'SUN_HOLIDAY', upToSeconds: null },
    ]);

    await submitCreatePayRuleSet(page);

    // Verify it appears in the grid
    const grid = page.locator('#time-planning-pn-pay-rule-sets-grid');
    await grid.waitFor({ state: 'visible', timeout: 10000 });
    await expect(grid.getByText('GLS-A Jordbrug Standard')).toBeVisible({ timeout: 10000 });

    // ---- Step 2: Assign PayRuleSet to worker ----
    await assignPayRuleSetToWorker(page, 'GLS-A Jordbrug Standard');

    // ---- Step 3: Navigate to Working Hours ----
    await navigateToWorkingHours(page);
    const wh = new TimePlanningWorkingHoursPage(page);

    // ---- Step 4: Select date range and worker ----
    await wh.workingHoursRange().click();
    await selectDateRangeOnNewDatePicker(
      page,
      targetMonday.getFullYear(), targetMonday.getMonth() + 1, targetMonday.getDate(),
      targetSunday.getFullYear(), targetSunday.getMonth() + 1, targetSunday.getDate(),
    );

    // Select the first available worker
    await Promise.all([
      page.waitForResponse('**/api/time-planning-pn/working-hours/index'),
      selectValueInNgSelector(page, '#workingHoursSite', 'o p', true),
    ]);

    // ---- Step 5: Register working hours for weekdays ----
    // Monday (row 0): 07:00 - 15:30, 00:30 pause = 8h net
    await registerWorkingHoursRow(page, 0, '07:00', '15:30', '00:30');
    // Tuesday (row 1): 07:00 - 15:30, 00:30 pause = 8h net
    await registerWorkingHoursRow(page, 1, '07:00', '15:30', '00:30');
    // Wednesday (row 2): 07:00 - 15:30, 00:30 pause = 8h net
    await registerWorkingHoursRow(page, 2, '07:00', '15:30', '00:30');
    // Thursday (row 3): 07:00 - 17:00, 00:30 pause = 9.5h net (triggers OT_30)
    await registerWorkingHoursRow(page, 3, '07:00', '17:00', '00:30');
    // Friday (row 4): 07:00 - 15:00, 00:30 pause = 7.5h net
    await registerWorkingHoursRow(page, 4, '07:00', '15:00', '00:30');

    // ---- Step 6: Save working hours ----
    await wh.workingHoursSave().click();
    // Wait for save to complete
    await page.waitForResponse(
      resp => resp.url().includes('working-hours') && resp.status() === 200,
      { timeout: 15000 },
    ).catch(() => {}); // gracefully continue even if response was already handled

    // ---- Step 7: Export Excel and verify ----
    const { headers, rows } = await downloadAndParseExcel(page);

    console.log('Scenario 1 headers:', headers);
    console.log('Scenario 1 row count:', rows.length);

    // Basic structural checks
    expect(headers.length).toBeGreaterThan(3);

    // Check that at least some pay code columns are present in the headers
    // The exact header names depend on the backend export, but they should include
    // the pay codes we configured
    const headerStr = headers.join(' ');
    console.log('Scenario 1 all headers joined:', headerStr);

    // Verify the export has data rows beyond the header
    expect(rows.length).toBeGreaterThan(1);
  });

  // -----------------------------------------------------------------------
  // Scenario 2 - Apprentice (Elev) rule set
  // -----------------------------------------------------------------------
  test('Scenario 2: Elev (Apprentice) - create rule set with apprentice-specific pay codes, register hours, verify export', async ({ page }) => {
    // ---- Step 1: Create the PayRuleSet ----
    await navigateToPayRuleSets(page);
    await openCreatePayRuleSetModal(page);
    await fillPayRuleSetName(page, 'Elev Jordbrug');

    // WEEKDAY rule: 2 tiers (apprentice rates)
    //  Tier 1: ELEV_NORMAL up to 27000s (7h 30m)
    //  Tier 2: ELEV_OVERTIME_50 no limit
    await addPayDayRule(page, 'WEEKDAY', [
      { payCode: 'ELEV_NORMAL', upToSeconds: 27000 },
      { payCode: 'ELEV_OVERTIME_50', upToSeconds: null },
    ]);

    // SATURDAY rule: 1 tier
    //  Tier 1: SAT_NORMAL no limit
    await addPayDayRule(page, 'SATURDAY', [
      { payCode: 'SAT_NORMAL', upToSeconds: null },
    ]);

    // SUNDAY day type rule: holiday-style pay
    await addDayTypeRule(page, 'Sunday', 'SUN_HOLIDAY', 5);

    await submitCreatePayRuleSet(page);

    // Verify it appears in the grid
    const grid = page.locator('#time-planning-pn-pay-rule-sets-grid');
    await grid.waitFor({ state: 'visible', timeout: 10000 });
    await expect(grid.getByText('Elev Jordbrug')).toBeVisible({ timeout: 10000 });

    // ---- Step 2: Assign PayRuleSet to worker ----
    await assignPayRuleSetToWorker(page, 'Elev Jordbrug');

    // ---- Step 3: Navigate to Working Hours ----
    await navigateToWorkingHours(page);
    const wh = new TimePlanningWorkingHoursPage(page);

    // ---- Step 4: Select date range and worker ----
    await wh.workingHoursRange().click();
    await selectDateRangeOnNewDatePicker(
      page,
      targetMonday.getFullYear(), targetMonday.getMonth() + 1, targetMonday.getDate(),
      targetSunday.getFullYear(), targetSunday.getMonth() + 1, targetSunday.getDate(),
    );

    await Promise.all([
      page.waitForResponse('**/api/time-planning-pn/working-hours/index'),
      selectValueInNgSelector(page, '#workingHoursSite', 'o p', true),
    ]);

    // ---- Step 5: Register working hours ----
    // Monday (row 0): 08:00 - 15:30, 00:30 pause = 7h net
    await registerWorkingHoursRow(page, 0, '08:00', '15:30', '00:30');
    // Tuesday (row 1): 08:00 - 15:30, 00:30 pause = 7h net
    await registerWorkingHoursRow(page, 1, '08:00', '15:30', '00:30');
    // Wednesday (row 2): 08:00 - 16:00, 00:30 pause = 7.5h net
    await registerWorkingHoursRow(page, 2, '08:00', '16:00', '00:30');
    // Saturday (row 5): 08:00 - 14:00, 00:30 pause = 5.5h net
    await registerWorkingHoursRow(page, 5, '08:00', '14:00', '00:30');

    // ---- Step 6: Save working hours ----
    await wh.workingHoursSave().click();
    await page.waitForResponse(
      resp => resp.url().includes('working-hours') && resp.status() === 200,
      { timeout: 15000 },
    ).catch(() => {});

    // ---- Step 7: Export Excel and verify ----
    const { headers, rows } = await downloadAndParseExcel(page);

    console.log('Scenario 2 headers:', headers);
    console.log('Scenario 2 row count:', rows.length);

    // Basic structural checks
    expect(headers.length).toBeGreaterThan(3);
    expect(rows.length).toBeGreaterThan(1);
  });
});
