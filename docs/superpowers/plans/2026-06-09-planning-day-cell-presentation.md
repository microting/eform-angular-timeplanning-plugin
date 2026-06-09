# Planning Day-Cell Presentation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Re-style the planning day-cell so every duration renders as `X t Y min (Z.ZZ timer)` (planned pause/plan-hours without the decimal), times stay HH:mm, and all cell icons use the thin Material Symbols Outlined style.

**Architecture:** Presentation-only change in the existing `#dayColumnTemplate` of `time-plannings-table.component`. One pure formatter method on the component does all duration formatting; the template rebinds the duration rows to it and adds `fontSet="material-symbols-outlined"` to every cell icon; three translatable labels (`t`/`min`/`timer`) are added to the module i18n dicts. No model, data-flow, cell-colour, or SCSS changes.

**Tech Stack:** Angular, `@ngx-translate/core`, Angular Material `mat-icon` (Material Symbols Outlined font, already loaded in `index.html`), Jasmine/Karma unit tests (CI `angular-unit-test`).

**Dev mode:** Base dev mode — edit in the host-app mirror
`/home/rene/Documents/workspace/microting/eform-angular-frontend/eform-client/src/app/plugins/modules/time-planning-pn/`,
then `devgetchanges.sh` back to the source plugin repo for committing. Do NOT touch
`eform-angular-frontend/eform-client/src/scss` (no SCSS change in this plan).

**Spec:** `docs/superpowers/specs/2026-06-09-planning-day-cell-presentation-design.md`
**Mockup:** `docs/superpowers/mockups/2026-06-09-planning-day-cell-mockup.html`

**Key files (host-app mirror):**
- TS: `components/plannings/time-plannings-table/time-plannings-table.component.ts`
- HTML: `components/plannings/time-plannings-table/time-plannings-table.component.html` (`#dayColumnTemplate`, lines ~139–335)
- Spec: `components/plannings/time-plannings-table/time-plannings-table.component.spec.ts` (exists)
- i18n: `i18n/*.ts` (26 locale dicts)

---

## Task 1: Add the `formatDuration` formatter (TDD)

**Files:**
- Modify: `…/time-plannings-table.component.ts` (add method beside `convertHoursToTime`, ~line 446)
- Test: `…/time-plannings-table.component.spec.ts`

The formatter takes a decimal-hours value (or numeric string, e.g. paid-out flex) and returns
`X t Y min (Z.ZZ timer)`, or `X t Y min` when `withDecimal` is false. Both the h/min part and
the decimal part derive from the same rounded total-minutes so they are always consistent.
Labels come from `translateService.instant('t' | 'min' | 'timer')`.

- [ ] **Step 1: Write the failing unit tests**

Add to `time-plannings-table.component.spec.ts`. This reuses the spec's existing component
setup; it spies on `translateService.instant` so labels resolve to `t`/`min`/`timer`
regardless of the test's configured language.

