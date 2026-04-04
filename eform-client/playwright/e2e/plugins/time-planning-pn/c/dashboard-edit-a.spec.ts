import { test, expect } from '@playwright/test';
import { LoginPage } from '../../../Page objects/Login.page';
import { PluginPage } from '../../../Page objects/Plugin.page';
import { TimePlanningWorkingHoursPage } from '../TimePlanningWorkingHours.page';
import { selectDateRangeOnNewDatePicker } from '../../../helper-functions';



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
  { date: lastWeekDates[0], hours: 1, sumFlex: 96.45, nettoHours: 0, flex: -1, humanFlex: '96:27'},
  { date: lastWeekDates[1], hours: 2, sumFlex: 94.45, nettoHours: 0, flex: -2, humanFlex: '94:27'},
  { date: lastWeekDates[2], hours: 3, sumFlex: 91.45, nettoHours: 0, flex: -3, humanFlex: '91:27'},
  { date: lastWeekDates[3], hours: 0, sumFlex: 91.45, nettoHours: 0, flex: 0, humanFlex: '91:27'},
  { date: lastWeekDates[4], hours: 4, sumFlex: 87.45, nettoHours: 0, flex: -4, humanFlex: '87:27'},
  { date: lastWeekDates[5], hours: 5, sumFlex: 82.45, nettoHours: 0, flex: -5, humanFlex: '82:27'},
  { date: lastWeekDates[6], hours: 6, sumFlex: 76.45, nettoHours: 0, flex: -6, humanFlex: '76:27'},
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

const updatePlanHoursNextWeek = [
  { date: nextWeekDates[0], hours: 8, sumFlex: 68.45, nettoHours: 0, flex: -8, humanFlex: '68:27'},
  { date: nextWeekDates[1], hours: 8, sumFlex: 60.45, nettoHours: 0, flex: -8, humanFlex: '60:27'},
  { date: nextWeekDates[2], hours: 0, sumFlex: 60.45, nettoHours: 0, flex: 0, humanFlex: '60:27'},
  { date: nextWeekDates[3], hours: 0, sumFlex: 60.45, nettoHours: 0, flex: 0, humanFlex: '60:27'},
  { date: nextWeekDates[4], hours: 0, sumFlex: 60.45, nettoHours: 0, flex: 0, humanFlex: '60:27'},
  { date: nextWeekDates[5], hours: 8, sumFlex: 52.45, nettoHours: 0, flex: -8, humanFlex: '52:27'},
  { date: nextWeekDates[6], hours: 8, sumFlex: 44.45, nettoHours: 0, flex: -8, humanFlex: '44:27'},
];

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
  { date: futureWeekDates[0], hours: 2, sumFlex: 42.45, nettoHours: 0, flex: -2, humanFlex: '42:27'},
  { date: futureWeekDates[1], hours: 4, sumFlex: 38.45, nettoHours: 0, flex: -4, humanFlex: '38:27'},
  { date: futureWeekDates[2], hours: 0, sumFlex: 38.45, nettoHours: 0, flex: 0, humanFlex: '38:27'},
  { date: futureWeekDates[3], hours: 10, sumFlex: 28.45, nettoHours: 0, flex: -10, humanFlex: '28:27'},
  { date: futureWeekDates[4], hours: 12, sumFlex: 16.45, nettoHours: 0, flex: -12, humanFlex: '16:27'},
  { date: futureWeekDates[5], hours: 3, sumFlex: 13.45, nettoHours: 0, flex: -3, humanFlex: '13:27'},
  { date: futureWeekDates[6], hours: 8, sumFlex: 5.45, nettoHours: 0, flex: -8, humanFlex:' 5:27'},
];

