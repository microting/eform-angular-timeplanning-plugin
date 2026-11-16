import {TranslateService} from '@ngx-translate/core';
import {
  addDays as fnsAddDays, eachDayOfInterval, format, getISODay, getISOWeek, isMonday, parse, startOfDay,
  startOfISOWeek, startOfToday,
} from 'date-fns';
import {PlanPatternDayModel} from './models';

/**
 * Calendar-date helpers for the planner (#5489).
 *
 * The planner works in calendar dates only. In transport a date is UTC midnight
 * of that calendar date ("2026-11-09T00:00:00.000Z", the same shape the rest of
 * the plugin sends with PARSING_DATE_FORMAT); for display it is a local Date at
 * local midnight. The server's "2026-11-09T00:00:00" (no zone) is read by its
 * date part alone, so no clock ever shifts a day.
 */

/**
 * "2026-11-09" from a server date. The host's DateInterceptor turns every ISO
 * date string in a response into a Date with date-fns parseJSON, which reads a
 * zone-less string as UTC, so such a Date is UTC midnight of the calendar date
 * and its UTC fields hold that date. A string is read by its date part.
 */
export function dateKey(value: string | Date): string {
  if (value instanceof Date) {
    return value.toISOString().substring(0, 10);
  }
  return (value ?? '').substring(0, 10);
}

/** "2026-11-09" from a local Date (its local calendar date). */
export function localDateKey(date: Date): string {
  return format(date, 'yyyy-MM-dd');
}

/** A server date (see dateKey) or "yyyy-MM-dd" as a local Date at local midnight, for display. */
export function parseCalendarDate(value: string | Date): Date {
  return parse(dateKey(value), 'yyyy-MM-dd', new Date());
}

/** The calendar date of a local Date, as UTC midnight in ISO form, for transport. */
export function toTransportDate(date: Date): string {
  return `${localDateKey(date)}T00:00:00.000Z`;
}

/** A "yyyy-MM-dd" key as UTC midnight in ISO form, for transport. */
export function keyToTransportDate(key: string): string {
  return `${dateKey(key)}T00:00:00.000Z`;
}

export function addDays(date: Date, days: number): Date {
  return fnsAddDays(startOfDay(date), days);
}

/** ISO weekday: 0 = Monday ... 6 = Sunday. */
export function isoDayOfWeek(date: Date): number {
  return getISODay(date) - 1;
}

export function mondayOf(date: Date): Date {
  return startOfISOWeek(date);
}

export function todayLocal(): Date {
  return startOfToday();
}

/** ISO 8601 week number of a local date. */
export function isoWeekNumber(date: Date): number {
  return getISOWeek(date);
}

/** Every date key from `from` to `to`, both included; none when from is after to. */
export function dateKeysBetween(from: Date, to: Date): string[] {
  return from > to ? [] : eachDayOfInterval({start: from, end: to}).map(localDateKey);
}

/** Datepicker filter: a rotation's anchor is always a Monday. */
export const mondaysOnly = (date: Date | null): boolean => !!date && isMonday(date);

/** The current translate language, for Intl/toLocale* formatting. */
export function plannerLocale(translate: TranslateService): string {
  return translate.currentLang || translate.defaultLang || 'da';
}

/** Weekday, day, month and year: "man. 9. nov. 2026". */
export const LONG_DATE: Intl.DateTimeFormatOptions = {weekday: 'short', day: 'numeric', month: 'short', year: 'numeric'};

/** A local date in the user's language. */
export function formatPlannerDate(translate: TranslateService, date: Date | null,
                                  options: Intl.DateTimeFormatOptions = LONG_DATE): string {
  return date ? date.toLocaleDateString(plannerLocale(translate), options) : '';
}

/** Hours in the user's language, at most 2 decimals: "37,5" (da), "37.5" (en); "" for null. */
export function formatPlannerHours(translate: TranslateService, value: number | null | undefined): string {
  if (value == null) {
    return '';
  }
  return new Intl.NumberFormat(plannerLocale(translate), {maximumFractionDigits: 2}).format(value);
}

const TIME = '[0-9]{1,2}(?:[:.,][0-9]{1,2}|½)?';
const BREAK = '(?:[0-9]{1,2}:[0-9]{1,2}|(?=[0-9.,½¾])[0-9]*(?:[.,][0-9]+)?[½¾]?)';
/** PlannerShortForm.WholeSegment, with the two times captured for the value checks. */
const WHOLE_SEGMENT = new RegExp(`^(${TIME})\\s*-\\s*(${TIME})(?:\\s*/\\s*${BREAK})?$`);
/** PlanTextParser.TimeRegex. */
const TIME_TOKEN = /^([0-9]{1,2})(?:([:.,])([0-9]{1,2})|(½))?$/;
const MAX_SHIFTS = 5;
const MINUTES_PER_DAY = 24 * 60;