```ts
describe('formatDuration', () => {
  beforeEach(() => {
    // component + translateService are created by the existing spec setup (TestBed).
    spyOn(component['translateService'], 'instant').and.callFake((key: string) => key);
  });

  it('formats a sub-hour duration with decimal', () => {
    expect(component.formatDuration(3 / 60)).toBe('0 t 3 min (0.05 timer)');
  });

  it('formats hours + minutes with decimal', () => {
    expect(component.formatDuration(7.58)).toBe('7 t 35 min (7.58 timer)');
  });

  it('shows a whole-hour value with zero minutes', () => {
    expect(component.formatDuration(2)).toBe('2 t 0 min (2.00 timer)');
  });

  it('formats a negative duration with a leading minus on both parts', () => {
    expect(component.formatDuration(-0.53)).toBe('-0 t 32 min (-0.53 timer)');
  });

  it('omits the decimal part when withDecimal is false', () => {
    expect(component.formatDuration(30 / 60, false)).toBe('0 t 30 min');
  });

  it('renders zero as a non-negative zero', () => {
    expect(component.formatDuration(0)).toBe('0 t 0 min (0.00 timer)');
    expect(component.formatDuration(-0.0001)).toBe('0 t 0 min (0.00 timer)');
  });

  it('carries rounding into the hour', () => {
    expect(component.formatDuration(0.999)).toBe('1 t 0 min (1.00 timer)');
  });

  it('parses a numeric string (paid-out flex) and handles comma decimals', () => {
    expect(component.formatDuration('2,00')).toBe('2 t 0 min (2.00 timer)');
  });

  it('treats null/NaN as zero', () => {
    expect(component.formatDuration(null as any)).toBe('0 t 0 min (0.00 timer)');
  });
});
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `cd /home/rene/Documents/workspace/microting/eform-angular-frontend/eform-client && npx ng test --watch=false --include='**/time-plannings-table/time-plannings-table.component.spec.ts'`
Expected: FAIL — `component.formatDuration is not a function`.

> If that `--include` glob isn't supported by the project's test runner, run the project's
> standard unit-test command (check `package.json` scripts, e.g. `yarn test` / `npm test`)
> and locate the `formatDuration` describe in the output. Do not change the runner config.

- [ ] **Step 3: Implement `formatDuration`**

Add to `time-plannings-table.component.ts` (right after `convertHoursToTime`, ~line 446):

```ts
  /**
   * Formats a decimal-hours value as "X t Y min (Z.ZZ timer)".
   * Both parts derive from the same rounded total-minutes, so they are always
   * internally consistent. When withDecimal is false the "(Z.ZZ timer)" part is
   * omitted (used for planned pause/break and planned plan-hours).
   * Accepts a number or a numeric string (paid-out flex may use a comma decimal).
   */
  formatDuration(hours: number | string | null | undefined, withDecimal: boolean = true): string {
    const parsed = typeof hours === 'string'
      ? parseFloat(hours.replace(',', '.'))
      : hours;
    const safe = (parsed === null || parsed === undefined || isNaN(parsed as number))
      ? 0
      : Number(parsed);

    const totalMinutes = Math.round(Math.abs(safe) * 60);
    const negative = safe < 0 && totalMinutes > 0;
    const hrs = Math.floor(totalMinutes / 60);
    const mins = totalMinutes % 60;
    const sign = negative ? '-' : '';

    const t = this.translateService.instant('t');
    const min = this.translateService.instant('min');
    const base = `${sign}${hrs} ${t} ${mins} ${min}`;
    if (!withDecimal) {
      return base;
    }
    const timer = this.translateService.instant('timer');
    const decimal = (totalMinutes / 60).toFixed(2);
    return `${base} (${sign}${decimal} ${timer})`;
  }
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `cd /home/rene/Documents/workspace/microting/eform-angular-frontend/eform-client && npx ng test --watch=false --include='**/time-plannings-table/time-plannings-table.component.spec.ts'`
Expected: PASS (all `formatDuration` specs green).

- [ ] **Step 5: Do not commit** (base dev mode — changes are synced + committed once at the end, Task 5).

---

## Task 2: Rebind the duration rows in the template

**Files:**
- Modify: `…/time-plannings-table.component.html` (`#dayColumnTemplate`)

Replace each duration binding. Time-range bindings (`convertMinutesToTime` for planned
start/end, `formatStamp`/`getStopTimeDisplayWithSeconds` for actual stamps) stay unchanged.

- [ ] **Step 1: Planned shift-1 break → no-decimal format (two blocks)**

There are two near-identical shift-1 planned blocks (the `plannedStartOfShift1 !== 0` block and
the `plannedStartOfShift1 === 0 && plannedEndOfShift1 !== 0` block). In BOTH, the third line is:

```html
            {{ convertMinutesToTime(row.planningPrDayModels[col.field]?.plannedBreakOfShift1) }}
```

Change that line (in both blocks) to:

```html
            {{ formatDuration(row.planningPrDayModels[col.field]?.plannedBreakOfShift1 / 60, false) }}
```

- [ ] **Step 2: Planned plan-hours-only → no-decimal format**

In the `planHours !== 0` block, change:

```html
            {{ convertHoursToTime(row.planningPrDayModels[col.field]?.planHours) }}
```
to:
```html
            {{ formatDuration(row.planningPrDayModels[col.field]?.planHours, false) }}
```

- [ ] **Step 3: Planned shift 2–5 breaks → no-decimal format**

For each of shifts 2,3,4,5 the planned block's third line is:
```html
          {{ convertMinutesToTime(row.planningPrDayModels[col.field]?.plannedBreakOfShiftN) }}
```
Change each (N = 2,3,4,5) to:
```html
          {{ formatDuration(row.planningPrDayModels[col.field]?.plannedBreakOfShiftN / 60, false) }}
```

- [ ] **Step 4: Pause total → full format**

Change:
```html
<span id="totalBreakTime{{index}}_{{col.field}}" [matTooltip]="'Total breaktime' | translate "><mat-icon class="neutral-icon">pause</mat-icon>{{ convertMinutesToTime(row.planningPrDayModels[col.field]?.pauseMinutes) }}</span>
```
to:
```html
<span id="totalBreakTime{{index}}_{{col.field}}" [matTooltip]="'Total breaktime' | translate "><mat-icon class="neutral-icon">pause</mat-icon>{{ formatDuration(row.planningPrDayModels[col.field]?.pauseMinutes / 60) }}</span>
```

