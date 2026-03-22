import { test, expect } from '@playwright/test';
import { LoginPage } from '../../../Login.page';
import { PluginPage } from '../../../Plugin.page';
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
  { date: nextWeekDates[0], hours: 8, sumFlex: 36.2, nettoHours: 0, flex: -8, humanFlex: '68:27'},
  { date: nextWeekDates[1], hours: 8, sumFlex: 28.2, nettoHours: 0, flex: -8, humanFlex: '60:27'},
  { date: nextWeekDates[2], hours: 0, sumFlex: 28.2, nettoHours: 0, flex: 0, humanFlex: '60:27'},
  { date: nextWeekDates[3], hours: 0, sumFlex: 28.2, nettoHours: 0, flex: 0, humanFlex: '60:27'},
  { date: nextWeekDates[4], hours: 0, sumFlex: 28.2, nettoHours: 0, flex: 0, humanFlex: '60:27'},
  { date: nextWeekDates[5], hours: 8, sumFlex: 20.2, nettoHours: 0, flex: -8, humanFlex: '52:27'},
  { date: nextWeekDates[6], hours: 8, sumFlex: 12.2, nettoHours: 0, flex: -8, humanFlex: '44:27'},
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
  { date: futureWeekDates[0], hours: 2, sumFlex: -11.05, nettoHours: 0, flex: -2, humanFlex: '42:27'},
  { date: futureWeekDates[1], hours: 4, sumFlex: -15.05, nettoHours: 0, flex: -4, humanFlex: '38:27'},
  { date: futureWeekDates[2], hours: 0, sumFlex: -15.05, nettoHours: 0, flex: 0, humanFlex: '38:27'},
  { date: futureWeekDates[3], hours: 10, sumFlex: -25.05, nettoHours: 0, flex: -10, humanFlex: '28:27'},
  { date: futureWeekDates[4], hours: 12, sumFlex: -37.05, nettoHours: 0, flex: -12, humanFlex: '16:27'},
  { date: futureWeekDates[5], hours: 3, sumFlex: -40.05, nettoHours: 0, flex: -3, humanFlex: '13:27'},
  { date: futureWeekDates[6], hours: 8, sumFlex: -48.05, nettoHours: 0, flex: -8, humanFlex:' 5:27'},
];

const planTexts = [
  { date: lastWeekDates[0], text: '07:30-15:30', plannedHours: '8:00', flexBalanceToDate: 'swap_vert89.45', calculatedHours: '8', plannedStartOfShift1: '07:30', plannedEndOfShift1: '15:30', plannedBreakOfShift1: '', plannedStartOfShift2: '', plannedEndOfShift2: '', plannedBreakOfShift2: '' },
  { date: lastWeekDates[1], text: '7:45-16:00/1', plannedHours: '8:00', flexBalanceToDate: 'swap_vert82.20', calculatedHours: '7.25', plannedStartOfShift1: '07:45', plannedEndOfShift1: '16:00', plannedBreakOfShift1: '01:00', plannedStartOfShift2: '', plannedEndOfShift2: '', plannedBreakOfShift2: '' },
  { date: lastWeekDates[2], text: '7:15-16:00/1;17-20/0,5', plannedHours: '8:00', flexBalanceToDate: 'swap_vert71.95', calculatedHours: '10.25', plannedStartOfShift1: '07:15', plannedEndOfShift1: '16:00', plannedBreakOfShift1: '01:00', plannedStartOfShift2: '17:00', plannedEndOfShift2: '20:00', plannedBreakOfShift2: '00:30' },
  { date: lastWeekDates[3], text: '6-12/½;18:00-20:00/0.5', plannedHours: '8:00', flexBalanceToDate: 'swap_vert64.95', calculatedHours: '7', plannedStartOfShift1: '06:00', plannedEndOfShift1: '12:00', plannedBreakOfShift1: '00:30', plannedStartOfShift2: '18:00', plannedEndOfShift2: '20:00', plannedBreakOfShift2: '00:30' },
  { date: lastWeekDates[4], text: '06:00-12:00/¾;18:00-20:00/0.5', plannedHours: '8:00', flexBalanceToDate: 'swap_vert58.20', calculatedHours: '6.75', plannedStartOfShift1: '06:00', plannedEndOfShift1: '12:00', plannedBreakOfShift1: '00:45', plannedStartOfShift2: '18:00', plannedEndOfShift2: '20:00', plannedBreakOfShift2: '00:30' },
  { date: lastWeekDates[5], text: '6-12/¾;18-20/¾', plannedHours: '8:00', flexBalanceToDate: 'swap_vert51.70', calculatedHours: '6.5', plannedStartOfShift1: '06:00', plannedEndOfShift1: '12:00', plannedBreakOfShift1: '00:45', plannedStartOfShift2: '18:00', plannedEndOfShift2: '20:00', plannedBreakOfShift2: '00:45' },
  { date: lastWeekDates[6], text: '6-14/½', plannedHours: '8:00', flexBalanceToDate: 'swap_vert44.20', calculatedHours: '7.5', plannedStartOfShift1: '06:00', plannedEndOfShift1: '14:00', plannedBreakOfShift1: '00:30', plannedStartOfShift2: '', plannedEndOfShift2: '', plannedBreakOfShift2: '' },
];

const updatePlanTexts = [
  { date: lastWeekDates[0], text: '07:30-15:30', plannedHours: '1', flexBalanceToDate: 'swap_vert89.45', calculatedHours: '8', plannedStartOfShift1: '07:30', plannedEndOfShift1: '15:30', plannedBreakOfShift1: '', plannedStartOfShift2: '', plannedEndOfShift2: '', plannedBreakOfShift2: '' },
  { date: lastWeekDates[1], text: '7:45-16:00/1', plannedHours: '2', flexBalanceToDate: 'swap_vert82.20', calculatedHours: '7.25', plannedStartOfShift1: '07:45', plannedEndOfShift1: '16:00', plannedBreakOfShift1: '01:00', plannedStartOfShift2: '', plannedEndOfShift2: '', plannedBreakOfShift2: '' },
  { date: lastWeekDates[2], text: '7:15-16:00/1;17-20/0,5', plannedHours: '3', flexBalanceToDate: 'swap_vert71.95', calculatedHours: '10.25', plannedStartOfShift1: '07:15', plannedEndOfShift1: '16:00', plannedBreakOfShift1: '01:00', plannedStartOfShift2: '17:00', plannedEndOfShift2: '20:00', plannedBreakOfShift2: '00:30' },
  { date: lastWeekDates[3], text: '6-12/½;18:00-20:00/0.5', plannedHours: '4', flexBalanceToDate: 'swap_vert64.95', calculatedHours: '7', plannedStartOfShift1: '06:00', plannedEndOfShift1: '12:00', plannedBreakOfShift1: '00:30', plannedStartOfShift2: '18:00', plannedEndOfShift2: '20:00', plannedBreakOfShift2: '00:30' },
  { date: lastWeekDates[4], text: '06:00-12:00/¾;18:00-20:00/0.5', plannedHours: '5', flexBalanceToDate: 'swap_vert58.20', calculatedHours: '6.75', plannedStartOfShift1: '06:00', plannedEndOfShift1: '12:00', plannedBreakOfShift1: '00:45', plannedStartOfShift2: '18:00', plannedEndOfShift2: '20:00', plannedBreakOfShift2: '00:30' },
  { date: lastWeekDates[5], text: '6-12/¾;18-20/¾', plannedHours: '6', flexBalanceToDate: 'swap_vert51.70', calculatedHours: '6.5', plannedStartOfShift1: '06:00', plannedEndOfShift1: '12:00', plannedBreakOfShift1: '00:45', plannedStartOfShift2: '18:00', plannedEndOfShift2: '20:00', plannedBreakOfShift2: '00:45' },
  { date: lastWeekDates[6], text: '6-14/½', plannedHours: '7', flexBalanceToDate: 'swap_vert44.20', calculatedHours: '7.5', plannedStartOfShift1: '06:00', plannedEndOfShift1: '14:00', plannedBreakOfShift1: '00:30', plannedStartOfShift2: '', plannedEndOfShift2: '', plannedBreakOfShift2: '' },
];