const planTexts = [
  { date: lastWeekDates[0], text: '07:30-15:30', plannedHours: '8:00', flexBalanceToDate: 'Flex saldo til dato: 89:27', calculatedHours: '8', plannedStartOfShift1: '00:00', plannedEndOfShift1: '00:00', plannedBreakOfShift1: '00:00', plannedStartOfShift2: '', plannedEndOfShift2: '', plannedBreakOfShift2: '' },
  { date: lastWeekDates[1], text: '7:45-16:00/1', plannedHours: '8:00', flexBalanceToDate: 'Flex saldo til dato: 81:27', calculatedHours: '8', plannedStartOfShift1: '00:00', plannedEndOfShift1: '00:00', plannedBreakOfShift1: '00:00', plannedStartOfShift2: '', plannedEndOfShift2: '', plannedBreakOfShift2: '' },
  { date: lastWeekDates[2], text: '7:15-16:00/1;17-20/0,5', plannedHours: '8:00', flexBalanceToDate: 'Flex saldo til dato: 73:27', calculatedHours: '8', plannedStartOfShift1: '00:00', plannedEndOfShift1: '00:00', plannedBreakOfShift1: '00:00', plannedStartOfShift2: '00:00', plannedEndOfShift2: '00:00', plannedBreakOfShift2: '00:00' },
  { date: lastWeekDates[3], text: '6-12/½;18:00-20:00/0.5', plannedHours: '8:00', flexBalanceToDate: 'Flex saldo til dato: 65:27', calculatedHours: '8', plannedStartOfShift1: '00:00', plannedEndOfShift1: '00:00', plannedBreakOfShift1: '00:00', plannedStartOfShift2: '00:00', plannedEndOfShift2: '00:00', plannedBreakOfShift2: '00:00' },
  { date: lastWeekDates[4], text: '06:00-12:00/¾;18:00-20:00/0.5', plannedHours: '8:00', flexBalanceToDate: 'Flex saldo til dato: 57:27', calculatedHours: '8', plannedStartOfShift1: '00:00', plannedEndOfShift1: '00:00', plannedBreakOfShift1: '00:00', plannedStartOfShift2: '00:00', plannedEndOfShift2: '00:00', plannedBreakOfShift2: '00:00' },
  { date: lastWeekDates[5], text: '6-12/¾;18-20/¾', plannedHours: '8:00', flexBalanceToDate: 'Flex saldo til dato: 49:27', calculatedHours: '8', plannedStartOfShift1: '00:00', plannedEndOfShift1: '00:00', plannedBreakOfShift1: '00:00', plannedStartOfShift2: '00:00', plannedEndOfShift2: '00:00', plannedBreakOfShift2: '00:00' },
  { date: lastWeekDates[6], text: '6-14/½', plannedHours: '8:00', flexBalanceToDate: 'Flex saldo til dato: 41:27', calculatedHours: '8', plannedStartOfShift1: '00:00', plannedEndOfShift1: '00:00', plannedBreakOfShift1: '00:00', plannedStartOfShift2: '', plannedEndOfShift2: '', plannedBreakOfShift2: '' },
];

