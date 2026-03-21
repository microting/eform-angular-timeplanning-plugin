import { test, expect } from '@playwright/test';
import { LoginPage } from '../../../Login.page';
import { PluginPage } from '../../../Plugin.page';

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
    await expect(disabledInputFields).toHaveCount(22);
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
    await expect(enabledInputFields).toHaveCount(22);
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
    await expect(disabledInputFields).toHaveCount(22);
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
    await expect(enabledInputFields).toHaveCount(22);
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

      await page.locator(`#${breakMinutesDividerFieldId}`).click();
      let degrees = 360 / 12 * (parseInt(newDayBreakMinutesDividerValues[index].split(':')[0]) % 12);
      let minuteDegrees = 360 / 60 * parseInt(newDayBreakMinutesDividerValues[index].split(':')[1]);
      if (degrees === 0) {
        await page.locator('[style="height: 85px; transform: rotateZ(720deg) translateX(-50%);"] > span').click();
      } else {
        await page.locator('[style="transform: rotateZ(' + degrees + 'deg) translateX(-50%);"] > span').click();
      }
      if (minuteDegrees > 0) {
        await page.waitForTimeout(1000);
        await page.locator('[style="transform: rotateZ(' + minuteDegrees + 'deg) translateX(-50%);"] > span').last().click({ force: true });
      }
      await page.waitForTimeout(500);
      await page.locator('.timepicker-button span').filter({ hasText: 'Ok' }).click();

      const breakMinutesPrDividerFieldId = `${day}BreakMinutesPrDivider`;
      const breakMinutesPrDividerInputField = page.locator(`#${breakMinutesPrDividerFieldId}`);
      await expect(breakMinutesPrDividerInputField).toHaveCount(1);
      await breakMinutesPrDividerInputField.scrollIntoViewIfNeeded();
      await expect(breakMinutesPrDividerInputField).toBeVisible();
      const prDividerClass = await breakMinutesPrDividerInputField.getAttribute('class') ?? '';
      expect(prDividerClass).not.toContain('mat-form-field-disabled');

      await page.locator(`#${breakMinutesPrDividerFieldId}`).click();
      degrees = 360 / 12 * (parseInt(newDayBreakMinutesPrDividerValues[index].split(':')[0]) % 12);
      minuteDegrees = 360 / 60 * parseInt(newDayBreakMinutesPrDividerValues[index].split(':')[1]);
      if (degrees === 0) {
        await page.locator('[style="height: 85px; transform: rotateZ(720deg) translateX(-50%);"] > span').click();
      } else {
        await page.locator('[style="transform: rotateZ(' + degrees + 'deg) translateX(-50%);"] > span').click();
      }
      if (minuteDegrees > 0) {
        await page.waitForTimeout(1000);
        await page.locator('[style="transform: rotateZ(' + minuteDegrees + 'deg) translateX(-50%);"] > span').last().click({ force: true });
      }
      await page.waitForTimeout(500);
      await page.locator('.timepicker-button span').filter({ hasText: 'Ok' }).click();

      const breakMinutesUpperLimitFieldId = `${day}BreakMinutesUpperLimit`;
      const breakMinutesUpperLimitInputField = page.locator(`#${breakMinutesUpperLimitFieldId}`);
      await expect(breakMinutesUpperLimitInputField).toHaveCount(1);
      await breakMinutesUpperLimitInputField.scrollIntoViewIfNeeded();
      await expect(breakMinutesUpperLimitInputField).toBeVisible();
      const upperLimitClass = await breakMinutesUpperLimitInputField.getAttribute('class') ?? '';
      expect(upperLimitClass).not.toContain('mat-form-field-disabled');

      await page.locator(`#${breakMinutesUpperLimitFieldId}`).click();
      degrees = 360 / 12 * (parseInt(newDayBreakMinutesUpperLimitValues[index].split(':')[0]) % 12);
      minuteDegrees = 360 / 60 * parseInt(newDayBreakMinutesUpperLimitValues[index].split(':')[1]);
      if (degrees === 0) {
        await page.locator('[style="height: 85px; transform: rotateZ(720deg) translateX(-50%);"] > span').click();
      } else {
        await page.locator('[style="transform: rotateZ(' + degrees + 'deg) translateX(-50%);"] > span').click();
      }
      if (minuteDegrees > 0) {
        await page.waitForTimeout(1000);
        await page.locator('[style="transform: rotateZ(' + minuteDegrees + 'deg) translateX(-50%);"] > span').last().click({ force: true });
      }
      await page.waitForTimeout(500);
      await page.locator('.timepicker-button span').filter({ hasText: 'Ok' }).click();
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
});
