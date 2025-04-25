import loginPage from '../../../Login.page';
import {selectDateRangeOnNewDatePicker} from '../../../helper-functions';
import TimePlanningWorkingHoursPage from '../TimePlanningWorkingHours.page';
import pluginPage from '../../../Plugin.page';


// thisMonday should be monday of the current week
const formatDate = (date: Date): string => {
  const day = date.getDate();
  const month = date.getMonth() + 1; // 0-indexed
  const year = date.getFullYear();
  return `${day}.${month}.${year}`;
};

// Utility to get Monday of a week given a base date
const getMonday = (baseDate: Date): Date => {
  const dayOfWeek = baseDate.getDay(); // 0 (Sun) to 6 (Sat)
  const diffToMonday = dayOfWeek === 0 ? -6 : 1 - dayOfWeek;
  const monday = new Date(baseDate);
  monday.setDate(baseDate.getDate() + diffToMonday);
  return monday;
};

const getSunday = (monday: Date): Date => {
  const sunday = new Date(monday);
  sunday.setDate(monday.getDate() + 6);
  return sunday;
};

// Utility to generate a full week from a given Monday
const getWeekDates = (monday: Date): string[] => {
  const dates: string[] = [];
  for (let i = 0; i < 7; i++) {
    const date = new Date(monday);
    date.setDate(monday.getDate() + i);
    dates.push(formatDate(date));
  }
  return dates;
};

// Get reference point (today)

const today = new Date();

// Last week
const lastWeekBase = new Date(today);
lastWeekBase.setDate(today.getDate() - 7);
const lastWeekMonday = getMonday(lastWeekBase);
const lastWeekSunday = getSunday(lastWeekMonday);
const lastWeekDates = getWeekDates(lastWeekMonday);

// This week
const thisWeekMonday = getMonday(today);
const thisWeekSunday = getSunday(thisWeekMonday);
const thisWeekDates = getWeekDates(thisWeekMonday);

// Next week
const nextWeekBase = new Date(today);
nextWeekBase.setDate(today.getDate() + 7);
const nextWeekMonday = getMonday(nextWeekBase);
const nextWeekSunday = getSunday(nextWeekMonday);
const nextWeekDates = getWeekDates(nextWeekMonday);

const filters = [
  {
    dateRange: {
      yearFrom: lastWeekMonday.getFullYear(),
      monthFrom: lastWeekMonday.getMonth() + 1, // getMonth() returns 0-indexed month
      dayFrom: lastWeekMonday.getDate(),
      yearTo: lastWeekSunday.getFullYear(),
      monthTo: lastWeekSunday.getMonth() + 1,
      dayTo: lastWeekSunday.getDate(),
    },
  },
];

const filtersNextWeek = [
  {
    dateRange: {
      yearFrom: nextWeekMonday.getFullYear(),
      monthFrom: nextWeekMonday.getMonth() + 1,
      dayFrom: nextWeekMonday.getDate(),
      yearTo: nextWeekSunday.getFullYear(),
      monthTo: nextWeekSunday.getMonth() + 1,
      dayTo: nextWeekSunday.getDate(),
    },
  },
];

const planHours = [
      { date: lastWeekDates[0], hours: 8, sumFlex: 89.45, nettoHours: 0, flex: -8, humanFlex: '89:27'},
      { date: lastWeekDates[1], hours: 8, sumFlex: 81.45, nettoHours: 0, flex: -8, humanFlex: '81:27'},
      { date: lastWeekDates[2], hours: 8, sumFlex: 73.45, nettoHours: 0, flex: -8, humanFlex: '73:27'},
      { date: lastWeekDates[3], hours: 8, sumFlex: 65.45, nettoHours: 0, flex: -8, humanFlex: '65:27'},
      { date: lastWeekDates[4], hours: 8, sumFlex: 57.45, nettoHours: 0, flex: -8, humanFlex: '57:27'},
      { date: lastWeekDates[5], hours: 8, sumFlex: 49.45, nettoHours: 0, flex: -8, humanFlex: '49:27'},
      { date: lastWeekDates[6], hours: 8, sumFlex: 41.45, nettoHours: 0, flex: -8, humanFlex: '41:27'},
];