const updatePlanTexts = [
  { date: lastWeekDates[0], text: '07:30-15:30', plannedHours: '1:00', flexBalanceToDate: 'Flex saldo til dato: 96:27', calculatedHours: '1', plannedStartOfShift1: '00:00', plannedEndOfShift1: '00:00', plannedBreakOfShift1: '00:00', plannedStartOfShift2: '', plannedEndOfShift2: '', plannedBreakOfShift2: '' },
  { date: lastWeekDates[1], text: '7:45-16:00/1', plannedHours: '2:00', flexBalanceToDate: 'Flex saldo til dato: 94:27', calculatedHours: '2', plannedStartOfShift1: '00:00', plannedEndOfShift1: '00:00', plannedBreakOfShift1: '00:00', plannedStartOfShift2: '', plannedEndOfShift2: '', plannedBreakOfShift2: '' },
  { date: lastWeekDates[2], text: '7:15-16:00/1;17-20/0,5', plannedHours: '3:00', flexBalanceToDate: 'Flex saldo til dato: 91:27', calculatedHours: '3', plannedStartOfShift1: '00:00', plannedEndOfShift1: '00:00', plannedBreakOfShift1: '00:00', plannedStartOfShift2: '00:00', plannedEndOfShift2: '00:00', plannedBreakOfShift2: '00:00' },
  { date: lastWeekDates[3], text: '6-12/½;18:00-20:00/0.5', plannedHours: '', flexBalanceToDate: '', calculatedHours: '0', plannedStartOfShift1: '00:00', plannedEndOfShift1: '00:00', plannedBreakOfShift1: '00:00', plannedStartOfShift2: '00:00', plannedEndOfShift2: '00:00', plannedBreakOfShift2: '00:00' },
  { date: lastWeekDates[4], text: '06:00-12:00/¾;18:00-20:00/0.5', plannedHours: '4:00', flexBalanceToDate: 'Flex saldo til dato: 87:27', calculatedHours: '4', plannedStartOfShift1: '00:00', plannedEndOfShift1: '00:00', plannedBreakOfShift1: '00:00', plannedStartOfShift2: '00:00', plannedEndOfShift2: '00:00', plannedBreakOfShift2: '00:00' },
  { date: lastWeekDates[5], text: '6-12/¾;18-20/¾', plannedHours: '5:00', flexBalanceToDate: 'Flex saldo til dato: 82:27', calculatedHours: '5', plannedStartOfShift1: '00:00', plannedEndOfShift1: '00:00', plannedBreakOfShift1: '00:00', plannedStartOfShift2: '00:00', plannedEndOfShift2: '00:00', plannedBreakOfShift2: '00:00' },
  { date: lastWeekDates[6], text: '6-14/½', plannedHours: '6:00', flexBalanceToDate: 'Flex saldo til dato: 76:27', calculatedHours: '6', plannedStartOfShift1: '00:00', plannedEndOfShift1: '00:00', plannedBreakOfShift1: '00:00', plannedStartOfShift2: '', plannedEndOfShift2: '', plannedBreakOfShift2: '' },
];

const planTextsNextWeek = [
  { date: nextWeekDates[0], text: '07:30-15:30', firstShift: '07:30 - 15:30 / 00:00', plannedHours: '8:00', flexBalanceToDate: 'Flex saldo til dato: 33:27', calculatedHours: '8', plannedStartOfShift1: '07:30', plannedEndOfShift1: '15:30', plannedBreakOfShift1: '00:00', plannedStartOfShift2: '', plannedEndOfShift2: '', plannedBreakOfShift2: '' },
  { date: nextWeekDates[1], text: '7:45-16:00/1', firstShift: '07:45 - 16:00 / 01:00', plannedHours: '8:00', flexBalanceToDate: 'Flex saldo til dato: 25:27', calculatedHours: '7.25', plannedStartOfShift1: '07:45', plannedEndOfShift1: '16:00', plannedBreakOfShift1: '01:00', plannedStartOfShift2: '', plannedEndOfShift2: '', plannedBreakOfShift2: '' },
  { date: nextWeekDates[2], text: '7:15-16:00/1;17-20/0,5', firstShift: '07:15 - 16:00 / 01:00', secondShift: '17:00 - 20:00 / 00:30', plannedHours: '8:00', flexBalanceToDate: 'Flex saldo til dato: 17:27', calculatedHours: '10.25', plannedStartOfShift1: '07:15', plannedEndOfShift1: '16:00', plannedBreakOfShift1: '01:00', plannedStartOfShift2: '17:00', plannedEndOfShift2: '20:00', plannedBreakOfShift2: '00:30' },
  { date: nextWeekDates[3], text: '6-12/½;18:00-20:00/0.5', firstShift: '06:00 - 12:00 / 00:30', secondShift: '18:00 - 20:00 / 00:30', plannedHours: '8:00', flexBalanceToDate: 'Flex saldo til dato: 9:27', calculatedHours: '7', plannedStartOfShift1: '06:00', plannedEndOfShift1: '12:00', plannedBreakOfShift1: '00:30', plannedStartOfShift2: '18:00', plannedEndOfShift2: '20:00', plannedBreakOfShift2: '00:30' },
  { date: nextWeekDates[4], text: '06:00-12:00/¾;18:00-20:00/0.5', firstShift: '06:00 - 12:00 / 00:45', secondShift: '18:00 - 20:00 / 00:30', plannedHours: '8:00', flexBalanceToDate: 'Flex saldo til dato: 1:27', calculatedHours: '7', plannedStartOfShift1: '06:00', plannedEndOfShift1: '12:00', plannedBreakOfShift1: '00:45', plannedStartOfShift2: '18:00', plannedEndOfShift2: '20:00', plannedBreakOfShift2: '00:30' },
  { date: nextWeekDates[5], text: '6-12/¾;18-20/¾', firstShift: '06:00 - 12:00 / 00:45', secondShift: '18:00 - 20:00 / 00:45', plannedHours: '8:00', flexBalanceToDate: 'Flex saldo til dato: -6:33', calculatedHours: '6.5', plannedStartOfShift1: '06:00', plannedEndOfShift1: '12:00', plannedBreakOfShift1: '00:45', plannedStartOfShift2: '18:00', plannedEndOfShift2: '20:00', plannedBreakOfShift2: '00:45' },
  { date: lastWeekDates[6], text: '6-14/½', firstShift: '06:00 - 14:00 / 00:30', plannedHours: '8:00', flexBalanceToDate: 'Flex saldo til dato: -14:33', calculatedHours: '7.5', plannedStartOfShift1: '06:00', plannedEndOfShift1: '14:00', plannedBreakOfShift1: '00:30', plannedStartOfShift2: '', plannedEndOfShift2: '', plannedBreakOfShift2: '' },
];

