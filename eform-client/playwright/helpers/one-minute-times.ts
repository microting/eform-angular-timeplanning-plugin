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

/**
 * c1m shard variant: uses afternoon/evening boundaries to exercise the
 * lower-half hour positions on the timepicker clock face (and inner-ring
 * 13-23 selectors). Every value is non-aligned to 5 minutes so the flag-on
 * `minutesGap=1` rendering is the only way the picker can land on these
 * values.
 *
 * Same shift-order constraint as `OFFGRID_TIMES` above:
 * shift{n+1}.start MUST be >= shift{n}.stop.
 */
export const OFFGRID_TIMES_C1M = {
  shift1Start: '08:01',
  shift1End:   '11:13',
  shift2Start: '12:17',
  shift2End:   '14:23',
  shift3Start: '14:35',
  shift3End:   '15:42',
  shift4Start: '15:55',
  shift4End:   '17:08',
  shift5Start: '17:21',
  shift5End:   '19:33',
  break:       '00:27',
} as const;

/**
 * d1m shard variant: late-afternoon-through-late-evening boundaries that
 * exercise the inner-ring 13-23 hour selectors of the timepicker clock face.
 * Where b1m sweeps the early-morning / outer-ring 1-10 range and c1m sweeps
 * the early-afternoon transition (8 outer → 19 inner), d1m keeps every shift
 * inside the inner ring (13-23) so the variant matrix as a whole touches the
 * full 24-hour clock surface. Every value is non-aligned to 5 minutes so the
 * flag-on `minutesGap=1` rendering is the only way the picker can land on
 * these values.
 *
 * Same shift-order constraint as `OFFGRID_TIMES` above:
 * shift{n+1}.start MUST be >= shift{n}.stop.
 */
export const OFFGRID_TIMES_D1M = {
  shift1Start: '13:02',
  shift1End:   '14:14',
  shift2Start: '14:26',
  shift2End:   '16:38',
  shift3Start: '16:49',
  shift3End:   '18:53',
  shift4Start: '19:04',
  shift4End:   '21:16',
  shift5Start: '21:28',
  shift5End:   '23:39',
  break:       '00:29',
} as const;
