# PlanText parsing: decimal breaks, unambiguous times, five shifts

**Date:** 2026-09-22
**Status:** Approved design, ready for implementation plan

> **Data note.** This design was derived from a read-only survey of all 227
> production tenant schemas. Tenant identifiers are deliberately omitted from
> this document; the affected-tenant list lives outside the repository.

## Problem

`PlanText` is a free-text shift string (`"7:00-15:00/1"`, `"6-13.30/0.5"`)
written by planners into a Google Sheet or the web grid, and parsed server-side
into `PlannedStartOfShiftN` / `PlannedEndOfShiftN` / `PlannedBreakOfShiftN` and
a recomputed `PlanHours`. Four independent parsers exist and disagree.

### 1. The break cannot express more than one hour

`PlanTextHelper.BreakTimeCalculator` is a 25-entry string whitelist whose
largest key is `"1"`. Everything else falls to `_ => 0`, silently. A break of
`1.5` or `2` becomes zero minutes and `RecalculatePlanHours` then deducts
nothing, inflating `PlanHours` by the full break.

Live rows losing their break entirely today:

| token | rows | should be |
|---|---|---|
| `1.5` / `1,5` | 2,436 | 90 min |
| `1.0` / `1,0` | 642 | 60 min |
| `0,50` | 372 | 30 min |
| `2` `3` `4` `5` `6` `7` | 176 | 120–420 min |
| `2 helligdag` / `2 arbejdsdag` | 144 | 120 min |
| `½ + AT` variants | 90 | 30 min |
| `0:30` | 25 | 30 min |

**~3,900 live rows.** The token distribution is unambiguously decimal hours —
`0,50` pairs with `0,5`, `1.5` with `1`, and no tenant uses clock-minutes in the
break position.

The inverse, `MinutesToBreakString`, caps identically (`60 => "1", _ => "0"`),
so a 90-minute break already stored is serialised as `/0` and destroyed on
round-trip. Any break that is not a multiple of five suffers the same.

### 2. The same token is read two different ways

`ContentHandoverService.TryParseSegment` parses the break with
`double.TryParse(InvariantCulture)` and `Math.Round(h * 60)`. For the same
input, `"0.1"` is 5 min under `BreakTimeCalculator` and 6 min here; `"1.5"` is
0 vs 90.

Worse, `ContentHandoverService.FormatBreakAsCanonicalHours` *writes* `1.5`,
`2`, … `4.75`, with a comment acknowledging the live parser will drop them.
Handover therefore produces `PlanText` the main parser cannot read back.

### 3. The fix already exists, unreachable

`TimePlanning.Pn/Infrastructure/Helpers/PlanRegistrationHelper.cs` contains a
`private static BreakTimeCalculator` with the extended table (`"1.5" => 90` …
`"4.75" => 285`). It has **zero call sites**. `GoogleSheetHelper.cs` holds a
third, also dead. The comments in `ContentHandoverService` point readers at the
dead extended copy, which is plausibly why the live one-hour ceiling survived
review.

### 4. Time parts are ambiguous

`ParseTimeToMinutes` splits on `.`/`:`/`½` and reads the fraction as literal
minutes with no padding or validation. Consequences:

- `7.5` parses as 07:**05**, `6.3` as 06:**03** — wrong for every tenant that
  writes either form.
- `7½` yields 07:00; the glyph is consumed as a separator, not a half hour.
- `int.Parse` throws on non-numeric input, and `GoogleSheetHelper` has no
  try/catch around its row loop, so **one bad cell aborts the import for every
  remaining worker and date**.

### 5. Shifts 3, 4 and 5 are lost

In the service plugin, the assignment blocks for shifts 3–5 sit inside
`if (match.Captures.Count == 0)` — the branch taken only when the *with-break*
regex failed. A third segment carrying a break matches that regex, so the block
is skipped and `PlannedStartOfShift3/4/5` are never written. Inside the branch
that does run, `match.Groups.Count == 4` can never hold for a three-group
regex, so `PlannedBreakOfShift3/4/5` is always zero.

