# Planning Timepicker — show 5-minute labels when UseOneMinuteIntervals=true

- **Date:** 2026-06-24
- **Status:** Approved design, ready for implementation plan
- **Repo:** `eform-angular-timeplanning-plugin` (Angular web client `eform-client`)
- **Surface:** web admin planning page `plugins/time-planning-pn/planning`

## 1. Problem

On the planning page, shift start/stop/break times are entered via `ngx-material-timepicker` (clock face). When the row's `AssignedSite` has `UseOneMinuteIntervals=true`, the picker is configured `[minutesGap]="1"`, so the clock face renders **all 60 minute labels (0–59)**. This clutters the clock and renders badly. We want the clock to **show only the 5-minute marks (0,5,…,55)** while **still allowing selection of any minute** (1-minute precision must be preserved — admins enter exact punch-aligned times).

## 2. Constraint that shapes the design

`ngx-material-timepicker` v13.1.1 couples display and selection: `minutesGap` controls *both* which labels render *and* which minutes are selectable/valid (typed off-grid values are rejected/snapped). So "show 5 / select any" cannot be achieved by changing `minutesGap` or via keyboard input — it requires keeping `minutesGap=1` (all 60 selectable) and hiding the unwanted **labels** visually.

## 3. Design

Two small, scoped changes in `components/plannings/time-planning-actions/workday-entity/workday-entity-dialog.component`.

### 3a. Template — tag the flag-on pickers
Keep `[minutesGap]="useOneMinuteIntervals ? 1 : 5"` unchanged (flag-on stays `gap=1` → all 60 minutes selectable). Add to each of the **6** `<ngx-material-timepicker>` instances (planned + actual × start/stop/break):
```html
[timepickerClass]="useOneMinuteIntervals ? 'timepicker--hide-non-multiples-of-5' : ''"
```
`timepickerClass` (a v13.1.1 input) applies the class to the overlay's root `.timepicker` element, per-instance. So only flag-on pickers get the marker class; `gap=5` pickers (and every other timepicker in the app) get no class and are unaffected.

### 3b. Global stylesheet — hide non-5 labels on tagged pickers
The picker overlay is appended to `<body>` (outside the component's view), so the rule must live in a **global** stylesheet, scoped by the unique marker class:
```scss
.timepicker.timepicker--hide-non-multiples-of-5 .clock-face__number--outer:not(:nth-child(5n+1)) > span {
  visibility: hidden;
}
```
- `visibility: hidden` (NOT `display: none`) — keeps each minute item's box, angle transform, and click target intact, so selection still works.
- `nth-child(5n+1)` selects the minute items at DOM positions 1,6,11,…,56. With `minutesGap=1` the library renders all 60 items in minute order (index = minute), so `5n+1` ⇒ minutes 0,5,…,55 are kept; all others have their label `<span>` hidden.
- Place in the eform-client's global styles (the stylesheet that already holds global timepicker/overlay overrides). Do NOT rely on component `::ng-deep` — the overlay is body-appended and the component's encapsulation attribute won't be on it.

## 4. Behavior

- **Flag-on (`UseOneMinuteIntervals=true`):** clock shows only `0,5,…,55` labels; clicking between marks lands on the exact minute; keyboard entry accepts any minute; off-grid times (e.g. `09:23`) select and save normally.
- **Flag-off (`false`):** unchanged (`minutesGap=5`, no marker class).
- Any other `ngx-material-timepicker` elsewhere in the app: unchanged (no marker class).

## 5. Testing

- **Selection is angle-based** in the library, so the existing position-based playwright `pickTime()` helper (used by the `b1m`/`c1m`/… off-grid shards) is **unaffected** — off-grid minutes still land correctly. No e2e helper rewrite.
- Add a focused e2e/component check on the flag-on planning picker: (a) a non-multiple-of-5 minute (e.g. `09:23`) can still be selected and persisted; (b) the non-5 minute labels are visually hidden (e.g. the relevant `clock-face__number--outer > span` have `visibility: hidden` / are not visible) while the `0,5,…,55` labels are visible.
- Existing `workday-entity-dialog.component.spec.ts` stays; extend if a unit-level assertion on `timepickerClass` binding is cheap.

## 6. Scope / non-goals

- Web-admin planning picker only. The flutter-time mobile picker is out of scope.
- No change to selection granularity (stays 1-minute when flag-on) or to `minutesGap` logic.
- No accessibility rework: hidden labels remain in the DOM; keyboard entry covers any minute (accepted trade-off).
- No library upgrade/fork.

## 7. Risks

- The `nth-child(5n+1)` selector depends on `ngx-material-timepicker` rendering the 60 minute items in order — verified for **v13.1.1**. Pin the version; re-verify the selector + `.clock-face__number--outer` structure on any timepicker upgrade.
- `timepickerClass` must be confirmed to land on the overlay `.timepicker` root in v13.1.1 (verified in design investigation).