- [ ] **Step 5: Worked hours + netto override → full format**

Change `{{ row.planningPrDayModels[col.field]?.actualHours.toFixed(2) }}` to
`{{ formatDuration(row.planningPrDayModels[col.field]?.actualHours) }}`,
and `{{ row.planningPrDayModels[col.field]?.nettoHoursOverride.toFixed(2) }}` to
`{{ formatDuration(row.planningPrDayModels[col.field]?.nettoHoursOverride) }}`.

- [ ] **Step 6: Paid-out flex → full format**

Change `{{ row.planningPrDayModels[col.field]?.paidOutFlex }}` to
`{{ formatDuration(row.planningPrDayModels[col.field]?.paidOutFlex) }}`.

- [ ] **Step 7: Flex balance → full format + red when negative**

Change:
```html
<span id="flexBalanceToDate{{index}}_{{col.field}}" [matTooltip]="'Flex balance to date' | translate "><mat-icon class="neutral-icon">swap_vert</mat-icon>{{ normalizeFlex(row.planningPrDayModels[col.field]?.sumFlexEnd) }}</span>
```
to:
```html
<span id="flexBalanceToDate{{index}}_{{col.field}}" [class.red-text]="row.planningPrDayModels[col.field]?.sumFlexEnd < 0" [matTooltip]="'Flex balance to date' | translate "><mat-icon class="neutral-icon">swap_vert</mat-icon>{{ formatDuration(row.planningPrDayModels[col.field]?.sumFlexEnd) }}</span>
```
(`.red-text` already exists in the global stylesheet — no SCSS change.)

- [ ] **Step 8: Build to verify the template compiles**

Run: `cd /home/rene/Documents/workspace/microting/eform-angular-frontend/eform-client && npx ng build --configuration development 2>&1 | tail -20`
Expected: build succeeds (no template/AOT errors). (If the project uses a custom build script, prefer `yarn build` per `package.json`.)

---

## Task 3: Switch all cell icons to Material Symbols Outlined

**Files:**
- Modify: `…/time-plannings-table.component.html` (`#dayColumnTemplate`)

Add `fontSet="material-symbols-outlined"` to every `<mat-icon>` inside `#dayColumnTemplate`
that does not already have it. The Material Symbols Outlined font is already loaded globally
(`index.html`); `clock_arrow_down` (message === 11) already uses it. Keep all existing classes
(`neutral-icon`, `blue-text`, `red-text`) and tooltips.

- [ ] **Step 1: Add fontSet to the left-content icons**

For each of these glyphs in the left `.plan-content` (planned `calendar_month`; actual
`login`, `logout`, `warning`; summary `pause`, `schedule`, `payments`, `swap_vert`; comments
`face`, `gite`), add `fontSet="material-symbols-outlined"` to the `<mat-icon>` tag, e.g.:

```html
<mat-icon fontSet="material-symbols-outlined" class="neutral-icon" [matTooltip]="'Worktime start' | translate ">login</mat-icon>
```

Apply the same to: `calendar_month` (all planned blocks), `login`/`logout`/`warning` (all five
actual-shift blocks), `pause`, `schedule` (both override branches), `payments`, `swap_vert`,
`face`, `gite`.

- [ ] **Step 2: Add fontSet to the right-column absence icons**

In `.plan-icons`, add `fontSet="material-symbols-outlined"` to the message icons that lack it:
`flight` (2), `pregnant_woman` (10), `sick` (3/7/8), `event_busy` (1 and 5), `school` (4),
`outdoor_grill` (9), `luggage` (12). (`clock_arrow_down` for message 11 already has it.)

- [ ] **Step 3: Build, then visually verify the glyphs render**

Run: `cd /home/rene/Documents/workspace/microting/eform-angular-frontend/eform-client && npx ng build --configuration development 2>&1 | tail -20`
Expected: build succeeds.

Visual check (Task 5 / browser): every icon must render as a glyph, not as literal text. If any
glyph name does not exist in Material Symbols Outlined (it would show as a word), revert that one
icon to its default font (remove `fontSet`) and note it. (`calendar_month`, `login`, `logout`,
`warning`, `pause`, `schedule`, `payments`, `swap_vert`, `face`, `gite`, `flight`,
`pregnant_woman`, `sick`, `event_busy`, `school`, `outdoor_grill`, `luggage` all exist in
Material Symbols Outlined, so this should not be needed.)

---

## Task 4: Add `t` / `min` / `timer` translation keys

**Files:**
- Modify: all 26 dicts in `…/time-planning-pn/i18n/*.ts`

