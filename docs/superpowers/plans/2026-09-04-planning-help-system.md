# Planning Help System Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give the TimePlanning planning page a searchable in-page help system — an ⓘ popover, a side panel with search, two guided tours, and inline hints — all fed by one content registry.

**Architecture:** A registry of ~48 entries (36 control descriptions + 12 how-to tasks) defines structure only; prose lives in separate per-locale files and resolves through `HelpContentService` with per-entry English fallback. Four presentation surfaces read that one source. Everything lives inside the plugin under `time-planning-pn/help/`; nothing is added to the host frontend, the backend, or package.json.

**Tech Stack:** Angular 20.3.17, Angular Material + CDK 20.2.14, `@ngx-translate/core` 17, NgModule (not standalone), Jest 30 via `@angular-builders/jest`, `mtx-grid` from `@ng-matero/extensions` 20.4.2.

**Spec:** `docs/superpowers/specs/2026-09-04-planning-help-system-design.md`

## Global Constraints

- **Repository:** all edits go in `eform-angular-timeplanning-plugin`. Do **not** edit the host copy under `eform-angular-frontend/eform-client/src/app/plugins/modules/time-planning-pn/`, and do **not** run `devgetchanges.sh` — the host mirror is 68 files behind and running it would delete working features.
- **No new npm dependency.** No tour library, no search library.
- **No change** to the 25 existing locale files under `time-planning-pn/i18n/`, to `eform-angular-frontend`, or to any `-base` repo. This includes the help components' own button and section labels — they live in `help/i18n/` as `HelpUiStrings` and are read through `HelpContentService.ui()`, **not** through `TranslateService` or the `| translate` pipe. Nothing in `help/` may use the `translate` pipe.
- **`cdkConnectedOverlayUsePopover` does not exist in CDK 20.2.14.** Use a plain `cdkConnectedOverlay` with `cdkOverlayOrigin`. Verified against `node_modules/@angular/cdk/overlay-module.d.d.ts`.
- **Copy rule — "admin" means Microting, not a customer role.** No help string may mention administrator capabilities, explain what more access would allow, or account for why a control did nothing. Entries describe what the reader sees and what the reader can do.
- **Copy rule — name which value is meant** when a label is reused (`plannedHours` is both a weekly total and a per-cell value).
- **Copy rule — describe the screen, not the server.** Never state the flex formula; `SumFlexEnd − PaiedOutFlex` is duplicated in ~5 backend places and `PaiedOutFlexInSeconds` is often unpopulated.
- **Components are declared in the existing NgModule** `time-planning-pn.module.ts`. This plugin does not use standalone components.
- **Test command** (verified to work; see Task 0 for the one-time setup it needs):

  ```bash
  cd /home/rene/Documents/workspace/microting/eform-angular-frontend/eform-client
  npx jest --roots /home/rene/Documents/workspace/microting/eform-angular-timeplanning-plugin/eform-client/src/app/plugins/modules/time-planning-pn \
           --testPathPatterns=help
  ```

  Jest must run from the **frontend** repo (that is where `jest.config.js`, the preset and `setup-jest.ts` live) but `--roots` points it at the **plugin** repo so it finds specs there. Without `--roots` it reports `No tests found` — its `testMatch: ['**/src/**/*.spec.ts']` is scoped to the frontend's own `rootDir`, and the plugin repo is a sibling path outside that tree.

  In CI this is unnecessary: the `angular-unit-test` job (`.github/workflows/dotnet-core-pr.yml:44-82`) copies the plugin folder into the frontend checkout (line 64) before running `npm run test:unit -- --testPathPatterns=time-planning-pn`. Specs therefore run in CI automatically — unlike the dotnet shards, there is no allowlist to update.
- **Pre-commit gate (mandatory, every task):** dispatch `pr-review-toolkit:code-reviewer` and `code-simplifier:code-simplifier` **in parallel, in one message**, on the task's diff. Resolve or consciously dismiss every finding before committing. If you edit after their feedback, re-run both on the new diff.
- **Branch:** `feat/planning-help-system`, PR toward `stable`. Never commit to `stable` or `master` directly.

---

## File Structure

All paths relative to `eform-client/src/app/plugins/modules/time-planning-pn/`.

| File | Responsibility |
|---|---|
| `help/help.model.ts` | Types: `HelpEntryId`, `HelpKind`, `HelpSection`, `HelpEntry`, `HelpProse`, `HelpProseMap` |
| `help/planning-help.registry.ts` | The 48 entries — structure only, no prose |
| `help/i18n/enUS.ts` | English prose, complete |
| `help/i18n/da.ts` | Danish prose, complete |
| `help/i18n/index.ts` | `HELP_LOCALES` map from locale code to prose map |
| `help/services/help-content.service.ts` | Locale resolution + per-entry English fallback |
| `help/services/help-search.service.ts` | Diacritic folding, matching, ranking |
| `help/services/help-panel.service.ts` | Panel open/close state and deep-link target |
| `help/services/help-tour.service.ts` | Tour sequencing, anchor lookup, seen-once |
| `help/components/help-icon/` | `tp-help-icon` — ⓘ button + popover |
| `help/components/help-hint/` | `tp-help-hint` — inline `.help-text` hint |
| `help/components/help-panel/` | `tp-help-panel` — side panel, browse + search |
| `help/components/help-tour/` | `tp-help-tour` — tour step overlay |

Section values actually used are `task`, `toolbar`, `grid`, `dayCell`, `flex`. The spec's type union also listed `shifts` and `flags`; no entry uses them, so they are omitted here.

---

### Task 0: Make the plugin repo testable

Jest lives in the frontend repo; the specs live here. TypeScript resolves `@angular/*`
imports by walking up from the file, and the plugin repo has no `node_modules`, so
without this step every spec fails with `TS2307: Cannot find module '@angular/core'`.

**Files:**
- Modify: `.gitignore`

**Interfaces:** none.

- [ ] **Step 1: Symlink the frontend's `node_modules`**

```bash
ln -s /home/rene/Documents/workspace/microting/eform-angular-frontend/eform-client/node_modules \
      /home/rene/Documents/workspace/microting/eform-angular-timeplanning-plugin/eform-client/node_modules
```

- [ ] **Step 2: Ignore it**

`.gitignore` line 276 is `node_modules/` — with the trailing slash it matches directories
only, and this is a symlink, so it is **not** covered. Append:

```
eform-client/node_modules
```

- [ ] **Step 3: Prove the loop works before writing any feature code**

Create `help/__setup-check.spec.ts`:

```ts
import { TestBed } from '@angular/core/testing';
import { Component } from '@angular/core';
import { OverlayModule } from '@angular/cdk/overlay';

@Component({ selector: 'tp-setup-check', template: '<span>{{ label }}</span>', standalone: false })
class SetupCheckComponent { label = 'ok'; }

describe('jest setup', () => {
  it('compiles an NgModule-declared component from the plugin repo', async () => {
    await TestBed.configureTestingModule({
      declarations: [SetupCheckComponent],
      imports: [OverlayModule],
    }).compileComponents();
    const fixture = TestBed.createComponent(SetupCheckComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('ok');
  });
});
```

Run the test command from Global Constraints. Expected: PASS, 1 test. If it reports
`No tests found`, `--roots` is wrong. If it reports `TS2307`, the symlink is missing.

- [ ] **Step 4: Delete `help/__setup-check.spec.ts`** — it has served its purpose.

- [ ] **Step 5: Commit**

```bash
cd /home/rene/Documents/workspace/microting/eform-angular-timeplanning-plugin
git add .gitignore
git commit -m "chore: ignore the local node_modules symlink used for running plugin tests"
```

---

### Task 1: Types, registry, and the integrity test

**Files:**
- Create: `help/help.model.ts`
- Create: `help/planning-help.registry.ts`
- Create: `help/i18n/enUS.ts` (ids + placeholder-free English prose for all 48)
- Create: `help/i18n/index.ts`
- Test: `help/planning-help.registry.spec.ts`

**Interfaces:**
- Consumes: nothing.
- Produces: `HelpEntryId` (string-literal union), `HelpKind`, `HelpSection`, `HelpEntry`, `HelpProse`, `HelpProseMap`, `PLANNING_HELP_ENTRIES: HelpEntry[]`, `HELP_LOCALES: Record<string, Partial<HelpProseMap>>`, `enUS: HelpProseMap`.

- [ ] **Step 1: Write the failing integrity test**

Create `help/planning-help.registry.spec.ts`:

```ts
import { PLANNING_HELP_ENTRIES } from './planning-help.registry';
import { enUS } from './i18n/enUS';
import { HelpEntry, HelpEntryId } from './help.model';

describe('planning help registry', () => {
  const byId = new Map<HelpEntryId, HelpEntry>(
    PLANNING_HELP_ENTRIES.map(e => [e.id, e]),
  );

  it('has no duplicate ids', () => {
    expect(byId.size).toBe(PLANNING_HELP_ENTRIES.length);
  });

  it('gives every entry English prose with a title, short text and a keyword', () => {
    for (const entry of PLANNING_HELP_ENTRIES) {
      const prose = enUS[entry.id];
      expect(prose).toBeDefined();
      expect(prose.title.length).toBeGreaterThan(0);
      expect(prose.short.length).toBeGreaterThan(0);
      expect(prose.keywords.length).toBeGreaterThan(0);
    }
  });

  it('gives every task steps, and no control steps', () => {
    for (const entry of PLANNING_HELP_ENTRIES) {
      const prose = enUS[entry.id];
      if (entry.kind === 'task') {
        expect(prose.steps?.length ?? 0).toBeGreaterThan(0);
      } else {
        expect(prose.steps).toBeUndefined();
      }
    }
  });

  it('keeps tasks out of tours and off the page', () => {
    for (const entry of PLANNING_HELP_ENTRIES.filter(e => e.kind === 'task')) {
      expect(entry.anchor).toBeUndefined();
      expect(entry.tourStep).toBeUndefined();
      expect(entry.tour).toBeUndefined();
    }
  });

  it('gives every tour step a tour and an anchor', () => {
    for (const entry of PLANNING_HELP_ENTRIES.filter(e => e.tourStep !== undefined)) {
      expect(entry.tour).toBeDefined();
      expect(entry.anchor).toBeDefined();
    }
  });

  it('numbers tour steps uniquely within each tour', () => {
    for (const tour of ['page', 'dialog'] as const) {
      const steps = PLANNING_HELP_ENTRIES
        .filter(e => e.tour === tour && e.tourStep !== undefined)
        .map(e => e.tourStep as number);
      expect(new Set(steps).size).toBe(steps.length);
      expect(steps.length).toBeGreaterThan(0);
    }
  });

  it('resolves every related id', () => {
    for (const entry of PLANNING_HELP_ENTRIES) {
      for (const related of entry.related ?? []) {
        expect(byId.has(related)).toBe(true);
      }
    }
  });

  it('marks exactly one entry admin-only', () => {
    const adminOnly = PLANNING_HELP_ENTRIES.filter(e => e.adminOnly);
    expect(adminOnly.map(e => e.id)).toEqual(['toolbar.payrollExport']);
  });

  it('never mentions administrators in user-facing copy', () => {
    const banned = /\badmin(istrator)?s?\b/i;
    for (const entry of PLANNING_HELP_ENTRIES) {
      const prose = enUS[entry.id];
      const text = [prose.title, prose.short, prose.detail ?? '', ...(prose.steps ?? [])].join(' ');
      expect(text).not.toMatch(banned);
    }
  });
});
```

- [ ] **Step 2: Run it and confirm it fails**

Run: `cd eform-angular-frontend/eform-client && npx jest --testPathPatterns=time-planning-pn/help`
Expected: FAIL — `Cannot find module './planning-help.registry'`.

- [ ] **Step 3: Write `help/help.model.ts`**