/**
 * PlanTextParser.TryResolveTime: minutes since midnight, or null where the
 * server refuses the time. A colon or two digits are clock minutes (00-59); a
 * single digit after '.' or ',' is shorthand and only 0, 3 and 5 are allowed
 * ("7,5" and "6.3" are both 07:30 / 06:30, "7.7" is refused); nothing past 24:00.
 */
function resolveTime(token: string): number | null {
  const match = TIME_TOKEN.exec(token);
  if (!match) {
    return null;
  }
  const [, hour, separator, fraction, half] = match;
  let minutes = 0;
  if (half) {
    minutes = 30;
  } else if (fraction !== undefined) {
    if (separator === ':' || fraction.length === 2) {
      minutes = Number(fraction);
      if (minutes > 59) {
        return null;
      }
    } else if (fraction === '0') {
      minutes = 0;
    } else if (fraction === '3' || fraction === '5') {
      minutes = 30;
    } else {
      return null;
    }
  }
  const total = Number(hour) * 60 + minutes;
  return total > MINUTES_PER_DAY ? null : total;
}

/**
 * One trimmed segment the server reads as a shift: wholly "start-end[/break]"
 * (PlannerShortForm.WholeSegment), both times valid, and start != end
 * (PlanTextParser.TryParseSegment). A break that has the right shape never
 * fails on the server, so its value is not checked.
 */
function isWholeShift(segment: string): boolean {
  const match = WHOLE_SEGMENT.exec(segment);
  if (!match) {
    return false;
  }
  const start = resolveTime(match[1]);
  const end = resolveTime(match[2]);
  return start !== null && end !== null && start !== end;
}

/**
 * The same check as the server's PlannerShortForm.Parse, so the editor can say
 * "kan ikke læses" before sending. The server stays the authority: it parses
 * again and refuses anything it cannot read.
 *
 * Returns 'clear' for empty text ("Ryd"), 'dayOff' for "Fri", 'plan' for text
 * that is wholly shifts (at most 5, ";"-separated), else 'invalid'.
 */
export type ShortFormKind = 'clear' | 'dayOff' | 'plan' | 'invalid';

export function shortFormKind(text: string | null | undefined): ShortFormKind {
  const trimmed = (text ?? '').trim();
  if (!trimmed) {
    return 'clear';
  }
  if (trimmed.toLowerCase() === 'fri') {
    return 'dayOff';
  }
  const segments = trimmed.split(';').map(s => s.trim()).filter(s => s.length > 0);
  if (segments.length === 0 || segments.length > MAX_SHIFTS) {
    return 'invalid';
  }
  return segments.every(isWholeShift) ? 'plan' : 'invalid';
}

/** A pattern day text is acceptable when empty, "Fri" or shifts. */
export function isValidPatternDayText(text: string | null | undefined): boolean {
  return shortFormKind(text) !== 'invalid';
}

/**
 * Every (week, weekday) of a rotation in order, keeping the texts of `days`
 * that still fit; new weeks start empty ("" = no plan).
 *
 * A day that still fits is the SAME object, not a copy: the editor's inputs are
 * bound to these objects, and text typed into an input while the grid is being
 * re-rendered for a new rotation length must land in the grid that is saved.
 */
export function fullPatternGrid(rotationWeeks: number, days: PlanPatternDayModel[]): PlanPatternDayModel[] {
  const byCell = new Map<string, PlanPatternDayModel>();
  for (const day of days ?? []) {
    const key = `${day.weekIndex}|${day.dayOfWeek}`;
    if (!byCell.has(key)) {
      byCell.set(key, day);
    }
  }
  const grid: PlanPatternDayModel[] = [];
  for (let week = 0; week < rotationWeeks; week++) {
    for (let dow = 0; dow < 7; dow++) {
      const day = byCell.get(`${week}|${dow}`) ?? {weekIndex: week, dayOfWeek: dow, planText: ''};
      day.planText ??= '';
      grid.push(day);
    }
  }
  return grid;
}

/** True when no day of the pattern holds a shift: every text is empty or "Fri". */
export function isEmptyPattern(days: PlanPatternDayModel[]): boolean {
  return days.every(d => shortFormKind(d.planText) !== 'plan');
}

/** A pattern can be saved: a Monday anchor and every day text readable. */
export function isValidPattern(anchorDate: Date | null, days: PlanPatternDayModel[]): boolean {
  return mondaysOnly(anchorDate) && days.every(d => isValidPatternDayText(d.planText));
}

/** The days as sent to the server: every text trimmed, null as "". */
export function trimmedPatternDays(days: PlanPatternDayModel[]): PlanPatternDayModel[] {
  return days.map(d => ({weekIndex: d.weekIndex, dayOfWeek: d.dayOfWeek, planText: (d.planText ?? '').trim()}));
}

/** Rotation lengths the server accepts. */
export const ROTATION_WEEK_OPTIONS = [1, 2, 3, 4, 5, 6, 7, 8];

/** Weekday translation keys, ISO order (the plugin already translates them). */
export const WEEKDAY_KEYS = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday'];
