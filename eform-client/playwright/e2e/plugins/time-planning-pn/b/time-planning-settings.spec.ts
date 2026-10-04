import { test, expect, Page } from '@playwright/test';
import { LoginPage } from '../../../Page objects/Login.page';
import { PluginPage } from '../../../Page objects/Plugin.page';

async function clickTimepickerValue(page: Page, inputId: string, timeStr: string): Promise<void> {
  const [hourStr, minuteStr] = timeStr.split(':');
  await page.locator(`#${inputId}`).click();

  // Clock face is 290x290px, center at (145, 145)
  const cx = 145;
  const cy = 145;

  // Wait for hour clock face
  const hourClockFace = page.locator('.clock-face');
  await hourClockFace.waitFor({ state: 'visible', timeout: 5000 });

  // Click hour using position relative to clock face element
  const hourNum = parseInt(hourStr);
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

  // Wait for minute face to render
  await page.waitForTimeout(500);
  const minuteClockFace = page.locator('.clock-face');
  await minuteClockFace.waitFor({ state: 'visible', timeout: 5000 });

  // Click minute
  const minuteNum = parseInt(minuteStr);
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
}

test.describe('Enable Backend Config plugin', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('http://localhost:4200');
    await new LoginPage(page).login();
    await new PluginPage(page).Navbar.goToPluginsPage();
  });

  test('should validate default Time registration plugin settings', async ({ page }) => {
    await page.locator('#actionMenu')
      .scrollIntoViewIfNeeded();
    await expect(page.locator('#actionMenu')).toBeVisible();
    await page.locator('#actionMenu').click({ force: true });

    const [settingsResponse] = await Promise.all([
      page.waitForResponse(r => r.url().includes('/api/time-planning-pn/settings') && r.request().method() === 'GET'),
      page.locator('#plugin-settings-link0').click(),
    ]);

    const googleSheetIdInputField = page.locator('.flex-cards.mt-3 mat-form-field');
    await expect(googleSheetIdInputField).toHaveCount(1);
    await googleSheetIdInputField.scrollIntoViewIfNeeded();
    await expect(googleSheetIdInputField).toBeVisible();
    const googleSheetClass = await googleSheetIdInputField.getAttribute('class');
    expect(googleSheetClass).not.toContain('mat-form-field-disabled');

    const disabledInputFields = page.locator('.flex-cards.mt-4 mat-form-field');
    await expect(disabledInputFields).toHaveCount(24);
    const disabledClass = await disabledInputFields.first().getAttribute('class');
    expect(disabledClass).toContain('mat-form-field-disabled');

    const dayBreakMinutesDividerValues = [
      '03:00',
      '03:00',
      '03:00',
      '03:00',
      '03:00',
      '02:00',
      '02:00'
    ];
    const dayBreakMinutesPrDividerValues = [
      '00:30',
      '00:30',
      '00:30',
      '00:30',
      '00:30',
      '00:30',
      '00:30'
    ];
    const dayBreakMinutesUpperLimitValues = [
      '01:00',
      '01:00',
      '01:00',
      '01:00',
      '01:00',
      '01:00',
      '01:00'
    ];

    const daysOfWeek = ['monday', 'tuesday', 'wednesday', 'thursday', 'friday', 'saturday', 'sunday'];
    for (let index = 0; index < daysOfWeek.length; index++) {
      const day = daysOfWeek[index];

      const breakMinutesDividerFieldId = `${day}BreakMinutesDivider`;
      const breakMinutesDividerInputField = page.locator(`#${breakMinutesDividerFieldId}`);
      await expect(breakMinutesDividerInputField).toHaveCount(1);
      await breakMinutesDividerInputField.scrollIntoViewIfNeeded();
      await expect(breakMinutesDividerInputField).toBeVisible();
      await expect(breakMinutesDividerInputField).toBeDisabled();
      await expect(breakMinutesDividerInputField).toHaveValue(new RegExp(dayBreakMinutesDividerValues[index]));

      const breakMinutesPrDividerFieldId = `${day}BreakMinutesPrDivider`;
      const breakMinutesPrDividerInputField = page.locator(`#${breakMinutesPrDividerFieldId}`);
      await expect(breakMinutesPrDividerInputField).toHaveCount(1);
      await breakMinutesPrDividerInputField.scrollIntoViewIfNeeded();
      await expect(breakMinutesPrDividerInputField).toBeVisible();
      await expect(breakMinutesPrDividerInputField).toBeDisabled();
      await expect(breakMinutesPrDividerInputField).toHaveValue(new RegExp(dayBreakMinutesPrDividerValues[index]));

      const breakMinutesUpperLimitFieldId = `${day}BreakMinutesUpperLimit`;
      const breakMinutesUpperLimitInputField = page.locator(`#${breakMinutesUpperLimitFieldId}`);
      await expect(breakMinutesUpperLimitInputField).toHaveCount(1);
      await breakMinutesUpperLimitInputField.scrollIntoViewIfNeeded();
      await expect(breakMinutesUpperLimitInputField).toBeVisible();
      await expect(breakMinutesUpperLimitInputField).toBeDisabled();
      await expect(breakMinutesUpperLimitInputField).toHaveValue(new RegExp(dayBreakMinutesUpperLimitValues[index]));
    }

    const autoBreakCalculationToggle = page.locator('#autoBreakCalculationActiveToggle');
    await autoBreakCalculationToggle.scrollIntoViewIfNeeded();
    await expect(autoBreakCalculationToggle).toBeVisible();
    await expect(autoBreakCalculationToggle.locator('button[role="switch"]')).toHaveAttribute('aria-checked', 'false');
    await autoBreakCalculationToggle.click();
    const autoBreakCalculationToggleButton = page.locator('#autoBreakCalculationActiveToggle div button');
    await expect(autoBreakCalculationToggleButton).toHaveAttribute('aria-checked', 'true');

    const enabledInputFields = page.locator('.flex-cards.mt-4 mat-form-field');
    await expect(enabledInputFields).toHaveCount(24);
    await expect(enabledInputFields.first()).toBeVisible();
    const enabledClass = await enabledInputFields.first().getAttribute('class');
    expect(enabledClass).not.toContain('mat-form-field-disabled');
  });

  test('should activate auto calculation break times', async ({ page }) => {
    await page.locator('#actionMenu')
      .scrollIntoViewIfNeeded();
    await expect(page.locator('#actionMenu')).toBeVisible();
    await page.locator('#actionMenu').click({ force: true });

    const [settingsResponse] = await Promise.all([
      page.waitForResponse(r => r.url().includes('/api/time-planning-pn/settings') && r.request().method() === 'GET'),
      page.locator('#plugin-settings-link0').click(),
    ]);

    const googleSheetIdInputField = page.locator('.flex-cards.mt-3 mat-form-field');
    await expect(googleSheetIdInputField).toHaveCount(1);
    await googleSheetIdInputField.scrollIntoViewIfNeeded();
    await expect(googleSheetIdInputField).toBeVisible();
    const googleSheetClass = await googleSheetIdInputField.getAttribute('class');
    expect(googleSheetClass).not.toContain('mat-form-field-disabled');

    const disabledInputFields = page.locator('.flex-cards.mt-4 mat-form-field');
    await expect(disabledInputFields).toHaveCount(24);
    await expect(disabledInputFields.first()).toBeVisible();
    const disabledClass = await disabledInputFields.first().getAttribute('class');
    expect(disabledClass).toContain('mat-form-field-disabled');

    const newDayBreakMinutesDividerValues = [
      '04:30',
      '05:45',
      '06:45',
      '07:40',
      '08:45',
      '09:50',
      '10:45'
    ];
    const newDayBreakMinutesPrDividerValues = [
      '01:30',
      '02:35',
      '03:40',
      '04:45',
      '05:50',
      '06:55',
      '07:30'
    ];
    const newDayBreakMinutesUpperLimitValues = [
      '02:05',
      '03:10',
      '04:15',
      '05:20',
      '06:25',
      '07:35',
      '08:40'
    ];

    const autoBreakCalculationToggle = page.locator('#autoBreakCalculationActiveToggle');
    await autoBreakCalculationToggle.scrollIntoViewIfNeeded();
    await expect(autoBreakCalculationToggle).toBeVisible();
    await expect(autoBreakCalculationToggle.locator('button[role="switch"]')).toHaveAttribute('aria-checked', 'false');
    await autoBreakCalculationToggle.click();
    const autoBreakCalculationToggleButton = page.locator('#autoBreakCalculationActiveToggle div button');
    await expect(autoBreakCalculationToggleButton).toHaveAttribute('aria-checked', 'true');

    const enabledInputFields = page.locator('.flex-cards.mt-4 mat-form-field');
    await expect(enabledInputFields).toHaveCount(24);
    await expect(enabledInputFields.first()).toBeVisible();
    const enabledClass = await enabledInputFields.first().getAttribute('class');
    expect(enabledClass).not.toContain('mat-form-field-disabled');

    const daysOfWeek = ['monday', 'tuesday', 'wednesday', 'thursday', 'friday', 'saturday', 'sunday'];
    for (let index = 0; index < daysOfWeek.length; index++) {
      const day = daysOfWeek[index];

      // set new values for break minutes divider
      const breakMinutesDividerFieldId = `${day}BreakMinutesDivider`;
      const breakMinutesDividerInputField = page.locator(`#${breakMinutesDividerFieldId}`);
      await expect(breakMinutesDividerInputField).toHaveCount(1);
      await breakMinutesDividerInputField.scrollIntoViewIfNeeded();
      await expect(breakMinutesDividerInputField).toBeVisible();
      const dividerClass = await breakMinutesDividerInputField.getAttribute('class') ?? '';
      expect(dividerClass).not.toContain('mat-form-field-disabled');

      await clickTimepickerValue(page, breakMinutesDividerFieldId, newDayBreakMinutesDividerValues[index]);

      const breakMinutesPrDividerFieldId = `${day}BreakMinutesPrDivider`;
      const breakMinutesPrDividerInputField = page.locator(`#${breakMinutesPrDividerFieldId}`);
      await expect(breakMinutesPrDividerInputField).toHaveCount(1);
      await breakMinutesPrDividerInputField.scrollIntoViewIfNeeded();
      await expect(breakMinutesPrDividerInputField).toBeVisible();
      const prDividerClass = await breakMinutesPrDividerInputField.getAttribute('class') ?? '';
      expect(prDividerClass).not.toContain('mat-form-field-disabled');

      await clickTimepickerValue(page, breakMinutesPrDividerFieldId, newDayBreakMinutesPrDividerValues[index]);

      const breakMinutesUpperLimitFieldId = `${day}BreakMinutesUpperLimit`;
      const breakMinutesUpperLimitInputField = page.locator(`#${breakMinutesUpperLimitFieldId}`);
      await expect(breakMinutesUpperLimitInputField).toHaveCount(1);
      await breakMinutesUpperLimitInputField.scrollIntoViewIfNeeded();
      await expect(breakMinutesUpperLimitInputField).toBeVisible();
      const upperLimitClass = await breakMinutesUpperLimitInputField.getAttribute('class') ?? '';
      expect(upperLimitClass).not.toContain('mat-form-field-disabled');

      await clickTimepickerValue(page, breakMinutesUpperLimitFieldId, newDayBreakMinutesUpperLimitValues[index]);
    }

    const [updateResp] = await Promise.all([
      page.waitForResponse(r => r.url().includes('/api/time-planning-pn/settings') && r.request().method() === 'PUT'),
      page.locator('#saveSettings').click(),
    ]);
    expect(updateResp.status()).toBe(200);

    await page.goto('http://localhost:4200');
    await new PluginPage(page).Navbar.goToPluginsPage();

    await page.locator('#actionMenu')
      .scrollIntoViewIfNeeded();
    await expect(page.locator('#actionMenu')).toBeVisible();
    await page.locator('#actionMenu').click({ force: true });

    const [settingsResponse2] = await Promise.all([
      page.waitForResponse(r => r.url().includes('/api/time-planning-pn/settings') && r.request().method() === 'GET'),
      page.locator('#plugin-settings-link0').click(),
    ]);

    for (let index = 0; index < daysOfWeek.length; index++) {
      const day = daysOfWeek[index];

      const breakMinutesDividerFieldId = `${day}BreakMinutesDivider`;
      const breakMinutesDividerInputField = page.locator(`#${breakMinutesDividerFieldId}`);
      await expect(breakMinutesDividerInputField).toHaveCount(1);
      await breakMinutesDividerInputField.scrollIntoViewIfNeeded();
      await expect(breakMinutesDividerInputField).toBeVisible();
      const dividerClass2 = await breakMinutesDividerInputField.getAttribute('class') ?? '';
      expect(dividerClass2).not.toContain('mat-form-field-disabled');
      await expect(page.locator(`#${breakMinutesDividerFieldId}`)).toHaveValue(new RegExp(newDayBreakMinutesDividerValues[index]));

      const breakMinutesPrDividerFieldId = `${day}BreakMinutesPrDivider`;
      const breakMinutesPrDividerInputField = page.locator(`#${breakMinutesPrDividerFieldId}`);
      await expect(breakMinutesPrDividerInputField).toHaveCount(1);
      await breakMinutesPrDividerInputField.scrollIntoViewIfNeeded();
      await expect(breakMinutesPrDividerInputField).toBeVisible();
      const prDividerClass2 = await breakMinutesPrDividerInputField.getAttribute('class') ?? '';
      expect(prDividerClass2).not.toContain('mat-form-field-disabled');
      await expect(page.locator(`#${breakMinutesPrDividerFieldId}`)).toHaveValue(new RegExp(newDayBreakMinutesPrDividerValues[index]));

      const breakMinutesUpperLimitFieldId = `${day}BreakMinutesUpperLimit`;
      const breakMinutesUpperLimitInputField = page.locator(`#${breakMinutesUpperLimitFieldId}`);
      await expect(breakMinutesUpperLimitInputField).toHaveCount(1);
      await breakMinutesUpperLimitInputField.scrollIntoViewIfNeeded();
      await expect(breakMinutesUpperLimitInputField).toBeVisible();
      const upperLimitClass2 = await breakMinutesUpperLimitInputField.getAttribute('class') ?? '';
      expect(upperLimitClass2).not.toContain('mat-form-field-disabled');
      await expect(page.locator(`#${breakMinutesUpperLimitFieldId}`)).toHaveValue(new RegExp(newDayBreakMinutesUpperLimitValues[index]));
    }
  });

  test('should toggle GPS and Snapshot settings and persist changes', async ({ page }) => {
    // Navigate to settings page
    await page.locator('#actionMenu')
      .scrollIntoViewIfNeeded();
    await expect(page.locator('#actionMenu')).toBeVisible();
    await page.locator('#actionMenu').click({ force: true });

    const [settingsResponse] = await Promise.all([
      page.waitForResponse(r => r.url().includes('/api/time-planning-pn/settings') && r.request().method() === 'GET'),
      page.locator('#plugin-settings-link0').click(),
    ]);

    // Test GPS toggle - turn ON
    const gpsToggle = page.locator('#gpsEnabledToggle');
    await gpsToggle.click();

    let gpsToggleButton = page.locator('#gpsEnabledToggle div button');
    await expect(gpsToggleButton).toHaveAttribute('aria-checked', 'true');

    // Save settings
    const [updateResp1] = await Promise.all([
      page.waitForResponse(r => r.url().includes('/api/time-planning-pn/settings') && r.request().method() === 'PUT'),
      page.locator('#saveSettings').click(),
    ]);
    expect(updateResp1.status()).toBe(200);

    // Reload page and verify GPS is still ON
    await page.goto('http://localhost:4200');
    await new PluginPage(page).Navbar.goToPluginsPage();
    await page.locator('#actionMenu')
      .scrollIntoViewIfNeeded();
    await expect(page.locator('#actionMenu')).toBeVisible();
    await page.locator('#actionMenu').click({ force: true });
    await Promise.all([
      page.waitForResponse(r => r.url().includes('/api/time-planning-pn/settings') && r.request().method() === 'GET'),
      page.locator('#plugin-settings-link0').click(),
    ]);

    gpsToggleButton = page.locator('#gpsEnabledToggle div button');
    await expect(gpsToggleButton).toHaveAttribute('aria-checked', 'true');

    // Test GPS toggle - turn OFF
    const snapshotToggle = page.locator('#snapshotEnabledToggle');
    const gpsToggle2 = page.locator('#gpsEnabledToggle');
    await gpsToggle2.click();

    gpsToggleButton = page.locator('#gpsEnabledToggle div button');
    await expect(gpsToggleButton).toHaveAttribute('aria-checked', 'false');

    // Save settings
    const [updateResp2] = await Promise.all([
      page.waitForResponse(r => r.url().includes('/api/time-planning-pn/settings') && r.request().method() === 'PUT'),
      page.locator('#saveSettings').click(),
    ]);
    expect(updateResp2.status()).toBe(200);

    // Reload page and verify GPS is still OFF
    await page.goto('http://localhost:4200');
    await new PluginPage(page).Navbar.goToPluginsPage();
    await page.locator('#actionMenu')
      .scrollIntoViewIfNeeded();
    await expect(page.locator('#actionMenu')).toBeVisible();
    await page.locator('#actionMenu').click({ force: true });
    await Promise.all([
      page.waitForResponse(r => r.url().includes('/api/time-planning-pn/settings') && r.request().method() === 'GET'),
      page.locator('#plugin-settings-link0').click(),
    ]);

    gpsToggleButton = page.locator('#gpsEnabledToggle div button');
    await expect(gpsToggleButton).toHaveAttribute('aria-checked', 'false');

    // Test Snapshot toggle - turn ON
    await snapshotToggle.click();

    let snapshotToggleButton = page.locator('#snapshotEnabledToggle div button');
    await expect(snapshotToggleButton).toHaveAttribute('aria-checked', 'true');

    // Save settings
    const [updateResp3] = await Promise.all([
      page.waitForResponse(r => r.url().includes('/api/time-planning-pn/settings') && r.request().method() === 'PUT'),
      page.locator('#saveSettings').click(),
    ]);
    expect(updateResp3.status()).toBe(200);

    // Reload page and verify Snapshot is still ON
    await page.goto('http://localhost:4200');
    await new PluginPage(page).Navbar.goToPluginsPage();
    await page.locator('#actionMenu')
      .scrollIntoViewIfNeeded();
    await expect(page.locator('#actionMenu')).toBeVisible();
    await page.locator('#actionMenu').click({ force: true });
    await Promise.all([
      page.waitForResponse(r => r.url().includes('/api/time-planning-pn/settings') && r.request().method() === 'GET'),
      page.locator('#plugin-settings-link0').click(),
    ]);

    snapshotToggleButton = page.locator('#snapshotEnabledToggle div button');
    await expect(snapshotToggleButton).toHaveAttribute('aria-checked', 'true');

    // Test Snapshot toggle - turn OFF
    const snapshotToggle2 = page.locator('#snapshotEnabledToggle');
    await snapshotToggle2.click();

    snapshotToggleButton = page.locator('#snapshotEnabledToggle div button');
    await expect(snapshotToggleButton).toHaveAttribute('aria-checked', 'false');

    // Save settings
    const [updateResp4] = await Promise.all([
      page.waitForResponse(r => r.url().includes('/api/time-planning-pn/settings') && r.request().method() === 'PUT'),
      page.locator('#saveSettings').click(),
    ]);
    expect(updateResp4.status()).toBe(200);

    // Reload page and verify Snapshot is still OFF
    await page.goto('http://localhost:4200');
    await new PluginPage(page).Navbar.goToPluginsPage();
    await page.locator('#actionMenu')
      .scrollIntoViewIfNeeded();
    await expect(page.locator('#actionMenu')).toBeVisible();
    await page.locator('#actionMenu').click({ force: true });
    await Promise.all([
      page.waitForResponse(r => r.url().includes('/api/time-planning-pn/settings') && r.request().method() === 'GET'),
      page.locator('#plugin-settings-link0').click(),
    ]);

    snapshotToggleButton = page.locator('#snapshotEnabledToggle div button');
    await expect(snapshotToggleButton).toHaveAttribute('aria-checked', 'false');
  });

  /**
   * #1745: the toggles sit at the bottom of the page, far from the page-wide save in
   * the subheader, and the only button near them saves payroll settings only. The
   * GPS/Snapshot card now has its own save, and the payroll button says what it saves.
   */
  test('saves the GPS toggle from the button in its own card, not from the payroll button', async ({ page }) => {
    // Three settings-page loads (open, reload, restore) on top of the login.
    test.setTimeout(180000);
    const UI_TIMEOUT = 15000;
    const API_TIMEOUT = 30000;
    const isPath = (url: string, path: string) => new URL(url).pathname === path;
    const SETTINGS = '/api/time-planning-pn/settings';
    const PAYROLL_SETTINGS = '/api/time-planning-pn/payroll/settings';
    const actionMenu = page.locator('#actionMenu');
    const gpsToggle = page.locator('#gpsEnabledToggle');
    const gpsChecked = gpsToggle.locator('div button');

    // Waits for BOTH loads: a payroll save before its GET lands would write the
    // component's defaults over the real payroll settings.
    const openSettings = async () => {
      await actionMenu.scrollIntoViewIfNeeded();
      await expect(actionMenu).toBeVisible({ timeout: UI_TIMEOUT });
      await actionMenu.click();
      await Promise.all([
        page.waitForResponse(r => isPath(r.url(), SETTINGS) && r.request().method() === 'GET',
          { timeout: API_TIMEOUT }),
        page.waitForResponse(r => isPath(r.url(), PAYROLL_SETTINGS) && r.request().method() === 'GET',
          { timeout: API_TIMEOUT }),
        page.locator('#plugin-settings-link0').click(),
      ]);
    };
    const saveFromCard = async () => {
      const [response] = await Promise.all([
        page.waitForResponse(r => isPath(r.url(), SETTINGS) && r.request().method() === 'PUT',
          { timeout: API_TIMEOUT }),
        page.locator('#saveGpsSnapshotSettings').click(),
      ]);
      expect(response.status()).toBe(200);
    };

    await openSettings();
    await expect(gpsChecked).toHaveAttribute('aria-checked', 'false', { timeout: UI_TIMEOUT });
    await gpsToggle.click();
    await expect(gpsChecked).toHaveAttribute('aria-checked', 'true', { timeout: UI_TIMEOUT });

    // The payroll button saves payroll only, and leaves the pending toggle alone.
    let generalPuts = 0;
    page.on('request', r => {
      if (isPath(r.url(), SETTINGS) && r.method() === 'PUT') {
        generalPuts++;
      }
    });
    const [payrollResp] = await Promise.all([
      page.waitForResponse(r => isPath(r.url(), PAYROLL_SETTINGS) && r.request().method() === 'PUT',
        { timeout: API_TIMEOUT }),
      page.locator('#savePayrollSettings').click(),
    ]);
    expect(payrollResp.status()).toBe(200);
    expect(generalPuts, 'the payroll button sends no general-settings PUT').toBe(0);
    await expect(gpsChecked).toHaveAttribute('aria-checked', 'true', { timeout: UI_TIMEOUT });

    // The card's own button persists it.
    await saveFromCard();

    await page.goto('http://localhost:4200');
    await new PluginPage(page).Navbar.goToPluginsPage();
    await openSettings();
    await expect(gpsChecked).toHaveAttribute('aria-checked', 'true', { timeout: UI_TIMEOUT });

    // Leave the shard's shared settings as the earlier test left them.
    await gpsToggle.click();
    await expect(gpsChecked).toHaveAttribute('aria-checked', 'false', { timeout: UI_TIMEOUT });
    await saveFromCard();
  });
});