const planHoursNextWeek = [
  { date: nextWeekDates[0], hours: 8, sumFlex: 33.45, nettoHours: 0, flex: -8, humanFlex: '33:27'},
  { date: nextWeekDates[1], hours: 8, sumFlex: 25.45, nettoHours: 0, flex: -8, humanFlex: '25:27'},
  { date: nextWeekDates[2], hours: 8, sumFlex: 17.45, nettoHours: 0, flex: -8, humanFlex: '17:27'},
  { date: nextWeekDates[3], hours: 8, sumFlex: 9.45, nettoHours: 0, flex: -8, humanFlex: '9:27'},
  { date: nextWeekDates[4], hours: 8, sumFlex: 1.45, nettoHours: 0, flex: -8, humanFlex: '1:27'},
  { date: nextWeekDates[5], hours: 8, sumFlex: -6.55, nettoHours: 0, flex: -8, humanFlex: '-6:33'},
  { date: nextWeekDates[6], hours: 8, sumFlex: -14.55, nettoHours: 0, flex: -8, humanFlex: '-14:33'},
];

const planTexts = [
  { date: lastWeekDates[0], text: '07:30-15:30' },
  { date: lastWeekDates[1], text: '7:45-16:00/1' },
  { date: lastWeekDates[2], text: '7:15-16:00/1;17-20/0,5' },
  { date: lastWeekDates[3], text: '6-12/½;18:00-20:00/0.5' },
  { date: lastWeekDates[4], text: '06:00-12:00/¾;18:00-20:00/0.5' },
  { date: lastWeekDates[5], text: '6-12/¾;18-20/¾' },
  { date: lastWeekDates[6], text: '6-14/½' },
];

const planTextsNextWeek = [
  { date: nextWeekDates[0], text: '07:30-15:30' },
  { date: nextWeekDates[1], text: '7:45-16:00/1' },
  { date: nextWeekDates[2], text: '7:15-16:00/1;17-20/0,5' },
  { date: nextWeekDates[3], text: '6-12/½;18:00-20:00/0.5' },
  { date: nextWeekDates[4], text: '06:00-12:00/¾;18:00-20:00/0.5' },
  { date: nextWeekDates[5], text: '6-12/¾;18-20/¾' },
  { date: lastWeekDates[6], text: '6-14/½' },
];


