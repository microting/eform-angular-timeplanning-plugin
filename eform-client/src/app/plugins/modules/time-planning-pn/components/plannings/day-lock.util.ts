import {DatePipe} from '@angular/common';
import {TranslateService} from '@ngx-translate/core';
import {format} from 'date-fns';
import {PlanningPrDayModel, TimePlanningModel} from '../../models';

/**
 * Calendar-day key 'yyyy-MM-dd', for comparing days with no timezone involved.
 *
 * Server dates (PlanningPrDayModel.date, TimePlanningModel.lockedThrough) arrive as
 * server-local midnight with no offset, e.g. '2026-09-07T00:00:00', so the first ten
 * characters ARE the calendar day. Turning them into a Date and comparing instants is
 * how a boundary ends up a day off for part of every evening. A Date is formatted in
 * local time.
 */
export function dayKey(value: string | Date | null | undefined): string | null {
  if (!value) {
    return null;
  }
  if (value instanceof Date) {
    return isNaN(value.getTime()) ? null : format(value, 'yyyy-MM-dd');
  }
  return /^\d{4}-\d{2}-\d{2}/.test(value) ? value.slice(0, 10) : null;
}

// ---- The three day states (spec §8.1, ruling F20) ---------------------------------
//
// A row's lock is derived from one date, row.lockedThrough (the boundary). Every day
// at or before it is LOCKED. Among the locked days:
// - the BOUNDARY is the single day whose date is lockedThrough;
// - a SEALED day carries its own reconcile mark (reconciled === true). The boundary
//   is always sealed, and older days stay sealed after a later reconcile moves the
//   boundary on, so several days of a row can be sealed at once. The flag alone
//   therefore never makes a day the boundary.
// Every comparison is by calendar day (dayKey), never by instant.

/** The part of a grid row the day-state predicates read. */
export type DayLockRow = Pick<TimePlanningModel, 'lockedThrough' | 'planningPrDayModels'>;

/** All four states of one day, resolved in a single pass. */
export interface DayLockState {
  /** At or before the row's boundary, the boundary included. A locked day is read-only. */
  locked: boolean;
  /** The one day whose date is row.lockedThrough. Only it offers unlock. */
  boundary: boolean;
  /** A locked day with its own reconcile mark: the boundary, or an older reconciled day. */
  sealed: boolean;
  /** A day with no registration behind it (F17). It is locked, and it renders blank. */
  placeholder: boolean;
}

/**
 * The day under a grid column. The column field is the day's index in the row
 * ('0', '1', …): the grid reads days by position.
 */
export function dayAt(
  row: DayLockRow | null | undefined, field: string | number,
): PlanningPrDayModel | undefined {
  return row?.planningPrDayModels?.[Number(field)];
}

/**
 * F17: the server sends a non-persisted stand-in, with id 0, for a locked date that
 * has no registration, so that every later day stays under its own column header.
 * An id is therefore the one mark of a day that exists in the database: no id, no
 * row to open, to save or to reconcile, and nothing to render.
 */
export function hasRegistration(day: PlanningPrDayModel | null | undefined): boolean {
  return !!day?.id;
}

/**
 * The day's lock state, with the day looked up and the dates parsed once. The
 * template, the cell class and the dialog all read it through this, so they can
 * never disagree about a cell.
 */
export function dayLockState(
  row: DayLockRow | null | undefined, field: string | number,
): DayLockState {
  const day = dayAt(row, field);
  const placeholder = day !== undefined && day !== null && !hasRegistration(day);
  const boundaryKey = dayKey(row?.lockedThrough);
  if (boundaryKey === null) {
    // Nothing on this row is locked. That is every row of every tenant who has never
    // reconciled, so it is answered before a single day date is parsed.
    return {locked: false, boundary: false, sealed: false, placeholder};
  }
  const key = dayKey(day?.date);
  const locked = key !== null && key <= boundaryKey;
  return {
    locked,
    boundary: key === boundaryKey,
    // The lock decides, not the flag: an unlock clears the flag, so a mark that
    // outlived its lock would be stale.
    sealed: locked && day?.reconciled === true,
    placeholder,
  };
}

/** At or before the row's boundary, the boundary itself included. A locked day is read-only. */
export function isDayLocked(row: DayLockRow | null | undefined, field: string | number): boolean {
  return dayLockState(row, field).locked;
}

/** The boundary: the one day whose date is row.lockedThrough. Only it offers unlock. */
export function isBoundaryDay(row: DayLockRow | null | undefined, field: string | number): boolean {
  return dayLockState(row, field).boundary;
}

