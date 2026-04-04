import { test, expect } from '@playwright/test';
import { LoginPage } from '../../../Page objects/Login.page';
import { TimePlanningWorkingHoursPage } from '../TimePlanningWorkingHours.page';
import { selectDateRangeOnNewDatePicker, selectValueInNgSelector } from '../../../helper-functions';
import * as XLSX from 'xlsx';
import * as path from 'path';
import * as fs from 'fs';

const dateRange = { yearFrom: 2023, monthFrom: 1, dayFrom: 1, yearTo: 2023, monthTo: 5, dayTo: 11 };
const fileNameExcelReport = '2023-01-01_2023-05-11_report';

test.describe('Time planning plugin working hours export', () => {
  test.describe.configure({ timeout: 240000 });

  test.beforeEach(async ({ page }) => {
    await page.goto('http://localhost:4200');
    await new LoginPage(page).login();
    const wh = new TimePlanningWorkingHoursPage(page);
    await Promise.all([
      page.waitForResponse('**/api/time-planning-pn/settings/sites'),
      wh.goToWorkingHours(),
    ]);
  });

  test('should export working hours to Excel', async ({ page }) => {
    const wh = new TimePlanningWorkingHoursPage(page);

    await wh.workingHoursRange().click();
    await selectDateRangeOnNewDatePicker(page,
      dateRange.yearFrom, dateRange.monthFrom, dateRange.dayFrom,
      dateRange.yearTo, dateRange.monthTo, dateRange.dayTo,
    );

    await Promise.all([
      page.waitForResponse('**/api/time-planning-pn/working-hours/index'),
      selectValueInNgSelector(page, '#workingHoursSite', 'o p', true),
    ]);

    const [download] = await Promise.all([
      page.waitForEvent('download'),
      wh.workingHoursExcel().click(),
    ]);
    const downloadPath = await download.path();

    const fixturesPath = path.join(__dirname, `${fileNameExcelReport}.xlsx`);

    const generatedContent = fs.readFileSync(downloadPath!);
    const fixtureContent = fs.readFileSync(fixturesPath);

    const wbGenerated = XLSX.read(generatedContent, { type: 'buffer' });
    const sheetGenerated = wbGenerated.Sheets[wbGenerated.SheetNames[0]];
    const jsonGenerated = XLSX.utils.sheet_to_json(sheetGenerated, { header: 1 });

    const wbFixture = XLSX.read(fixtureContent, { type: 'buffer' });
    const sheetFixture = wbFixture.Sheets[wbFixture.SheetNames[0]];
    const jsonFixture = XLSX.utils.sheet_to_json(sheetFixture, { header: 1 });

    expect(jsonGenerated).toEqual(jsonFixture);
  });
});