Separately, `PlanTextHelper.ParsePlanText` writes only as many shifts as the
input has segments, leaving stale values in the rest.

### 6. Excel import never derives shifts

`TimePlanningWorkingHoursService` stores `PlanText` from an uploaded workbook
but never calls `ParsePlanText`, so no shift or break fields are derived.

## Evidence summary

Full sweep of 227 tenant schemas, live rows only (`WorkflowState <> 'removed'`):

| bucket | rows |
|---|---|
| Rows carrying `PlanText` | 109,820 |
| Colon-style times (`7:30`) | 33,962 |
| Two-digit fraction, non-zero minutes (`7.30`) | 27,449 |
| Single-digit fraction, ranged | 455 |
| Bare values with no range | 129 |

The two-digit population is dominated by `.30` / `.45` / `.15` — a quarter-hour
*clock* distribution, with end times like `16.00` and `13.30`. These are HH.MM,
not decimal; reading them as decimal would move 27,449 rows (25% of all
`PlanText`), 7,126 of them future-dated, across 33 tenants.

In ranged position only three single digits occur anywhere: `,0` (66), `,3`
(326), `,5` (63). Two tenants use opposite shorthands for the same clock time —
one writes `,3` for `:30` (dropped trailing zero), the other `,5` for `:30`
(decimal half hour). Both are wrong today (`06:03`, `13:05`).

## Design

### Grammar

```
planText  := segment { ";" segment }            up to 5 segments
segment   := time "-" time [ "/" break ]
time      := hours [ sep fraction ] | hours "½"
sep       := ":" | "." | ","
hours     := 1..2 digits
fraction  := 1..2 digits
break     := decimal | "½" | "¾" | hours ":" minutes
```

A segment **must contain `-`**. A string without one is not a shift and is not
parsed; bare numeric values are plan hours, not times, and are ignored here.

### Time resolution

| form | rule | example |
|---|---|---|
| `H:MM` | clock minutes, `MM` must be 00–59 | `7:30` → 07:30 |
| `H.MM` / `H,MM`, `MM` ≤ 59 | clock minutes | `7.30` → 07:30 |
| `H.MM` / `H,MM`, `MM` > 59 | **non-conforming**, segment discarded | `7.75` → — |
| `H.D` / `H,D`, D ∈ {0} | `:00` | `7.0` → 07:00 |
| `H.D` / `H,D`, D ∈ {3, 5} | `:30` | `6.3`, `7,5` → 06:30 / 07:30 |
| `H.D` / `H,D`, other D | **non-conforming**, segment discarded | `7,4` → — |
| `H½` | `:30` | `7½` → 07:30 |
| `H` | `:00` | `8` → 08:00 |

The single-digit table covers 100% of the observed corpus. Digits outside it
have zero occurrences and are rejected rather than guessed, so a new convention
surfaces as a warning instead of a silent wrong answer.

`,3` and `,5` both resolving to `:30` is intentional: they are two shorthands
for the same clock time, used by different tenants, and the data contains no
counterexample.

### Break resolution

Decimal hours throughout, parsed arithmetically:

```
normalise ',' -> '.'
"½" -> 30   "¾" -> 45
"H:MM" -> H*60 + MM
otherwise double.TryParse(NumberStyles.Float, CultureInfo.InvariantCulture)
         -> (int)Math.Round(hours * 60)
unparseable -> 0
```

No upper bound. `1.5` → 90, `2` → 120, `0,50` → 30, `0.75` → 45.

### Salvage

Junk around a conforming token is stripped, not fatal. Each `;`-separated
segment is scanned for the first conforming `time-time[/break]` token;
surrounding text is discarded. A segment with no conforming token yields no
shift. **Nothing in the parse path throws.**