const planTextsNextWeek = [
  { date: nextWeekDates[0], text: '07:30-15:30', firstShift: '07:30 - 15:30 / 00:00', plannedHours: '8:00', flexBalanceToDate: 'swap_vert33:27', calculatedHours: '8', plannedStartOfShift1: '07:30', plannedEndOfShift1: '15:30', plannedBreakOfShift1: '', plannedStartOfShift2: '', plannedEndOfShift2: '', plannedBreakOfShift2: '' },
  { date: nextWeekDates[1], text: '7:45-16:00/1', firstShift: '07:45 - 16:00 / 01:00', plannedHours: '8:00', flexBalanceToDate: 'swap_vert25:27', calculatedHours: '7.25', plannedStartOfShift1: '07:45', plannedEndOfShift1: '16:00', plannedBreakOfShift1: '01:00', plannedStartOfShift2: '', plannedEndOfShift2: '', plannedBreakOfShift2: '' },
  { date: nextWeekDates[2], text: '7:15-16:00/1;17-20/0,5', firstShift: '07:15 - 16:00 / 01:00', secondShift: '17:00 - 20:00 / 00:30', plannedHours: '8:00', flexBalanceToDate: 'swap_vert17:27', calculatedHours: '10.25', plannedStartOfShift1: '07:15', plannedEndOfShift1: '16:00', plannedBreakOfShift1: '01:00', plannedStartOfShift2: '17:00', plannedEndOfShift2: '20:00', plannedBreakOfShift2: '00:30' },
  { date: nextWeekDates[3], text: '6-12/½;18:00-20:00/0.5', firstShift: '06:00 - 12:00 / 00:30', secondShift: '18:00 - 20:00 / 00:30', plannedHours: '8:00', flexBalanceToDate: 'swap_vert9:27', calculatedHours: '7', plannedStartOfShift1: '06:00', plannedEndOfShift1: '12:00', plannedBreakOfShift1: '00:30', plannedStartOfShift2: '18:00', plannedEndOfShift2: '20:00', plannedBreakOfShift2: '00:30' },
  { date: nextWeekDates[4], text: '06:00-12:00/¾;18:00-20:00/0.5', firstShift: '06:00 - 12:00 / 00:45', secondShift: '18:00 - 20:00 / 00:30', plannedHours: '8:00', flexBalanceToDate: 'swap_vert1:27', calculatedHours: '6.75', plannedStartOfShift1: '06:00', plannedEndOfShift1: '12:00', plannedBreakOfShift1: '00:45', plannedStartOfShift2: '18:00', plannedEndOfShift2: '20:00', plannedBreakOfShift2: '00:30' },
  { date: nextWeekDates[5], text: '6-12/¾;18-20/¾', firstShift: '06:00 - 12:00 / 00:45', secondShift: '18:00 - 20:00 / 00:45', plannedHours: '8:00', flexBalanceToDate: 'swap_vert-6:33', calculatedHours: '6.5', plannedStartOfShift1: '06:00', plannedEndOfShift1: '12:00', plannedBreakOfShift1: '00:45', plannedStartOfShift2: '18:00', plannedEndOfShift2: '20:00', plannedBreakOfShift2: '00:45' },
  { date: lastWeekDates[6], text: '6-14/½', firstShift: '06:00 - 14:00 / 00:30', plannedHours: '8:00', flexBalanceToDate: 'swap_vert-14:33', calculatedHours: '7.5', plannedStartOfShift1: '06:00', plannedEndOfShift1: '14:00', plannedBreakOfShift1: '00:30', plannedStartOfShift2: '', plannedEndOfShift2: '', plannedBreakOfShift2: '' },
];