const updatePlanTextsNextWeek = [
  { date: nextWeekDates[0], text: '07:30-15:30', firstShift: '07:30 - 15:30 / 00:00', plannedHours: '8:00', flexBalanceToDate: 'Flex saldo til dato: 33:27', calculatedHours: '8', plannedStartOfShift1: '07:30', plannedEndOfShift1: '15:30', plannedBreakOfShift1: '00:00', plannedStartOfShift2: '', plannedEndOfShift2: '', plannedBreakOfShift2: '' },
  { date: nextWeekDates[1], text: '7:45-16:00/1', firstShift: '07:45 - 16:00 / 01:00', plannedHours: '8:00', flexBalanceToDate: 'Flex saldo til dato: 25:27', calculatedHours: '7.25', plannedStartOfShift1: '07:45', plannedEndOfShift1: '16:00', plannedBreakOfShift1: '01:00', plannedStartOfShift2: '', plannedEndOfShift2: '', plannedBreakOfShift2: '' },
  { date: nextWeekDates[2], text: '7:15-16:00/1;17-20/0,5', firstShift: '07:15 - 16:00 / 01:00', secondShift: '17:00 - 20:00 / 00:30', plannedHours: '8:00', flexBalanceToDate: 'Flex saldo til dato: 17:27', calculatedHours: '10.25', plannedStartOfShift1: '07:15', plannedEndOfShift1: '16:00', plannedBreakOfShift1: '01:00', plannedStartOfShift2: '17:00', plannedEndOfShift2: '20:00', plannedBreakOfShift2: '00:30' },
  { date: nextWeekDates[3], text: '6-12/½;18:00-20:00/0.5', firstShift: '06:00 - 12:00 / 00:30', secondShift: '18:00 - 20:00 / 00:30', plannedHours: '8:00', flexBalanceToDate: 'Flex saldo til dato: 9:27', calculatedHours: '7', plannedStartOfShift1: '06:00', plannedEndOfShift1: '12:00', plannedBreakOfShift1: '00:30', plannedStartOfShift2: '18:00', plannedEndOfShift2: '20:00', plannedBreakOfShift2: '00:30' },
  { date: nextWeekDates[4], text: '06:00-12:00/¾;18:00-20:00/0.5', firstShift: '06:00 - 12:00 / 00:45', secondShift: '18:00 - 20:00 / 00:30', plannedHours: '8:00', flexBalanceToDate: 'Flex saldo til dato: 1:27', calculatedHours: '7', plannedStartOfShift1: '06:00', plannedEndOfShift1: '12:00', plannedBreakOfShift1: '00:45', plannedStartOfShift2: '18:00', plannedEndOfShift2: '20:00', plannedBreakOfShift2: '00:30' },
  { date: nextWeekDates[5], text: '6-12/¾;18-20/¾', firstShift: '06:00 - 12:00 / 00:45', secondShift: '18:00 - 20:00 / 00:45', plannedHours: '8:00', flexBalanceToDate: 'Flex saldo til dato: -6:33', calculatedHours: '6.5', plannedStartOfShift1: '06:00', plannedEndOfShift1: '12:00', plannedBreakOfShift1: '00:45', plannedStartOfShift2: '18:00', plannedEndOfShift2: '20:00', plannedBreakOfShift2: '00:45' },
  { date: lastWeekDates[6], text: '6-14/½', firstShift: '06:00 - 14:00 / 00:30', plannedHours: '8:00', flexBalanceToDate: 'Flex saldo til dato: -14:33', calculatedHours: '7.5', plannedStartOfShift1: '06:00', plannedEndOfShift1: '14:00', plannedBreakOfShift1: '00:30', plannedStartOfShift2: '', plannedEndOfShift2: '', plannedBreakOfShift2: '' },
];

