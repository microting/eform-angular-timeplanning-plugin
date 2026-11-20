import {TranslateService} from '@ngx-translate/core';
import {
  addDays as fnsAddDays, addMonths as fnsAddMonths, eachDayOfInterval, format, getDaysInMonth, getISODay, getISOWeek,
  isMonday, parse, startOfDay, startOfISOWeek, startOfMonth, startOfToday,
} from 'date-fns';
import {defer, MonoTypeOperatorFunction, Observable, tap} from 'rxjs';
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

/** The 1st of the date's calendar month, at local midnight. */
export function monthStartOf(date: Date): Date {
  return startOfMonth(date);
}

/** The 1st of the month `months` calendar months from the month of `date`. */
export function addMonthsToStart(date: Date, months: number): Date {
  return fnsAddMonths(startOfMonth(date), months);
}

/** Days in the date's calendar month (28-31). */
export function daysInMonthOf(date: Date): number {
  return getDaysInMonth(date);
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

/** The text with its first letter upper-cased in the user's language: "tirsdag 6. okt." -> "Tirsdag 6. okt.". */
export function capitalizeFirst(translate: TranslateService, text: string): string {
  return text.charAt(0).toLocaleUpperCase(plannerLocale(translate)) + text.slice(1);
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
/** PlannerShortForm.WholeSegment, with the two times and the break captured for the value checks and the hours. */
const WHOLE_SEGMENT = new RegExp(`^(${TIME})\\s*-\\s*(${TIME})(?:\\s*/\\s*(${BREAK}))?$`);
/** PlanTextParser.BreakColonRegex / BreakValueRegex, over a break WHOLE_SEGMENT has already delimited. */
const BREAK_COLON = /^([0-9]{1,2}):([0-9]{1,2})/;
const BREAK_VALUE = /^([0-9]*(?:\.[0-9]+)?)([½¾])?/;
const GLYPH_MINUTES: Record<string, number> = {'½': 30, '¾': 45};
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
 * PlanTextParser.ParseBreakMinutes for a break of the right shape: decimal
 * hours ("0,5" is 30 minutes), the '½' and '¾' glyphs, or "H:MM"; at most a day.
 */
function breakMinutes(text: string | undefined): number {
  const normalized = (text ?? '').replace(/,/g, '.').trim();
  if (!normalized) {
    return 0;
  }
  const colon = BREAK_COLON.exec(normalized);
  if (colon) {
    return Math.min(Number(colon[1]) * 60 + Number(colon[2]), MINUTES_PER_DAY);
  }
  const [, number, glyph] = BREAK_VALUE.exec(normalized) ?? [];
  const glyphMinutes = glyph ? GLYPH_MINUTES[glyph] : 0;
  const numberMinutes = number ? Math.round(Number(number) * 60) : 0;
  return Math.min(numberMinutes + glyphMinutes, MINUTES_PER_DAY);
}

/**
 * One trimmed segment the server reads as a shift: wholly "start-end[/break]"
 * (PlannerShortForm.WholeSegment), both times valid, and start != end
 * (PlanTextParser.TryParseSegment). A break that has the right shape never
 * fails on the server, so its value is not checked.
 *
 * Returns the shift's planned minutes as PlanRegistrationPlanText sums them
 * (an end before the start crosses midnight, less the break), or null when
 * the segment is not a shift.
 */
function wholeShiftMinutes(segment: string): number | null {
  const match = WHOLE_SEGMENT.exec(segment);
  if (!match) {
    return null;
  }
  const start = resolveTime(match[1]);
  const end = resolveTime(match[2]);
  if (start === null || end === null || start === end) {
    return null;
  }
  const length = end < start ? end + MINUTES_PER_DAY - start : end - start;
  return length - breakMinutes(match[3]);
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

/** A read short form: its kind, and for 'plan' the hours the server will store (PlanHours), else null. */
export interface ShortFormReading {
  kind: ShortFormKind;
  hours: number | null;
}

export function readShortForm(text: string | null | undefined): ShortFormReading {
  const trimmed = (text ?? '').trim();
  if (!trimmed) {
    return {kind: 'clear', hours: null};
  }
  if (trimmed.toLowerCase() === 'fri') {
    return {kind: 'dayOff', hours: null};
  }
  const segments = trimmed.split(';').map(s => s.trim()).filter(s => s.length > 0);
  if (segments.length === 0 || segments.length > MAX_SHIFTS) {
    return {kind: 'invalid', hours: null};
  }
  let total = 0;
  for (const segment of segments) {
    const minutes = wholeShiftMinutes(segment);
    if (minutes === null) {
      return {kind: 'invalid', hours: null};
    }
    total += minutes;
  }
  // PlanRegistrationPlanText.SumPlannedMinutes: breaks longer than the shifts never go below 0.
  return {kind: 'plan', hours: Math.max(total, 0) / 60};
}

export function shortFormKind(text: string | null | undefined): ShortFormKind {
  return readShortForm(text).kind;
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

/**
 * Runs `onSilence` when a request completes without emitting a value or an
 * error. The host's HTTP error interceptor answers a 401, a 403 whose token
 * refresh failed, and exhausted retries with EMPTY, so `next` and `error`
 * never run and a busy flag set before the call would stay set.
 *
 * The flag lives per subscription (defer), so one request's completion never
 * releases another request's state.
 */
export function onNoReply<T>(onSilence: () => void): MonoTypeOperatorFunction<T> {
  return (source: Observable<T>) => defer(() => {
    let replied = false;
    return source.pipe(tap({
      next: () => { replied = true; },
      complete: () => {
        if (!replied) {
          onSilence();
        }
      },
    }));
  });
}

/** Initials for an avatar: the first letters of the first and the last word ("Anna Hansen" -> "AH"). */
export function workerInitials(name: string | null | undefined): string {
  const words = (name ?? '').trim().split(/\s+/).filter(w => w.length > 0);
  if (words.length === 0) {
    return '?';
  }
  const first = Array.from(words[0])[0];
  const last = words.length > 1 ? Array.from(words[words.length - 1])[0] : '';
  return (first + last).toLocaleUpperCase();
}

/**
 * Keyboard handling of a `.segmented` radiogroup (the workspace segmented
 * control, as the Compliance report's view switcher): ArrowRight/Down checks
 * the next option, ArrowLeft/Up the previous one, wrapping at both ends,
 * Home/End the first/last. Returns the index to check, or null for any other
 * key. Focus follows the checked option; the roving tabindex in the template
 * keeps it the only tab stop.
 */
export function segmentedKeydown(event: KeyboardEvent, index: number, count: number): number | null {
  let next: number;
  switch (event.key) {
    case 'ArrowRight':
    case 'ArrowDown':
      next = (index + 1) % count;
      break;
    case 'ArrowLeft':
    case 'ArrowUp':
      next = (index - 1 + count) % count;
      break;
    case 'Home':
      next = 0;
      break;
    case 'End':
      next = count - 1;
      break;
    default:
      return null;
  }
  event.preventDefault();
  const group = (event.currentTarget as HTMLElement | null)?.parentElement;
  const options = Array.from(group?.children ?? []).filter(el => el.getAttribute('role') === 'radio');
  (options[next] as HTMLElement | undefined)?.focus();
  return next;
}