const updatePlanTextsNextWeek = [
  { date: nextWeekDates[0], text: '07:30-15:30', firstShift: '07:30 - 15:30 / 00:00', plannedHours: '8:00', flexBalanceToDate: 'swap_vert33:27', calculatedHours: '8', plannedStartOfShift1: '07:30', plannedEndOfShift1: '15:30', plannedBreakOfShift1: '', plannedStartOfShift2: '', plannedEndOfShift2: '', plannedBreakOfShift2: '' },
  { date: nextWeekDates[1], text: '7:45-16:00/1', firstShift: '07:45 - 16:00 / 01:00', plannedHours: '8:00', flexBalanceToDate: 'swap_vert25:27', calculatedHours: '7.25', plannedStartOfShift1: '07:45', plannedEndOfShift1: '16:00', plannedBreakOfShift1: '01:00', plannedStartOfShift2: '', plannedEndOfShift2: '', plannedBreakOfShift2: '' },
  { date: nextWeekDates[2], text: '7:15-16:00/1;17-20/0,5', firstShift: '07:15 - 16:00 / 01:00', secondShift: '17:00 - 20:00 / 00:30', plannedHours: '8:00', flexBalanceToDate: 'swap_vert17:27', calculatedHours: '10.25', plannedStartOfShift1: '07:15', plannedEndOfShift1: '16:00', plannedBreakOfShift1: '01:00', plannedStartOfShift2: '17:00', plannedEndOfShift2: '20:00', plannedBreakOfShift2: '00:30' },
  { date: nextWeekDates[3], text: '6-12/½;18:00-20:00/0.5', firstShift: '06:00 - 12:00 / 00:30', secondShift: '18:00 - 20:00 / 00:30', plannedHours: '8:00', flexBalanceToDate: 'swap_vert9:27', calculatedHours: '7', plannedStartOfShift1: '06:00', plannedEndOfShift1: '12:00', plannedBreakOfShift1: '00:30', plannedStartOfShift2: '18:00', plannedEndOfShift2: '20:00', plannedBreakOfShift2: '00:30' },
  { date: nextWeekDates[4], text: '06:00-12:00/¾;18:00-20:00/0.5', firstShift: '06:00 - 12:00 / 00:45', secondShift: '18:00 - 20:00 / 00:30', plannedHours: '8:00', flexBalanceToDate: 'swap_vert1:27', calculatedHours: '7', plannedStartOfShift1: '06:00', plannedEndOfShift1: '12:00', plannedBreakOfShift1: '00:45', plannedStartOfShift2: '18:00', plannedEndOfShift2: '20:00', plannedBreakOfShift2: '00:30' },
  { date: nextWeekDates[5], text: '6-12/¾;18-20/¾', firstShift: '06:00 - 12:00 / 00:45', secondShift: '18:00 - 20:00 / 00:45', plannedHours: '8:00', flexBalanceToDate: 'swap_vert-6:33', calculatedHours: '6.5', plannedStartOfShift1: '06:00', plannedEndOfShift1: '12:00', plannedBreakOfShift1: '00:45', plannedStartOfShift2: '18:00', plannedEndOfShift2: '20:00', plannedBreakOfShift2: '00:45' },
  { date: lastWeekDates[6], text: '6-14/½', firstShift: '06:00 - 14:00 / 00:30', plannedHours: '8:00', flexBalanceToDate: 'swap_vert-14:33', calculatedHours: '7.5', plannedStartOfShift1: '06:00', plannedEndOfShift1: '14:00', plannedBreakOfShift1: '00:30', plannedStartOfShift2: '', plannedEndOfShift2: '', plannedBreakOfShift2: '' },
];

const planTextsFutureWeek = [
  { date: futureWeekDates[0], text: '07:30-15:30;foobar', firstShift: '07:30 - 15:30 / 00:00', plannedHours: '8:00', flexBalanceToDate: 'swap_vert-22:33', calculatedHours: '8', plannedStartOfShift1: '07:30', plannedEndOfShift1: '15:30', plannedBreakOfShift1: '', plannedStartOfShift2: '', plannedEndOfShift2: '', plannedBreakOfShift2: '' },
  { date: futureWeekDates[1], text: '7:45-16/0.75', firstShift: '07:45 - 16:00 / 00:45', plannedHours: '8:00', flexBalanceToDate: 'swap_vert-30:33', calculatedHours: '7.5', plannedStartOfShift1: '07:45', plannedEndOfShift1: '16:00', plannedBreakOfShift1: '00:45', plannedStartOfShift2: '', plannedEndOfShift2: '', plannedBreakOfShift2: '' },
  { date: futureWeekDates[2], text: 'foo bar', plannedHours: '16:00', flexBalanceToDate: 'swap_vert-46:33', calculatedHours: '16', plannedStartOfShift1: '', plannedEndOfShift1: '', plannedBreakOfShift1: '', plannedStartOfShift2: '', plannedEndOfShift2: '', plannedBreakOfShift2: '' },
  { date: futureWeekDates[3], text: '6-12;18:00-20:00', firstShift: '06:00 - 12:00 / 00:00', secondShift: '18:00 - 20:00 / 00:00', plannedHours: '8:00', flexBalanceToDate: 'swap_vert-54:33', calculatedHours: '8', plannedStartOfShift1: '06:00', plannedEndOfShift1: '12:00', plannedBreakOfShift1: '', plannedStartOfShift2: '18:00', plannedEndOfShift2: '20:00', plannedBreakOfShift2: '' },
  { date: futureWeekDates[4], text: ' ', plannedHours: '8:00', flexBalanceToDate: 'swap_vert-62:33', calculatedHours: '8', plannedStartOfShift1: '', plannedEndOfShift1: '', plannedBreakOfShift1: '', plannedStartOfShift2: '', plannedEndOfShift2: '', plannedBreakOfShift2: '' },
  { date: futureWeekDates[5], text: '6-12/¾;18-20/¾', firstShift: '06:00 - 12:00 / 00:45', secondShift: '18:00 - 20:00 / 00:45', plannedHours: '8:00', flexBalanceToDate: 'swap_vert-70:33', calculatedHours: '6.5', plannedStartOfShift1: '06:00', plannedEndOfShift1: '12:00', plannedBreakOfShift1: '00:45', plannedStartOfShift2: '18:00', plannedEndOfShift2: '20:00', plannedBreakOfShift2: '00:45' },
  { date: futureWeekDates[6], text: '6-14/½', firstShift: '06:00 - 14:00 / 00:30', plannedHours: '8:00', flexBalanceToDate: 'swap_vert-78:33', calculatedHours: '7.5', plannedStartOfShift1: '06:00', plannedEndOfShift1: '14:00', plannedBreakOfShift1: '00:30', plannedStartOfShift2: '', plannedEndOfShift2: '', plannedBreakOfShift2: '' },
];

