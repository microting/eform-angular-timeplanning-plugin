# Design: Planning day-cell presentation (duration formatting + thin icons)

**Date:** 2026-06-09
**Plugin:** eform-angular-timeplanning-plugin (Angular frontend)
**Dev mode:** Base dev mode — edit in the `eform-angular-frontend` host-app mirror, then
`devgetchanges.sh` back to the source repo. SCSS is centralized in `eform-angular-frontend`.
**Mockup:** `docs/superpowers/mockups/2026-06-09-planning-day-cell-mockup.html`

## Summary

Re-style the presentation of a single day cell in the planning view
(`/plugins/time-planning-pn/planning`) to match a target mockup. This is a
**presentation-only** change: all existing rows are kept, the cell colouring logic is
unchanged, and no data/model changes are made. Two things change:

1. Every **duration value** renders as `X t Y min (Z.ZZ timer)` (Danish: `t`=hours,
   `min`=minutes, `timer`=hours), instead of bare decimals (`9.00`) or `HH:mm`.
   The **planned** pause/break and planned plan-hours render as `X t Y min` **without**
   the `(Z.ZZ timer)` decimal part.
2. The cell icons switch to the thin **Material Symbols Outlined** style to match the
   image.

Time-of-day stamps (actual start/stop, planned start/end) **stay `HH:mm`** — no seconds.

## Target & current state

The cell is the existing `#dayColumnTemplate` in
`eform-client/src/app/plugins/modules/time-planning-pn/components/plannings/time-plannings-table/time-plannings-table.component.html`
(~lines 139–335), driven by `time-plannings-table.component.ts` and styled by the global
`eform-client/src/scss/styles.scss` (`.green-background`, `.plan-container`, `.neutral-icon`, …).

Current rows (all KEPT): planned shift(s), actual shift stamps, pause total, worked/net
hours, paid-out flex, flex balance, comments, and absence icons in the right column.

## Decisions (from brainstorming)

