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

/**
 * e1m shard variant: early-morning small-hour boundaries that sweep the
 * outer-ring 1-10 (and 11-12) hour selectors of the timepicker clock face,
 * deliberately crossing the inner→outer boundary at hour 12. b1m sweeps a
 * narrower mid-morning band (08-16), c1m straddles outer→inner around 17-19,
 * d1m parks inside the inner ring (13-23). e1m mirrors d1m on the opposite
 * side of the clock by sweeping the small-hour outer ring (01-10) so the
 * variant matrix as a whole touches every quadrant of the 24-hour clock
 * surface. Every value is non-aligned to 5 minutes so the flag-on
 * `minutesGap=1` rendering is the only way the picker can land on these
 * values.
 *
 * Same shift-order constraint as `OFFGRID_TIMES` above:
 * shift{n+1}.start MUST be >= shift{n}.stop.
 */
export const OFFGRID_TIMES_E1M = {
  shift1Start: '01:03',
  shift1End:   '02:17',
  shift2Start: '02:29',
  shift2End:   '04:41',
  shift3Start: '04:52',
  shift3End:   '06:58',
  shift4Start: '07:09',
  shift4End:   '09:21',
  shift5Start: '09:34',
  shift5End:   '11:46',
  break:       '00:31',
} as const;

/**
 * f1m shard variant: validation-pair time literals for the SAVE-FAILURE
 * (negative path) tests cloned from `f/dashboard-edit-a.spec.ts` — i.e.
 * "stop before start", "same start/stop" and "pause longer than shift"
 * checks. Where b1m/c1m/d1m/e1m sweep different quadrants of the clock
 * with ascending 5-shift round-trip data, f1m's spec only fills shift 1
 * with single-shift pairs (and a midnight-wrap math test). The contract
 * that matters for these tests is the INVALID RELATIONSHIP between the
 * paired times — not their position on the clock face — so each pair
 * here intentionally preserves the same negative-path semantics as the
 * legacy `f` shard's `'10:00' / '9:00'`-style values, just shifted to
 * off-grid (non-multiple-of-5) minutes so the flag-on `minutesGap=1`
 * picker is the only way to land on them.
 *
 * Math-friendly midnight pair (`midnight*` keys below) lands on a clean
 * fractional planHours: `00:00 → 02:24` ⇒ 144 min ⇒ planHours = 2.4 (and
 * the mirror `02:24 → 00:00` ⇒ 1296 min ⇒ planHours = 21.6) so the
 * spec's `toHaveValue` assertions stay deterministic without float
 * pretty-printing.
 *
 * Invalid-relationship checks (no shift-order constraint here — these
 * pairs INTENTIONALLY violate constraints to trigger validators):
 *   • plannedStopBefore  : start > stop
 *   • plannedSameTime    : start === stop (both nonzero)
 *   • actualStopBefore   : start > stop
 *   • actualPauseTooLong : pause >= (stop - start)
 *   • actualSameTime     : start === stop (both nonzero)
 */
export const OFFGRID_TIMES_F1M = {
  // Planned: stop-before-start (10:23 > 09:17).
  plannedStopBefore: { start: '10:23', stop: '09:17' },
  // Planned: same-start-stop (both 09:43, both nonzero).
  plannedSameTime: { start: '09:43', stop: '09:43' },
  // Actual: stop-before-start (11:29 > 09:11), pause kept at 00:00.
  actualStopBefore: { start: '11:29', stop: '09:11', pause: '00:00' },
  // Actual: pause equal to shift duration (boundary case). 10:31 - 08:13 =
  // 138 min; pause = 02:18 = 138 min ⇒ `breakMin >= duration` (138 ≥ 138)
  // trips `breakTooLong`. Mirrors the legacy `f` shard which uses pause =
  // shift-duration exactly (8:00-10:00 / 2:00 ⇒ 120 ≥ 120). The pause
  // input has `[max]=getMaxDifference(start,stop)` so the timepicker caps
  // selection at the duration; picking AT the max equals the boundary
  // and fires the validator. Off-grid: 18 mod 5 = 3 ≠ 0.
  actualPauseTooLong: { start: '08:13', stop: '10:31', pause: '02:18' },
  // Actual: same-start-stop (both 09:43, both nonzero), pause 00:00.
  actualSameTime: { start: '09:43', stop: '09:43', pause: '00:00' },
  // Math: midnight → off-grid hour. 00:00 → 02:24 ⇒ 144 min plan ⇒ 2.4 h.
  midnightToHours: { start: '00:00', stop: '02:24' },
  // Math: off-grid hour → midnight. 02:24 → 00:00 ⇒ 1296 min plan ⇒ 21.6 h.
  hoursToMidnight: { start: '02:24', stop: '00:00' },
  // Computed expectations for the math tests above.
  midnightToHoursPlan: '2.4',
  hoursToMidnightPlan: '21.6',
  zeroFlex: '0.00',
} as const;
