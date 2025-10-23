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
  { date: lastWeekDates[0], hours: 1, sumFlex: 96.45, nettoHours: 0, flex: -1, humanFlex: '96:27'},
  { date: lastWeekDates[1], hours: 2, sumFlex: 94.45, nettoHours: 0, flex: -2, humanFlex: '94:27'},
  { date: lastWeekDates[2], hours: 3, sumFlex: 91.45, nettoHours: 0, flex: -3, humanFlex: '91:27'},
  { date: lastWeekDates[3], hours: 0, sumFlex: 91.45, nettoHours: 0, flex: 0, humanFlex: '91:27'},
  { date: lastWeekDates[4], hours: 4, sumFlex: 87.45, nettoHours: 0, flex: -4, humanFlex: '87:27'},
  { date: lastWeekDates[5], hours: 5, sumFlex: 82.45, nettoHours: 0, flex: -5, humanFlex: '82:27'},
  { date: lastWeekDates[6], hours: 6, sumFlex: 76.45, nettoHours: 0, flex: -6, humanFlex: '76:27'},
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
  { date: nextWeekDates[0], hours: 8, sumFlex: 36.2, nettoHours: 0, flex: -8, humanFlex: '68:27'},
  { date: nextWeekDates[1], hours: 8, sumFlex: 28.2, nettoHours: 0, flex: -8, humanFlex: '60:27'},
  { date: nextWeekDates[2], hours: 0, sumFlex: 28.2, nettoHours: 0, flex: 0, humanFlex: '60:27'},
  { date: nextWeekDates[3], hours: 0, sumFlex: 28.2, nettoHours: 0, flex: 0, humanFlex: '60:27'},
  { date: nextWeekDates[4], hours: 0, sumFlex: 28.2, nettoHours: 0, flex: 0, humanFlex: '60:27'},
  { date: nextWeekDates[5], hours: 8, sumFlex: 20.2, nettoHours: 0, flex: -8, humanFlex: '52:27'},
  { date: nextWeekDates[6], hours: 8, sumFlex: 12.2, nettoHours: 0, flex: -8, humanFlex: '44:27'},
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
  { date: futureWeekDates[0], hours: 2, sumFlex: -11.05, nettoHours: 0, flex: -2, humanFlex: '42:27'},
  { date: futureWeekDates[1], hours: 4, sumFlex: -15.05, nettoHours: 0, flex: -4, humanFlex: '38:27'},
  { date: futureWeekDates[2], hours: 0, sumFlex: -15.05, nettoHours: 0, flex: 0, humanFlex: '38:27'},
  { date: futureWeekDates[3], hours: 10, sumFlex: -25.05, nettoHours: 0, flex: -10, humanFlex: '28:27'},
  { date: futureWeekDates[4], hours: 12, sumFlex: -37.05, nettoHours: 0, flex: -12, humanFlex: '16:27'},
  { date: futureWeekDates[5], hours: 3, sumFlex: -40.05, nettoHours: 0, flex: -3, humanFlex: '13:27'},
  { date: futureWeekDates[6], hours: 8, sumFlex: -48.05, nettoHours: 0, flex: -8, humanFlex:' 5:27'},
]

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