| # | Decision |
|---|----------|
| 1 | **Keep all rows.** Presentation only; cell colour logic (`getCellClass`) unchanged. |
| 2 | **Times stay `HH:mm`** (no seconds) for both actual stamps and planned ranges. |
| 3 | **Duration format** = `X t Y min (Z.ZZ timer)`: hours part always shown (`0 t 3 min`), decimal hours to **2 places** with the word `timer`. |
| 4 | **Both parts derive from the same underlying value**, so they are always internally consistent. (The sample image's parts disagree numerically — that is unrelated dummy data and is **not** replicated.) |
| 5 | **Full format** (`X t Y min (Z.ZZ timer)`) applies to: **actual pause total, worked/net hours, flex balance, paid-out flex.** |
| 6 | **`X t Y min` only** (no decimal) applies to: **planned pause/break, planned plan-hours.** |
| 7 | **Negative** values render `-X t Y min (-Z.ZZ timer)` and are coloured **red**. |
| 8 | **Icons** switch to **Material Symbols Outlined**, thin weight (matches the image). |
| 9 | The decimal part renders in the **same colour** as the rest of the row (not greyed). |
| 10 | Labels `t` / `min` / `timer` are **translatable** (ngx-translate, per-locale dicts). |

## Format specification

A single pure formatter converts a **decimal-hours** value into the display string.

```
formatHoursDuration(hours, withDecimal):
  negative = hours < 0
  abs      = |hours|
  h        = floor(abs)
  m        = round((abs - h) * 60)
  if m == 60: m = 0; h = h + 1            # rounding carry
  sign     = negative ? "-" : ""
  base     = `${sign}${h} ${t} ${m} ${min}`            # e.g. "-0 t 32 min"
  if withDecimal:
     dec   = `(${sign}${abs.toFixed(2)} ${timer})`     # e.g. "(-0.53 timer)"
     return `${base} ${dec}`
  return base
```

- `t`, `min`, `timer` come from `translateService.instant('t' | 'min' | 'timer')`.
- **Pause** is stored in minutes (`pauseMinutes`) and **planned break** in minutes
  (`plannedBreakOfShiftN`); callers convert to hours (`minutes / 60`) before formatting,
  so both parts stay consistent (3 min → `0 t 3 min (0.05 timer)`).
- Worked hours = `nettoHoursOverrideActive ? nettoHoursOverride : actualHours`.
- Flex balance = `sumFlexEnd`; paid-out flex = `paidOutFlex` (parse to number).

### Worked examples (for tests)
| Input (hours) | withDecimal | Output |
|---|---|---|
| `3/60` (pause 3 min) | yes | `0 t 3 min (0.05 timer)` |
| `7.58` | yes | `7 t 35 min (7.58 timer)` |
| `2.0` | yes | `2 t 0 min (2.00 timer)` |
| `-0.53` | yes | `-0 t 32 min (-0.53 timer)` |
| `30/60` (planned break) | no | `0 t 30 min` |
| `0` | yes | `0 t 0 min (0.00 timer)` |
| `0.999` | yes | `1 t 0 min (1.00 timer)` (rounding carry) |

## Architecture & files

Small, focused change across four units. No new components.

**1. `time-plannings-table.component.ts`** — add formatter method(s) beside the existing
`convertHoursToTime*` converters (the established convention is component methods, not a
pipe — there is no `pipes/` dir in the module):
- `formatHoursDuration(hours: number, withDecimal: boolean): string` (the spec above).
- Thin convenience wrappers if helpful: `formatDurationFull(hours)` and
  `formatDurationShort(hours)`.
- A predicate the template already can use: a value `< 0` triggers the red class.

**2. `time-plannings-table.component.html`** (`#dayColumnTemplate`) — bindings only:
- Actual **pause** row → `formatDurationFull(pauseMinutes / 60)`.
- **Worked/net** row → `formatDurationFull(actualHours)` / override branch
  `formatDurationFull(nettoHoursOverride)`.
- **Paid-out flex** row → `formatDurationFull(+paidOutFlex)`.
- **Flex balance** row → `formatDurationFull(sumFlexEnd)`, plus `[class.red-text]="sumFlexEnd < 0"`.
- **Planned break** (within planned rows) → `formatDurationShort(plannedBreakOfShiftN / 60)`.
- **Plan-hours-only** variant → `formatDurationShort(planHours)`.
- Time ranges unchanged (`formatStamp` / `getStopTimeDisplayWithSeconds`, planned
  `convertMinutesToTime` for start/end).
- Add `fontSet="material-symbols-outlined"` to the cell's icons: `calendar_month`,
  `login`, `logout`, `pause`, `schedule`, `payments`, `swap_vert` (and `warning`, `face`,
  `gite`, and the absence icons, for a consistent thin look). Keep the `neutral-icon` class.

**3. `eform-client/src/scss/styles.scss`** (central) — minimal:
- Ensure Material Symbols Outlined renders at the same size as the current icons
  (the existing `.neutral-icon` is `font-size:20px; font-weight:200`; Material Symbols is a
  variable font so weight 200 already gives the thin look). Add
  `font-variation-settings: 'FILL' 0, 'wght' 300, 'opsz' 20;` to `.neutral-icon` (or a
  scoped class) if needed so the outlined glyphs match the mockup weight.
- Reuse existing `.red-text` for negative durations — likely no new rule needed.

**4. i18n** — add `t`, `min`, `timer` keys to every locale dict under
`eform-client/src/app/plugins/modules/time-planning-pn/i18n/*.ts`:
- `da.ts`: `t: 't'`, `min: 'min'`, `timer: 'timer'`.
- `enUS.ts`: `t: 'h'`, `min: 'min'`, `timer: 'hrs'` (English wording — confirm at review).
- Other locales: sensible equivalents (fall back to the da/en wording where unsure;
  flag low-confidence ones).

## Data flow

Unchanged. The cell still binds `row.planningPrDayModels[col.field]` (`PlanningPrDayModel`).
All values needed (`pauseMinutes`, `actualHours`, `nettoHoursOverride[Active]`, `sumFlexEnd`,
`paidOutFlex`, `plannedBreakOfShift1..5`, `planHours`) already exist on the model. Only the
**rendering** of these values changes.

## Edge cases

- **Rounding carry**: `m == 60` after rounding carries into the hour (see formatter).
- **Negative zero**: `-0.00` must show as `0 t 0 min (0.00 timer)` (not `-0…`); guard
  `hours === 0` (and the existing `normalizeFlex` `-0.00`→`0.00` behaviour informs this).
- **Paid-out flex** is a string on the model → parse with `parseFloat` (handle `,`/`.` and
  empty → 0), mirroring existing handling.
- **`useOneMinuteIntervals`** does not affect this change — times remain `HH:mm` regardless.

## Testing

- **Unit test (primary)** — a Jasmine/Karma spec for `formatHoursDuration` covering every
  row of the "Worked examples" table, including the negative, zero, planned-short (no
  decimal), and rounding-carry cases. (CI already runs `angular-unit-test`.)
- **Visual check** — run the planning view in the app and confirm a completed day matches
  the mockup (icons thin/outlined, durations formatted, times still HH:mm, negative flex
  red).
- No change to e2e parity/export tests (this is display-only and does not touch exports).

## Out of scope

- No change to which rows appear, cell colour logic, the workday dialog, or any export.
- No seconds in any timestamp.
- No new pipe or component; no model/back-end change.

## Resolved (post-review)

- English wording confirmed: `t: 'h'`, `min: 'min'`, `timer: 'hrs'`.
- **All** cell icons switch to Material Symbols Outlined (duration/shift icons AND
  `warning` / `face` / `gite` / the absence icons) for a consistent thin look.