```ts
export const HELP_IDS = [
  // tasks
  'task.registerVacation', 'task.registerSickness', 'task.registerDayOff',
  'task.correctRegisteredTime', 'task.addMissingRegistration', 'task.addExtraShift',
  'task.changePlannedHours', 'task.payOutFlex', 'task.exportForPayroll',
  'task.whoChangedThis', 'task.whereWasThisRegistered', 'task.filterToOneTeam',
  // toolbar controls
  'toolbar.showResigned', 'toolbar.navBackward', 'toolbar.navForward',
  'toolbar.workerFilter', 'toolbar.tagFilter', 'toolbar.dateRange',
  'toolbar.downloadExcel', 'toolbar.payrollExport', 'toolbar.reload',
  // grid controls
  'grid.nameColumn', 'grid.tagChips', 'grid.settingsStrip', 'grid.dayCellAnatomy',
  'grid.weeklyPlannedHours', 'grid.messageIcons', 'grid.sortName', 'grid.openDay',
  // day-cell dialog controls
  'dayCell.versionHistory', 'dayCell.plannedTimes', 'dayCell.actualTimes',
  'dayCell.shiftCount', 'dayCell.resetField', 'dayCell.resetPauseToRecorded',
  'dayCell.gps', 'dayCell.snapshot', 'dayCell.futureDisabled', 'dayCell.planHours',
  'dayCell.nettoOverride', 'dayCell.paidOutFlex', 'dayCell.flags',
  'dayCell.commentOffice', 'dayCell.save', 'dayCell.oneMinuteIntervals',
  // flex controls
  'flex.whatIsFlex', 'flex.sumFlex', 'flex.paidOutFlexRelation',
] as const;

export type HelpEntryId = typeof HELP_IDS[number];
export type HelpKind = 'control' | 'task';
export type HelpSection = 'task' | 'toolbar' | 'grid' | 'dayCell' | 'flex';
export type HelpTourName = 'page' | 'dialog';

export interface HelpEntry {
  id: HelpEntryId;
  kind: HelpKind;
  section: HelpSection;
  /** data-tp-help value on the element this entry describes. Controls only. */
  anchor?: string;
  tour?: HelpTourName;
  tourStep?: number;
  adminOnly?: boolean;
  /** Tasks only: the controls this task touches. */
  related?: HelpEntryId[];
}

export interface HelpProse {
  title: string;
  short: string;
  detail?: string;
  /** Tasks only, in order. */
  steps?: string[];
  /** Search synonyms, in this locale's language. */
  keywords: string[];
}

export type HelpProseMap = Record<HelpEntryId, HelpProse>;

/**
 * Labels for the help components' own chrome. These live here rather than in the
 * plugin's 25 shared locale files, which this work must not touch.
 */
export interface HelpUiStrings {
  help: string;
  searchHelp: string;
  clear: string;
  close: string;
  moreInHelp: string;
  replayTour: string;
  skip: string;
  next: string;
  noResults: string;
  sectionTask: string;
  sectionToolbar: string;
  sectionGrid: string;
  sectionDayCell: string;
  sectionFlex: string;
}

export type HelpUiKey = keyof HelpUiStrings;
```

- [ ] **Step 4: Write `help/planning-help.registry.ts`**

Every id in `HELP_IDS` gets exactly one entry. Tasks carry `related` and no anchor; controls carry `anchor`; eight controls carry `tour: 'page'` with `tourStep` 1-8 and six carry `tour: 'dialog'` with `tourStep` 1-6.

```ts
import { HelpEntry } from './help.model';

export const PLANNING_HELP_ENTRIES: HelpEntry[] = [
  // ---- tasks (no anchor, no tour) ----
  { id: 'task.registerVacation', kind: 'task', section: 'task',
    related: ['dayCell.flags', 'dayCell.nettoOverride', 'dayCell.save'] },
  { id: 'task.registerSickness', kind: 'task', section: 'task',
    related: ['dayCell.flags', 'dayCell.save'] },
  { id: 'task.registerDayOff', kind: 'task', section: 'task',
    related: ['dayCell.flags', 'dayCell.nettoOverride'] },
  { id: 'task.correctRegisteredTime', kind: 'task', section: 'task',
    related: ['dayCell.actualTimes', 'dayCell.resetField', 'dayCell.save'] },
  { id: 'task.addMissingRegistration', kind: 'task', section: 'task',
    related: ['grid.openDay', 'dayCell.actualTimes', 'dayCell.save'] },
  { id: 'task.addExtraShift', kind: 'task', section: 'task',
    related: ['dayCell.shiftCount', 'grid.settingsStrip'] },
  { id: 'task.changePlannedHours', kind: 'task', section: 'task',
    related: ['dayCell.plannedTimes', 'dayCell.planHours'] },
  { id: 'task.payOutFlex', kind: 'task', section: 'task',
    related: ['dayCell.paidOutFlex', 'flex.sumFlex'] },
  { id: 'task.exportForPayroll', kind: 'task', section: 'task',
    related: ['toolbar.downloadExcel'] },
  { id: 'task.whoChangedThis', kind: 'task', section: 'task',
    related: ['dayCell.versionHistory'] },
  { id: 'task.whereWasThisRegistered', kind: 'task', section: 'task',
    related: ['dayCell.gps', 'dayCell.snapshot'] },
  { id: 'task.filterToOneTeam', kind: 'task', section: 'task',
    related: ['toolbar.tagFilter', 'grid.tagChips'] },

  // ---- toolbar ----
  { id: 'toolbar.showResigned', kind: 'control', section: 'toolbar', anchor: 'toolbar.showResigned' },
  { id: 'toolbar.navBackward', kind: 'control', section: 'toolbar', anchor: 'toolbar.navBackward' },
  { id: 'toolbar.navForward', kind: 'control', section: 'toolbar', anchor: 'toolbar.navForward',
    tour: 'page', tourStep: 2 },
  { id: 'toolbar.workerFilter', kind: 'control', section: 'toolbar', anchor: 'toolbar.workerFilter',
    tour: 'page', tourStep: 3 },
  { id: 'toolbar.tagFilter', kind: 'control', section: 'toolbar', anchor: 'toolbar.tagFilter' },
  { id: 'toolbar.dateRange', kind: 'control', section: 'toolbar', anchor: 'toolbar.dateRange',
    tour: 'page', tourStep: 1 },
  { id: 'toolbar.downloadExcel', kind: 'control', section: 'toolbar', anchor: 'toolbar.downloadExcel',
    tour: 'page', tourStep: 7 },
  { id: 'toolbar.payrollExport', kind: 'control', section: 'toolbar', anchor: 'toolbar.payrollExport',
    tour: 'page', tourStep: 8, adminOnly: true },
  { id: 'toolbar.reload', kind: 'control', section: 'toolbar', anchor: 'toolbar.reload' },

  // ---- grid ----
  { id: 'grid.nameColumn', kind: 'control', section: 'grid', anchor: 'grid.nameColumn',
    tour: 'page', tourStep: 4 },
  { id: 'grid.tagChips', kind: 'control', section: 'grid', anchor: 'grid.tagChips' },
  { id: 'grid.settingsStrip', kind: 'control', section: 'grid', anchor: 'grid.settingsStrip' },
  { id: 'grid.dayCellAnatomy', kind: 'control', section: 'grid', anchor: 'grid.dayCellAnatomy',
    tour: 'page', tourStep: 5 },
  { id: 'grid.weeklyPlannedHours', kind: 'control', section: 'grid', anchor: 'grid.weeklyPlannedHours' },
  { id: 'grid.messageIcons', kind: 'control', section: 'grid', anchor: 'grid.messageIcons' },
  { id: 'grid.sortName', kind: 'control', section: 'grid', anchor: 'grid.sortName' },
  { id: 'grid.openDay', kind: 'control', section: 'grid', anchor: 'grid.openDay',
    tour: 'page', tourStep: 6 },

  // ---- day-cell dialog ----
  { id: 'dayCell.versionHistory', kind: 'control', section: 'dayCell', anchor: 'dayCell.versionHistory' },
  { id: 'dayCell.plannedTimes', kind: 'control', section: 'dayCell', anchor: 'dayCell.plannedTimes',
    tour: 'dialog', tourStep: 1 },
  { id: 'dayCell.actualTimes', kind: 'control', section: 'dayCell', anchor: 'dayCell.actualTimes',
    tour: 'dialog', tourStep: 2 },
  { id: 'dayCell.shiftCount', kind: 'control', section: 'dayCell', anchor: 'dayCell.shiftCount' },
  { id: 'dayCell.resetField', kind: 'control', section: 'dayCell', anchor: 'dayCell.resetField' },
  { id: 'dayCell.resetPauseToRecorded', kind: 'control', section: 'dayCell', anchor: 'dayCell.resetPauseToRecorded' },
  { id: 'dayCell.gps', kind: 'control', section: 'dayCell', anchor: 'dayCell.gps' },
  { id: 'dayCell.snapshot', kind: 'control', section: 'dayCell', anchor: 'dayCell.snapshot' },
  { id: 'dayCell.futureDisabled', kind: 'control', section: 'dayCell', anchor: 'dayCell.futureDisabled' },
  { id: 'dayCell.planHours', kind: 'control', section: 'dayCell', anchor: 'dayCell.planHours',
    tour: 'dialog', tourStep: 3 },
  { id: 'dayCell.nettoOverride', kind: 'control', section: 'dayCell', anchor: 'dayCell.nettoOverride',
    tour: 'dialog', tourStep: 5 },
  { id: 'dayCell.paidOutFlex', kind: 'control', section: 'dayCell', anchor: 'dayCell.paidOutFlex' },
  { id: 'dayCell.flags', kind: 'control', section: 'dayCell', anchor: 'dayCell.flags',
    tour: 'dialog', tourStep: 4 },
  { id: 'dayCell.commentOffice', kind: 'control', section: 'dayCell', anchor: 'dayCell.commentOffice' },
  { id: 'dayCell.save', kind: 'control', section: 'dayCell', anchor: 'dayCell.save',
    tour: 'dialog', tourStep: 6 },
  { id: 'dayCell.oneMinuteIntervals', kind: 'control', section: 'dayCell', anchor: 'dayCell.oneMinuteIntervals' },

  // ---- flex ----
  { id: 'flex.whatIsFlex', kind: 'control', section: 'flex', anchor: 'flex.whatIsFlex' },
  { id: 'flex.sumFlex', kind: 'control', section: 'flex', anchor: 'flex.sumFlex' },
  { id: 'flex.paidOutFlexRelation', kind: 'control', section: 'flex', anchor: 'flex.paidOutFlexRelation' },
];
```

- [ ] **Step 5: Write `help/i18n/enUS.ts` — all 48 entries**

Authoring rules, enforced by the tests above: no entry may contain the word "admin"/"administrator"; every entry needs `keywords`; tasks need `steps`; controls must not have `steps`.

The three leave tasks carry the rule the UI hides — the day flags render as checkboxes but are mutually exclusive, and ticking one rewrites netto hours (`workday-entity-dialog.component.ts:1263-1290`): `DayOff` and `VacationDayOff` set netto to `0`; every other flag sets it to the day's planned hours.

```ts
import { HelpProseMap } from '../help.model';

export const enUS: HelpProseMap = {
  'task.registerVacation': {
    title: 'Register vacation for a worker',
    short: 'Mark a day as vacation. The day still counts as the hours the worker was planned to work.',
    steps: [
      'Click the day in the grid where the vacation starts.',
      'Tick Vacation in the list of day types.',
      'Click Save. The day now counts as the planned hours.',
      'Repeat for each vacation day.',
    ],
    detail: 'A day carries one day type at a time — ticking Vacation clears any other type already set. Use Vacation day off instead if the day should count as zero hours.',
    keywords: ['vacation', 'holiday', 'time off', 'leave', 'absent', 'away'],
  },
  'task.registerDayOff': {
    title: 'Register a day off',
    short: 'Mark a day as a day off. Unlike vacation, the day counts as zero hours.',
    steps: [
      'Click the day in the grid.',
      'Tick Day off, or Vacation day off if it comes out of the vacation balance.',
      'Click Save. The day now counts as zero hours.',
    ],
    detail: 'Day off and Vacation day off both set the day to zero hours. Vacation, sickness, course and the other day types keep the planned hours instead. This is the difference to watch for.',
    keywords: ['day off', 'off', 'free', 'not working', 'zero hours', 'vacation day off'],
  },
  'dayCell.flags': {
    title: 'Day type',
    short: 'Marks what kind of day this is — vacation, sickness, course, and so on. A day carries one type at a time; ticking a new one clears the previous.',
    detail: 'Setting a day type also sets the netto hours for that day. Day off and Vacation day off set it to zero; every other type sets it to the hours planned for that day. Clearing the type removes that override.',
    keywords: ['day type', 'vacation', 'sickness', 'sick', 'course', 'maternity', 'leave', 'holiday', 'flag', 'absence'],
  },
  // ... the remaining 45 entries, in the same shape and to the same rules.
};

export const enUSUi: HelpUiStrings = {
  help: 'Help',
  searchHelp: 'Search help',
  clear: 'Clear',
  close: 'Close',
  moreInHelp: 'More in help',
  replayTour: 'Take the tour',
  skip: 'Skip',
  next: 'Next',
  noResults: 'Nothing matched. Here is what people usually need:',
  sectionTask: 'Common tasks',
  sectionToolbar: 'Toolbar',
  sectionGrid: 'The grid',
  sectionDayCell: 'Editing a day',
  sectionFlex: 'Flex',
};
```

Import `HelpUiStrings` alongside `HelpProseMap` at the top of the file.

Write all 48. The registry spec fails until every id has prose, so completeness is enforced, not trusted.

- [ ] **Step 6: Write `help/i18n/index.ts`**

```ts
import { HelpProseMap, HelpUiStrings } from '../help.model';
import { enUS, enUSUi } from './enUS';

/** Locale code (as ngx-translate reports it) to prose. Partial maps fall back per entry. */
export const HELP_LOCALES: Record<string, Partial<HelpProseMap>> = {
  'en-US': enUS,
};

export const HELP_UI_LOCALES: Record<string, HelpUiStrings> = {
  'en-US': enUSUi,
};

export const HELP_FALLBACK: HelpProseMap = enUS;
export const HELP_UI_FALLBACK: HelpUiStrings = enUSUi;
```

- [ ] **Step 7: Run the tests and confirm they pass**

Run: `cd eform-angular-frontend/eform-client && npx jest --testPathPatterns=time-planning-pn/help`
Expected: PASS, 9 tests.

- [ ] **Step 8: Pre-commit gate**

