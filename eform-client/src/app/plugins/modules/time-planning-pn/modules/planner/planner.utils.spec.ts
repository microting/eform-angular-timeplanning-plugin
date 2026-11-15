import {describe, it, expect} from 'vitest';
import {TranslateService} from '@ngx-translate/core';
import {
  dateKey, dateKeysBetween, formatPlannerHours, fullPatternGrid, isoDayOfWeek, isoWeekNumber, isValidPattern,
  keyToTransportDate, mondayOf, parseCalendarDate, shortFormKind, toTransportDate, trimmedPatternDays,
} from './planner.utils';
import {importReasonKey} from './components/planner-import-dialog/planner-import-dialog.component';
import {absenceTypeKey, PLANNER_ABSENCE_TYPES} from './planner-absence-types';
import {da} from '../../i18n/da';
import {enUS} from '../../i18n/enUS';

describe('planner short form', () => {
  it('reads the forms the server accepts', () => {
    expect(shortFormKind('7-15/0,5')).toBe('plan');
    expect(shortFormKind('7:00-15:00/0.5')).toBe('plan');
    expect(shortFormKind('6-10;16-20')).toBe('plan');
    expect(shortFormKind(' 7.30 - 15½ / ½ ')).toBe('plan');
    expect(shortFormKind('Fri')).toBe('dayOff');
    expect(shortFormKind('fri')).toBe('dayOff');
    expect(shortFormKind('')).toBe('clear');
    expect(shortFormKind('   ')).toBe('clear');
    expect(shortFormKind(null)).toBe('clear');
  });

  it('refuses text that is not wholly shifts, as the server does', () => {
    expect(shortFormKind('7-15 syg')).toBe('invalid');
    expect(shortFormKind('7-15/')).toBe('invalid');
    expect(shortFormKind('syg')).toBe('invalid');
    expect(shortFormKind('1-2;3-4;5-6;7-8;9-10;11-12')).toBe('invalid');
  });
});

describe('planner dates', () => {
  it('reads a server date by its calendar date, whatever the clock', () => {
    const date = parseCalendarDate('2026-11-09T00:00:00');
    expect([date.getFullYear(), date.getMonth(), date.getDate()]).toEqual([2026, 10, 9]);
    expect(dateKey('2026-11-09T00:00:00')).toBe('2026-11-09');
  });

  it('sends a calendar date as UTC midnight of that date', () => {
    expect(toTransportDate(new Date(2026, 10, 9))).toBe('2026-11-09T00:00:00.000Z');
    expect(keyToTransportDate('2026-11-09')).toBe('2026-11-09T00:00:00.000Z');
  });

  it('counts ISO weekdays and weeks from Monday', () => {
    expect(isoDayOfWeek(new Date(2026, 10, 9))).toBe(0);
    expect(isoDayOfWeek(new Date(2026, 10, 15))).toBe(6);
    expect(mondayOf(new Date(2026, 10, 15)).getDate()).toBe(9);
    expect(isoWeekNumber(new Date(2026, 10, 9))).toBe(46);
    expect(isoWeekNumber(new Date(2027, 0, 1))).toBe(53);
  });
});

describe('planner hours', () => {
  const translate = (lang: string) => ({currentLang: lang, defaultLang: 'da'}) as TranslateService;

  it('formats hours in the user\'s language with at most 2 decimals', () => {
    expect(formatPlannerHours(translate('da'), 37.5)).toBe('37,5');
    expect(formatPlannerHours(translate('en-US'), 37.5)).toBe('37.5');
    expect(formatPlannerHours(translate('da'), 7.4)).toBe('7,4');
    expect(formatPlannerHours(translate('da'), 37)).toBe('37');
    expect(formatPlannerHours(translate('en-US'), 1 / 3)).toBe('0.33');
  });

  it('formats nothing for a missing norm', () => {
    expect(formatPlannerHours(translate('da'), null)).toBe('');
    expect(formatPlannerHours(translate('da'), undefined)).toBe('');
  });
});

describe('planner date ranges', () => {
  it('lists every day of a range, and none when it is reversed', () => {
    expect(dateKeysBetween(new Date(2026, 10, 9), new Date(2026, 10, 11)))
      .toEqual(['2026-11-09', '2026-11-10', '2026-11-11']);
    expect(dateKeysBetween(new Date(2026, 10, 11), new Date(2026, 10, 9))).toEqual([]);
  });
});

describe('planner pattern grid', () => {
  it('accepts a pattern only with a Monday anchor and readable days', () => {
    const days = fullPatternGrid(1, [{weekIndex: 0, dayOfWeek: 0, planText: '7-15/0,5'}]);
    expect(isValidPattern(new Date(2026, 10, 9), days)).toBe(true);
    expect(isValidPattern(new Date(2026, 10, 10), days)).toBe(false);
    expect(isValidPattern(null, days)).toBe(false);
    days[1].planText = '7-15 syg';
    expect(isValidPattern(new Date(2026, 10, 9), days)).toBe(false);
  });

  it('trims every day text and sends no plan as ""', () => {
    expect(trimmedPatternDays([
      {weekIndex: 0, dayOfWeek: 0, planText: ' 7-15 '},
      {weekIndex: 0, dayOfWeek: 1, planText: null},
    ])).toEqual([
      {weekIndex: 0, dayOfWeek: 0, planText: '7-15'},
      {weekIndex: 0, dayOfWeek: 1, planText: ''},
    ]);
  });

  it('fills every (week, weekday) and keeps the texts that still fit', () => {
    const grid = fullPatternGrid(2, [
      {weekIndex: 0, dayOfWeek: 0, planText: '7:00-15:00/0.5'},
      {weekIndex: 2, dayOfWeek: 0, planText: 'dropped'},
    ]);
    expect(grid.length).toBe(14);
    expect(grid[0].planText).toBe('7:00-15:00/0.5');
    expect(grid.every(d => d.weekIndex < 2)).toBe(true);
    expect(grid.filter(d => d.planText).length).toBe(1);
  });
});

describe('planner translations', () => {
  const keyOf = (catalogue: object) => new Set(Object.keys(catalogue));

  it('has a message for every import reason code the server sends', () => {
    for (const reason of ['inconsistent', 'unparsable', 'insufficientHistory', 'hoursOnly', null]) {
      const key = importReasonKey(reason);
      expect(keyOf(da).has(key)).toBe(true);
      expect(keyOf(enUS).has(key)).toBe(true);
    }
  });

  it('labels every absence type with an existing key', () => {
    expect(PLANNER_ABSENCE_TYPES.length).toBeGreaterThan(0);
    for (const type of PLANNER_ABSENCE_TYPES) {
      expect(keyOf(da).has(type.key)).toBe(true);
      expect(absenceTypeKey(type.id)).toBe(type.key);
    }
    expect(absenceTypeKey(null)).toBeNull();
  });

  it('carries the same planner keys in Danish and English', () => {
    const planner = (catalogue: object) => [...keyOf(catalogue)].filter(k => k.startsWith('planner')).sort();
    expect(planner(da)).toEqual(planner(enUS));
    expect(planner(da).length).toBeGreaterThan(100);
  });
});