Add three keys to each locale dict. Insert them next to existing entries (anywhere in the
object literal). The Danish and English values are confirmed; the other locales use English
fallback values (`h` / `min` / `hrs`) as an interim — flag them for native review.

- [ ] **Step 1: Danish — `i18n/da.ts`**

```ts
  t: 't',
  min: 'min',
  timer: 'timer',
```

- [ ] **Step 2: English — `i18n/enUS.ts`**

```ts
  t: 'h',
  min: 'min',
  timer: 'hrs',
```

- [ ] **Step 3: All other locales — interim English fallback**

Add the same three keys to each of: `bgBG.ts, csCZ.ts, deDE.ts, elGR.ts, esES.ts, etET.ts,
fiFI.ts, frFR.ts, hrHR.ts, huHU.ts, isIS.ts, itIT.ts, ltLT.ts, lvLV.ts, nlNL.ts, noNO.ts,
plPL.ts, ptBR.ts, ptPT.ts, roRO.ts, skSK.ts, slSL.ts, svSE.ts, ukUA.ts` using:

```ts
  t: 'h',
  min: 'min',
  timer: 'hrs',
```

> These 24 are interim English fallbacks pending native-speaker review (so no raw key like
> `timer` is ever displayed). Note this in the PR description.

- [ ] **Step 4: Build to verify the dicts compile**

Run: `cd /home/rene/Documents/workspace/microting/eform-angular-frontend/eform-client && npx ng build --configuration development 2>&1 | tail -20`
Expected: build succeeds (no TS errors in the i18n files).

---

## Task 5: Verify, code-review, and ship (normal cycle)

- [ ] **Step 1: Unit tests pass**

Run the unit tests for the component spec (as in Task 1 Step 4). Expected: all `formatDuration`
specs pass and no existing specs in that file regress.

- [ ] **Step 2: Visual check in the running app**

Open `/plugins/time-planning-pn/planning`, select a worker + a past date range, and confirm a
completed day matches the mockup: durations show `X t Y min (Z.ZZ timer)` (planned break/plan-hours
show `X t Y min` only), times are still HH:mm, icons are thin/outlined, and a negative flex shows
red. Confirm no icon renders as literal text.

- [ ] **Step 3: Code review**

Use `superpowers:requesting-code-review` on the diff (component TS + HTML + i18n + spec).

- [ ] **Step 4: Sync to source repo**

From the source plugin repo `/home/rene/Documents/workspace/microting/eform-angular-timeplanning-plugin`:
run `./devgetchanges.sh`, then `git checkout -- '*.csproj' '*.conf.ts' '*.xlsx' '*.docx'` (discard
dev-mode/build artifacts), then `git status`. Confirm only the intended files changed:
`components/plannings/time-plannings-table/time-plannings-table.component.{ts,html,spec.ts}`
and `i18n/*.ts` (under `eform-client/src/app/plugins/modules/time-planning-pn/`). `git checkout`
any unintended files (e.g. unrelated frontend module copies). Confirm NO `src/scss` changes are
present (this plan makes none).

- [ ] **Step 5: Branch, commit, push, PR, watch CI**

Create `feat/planning-day-cell-presentation` off `stable`, stage only the intended files by name,
commit (end message with the `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>` trailer),
push, and open a PR toward `stable`. Watch CI to green (`angular-unit-test`, build, dotnet shards,
playwright). Note: this change is display-only and does not touch the Excel export, so the export
parity playwright test is unaffected. Fix any genuine failures; treat unrelated flaky playwright
shards as flaky (re-run).

---

## Self-Review

- **Spec coverage:** duration format (Task 1) ✓; full vs no-decimal rows — pause/worked/paid-out/flex full (Task 2 Steps 4–7), planned break/plan-hours no-decimal (Task 2 Steps 1–3) ✓; negative red (Task 2 Step 7) ✓; times stay HH:mm (untouched) ✓; all icons outlined (Task 3) ✓; decimal same colour (no special styling — inherits row colour) ✓; translatable labels da+en exact, others fallback (Task 4) ✓; both parts from same value (formatter uses one `totalMinutes`) ✓.
- **Placeholder scan:** none — every step has concrete code/commands.
- **Type/name consistency:** `formatDuration(hours, withDecimal=true)` signature is identical across Task 1 (definition) and all Task 2 call sites; `.red-text` and `material-symbols-outlined` match existing usages in the file.
- **Cross-repo note:** SCSS intentionally untouched to keep this a single plugin-repo PR; icon weight relies on the default Material Symbols Outlined rendering (matches "outlined"; if the user later wants it thinner, that's a follow-up, possibly in the core `eform-angular-frontend` SCSS repo).
