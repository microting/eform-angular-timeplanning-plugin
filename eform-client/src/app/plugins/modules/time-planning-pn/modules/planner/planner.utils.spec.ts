import {describe, it, expect, vi} from 'vitest';
import {TranslateService} from '@ngx-translate/core';
import {EMPTY, of, throwError} from 'rxjs';
import {
  dateKey, dateKeysBetween, formatPlannerHours, fullPatternGrid, isEmptyPattern, isoDayOfWeek, isoWeekNumber, isValidPattern,
  keyToTransportDate, labelInSentence, mondayOf, onNoReply, parseCalendarDate, readShortForm, segmentedKeydown, shortFormKind,
  toTransportDate, trimmedPatternDays, workerInitials,
} from './planner.utils';
import {importReasonKey} from './components/planner-import-dialog/planner-import-dialog.component';
import {absenceTypeKey, DEFAULT_ABSENCE_TYPE, PLANNER_ABSENCE_TYPES} from './planner-absence-types';
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

describe('planner short form matches the server (PlannerPureLogicTests)', () => {
  // ShortForm_TimeText_IsNormalisedToCanonicalPlanText
  it.each([
    '7-15/0,5', ' 7-15/0.5 ', '7.30-15', '6-10;14-18/0,25', '8:00-16:00/0.5', '7½-15', '7,5-15/½', '6-14/0:45',
    '6 - 14 / .5', '7:00-15:00/0.5', '6.3-16/1', '22-24',
  ])('accepts %j', (input) => {
    expect(shortFormKind(input)).toBe('plan');
  });

  // ShortForm_Empty_IsClear and ShortForm_Fri_IsADayWithNoPlan
  it.each([null, '', '   '])('reads %j as clear', (input) => {
    expect(shortFormKind(input)).toBe('clear');
  });
  it.each(['Fri', 'fri', ' FRI '])('reads %j as a day off', (input) => {
    expect(shortFormKind(input)).toBe('dayOff');
  });

  // ShortForm_TextThatIsNotWhollyAShift_IsInvalid, plus PlanTextParser's value checks
  it.each([
    'hello', '7-15;x', '8', '7.7-15', '7-15 syg', 'syg 7-15', 'x7-15', '7-15x', '7-15/', '7-15/0,5 syg', '7-15/,',
    '٧-١٥', '7:75-15', '7.60-15', '7-24:30', '25-30', '7-7', '0-0',
  ])('refuses %j', (input) => {
    expect(shortFormKind(input)).toBe('invalid');
  });
});

describe('planner short form hours (PlanRegistrationPlanText.SumPlannedMinutes)', () => {
  it('sums every shift less its break', () => {
    expect(readShortForm('7-15/0,5')).toEqual({kind: 'plan', hours: 7.5});
    expect(readShortForm('7:00-15:00')).toEqual({kind: 'plan', hours: 8});
    expect(readShortForm('6-10;16-20')).toEqual({kind: 'plan', hours: 8});
    expect(readShortForm('6-10;14-18/0,25').hours).toBe(7.75);
    expect(readShortForm('7½-15/½').hours).toBe(7);
    expect(readShortForm('6-14/0:45').hours).toBe(7.25);
    expect(readShortForm('6 - 14 / .5').hours).toBe(7.5);
    expect(readShortForm('6-14/1¾').hours).toBe(6.25);
    expect(readShortForm('7:00-14:54/0.5').hours).toBeCloseTo(7.4, 10);
  });

  it('reads an end before the start as crossing midnight', () => {
    expect(readShortForm('22-6/0,5').hours).toBe(7.5);
    expect(readShortForm('22:00-0:00').hours).toBe(2);
  });

  it('never goes below zero when the break is longer than the shift', () => {
    expect(readShortForm('7-8/2')).toEqual({kind: 'plan', hours: 0});
  });

  it('has no hours for empty, Fri or unreadable text', () => {
    expect(readShortForm('')).toEqual({kind: 'clear', hours: null});
    expect(readShortForm('Fri')).toEqual({kind: 'dayOff', hours: null});
    expect(readShortForm('7-15 syg')).toEqual({kind: 'invalid', hours: null});
    expect(readShortForm('7-15;x')).toEqual({kind: 'invalid', hours: null});
  });
});