const planTextsFutureWeek = [
  { date: futureWeekDates[0], text: '07:30-15:30;foobar', firstShift: '07:30 - 15:30 / 00:00', plannedHours: '8:00', flexBalanceToDate: 'Flex saldo til dato: -22:33', calculatedHours: '8', plannedStartOfShift1: '07:30', plannedEndOfShift1: '15:30', plannedBreakOfShift1: '00:00', plannedStartOfShift2: '', plannedEndOfShift2: '', plannedBreakOfShift2: '' },
  { date: futureWeekDates[1], text: '7:45-16/0.75', firstShift: '07:45 - 16:00 / 00:45', plannedHours: '8:00', flexBalanceToDate: 'Flex saldo til dato: -30:33', calculatedHours: '7.5', plannedStartOfShift1: '07:45', plannedEndOfShift1: '16:00', plannedBreakOfShift1: '00:45', plannedStartOfShift2: '', plannedEndOfShift2: '', plannedBreakOfShift2: '' },
  { date: futureWeekDates[2], text: 'foo bar', plannedHours: '16:00', flexBalanceToDate: 'Flex saldo til dato: -46:33', calculatedHours: '16', plannedStartOfShift1: '', plannedEndOfShift1: '', plannedBreakOfShift1: '', plannedStartOfShift2: '', plannedEndOfShift2: '', plannedBreakOfShift2: '' },
  { date: futureWeekDates[3], text: '6-12;18:00-20:00', firstShift: '06:00 - 12:00 / 00:00', secondShift: '18:00 - 20:00 / 00:00', plannedHours: '8:00', flexBalanceToDate: 'Flex saldo til dato: -54:33', calculatedHours: '8', plannedStartOfShift1: '06:00', plannedEndOfShift1: '12:00', plannedBreakOfShift1: '00:00', plannedStartOfShift2: '18:00', plannedEndOfShift2: '20:00', plannedBreakOfShift2: '00:00' },
  { date: futureWeekDates[4], text: ' ', plannedHours: '8:00', flexBalanceToDate: 'Flex saldo til dato: -62:33', calculatedHours: '8', plannedStartOfShift1: '', plannedEndOfShift1: '', plannedBreakOfShift1: '', plannedStartOfShift2: '', plannedEndOfShift2: '', plannedBreakOfShift2: '' },
  { date: futureWeekDates[5], text: '6-12/¾;18-20/¾', firstShift: '06:00 - 12:00 / 00:45', secondShift: '18:00 - 20:00 / 00:45', plannedHours: '8:00', flexBalanceToDate: 'Flex saldo til dato: -70:33', calculatedHours: '6.5', plannedStartOfShift1: '06:00', plannedEndOfShift1: '12:00', plannedBreakOfShift1: '00:45', plannedStartOfShift2: '18:00', plannedEndOfShift2: '20:00', plannedBreakOfShift2: '00:45' },
  { date: futureWeekDates[6], text: '6-14/½', firstShift: '06:00 - 14:00 / 00:30', plannedHours: '8:00', flexBalanceToDate: 'Flex saldo til dato: -78:33', calculatedHours: '7.5', plannedStartOfShift1: '06:00', plannedEndOfShift1: '14:00', plannedBreakOfShift1: '00:30', plannedStartOfShift2: '', plannedEndOfShift2: '', plannedBreakOfShift2: '' },
];