Dispatch `pr-review-toolkit:code-reviewer` and `code-simplifier:code-simplifier` in parallel on the diff. Resolve findings.

- [ ] **Step 9: Commit**

```bash
cd /home/rene/Documents/workspace/microting/eform-angular-timeplanning-plugin
git add eform-client/src/app/plugins/modules/time-planning-pn/help
git commit -m "feat(help): add planning help registry, types and English content"
```

---

### Task 2: HelpContentService

**Files:**
- Create: `help/services/help-content.service.ts`
- Test: `help/services/help-content.service.spec.ts`

**Interfaces:**
- Consumes: `PLANNING_HELP_ENTRIES`, `HELP_LOCALES`, `HELP_FALLBACK`, `HelpEntry`, `HelpEntryId`, `HelpProse` from Task 1.
- Produces: `HelpContentService` with `entry(id: HelpEntryId): HelpEntry | undefined`, `prose(id: HelpEntryId): HelpProse`, `entries(opts: { isAdmin: boolean }): HelpEntry[]`, `tourEntries(tour: HelpTourName, opts: { isAdmin: boolean }): HelpEntry[]`.

- [ ] **Step 1: Write the failing test**

```ts
import { TestBed } from '@angular/core/testing';
import { TranslateService } from '@ngx-translate/core';
import { HelpContentService } from './help-content.service';
import { enUS } from '../i18n/enUS';

describe('HelpContentService', () => {
  let translate: { currentLang: string };

  const make = (lang: string) => {
    translate = { currentLang: lang };
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        HelpContentService,
        { provide: TranslateService, useValue: translate },
      ],
    });
    return TestBed.inject(HelpContentService);
  };

  // These compare against the content file rather than a hard-coded string, so a
  // copywriting choice made later in Task 1 cannot fail a resolution test.
  it('returns English prose for an English locale', () => {
    const service = make('en-US');
    expect(service.prose('toolbar.dateRange')).toEqual(enUS['toolbar.dateRange']);
  });

  it('falls back to English for a locale with no prose file', () => {
    const service = make('de-DE');
    expect(service.prose('toolbar.dateRange')).toEqual(enUS['toolbar.dateRange']);
  });

  it('resolves a bare language code to its locale file', () => {
    const service = make('da');
    expect(service.prose('toolbar.dateRange')).toBeDefined();
  });

  it('hides admin-only entries from a non-admin', () => {
    const service = make('en-US');
    const ids = service.entries({ isAdmin: false }).map(e => e.id);
    expect(ids).not.toContain('toolbar.payrollExport');
    expect(service.entries({ isAdmin: true }).map(e => e.id))
      .toContain('toolbar.payrollExport');
  });

  it('orders tour entries by step and drops admin-only steps for a non-admin', () => {
    const service = make('en-US');
    const steps = service.tourEntries('page', { isAdmin: false });
    expect(steps.map(e => e.tourStep)).toEqual([...steps.map(e => e.tourStep)].sort((a, b) => (a ?? 0) - (b ?? 0)));
    expect(steps.map(e => e.id)).not.toContain('toolbar.payrollExport');
    expect(service.tourEntries('page', { isAdmin: true }).map(e => e.id))
      .toContain('toolbar.payrollExport');
  });

  it('never returns undefined prose for a registry id', () => {
    const service = make('da');
    for (const entry of service.entries({ isAdmin: true })) {
      expect(service.prose(entry.id).short.length).toBeGreaterThan(0);
    }
  });
});
```

- [ ] **Step 2: Run it and confirm it fails**

Run: `cd eform-angular-frontend/eform-client && npx jest --testPathPatterns=time-planning-pn/help`
Expected: FAIL — `Cannot find module './help-content.service'`.

- [ ] **Step 3: Implement the service**

```ts
import { Injectable } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';
import { HelpEntry, HelpEntryId, HelpProse, HelpTourName, HelpUiStrings } from '../help.model';
import { PLANNING_HELP_ENTRIES } from '../planning-help.registry';
import { HELP_FALLBACK, HELP_LOCALES, HELP_UI_FALLBACK, HELP_UI_LOCALES } from '../i18n';

@Injectable({ providedIn: 'root' })
export class HelpContentService {
  private readonly byId = new Map<HelpEntryId, HelpEntry>(
    PLANNING_HELP_ENTRIES.map(entry => [entry.id, entry]),
  );

  constructor(private translateService: TranslateService) {}

  entry(id: HelpEntryId): HelpEntry | undefined {
    return this.byId.get(id);
  }

  /** Active locale, falling back to English one entry at a time. */
  prose(id: HelpEntryId): HelpProse {
    return this.localeProse()[id] ?? HELP_FALLBACK[id];
  }

  entries(opts: { isAdmin: boolean }): HelpEntry[] {
    return PLANNING_HELP_ENTRIES.filter(entry => !entry.adminOnly || opts.isAdmin);
  }

  tourEntries(tour: HelpTourName, opts: { isAdmin: boolean }): HelpEntry[] {
    return this.entries(opts)
      .filter(entry => entry.tour === tour && entry.tourStep !== undefined)
      .sort((a, b) => (a.tourStep as number) - (b.tourStep as number));
  }

  /** Chrome labels for the help components, resolved the same way as prose. */
  ui(): HelpUiStrings {
    const lang = this.lang();
    return HELP_UI_LOCALES[lang] ?? HELP_UI_LOCALES[lang.split('-')[0]] ?? HELP_UI_FALLBACK;
  }

  private localeProse(): Partial<Record<HelpEntryId, HelpProse>> {
    const lang = this.lang();
    return HELP_LOCALES[lang] ?? HELP_LOCALES[lang.split('-')[0]] ?? HELP_FALLBACK;
  }

  private lang(): string {
    return this.translateService.currentLang || 'en-US';
  }
}
```

- [ ] **Step 4: Run the tests and confirm they pass**

Run: `cd eform-angular-frontend/eform-client && npx jest --testPathPatterns=time-planning-pn/help`
Expected: PASS. The `'da'` cases pass against the English fallback until Task 3 adds the Danish file.

- [ ] **Step 5: Pre-commit gate** — code-reviewer and code-simplifier in parallel; resolve findings.

- [ ] **Step 6: Commit**

```bash
git add eform-client/src/app/plugins/modules/time-planning-pn/help/services
git commit -m "feat(help): resolve help prose by locale with per-entry English fallback"
```

---

### Task 3: Danish content

**Files:**
- Create: `help/i18n/da.ts`
- Modify: `help/i18n/index.ts`
- Test: `help/i18n/da.spec.ts`

**Interfaces:**
- Consumes: `HelpProseMap`, `HELP_IDS`, `enUS` from Task 1.
- Produces: `da: HelpProseMap`, registered in `HELP_LOCALES` under `'da'`.

- [ ] **Step 1: Write the failing test**

```ts
import { da } from './da';
import { enUS } from './enUS';
import { HELP_IDS } from '../help.model';
import { PLANNING_HELP_ENTRIES } from '../planning-help.registry';

describe('Danish help content', () => {
  it('covers every registry id', () => {
    for (const id of HELP_IDS) {
      expect(da[id]).toBeDefined();
    }
  });

  it('is actually translated, not copied from English', () => {
    const identical = HELP_IDS.filter(id => da[id].short === enUS[id].short);
    expect(identical).toEqual([]);
  });

  it('gives every task Danish steps', () => {
    for (const entry of PLANNING_HELP_ENTRIES.filter(e => e.kind === 'task')) {
      expect(da[entry.id].steps?.length ?? 0).toBeGreaterThan(0);
    }
  });

  it('carries Danish search keywords the English file does not have', () => {
    const danish = new Set(HELP_IDS.flatMap(id => da[id].keywords));
    for (const word of ['ferie', 'sygdom', 'fri', 'afspadsering', 'barsel']) {
      expect(danish.has(word)).toBe(true);
    }
  });

  it('never mentions administrators', () => {
    // \w* catches the Danish definite and possessive forms — administratoren,
    // administratorens — which a content author is most likely to reach for.
    const banned = /\badministrator\w*\b|\badmin\b/i;
    for (const id of HELP_IDS) {
      const prose = da[id];
      const text = [prose.title, prose.short, prose.detail ?? '', ...(prose.steps ?? [])].join(' ');
      expect(text).not.toMatch(banned);
    }
  });
});
```

- [ ] **Step 2: Run it and confirm it fails**

Run: `cd eform-angular-frontend/eform-client && npx jest --testPathPatterns=time-planning-pn/help`
Expected: FAIL — `Cannot find module './da'`.

- [ ] **Step 3: Write `help/i18n/da.ts` — all 48 entries in Danish**

```ts
import { HelpProseMap } from '../help.model';

export const da: HelpProseMap = {
  'task.registerVacation': {
    title: 'Registrér ferie for en medarbejder',
    short: 'Markér en dag som ferie. Dagen tæller stadig som de timer, medarbejderen var planlagt til.',
    steps: [
      'Klik på dagen i skemaet, hvor ferien begynder.',
      'Sæt flueben ved Ferie.',
      'Klik Gem. Dagen tæller nu som de planlagte timer.',
      'Gentag for hver feriedag.',
    ],
    detail: 'En dag har én dagtype ad gangen — sætter du Ferie, fjernes en anden type, der måtte være sat. Brug Feriefridag i stedet, hvis dagen skal tælle som nul timer.',
    keywords: ['ferie', 'fri', 'fravær', 'orlov', 'væk', 'feriedag'],
  },
  'task.registerDayOff': {
    title: 'Registrér en fridag',
    short: 'Markér en dag som fridag. Modsat ferie tæller dagen som nul timer.',
    steps: [
      'Klik på dagen i skemaet.',
      'Sæt flueben ved Fridag, eller Feriefridag hvis dagen trækkes fra ferien.',
      'Klik Gem. Dagen tæller nu som nul timer.',
    ],
    detail: 'Fridag og Feriefridag sætter begge dagen til nul timer. Ferie, sygdom, kursus og de øvrige dagtyper beholder de planlagte timer. Det er forskellen, man skal være opmærksom på.',
    keywords: ['fridag', 'fri', 'afspadsering', 'nul timer', 'feriefridag', 'ikke på arbejde'],
  },
  // ... the remaining 46 entries.
};

export const daUi: HelpUiStrings = {
  help: 'Hjælp',
  searchHelp: 'Søg i hjælp',
  clear: 'Ryd',
  close: 'Luk',
  moreInHelp: 'Mere i hjælp',
  replayTour: 'Tag rundvisningen',
  skip: 'Spring over',
  next: 'Næste',
  noResults: 'Ingen træffere. Her er det, folk oftest har brug for:',
  sectionTask: 'Almindelige opgaver',
  sectionToolbar: 'Værktøjslinje',
  sectionGrid: 'Skemaet',
  sectionDayCell: 'Rediger dag',
  sectionFlex: 'Flex',
};
```

Register both in `help/i18n/index.ts`: add `'da': da` to `HELP_LOCALES` and `'da': daUi`
to `HELP_UI_LOCALES`.

- [ ] **Step 4: Register the locale in `help/i18n/index.ts`**

```ts
import { HelpProseMap, HelpUiStrings } from '../help.model';
import { enUS, enUSUi } from './enUS';
import { da, daUi } from './da';

export const HELP_LOCALES: Record<string, Partial<HelpProseMap>> = {
  'en-US': enUS,
  'da': da,
};

export const HELP_UI_LOCALES: Record<string, HelpUiStrings> = {
  'en-US': enUSUi,
  'da': daUi,
};

export const HELP_FALLBACK: HelpProseMap = enUS;
export const HELP_UI_FALLBACK: HelpUiStrings = enUSUi;
```

- [ ] **Step 5: Run the tests and confirm they pass**

Run: `cd eform-angular-frontend/eform-client && npx jest --testPathPatterns=time-planning-pn/help`
Expected: PASS.

- [ ] **Step 6: Pre-commit gate** — code-reviewer and code-simplifier in parallel; resolve findings.

- [ ] **Step 7: Commit**

```bash
git add eform-client/src/app/plugins/modules/time-planning-pn/help/i18n
git commit -m "feat(help): add Danish help content and search keywords"
```

---

### Task 4: HelpSearchService

**Files:**
- Create: `help/services/help-search.service.ts`
- Test: `help/services/help-search.service.spec.ts`

**Interfaces:**
- Consumes: `HelpContentService` (Task 2), `HELP_FALLBACK`, `HelpEntry`, `HelpProse`.
- Produces: `HelpSearchService` with `search(query: string, opts: { isAdmin: boolean }): HelpSearchResult[]`, and the exported interface `HelpSearchResult { entry: HelpEntry; prose: HelpProse; }`.

- [ ] **Step 1: Write the failing test**