describe('Dashboard assert', () => {
  beforeEach(() => {
    cy.visit('http://localhost:4200');
    loginPage.login();
  });

  // it('should go to dashboard', () => {
  //   cy.get('mat-nested-tree-node').contains('Timeregistrering').click();
  //   cy.get('mat-tree-node').contains('Timeregistrering').click();
  //   cy.get('mat-toolbar > button .mat-mdc-button-persistent-ripple').parent().click();
  //   cy.get('#workingHoursSite').clear().type('c d');
  //   cy.get('.ng-option.ng-option-marked').click();
  //   cy.intercept('POST', '**/api/time-planning-pn/working-hours/index').as('update');
  //   TimePlanningWorkingHoursPage.dateFormInput().click();
  //   selectDateRangeOnNewDatePicker(
  //     filters[0].dateRange.yearFrom, filters[0].dateRange.monthFrom,  filters[0].dateRange.dayFrom,
  //     filters[0].dateRange.yearTo, filters[0].dateRange.monthTo, filters[0].dateRange.dayTo
  //   );
  //   cy.wait('@update');
  //   cy.get('#sumFlex0 input').should('contain.value', '97.45');
  //   for (let i = 0; i < planHours.length; i++) {
  //     let id = `#planHours${i+1}`;
  //     cy.get(id).find('input').clear().type(planHours[i].hours.toString());
  //     let sumFlexId = `#sumFlex${i+1}`;
  //     cy.get(sumFlexId).find('input').should('contain.value', planHours[i].sumFlex.toString());
  //     let nettoHoursId = `#nettoHours${i+1}`;
  //     cy.get(nettoHoursId).find('input').should('contain.value', planHours[i].nettoHours.toString());
  //     let flexId = `#flexHours${i+1}`;
  //     cy.get(flexId).find('input').should('contain.value', planHours[i].flex.toString());
  //   }
  //   for (let i = 0; i < planTexts.length; i++) {
  //     let id = `#planText${i+1}`;
  //     cy.get(id).find('input').clear().type(planTexts[i].text);
  //   }
  //
  //   cy.intercept('PUT', '**/api/time-planning-pn/working-hours').as('save');
  //   cy.get('#workingHoursSave').click();
  //   cy.wait('@save');
  //   cy.get('#sumFlex7 input').should('contain.value', '41.45');
  //
  //   cy.intercept('POST', '**/api/time-planning-pn/working-hours/index').as('update');
  //   TimePlanningWorkingHoursPage.dateFormInput().click();
  //   selectDateRangeOnNewDatePicker(
  //     filtersNextWeek[0].dateRange.yearFrom, filtersNextWeek[0].dateRange.monthFrom,  filtersNextWeek[0].dateRange.dayFrom,
  //     filtersNextWeek[0].dateRange.yearTo, filtersNextWeek[0].dateRange.monthTo, filtersNextWeek[0].dateRange.dayTo
  //   );
  //   cy.wait('@update');
  //   cy.get('#sumFlex0 input').should('contain.value', '41.45');
  //   cy.get('#nettoHours0 input').should('contain.value', '0');
  //
  //   for (let i = 0; i < planHoursNextWeek.length; i++) {
  //     let id = `#planHours${i+1}`;
  //     cy.get(id).find('input').clear().type(planHoursNextWeek[i].hours.toString());
  //     let sumFlexId = `#sumFlex${i+1}`;
  //     cy.get(sumFlexId).find('input').should('contain.value', planHoursNextWeek[i].sumFlex.toString());
  //     let nettoHoursId = `#nettoHours${i+1}`;
  //     cy.get(nettoHoursId).find('input').should('contain.value', planHoursNextWeek[i].nettoHours.toString());
  //     let flexId = `#flexHours${i+1}`;
  //     cy.get(flexId).find('input').should('contain.value', planHoursNextWeek[i].flex.toString());
  //   }
  //   for (let i = 0; i < planTextsNextWeek.length; i++) {
  //     let id = `#planText${i+1}`;
  //     cy.get(id).find('input').clear().type(planTextsNextWeek[i].text);
  //   }
  //
  //   cy.intercept('PUT', '**/api/time-planning-pn/working-hours').as('save');
  //   cy.get('#workingHoursSave').click();
  //   cy.wait('@save');
  //   cy.get('#sumFlex7 input').should('contain.value', '-14.55');
  //
  //   cy.get('mat-toolbar > button .mat-mdc-button-persistent-ripple').parent().click();
  //   pluginPage.Navbar.goToPluginsPage();
  //   const pluginName = 'Microting Time Planning Plugin';
  //   // pluginPage.enablePluginByName(pluginName);
  //   let row = cy.contains('.mat-mdc-row', pluginName).first();
  //   row.find('.mat-column-actions button')
  //     .should('contain.text', 'toggle_on'); // plugin is enabled
  //   row = cy.contains('.mat-mdc-row', pluginName).first();
  //   row.find('.mat-column-actions a')
  //     .should('contain.text', 'settings'); // plugin is enabled
  //   row = cy.contains('.mat-mdc-row', pluginName).first();
  //   let settingsElement = row
  //     .find('.mat-column-actions a')
  //     // .should('be.enabled')
  //     .should('be.visible');
  //   settingsElement.click();
  //   cy.get('#forceLoadAllPlanningsFromGoogleSheet').click();
  //   cy.get('#saveSettings').click();
  //   cy.intercept('POST', '**/api/time-planning-pn/plannings/index').as('index-update');
  //   cy.get('mat-tree-node').contains('Dashboard').click();
  //   cy.wait('@index-update', { timeout: 60000 });
  //   cy.get('#backwards').click();
  //   cy.wait('@index-update', { timeout: 60000 });
  //   cy.get('#plannedHours3').should('include.text', '56:00');
  //
  //   for (let i = 0; i < planTexts.length; i++) {
  //     let plannedHoursId = `#plannedHours0_${i}`;
  //     cy.get(plannedHoursId).should('include.text', planTexts[i].plannedHours);
  //     let flexBalanceToDateId = `#flexBalanceToDate0_${i}`;
  //     cy.get(flexBalanceToDateId).should('include.text', planTexts[i].flexBalanceToDate);
  //
  //     let cellId = `#cell0_${i}`;
  //     cy.get(cellId).click();
  //     cy.get('#planHours').should('be.visible');
  //     cy.get('#planHours').should('include.value', planTexts[i].calculatedHours);
  //     cy.get('#plannedStartOfShift1').should('include.value', planTexts[i].plannedStartOfShift1);
  //     cy.get('#plannedBreakOfShift1').should('include.value', planTexts[i].plannedBreakOfShift1);
  //     cy.get('#plannedEndOfShift1').should('include.value', planTexts[i].plannedEndOfShift1);
  //     cy.get('#plannedStartOfShift2').should('include.value', planTexts[i].plannedStartOfShift2);
  //     cy.get('#plannedBreakOfShift2').should('include.value', planTexts[i].plannedBreakOfShift2);
  //     cy.get('#plannedEndOfShift2').should('include.value', planTexts[i].plannedEndOfShift2);
  //     cy.get('#cancelButton').click();
  //   }
  //
  //   cy.get('#forwards').click();
  //   cy.wait('@index-update', { timeout: 60000 });
  //   cy.wait(1000);
  //
  //
  //   for (let i = 0; i < planTextsNextWeek.length; i++) {
  //
  //     let cellId = `#cell0_${i}`;
  //     cy.get(cellId).click();
  //     cy.get('#planHours').should('be.visible');
  //     cy.get('#planHours').should('include.value', 0);
  //     cy.get('#plannedStartOfShift1').should('include.value', '00:00');
  //     cy.get('#plannedBreakOfShift1').should('include.value', '00:00');
  //     cy.get('#plannedEndOfShift1').should('include.value', '00:00');
  //     cy.get('#plannedStartOfShift2').should('include.value', '00:00');
  //     cy.get('#plannedBreakOfShift2').should('include.value', '00:00');
  //     cy.get('#plannedEndOfShift2').should('include.value', '00:00');
  //     cy.get('#cancelButton').click();
  //   }
  // });

  it('should go to dashboard and set to use google sheet as default and check if the settings are correct', () => {
    cy.get('mat-nested-tree-node').contains('Timeregistrering').click();
    cy.intercept('POST', '**/api/time-planning-pn/plannings/index').as('index-update');
    cy.get('mat-tree-node').contains('Dashboard').click();
    cy.wait('@index-update', { timeout: 60000 });
    cy.get('#firstColumn3').click();
    cy.get('#useGoogleSheetAsDefault').click();
    cy.get('#saveButton').click();
    cy.wait('@index-update', { timeout: 160000 });
    cy.wait(1000);
    cy.get('mat-tree-node').contains('Timeregistrering').click();
    cy.get('mat-toolbar > button .mat-mdc-button-persistent-ripple').parent().click();
    cy.get('#workingHoursSite').clear().type('c d');
    cy.get('.ng-option.ng-option-marked').click();

    cy.intercept('POST', '**/api/time-planning-pn/working-hours/index').as('update');
    TimePlanningWorkingHoursPage.dateFormInput().click();
    selectDateRangeOnNewDatePicker(
      filters[0].dateRange.yearFrom, filters[0].dateRange.monthFrom,  filters[0].dateRange.dayFrom,
      filters[0].dateRange.yearTo, filters[0].dateRange.monthTo, filters[0].dateRange.dayTo
    );
    cy.wait('@update');
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
    cy.intercept('PUT', '**/api/time-planning-pn/working-hours').as('save');
    cy.get('#workingHoursSave').click();
    cy.wait('@save');
    cy.get('#sumFlex7 input').should('contain.value', '41.45');

    cy.intercept('POST', '**/api/time-planning-pn/working-hours/index').as('update');
    TimePlanningWorkingHoursPage.dateFormInput().click();
    selectDateRangeOnNewDatePicker(
      filtersNextWeek[0].dateRange.yearFrom, filtersNextWeek[0].dateRange.monthFrom,  filtersNextWeek[0].dateRange.dayFrom,
      filtersNextWeek[0].dateRange.yearTo, filtersNextWeek[0].dateRange.monthTo, filtersNextWeek[0].dateRange.dayTo
    );
    cy.wait('@update');

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
    cy.intercept('PUT', '**/api/time-planning-pn/working-hours').as('save');
    cy.get('#workingHoursSave').click();
    cy.wait('@save');
    cy.get('#sumFlex7 input').should('contain.value', '-14.55');

    cy.intercept('POST', '**/api/time-planning-pn/working-hours/index').as('update');
    TimePlanningWorkingHoursPage.dateFormInput().click();
    selectDateRangeOnNewDatePicker(
      filtersFutureWeek[0].dateRange.yearFrom, filtersFutureWeek[0].dateRange.monthFrom,  filtersFutureWeek[0].dateRange.dayFrom,
      filtersFutureWeek[0].dateRange.yearTo, filtersFutureWeek[0].dateRange.monthTo, filtersFutureWeek[0].dateRange.dayTo
    );
    cy.wait('@update');

    cy.get('#nettoHours0 input').should('contain.value', '0');
    for (let i = 0; i < planHoursFutureWeek.length; i++) {
      cy.wait(1000);
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

    cy.intercept('PUT', '**/api/time-planning-pn/working-hours').as('save');
    cy.get('#workingHoursSave').click();
    cy.wait('@save');
    cy.get('mat-toolbar > button .mat-mdc-button-persistent-ripple').parent().click();
    cy.get('#sumFlex7 input').should('contain.value', '-78.55');
    pluginPage.Navbar.goToPluginsPage();
    const pluginName = 'Microting Time Planning Plugin';

    let row = cy.contains('.mat-mdc-row', pluginName).first();
    row.find('.mat-column-actions button')
      .should('contain.text', 'toggle_on'); // plugin is enabled
    row = cy.contains('.mat-mdc-row', pluginName).first();
    row.find('.mat-column-actions a')

      .should('contain.text', 'settings'); // plugin is enabled
    row = cy.contains('.mat-mdc-row', pluginName).first();
    let settingsElement = row
      .find('.mat-column-actions a')

      .should('be.visible');

    settingsElement.click();
    cy.get('#forceLoadAllPlanningsFromGoogleSheet').click();
    cy.get('#saveSettings').click();
    cy.get('mat-tree-node').contains('Dashboard').click();
    cy.wait('@index-update', { timeout: 60000 });
    cy.get('mat-toolbar > button .mat-mdc-button-persistent-ripple').parent().click();

    cy.get('#workingHoursSite').clear().type('c d');
    cy.get('.ng-option.ng-option-marked').click();
    cy.wait('@index-update', { timeout: 60000 });
    cy.get('#backwards').click();
    cy.wait('@index-update', { timeout: 60000 });
    cy.get('#plannedHours3').should('include.text', '53:15');
    for (let i = 0; i < planTexts.length; i++) {
      let plannedHoursId = `#plannedHours0_${i}`;
      // cy.get(plannedHoursId).should('include.text', planTexts[i].plannedHours);
      let flexBalanceToDateId = `#flexBalanceToDate0_${i}`;
      cy.get(flexBalanceToDateId).should('include.text', planTexts[i].flexBalanceToDate);

      let cellId = `#cell0_${i}`;
      cy.get(cellId).scrollIntoView();
      cy.get(cellId).click();
      cy.get('#planHours').should('be.visible');
      cy.get('#planHours').should('have.value', planTexts[i].calculatedHours);
      cy.get('#plannedStartOfShift1').should('have.value', planTexts[i].plannedStartOfShift1);
      cy.get('#plannedBreakOfShift1').should('have.value', planTexts[i].plannedBreakOfShift1);
      cy.get('#plannedEndOfShift1').should('have.value', planTexts[i].plannedEndOfShift1);
      cy.get('#plannedStartOfShift2').should('have.value', planTexts[i].plannedStartOfShift2);
      cy.get('#plannedBreakOfShift2').should('have.value', planTexts[i].plannedBreakOfShift2);
      cy.get('#plannedEndOfShift2').should('have.value', planTexts[i].plannedEndOfShift2);
      cy.get('#cancelButton').click();
      cy.wait(500);
    }

    cy.get('#forwards').click();
    cy.wait('@index-update', { timeout: 60000 });
    cy.wait(1000);
    cy.get('#forwards').click();
    cy.wait('@index-update', { timeout: 60000 });
    cy.wait(1000);
    for (let i = 0; i < planTextsNextWeek.length; i++) {
      let firstShiftId = `#firstShift0_${i}`;
      cy.get(firstShiftId).should('include.text', planTextsNextWeek[i].firstShift);
      if (planTextsNextWeek[i].secondShift) {
        let secondShiftId = `#secondShift0_${i}`;
        cy.get(secondShiftId).should('include.text', planTextsNextWeek[i].secondShift);
      }

      let cellId = `#cell0_${i}`;
      cy.get(cellId).scrollIntoView();
      cy.get(cellId).click();
      cy.get('#planHours').should('be.visible');
      cy.get('#planHours').should('have.value', planTextsNextWeek[i].calculatedHours);
      cy.get('#plannedStartOfShift1').should('have.value', planTextsNextWeek[i].plannedStartOfShift1);
      cy.get('#plannedBreakOfShift1').should('have.value', planTextsNextWeek[i].plannedBreakOfShift1);
      cy.get('#plannedEndOfShift1').should('have.value', planTextsNextWeek[i].plannedEndOfShift1);
      cy.get('#plannedStartOfShift2').should('have.value', planTextsNextWeek[i].plannedStartOfShift2);
      cy.get('#plannedBreakOfShift2').should('have.value', planTextsNextWeek[i].plannedBreakOfShift2);
      cy.get('#plannedEndOfShift2').should('have.value', planTextsNextWeek[i].plannedEndOfShift2);
      cy.get('#cancelButton').click();
    }

    cy.get('#forwards').click();
    cy.wait('@index-update', { timeout: 60000 });
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

      let cellId = `#cell0_${i}`;
      cy.get(cellId).scrollIntoView();
      cy.get(cellId).click();
      cy.get('#planHours').should('be.visible');
      cy.get('#planHours').should('have.value', planTextsFutureWeek[i].calculatedHours);
      cy.get('#plannedStartOfShift1').should('have.value', planTextsFutureWeek[i].plannedStartOfShift1);
      cy.get('#plannedBreakOfShift1').should('have.value', planTextsFutureWeek[i].plannedBreakOfShift1);
      cy.get('#plannedEndOfShift1').should('have.value', planTextsFutureWeek[i].plannedEndOfShift1);
      cy.get('#plannedStartOfShift2').should('have.value', planTextsFutureWeek[i].plannedStartOfShift2);
      cy.get('#plannedBreakOfShift2').should('have.value', planTextsFutureWeek[i].plannedBreakOfShift2);
      cy.get('#plannedEndOfShift2').should('have.value', planTextsFutureWeek[i].plannedEndOfShift2);
      cy.get('#cancelButton').click();
    }
  });

  it('should go to dashboard after updating planText to new values and they should change in dashboard', () => {
    cy.get('mat-nested-tree-node').contains('Timeregistrering').click();
    cy.get('mat-tree-node').contains('Timeregistrering').click();
    cy.get('mat-toolbar > button .mat-mdc-button-persistent-ripple').parent().click();
    cy.get('#workingHoursSite').clear().type('c d');
    cy.get('.ng-option.ng-option-marked').click();
    cy.intercept('POST', '**/api/time-planning-pn/working-hours/index').as('update');
    TimePlanningWorkingHoursPage.dateFormInput().click();
    selectDateRangeOnNewDatePicker(
      filters[0].dateRange.yearFrom, filters[0].dateRange.monthFrom,  filters[0].dateRange.dayFrom,
      filters[0].dateRange.yearTo, filters[0].dateRange.monthTo, filters[0].dateRange.dayTo
    );
    cy.wait('@update');
    cy.get('#sumFlex0 input').should('contain.value', '97.45');
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
    cy.intercept('PUT', '**/api/time-planning-pn/working-hours').as('save');
    cy.get('#workingHoursSave').click();
    cy.wait('@save');
    cy.get('#sumFlex7 input').should('contain.value', '76.45');

    cy.intercept('POST', '**/api/time-planning-pn/working-hours/index').as('update');
    TimePlanningWorkingHoursPage.dateFormInput().click();
    selectDateRangeOnNewDatePicker(
      filtersNextWeek[0].dateRange.yearFrom, filtersNextWeek[0].dateRange.monthFrom,  filtersNextWeek[0].dateRange.dayFrom,
      filtersNextWeek[0].dateRange.yearTo, filtersNextWeek[0].dateRange.monthTo, filtersNextWeek[0].dateRange.dayTo
    );
    cy.wait('@update');

    cy.get('#sumFlex0 input').should('contain.value', '44.2');
    cy.get('#nettoHours0 input').should('contain.value', '0');

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

    cy.intercept('PUT', '**/api/time-planning-pn/working-hours').as('save');
    cy.get('#workingHoursSave').click();
    cy.wait('@save');
    cy.get('#sumFlex7 input').should('contain.value', '12.2');

    cy.intercept('POST', '**/api/time-planning-pn/working-hours/index').as('update');
    TimePlanningWorkingHoursPage.dateFormInput().click();
    selectDateRangeOnNewDatePicker(
      filtersFutureWeek[0].dateRange.yearFrom, filtersFutureWeek[0].dateRange.monthFrom,  filtersFutureWeek[0].dateRange.dayFrom,
      filtersFutureWeek[0].dateRange.yearTo, filtersFutureWeek[0].dateRange.monthTo, filtersFutureWeek[0].dateRange.dayTo
    );
    cy.wait('@update');


    cy.get('#nettoHours0 input').should('contain.value', '0');
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

    cy.intercept('PUT', '**/api/time-planning-pn/working-hours').as('save');
    cy.get('#workingHoursSave').click();
    cy.wait('@save');
    cy.get('mat-toolbar > button .mat-mdc-button-persistent-ripple').parent().click();
    cy.get('#sumFlex7 input').should('contain.value', '-48.05');



    cy.intercept('POST', '**/api/time-planning-pn/plannings/index').as('index-update');
    cy.get('mat-tree-node').contains('Dashboard').click();
    cy.wait('@index-update', { timeout: 60000 });
    cy.get('#workingHoursSite').clear().type('c d');
    cy.get('.ng-option.ng-option-marked').click();
    cy.wait('@index-update', { timeout: 60000 });
    cy.get('#backwards').click();
    cy.wait('@index-update', { timeout: 60000 });
    cy.get('#plannedHours3').should('include.text', '53:15');
    for (let i = 0; i < updatePlanTexts.length; i++) {
      // let plannedHoursId = `#plannedHours0_${i}`;
      // if (updatePlanTexts[i].plannedHours !== '') {
      //   cy.get(plannedHoursId).should('include.text', updatePlanTexts[i].plannedHours);
      // }
      let flexBalanceToDateId = `#flexBalanceToDate0_${i}`;
      if (updatePlanTexts[i].flexBalanceToDate !== '') {
        cy.get(flexBalanceToDateId).should('include.text', updatePlanTexts[i].flexBalanceToDate);
      }

      let cellId = `#cell0_${i}`;
      cy.get(cellId).scrollIntoView();
      cy.get(cellId).click();
      cy.get('#planHours').should('be.visible');
      cy.get('#planHours').should('include.value', updatePlanTexts[i].calculatedHours);
      cy.get('#plannedStartOfShift1').should('include.value', updatePlanTexts[i].plannedStartOfShift1);
      cy.get('#plannedBreakOfShift1').should('include.value', updatePlanTexts[i].plannedBreakOfShift1);
      cy.get('#plannedEndOfShift1').should('include.value', updatePlanTexts[i].plannedEndOfShift1);
      cy.get('#plannedStartOfShift2').should('include.value', updatePlanTexts[i].plannedStartOfShift2);
      cy.get('#plannedBreakOfShift2').should('include.value', updatePlanTexts[i].plannedBreakOfShift2);
      cy.get('#plannedEndOfShift2').should('include.value', updatePlanTexts[i].plannedEndOfShift2);
      cy.get('#cancelButton').click();
      cy.wait(500);
    }

    cy.get('#forwards').click();
    cy.wait('@index-update', { timeout: 60000 });
    cy.wait(500);
    cy.get('#forwards').click();
    cy.wait('@index-update', { timeout: 60000 });
    cy.wait(500);
    for (let i = 0; i < updatePlanTextsNextWeek.length; i++) {
      let firstShiftId = `#firstShift0_${i}`;
      cy.get(firstShiftId).should('include.text', updatePlanTextsNextWeek[i].firstShift);
      if (planTextsNextWeek[i].secondShift) {
        let secondShiftId = `#secondShift0_${i}`;
        cy.get(secondShiftId).should('include.text', updatePlanTextsNextWeek[i].secondShift);
      }

      let cellId = `#cell0_${i}`;
      cy.get(cellId).scrollIntoView();
      cy.get(cellId).click();
      cy.get('#planHours').should('be.visible');
      cy.get('#planHours').should('include.value', updatePlanTextsNextWeek[i].calculatedHours);
      cy.get('#plannedStartOfShift1').should('include.value', updatePlanTextsNextWeek[i].plannedStartOfShift1);
      cy.get('#plannedBreakOfShift1').should('include.value', updatePlanTextsNextWeek[i].plannedBreakOfShift1);
      cy.get('#plannedEndOfShift1').should('include.value', updatePlanTextsNextWeek[i].plannedEndOfShift1);
      cy.get('#plannedStartOfShift2').should('include.value', updatePlanTextsNextWeek[i].plannedStartOfShift2);
      cy.get('#plannedBreakOfShift2').should('include.value', updatePlanTextsNextWeek[i].plannedBreakOfShift2);
      cy.get('#plannedEndOfShift2').should('include.value', updatePlanTextsNextWeek[i].plannedEndOfShift2);
      cy.get('#cancelButton').click();
    }
    cy.get('#forwards').click();
    cy.wait('@index-update', { timeout: 60000 });
    cy.wait(500);
    for (let i = 0; i < updatePlanTextsFutureWeek.length; i++) {
      if (planTextsFutureWeek[i].firstShift) {
        let firstShiftId = `#firstShift0_${i}`;
        cy.get(firstShiftId).should('include.text', updatePlanTextsFutureWeek[i].firstShift);
      } else {
        if (updatePlanTextsFutureWeek[i].plannedHours !== '') {
          let plannedHoursId = `#plannedHours0_${i}`;
          cy.get(plannedHoursId).should('include.text', updatePlanTextsFutureWeek[i].plannedHours);
        }
      }
      if (planTextsFutureWeek[i].secondShift) {
        let secondShiftId = `#secondShift0_${i}`;
        cy.get(secondShiftId).should('include.text', updatePlanTextsFutureWeek[i].secondShift);
      }
      let cellId = `#cell0_${i}`;
      cy.get(cellId).scrollIntoView();
      cy.get(cellId).click();
      cy.get('#planHours').should('be.visible');
      cy.get('#planHours').should('include.value', updatePlanTextsFutureWeek[i].calculatedHours);
      cy.get('#plannedStartOfShift1').should('include.value', updatePlanTextsFutureWeek[i].plannedStartOfShift1);
      cy.get('#plannedBreakOfShift1').should('include.value', updatePlanTextsFutureWeek[i].plannedBreakOfShift1);
      cy.get('#plannedEndOfShift1').should('include.value', updatePlanTextsFutureWeek[i].plannedEndOfShift1);
      cy.get('#plannedStartOfShift2').should('include.value', updatePlanTextsFutureWeek[i].plannedStartOfShift2);
      cy.get('#plannedBreakOfShift2').should('include.value', updatePlanTextsFutureWeek[i].plannedBreakOfShift2);
      cy.get('#plannedEndOfShift2').should('include.value', updatePlanTextsFutureWeek[i].plannedEndOfShift2);
      cy.get('#cancelButton').click();
    }
  });
});