const updatePlanTextsFutureWeek = [
  { date: futureWeekDates[0], text: '07:30-15:30;foobar', firstShift: '07:30 - 15:30 / 00:00', plannedHours: '8:00', flexBalanceToDate: 'Flex saldo til dato: -22:33', calculatedHours: '8', plannedStartOfShift1: '07:30', plannedEndOfShift1: '15:30', plannedBreakOfShift1: '00:00', plannedStartOfShift2: '', plannedEndOfShift2: '', plannedBreakOfShift2: '' },
  { date: futureWeekDates[1], text: '7:45-16/0.75', firstShift: '07:45 - 16:00 / 00:45', plannedHours: '8:00', flexBalanceToDate: 'Flex saldo til dato: -30:33', calculatedHours: '7.5', plannedStartOfShift1: '07:45', plannedEndOfShift1: '16:00', plannedBreakOfShift1: '00:45', plannedStartOfShift2: '', plannedEndOfShift2: '', plannedBreakOfShift2: '' },
  { date: futureWeekDates[2], text: 'foo bar', plannedHours: '', flexBalanceToDate: 'Flex saldo til dato: -46:33', calculatedHours: '0', plannedStartOfShift1: '', plannedEndOfShift1: '', plannedBreakOfShift1: '', plannedStartOfShift2: '', plannedEndOfShift2: '', plannedBreakOfShift2: '' },
  { date: futureWeekDates[3], text: '6-12;18:00-20:00', firstShift: '06:00 - 12:00 / 00:00', secondShift: '18:00 - 20:00 / 00:00', plannedHours: '8:00', flexBalanceToDate: 'Flex saldo til dato: -54:33', calculatedHours: '8', plannedStartOfShift1: '06:00', plannedEndOfShift1: '12:00', plannedBreakOfShift1: '00:00', plannedStartOfShift2: '18:00', plannedEndOfShift2: '20:00', plannedBreakOfShift2: '00:00' },
  { date: futureWeekDates[4], text: ' ', plannedHours: '12:00', flexBalanceToDate: 'Flex saldo til dato: -62:33', calculatedHours: '12', plannedStartOfShift1: '', plannedEndOfShift1: '', plannedBreakOfShift1: '', plannedStartOfShift2: '', plannedEndOfShift2: '', plannedBreakOfShift2: '' },
  { date: futureWeekDates[5], text: '6-12/¾;18-20/¾', firstShift: '06:00 - 12:00 / 00:45', secondShift: '18:00 - 20:00 / 00:45', plannedHours: '8:00', flexBalanceToDate: 'Flex saldo til dato: -70:33', calculatedHours: '6.5', plannedStartOfShift1: '06:00', plannedEndOfShift1: '12:00', plannedBreakOfShift1: '00:45', plannedStartOfShift2: '18:00', plannedEndOfShift2: '20:00', plannedBreakOfShift2: '00:45' },
  { date: futureWeekDates[6], text: '6-14/½', firstShift: '06:00 - 14:00 / 00:30', plannedHours: '8:00', flexBalanceToDate: 'Flex saldo til dato: -78:33', calculatedHours: '7.5', plannedStartOfShift1: '06:00', plannedEndOfShift1: '14:00', plannedBreakOfShift1: '00:30', plannedStartOfShift2: '', plannedEndOfShift2: '', plannedBreakOfShift2: '' },
];