```ts
import { TestBed } from '@angular/core/testing';
import { TranslateService } from '@ngx-translate/core';
import { HelpSearchService } from './help-search.service';
import { HelpContentService } from './help-content.service';

describe('HelpSearchService', () => {
  const make = (lang: string) => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        HelpSearchService,
        HelpContentService,
        { provide: TranslateService, useValue: { currentLang: lang } },
      ],
    });
    return TestBed.inject(HelpSearchService);
  };

  it('finds the vacation task from the Danish word', () => {
    const ids = make('da').search('ferie', { isAdmin: false }).map(r => r.entry.id);
    expect(ids).toContain('task.registerVacation');
  });

  it('finds a Danish entry from an English word, through the fallback', () => {
    const ids = make('da').search('vacation', { isAdmin: false }).map(r => r.entry.id);
    expect(ids).toContain('task.registerVacation');
  });

  it('folds diacritics so ae matches æ', () => {
    const service = make('da');
    const withLigature = service.search('læge', { isAdmin: false }).map(r => r.entry.id);
    const folded = service.search('laege', { isAdmin: false }).map(r => r.entry.id);
    expect(folded).toEqual(withLigature);
  });

  it('folds ø and å', () => {
    const service = make('da');
    expect(service.search('sygdom', { isAdmin: false }).length).toBeGreaterThan(0);
    expect(service.search('arstid', { isAdmin: false })).toEqual(
      service.search('årstid', { isAdmin: false }),
    );
  });

  it('ranks tasks above controls', () => {
    const results = make('en-US').search('vacation', { isAdmin: false });
    const firstControl = results.findIndex(r => r.entry.kind === 'control');
    const lastTask = results.map(r => r.entry.kind).lastIndexOf('task');
    // Assert both groups are present, so a content edit that removes one cannot
    // make this test pass without checking anything.
    expect(firstControl).not.toBe(-1);
    expect(lastTask).not.toBe(-1);
    expect(lastTask).toBeLessThan(firstControl);
  });

  it('ranks a title match above a body-only match', () => {
    const results = make('en-US').search('flex', { isAdmin: false });
    expect(results.length).toBeGreaterThan(1);
    expect(results[0].prose.title.toLowerCase()).toContain('flex');
  });

  it('returns the task list when nothing matches', () => {
    const results = make('en-US').search('zzzznomatch', { isAdmin: false });
    expect(results.length).toBeGreaterThan(0);
    expect(results.every(r => r.entry.kind === 'task')).toBe(true);
  });

  it('returns the task list for an empty query', () => {
    const results = make('en-US').search('   ', { isAdmin: false });
    expect(results.every(r => r.entry.kind === 'task')).toBe(true);
  });

  it('never returns an admin-only entry to a non-admin', () => {
    const ids = make('en-US').search('payroll', { isAdmin: false }).map(r => r.entry.id);
    expect(ids).not.toContain('toolbar.payrollExport');
  });
});
```

- [ ] **Step 2: Run it and confirm it fails**

Run: `cd eform-angular-frontend/eform-client && npx jest --testPathPatterns=time-planning-pn/help`
Expected: FAIL — `Cannot find module './help-search.service'`.

- [ ] **Step 3: Implement the service**

`ø` and `æ` have no Unicode decomposition, so NFD alone does not fold them — they need an explicit map. `å` does decompose, and the combining-mark strip handles it.

```ts
import { Injectable } from '@angular/core';
import { HelpEntry, HelpEntryId, HelpProse } from '../help.model';
import { HELP_FALLBACK } from '../i18n';
import { HelpContentService } from './help-content.service';

export interface HelpSearchResult {
  entry: HelpEntry;
  prose: HelpProse;
}

/** Match location, lower is better. */
const RANK_TITLE = 0;
const RANK_KEYWORD = 1;
const RANK_BODY = 2;
const RANK_NONE = 99;

const LIGATURES: Record<string, string> = { æ: 'ae', ø: 'o', Æ: 'ae', Ø: 'o' };

export function fold(value: string): string {
  return value
    .replace(/[æøÆØ]/g, char => LIGATURES[char])
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .toLowerCase()
    .trim();
}

@Injectable({ providedIn: 'root' })
export class HelpSearchService {
  constructor(private helpContent: HelpContentService) {}

  search(query: string, opts: { isAdmin: boolean }): HelpSearchResult[] {
    const needle = fold(query);
    const entries = this.helpContent.entries(opts);

    if (!needle) {
      return this.tasksOnly(entries);
    }

    const ranked = entries
      .map(entry => ({ entry, prose: this.helpContent.prose(entry.id), rank: this.rank(entry.id, needle) }))
      .filter(result => result.rank !== RANK_NONE);

    if (!ranked.length) {
      return this.tasksOnly(entries);
    }

    return ranked
      .sort((a, b) =>
        (a.entry.kind === 'task' ? 0 : 1) - (b.entry.kind === 'task' ? 0 : 1) ||
        a.rank - b.rank)
      .map(({ entry, prose }) => ({ entry, prose }));
  }

  /** Best match location across the active locale and the English fallback. */
  private rank(id: HelpEntryId, needle: string): number {
    const candidates = [this.helpContent.prose(id), HELP_FALLBACK[id]];
    let best = RANK_NONE;

    for (const prose of candidates) {
      if (fold(prose.title).includes(needle)) {
        return RANK_TITLE;
      }
      if (prose.keywords.some(keyword => fold(keyword).includes(needle))) {
        // A keyword match already beats any body match, so skip the body scan.
        best = Math.min(best, RANK_KEYWORD);
        continue;
      }
      const body = [prose.short, prose.detail ?? '', ...(prose.steps ?? [])].join(' ');
      if (fold(body).includes(needle)) {
        best = Math.min(best, RANK_BODY);
      }
    }

    return best;
  }

  private tasksOnly(entries: HelpEntry[]): HelpSearchResult[] {
    return entries
      .filter(entry => entry.kind === 'task')
      .map(entry => ({ entry, prose: this.helpContent.prose(entry.id) }));
  }
}
```

- [ ] **Step 4: Run the tests and confirm they pass**

Run: `cd eform-angular-frontend/eform-client && npx jest --testPathPatterns=time-planning-pn/help`
Expected: PASS.

- [ ] **Step 5: Pre-commit gate** — code-reviewer and code-simplifier in parallel; resolve findings.

- [ ] **Step 6: Commit**

```bash
git add eform-client/src/app/plugins/modules/time-planning-pn/help/services
git commit -m "feat(help): add diacritic-folding help search with tasks ranked first"
```

---

### Task 5: tp-help-icon

**Files:**
- Create: `help/components/help-icon/help-icon.component.ts`
- Create: `help/components/help-icon/help-icon.component.html`
- Create: `help/components/help-icon/help-icon.component.scss`
- Modify: `time-planning-pn.module.ts` (declare `HelpIconComponent`, import `OverlayModule`)
- Test: `help/components/help-icon/help-icon.component.spec.ts`

**Interfaces:**
- Consumes: `HelpContentService` (Task 2), `HelpPanelService` is **not** used here — the "More" link emits an output instead, so this component stays independent of Task 7.
- Produces: `HelpIconComponent`, selector `tp-help-icon`, `@Input() helpId: HelpEntryId`, `@Output() openInPanel = new EventEmitter<HelpEntryId>()`.

- [ ] **Step 1: Write the failing test**

```ts
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { OverlayModule } from '@angular/cdk/overlay';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { TranslateService } from '@ngx-translate/core';
import { HelpIconComponent } from './help-icon.component';
import { enUS } from '../../i18n/enUS';

describe('HelpIconComponent', () => {
  let fixture: ComponentFixture<HelpIconComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [HelpIconComponent],
      imports: [OverlayModule, NoopAnimationsModule, MatIconModule, MatButtonModule],
      providers: [{ provide: TranslateService, useValue: { currentLang: 'en-US' } }],
    }).compileComponents();

    fixture = TestBed.createComponent(HelpIconComponent);
    fixture.componentInstance.helpId = 'toolbar.dateRange';
    fixture.detectChanges();
  });

  it('labels the button with the entry title', () => {
    const button: HTMLButtonElement = fixture.nativeElement.querySelector('button');
    expect(button.getAttribute('aria-label')).toBe(enUS['toolbar.dateRange'].title);
  });

  it('starts closed and opens on click', () => {
    expect(fixture.componentInstance.isOpen).toBe(false);
    fixture.nativeElement.querySelector('button').click();
    fixture.detectChanges();
    expect(fixture.componentInstance.isOpen).toBe(true);
  });

  it('closes on Escape', () => {
    fixture.componentInstance.isOpen = true;
    fixture.componentInstance.onOverlayKeydown(new KeyboardEvent('keydown', { key: 'Escape' }));
    expect(fixture.componentInstance.isOpen).toBe(false);
  });

  it('ignores other keys', () => {
    fixture.componentInstance.isOpen = true;
    fixture.componentInstance.onOverlayKeydown(new KeyboardEvent('keydown', { key: 'a' }));
    expect(fixture.componentInstance.isOpen).toBe(true);
  });

  it('emits the id when More is used, and closes', () => {
    const seen: string[] = [];
    fixture.componentInstance.openInPanel.subscribe(id => seen.push(id));
    fixture.componentInstance.isOpen = true;
    fixture.componentInstance.onMore();
    expect(seen).toEqual(['toolbar.dateRange']);
    expect(fixture.componentInstance.isOpen).toBe(false);
  });

  it('renders nothing for an unknown id rather than throwing', () => {
    const other = TestBed.createComponent(HelpIconComponent);
    other.componentInstance.helpId = 'nope' as never;
    expect(() => other.detectChanges()).not.toThrow();
    expect(other.nativeElement.querySelector('button')).toBeNull();
  });
});
```

- [ ] **Step 2: Run it and confirm it fails**

Run: `cd eform-angular-frontend/eform-client && npx jest --testPathPatterns=time-planning-pn/help`
Expected: FAIL — `Cannot find module './help-icon.component'`.

- [ ] **Step 3: Write the component class**

```ts
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { ConnectedPosition } from '@angular/cdk/overlay';
import { HelpEntryId, HelpProse } from '../../help.model';
import { HelpContentService } from '../../services/help-content.service';

@Component({
  selector: 'tp-help-icon',
  templateUrl: './help-icon.component.html',
  styleUrls: ['./help-icon.component.scss'],
  standalone: false,
})
export class HelpIconComponent {
  @Input() helpId!: HelpEntryId;
  @Output() openInPanel = new EventEmitter<HelpEntryId>();

  isOpen = false;

  readonly positions: ConnectedPosition[] = [
    { originX: 'center', originY: 'bottom', overlayX: 'start', overlayY: 'top', offsetY: 6 },
    { originX: 'center', originY: 'top', overlayX: 'start', overlayY: 'bottom', offsetY: -6 },
    { originX: 'center', originY: 'bottom', overlayX: 'end', overlayY: 'top', offsetY: 6 },
    { originX: 'center', originY: 'top', overlayX: 'end', overlayY: 'bottom', offsetY: -6 },
  ];

  constructor(private helpContent: HelpContentService) {}

  get prose(): HelpProse | undefined {
    return this.helpContent.entry(this.helpId) ? this.helpContent.prose(this.helpId) : undefined;
  }

  toggle(): void {
    this.isOpen = !this.isOpen;
  }

  close(): void {
    this.isOpen = false;
  }

  onOverlayKeydown(event: KeyboardEvent): void {
    if (event.key === 'Escape') {
      this.close();
    }
  }

  onMore(): void {
    this.openInPanel.emit(this.helpId);
    this.close();
  }
}
```

- [ ] **Step 4: Write the template**

`cdkConnectedOverlayUsePopover` is not available in CDK 20.2.14; this uses the standard connected overlay, which stacks correctly above an open `MatDialog` because CDK appends later overlays after the dialog pane in `.cdk-overlay-container`.

```html
<ng-container *ngIf="prose as helpProse">
  <button
    type="button"
    mat-icon-button
    class="tp-help-icon"
    cdkOverlayOrigin
    #helpOrigin="cdkOverlayOrigin"
    [attr.aria-label]="helpProse.title"
    [attr.aria-expanded]="isOpen"
    (click)="toggle()">
    <mat-icon>info_outline</mat-icon>
  </button>

  <ng-template
    cdkConnectedOverlay
    [cdkConnectedOverlayOrigin]="helpOrigin"
    [cdkConnectedOverlayOpen]="isOpen"
    [cdkConnectedOverlayPositions]="positions"
    [cdkConnectedOverlayHasBackdrop]="true"
    cdkConnectedOverlayBackdropClass="cdk-overlay-transparent-backdrop"
    [cdkConnectedOverlayPush]="true"
    (backdropClick)="close()"
    (overlayKeydown)="onOverlayKeydown($event)"
    (detach)="close()">
    <div class="tp-help-popover" role="dialog" [attr.aria-label]="helpProse.title">
      <h5 class="tp-help-popover__title">{{ helpProse.title }}</h5>
      <p class="tp-help-popover__body">{{ helpProse.short }}</p>
      <button type="button" class="tp-help-popover__more" (click)="onMore()">
        {{ ui.moreInHelp }}
      </button>
    </div>
  </ng-template>
</ng-container>
```

The label comes from `HelpUiStrings.moreInHelp`; add a `get ui(): HelpUiStrings { return this.helpContent.ui(); }` accessor to the component. No key is added to the plugin's shared locale files.

- [ ] **Step 5: Write the SCSS**