const updatePlanTextsFutureWeek = [
  { date: futureWeekDates[0], text: '07:30-15:30;foobar', firstShift: '07:30 - 15:30 / 00:00', plannedHours: '8:00', flexBalanceToDate: 'swap_vert-22:33', calculatedHours: '8', plannedStartOfShift1: '07:30', plannedEndOfShift1: '15:30', plannedBreakOfShift1: '', plannedStartOfShift2: '', plannedEndOfShift2: '', plannedBreakOfShift2: '' },
  { date: futureWeekDates[1], text: '7:45-16/0.75', firstShift: '07:45 - 16:00 / 00:45', plannedHours: '8:00', flexBalanceToDate: 'swap_vert-30:33', calculatedHours: '7.5', plannedStartOfShift1: '07:45', plannedEndOfShift1: '16:00', plannedBreakOfShift1: '00:45', plannedStartOfShift2: '', plannedEndOfShift2: '', plannedBreakOfShift2: '' },
  { date: futureWeekDates[2], text: 'foo bar', plannedHours: '', flexBalanceToDate: 'swap_vert-46:33', calculatedHours: '0', plannedStartOfShift1: '', plannedEndOfShift1: '', plannedBreakOfShift1: '', plannedStartOfShift2: '', plannedEndOfShift2: '', plannedBreakOfShift2: '' },
  { date: futureWeekDates[3], text: '6-12;18:00-20:00', firstShift: '06:00 - 12:00 / 00:00', secondShift: '18:00 - 20:00 / 00:00', plannedHours: '8:00', flexBalanceToDate: 'swap_vert-54:33', calculatedHours: '8', plannedStartOfShift1: '06:00', plannedEndOfShift1: '12:00', plannedBreakOfShift1: '', plannedStartOfShift2: '18:00', plannedEndOfShift2: '20:00', plannedBreakOfShift2: '' },
  { date: futureWeekDates[4], text: ' ', plannedHours: '12:00', flexBalanceToDate: 'swap_vert-62:33', calculatedHours: '12', plannedStartOfShift1: '', plannedEndOfShift1: '', plannedBreakOfShift1: '', plannedStartOfShift2: '', plannedEndOfShift2: '', plannedBreakOfShift2: '' },
  { date: futureWeekDates[5], text: '6-12/¾;18-20/¾', firstShift: '06:00 - 12:00 / 00:45', secondShift: '18:00 - 20:00 / 00:45', plannedHours: '8:00', flexBalanceToDate: 'swap_vert-70:33', calculatedHours: '6.5', plannedStartOfShift1: '06:00', plannedEndOfShift1: '12:00', plannedBreakOfShift1: '00:45', plannedStartOfShift2: '18:00', plannedEndOfShift2: '20:00', plannedBreakOfShift2: '00:45' },
  { date: futureWeekDates[6], text: '6-14/½', firstShift: '06:00 - 14:00 / 00:30', plannedHours: '8:00', flexBalanceToDate: 'swap_vert-78:33', calculatedHours: '7.5', plannedStartOfShift1: '06:00', plannedEndOfShift1: '14:00', plannedBreakOfShift1: '00:30', plannedStartOfShift2: '', plannedEndOfShift2: '', plannedBreakOfShift2: '' },
];

async function waitForSpinner(page: import('@playwright/test').Page) {
  if (await page.locator('.overlay-spinner').count() > 0) {
    await page.locator('.overlay-spinner').waitFor({ state: 'hidden', timeout: 30000 });
  }
}

