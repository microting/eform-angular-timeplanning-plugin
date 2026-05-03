/**
 * Off-grid time literals for the `b1m` (and future variant) shards that
 * exercise the `UseOneMinuteIntervals` flag-on code path.
 *
 * Every value here is intentionally NOT a multiple of 5. The legacy
 * `b` shard relies on 5-min-aligned times because the Material clock-face
 * timepicker only renders 12 minute slots when `minutesGap=5`. With the
 * flag on, `minutesGap=1` so the picker renders all 60 slots and the
 * position-based `pickTime()` helper in the variant specs can land on
 * any minute 0-59.
 *
 * Add new constants here rather than hard-coding off-grid times into
 * specs — keeping a single canonical source makes it trivial to spot
 * non-aligned values that are supposed to be aligned (and vice versa).
 */
// Shift order constraint (workday-entity-dialog.shiftWiseValidator):
// shift{n+1}.start MUST be >= shift{n}.stop, otherwise saveButton stays
// disabled with `hierarchyError`. Tests pick up the saved row, so all
// values below are non-overlapping and ascending.
export const OFFGRID_TIMES = {
  shift1Start: '08:01',
  shift1End:   '11:13',
  shift2Start: '12:17',
  shift2End:   '16:23',
  break:       '00:27',
} as const;