```scss
.tp-help-icon {
  width: 20px;
  height: 20px;
  line-height: 20px;
  vertical-align: middle;

  .mat-icon {
    font-size: 15px;
    width: 15px;
    height: 15px;
    color: var(--text-body, #7f868d);
  }

  &:hover .mat-icon {
    color: var(--primary, #289694);
  }
}

.tp-help-popover {
  max-width: 320px;
  padding: 12px 14px;
  border: 1px solid var(--border, #e2e6e9);
  border-radius: 8px;
  background: var(--bg, #ffffff);
  box-shadow: 0 2px 6px rgba(15, 19, 22, 0.1), 0 12px 32px rgba(15, 19, 22, 0.16);

  &__title {
    margin: 0 0 6px;
    font-size: 13px;
    font-weight: 600;
    color: var(--text-header, #0f1316);
  }

  &__body {
    margin: 0;
    font-size: 12.5px;
    line-height: 1.5;
    color: var(--text-body, #7f868d);
  }

  &__more {
    margin-top: 9px;
    padding: 0;
    border: 0;
    background: none;
    font-size: 12px;
    font-weight: 500;
    color: var(--primary, #289694);
    cursor: pointer;
  }
}
```

- [ ] **Step 6: Declare in the module**

In `time-planning-pn.module.ts`: add `OverlayModule` from `@angular/cdk/overlay` to `imports`, and `HelpIconComponent` to `declarations`.

- [ ] **Step 7: Run the tests and confirm they pass**

Run: `cd eform-angular-frontend/eform-client && npx jest --testPathPatterns=time-planning-pn/help`
Expected: PASS.

- [ ] **Step 8: Pre-commit gate** — code-reviewer and code-simplifier in parallel; resolve findings.

- [ ] **Step 9: Commit**

```bash
git add eform-client/src/app/plugins/modules/time-planning-pn/help \
        eform-client/src/app/plugins/modules/time-planning-pn/time-planning-pn.module.ts \
        eform-client/src/app/plugins/modules/time-planning-pn/i18n/enUS.ts \
        eform-client/src/app/plugins/modules/time-planning-pn/i18n/da.ts
git commit -m "feat(help): add tp-help-icon popover built on cdkConnectedOverlay"
```

---

### Task 6: tp-help-hint

**Files:**
- Create: `help/components/help-hint/help-hint.component.ts`
- Create: `help/components/help-hint/help-hint.component.html`
- Create: `help/components/help-hint/help-hint.component.scss`
- Modify: `time-planning-pn.module.ts`
- Test: `help/components/help-hint/help-hint.component.spec.ts`

**Interfaces:**
- Consumes: `HelpContentService`.
- Produces: `HelpHintComponent`, selector `tp-help-hint`, `@Input() helpId: HelpEntryId`, `@Input() tone: 'info' | 'warn' = 'info'`.

- [ ] **Step 1: Write the failing test**

```ts
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatIconModule } from '@angular/material/icon';
import { TranslateService } from '@ngx-translate/core';
import { HelpHintComponent } from './help-hint.component';
import { enUS } from '../../i18n/enUS';

describe('HelpHintComponent', () => {
  let fixture: ComponentFixture<HelpHintComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [HelpHintComponent],
      imports: [MatIconModule],
      providers: [{ provide: TranslateService, useValue: { currentLang: 'en-US' } }],
    }).compileComponents();
    fixture = TestBed.createComponent(HelpHintComponent);
  });

  it('renders the entry short text', () => {
    fixture.componentInstance.helpId = 'dayCell.futureDisabled';
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain(enUS['dayCell.futureDisabled'].short);
  });

  it('uses the info tone by default and warn when asked', () => {
    fixture.componentInstance.helpId = 'dayCell.futureDisabled';
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.help-text')?.classList).not.toContain('help-text--warn');

    fixture.componentInstance.tone = 'warn';
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.help-text')?.classList).toContain('help-text--warn');
  });

  it('renders nothing for an unknown id', () => {
    fixture.componentInstance.helpId = 'nope' as never;
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.help-text')).toBeNull();
  });
});
```

- [ ] **Step 2: Run it and confirm it fails**

Run: `cd eform-angular-frontend/eform-client && npx jest --testPathPatterns=time-planning-pn/help`
Expected: FAIL — `Cannot find module './help-hint.component'`.

- [ ] **Step 3: Write the component**

```ts
import { Component, Input } from '@angular/core';
import { HelpEntryId, HelpProse } from '../../help.model';
import { HelpContentService } from '../../services/help-content.service';

@Component({
  selector: 'tp-help-hint',
  templateUrl: './help-hint.component.html',
  styleUrls: ['./help-hint.component.scss'],
  standalone: false,
})
export class HelpHintComponent {
  @Input() helpId!: HelpEntryId;
  @Input() tone: 'info' | 'warn' = 'info';

  constructor(private helpContent: HelpContentService) {}

  get prose(): HelpProse | undefined {
    return this.helpContent.entry(this.helpId) ? this.helpContent.prose(this.helpId) : undefined;
  }
}
```

- [ ] **Step 4: Write the template**

This reuses the plugin's existing `.help-text` + `mat-icon>info` pattern. It appears in exactly two places today — `pay-day-rule-form.component.html:105-108` and `day-type-rule-dialog.component.html:161` — so this component both reuses and standardises it.

```html
<div class="help-text" *ngIf="prose as helpProse" [class.help-text--warn]="tone === 'warn'">
  <mat-icon>{{ tone === 'warn' ? 'warning' : 'info' }}</mat-icon>
  <span>{{ helpProse.short }}</span>
</div>
```

- [ ] **Step 5: Write the SCSS**

```scss
.help-text {
  display: flex;
  gap: 8px;
  align-items: flex-start;
  padding: 9px 11px;
  border-left: 3px solid var(--primary, #289694);
  border-radius: 0 5px 5px 0;
  background: var(--primary-light, #f5fcfc);
  font-size: 12.5px;
  line-height: 1.5;
  color: var(--text-body, #7f868d);

  .mat-icon {
    flex: none;
    font-size: 16px;
    width: 16px;
    height: 16px;
    color: var(--primary, #289694);
  }

  &--warn {
    border-left-color: var(--warning, #e2a01c);
    background: rgba(226, 160, 28, 0.12);

    .mat-icon {
      color: var(--warning, #e2a01c);
    }
  }
}
```

- [ ] **Step 6: Declare `HelpHintComponent` in `time-planning-pn.module.ts`**

- [ ] **Step 7: Run the tests and confirm they pass**

Run: `cd eform-angular-frontend/eform-client && npx jest --testPathPatterns=time-planning-pn/help`
Expected: PASS.

- [ ] **Step 8: Pre-commit gate** — code-reviewer and code-simplifier in parallel; resolve findings.

- [ ] **Step 9: Commit**

```bash
git add eform-client/src/app/plugins/modules/time-planning-pn/help \
        eform-client/src/app/plugins/modules/time-planning-pn/time-planning-pn.module.ts
git commit -m "feat(help): add tp-help-hint inline hint component"
```

---

### Task 7: HelpPanelService and tp-help-panel

**Files:**
- Create: `help/services/help-panel.service.ts`
- Create: `help/components/help-panel/help-panel.component.ts`
- Create: `help/components/help-panel/help-panel.component.html`
- Create: `help/components/help-panel/help-panel.component.scss`
- Modify: `time-planning-pn.module.ts`
- Test: `help/services/help-panel.service.spec.ts`
- Test: `help/components/help-panel/help-panel.component.spec.ts`

**Interfaces:**
- Consumes: `HelpContentService` (Task 2), `HelpSearchService` + `HelpSearchResult` (Task 4).
- Produces: `HelpPanelService` with `isOpen$: Observable<boolean>`, `target$: Observable<HelpEntryId | null>`, `open(target?: HelpEntryId): void`, `close(): void`; and `HelpPanelComponent`, selector `tp-help-panel`, `@Input() isAdmin = false`, `@Output() replayTourRequested = new EventEmitter<void>()`.
- **Does not** consume `HelpTourService` — that service does not exist until Task 8. The panel raises `replayTourRequested` and Task 9 wires it to the tour.

- [ ] **Step 1: Write the failing service test**

```ts
import { HelpPanelService } from './help-panel.service';
import { firstValueFrom } from 'rxjs';

describe('HelpPanelService', () => {
  it('starts closed', async () => {
    const service = new HelpPanelService();
    expect(await firstValueFrom(service.isOpen$)).toBe(false);
  });

  it('opens with no target', async () => {
    const service = new HelpPanelService();
    service.open();
    expect(await firstValueFrom(service.isOpen$)).toBe(true);
    expect(await firstValueFrom(service.target$)).toBeNull();
  });

  it('opens on a target and clears it on close', async () => {
    const service = new HelpPanelService();
    service.open('flex.sumFlex');
    expect(await firstValueFrom(service.target$)).toBe('flex.sumFlex');
    service.close();
    expect(await firstValueFrom(service.isOpen$)).toBe(false);
    expect(await firstValueFrom(service.target$)).toBeNull();
  });
});
```

- [ ] **Step 2: Write the failing component test**

```ts
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { TranslateService } from '@ngx-translate/core';
import { HelpPanelComponent } from './help-panel.component';
import { HelpPanelService } from '../../services/help-panel.service';

describe('HelpPanelComponent', () => {
  let fixture: ComponentFixture<HelpPanelComponent>;
  let panel: HelpPanelService;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [HelpPanelComponent],
      imports: [FormsModule, MatIconModule, MatButtonModule],
      providers: [
        HelpPanelService,
        { provide: TranslateService, useValue: { currentLang: 'en-US' } },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(HelpPanelComponent);
    panel = TestBed.inject(HelpPanelService);
    fixture.detectChanges();
  });

  it('renders nothing while closed', () => {
    expect(fixture.nativeElement.querySelector('.tp-help-panel')).toBeNull();
  });

  it('browses grouped sections when open with no query', () => {
    panel.open();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.tp-help-panel')).not.toBeNull();
    expect(fixture.componentInstance.isSearching).toBe(false);
    expect(fixture.nativeElement.textContent).toContain('Date range');
  });

  it('switches to results when a query is typed', () => {
    panel.open();
    fixture.componentInstance.onQueryChange('vacation');
    fixture.detectChanges();
    expect(fixture.componentInstance.isSearching).toBe(true);
    expect(fixture.componentInstance.results.length).toBeGreaterThan(0);
  });

  it('hides admin-only entries when isAdmin is false', () => {
    fixture.componentInstance.isAdmin = false;
    panel.open();
    fixture.detectChanges();
    const ids = fixture.componentInstance.sections
      .flatMap(section => section.entries.map(entry => entry.id));
    expect(ids).not.toContain('toolbar.payrollExport');
  });

  it('marks the deep-link target', () => {
    panel.open('flex.sumFlex');
    fixture.detectChanges();
    expect(fixture.componentInstance.targetId).toBe('flex.sumFlex');
  });

  it('closes through the service', () => {
    panel.open();
    fixture.detectChanges();
    fixture.componentInstance.close();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.tp-help-panel')).toBeNull();
  });
});
```

- [ ] **Step 3: Run both and confirm they fail**

Run: `cd eform-angular-frontend/eform-client && npx jest --testPathPatterns=time-planning-pn/help`
Expected: FAIL — modules not found.

- [ ] **Step 4: Implement `HelpPanelService`**

```ts
import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable } from 'rxjs';
import { HelpEntryId } from '../help.model';

@Injectable({ providedIn: 'root' })
export class HelpPanelService {
  private readonly openState = new BehaviorSubject<boolean>(false);
  private readonly targetState = new BehaviorSubject<HelpEntryId | null>(null);

  readonly isOpen$: Observable<boolean> = this.openState.asObservable();
  readonly target$: Observable<HelpEntryId | null> = this.targetState.asObservable();

  open(target?: HelpEntryId): void {
    this.targetState.next(target ?? null);
    this.openState.next(true);
  }

  close(): void {
    this.openState.next(false);
    this.targetState.next(null);
  }
}
```

- [ ] **Step 5: Implement `HelpPanelComponent`**

