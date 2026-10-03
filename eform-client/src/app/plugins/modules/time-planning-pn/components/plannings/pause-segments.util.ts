/** One pause as shown on the web: wall-clock 'HH:mm', stop null while it is running. */
export interface PauseSegment {
  start: string;
  stop: string | null;
}

const SECONDS_PER_DAY = 86400;

/**
 * Seconds-of-day of a stamp. The stamps are wall-clock times serialised as UTC, the
 * same reading the grid and the dialog give them through DatePipe(..., 'UTC').
 */
function secondOfDay(stamp: Date): number {
  return stamp.getUTCHours() * 3600 + stamp.getUTCMinutes() * 60 + stamp.getUTCSeconds();
}

function utcDay(stamp: Date): number {
  return Date.UTC(stamp.getUTCFullYear(), stamp.getUTCMonth(), stamp.getUTCDate());
}

function toHhmm(seconds: number): string {
  const minutes = Math.floor(seconds / 60);
  return `${String(Math.floor(minutes / 60)).padStart(2, '0')}:${String(minutes % 60).padStart(2, '0')}`;
}

function parse(value: string | null | undefined): Date | null {
  if (!value) {
    return null;
  }
  const date = new Date(value);
  return isNaN(date.getTime()) ? null : date;
}

/**
 * The pauses of one shift as the web shows them, built from its
 * Pause{N}StartedAt / Pause{N}StoppedAt slot pairs.
 *
 * Closed pauses follow the rules of the backend's BuildMergedPauseSegments
 * (TimePlanningWorkingHoursService), applied per shift rather than across the whole
 * day as the backend does, because the dialog shows them under each shift:
 *  - each pair resolves like ResolveShiftSeconds: a stop on a later day is clamped to
 *    24:00, and a pair whose stop is not after its start is dropped;
 *  - the rest are sorted by start, and overlapping or touching pauses are merged.
 *
 * A pause with a start and no stop is still running. The backend has nothing to cut
 * for it, but the admin should see it, so it is listed open-ended and never merged.
 * A stop with no start says nothing and is skipped.
 */
export function buildPauseSegments(
  pairs: ReadonlyArray<readonly [string | null | undefined, string | null | undefined]>,
): PauseSegment[] {
  const closed: Array<{ start: number; stop: number }> = [];
  const open: number[] = [];

  for (const [rawStart, rawStop] of pairs) {
    const start = parse(rawStart);
    if (!start) {
      continue;
    }
    const startSec = secondOfDay(start);
    const stop = parse(rawStop);
    if (!stop) {
      open.push(startSec);
      continue;
    }
    const stopSec = utcDay(stop) > utcDay(start) ? SECONDS_PER_DAY : secondOfDay(stop);
    if (stopSec > startSec) {
      closed.push({ start: startSec, stop: stopSec });
    }
  }

  closed.sort((a, b) => a.start - b.start);
  const merged: Array<{ start: number; stop: number }> = [];
  for (const current of closed) {
    const last = merged[merged.length - 1];
    if (last && current.start <= last.stop) {
      last.stop = Math.max(last.stop, current.stop);
    } else {
      merged.push({ ...current });
    }
  }

  const segments: Array<{ start: number; stop: number | null }> =
    [...merged, ...open.map(start => ({ start, stop: null }))];
  return segments
    .sort((a, b) => a.start - b.start)
    .map(s => ({ start: toHhmm(s.start), stop: s.stop === null ? null : toHhmm(s.stop) }));
}
