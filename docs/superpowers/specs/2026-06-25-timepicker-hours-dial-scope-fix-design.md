# Timepicker hours-dial regression — scoped 5-minute-label fix

**Date:** 2026-06-25
**Status:** design approved, pending implementation
**Branch:** `fix/timepicker-hours-dial-scope` off `stable`
**Fixes:** regression introduced by #1625 (`feat(planning): show only 5-min labels on one-minute timepicker`), live on `stable`.

## Problem

In one-minute mode the workday-entity dialog's `ngx-material-timepicker` (v13.1.1) **HOURS** dial shows only hours 1, 6, 11 — the rest are hidden. The 5-minute-minute-label feature's CSS leaks onto the hours face.

### Root cause

The feature thins minute labels with:

```scss
.timepicker.timepicker--hide-non-multiples-of-5 .clock-face__number--outer:not(:nth-child(5n+1)) > span { visibility: hidden; }
```

applied via `[timepickerClass]` on the persistent overlay root `.timepicker`. Both the **hours** outer ring (12 cells) and the **minutes** ring (60 cells) use the same class `clock-face__number--outer`, and the root `.timepicker` is present on both faces. On the 12-cell hours dial, `:nth-child(5n+1)` keeps positions 1/6/11 → hours 1, 6, 11; all other hours are hidden via `visibility:hidden`. It only works for minutes by coincidence of count (60 cells → minutes 0,5,…,55). The 24h inner ring uses `clock-face__number--inner`, so it is unaffected.

The library has no `minutesGap`-style hook that separates *labels* from *selection* (gap=5 would also snap selection to 5-minute steps), so CSS thinning is the correct strategy — it just must be scoped to the minute face. The library renders only the **active** face into the DOM (`*ngSwitchCase`), so the minute face is identifiable by having more than 12 outer cells.

## Requirements

- **Hours dial:** every hour visible and selectable (full 1–12 outer ring + 00/13–23 inner ring) — stock Material behavior, untouched.
- **Minute dial:** only labels for multiples of 5 (0,5,10,…,55) shown; **every** minute still selectable (1-minute precision preserved).
- Applies only when the tenant is in one-minute mode (the existing `[timepickerClass]` binding gate is unchanged).

## Design

### 1. Scope the selector to the minute face (`:has()`)

Gate the thinning behind a `:has()` that matches only the minute face — keyed on the minute face having a 13th outer cell, which the hours outer ring never has:

```scss
.timepicker.timepicker--hide-non-multiples-of-5
  .clock-face:has(.clock-face__number--outer:nth-child(13))
  .clock-face__number--outer:not(:nth-child(5n+1)) > span { visibility: hidden; }
```

The exact ancestor element (`.clock-face` vs the numbers' direct container) and the `:has()` predicate will be confirmed against the live rendered DOM during implementation; the count-based `:nth-child(13)` predicate is format-independent (the hours outer ring is always 12 cells in both 12h and 24h formats). `:has()` is supported in all current evergreen browsers — acceptable for this internal admin app. The CI test (below) validates the chosen selector against both dials, so a wrong guess cannot ship.

### 2. Rule location — kept in the component (as built)

The original feature spec wanted the rule in a global stylesheet. On investigation that is not appropriate here: the **plugin** repo owns no global stylesheet — the angular.json global styles (`src/scss/styles.scss`, `theme.scss`) live in the **host** repo (`eform-angular-frontend`), and `devinstall.sh` only ships the plugin module dir + DLL + e2e tests, never host globals. Moving the rule there would disconnect it from the plugin's release pipeline. So the rule stays in `time-plannings-table.component.scss`, which is emitted globally via `ViewEncapsulation.None` (the existing mechanism that already reaches the body-appended overlay), with a comment documenting both the minute-face scoping and this ownership rationale.

### 3. Harden the Playwright test

The current test (`b1m/dashboard-edit-a.spec.ts`) clicks an hour *first*, then asserts label visibility — so it never inspects the hours dial while open, which is why it missed the regression. Update it to:

1. Open the picker (starts on the **hours** dial) and assert **all 12** hour outer-ring labels are visible (`toBeVisible` for each).
2. Click an hour to move to the minute dial; assert only multiples of 5 are visible and a non-multiple (e.g. minute 23) is hidden.
3. Confirm a non-multiple minute is still **selectable** (round-trips to a value like `09:23`).

## Out of scope (YAGNI)

- No library fork, no TypeScript, no change to selection behavior.
- The six `[timepickerClass]` bindings stay as-is.
- The stale `feat/planning-timepicker-5min-labels` branch is abandoned (its content shipped via #1625).

## Verification

- Playwright spec asserts both dials (hours intact + minutes thinned + non-5 minute selectable).
- Manual: open the workday dialog on a one-minute tenant, confirm the hours dial shows every hour and the minute dial shows only 0/5/…/55.