```ts
import { Component, EventEmitter, Input, OnDestroy, OnInit, Output } from '@angular/core';
import { Subscription } from 'rxjs';
import { HelpEntry, HelpEntryId, HelpProse, HelpSection, HelpUiStrings } from '../../help.model';
import { HelpContentService } from '../../services/help-content.service';
import { HelpPanelService } from '../../services/help-panel.service';
import { HelpSearchResult, HelpSearchService } from '../../services/help-search.service';

interface PanelSection {
  section: HelpSection;
  entries: HelpEntry[];
}

const SECTION_ORDER: HelpSection[] = ['task', 'toolbar', 'grid', 'dayCell', 'flex'];

@Component({
  selector: 'tp-help-panel',
  templateUrl: './help-panel.component.html',
  styleUrls: ['./help-panel.component.scss'],
  standalone: false,
})
export class HelpPanelComponent implements OnInit, OnDestroy {
  @Input() isAdmin = false;

  isOpen = false;
  targetId: HelpEntryId | null = null;
  query = '';
  results: HelpSearchResult[] = [];
  sections: PanelSection[] = [];
  expanded: HelpEntryId | null = null;

  private readonly subscriptions = new Subscription();

  @Output() replayTourRequested = new EventEmitter<void>();

  constructor(
    private helpContent: HelpContentService,
    private helpSearch: HelpSearchService,
    private helpPanel: HelpPanelService,
  ) {}

  get ui(): HelpUiStrings {
    return this.helpContent.ui();
  }

  get isSearching(): boolean {
    return this.query.trim().length > 0;
  }

  ngOnInit(): void {
    this.subscriptions.add(this.helpPanel.isOpen$.subscribe(isOpen => {
      this.isOpen = isOpen;
      if (isOpen) {
        this.buildSections();
      } else {
        this.query = '';
        this.results = [];
      }
    }));
    this.subscriptions.add(this.helpPanel.target$.subscribe(target => {
      this.targetId = target;
      this.expanded = target;
    }));
  }

  ngOnDestroy(): void {
    this.subscriptions.unsubscribe();
  }

  onQueryChange(query: string): void {
    this.query = query;
    this.results = this.helpSearch.search(query, { isAdmin: this.isAdmin });
  }

  clearQuery(): void {
    this.onQueryChange('');
  }

  toggleEntry(id: HelpEntryId): void {
    this.expanded = this.expanded === id ? null : id;
  }

  prose(id: HelpEntryId): HelpProse {
    return this.helpContent.prose(id);
  }

  sectionLabel(section: HelpSection): string {
    const labels: Record<HelpSection, string> = {
      task: this.ui.sectionTask,
      toolbar: this.ui.sectionToolbar,
      grid: this.ui.sectionGrid,
      dayCell: this.ui.sectionDayCell,
      flex: this.ui.sectionFlex,
    };
    return labels[section];
  }

  close(): void {
    this.helpPanel.close();
  }

  /**
   * Asks the host to replay the tour. The panel deliberately does not depend on
   * HelpTourService — it is built before the tour exists, and keeping the panel
   * independent of it means neither has to know about the other.
   */
  replayTour(): void {
    this.helpPanel.close();
    this.replayTourRequested.emit();
  }

  private buildSections(): void {
    const entries = this.helpContent.entries({ isAdmin: this.isAdmin });
    this.sections = SECTION_ORDER
      .map(section => ({ section, entries: entries.filter(entry => entry.section === section) }))
      .filter(group => group.entries.length > 0);
  }
}
```

- [ ] **Step 6: Write the template**

```html
<aside class="tp-help-panel" *ngIf="isOpen" role="complementary" [attr.aria-label]="ui.help">
  <header class="tp-help-panel__top">
    <h5>{{ ui.help }}</h5>
    <span class="tp-help-panel__spacer"></span>
    <button type="button" mat-icon-button [attr.aria-label]="ui.close" (click)="close()">
      <mat-icon>close</mat-icon>
    </button>
  </header>

  <div class="tp-help-panel__search">
    <mat-icon>search</mat-icon>
    <input
      type="search"
      [ngModel]="query"
      (ngModelChange)="onQueryChange($event)"
      [attr.aria-label]="ui.searchHelp"
      [placeholder]="ui.searchHelp" />
    <button type="button" *ngIf="isSearching" [attr.aria-label]="ui.clear" (click)="clearQuery()">
      <mat-icon>close</mat-icon>
    </button>
  </div>

  <div class="tp-help-panel__replay">
    <button type="button" (click)="replayTour()">{{ ui.replayTour }}</button>
  </div>

  <div class="tp-help-panel__body">
    <ng-container *ngIf="isSearching; else browse">
      <p class="tp-help-panel__count">{{ results.length }}</p>
      <article class="tp-help-entry" *ngFor="let result of results">
        <button type="button" class="tp-help-entry__head" (click)="toggleEntry(result.entry.id)">
          <span class="tp-help-entry__kind" [class.tp-help-entry__kind--task]="result.entry.kind === 'task'">
            {{ result.entry.kind }}
          </span>
          <b>{{ result.prose.title }}</b>
        </button>
        <p>{{ result.prose.short }}</p>
        <ol class="tp-help-entry__steps" *ngIf="expanded === result.entry.id && result.prose.steps">
          <li *ngFor="let step of result.prose.steps">{{ step }}</li>
        </ol>
      </article>
    </ng-container>

    <ng-template #browse>
      <ng-container *ngFor="let group of sections">
        <h6 class="tp-help-panel__section">{{ sectionLabel(group.section) }}</h6>
        <article
          class="tp-help-entry"
          *ngFor="let entry of group.entries"
          [class.tp-help-entry--target]="entry.id === targetId">
          <button type="button" class="tp-help-entry__head" (click)="toggleEntry(entry.id)">
            <b>{{ prose(entry.id).title }}</b>
          </button>
          <p>{{ prose(entry.id).short }}</p>
          <p class="tp-help-entry__detail" *ngIf="expanded === entry.id && prose(entry.id).detail">
            {{ prose(entry.id).detail }}
          </p>
          <ol class="tp-help-entry__steps" *ngIf="expanded === entry.id && prose(entry.id).steps">
            <li *ngFor="let step of prose(entry.id).steps">{{ step }}</li>
          </ol>
        </article>
      </ng-container>
    </ng-template>
  </div>
</aside>
```

All chrome labels come from `HelpUiStrings` (Task 1), never the `translate` pipe — the plugin's 25 shared locale files stay untouched.

- [ ] **Step 7: Write the SCSS**

```scss
.tp-help-panel {
  position: fixed;
  top: 0;
  right: 0;
  z-index: 900;
  display: flex;
  flex-direction: column;
  width: 340px;
  max-width: 100vw;
  height: 100vh;
  border-left: 1px solid var(--border, #e2e6e9);
  background: var(--bg, #ffffff);
  box-shadow: -8px 0 24px rgba(15, 19, 22, 0.08);

  &__top {
    display: flex;
    align-items: center;
    gap: 10px;
    padding: 13px 15px;
    border-bottom: 1px solid var(--border, #e2e6e9);

    h5 {
      margin: 0;
      font-size: 14px;
      font-weight: 600;
    }
  }

  &__spacer { flex: 1; }

  &__search {
    display: flex;
    align-items: center;
    gap: 8px;
    margin: 11px 15px;
    padding: 8px 13px;
    border: 1px solid var(--border, #e2e6e9);
    border-radius: 19px;

    input {
      flex: 1;
      border: 0;
      background: none;
      font-size: 13px;
      color: var(--text-header, #0f1316);

      &:focus { outline: none; }
    }

    button {
      border: 0;
      background: none;
      cursor: pointer;
    }
  }

  &__body {
    flex: 1;
    overflow-y: auto;
    padding-bottom: 16px;
  }

  &__section {
    margin: 14px 0 5px;
    padding: 0 15px;
    font-size: 10.5px;
    font-weight: 500;
    letter-spacing: 0.1em;
    text-transform: uppercase;
    color: var(--primary, #289694);
  }

  &__count {
    margin: 10px 15px 2px;
    font-size: 11px;
    color: var(--text-body, #7f868d);
  }
}

.tp-help-entry {
  padding: 7px 15px 9px;
  border-left: 2px solid transparent;

  &--target {
    border-left-color: var(--primary, #289694);
    background: var(--primary-light, #f5fcfc);
  }

  &__head {
    display: flex;
    align-items: center;
    gap: 7px;
    width: 100%;
    padding: 0;
    border: 0;
    background: none;
    text-align: left;
    cursor: pointer;

    b {
      font-size: 12.5px;
      font-weight: 500;
      color: var(--text-header, #0f1316);
    }
  }

  &__kind {
    flex: none;
    padding: 0 4px;
    border: 1px solid var(--border, #e2e6e9);
    border-radius: 3px;
    font-size: 9.5px;
    letter-spacing: 0.08em;
    text-transform: uppercase;
    color: var(--text-body, #7f868d);

    &--task {
      border-color: var(--primary, #289694);
      color: var(--primary, #289694);
    }
  }

  p {
    margin: 2px 0 0;
    font-size: 12px;
    line-height: 1.5;
    color: var(--text-body, #7f868d);
  }

  &__steps {
    margin: 8px 0 0;
    padding-left: 17px;

    li {
      margin-bottom: 3px;
      font-size: 12px;
      line-height: 1.5;
      color: var(--text-header, #0f1316);
    }
  }
}
```

- [ ] **Step 8: Declare `HelpPanelComponent` in `time-planning-pn.module.ts`** and ensure `FormsModule` is imported there.

- [ ] **Step 9: Run the tests and confirm they pass**

Run: `cd eform-angular-frontend/eform-client && npx jest --testPathPatterns=time-planning-pn/help`
Expected: PASS.

- [ ] **Step 10: Pre-commit gate** — code-reviewer and code-simplifier in parallel; resolve findings.

- [ ] **Step 11: Commit**

```bash
git add eform-client/src/app/plugins/modules/time-planning-pn/help \
        eform-client/src/app/plugins/modules/time-planning-pn/time-planning-pn.module.ts \
        eform-client/src/app/plugins/modules/time-planning-pn/i18n
git commit -m "feat(help): add searchable help side panel"
```

---

### Task 8: HelpTourService and tp-help-tour

**Files:**
- Create: `help/services/help-tour.service.ts`
- Create: `help/components/help-tour/help-tour.component.ts`
- Create: `help/components/help-tour/help-tour.component.html`
- Create: `help/components/help-tour/help-tour.component.scss`
- Modify: `time-planning-pn.module.ts`
- Test: `help/services/help-tour.service.spec.ts`
- Test: `help/components/help-tour/help-tour.component.spec.ts`

**Interfaces:**
- Consumes: `HelpContentService.tourEntries` (Task 2).
- Produces: `HelpTourService` with `state$: Observable<HelpTourState | null>`, `start(tour: HelpTourName, opts: { isAdmin: boolean }): void`, `next(): void`, `stop(): void`, `hasSeen(tour: HelpTourName): boolean`, `markSeen(tour: HelpTourName): void`; the exported interface `HelpTourState { entry: HelpEntry; index: number; total: number; }`; the storage key constant `TOUR_STORAGE_KEY = 'tp.planning.tour.v1'`; and `HelpTourComponent`, selector `tp-help-tour`.

- [ ] **Step 1: Write the failing test**

The tour must skip a step whose anchor is not in the DOM — that is the behaviour that keeps it working for a non-admin, and when the worker select is hidden because only one site exists.

```ts
import { TestBed } from '@angular/core/testing';
import { TranslateService } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';
import { HelpTourService, TOUR_STORAGE_KEY } from './help-tour.service';
import { HelpContentService } from './help-content.service';

describe('HelpTourService', () => {
  let service: HelpTourService;

  const anchor = (id: string) => {
    const element = document.createElement('div');
    element.setAttribute('data-tp-help', id);
    document.body.appendChild(element);
  };

  beforeEach(() => {
    document.body.innerHTML = '';
    localStorage.clear();
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        HelpTourService,
        HelpContentService,
        { provide: TranslateService, useValue: { currentLang: 'en-US' } },
      ],
    });
    service = TestBed.inject(HelpTourService);
  });

  it('is idle before it starts', async () => {
    expect(await firstValueFrom(service.state$)).toBeNull();
  });

  it('starts on the first step whose anchor exists', async () => {
    anchor('grid.dayCellAnatomy');
    service.start('page', { isAdmin: false });
    const state = await firstValueFrom(service.state$);
    expect(state?.entry.id).toBe('grid.dayCellAnatomy');
    expect(state?.index).toBe(0);
    expect(state?.total).toBe(1);
  });

  it('skips steps with no anchor in the DOM', async () => {
    anchor('toolbar.dateRange');
    anchor('grid.openDay');
    service.start('page', { isAdmin: false });
    let state = await firstValueFrom(service.state$);
    expect(state?.entry.id).toBe('toolbar.dateRange');
    expect(state?.total).toBe(2);
    service.next();
    state = await firstValueFrom(service.state$);
    expect(state?.entry.id).toBe('grid.openDay');
  });

  it('never offers the payroll step to a non-admin even when its anchor exists', async () => {
    anchor('toolbar.payrollExport');
    anchor('toolbar.dateRange');
    service.start('page', { isAdmin: false });
    const state = await firstValueFrom(service.state$);
    expect(state?.total).toBe(1);
    expect(state?.entry.id).toBe('toolbar.dateRange');
  });

  it('ends after the last step', async () => {
    anchor('toolbar.dateRange');
    service.start('page', { isAdmin: false });
    service.next();
    expect(await firstValueFrom(service.state$)).toBeNull();
  });

  it('does not start when no anchor is present', async () => {
    service.start('page', { isAdmin: false });
    expect(await firstValueFrom(service.state$)).toBeNull();
  });

  it('does not mark a tour seen merely by being subscribed to', async () => {
    // Regression guard: state$ replays null to every new subscriber.
    await firstValueFrom(service.state$);
    expect(service.hasSeen('page')).toBe(false);
  });

  it('marks the tour seen once it runs to the end', () => {
    anchor('toolbar.dateRange');
    service.start('page', { isAdmin: false });
    expect(service.hasSeen('page')).toBe(false);
    service.next();
    expect(service.hasSeen('page')).toBe(true);
  });

  it('marks the tour seen when it is skipped', () => {
    anchor('toolbar.dateRange');
    service.start('page', { isAdmin: false });
    service.stop();
    expect(service.hasSeen('page')).toBe(true);
  });

  it('does not mark a tour seen when it could not start for lack of anchors', () => {
    service.start('page', { isAdmin: false });
    expect(service.hasSeen('page')).toBe(false);
  });

  it('records that a tour has been seen', () => {
    expect(service.hasSeen('page')).toBe(false);
    service.markSeen('page');
    expect(service.hasSeen('page')).toBe(true);
    expect(localStorage.getItem(TOUR_STORAGE_KEY)).toContain('page');
  });

  it('survives localStorage being unavailable', () => {
    const getItem = jest.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new Error('blocked');
    });
    expect(service.hasSeen('page')).toBe(false);
    getItem.mockRestore();
  });
});
```