This turns `2 helligdag` into a 120-minute break, `½ + AT` into 30, and
`8-16 hjemme` into an 08:00–16:00 shift, instead of zero or an exception.

### Shifts

All five shift triples are cleared first, then populated from the segments
present, so removing a segment clears its shift rather than leaving a stale
value. Segment *n* always populates shift *n*, whether or not it carries a
break.

### Emission

`GeneratePlanText` emits `H:MM` for times — already the case, and unambiguous
under this grammar — and the break as invariant decimal hours (`0.5`, `1.5`,
`2`), `0` when absent. Round-trip is exact for every representable value, which
the current `MinutesToBreakString` cannot manage above 60 minutes or off the
five-minute grid.

### Single implementation

The parser moves to `Microting.TimePlanningBase`
(`Infrastructure/Helpers/`), which both the plugin and the service plugin
already reference. The plugin's `PlanTextHelper`, the service plugin's seven
inline copies, and the two dead `BreakTimeCalculator` tables are deleted in
favour of it. `ContentHandoverService` calls the same helper instead of its
private `TryParseSegment` / `FormatBreakAsCanonicalHours`.

This is the change that prevents the four copies from diverging again, and it
is the reason the defect survived: the corrected table was added to a copy
nobody called.

> Editing `eform-timeplanning-base` requires Full dev mode or no dev mode. In
> Base dev mode the base is a NuGet reference and this step is not possible;
> the fallback is to keep the helper in the plugin and have the service plugin
> retain a copy, accepting the divergence risk.

## Behaviour changes on live data

| change | rows | direction |
|---|---|---|
| Breaks above one hour now deducted | ~2,400 | `PlanHours` decreases |
| `1.0` / `0,50` / `1,0` breaks now deducted | ~1,000 | `PlanHours` decreases |
| Junk-suffixed breaks now parsed | ~230 | `PlanHours` decreases |
| `,3` / `,5` times corrected | ~390 | start/end move up to 27 min |
| Two-digit times (`7.30`) | 27,449 | **unchanged** |
| Colon times | 33,962 | **unchanged** |

`PlanHours` decreasing is the correction, not a regression: those rows have been
over-reporting planned time by the full length of an unparsed break.

## Testing

`PlanTextHelperTests` currently stops at `[TestCase("1", 60)]` — no case above
one hour, no negative case. That gap is why this shipped.

New coverage, driven by the real corpus:

- Break: `1.5`, `1,5`, `1.0`, `2`, `0,50`, `0.75`, `½`, `¾`, `0:30`,
  `2 helligdag`, `½ + AT`, `0`, empty, garbage.
- Time: every row of the resolution table, both separators, plus `7½`.
- Rejection: `7,4`, `7.75`, `8.99`, bare `7,5`, a string with no `-`.
- Salvage: `8-16 hjemme`, `ferie 7,5-15,5`, `ferie`.
- Five shifts: five segments with and without breaks; a shortened string
  clearing trailing shifts.
- Round-trip: parse → generate → parse for every representable break.
- Regression corpus: the distinct `PlanText` shapes observed in production,
  asserted against their intended clock times.

Service-plugin tests get the shift 3/4/5 cases that currently pass only because
nothing asserts those fields.

> New test classes must be added to the shard filters in **both** CI workflow
> files, or they silently do not run.

## Out of scope

- Migrating stored `PlanText`. The sheet remains the source of truth and would
  re-import the old text on the next pull, so a migration cannot hold.
- The `Pause{N}Id` tick encoding (`(id-1) * 5`), which is a separate mechanism
  and already handles durations above an hour correctly.
- `PauseMinutesCalculator.DerivePauseId` returning `totalMinutes / 5` without
  the `+1` every decoder subtracts. Real, documented as intentional, unrelated.
- Excel-import deriving shifts. Listed as defect 6; it is a behaviour addition
  rather than a parsing correction and should be decided separately.
