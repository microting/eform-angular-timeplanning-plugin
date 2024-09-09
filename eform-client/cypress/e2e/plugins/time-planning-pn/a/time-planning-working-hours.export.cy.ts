import loginPage from '../../../Login.page';
import timePlanningWorkingHoursPage from '../TimePlanningWorkingHours.page';
import {selectDateRangeOnNewDatePicker, selectValueInNgSelector} from '../../../helper-functions';

import {read, utils} from 'xlsx';
import path = require('path');

const dateRange = {
  yearFrom: 2023,
  monthFrom: 1,
  dayFrom: 1,
  yearTo: 2023,
  monthTo: 5,
  dayTo: 11,
};
const fileNameExcelReport: string = '2023-01-01_2023-05-11_report';
const downloadsFolder: string = Cypress.config('downloadsFolder');
const fixturesFolder = Cypress.config('fixturesFolder');

describe('Time planning plugin working hours export', () => {
  beforeEach(() => {
    cy.visit('http://localhost:4200');
    loginPage.login();
    cy.intercept('GET', '/api/time-planning-pn/settings/sites').as('getData');
    timePlanningWorkingHoursPage.goToWorkingHours();
    cy.wait('@getData');
  });
  it('should enabled Time registration plugin', () => {

    timePlanningWorkingHoursPage.workingHoursRange().click();
    selectDateRangeOnNewDatePicker(
      dateRange.yearFrom, dateRange.monthFrom, dateRange.dayFrom,
      dateRange.yearTo, dateRange.monthTo, dateRange.dayTo); // select date range
    cy.intercept('POST', '/api/time-planning-pn/working-hours/index').as('postData');
    selectValueInNgSelector('#workingHoursSite', 'o p', true);// select worker
    cy.wait('@postData');

    cy.log('**GENERATE EXCEL REPORT**');
    cy.intercept('GET', '**').as('getData');
    timePlanningWorkingHoursPage.workingHoursExcel().click();
    cy.wait('@getData');
    const downloadedExcelFilename = path.join(downloadsFolder, `${fileNameExcelReport}.xlsx`);
    const fixturesExcelFilename = path.join(<string>fixturesFolder, `${fileNameExcelReport}.xlsx`);

    cy.readFile(fixturesExcelFilename, 'binary').then((file1Content) => {
      cy.readFile(downloadedExcelFilename, 'binary').then((file2Content) => {

        const workbook1 = read(file1Content, { type: 'binary' });
        const sheetName1 = workbook1.SheetNames[0]; // Assuming you're comparing the first sheet
        const sheet1 = workbook1.Sheets[sheetName1];
        const jsonData1 = utils.sheet_to_json(sheet1, { header: 1 }); // Convert sheet to array of arrays
        const workbook = read(file2Content, { type: 'binary' });
        const sheetName = workbook.SheetNames[0]; // Assuming you're comparing the first sheet
        const sheet = workbook.Sheets[sheetName];
        const jsonData = utils.sheet_to_json(sheet, { header: 1 }); // Convert sheet to array of arrays
        console.log(jsonData);
        console.log(jsonData1);
        expect(jsonData).to.deep.equal(jsonData1);
        //expect(read(file1Content, {type: 'binary'}), 'excel file').to.deep.equal(read(file2Content, {type: 'binary'}));
      });
    });
  });
});