- [ ] **Step 2: Run it and confirm it fails**

Run: `cd eform-angular-frontend/eform-client && npx jest --testPathPatterns=time-planning-pn/help`
Expected: FAIL — `Cannot find module './help-tour.service'`.

- [ ] **Step 3: Implement `HelpTourService`**

```ts
import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable } from 'rxjs';
import { HelpEntry, HelpTourName } from '../help.model';
import { HelpContentService } from './help-content.service';

export const TOUR_STORAGE_KEY = 'tp.planning.tour.v1';

export interface HelpTourState {
  entry: HelpEntry;
  index: number;
  total: number;
}

@Injectable({ providedIn: 'root' })
export class HelpTourService {
  private readonly stateSubject = new BehaviorSubject<HelpTourState | null>(null);
  private steps: HelpEntry[] = [];
  private index = 0;
  private current: HelpTourName | null = null;

  readonly state$: Observable<HelpTourState | null> = this.stateSubject.asObservable();

  constructor(private helpContent: HelpContentService) {}

  /** Steps whose anchor is absent are dropped, never treated as an error. */
  start(tour: HelpTourName, opts: { isAdmin: boolean }): void {
    this.current = tour;
    this.steps = this.helpContent
      .tourEntries(tour, opts)
      .filter(entry => !!this.anchorElement(entry));
    this.index = 0;
    this.emit();
  }

  next(): void {
    this.index += 1;
    this.emit();
  }

  /** Skipping counts as having seen it — but only if a step was actually shown. */
  stop(): void {
    if (this.current && this.steps.length > 0) {
      this.markSeen(this.current);
    }
    this.current = null;
    this.steps = [];
    this.index = 0;
    this.stateSubject.next(null);
  }

  /** True while a tour is on screen. */
  get isRunning(): boolean {
    return this.stateSubject.value !== null;
  }

  anchorElement(entry: HelpEntry): HTMLElement | null {
    return entry.anchor
      ? document.querySelector<HTMLElement>(`[data-tp-help="${entry.anchor}"]`)
      : null;
  }

  hasSeen(tour: HelpTourName): boolean {
    return this.readSeen().includes(tour);
  }

  markSeen(tour: HelpTourName): void {
    const seen = this.readSeen();
    if (!seen.includes(tour)) {
      this.writeSeen([...seen, tour]);
    }
  }

  private emit(): void {
    const entry = this.steps[this.index];
    if (entry) {
      this.stateSubject.next({ entry, index: this.index, total: this.steps.length });
      return;
    }
    // Ran to the end. Record it here, in the service, rather than in the component:
    // state$ is a BehaviorSubject seeded null, so a component that marks "seen"
    // whenever it observes null would do so on its very first subscription — before
    // any tour has run — and the automatic first-run tour would never appear.
    // `steps.length` guards the other direction: a tour that could not start because
    // none of its anchors were in the DOM has not been seen, and must be offered again.
    if (this.current && this.steps.length > 0) {
      this.markSeen(this.current);
    }
    this.current = null;
    this.stateSubject.next(null);
  }

  private readSeen(): string[] {
    try {
      return JSON.parse(localStorage.getItem(TOUR_STORAGE_KEY) ?? '[]') as string[];
    } catch {
      return [];
    }
  }

  private writeSeen(seen: string[]): void {
    try {
      localStorage.setItem(TOUR_STORAGE_KEY, JSON.stringify(seen));
    } catch {
      // Storage unavailable (private mode, blocked cookies) — the tour simply reruns.
    }
  }
}
```

- [ ] **Step 4: Implement `HelpTourComponent`**

```ts
import { Component, Input, OnDestroy, OnInit } from '@angular/core';
import { ConnectedPosition } from '@angular/cdk/overlay';
import { Subscription } from 'rxjs';
import { HelpProse, HelpTourName } from '../../help.model';
import { HelpContentService } from '../../services/help-content.service';
import { HelpTourService, HelpTourState } from '../../services/help-tour.service';

@Component({
  selector: 'tp-help-tour',
  templateUrl: './help-tour.component.html',
  styleUrls: ['./help-tour.component.scss'],
  standalone: false,
})
export class HelpTourComponent implements OnInit, OnDestroy {
  @Input() tour: HelpTourName = 'page';
  @Input() isAdmin = false;

  state: HelpTourState | null = null;
  origin: HTMLElement | null = null;

  readonly positions: ConnectedPosition[] = [
    { originX: 'center', originY: 'bottom', overlayX: 'start', overlayY: 'top', offsetY: 10 },
    { originX: 'center', originY: 'top', overlayX: 'start', overlayY: 'bottom', offsetY: -10 },
  ];

  private readonly subscriptions = new Subscription();

  constructor(
    private helpContent: HelpContentService,
    private helpTour: HelpTourService,
  ) {}

  get prose(): HelpProse | null {
    return this.state ? this.helpContent.prose(this.state.entry.id) : null;
  }

  ngOnInit(): void {
    // Deliberately does NOT mark the tour seen here. state$ is a BehaviorSubject
    // seeded null, so this fires once at mount with state === null; marking seen
    // there would suppress the automatic first run. The service records it instead,
    // when a tour actually ends or is skipped.
    this.subscriptions.add(this.helpTour.state$.subscribe(state => {
      this.state = state;
      this.origin = state ? this.helpTour.anchorElement(state.entry) : null;
    }));
  }

  ngOnDestroy(): void {
    this.subscriptions.unsubscribe();
  }

  next(): void {
    this.helpTour.next();
  }

  skip(): void {
    this.helpTour.stop();
  }
}
```

- [ ] **Step 5: Write the template**

```html
<ng-template
  cdkConnectedOverlay
  [cdkConnectedOverlayOrigin]="origin!"
  [cdkConnectedOverlayOpen]="!!state && !!origin"
  [cdkConnectedOverlayPositions]="positions"
  [cdkConnectedOverlayPush]="true">
  <div class="tp-help-tour" role="dialog" *ngIf="prose as tourProse">
    <p class="tp-help-tour__step">
      {{ (state!.index + 1) }} / {{ state!.total }}
    </p>
    <h5>{{ tourProse.title }}</h5>
    <p class="tp-help-tour__body">{{ tourProse.short }}</p>
    <div class="tp-help-tour__actions">
      <button type="button" class="tp-help-tour__skip" (click)="skip()">{{ ui.skip }}</button>
      <button type="button" class="tp-help-tour__next" (click)="next()">{{ ui.next }}</button>
    </div>
  </div>
</ng-template>
```

Labels come from `HelpUiStrings`; add `get ui(): HelpUiStrings { return this.helpContent.ui(); }` to `HelpTourComponent`. No key is added to the plugin's shared locale files.

- [ ] **Step 6: Write the SCSS**

```scss
.tp-help-tour {
  width: 320px;
  padding: 15px 16px 13px;
  border: 1px solid var(--border, #e2e6e9);
  border-radius: 9px;
  background: var(--bg, #ffffff);
  box-shadow: 0 2px 6px rgba(15, 19, 22, 0.1), 0 12px 32px rgba(15, 19, 22, 0.16);

  h5 {
    margin: 0 0 6px;
    font-size: 14px;
    font-weight: 600;
  }

  &__step {
    margin: 0 0 7px;
    font-size: 10.5px;
    letter-spacing: 0.1em;
    text-transform: uppercase;
    color: var(--primary, #289694);
  }

  &__body {
    margin: 0 0 13px;
    font-size: 12.5px;
    line-height: 1.5;
    color: var(--text-body, #7f868d);
  }

  &__actions {
    display: flex;
    gap: 9px;
    align-items: center;
  }

  &__skip,
  &__next {
    padding: 7px 15px;
    border: 1px solid var(--primary, #289694);
    border-radius: 18px;
    font-size: 12.5px;
    font-weight: 500;
    cursor: pointer;
  }

  &__skip {
    background: transparent;
    color: var(--primary, #289694);
  }

  &__next {
    background: var(--primary, #289694);
    color: #ffffff;
  }
}
```

- [ ] **Step 6b: Write the component regression spec**

The bug this guards against is subtle and silent: a component that marks the tour seen
whenever it observes a null state does so at mount, because `state$` is a
`BehaviorSubject` seeded null — and the automatic first run then never happens.

```ts
import { TestBed } from '@angular/core/testing';
import { OverlayModule } from '@angular/cdk/overlay';
import { TranslateService } from '@ngx-translate/core';
import { HelpTourComponent } from './help-tour.component';
import { HelpTourService } from '../../services/help-tour.service';

describe('HelpTourComponent', () => {
  beforeEach(() => {
    localStorage.clear();
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      declarations: [HelpTourComponent],
      imports: [OverlayModule],
      providers: [{ provide: TranslateService, useValue: { currentLang: 'en-US' } }],
    });
  });

  it('does not mark the tour seen just by being mounted', () => {
    const fixture = TestBed.createComponent(HelpTourComponent);
    fixture.componentInstance.tour = 'page';
    fixture.detectChanges();
    expect(TestBed.inject(HelpTourService).hasSeen('page')).toBe(false);
  });

  it('shows no card while no tour is running', () => {
    const fixture = TestBed.createComponent(HelpTourComponent);
    fixture.detectChanges();
    expect(fixture.componentInstance.state).toBeNull();
  });
});
```

- [ ] **Step 7: Declare `HelpTourComponent` in `time-planning-pn.module.ts`**

- [ ] **Step 8: Run the tests and confirm they pass**

Run: `cd eform-angular-frontend/eform-client && npx jest --testPathPatterns=time-planning-pn/help`
Expected: PASS.

- [ ] **Step 9: Pre-commit gate** — code-reviewer and code-simplifier in parallel; resolve findings.

- [ ] **Step 10: Commit**

```bash
git add eform-client/src/app/plugins/modules/time-planning-pn/help \
        eform-client/src/app/plugins/modules/time-planning-pn/time-planning-pn.module.ts \
        eform-client/src/app/plugins/modules/time-planning-pn/i18n
git commit -m "feat(help): add guided tour that skips steps with no anchor"
```

---

### Task 9: Wire the surfaces into the page

**Files:**
- Modify: `components/plannings/time-plannings-container/time-plannings-container.component.html` (toolbar `?` button after the `div.line-vert` at :78; `data-tp-help` on the toolbar controls; mount `tp-help-panel` and `tp-help-tour`)
- Modify: `components/plannings/time-plannings-container/time-plannings-container.component.ts` (open panel, start page tour once)
- Modify: `components/plannings/time-plannings-table/time-plannings-table.component.html` (`data-tp-help` anchors, `tp-help-hint` under the grid)
- Modify: `components/plannings/time-planning-actions/workday-entity/workday-entity-dialog.component.html` (`tp-help-icon` on the field groups, `data-tp-help` anchors, `tp-help-hint` for the future-date case, `tp-help-tour` for the dialog tour)
- Modify: `components/plannings/time-planning-actions/workday-entity/workday-entity-dialog.component.ts` (start the dialog tour once)
- Test: `help/help-wiring.spec.ts`

**Interfaces:**
- Consumes: everything from Tasks 5-8.
- Produces: no new exports. This task is additive markup plus two small container methods.

- [ ] **Step 1: Write the failing wiring test**

This is the test that stops the anchors rotting. It reads the templates off disk and asserts each side of the contract.