test.describe('Dashboard assert', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('http://localhost:4200');
    await new LoginPage(page).login();
  });

  test('should go to dashboard and set to use google sheet as default and check if the settings are correct', async ({ page }) => {
    const whPage = new TimePlanningWorkingHoursPage(page);
    const pluginPage = new PluginPage(page);

    await page.locator('mat-nested-tree-node').filter({ hasText: 'Timeregistrering' }).click();
    const indexUpdatePromise = page.waitForResponse(r => r.url().includes('/api/time-planning-pn/plannings/index') && r.request().method() === 'POST');
    await page.locator('mat-tree-node').filter({ hasText: 'Dashboard' }).click();
    await indexUpdatePromise;
    await waitForSpinner(page);

    // Wait for spinner before clicking firstColumn3
    await waitForSpinner(page);
    await page.locator('#firstColumn3').click();
    // Wait for spinner before clicking checkbox
    await waitForSpinner(page);
    await page.locator('#useGoogleSheetAsDefault').click();
    // Wait for spinner before clicking Save button
    await waitForSpinner(page);
    const assignSitePromise = page.waitForResponse(r => r.url().includes('/api/time-planning-pn/settings/assigned-site') && r.request().method() === 'PUT');
    await page.locator('#saveButton').click();
    await assignSitePromise;
    const indexUpdatePromise2 = page.waitForResponse(r => r.url().includes('/api/time-planning-pn/plannings/index') && r.request().method() === 'POST');
    await indexUpdatePromise2;
    await waitForSpinner(page);
    await page.waitForTimeout(2000);

    // Wait for spinner before clicking Timeregistrering menu
    await waitForSpinner(page);
    await page.locator('mat-tree-node').filter({ hasText: 'Timeregistrering' }).click();
    // Wait for spinner before clicking toolbar button
    await waitForSpinner(page);
    await page.locator('mat-toolbar > div > button .mat-mdc-button-persistent-ripple').first().locator('..').click();
    await page.locator('#workingHoursSite').locator('input').fill('c d');
    await page.locator('.ng-option.ng-option-marked').click();

    const updatePromise = page.waitForResponse(r => r.url().includes('/api/time-planning-pn/working-hours/index') && r.request().method() === 'POST');
    await whPage.dateFormInput().click();
    await selectDateRangeOnNewDatePicker(
      page,
      filters[0].dateRange.yearFrom, filters[0].dateRange.monthFrom, filters[0].dateRange.dayFrom,
      filters[0].dateRange.yearTo, filters[0].dateRange.monthTo, filters[0].dateRange.dayTo
    );
    await updatePromise;
    await page.locator('.overlay-spinner', { hasText: '' }).waitFor({ state: 'hidden', timeout: 30000 });

    await expect(page.locator('#sumFlex0 input')).toHaveValue(/97\.45/);

    for (let i = 0; i < planHours.length; i++) {
      const id = `#planHours${i + 1}`;
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
      await page.locator(id).locator('input').fill(planTexts[i].text);
    }

    const savePromise = page.waitForResponse(r => r.url().includes('/api/time-planning-pn/working-hours') && r.request().method() === 'PUT');
    await page.locator('#workingHoursSave').click();
    await savePromise;
    await page.locator('.overlay-spinner').waitFor({ state: 'hidden', timeout: 30000 });
    await expect(page.locator('#sumFlex7 input')).toHaveValue(/41\.45/);

    // Next week
    await whPage.dateFormInput().click();
    const updatePromise2 = page.waitForResponse(r => r.url().includes('/api/time-planning-pn/working-hours/index') && r.request().method() === 'POST');
    await selectDateRangeOnNewDatePicker(
      page,
      filtersNextWeek[0].dateRange.yearFrom, filtersNextWeek[0].dateRange.monthFrom, filtersNextWeek[0].dateRange.dayFrom,
      filtersNextWeek[0].dateRange.yearTo, filtersNextWeek[0].dateRange.monthTo, filtersNextWeek[0].dateRange.dayTo
    );
    await updatePromise2;
    await page.locator('.overlay-spinner').waitFor({ state: 'hidden', timeout: 30000 });

    await expect(page.locator('#sumFlex0 input')).toHaveValue(/41\.45/);
    await expect(page.locator('#nettoHours0 input')).toHaveValue(/0/);

    for (let i = 0; i < planHoursNextWeek.length; i++) {
      const id = `#planHours${i + 1}`;
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
      await page.locator(id).locator('input').fill(planTextsNextWeek[i].text);
    }

    await page.locator('#workingHoursSave').click();
    const savePromise2 = page.waitForResponse(r => r.url().includes('/api/time-planning-pn/working-hours') && r.request().method() === 'PUT');
    await savePromise2;
    await page.locator('.overlay-spinner').waitFor({ state: 'hidden', timeout: 30000 });
    await expect(page.locator('#sumFlex7 input')).toHaveValue(/-14\.55/);

    // Future week
    const updatePromise4 = page.waitForResponse(r => r.url().includes('/api/time-planning-pn/working-hours/index') && r.request().method() === 'POST');
    await whPage.dateFormInput().click();
    await selectDateRangeOnNewDatePicker(
      page,
      filtersFutureWeek[0].dateRange.yearFrom, filtersFutureWeek[0].dateRange.monthFrom, filtersFutureWeek[0].dateRange.dayFrom,
      filtersFutureWeek[0].dateRange.yearTo, filtersFutureWeek[0].dateRange.monthTo, filtersFutureWeek[0].dateRange.dayTo
    );
    await updatePromise4;
    await page.locator('.overlay-spinner').waitFor({ state: 'hidden', timeout: 30000 });

    await expect(page.locator('#nettoHours0 input')).toHaveValue(/0/);

    for (let i = 0; i < planHoursFutureWeek.length; i++) {
      await page.waitForTimeout(1000);
      const id = `#planHours${i + 1}`;
      await page.locator(id).locator('input').fill(planHoursFutureWeek[i].hours.toString());
      const sumFlexId = `#sumFlex${i + 1}`;
      await expect(page.locator(sumFlexId).locator('input')).toHaveValue(new RegExp(planHoursFutureWeek[i].sumFlex.toString()));
      const nettoHoursId = `#nettoHours${i + 1}`;
      await expect(page.locator(nettoHoursId).locator('input')).toHaveValue(new RegExp(planHoursFutureWeek[i].nettoHours.toString()));
      const flexId = `#flexHours${i + 1}`;
      await expect(page.locator(flexId).locator('input')).toHaveValue(new RegExp(planHoursFutureWeek[i].flex.toString()));
    }

    for (let i = 0; i < planTextsFutureWeek.length; i++) {
      const id = `#planText${i + 1}`;
      await page.locator(id).locator('input').fill(planTextsFutureWeek[i].text);
    }

    await page.locator('#workingHoursSave').click();
    const savePromise3 = page.waitForResponse(r => r.url().includes('/api/time-planning-pn/working-hours') && r.request().method() === 'PUT');
    await savePromise3;
    await page.locator('.overlay-spinner').waitFor({ state: 'hidden', timeout: 30000 });
    await page.locator('mat-toolbar > div > button .mat-mdc-button-persistent-ripple').first().locator('..').click();
    await expect(page.locator('#sumFlex7 input')).toHaveValue(/-78\.55/);

    await pluginPage.Navbar.goToPluginsPage();

    await page.locator('#actionMenu').scrollIntoViewIfNeeded();
    await expect(page.locator('#actionMenu')).toBeVisible();
    await page.locator('#actionMenu').click({ force: true });

    const [settingsResp] = await Promise.all([
      page.waitForResponse(r => r.url().includes('/api/time-planning-pn/settings') && r.request().method() === 'GET'),
      page.locator('#plugin-settings-link0').click(),
    ]);

    await page.locator('#forceLoadAllPlanningsFromGoogleSheet').click();
    await page.locator('#saveSettings').click();

    const indexUpdatePromise3 = page.waitForResponse(r => r.url().includes('/api/time-planning-pn/plannings/index') && r.request().method() === 'POST');
    await page.locator('mat-tree-node').filter({ hasText: 'Dashboard' }).click();
    await indexUpdatePromise3;
    await waitForSpinner(page);

    await page.locator('mat-toolbar > div > button .mat-mdc-button-persistent-ripple').first().locator('..').click();

    await page.locator('#workingHoursSite').locator('input').fill('c d');
    await page.locator('.ng-option.ng-option-marked').click();
    const indexUpdatePromise4 = page.waitForResponse(r => r.url().includes('/api/time-planning-pn/plannings/index') && r.request().method() === 'POST');
    await indexUpdatePromise4;
    await waitForSpinner(page);

    const indexUpdatePromise5 = page.waitForResponse(r => r.url().includes('/api/time-planning-pn/plannings/index') && r.request().method() === 'POST');
    await page.locator('#backwards').click();
    await indexUpdatePromise5;
    await waitForSpinner(page);

    await expect(page.locator('#plannedHours0')).toContainText('53:15');

    for (let i = 0; i < planTexts.length; i++) {
      const flexBalanceToDateId = `#flexBalanceToDate0_${i}`;
      await expect(page.locator(flexBalanceToDateId)).toContainText(planTexts[i].flexBalanceToDate);

      const cellId = `#cell0_${i}`;
      await page.locator(cellId).scrollIntoViewIfNeeded();
      await page.locator(cellId).click();
      await page.locator('#planHours').waitFor({ state: 'visible', timeout: 15000 });
      await page.waitForTimeout(500);
      await page.locator('#planHours').scrollIntoViewIfNeeded();
      await expect(page.locator('#planHours')).toHaveValue(planTexts[i].calculatedHours);
      await expect(page.locator('[data-testid="plannedStartOfShift1"]')).toHaveValue(planTexts[i].plannedStartOfShift1);
      await expect(page.locator('[data-testid="plannedBreakOfShift1"]')).toHaveValue(planTexts[i].plannedBreakOfShift1);
      await expect(page.locator('[data-testid="plannedEndOfShift1"]')).toHaveValue(planTexts[i].plannedEndOfShift1);
      await expect(page.locator('[data-testid="plannedStartOfShift2"]')).toHaveValue(planTexts[i].plannedStartOfShift2);
      await expect(page.locator('[data-testid="plannedBreakOfShift2"]')).toHaveValue(planTexts[i].plannedBreakOfShift2);
      await expect(page.locator('[data-testid="plannedEndOfShift2"]')).toHaveValue(planTexts[i].plannedEndOfShift2);
      await page.locator('#cancelButton').click();
      await page.waitForTimeout(500);
    }

    // Navigate forwards twice to get to next week
    const indexUpdatePromise6 = page.waitForResponse(r => r.url().includes('/api/time-planning-pn/plannings/index') && r.request().method() === 'POST');
    await page.locator('#forwards').click();
    await indexUpdatePromise6;
    await waitForSpinner(page);
    await page.waitForTimeout(1000);

    const indexUpdatePromise7 = page.waitForResponse(r => r.url().includes('/api/time-planning-pn/plannings/index') && r.request().method() === 'POST');
    await page.locator('#forwards').click();
    await indexUpdatePromise7;
    await waitForSpinner(page);
    await page.waitForTimeout(1000);

    for (let i = 0; i < planTextsNextWeek.length; i++) {
      const firstShiftId = `#firstShift0_${i}`;
      await expect(page.locator(firstShiftId)).toContainText(planTextsNextWeek[i].firstShift!);
      if (planTextsNextWeek[i].secondShift) {
        const secondShiftId = `#secondShift0_${i}`;
        await expect(page.locator(secondShiftId)).toContainText(planTextsNextWeek[i].secondShift!);
      }

      const cellId = `#cell0_${i}`;
      await page.locator(cellId).scrollIntoViewIfNeeded();
      await page.locator(cellId).click();
      await page.locator('#planHours').waitFor({ state: 'visible', timeout: 15000 });
      await page.waitForTimeout(500);
      await page.locator('#planHours').scrollIntoViewIfNeeded();
      await expect(page.locator('#planHours')).toHaveValue(planTextsNextWeek[i].calculatedHours);
      await expect(page.locator('[data-testid="plannedStartOfShift1"]')).toHaveValue(planTextsNextWeek[i].plannedStartOfShift1);
      await expect(page.locator('[data-testid="plannedBreakOfShift1"]')).toHaveValue(planTextsNextWeek[i].plannedBreakOfShift1);
      await expect(page.locator('[data-testid="plannedEndOfShift1"]')).toHaveValue(planTextsNextWeek[i].plannedEndOfShift1);
      await expect(page.locator('[data-testid="plannedStartOfShift2"]')).toHaveValue(planTextsNextWeek[i].plannedStartOfShift2);
      await expect(page.locator('[data-testid="plannedBreakOfShift2"]')).toHaveValue(planTextsNextWeek[i].plannedBreakOfShift2);
      await expect(page.locator('[data-testid="plannedEndOfShift2"]')).toHaveValue(planTextsNextWeek[i].plannedEndOfShift2);
      await page.locator('#cancelButton').click();
    }

    // Navigate forwards for future week
    const indexUpdatePromise8 = page.waitForResponse(r => r.url().includes('/api/time-planning-pn/plannings/index') && r.request().method() === 'POST');
    await page.locator('#forwards').click();
    await indexUpdatePromise8;
    await waitForSpinner(page);
    await page.waitForTimeout(1000);

    for (let i = 0; i < planTextsFutureWeek.length; i++) {
      if (planTextsFutureWeek[i].firstShift) {
        const firstShiftId = `#firstShift0_${i}`;
        await expect(page.locator(firstShiftId)).toContainText(planTextsFutureWeek[i].firstShift!);
      } else {
        const plannedHoursId = `#plannedHours0_${i}`;
        await expect(page.locator(plannedHoursId)).toContainText(planTextsFutureWeek[i].plannedHours);
      }
      if (planTextsFutureWeek[i].secondShift) {
        const secondShiftId = `#secondShift0_${i}`;
        await expect(page.locator(secondShiftId)).toContainText(planTextsFutureWeek[i].secondShift!);
      }

      const cellId = `#cell0_${i}`;
      await page.locator(cellId).scrollIntoViewIfNeeded();
      await page.locator(cellId).click();
      await page.locator('#planHours').waitFor({ state: 'visible', timeout: 15000 });
      await page.waitForTimeout(500);
      await page.locator('#planHours').scrollIntoViewIfNeeded();
      await expect(page.locator('#planHours')).toHaveValue(planTextsFutureWeek[i].calculatedHours);
      await expect(page.locator('[data-testid="plannedStartOfShift1"]')).toHaveValue(planTextsFutureWeek[i].plannedStartOfShift1);
      await expect(page.locator('[data-testid="plannedBreakOfShift1"]')).toHaveValue(planTextsFutureWeek[i].plannedBreakOfShift1);
      await expect(page.locator('[data-testid="plannedEndOfShift1"]')).toHaveValue(planTextsFutureWeek[i].plannedEndOfShift1);
      await expect(page.locator('[data-testid="plannedStartOfShift2"]')).toHaveValue(planTextsFutureWeek[i].plannedStartOfShift2);
      await expect(page.locator('[data-testid="plannedBreakOfShift2"]')).toHaveValue(planTextsFutureWeek[i].plannedBreakOfShift2);
      await expect(page.locator('[data-testid="plannedEndOfShift2"]')).toHaveValue(planTextsFutureWeek[i].plannedEndOfShift2);
      await page.locator('#cancelButton').click();
    }
  });

  test('should go to dashboard after updating planText to new values and they should change in dashboard', async ({ page }) => {
    const whPage = new TimePlanningWorkingHoursPage(page);
    const pluginPage = new PluginPage(page);

    // Navigate to working hours
    await waitForSpinner(page);
    await page.locator('mat-nested-tree-node').filter({ hasText: 'Timeregistrering' }).click();
    await waitForSpinner(page);
    await page.locator('mat-tree-node').filter({ hasText: 'Timeregistrering' }).click();
    await waitForSpinner(page);
    await page.locator('mat-toolbar > div > button .mat-mdc-button-persistent-ripple').first().locator('..').click();
    await page.locator('#workingHoursSite').locator('input').fill('c d');
    await page.locator('.ng-option.ng-option-marked').click();

    const updatePromise = page.waitForResponse(r => r.url().includes('/api/time-planning-pn/working-hours/index') && r.request().method() === 'POST');
    await whPage.dateFormInput().click();
    await selectDateRangeOnNewDatePicker(
      page,
      filters[0].dateRange.yearFrom, filters[0].dateRange.monthFrom, filters[0].dateRange.dayFrom,
      filters[0].dateRange.yearTo, filters[0].dateRange.monthTo, filters[0].dateRange.dayTo
    );
    await updatePromise;
    await page.locator('.overlay-spinner').waitFor({ state: 'hidden', timeout: 30000 });

    await expect(page.locator('#sumFlex0 input')).toHaveValue(/97\.45/);

    for (let i = 0; i < updatePlanHours.length; i++) {
      const id = `#planHours${i + 1}`;
      await page.locator(id).locator('input').fill(updatePlanHours[i].hours.toString());
      const sumFlexId = `#sumFlex${i + 1}`;
      await expect(page.locator(sumFlexId).locator('input')).toHaveValue(new RegExp(updatePlanHours[i].sumFlex.toString()));
      const nettoHoursId = `#nettoHours${i + 1}`;
      await expect(page.locator(nettoHoursId).locator('input')).toHaveValue(new RegExp(updatePlanHours[i].nettoHours.toString()));
      const flexId = `#flexHours${i + 1}`;
      await expect(page.locator(flexId).locator('input')).toHaveValue(new RegExp(updatePlanHours[i].flex.toString()));
    }

    for (let i = 0; i < updatePlanTexts.length; i++) {
      const id = `#planText${i + 1}`;
      await page.locator(id).locator('input').fill(updatePlanTexts[i].text);
    }

    const savePromise = page.waitForResponse(r => r.url().includes('/api/time-planning-pn/working-hours') && r.request().method() === 'PUT');
    await waitForSpinner(page);
    await page.locator('#workingHoursSave').click();
    await savePromise;
    await page.locator('.overlay-spinner').waitFor({ state: 'hidden', timeout: 30000 });
    await expect(page.locator('#sumFlex7 input')).toHaveValue(/76\.45/);

    // Next week
    await whPage.dateFormInput().click();
    const updatePromise2 = page.waitForResponse(r => r.url().includes('/api/time-planning-pn/working-hours/index') && r.request().method() === 'POST');
    await selectDateRangeOnNewDatePicker(
      page,
      filtersNextWeek[0].dateRange.yearFrom, filtersNextWeek[0].dateRange.monthFrom, filtersNextWeek[0].dateRange.dayFrom,
      filtersNextWeek[0].dateRange.yearTo, filtersNextWeek[0].dateRange.monthTo, filtersNextWeek[0].dateRange.dayTo
    );
    await updatePromise2;
    await page.locator('.overlay-spinner').waitFor({ state: 'hidden', timeout: 30000 });

    await expect(page.locator('#sumFlex0 input')).toHaveValue(/44\.2/);
    await expect(page.locator('#nettoHours0 input')).toHaveValue(/0/);

    for (let i = 0; i < updatePlanHoursNextWeek.length; i++) {
      const id = `#planHours${i + 1}`;
      await page.locator(id).locator('input').fill(updatePlanHoursNextWeek[i].hours.toString());
      const sumFlexId = `#sumFlex${i + 1}`;
      await expect(page.locator(sumFlexId).locator('input')).toHaveValue(new RegExp(updatePlanHoursNextWeek[i].sumFlex.toString()));
      const nettoHoursId = `#nettoHours${i + 1}`;
      await expect(page.locator(nettoHoursId).locator('input')).toHaveValue(new RegExp(updatePlanHoursNextWeek[i].nettoHours.toString()));
      const flexId = `#flexHours${i + 1}`;
      await expect(page.locator(flexId).locator('input')).toHaveValue(new RegExp(updatePlanHoursNextWeek[i].flex.toString()));
    }

    for (let i = 0; i < updatePlanTextsNextWeek.length; i++) {
      const id = `#planText${i + 1}`;
      await page.locator(id).locator('input').fill(updatePlanTextsNextWeek[i].text);
    }

    const savePromise2 = page.waitForResponse(r => r.url().includes('/api/time-planning-pn/working-hours') && r.request().method() === 'PUT');
    await page.locator('#workingHoursSave').click();
    await savePromise2;
    await page.locator('.overlay-spinner').waitFor({ state: 'hidden', timeout: 30000 });
    await expect(page.locator('#sumFlex7 input')).toHaveValue(/12\.2/);

    // Future week
    await whPage.dateFormInput().click();
    const updatePromise4 = page.waitForResponse(r => r.url().includes('/api/time-planning-pn/working-hours/index') && r.request().method() === 'POST');
    await selectDateRangeOnNewDatePicker(
      page,
      filtersFutureWeek[0].dateRange.yearFrom, filtersFutureWeek[0].dateRange.monthFrom, filtersFutureWeek[0].dateRange.dayFrom,
      filtersFutureWeek[0].dateRange.yearTo, filtersFutureWeek[0].dateRange.monthTo, filtersFutureWeek[0].dateRange.dayTo
    );
    await updatePromise4;
    await page.locator('.overlay-spinner').waitFor({ state: 'hidden', timeout: 30000 });

    await expect(page.locator('#nettoHours0 input')).toHaveValue(/0/);

    for (let i = 0; i < updatePlanHoursFutureWeek.length; i++) {
      const id = `#planHours${i + 1}`;
      await page.locator(id).locator('input').fill(updatePlanHoursFutureWeek[i].hours.toString());
      const sumFlexId = `#sumFlex${i + 1}`;
      await expect(page.locator(sumFlexId).locator('input')).toHaveValue(new RegExp(updatePlanHoursFutureWeek[i].sumFlex.toString()));
      const nettoHoursId = `#nettoHours${i + 1}`;
      await expect(page.locator(nettoHoursId).locator('input')).toHaveValue(new RegExp(updatePlanHoursFutureWeek[i].nettoHours.toString()));
      const flexId = `#flexHours${i + 1}`;
      await expect(page.locator(flexId).locator('input')).toHaveValue(new RegExp(updatePlanHoursFutureWeek[i].flex.toString()));
    }

    for (let i = 0; i < updatePlanTextsFutureWeek.length; i++) {
      const id = `#planText${i + 1}`;
      await page.locator(id).locator('input').fill(updatePlanTextsFutureWeek[i].text);
    }

    const savePromise3 = page.waitForResponse(r => r.url().includes('/api/time-planning-pn/working-hours') && r.request().method() === 'PUT');
    await page.locator('#workingHoursSave').click();
    await savePromise3;
    await page.locator('.overlay-spinner').waitFor({ state: 'hidden', timeout: 30000 });
    await page.locator('mat-toolbar > div > button .mat-mdc-button-persistent-ripple').first().locator('..').click();
    await expect(page.locator('#sumFlex7 input')).toHaveValue(/-48\.05/);

    // Go to Dashboard
    const indexUpdatePromise = page.waitForResponse(r => r.url().includes('/api/time-planning-pn/plannings/index') && r.request().method() === 'POST');
    await page.locator('mat-tree-node').filter({ hasText: 'Dashboard' }).click();
    await indexUpdatePromise;
    await waitForSpinner(page);

    await page.locator('#workingHoursSite').locator('input').fill('c d');
    await page.locator('.ng-option.ng-option-marked').click();
    const indexUpdatePromise2 = page.waitForResponse(r => r.url().includes('/api/time-planning-pn/plannings/index') && r.request().method() === 'POST');
    await indexUpdatePromise2;
    await waitForSpinner(page);

    const indexUpdatePromise3 = page.waitForResponse(r => r.url().includes('/api/time-planning-pn/plannings/index') && r.request().method() === 'POST');
    await page.locator('#backwards').click();
    await indexUpdatePromise3;
    await waitForSpinner(page);

    await expect(page.locator('#plannedHours0')).toContainText('53:15');

    for (let i = 0; i < updatePlanTexts.length; i++) {
      const flexBalanceToDateId = `#flexBalanceToDate0_${i}`;
      if (updatePlanTexts[i].flexBalanceToDate !== '') {
        await expect(page.locator(flexBalanceToDateId)).toContainText(updatePlanTexts[i].flexBalanceToDate);
      }

      const cellId = `#cell0_${i}`;
      await page.locator(cellId).scrollIntoViewIfNeeded();
      await page.locator(cellId).click();
      await page.locator('#planHours').waitFor({ state: 'visible', timeout: 15000 });
      await page.waitForTimeout(500);
      await page.locator('#planHours').scrollIntoViewIfNeeded();
      await expect(page.locator('#planHours')).toHaveValue(new RegExp(updatePlanTexts[i].calculatedHours));
      await expect(page.locator('[data-testid="plannedStartOfShift1"]')).toHaveValue(new RegExp(updatePlanTexts[i].plannedStartOfShift1));
      await expect(page.locator('[data-testid="plannedBreakOfShift1"]')).toHaveValue(new RegExp(updatePlanTexts[i].plannedBreakOfShift1));
      await expect(page.locator('[data-testid="plannedEndOfShift1"]')).toHaveValue(new RegExp(updatePlanTexts[i].plannedEndOfShift1));
      await expect(page.locator('[data-testid="plannedStartOfShift2"]')).toHaveValue(new RegExp(updatePlanTexts[i].plannedStartOfShift2));
      await expect(page.locator('[data-testid="plannedBreakOfShift2"]')).toHaveValue(new RegExp(updatePlanTexts[i].plannedBreakOfShift2));
      await expect(page.locator('[data-testid="plannedEndOfShift2"]')).toHaveValue(new RegExp(updatePlanTexts[i].plannedEndOfShift2));
      await page.locator('#cancelButton').click();
      await page.waitForTimeout(500);
    }

    // Navigate forwards twice
    const indexUpdatePromise4 = page.waitForResponse(r => r.url().includes('/api/time-planning-pn/plannings/index') && r.request().method() === 'POST');
    await page.locator('#forwards').click();
    await indexUpdatePromise4;
    await waitForSpinner(page);
    await page.waitForTimeout(500);

    const indexUpdatePromise5 = page.waitForResponse(r => r.url().includes('/api/time-planning-pn/plannings/index') && r.request().method() === 'POST');
    await page.locator('#forwards').click();
    await indexUpdatePromise5;
    await waitForSpinner(page);
    await page.waitForTimeout(500);

    for (let i = 0; i < updatePlanTextsNextWeek.length; i++) {
      const firstShiftId = `#firstShift0_${i}`;
      const cellId = `#cell0_${i}`;
      await page.locator(cellId).scrollIntoViewIfNeeded();
      await page.locator(firstShiftId).scrollIntoViewIfNeeded();
      await expect(page.locator(firstShiftId)).toContainText(updatePlanTextsNextWeek[i].firstShift!);
      if (planTextsNextWeek[i].secondShift) {
        const secondShiftId = `#secondShift0_${i}`;
        await expect(page.locator(secondShiftId)).toContainText(updatePlanTextsNextWeek[i].secondShift!);
      }

      await page.locator(cellId).click();
      await page.locator('#planHours').waitFor({ state: 'visible', timeout: 15000 });
      await page.waitForTimeout(500);
      await page.locator('#planHours').scrollIntoViewIfNeeded();
      await expect(page.locator('#planHours')).toHaveValue(new RegExp(updatePlanTextsNextWeek[i].calculatedHours));
      await expect(page.locator('[data-testid="plannedStartOfShift1"]')).toHaveValue(new RegExp(updatePlanTextsNextWeek[i].plannedStartOfShift1));
      await expect(page.locator('[data-testid="plannedBreakOfShift1"]')).toHaveValue(new RegExp(updatePlanTextsNextWeek[i].plannedBreakOfShift1));
      await expect(page.locator('[data-testid="plannedEndOfShift1"]')).toHaveValue(new RegExp(updatePlanTextsNextWeek[i].plannedEndOfShift1));
      await expect(page.locator('[data-testid="plannedStartOfShift2"]')).toHaveValue(new RegExp(updatePlanTextsNextWeek[i].plannedStartOfShift2));
      await expect(page.locator('[data-testid="plannedBreakOfShift2"]')).toHaveValue(new RegExp(updatePlanTextsNextWeek[i].plannedBreakOfShift2));
      await expect(page.locator('[data-testid="plannedEndOfShift2"]')).toHaveValue(new RegExp(updatePlanTextsNextWeek[i].plannedEndOfShift2));
      await page.locator('#cancelButton').click();
    }

    // Navigate forwards for future week
    const indexUpdatePromise6 = page.waitForResponse(r => r.url().includes('/api/time-planning-pn/plannings/index') && r.request().method() === 'POST');
    await page.locator('#forwards').click();
    await indexUpdatePromise6;
    await waitForSpinner(page);
    await page.waitForTimeout(500);

    for (let i = 0; i < updatePlanTextsFutureWeek.length; i++) {
      const cellId = `#cell0_${i}`;
      await page.locator(cellId).scrollIntoViewIfNeeded();
      if (planTextsFutureWeek[i].firstShift) {
        const firstShiftId = `#firstShift0_${i}`;
        await expect(page.locator(firstShiftId)).toContainText(updatePlanTextsFutureWeek[i].firstShift!);
      } else {
        if (updatePlanTextsFutureWeek[i].plannedHours !== '') {
          const plannedHoursId = `#plannedHours0_${i}`;
          await expect(page.locator(plannedHoursId)).toContainText(updatePlanTextsFutureWeek[i].plannedHours);
        }
      }
      if (planTextsFutureWeek[i].secondShift) {
        const secondShiftId = `#secondShift0_${i}`;
        await expect(page.locator(secondShiftId)).toContainText(updatePlanTextsFutureWeek[i].secondShift!);
      }
      await page.locator(cellId).click();
      await page.locator('#planHours').waitFor({ state: 'visible', timeout: 15000 });
      await page.waitForTimeout(500);
      await page.locator('#planHours').scrollIntoViewIfNeeded();
      await expect(page.locator('#planHours')).toHaveValue(new RegExp(updatePlanTextsFutureWeek[i].calculatedHours));
      await expect(page.locator('[data-testid="plannedStartOfShift1"]')).toHaveValue(new RegExp(updatePlanTextsFutureWeek[i].plannedStartOfShift1));
      await expect(page.locator('[data-testid="plannedBreakOfShift1"]')).toHaveValue(new RegExp(updatePlanTextsFutureWeek[i].plannedBreakOfShift1));
      await expect(page.locator('[data-testid="plannedEndOfShift1"]')).toHaveValue(new RegExp(updatePlanTextsFutureWeek[i].plannedEndOfShift1));
      await expect(page.locator('[data-testid="plannedStartOfShift2"]')).toHaveValue(new RegExp(updatePlanTextsFutureWeek[i].plannedStartOfShift2));
      await expect(page.locator('[data-testid="plannedBreakOfShift2"]')).toHaveValue(new RegExp(updatePlanTextsFutureWeek[i].plannedBreakOfShift2));
      await expect(page.locator('[data-testid="plannedEndOfShift2"]')).toHaveValue(new RegExp(updatePlanTextsFutureWeek[i].plannedEndOfShift2));
      await page.locator('#cancelButton').click();
    }
  });
});
