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

// 2 weeks in the future
const futureWeekBase = new Date(today);
futureWeekBase.setDate(today.getDate() + 14);
const futureWeekMonday = getMonday(futureWeekBase);
const futureWeekSunday = getSunday(futureWeekMonday);
const futureWeekDates = getWeekDates(futureWeekMonday);

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

const filtersFutureWeek = [
  {
    dateRange: {
      yearFrom: futureWeekMonday.getFullYear(),
      monthFrom: futureWeekMonday.getMonth() + 1,
      dayFrom: futureWeekMonday.getDate(),
      yearTo: futureWeekSunday.getFullYear(),
      monthTo: futureWeekSunday.getMonth() + 1,
      dayTo: futureWeekSunday.getDate(),
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

const updatePlanHours = [
  { date: lastWeekDates[0], hours: 1, sumFlex: 96.45, nettoHours: 0, flex: -1, humanFlex: '89:27'},
  { date: lastWeekDates[1], hours: 2, sumFlex: 94.45, nettoHours: 0, flex: -2, humanFlex: '81:27'},
  { date: lastWeekDates[2], hours: 3, sumFlex: 91.45, nettoHours: 0, flex: -3, humanFlex: '73:27'},
  { date: lastWeekDates[3], hours: 0, sumFlex: 91.45, nettoHours: 0, flex: 0, humanFlex: '65:27'},
  { date: lastWeekDates[4], hours: 4, sumFlex: 87.45, nettoHours: 0, flex: -4, humanFlex: '57:27'},
  { date: lastWeekDates[5], hours: 5, sumFlex: 82.45, nettoHours: 0, flex: -5, humanFlex: '49:27'},
  { date: lastWeekDates[6], hours: 6, sumFlex: 76.45, nettoHours: 0, flex: -6, humanFlex: '41:27'},
]

const planHoursNextWeek = [
  { date: nextWeekDates[0], hours: 8, sumFlex: 33.45, nettoHours: 0, flex: -8, humanFlex: '33:27'},
  { date: nextWeekDates[1], hours: 8, sumFlex: 25.45, nettoHours: 0, flex: -8, humanFlex: '25:27'},
  { date: nextWeekDates[2], hours: 8, sumFlex: 17.45, nettoHours: 0, flex: -8, humanFlex: '17:27'},
  { date: nextWeekDates[3], hours: 8, sumFlex: 9.45, nettoHours: 0, flex: -8, humanFlex: '9:27'},
  { date: nextWeekDates[4], hours: 8, sumFlex: 1.45, nettoHours: 0, flex: -8, humanFlex: '1:27'},
  { date: nextWeekDates[5], hours: 8, sumFlex: -6.55, nettoHours: 0, flex: -8, humanFlex: '-6:33'},
  { date: nextWeekDates[6], hours: 8, sumFlex: -14.55, nettoHours: 0, flex: -8, humanFlex: '-14:33'},
];

const updatePlanHoursNextWeek = [
  { date: nextWeekDates[0], hours: 8, sumFlex: 68.45, nettoHours: 0, flex: -8, humanFlex: '33:27'},
  { date: nextWeekDates[1], hours: 8, sumFlex: 60.45, nettoHours: 0, flex: -8, humanFlex: '25:27'},
  { date: nextWeekDates[2], hours: 0, sumFlex: 60.45, nettoHours: 0, flex: 0, humanFlex: '17:27'},
  { date: nextWeekDates[3], hours: 0, sumFlex: 60.45, nettoHours: 0, flex: 0, humanFlex: '9:27'},
  { date: nextWeekDates[4], hours: 0, sumFlex: 60.45, nettoHours: 0, flex: 0, humanFlex: '1:27'},
  { date: nextWeekDates[5], hours: 8, sumFlex: 52.45, nettoHours: 0, flex: -8, humanFlex: '-6:33'},
  { date: nextWeekDates[6], hours: 8, sumFlex: 44.45, nettoHours: 0, flex: -8, humanFlex: '-14:33'},
]

const planHoursFutureWeek = [
  { date: futureWeekDates[0], hours: 8, sumFlex: -22.55, nettoHours: 0, flex: -8, humanFlex: '-22:33'},
  { date: futureWeekDates[1], hours: 8, sumFlex: -30.55, nettoHours: 0, flex: -8, humanFlex: '-30:33'},
  { date: futureWeekDates[2], hours: 16, sumFlex: -46.55, nettoHours: 0, flex: -16, humanFlex: '-46:33'},
  { date: futureWeekDates[3], hours: 8, sumFlex: -54.55, nettoHours: 0, flex: -8, humanFlex: '-54:33'},
  { date: futureWeekDates[4], hours: 8, sumFlex: -62.55, nettoHours: 0, flex: -8, humanFlex: '-64:33'},
  { date: futureWeekDates[5], hours: 8, sumFlex: -70.55, nettoHours: 0, flex: -8, humanFlex: '-70:33'},
  { date: futureWeekDates[6], hours: 8, sumFlex: -78.55, nettoHours: 0, flex: -8, humanFlex:'-78.33'},
];

const updatePlanHoursFutureWeek = [
  { date: futureWeekDates[0], hours: 2, sumFlex: 42.45, nettoHours: 0, flex: -2, humanFlex: '-22:33'},
  { date: futureWeekDates[1], hours: 4, sumFlex: 38.45, nettoHours: 0, flex: -4, humanFlex: '-30:33'},
  { date: futureWeekDates[2], hours: 0, sumFlex: 38.45, nettoHours: 0, flex: 0, humanFlex: '-46:33'},
  { date: futureWeekDates[3], hours: 10, sumFlex: 28.45, nettoHours: 0, flex: -10, humanFlex: '-54:33'},
  { date: futureWeekDates[4], hours: 12, sumFlex: 16.45, nettoHours: 0, flex: -12, humanFlex: '-64:33'},
  { date: futureWeekDates[5], hours: 3, sumFlex: 13.45, nettoHours: 0, flex: -3, humanFlex: '-70:33'},
  { date: futureWeekDates[6], hours: 8, sumFlex: 5.45, nettoHours: 0, flex: -8, humanFlex:'-78.33'},
]

const planTexts = [
  { date: lastWeekDates[0], text: '07:30-15:30', plannedHours: '8:00', flexBalanceToDate: 'Flex saldo til dato: 89:27', calculatedHours: '8:00' },
  { date: lastWeekDates[1], text: '7:45-16:00/1', plannedHours: '8:00', flexBalanceToDate: 'Flex saldo til dato: 81:27', calculatedHours: '8:15' },
  { date: lastWeekDates[2], text: '7:15-16:00/1;17-20/0,5', plannedHours: '8:00', flexBalanceToDate: 'Flex saldo til dato: 73:27', calculatedHours: '9:45' },
  { date: lastWeekDates[3], text: '6-12/½;18:00-20:00/0.5', plannedHours: '8:00', flexBalanceToDate: 'Flex saldo til dato: 65:27', calculatedHours: '7:0' },
  { date: lastWeekDates[4], text: '06:00-12:00/¾;18:00-20:00/0.5', plannedHours: '8:00', flexBalanceToDate: 'Flex saldo til dato: 57:27', calculatedHours: '7:0' },
  { date: lastWeekDates[5], text: '6-12/¾;18-20/¾', plannedHours: '8:00', flexBalanceToDate: 'Flex saldo til dato: 49:27', calculatedHours: '7:0' },
  { date: lastWeekDates[6], text: '6-14/½', plannedHours: '8:00', flexBalanceToDate: 'Flex saldo til dato: 41:27', calculatedHours: '7:30' },
];

const updatePlanTexts = [
  { date: lastWeekDates[0], text: '07:30-15:30', plannedHours: '8:00', flexBalanceToDate: 'Flex saldo til dato: 89:27', calculatedHours: '8:00' },
  { date: lastWeekDates[1], text: '7:45-16:00/1', plannedHours: '8:00', flexBalanceToDate: 'Flex saldo til dato: 81:27', calculatedHours: '8:15' },
  { date: lastWeekDates[2], text: '7:15-16:00/1;17-20/0,5', plannedHours: '8:00', flexBalanceToDate: 'Flex saldo til dato: 73:27', calculatedHours: '9:45' },
  { date: lastWeekDates[3], text: '6-12/½;18:00-20:00/0.5', plannedHours: '8:00', flexBalanceToDate: 'Flex saldo til dato: 65:27', calculatedHours: '7:0' },
  { date: lastWeekDates[4], text: '06:00-12:00/¾;18:00-20:00/0.5', plannedHours: '8:00', flexBalanceToDate: 'Flex saldo til dato: 57:27', calculatedHours: '7:0' },
  { date: lastWeekDates[5], text: '6-12/¾;18-20/¾', plannedHours: '8:00', flexBalanceToDate: 'Flex saldo til dato: 49:27', calculatedHours: '7:0' },
  { date: lastWeekDates[6], text: '6-14/½', plannedHours: '8:00', flexBalanceToDate: 'Flex saldo til dato: 41:27', calculatedHours: '7:30' },
];

const planTextsNextWeek = [
  { date: nextWeekDates[0], text: '07:30-15:30', firstShift: '07:30 - 15:30 / 00:00', plannedHours: '8:00', flexBalanceToDate: 'Flex saldo til dato: 33:27', calculatedHours: '8:00' },
  { date: nextWeekDates[1], text: '7:45-16:00/1', firstShift: '07:45 - 16:00 / 01:00', plannedHours: '8:00', flexBalanceToDate: 'Flex saldo til dato: 25:27', calculatedHours: '8:15' },
  { date: nextWeekDates[2], text: '7:15-16:00/1;17-20/0,5', firstShift: '07:15 - 16:00 / 01:00', secondShift: '17:00 - 20:00 / 00:30', plannedHours: '8:00', flexBalanceToDate: 'Flex saldo til dato: 17:27', calculatedHours: '9:45' },
  { date: nextWeekDates[3], text: '6-12/½;18:00-20:00/0.5', firstShift: '06:00 - 12:00 / 00:30', secondShift: '18:00 - 20:00 / 00:30', plannedHours: '8:00', flexBalanceToDate: 'Flex saldo til dato: 9:27', calculatedHours: '7:0' },
  { date: nextWeekDates[4], text: '06:00-12:00/¾;18:00-20:00/0.5', firstShift: '06:00 - 12:00 / 00:45', secondShift: '18:00 - 20:00 / 00:30', plannedHours: '8:00', flexBalanceToDate: 'Flex saldo til dato: 1:27', calculatedHours: '7:0' },
  { date: nextWeekDates[5], text: '6-12/¾;18-20/¾', firstShift: '06:00 - 12:00 / 00:45', secondShift: '18:00 - 20:00 / 00:45', plannedHours: '8:00', flexBalanceToDate: 'Flex saldo til dato: -6:33', calculatedHours: '7:0' },
  { date: lastWeekDates[6], text: '6-14/½', firstShift: '06:00 - 14:00 / 00:30', plannedHours: '8:00', flexBalanceToDate: 'Flex saldo til dato: -14:33', calculatedHours: '7:30' },
];

const updatePlanTextsNextWeek = [
  { date: nextWeekDates[0], text: '07:30-15:30', firstShift: '07:30 - 15:30 / 00:00', plannedHours: '8:00', flexBalanceToDate: 'Flex saldo til dato: 33:27', calculatedHours: '8:00' },
  { date: nextWeekDates[1], text: '7:45-16:00/1', firstShift: '07:45 - 16:00 / 01:00', plannedHours: '8:00', flexBalanceToDate: 'Flex saldo til dato: 25:27', calculatedHours: '8:15' },
  { date: nextWeekDates[2], text: '7:15-16:00/1;17-20/0,5', firstShift: '07:15 - 16:00 / 01:00', secondShift: '17:00 - 20:00 / 00:30', plannedHours: '8:00', flexBalanceToDate: 'Flex saldo til dato: 17:27', calculatedHours: '9:45' },
  { date: nextWeekDates[3], text: '6-12/½;18:00-20:00/0.5', firstShift: '06:00 - 12:00 / 00:30', secondShift: '18:00 - 20:00 / 00:30', plannedHours: '8:00', flexBalanceToDate: 'Flex saldo til dato: 9:27', calculatedHours: '7:0' },
  { date: nextWeekDates[4], text: '06:00-12:00/¾;18:00-20:00/0.5', firstShift: '06:00 - 12:00 / 00:45', secondShift: '18:00 - 20:00 / 00:30', plannedHours: '8:00', flexBalanceToDate: 'Flex saldo til dato: 1:27', calculatedHours: '7:0' },
  { date: nextWeekDates[5], text: '6-12/¾;18-20/¾', firstShift: '06:00 - 12:00 / 00:45', secondShift: '18:00 - 20:00 / 00:45', plannedHours: '8:00', flexBalanceToDate: 'Flex saldo til dato: -6:33', calculatedHours: '7:0' },
  { date: lastWeekDates[6], text: '6-14/½', firstShift: '06:00 - 14:00 / 00:30', plannedHours: '8:00', flexBalanceToDate: 'Flex saldo til dato: -14:33', calculatedHours: '7:30' },
];

const planTextsFutureWeek = [
  { date: futureWeekDates[0], text: '07:30-15:30;foobar', firstShift: '07:30 - 15:30 / 00:00', plannedHours: '8:00', flexBalanceToDate: 'Flex saldo til dato: -22:33', calculatedHours: '8:00' },
  { date: futureWeekDates[1], text: '7:45-16/0.75', firstShift: '07:45 - 16:00 / 00:45', plannedHours: '8:00', flexBalanceToDate: 'Flex saldo til dato: -30:33', calculatedHours: '8:15' },
  { date: futureWeekDates[2], text: 'foo bar', plannedHours: '16:00', flexBalanceToDate: 'Flex saldo til dato: -46:33', calculatedHours: '16:00' },
  { date: futureWeekDates[3], text: '6-12;18:00-20:00', firstShift: '06:00 - 12:00 / 00:00', secondShift: '18:00 - 20:00 / 00:00', plannedHours: '8:00', flexBalanceToDate: 'Flex saldo til dato: -54:33', calculatedHours: '8:00' },
  { date: futureWeekDates[4], text: ' ', plannedHours: '8:00', flexBalanceToDate: 'Flex saldo til dato: -62:33', calculatedHours: '8:00'},
  { date: futureWeekDates[5], text: '6-12/¾;18-20/¾', firstShift: '06:00 - 12:00 / 00:45', secondShift: '18:00 - 20:00 / 00:45', plannedHours: '8:00', flexBalanceToDate: 'Flex saldo til dato: -70:33', calculatedHours: '8:00' },
  { date: futureWeekDates[6], text: '6-14/½', firstShift: '06:00 - 14:00 / 00:30', plannedHours: '8:00', flexBalanceToDate: 'Flex saldo til dato: -78:33', calculatedHours: '8:00' },
];

const updatePlanTextsFutureWeek = [
  { date: futureWeekDates[0], text: '07:30-15:30;foobar', firstShift: '07:30 - 15:30 / 00:00', plannedHours: '8:00', flexBalanceToDate: 'Flex saldo til dato: -22:33', calculatedHours: '8:00' },
  { date: futureWeekDates[1], text: '7:45-16/0.75', firstShift: '07:45 - 16:00 / 00:45', plannedHours: '8:00', flexBalanceToDate: 'Flex saldo til dato: -30:33', calculatedHours: '8:15' },
  { date: futureWeekDates[2], text: 'foo bar', plannedHours: '16:00', flexBalanceToDate: 'Flex saldo til dato: -46:33', calculatedHours: '16:00' },
  { date: futureWeekDates[3], text: '6-12;18:00-20:00', firstShift: '06:00 - 12:00 / 00:00', secondShift: '18:00 - 20:00 / 00:00', plannedHours: '8:00', flexBalanceToDate: 'Flex saldo til dato: -54:33', calculatedHours: '8:00' },
  { date: futureWeekDates[4], text: ' ', plannedHours: '8:00', flexBalanceToDate: 'Flex saldo til dato: -62:33', calculatedHours: '8:00'},
  { date: futureWeekDates[5], text: '6-12/¾;18-20/¾', firstShift: '06:00 - 12:00 / 00:45', secondShift: '18:00 - 20:00 / 00:45', plannedHours: '8:00', flexBalanceToDate: 'Flex saldo til dato: -70:33', calculatedHours: '8:00' },
  { date: futureWeekDates[6], text: '6-14/½', firstShift: '06:00 - 14:00 / 00:30', plannedHours: '8:00', flexBalanceToDate: 'Flex saldo til dato: -78:33', calculatedHours: '8:00' },
];


describe('Enable Backend Config plugin', () => {
  beforeEach(() => {
    cy.visit('http://localhost:4200');
    loginPage.login();
  });

  it('should go to dashboard', () => {
    // we have more than one mat-nested-tree-node so we beed to select the own with the text "Timeregistrering"
    cy.get('mat-nested-tree-node').contains('Time Planning').click();
    cy.get('mat-tree-node').contains('Working hours').click();
    cy.get('mat-toolbar > button .mat-mdc-button-persistent-ripple').parent().click();
    cy.get('#workingHoursSite').clear().type('c d');
    cy.get('.ng-option.ng-option-marked').click();
    TimePlanningWorkingHoursPage.dateFormInput().click();
    selectDateRangeOnNewDatePicker(
      filters[0].dateRange.yearFrom, filters[0].dateRange.monthFrom,  filters[0].dateRange.dayFrom,
      filters[0].dateRange.yearTo, filters[0].dateRange.monthTo, filters[0].dateRange.dayTo
    );
    cy.get('#sumFlex0 input').should('contain.value', '97.45');
    // cy.get('#nettoHours0 input').should('contain.value', '8.83');
    // cy.get('#flexHours0 input').should('contain.value', '8.83');
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
  });

  it('should go to dashboard and set to use google sheet as default and check if the settings are correct', () => {
    cy.get('mat-nested-tree-node').contains('Time Planning').click();
    cy.intercept('POST', '**/api/time-planning-pn/plannings/index').as('update');
    cy.get('mat-tree-node').contains('Dashboard').click();
    cy.wait('@update', { timeout: 60000 });
    cy.get('#firstColumn0').click();
    cy.get('#useGoogleSheetAsDefault').click();
    cy.get('#saveButton').click();
      // cy.get('mat-nested-tree-node').contains('Time Planning').click();
      cy.get('mat-tree-node').contains('Working hours').click();
      cy.get('mat-toolbar > button .mat-mdc-button-persistent-ripple').parent().click();
      cy.get('#workingHoursSite').clear().type('c d');
      cy.get('.ng-option.ng-option-marked').click();
      TimePlanningWorkingHoursPage.dateFormInput().click();
      selectDateRangeOnNewDatePicker(
        filters[0].dateRange.yearFrom, filters[0].dateRange.monthFrom,  filters[0].dateRange.dayFrom,
        filters[0].dateRange.yearTo, filters[0].dateRange.monthTo, filters[0].dateRange.dayTo
      );
      cy.get('#sumFlex0 input').should('contain.value', '97.45');
      // cy.get('#nettoHours0 input').should('contain.value', '8.83');
      // cy.get('#flexHours0 input').should('contain.value', '8.83');
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

    TimePlanningWorkingHoursPage.dateFormInput().click();
    selectDateRangeOnNewDatePicker(
      filtersFutureWeek[0].dateRange.yearFrom, filtersFutureWeek[0].dateRange.monthFrom,  filtersFutureWeek[0].dateRange.dayFrom,
      filtersFutureWeek[0].dateRange.yearTo, filtersFutureWeek[0].dateRange.monthTo, filtersFutureWeek[0].dateRange.dayTo
    );

    // cy.get('#sumFlex0 input').should('contain.value', '41.45');
    cy.get('#nettoHours0 input').should('contain.value', '0');
    // cy.get('#flexHours0 input').should('contain.value', '-8');
    for (let i = 0; i < planHoursFutureWeek.length; i++) {
      let id = `#planHours${i+1}`;
      cy.get(id).find('input').clear().type(planHoursFutureWeek[i].hours.toString());
      let sumFlexId = `#sumFlex${i+1}`;
      cy.get(sumFlexId).find('input').should('contain.value', planHoursFutureWeek[i].sumFlex.toString());
      let nettoHoursId = `#nettoHours${i+1}`;
      cy.get(nettoHoursId).find('input').should('contain.value', planHoursFutureWeek[i].nettoHours.toString());
      let flexId = `#flexHours${i+1}`;
      cy.get(flexId).find('input').should('contain.value', planHoursFutureWeek[i].flex.toString());
    }
    for (let i = 0; i < planTextsFutureWeek.length; i++) {
      let id = `#planText${i+1}`;
      cy.get(id).find('input').clear().type(planTextsFutureWeek[i].text);
    }
    cy.get('#workingHoursSave').click();
    cy.get('#sumFlex7 input').should('contain.value', '-78.55');

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
    for (let i = 0; i < planTexts.length; i++) {
      let plannedHoursId = `#plannedHours0_${i}`;
      cy.get(plannedHoursId).should('include.text', planTexts[i].plannedHours);
      // cy.get(id).find('input').should('contain.value', planHoursNextWeek[i].hours.toString());
      let flexBalanceToDateId = `#flexBalanceToDate0_${i}`;
      cy.get(flexBalanceToDateId).should('include.text', planTexts[i].flexBalanceToDate);
      // cy.get(sumFlexId).find('input').should('contain.value', planHoursNextWeek[i].sumFlex.toString());
      // let nettoHoursId = `#nettoHours${i+1}`;
      // cy.get(nettoHoursId).find('input').should('contain.value', planHoursNextWeek[i].nettoHours.toString());
      // let flexId = `#flexHours${i+1}`;
      // cy.get(flexId).find('input').should('contain.value', planHoursNextWeek[i].flex.toString());
    }

    cy.get('#forwards').click();
    cy.wait(1000);
    cy.get('#forwards').click();
    cy.wait(1000);
    for (let i = 0; i < planTextsNextWeek.length; i++) {
      let firstShiftId = `#firstShift0_${i}`;
      cy.get(firstShiftId).should('include.text', planTextsNextWeek[i].firstShift);
      if (planTextsNextWeek[i].secondShift) {
        let secondShiftId = `#secondShift0_${i}`;
        cy.get(secondShiftId).should('include.text', planTextsNextWeek[i].secondShift);
      }
    }
    cy.get('#forwards').click();
    cy.wait(1000);
    for (let i = 0; i < planTextsFutureWeek.length; i++) {
      if (planTextsFutureWeek[i].firstShift) {
        let firstShiftId = `#firstShift0_${i}`;
        cy.get(firstShiftId).should('include.text', planTextsFutureWeek[i].firstShift);
      } else {
        let plannedHoursId = `#plannedHours0_${i}`;
        cy.get(plannedHoursId).should('include.text', planTextsFutureWeek[i].plannedHours);
      }
      if (planTextsFutureWeek[i].secondShift) {
        let secondShiftId = `#secondShift0_${i}`;
        cy.get(secondShiftId).should('include.text', planTextsFutureWeek[i].secondShift);
      }
    }
  });

  it('should go to dashboard and after updating planText to new values and they should change in dashboard', () => {
    cy.get('mat-nested-tree-node').contains('Time Planning').click();
    cy.get('mat-tree-node').contains('Working hours').click();
    cy.get('mat-toolbar > button .mat-mdc-button-persistent-ripple').parent().click();
    cy.get('#workingHoursSite').clear().type('c d');
    cy.get('.ng-option.ng-option-marked').click();
    TimePlanningWorkingHoursPage.dateFormInput().click();
    selectDateRangeOnNewDatePicker(
      filters[0].dateRange.yearFrom, filters[0].dateRange.monthFrom,  filters[0].dateRange.dayFrom,
      filters[0].dateRange.yearTo, filters[0].dateRange.monthTo, filters[0].dateRange.dayTo
    );
    cy.get('#sumFlex0 input').should('contain.value', '97.45');
    // cy.get('#nettoHours0 input').should('contain.value', '8.83');
    // cy.get('#flexHours0 input').should('contain.value', '8.83');
    for (let i = 0; i < updatePlanHours.length; i++) {
      let id = `#planHours${i+1}`;
      cy.get(id).find('input').clear().type(updatePlanHours[i].hours.toString());
      let sumFlexId = `#sumFlex${i+1}`;
      cy.get(sumFlexId).find('input').should('contain.value', updatePlanHours[i].sumFlex.toString());
      let nettoHoursId = `#nettoHours${i+1}`;
      cy.get(nettoHoursId).find('input').should('contain.value', updatePlanHours[i].nettoHours.toString());
      let flexId = `#flexHours${i+1}`;
      cy.get(flexId).find('input').should('contain.value', updatePlanHours[i].flex.toString());
    }
    for (let i = 0; i < updatePlanTexts.length; i++) {
      let id = `#planText${i+1}`;
      cy.get(id).find('input').clear().type(updatePlanTexts[i].text);
    }
    cy.get('#workingHoursSave').click();
    cy.get('#sumFlex7 input').should('contain.value', '76.45');

    TimePlanningWorkingHoursPage.dateFormInput().click();
    selectDateRangeOnNewDatePicker(
      filtersNextWeek[0].dateRange.yearFrom, filtersNextWeek[0].dateRange.monthFrom,  filtersNextWeek[0].dateRange.dayFrom,
      filtersNextWeek[0].dateRange.yearTo, filtersNextWeek[0].dateRange.monthTo, filtersNextWeek[0].dateRange.dayTo
    );

    cy.get('#sumFlex0 input').should('contain.value', '76.45');
    cy.get('#nettoHours0 input').should('contain.value', '0');
    // cy.get('#flexHours0 input').should('contain.value', '-8');
    for (let i = 0; i < updatePlanHoursNextWeek.length; i++) {
      let id = `#planHours${i+1}`;
      cy.get(id).find('input').clear().type(updatePlanHoursNextWeek[i].hours.toString());
      let sumFlexId = `#sumFlex${i+1}`;
      cy.get(sumFlexId).find('input').should('contain.value', updatePlanHoursNextWeek[i].sumFlex.toString());
      let nettoHoursId = `#nettoHours${i+1}`;
      cy.get(nettoHoursId).find('input').should('contain.value', updatePlanHoursNextWeek[i].nettoHours.toString());
      let flexId = `#flexHours${i+1}`;
      cy.get(flexId).find('input').should('contain.value', updatePlanHoursNextWeek[i].flex.toString());
    }
    for (let i = 0; i < updatePlanTextsNextWeek.length; i++) {
      let id = `#planText${i+1}`;
      cy.get(id).find('input').clear().type(updatePlanTextsNextWeek[i].text);
    }
    cy.get('#workingHoursSave').click();
    cy.get('#sumFlex7 input').should('contain.value', '44.45');

    TimePlanningWorkingHoursPage.dateFormInput().click();
    selectDateRangeOnNewDatePicker(
      filtersFutureWeek[0].dateRange.yearFrom, filtersFutureWeek[0].dateRange.monthFrom,  filtersFutureWeek[0].dateRange.dayFrom,
      filtersFutureWeek[0].dateRange.yearTo, filtersFutureWeek[0].dateRange.monthTo, filtersFutureWeek[0].dateRange.dayTo
    );

    // cy.get('#sumFlex0 input').should('contain.value', '41.45');
    cy.get('#nettoHours0 input').should('contain.value', '0');
    // cy.get('#flexHours0 input').should('contain.value', '-8');
    for (let i = 0; i < updatePlanHoursFutureWeek.length; i++) {
      let id = `#planHours${i+1}`;
      cy.get(id).find('input').clear().type(updatePlanHoursFutureWeek[i].hours.toString());
      let sumFlexId = `#sumFlex${i+1}`;
      cy.get(sumFlexId).find('input').should('contain.value', updatePlanHoursFutureWeek[i].sumFlex.toString());
      let nettoHoursId = `#nettoHours${i+1}`;
      cy.get(nettoHoursId).find('input').should('contain.value', updatePlanHoursFutureWeek[i].nettoHours.toString());
      let flexId = `#flexHours${i+1}`;
      cy.get(flexId).find('input').should('contain.value', updatePlanHoursFutureWeek[i].flex.toString());
    }
    for (let i = 0; i < updatePlanTextsFutureWeek.length; i++) {
      let id = `#planText${i+1}`;
      cy.get(id).find('input').clear().type(updatePlanTextsFutureWeek[i].text);
    }
    cy.get('#workingHoursSave').click();
    cy.get('#sumFlex7 input').should('contain.value', '5.45');

    cy.get('mat-toolbar > button .mat-mdc-button-persistent-ripple').parent().click();
  });
});