```ts
import { readFileSync } from 'fs';
import { join } from 'path';
import { PLANNING_HELP_ENTRIES } from './planning-help.registry';

const MODULE_ROOT = join(__dirname, '..');

const TEMPLATES = [
  'components/plannings/time-plannings-container/time-plannings-container.component.html',
  'components/plannings/time-plannings-table/time-plannings-table.component.html',
  'components/plannings/time-planning-actions/workday-entity/workday-entity-dialog.component.html',
].map(relative => readFileSync(join(MODULE_ROOT, relative), 'utf8'));

const MARKUP = TEMPLATES.join('\n');

describe('help wiring', () => {
  it('anchors every entry that a tour needs', () => {
    for (const entry of PLANNING_HELP_ENTRIES.filter(e => e.tourStep !== undefined)) {
      expect(MARKUP).toContain(`data-tp-help="${entry.anchor}"`);
    }
  });

  it('only uses helpIds that exist in the registry', () => {
    const known = new Set(PLANNING_HELP_ENTRIES.map(e => e.id));
    const used = [...MARKUP.matchAll(/helpId="([^"]+)"/g)].map(match => match[1]);
    expect(used.length).toBeGreaterThan(0);
    for (const id of used) {
      expect(known.has(id as never)).toBe(true);
    }
  });

  it('only uses anchors that exist in the registry', () => {
    const known = new Set(PLANNING_HELP_ENTRIES.map(e => e.anchor).filter(Boolean));
    const used = [...MARKUP.matchAll(/data-tp-help="([^"]+)"/g)].map(match => match[1]);
    for (const anchor of used) {
      expect(known.has(anchor)).toBe(true);
    }
  });

  it('mounts the panel and both tours exactly once', () => {
    expect((MARKUP.match(/<tp-help-panel/g) ?? []).length).toBe(1);
    expect((MARKUP.match(/tour="page"/g) ?? []).length).toBe(1);
    expect((MARKUP.match(/tour="dialog"/g) ?? []).length).toBe(1);
  });

  it('anchors the flex entries on the page, not only in the panel', () => {
    for (const id of ['flex.whatIsFlex', 'flex.sumFlex', 'flex.paidOutFlexRelation']) {
      expect(MARKUP).toContain(`data-tp-help="${id}"`);
    }
  });

  it('starts each tour from a component, since mounting alone does not', () => {
    const containerTs = readFileSync(join(MODULE_ROOT,
      'components/plannings/time-plannings-container/time-plannings-container.component.ts'), 'utf8');
    const dialogTs = readFileSync(join(MODULE_ROOT,
      'components/plannings/time-planning-actions/workday-entity/workday-entity-dialog.component.ts'), 'utf8');
    expect(containerTs).toMatch(/start\(\s*'page'/);
    expect(dialogTs).toMatch(/start\(\s*'dialog'/);
  });

  it('never uses the translate pipe inside help templates', () => {
    const helpTemplates = [
      'help/components/help-panel/help-panel.component.html',
      'help/components/help-icon/help-icon.component.html',
      'help/components/help-tour/help-tour.component.html',
      'help/components/help-hint/help-hint.component.html',
    ].map(relative => readFileSync(join(MODULE_ROOT, relative), 'utf8')).join('\n');
    expect(helpTemplates).not.toContain('| translate');
  });

  it('binds the help-icon and panel outputs, or they are inert', () => {
    expect(MARKUP).toContain('(openInPanel)=');
    expect(MARKUP).toContain('(replayTourRequested)=');
  });

  it('does not introduce new translate keys for help chrome', () => {
    // The help button's tooltip must come from HelpUiStrings, not a new shared key.
    expect(MARKUP).not.toContain("'Help' | translate");
  });
});
```

- [ ] **Step 2: Run it and confirm it fails**

Run: `cd eform-angular-frontend/eform-client && npx jest --testPathPatterns=time-planning-pn/help`
Expected: FAIL — no `data-tp-help` attributes exist yet.

- [ ] **Step 3: Add the `?` button and mounts to the container template**

After the last `div.line-vert` (currently line 78), as a sibling of the existing icon buttons:

```html
<button
  mat-icon-button
  type="button"
  class="btn-secondary btn-secondary--icon-rounded-border"
  [matTooltip]="helpUi.help"
  (click)="openHelp()">
  <mat-icon>help_outline</mat-icon>
</button>
```

Add `data-tp-help` to the toolbar controls that carry an anchor: `toolbar.showResigned`, `toolbar.navBackward`, `toolbar.navForward`, `toolbar.workerFilter`, `toolbar.tagFilter`, `toolbar.dateRange`, `toolbar.downloadExcel`, `toolbar.payrollExport`, `toolbar.reload`.

At the end of the container template, outside `eform-new-subheader`:

```html
<tp-help-panel [isAdmin]="isAdmin" (replayTourRequested)="replayPageTour()"></tp-help-panel>
<tp-help-tour tour="page"></tp-help-tour>
```

Every `tp-help-icon` on the page binds its `openInPanel` output so the popover's
"More in help" link actually opens the panel on that entry:

```html
<tp-help-icon helpId="toolbar.dateRange" (openInPanel)="openHelp($event)"></tp-help-icon>
```

- [ ] **Step 4: Add the two container methods**

In `time-plannings-container.component.ts`, injecting `HelpContentService`, `HelpPanelService` and `HelpTourService`:

```ts
get helpUi(): HelpUiStrings {
  return this.helpContent.ui();
}

openHelp(target?: HelpEntryId): void {
  this.helpPanel.open(target);
}

replayPageTour(): void {
  // The panel has already closed itself; let that settle before querying anchors.
  setTimeout(() => this.helpTour.start('page', { isAdmin: this.isAdmin }));
}

private startTourOnce(): void {
  if (!this.helpTour.hasSeen('page')) {
    setTimeout(() => this.helpTour.start('page', { isAdmin: this.isAdmin }));
  }
}
```

Call `startTourOnce()` at the end of the existing plannings-loaded handler, so the grid is rendered and the anchors exist. The `setTimeout` lets the current change-detection pass finish before the DOM is queried.

- [ ] **Step 5: Add anchors and the hint to the table template**

`data-tp-help` on: the pinned name cell (`grid.nameColumn`), the tag chips (`grid.tagChips`), the settings strip (`grid.settingsStrip`), a day cell (`grid.dayCellAnatomy` and `grid.openDay`), the weekly total (`grid.weeklyPlannedHours`), the message icons (`grid.messageIcons`), the name column header (`grid.sortName`).

Under the grid:

```html
<tp-help-hint helpId="grid.nameColumn"></tp-help-hint>
```

- [ ] **Step 6: Add icons, anchors, hints and the dialog tour to the workday dialog**

`tp-help-icon` beside the Planned group (`dayCell.plannedTimes`), the Registered group (`dayCell.actualTimes`), the registered pause (`dayCell.resetPauseToRecorded`), Plan hours (`dayCell.planHours`), Netto override (`dayCell.nettoOverride`), Paid-out flex (`dayCell.paidOutFlex`), the day-type checkboxes (`dayCell.flags`), **the Save button (`dayCell.save`)**, the version-history button (`dayCell.versionHistory`), the shift rows (`dayCell.shiftCount`), a per-field reset (`dayCell.resetField`), the GPS button (`dayCell.gps`), the snapshot button (`dayCell.snapshot`), and the timepickers (`dayCell.oneMinuteIntervals`). Matching `data-tp-help` on each.

`dayCell.save` is not optional: it is `tourStep: 6` of the dialog tour, and Step 1's wiring test asserts an anchor exists for every entry carrying a `tourStep`.

The three flex entries get icons and anchors too, next to the figures they explain — `flex.whatIsFlex` and `flex.paidOutFlexRelation` beside the paid-out-flex field in this dialog, and `flex.sumFlex` beside the flex sum. These are the numbers the spec calls out as most likely to mislead, so reaching them only through the panel would miss the point.

Then, at the end of the dialog template:

```html
<tp-help-hint helpId="dayCell.futureDisabled" tone="warn" *ngIf="isInTheFuture"></tp-help-hint>
<tp-help-tour tour="dialog"></tp-help-tour>
```

`tp-help-tour` takes `[tour]` only. It has no `isAdmin` input — admin filtering happens inside `HelpTourService.start(tour, { isAdmin })`, which the container and the dialog each call. Each mounted instance renders only its own tour, so the page and dialog instances do not collide.

- [ ] **Step 6b: Start the dialog tour**

Mounting `tp-help-tour` only subscribes to state — nothing starts a tour. Without this the dialog tour can never fire. In `workday-entity-dialog.component.ts`, injecting `HelpTourService`:

```ts
private startDialogTourOnce(): void {
  if (!this.helpTourService.hasSeen('dialog')) {
    setTimeout(() => this.helpTourService.start('dialog', { isAdmin: false }));
  }
}
```

Call it at the end of `ngOnInit`, after the form is built, so the anchors exist in the DOM.

If the dialog tears the tour down explicitly when it closes, it must call
`HelpTourService.abort()`, **not** `stop()`. `stop()` marks the tour seen — it is the
user-initiated end, used by Skip, Escape and completion. `abort()` ends the tour without
marking it seen, and is for the page changing underneath: someone who opens a row,
glances and closes it has not seen the tour and must still be offered it.

- [ ] **Step 7: Run the tests and confirm they pass**

Run: `cd eform-angular-frontend/eform-client && npx jest --testPathPatterns=time-planning-pn`
Expected: PASS — the whole plugin suite, to prove nothing else regressed.

- [ ] **Step 8: Verify the build compiles**

Run: `cd eform-angular-frontend/eform-client && npx ng build --configuration development`
Expected: build succeeds. Template errors do not surface in Jest, so this step is required before the commit.

- [ ] **Step 9: Pre-commit gate** — code-reviewer and code-simplifier in parallel; resolve findings.

- [ ] **Step 10: Commit**

```bash
git add eform-client/src/app/plugins/modules/time-planning-pn
git commit -m "feat(help): wire help icon, panel, tours and hints into the planning page"
```

---

### Task 10: Ship

- [ ] **Step 1: Push the branch**

```bash
cd /home/rene/Documents/workspace/microting/eform-angular-timeplanning-plugin
git push -u origin feat/planning-help-system
```

- [ ] **Step 2: Open the PR toward `stable`**

```bash
gh pr create --base stable --title "feat(help): searchable in-page help for the planning page" --body "..."
```

- [ ] **Step 3: Watch CI**

```bash
gh pr checks --watch
```

The `angular-unit-test` job runs the new specs. If it fails, investigate — do not rewrite existing tests to make it pass. Only fix tests added by this plan. After any fix, re-run the pre-commit gate before committing.

- [ ] **Step 4: Merge once green**

---

## Self-Review

**Spec coverage.** Content model → Task 1. Locale coverage and fallback → Tasks 2, 3. Search → Task 4. ⓘ icon → Task 5. Inline hint → Task 6. Side panel → Task 7. Tours and anchor-skipping → Task 8. Anchoring and template wiring → Task 9. Admin filtering → covered by tests in Tasks 2, 4, 7, 8. Copy rules → enforced by the banned-word tests in Tasks 1 and 3. Testing section → each task's tests. Out-of-scope items → Global Constraints.

**Deviation from the spec, recorded deliberately.** The spec's `HelpSection` union listed `shifts` and `flags`; no entry uses them, so this plan omits both. The spec also described the ⓘ overlay as using `cdkConnectedOverlayUsePopover="inline"`; that input does not exist in CDK 20.2.14, and the spec has been corrected — this plan uses a standard `cdkConnectedOverlay`.

**Fixed after review.** Both reviewers ran against the first draft of this plan; these are the changes their findings produced.

- The local test loop did not work at all. Jest runs from the frontend repo and its `testMatch` is scoped to that `rootDir`, so specs written only in the plugin repo were never discovered — every "run it and confirm it fails" step would have reported `No tests found`. Task 0 now establishes the `--roots` invocation and the `node_modules` symlink it needs, both verified end to end with a throwaway Angular TestBed spec before this plan was finalised.
- The plan contradicted its own constraint by adding UI labels to the 25 shared locale files. Those labels are now `HelpUiStrings` in `help/i18n/`, and a wiring test asserts no help template uses the `translate` pipe.
- The dialog tour could never start — mounting `tp-help-tour` only subscribes. Task 9 Step 6b adds the trigger, the dialog `.ts` is now in Task 9's file list, and a wiring test asserts both tours are started from a component.
- "Replayable from the panel" was specified but never built. The panel now has a replay action.
- `HelpTourComponent` marked the tour seen at mount, because `state$` replays `null` to new subscribers — which would have suppressed the automatic first run for every genuine first-time user. Recording moved into the service, guarded so a tour that could not start is not marked seen, with four service tests and a component regression spec.
- `dayCell.save` was required by the dialog tour and by Task 9's own wiring test, but Step 6 never anchored it. The three `flex.*` entries were reachable only through the panel, despite being the numbers the spec calls most misleading. Both now anchored.
- The "ranks tasks above controls" test was vacuous when a query returned only tasks; it now asserts both groups are present first.
- The Danish banned-word regex missed `administratoren` and `administratorens`, the forms most likely to be written. Now `\badministrator\w*\b`.
- Three tests asserted prose text that Task 1 never mandates ("Date range", "future"), so a copywriting choice could fail them. They now compare against the content file.
- Corrected line citations and one overstatement: the `.help-text` pattern is used twice in this plugin, not ~8 times.

**Type consistency.** `HelpEntryId`, `HelpEntry`, `HelpProse`, `HelpProseMap` are defined once in Task 1 and used unchanged thereafter. `HelpContentService.prose/entry/entries/tourEntries/localeProse` are consumed with those exact names in Tasks 4, 5, 6, 7, 8. `HelpSearchResult` is defined in Task 4 and consumed in Task 7. `HelpTourState` and `TOUR_STORAGE_KEY` are defined in Task 8 and used in its own component and spec. `HelpPanelService.open/close/isOpen$/target$` are defined in Task 7 and called from Task 9.

**One deliberate content decision.** Tasks 1 and 3 each write 48 prose entries. The plan gives the complete id list, the enforced authoring rules, the machine-checked constraints, and fully worked exemplars of every shape (task with steps and detail, control with detail, control without). Writing the remaining entries is the content work itself, not a placeholder — and the registry spec fails until all 48 exist, so completeness is verified rather than assumed.