describe('Enable Backend Config plugin', () => {
  beforeEach(() => {
    cy.visit('http://localhost:4200');
    loginPage.login();
  });

  it('should go to dashboard', () => {
    // we have more than one mat-nested-tree-node so we beed to select the own with the text "Timeregistrering"
    cy.get('mat-nested-tree-node').contains('Timeregistrering').click();
    cy.get('mat-tree-node').contains('Timeregistrering').click();
    cy.get('mat-toolbar > button .mat-mdc-button-persistent-ripple').parent().click();
    cy.get('#workingHoursSite').clear().type('c d');
    cy.get('.ng-option.ng-option-marked').click();
    TimePlanningWorkingHoursPage.dateFormInput().click();
    selectDateRangeOnNewDatePicker(
      filters[0].dateRange.yearFrom, filters[0].dateRange.monthFrom,  filters[0].dateRange.dayFrom,
      filters[0].dateRange.yearTo, filters[0].dateRange.monthTo, filters[0].dateRange.dayTo
    );
    cy.get('#sumFlex0 input').should('contain.value', '97.45');
    cy.get('#nettoHours0 input').should('contain.value', '8.83');
    cy.get('#flexHours0 input').should('contain.value', '8.83');
    for (let i = 0; i < planHours.length; i++) {
      let id = `#planHours${i+1}`;
      cy.get(id).find('input').clear().type(planHours[i].hours.toString());
      let sumFlexId = `#sumFlex${i+1}`;
      cy.get(sumFlexId).find('input').should('contain.value', planHours[i].sumFlex.toString());
      let nettoHoursId = `#nettoHours${i+1}`;
      cy.get(nettoHoursId).find('input').should('contain.value', planHours[i].nettoHours.toString());
      let flexId = `#flexHours${i+1}`;
      cy.get(flexId).find('input').should('contain.value', planHours[i].flex.toString());
    }
    for (let i = 0; i < planTexts.length; i++) {
      let id = `#planText${i+1}`;
      cy.get(id).find('input').clear().type(planTexts[i].text);
    }
    cy.get('#workingHoursSave').click();
    cy.get('#sumFlex7 input').should('contain.value', '41.45');

    TimePlanningWorkingHoursPage.dateFormInput().click();
    selectDateRangeOnNewDatePicker(
      filtersNextWeek[0].dateRange.yearFrom, filtersNextWeek[0].dateRange.monthFrom,  filtersNextWeek[0].dateRange.dayFrom,
      filtersNextWeek[0].dateRange.yearTo, filtersNextWeek[0].dateRange.monthTo, filtersNextWeek[0].dateRange.dayTo
    );

    cy.get('#sumFlex0 input').should('contain.value', '41.45');
    cy.get('#nettoHours0 input').should('contain.value', '0');
    // cy.get('#flexHours0 input').should('contain.value', '-8');
    for (let i = 0; i < planHoursNextWeek.length; i++) {
      let id = `#planHours${i+1}`;
      cy.get(id).find('input').clear().type(planHoursNextWeek[i].hours.toString());
      let sumFlexId = `#sumFlex${i+1}`;
      cy.get(sumFlexId).find('input').should('contain.value', planHoursNextWeek[i].sumFlex.toString());
      let nettoHoursId = `#nettoHours${i+1}`;
      cy.get(nettoHoursId).find('input').should('contain.value', planHoursNextWeek[i].nettoHours.toString());
      let flexId = `#flexHours${i+1}`;
      cy.get(flexId).find('input').should('contain.value', planHoursNextWeek[i].flex.toString());
    }
    for (let i = 0; i < planTextsNextWeek.length; i++) {
      let id = `#planText${i+1}`;
      cy.get(id).find('input').clear().type(planTextsNextWeek[i].text);
    }
    cy.get('#workingHoursSave').click();
    cy.get('#sumFlex7 input').should('contain.value', '-14.55');

    cy.get('mat-toolbar > button .mat-mdc-button-persistent-ripple').parent().click();
    pluginPage.Navbar.goToPluginsPage();
    const pluginName = 'Microting Time Planning Plugin';
    // pluginPage.enablePluginByName(pluginName);
    let row = cy.contains('.mat-mdc-row', pluginName).first();
    row.find('.mat-column-actions button')
      .should('contain.text', 'toggle_on'); // plugin is enabled
    row = cy.contains('.mat-mdc-row', pluginName).first();
    row.find('.mat-column-actions a')
      .should('contain.text', 'settings'); // plugin is enabled
    row = cy.contains('.mat-mdc-row', pluginName).first();
    let settingsElement = row
      .find('.mat-column-actions a')
      // .should('be.enabled')
      .should('be.visible');
    settingsElement.click();
    cy.get('#forceLoadAllPlanningsFromGoogleSheet').click();
    cy.get('#saveSettings').click();
    cy.intercept('POST', '**/api/time-planning-pn/plannings/index').as('update');
    cy.get('mat-tree-node').contains('Dashboard').click();
    cy.wait('@update', { timeout: 60000 });
    cy.get('#backwards').click();
    cy.get('#plannedHours0').should('include.text', '56:00');

    cy.get('#forwards').click();
    cy.wait(1000);
    cy.get('#forwards').click();
    cy.wait(1000);
    // cy.get('#plannedHours0').should('include.text', '0:00');
    // cy.visit('http://localhost:4200');
    // TimePlanningWorkingHoursPage.Navbar.advancedBtn();
  });
});