describe('planner dates', () => {
  it('reads a server date by its calendar date, whatever the clock', () => {
    const date = parseCalendarDate('2026-11-09T00:00:00');
    expect([date.getFullYear(), date.getMonth(), date.getDate()]).toEqual([2026, 10, 9]);
    expect(dateKey('2026-11-09T00:00:00')).toBe('2026-11-09');
  });

  it('reads the Date the host DateInterceptor makes of a server date (UTC midnight)', () => {
    const parsed = new Date(Date.UTC(2026, 10, 9));
    expect(dateKey(parsed)).toBe('2026-11-09');
    const date = parseCalendarDate(parsed);
    expect([date.getFullYear(), date.getMonth(), date.getDate()]).toEqual([2026, 10, 9]);
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

  it('formats hours in the user\'s language with exactly 2 decimals', () => {
    expect(formatPlannerHours(translate('da'), 37.5)).toBe('37,50');
    expect(formatPlannerHours(translate('en-US'), 37.5)).toBe('37.50');
    expect(formatPlannerHours(translate('da'), 7.4)).toBe('7,40');
    expect(formatPlannerHours(translate('da'), 37)).toBe('37,00');
    expect(formatPlannerHours(translate('da'), 7.756)).toBe('7,76');
    expect(formatPlannerHours(translate('en-US'), 1 / 3)).toBe('0.33');
  });

  it('formats nothing for a missing norm', () => {
    expect(formatPlannerHours(translate('da'), null)).toBe('');
    expect(formatPlannerHours(translate('da'), undefined)).toBe('');
  });
});

describe('planner labels mid-sentence', () => {
  const translate = (lang: string, label: string) =>
    ({currentLang: lang, defaultLang: 'da', instant: () => label}) as unknown as TranslateService;

  it('lower-cases the label, except German nouns, which keep their capital', () => {
    expect(labelInSentence(translate('de-DE', 'Urlaub'), 'Vacation')).toBe('Urlaub');
    expect(labelInSentence(translate('da', 'Ferie'), 'Vacation')).toBe('ferie');
    expect(labelInSentence(translate('en-US', 'Vacation'), 'Vacation')).toBe('vacation');
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

  it('keeps the same day objects when the rotation grows, so text typed meanwhile is kept', () => {
    const twoWeeks = fullPatternGrid(2, []);
    const sixWeeks = fullPatternGrid(6, twoWeeks);
    // Typed into an input of the 2-week grid after the 6-week grid was built.
    twoWeeks[0].planText = '7-15/0,5';
    twoWeeks[13].planText = '8-16';
    expect(sixWeeks[0]).toBe(twoWeeks[0]);
    expect(sixWeeks[0].planText).toBe('7-15/0,5');
    expect(sixWeeks[13].planText).toBe('8-16');
    expect(sixWeeks.length).toBe(42);
  });

  it('calls a pattern with no shift at all empty', () => {
    expect(isEmptyPattern(fullPatternGrid(1, []))).toBe(true);
    expect(isEmptyPattern([{weekIndex: 0, dayOfWeek: 0, planText: 'Fri'}, {weekIndex: 0, dayOfWeek: 1, planText: null}]))
      .toBe(true);
    expect(isEmptyPattern([{weekIndex: 0, dayOfWeek: 0, planText: '7:00-15:00/0.5'}])).toBe(false);
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

  it('offers the design\'s types first, vacation as the default', () => {
    expect(PLANNER_ABSENCE_TYPES.slice(0, 7).map(t => t.key)).toEqual(
      ['Vacation', 'TimeOff', 'VacationDayOff', 'DayOff', 'Course', 'LeaveOfAbsence', 'Maternity']);
    expect(PLANNER_ABSENCE_TYPES[0].id).toBe(DEFAULT_ABSENCE_TYPE);
    expect(PLANNER_ABSENCE_TYPES.some(t => t.key === 'Blank')).toBe(false);
  });

  it('carries the same planner keys in Danish and English', () => {
    const planner = (catalogue: object) => [...keyOf(catalogue)].filter(k => k.startsWith('planner')).sort();
    expect(planner(da)).toEqual(planner(enUS));
    expect(planner(da).length).toBeGreaterThan(100);
  });
});

describe('onNoReply', () => {
  it('runs only when the request completes without a value or an error', () => {
    const silence = vi.fn();
    EMPTY.pipe(onNoReply(silence)).subscribe();
    expect(silence).toHaveBeenCalledTimes(1);

    silence.mockClear();
    of(1).pipe(onNoReply(silence)).subscribe();
    throwError(() => new Error('x')).pipe(onNoReply(silence)).subscribe({error: () => undefined});
    expect(silence).not.toHaveBeenCalled();
  });
});

describe('workerInitials', () => {
  it('takes the first letters of the first and last word', () => {
    expect(workerInitials('Anna Hansen')).toBe('AH');
    expect(workerInitials('  bo  Ole  jensen ')).toBe('BJ');
    expect(workerInitials('Øjvind')).toBe('Ø');
    expect(workerInitials('')).toBe('?');
    expect(workerInitials(null)).toBe('?');
  });
});

describe('segmentedKeydown', () => {
  function group(count: number): HTMLButtonElement[] {
    const div = document.createElement('div');
    div.setAttribute('role', 'radiogroup');
    const buttons = Array.from({length: count}, () => {
      const b = document.createElement('button');
      b.setAttribute('role', 'radio');
      div.appendChild(b);
      return b;
    });
    document.body.appendChild(div);
    return buttons;
  }

  function press(target: HTMLElement, key: string, index: number, count: number): number | null {
    const event = new KeyboardEvent('keydown', {key, cancelable: true});
    Object.defineProperty(event, 'currentTarget', {value: target});
    return segmentedKeydown(event, index, count);
  }

  it('moves with the arrows, wrapping, and jumps with Home/End, focusing the new option', () => {
    const buttons = group(3);
    expect(press(buttons[2], 'ArrowRight', 2, 3)).toBe(0);
    expect(document.activeElement).toBe(buttons[0]);
    expect(press(buttons[0], 'ArrowLeft', 0, 3)).toBe(2);
    expect(press(buttons[0], 'ArrowDown', 0, 3)).toBe(1);
    expect(press(buttons[1], 'ArrowUp', 1, 3)).toBe(0);
    expect(press(buttons[1], 'End', 1, 3)).toBe(2);
    expect(press(buttons[1], 'Home', 1, 3)).toBe(0);
  });

  it('ignores other keys', () => {
    const buttons = group(2);
    expect(press(buttons[0], 'Enter', 0, 2)).toBeNull();
    expect(press(buttons[0], 'a', 0, 2)).toBeNull();
  });
});