/** A locked day with its own reconcile mark: the boundary, or an older reconciled day. */
export function isDaySealed(row: DayLockRow | null | undefined, field: string | number): boolean {
  return dayLockState(row, field).sealed;
}

/** A stand-in for a locked date with no registration (F17): not openable, rendered blank. */
export function isPlaceholderDay(row: DayLockRow | null | undefined, field: string | number): boolean {
  return dayLockState(row, field).placeholder;
}

/**
 * "Afstemt 14.09.2026 kl. 10:32" (spec §8.1, §8.2). There is no "by whom":
 * ReconciledBy is a declared non-goal.
 *
 * ReconciledAt is written as DateTime.Now (Task 4) and read back from a datetime(6)
 * column, so EF materialises it as DateTimeKind.Unspecified. Newtonsoft
 * (RoundtripKind) serialises it with NO offset, and the browser reads it as local
 * wall-clock time, which is the server's clock on a Danish deployment. Do NOT pass
 * 'UTC' here, the way formatStamp does for the shift stamps.
 */
export function formatReconciledProvenance(
  reconciledAt: string | null | undefined,
  datePipe: DatePipe,
  translate: TranslateService,
): string {
  if (!reconciledAt) {
    // I1 says a reconciled day always has a timestamp; this covers a malformed row.
    return translate.instant('Reconciled');
  }
  return translate.instant('reconciledProvenance', {
    date: datePipe.transform(reconciledAt, 'dd.MM.yyyy'),
    time: datePipe.transform(reconciledAt, 'HH:mm'),
  });
}

/** 'lock' = the boundary moves to a day on screen. 'skip' = already at or past the target. */
export type ReconcileRowOutcome = 'lock' | 'skip';

export interface ReconcilePreview {
  /** 'yyyy-MM-dd'. Sent to reconcile-through as it is. */
  target: string;
  /**
   * Workers sent to reconcile-through: every row in scope that the preview drew,
   * as 'lock' or as 'skip', each once. Skipped rows are sent too, so that the server
   * reports them. A row the preview could not draw is never sent (§8.3: the region
   * is previewed before it is committed).
   */
  siteIds: number[];
  /** Per worker: the day the mark lands on. */
  landingBySiteId: Record<number, string>;
  /** Per worker: the boundary they already have, so already-locked cells are not re-highlighted. */
  existingBySiteId: Record<number, string | null>;
  outcomeBySiteId: Record<number, ReconcileRowOutcome>;
  /** Workers whose boundary will move. */
  willReconcileCount: number;
  skipCount: number;
}

/**
 * The region a reconcile-through WILL lock, per worker (spec §8.3). It mirrors the
 * server rules in Task 4:
 * - A worker already reconciled at or past the target is skipped, never moved back.
 *   Moving back would be an unlock, which is a separate, heavier action (§8.4).
 * - Otherwise the mark lands on the latest day at or before the target that has a
 *   registration.
 *
 * Only days on screen are known here. A row with no registered day on screen
 * between its boundary and the target has nothing to draw, so it is left out of the
 * commit. The date field is limited to days on screen (12A) and the index creates a
 * registration for every visible day (fact 11), so this only happens when a visible
 * day failed to materialise.
 */
export function buildReconcilePreview(
  rows: TimePlanningModel[],
  scopeSiteIds: number[],
  target: string,
): ReconcilePreview {
  const inScope = new Set(scopeSiteIds);
  const preview: ReconcilePreview = {
    target,
    siteIds: [],
    landingBySiteId: {},
    existingBySiteId: {},
    outcomeBySiteId: {},
    willReconcileCount: 0,
    skipCount: 0,
  };

  for (const row of rows) {
    if (!inScope.has(row.siteId) || row.siteId in preview.outcomeBySiteId) {
      continue;
    }
    const existing = dayKey(row.lockedThrough);
    preview.existingBySiteId[row.siteId] = existing;

    if (existing !== null && existing >= target) {
      preview.outcomeBySiteId[row.siteId] = 'skip';
      preview.siteIds.push(row.siteId);
      preview.skipCount++;
      continue;
    }

    const landing = (row.planningPrDayModels ?? [])
      .filter(hasRegistration)
      .map(day => dayKey(day.date))
      .filter((key): key is string => key !== null && key <= target)
      .sort()
      .pop();
    if (!landing || (existing !== null && landing <= existing)) {
      continue;
    }

    preview.landingBySiteId[row.siteId] = landing;
    preview.outcomeBySiteId[row.siteId] = 'lock';
    preview.siteIds.push(row.siteId);
    preview.willReconcileCount++;
  }
  return preview;
}