test.describe('Dashboard edit values', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('http://localhost:4200');
    await new LoginPage(page).login();
  });

  test('should enable auto break calculations with empty values', async ({ page }) => {
    const pluginPage = new PluginPage(page);
    await pluginPage.Navbar.goToPluginsPage();
    await page.locator('#actionMenu')
      .scrollIntoViewIfNeeded();
    await expect(page.locator('#actionMenu')).toBeVisible();
    await page.locator('#actionMenu').click({ force: true });

    const settingsGetPromise = page.waitForResponse(
      r => r.url().includes('/api/time-planning-pn/settings') && r.request().method() === 'GET'
    );
    await page.locator('#plugin-settings-link0').click();
    await settingsGetPromise;

    // autoBreakCalculationActiveToggle-button
    const currentState = await page.locator('#autoBreakCalculationActiveToggle-button').getAttribute('aria-checked');
    if (currentState === 'false') {
      await page.locator('#autoBreakCalculationActiveToggle-button').click();
    }

    const settingsUpdatePromise = page.waitForResponse(
      r => r.url().includes('/api/time-planning-pn/settings') && r.request().method() === 'PUT'
    );
    await page.locator('#saveSettings').click();
    await settingsUpdatePromise;

    const indexUpdatePromise = page.waitForResponse(
      r => r.url().includes('/api/time-planning-pn/plannings/index') && r.request().method() === 'POST'
    );
    await page.locator('mat-nested-tree-node').filter({ hasText: 'Timeregistrering' }).click();
    await page.locator('mat-tree-node').filter({ hasText: 'Dashboard' }).click();
    await indexUpdatePromise;

    // Wait for spinner after index update
    if (await page.locator('.overlay-spinner').count() > 0) {
      await page.locator('.overlay-spinner').waitFor({ state: 'hidden', timeout: 30000 });
    }

    await page.locator('#firstColumn0').click();

    const useGoogleSheetClass = await page.locator('#useGoogleSheetAsDefault > div > div > input').getAttribute('class');
    if (useGoogleSheetClass !== 'mdc-checkbox__native-control mdc-checkbox--selected') {
      await page.locator('#useGoogleSheetAsDefault').click();
    }

    const autoBreakClass = await page.locator('#autoBreakCalculationActive > div > div > input').getAttribute('class');
    if (autoBreakClass !== 'mdc-checkbox__native-control mdc-checkbox--selected') {
      await page.locator('#autoBreakCalculationActive').click();
    }
  });

  test('should enable auto break calculations with values', async ({ page }) => {
    await page.locator('mat-nested-tree-node').filter({ hasText: 'Timeregistrering' }).click();
    await page.locator('mat-tree-node').filter({ hasText: 'Timeregistrering' }).click();
    await page.locator('mat-toolbar > div > button .mat-mdc-button-persistent-ripple').first().locator('..').click();
    await page.locator('#workingHoursSite').locator('input').clear();
    await page.locator('#workingHoursSite').locator('input').fill('c d');
    await page.locator('.ng-option.ng-option-marked').click();

    const updatePromise = page.waitForResponse(
      r => r.url().includes('/api/time-planning-pn/working-hours/index') && r.request().method() === 'POST'
    );
    await new TimePlanningWorkingHoursPage(page).dateFormInput().click();
    await selectDateRangeOnNewDatePicker(
      page,
      filters[0].dateRange.yearFrom, filters[0].dateRange.monthFrom, filters[0].dateRange.dayFrom,
      filters[0].dateRange.yearTo, filters[0].dateRange.monthTo, filters[0].dateRange.dayTo
    );
    await updatePromise;

    await expect(page.locator('#sumFlex0 input')).toHaveValue(new RegExp('97.45'));
    for (let i = 0; i < planHours.length; i++) {
      const id = `#planHours${i + 1}`;
      await page.locator(id).locator('input').clear();
      await page.locator(id).locator('input').fill(planHours[i].hours.toString());
      const sumFlexId = `#sumFlex${i + 1}`;
      await expect(page.locator(sumFlexId).locator('input')).toHaveValue(new RegExp(planHours[i].sumFlex.toString()));
      const nettoHoursId = `#nettoHours${i + 1}`;
      await expect(page.locator(nettoHoursId).locator('input')).toHaveValue(new RegExp(planHours[i].nettoHours.toString()));
      const flexId = `#flexHours${i + 1}`;
      await expect(page.locator(flexId).locator('input')).toHaveValue(new RegExp(planHours[i].flex.toString()));
    }
    for (let i = 0; i < planTexts.length; i++) {
      const id = `#planText${i + 1}`;
      await page.locator(id).locator('input').clear();
      await page.locator(id).locator('input').fill(planTexts[i].text);
    }

    const savePromise = page.waitForResponse(
      r => r.url().includes('/api/time-planning-pn/working-hours') && r.request().method() === 'PUT'
    );
    await page.locator('#workingHoursSave').click();
    await savePromise;
    await expect(page.locator('#sumFlex7 input')).toHaveValue(new RegExp('41.45'));

    const updatePromise2 = page.waitForResponse(
      r => r.url().includes('/api/time-planning-pn/working-hours/index') && r.request().method() === 'POST'
    );
    await new TimePlanningWorkingHoursPage(page).dateFormInput().click();
    await selectDateRangeOnNewDatePicker(
      page,
      filtersNextWeek[0].dateRange.yearFrom, filtersNextWeek[0].dateRange.monthFrom, filtersNextWeek[0].dateRange.dayFrom,
      filtersNextWeek[0].dateRange.yearTo, filtersNextWeek[0].dateRange.monthTo, filtersNextWeek[0].dateRange.dayTo
    );
    await updatePromise2;

    await expect(page.locator('#sumFlex0 input')).toHaveValue(new RegExp('41.45'));
    await expect(page.locator('#nettoHours0 input')).toHaveValue(new RegExp('0'));

    for (let i = 0; i < planHoursNextWeek.length; i++) {
      const id = `#planHours${i + 1}`;
      await page.locator(id).locator('input').clear();
      await page.locator(id).locator('input').fill(planHoursNextWeek[i].hours.toString());
      const sumFlexId = `#sumFlex${i + 1}`;
      await expect(page.locator(sumFlexId).locator('input')).toHaveValue(new RegExp(planHoursNextWeek[i].sumFlex.toString()));
      const nettoHoursId = `#nettoHours${i + 1}`;
      await expect(page.locator(nettoHoursId).locator('input')).toHaveValue(new RegExp(planHoursNextWeek[i].nettoHours.toString()));
      const flexId = `#flexHours${i + 1}`;
      await expect(page.locator(flexId).locator('input')).toHaveValue(new RegExp(planHoursNextWeek[i].flex.toString()));
    }
    for (let i = 0; i < planTextsNextWeek.length; i++) {
      const id = `#planText${i + 1}`;
      await page.locator(id).locator('input').clear();
      await page.locator(id).locator('input').fill(planTextsNextWeek[i].text);
    }

    const savePromise2 = page.waitForResponse(
      r => r.url().includes('/api/time-planning-pn/working-hours') && r.request().method() === 'PUT'
    );
    await page.locator('#workingHoursSave').click();
    await savePromise2;
    await expect(page.locator('#sumFlex7 input')).toHaveValue(new RegExp('-14.55'));
  });
});
