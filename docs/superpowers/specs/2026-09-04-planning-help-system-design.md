# In-page help system for the planning page

**Date:** 2026-09-04
**Status:** Design approved, implementation plan not yet written
**Scope:** `plugins/time-planning-pn/planning` only

## Problem

A team lead or planner opens the planning page and can edit almost everything on it,
but nothing on the page explains the rules they are editing against. Flex, pauses,
netto hours, the difference between planned and actual times, what saving actually
triggers — none of it is written down anywhere the user can reach. Customer support
answers the same questions repeatedly.

The page carries 75 `matTooltip`s, but they are icon labels ("Download Excel",
"Reload table"), not explanations. There is no help affordance of any kind — no `?`
button, no popover, no tour — anywhere in either repo.

## Audience

Primary: the **team lead / planner** — a non-admin who plans hours for a group of
workers, can edit rows, and does not know the rules.

Secondary: **customer support and onboarding** — the goal is that an answer written
once appears everywhere a user might look for it.

## What a non-admin can actually do

The planning page is **not admin-gated**. Its route guard requires only the
`time_planning_plugin_access` claim (`time-planning-pn.routing.ts:19-24`). Inside the
page, exactly two things are admin-only:

| Control | Gate |
|---|---|
| Export to payroll button | `time-plannings-container.component.html:88`, and server-side via `PayrollExportController.cs:12` |
| Assigned-site dialog (click on worker name) | `time-plannings-table.component.ts:370-372`, **client-side only** — the endpoint it calls checks the `GetWorkingHours` claim, not the admin role |

Everything else — filters, navigation, Excel download, reload, and the entire day-cell
editor — is available to any user who can load the page. That editor,
`WorkdayEntityDialogComponent`, is a 2001-line component with up to five shifts of
planned and actual times, per-field resets, GPS and snapshot viewers, plan hours,
netto override, paid-out flex, flag checkboxes and comments. It is where a planner is
lost, and it is therefore the centre of gravity for this work.

## Approach

One **help registry** defines every explainable thing on the page. Four surfaces read
from it. Support edits an answer once and it appears in all four.

Rejected alternatives:

- **Flat translate keys in the existing locale files.** No new machinery, but it
  roughly doubles each of the 25 locale files, buries UI labels among prose, and makes
  every wording tweak a 25-file diff.
- **Backend-served help content.** Editable without a release, but requires a new API,
  table and migration in a `-base` repo, which base dev mode cannot touch.

## Content model

`time-planning-pn/help/planning-help.registry.ts` — structure only, no prose:

```ts
export type HelpSection =
  | 'task' | 'toolbar' | 'grid' | 'dayCell' | 'shifts' | 'flex' | 'flags';

export type HelpKind = 'control' | 'task';

export interface HelpEntry {
  id: HelpEntryId;      // stable identifier, e.g. 'dayCell.actualPause'
  kind: HelpKind;
  section: HelpSection;
  anchor?: string;      // data-tp-help value; required if tourStep is set
  tourStep?: number;    // present = included in a tour; value = order within it
  tour?: 'page' | 'dialog';
  adminOnly?: boolean;  // filtered out when the current user is not an admin
  related?: HelpEntryId[];  // tasks only: the controls the task touches
}
```

Prose lives separately, in `help/i18n/enUS.ts` and `help/i18n/da.ts`:

```ts
export const enUS: Record<HelpEntryId, {
  title: string;
  short: string;
  detail?: string;
  steps?: string[];     // tasks only, in order
  keywords: string[];   // search synonyms, in this locale's language
}> = { ... };
```

`short` is one or two sentences and is what the ⓘ popover and the panel summary show.
`detail` is an optional further paragraph shown only in the panel.

### Controls and tasks

A **control** entry answers "what is this thing?" and is anchored to something on
screen. A **task** entry answers "how do I do X?", has ordered `steps`, and is anchored
to nothing.

Both are needed, and the distinction is what makes the panel searchable. A planner who
needs to register vacation does not search for a control — no control on the page is
called "vacation". They search for the task. Control entries alone would return
nothing for the most common question the page receives.

Tours are built from control entries only; tasks have no `anchor` and no `tourStep`.

`HelpContentService` resolves prose against ngx-translate's `currentLang` and falls
back to English **per entry**, so a partially translated Danish file degrades
entry-by-entry rather than all-or-nothing.

The existing 25 locale files under `time-planning-pn/i18n/` are not modified.

### Locale coverage

English and Danish are authored properly. The other 23 locales fall back to English.
Adding a locale later is a pure content commit — a new file under `help/i18n/`, no code
change. Machine-translating ~1750 strings up front was rejected as worse than honest
English.

## The four surfaces

| Surface | Component | Reads |
|---|---|---|
| ⓘ icon | `<tp-help-icon helpId>` | `short` |
| Side panel | `<tp-help-panel>` | all entries, grouped by `section` |
| Tour | `HelpTourService` | entries with `tourStep`, positioned via `anchor` |
| Inline hint | `<tp-help-hint helpId>` | `short` |

**ⓘ icon.** A small `mat-icon-button` whose `aria-label` comes from the entry. Opens a
`cdkConnectedOverlay` anchored to the button via `cdkOverlayOrigin`, with a
close-on-scroll strategy and fallback positions. Dismissed on Escape
(`overlayKeydown`), an outside click (`overlayOutsideClick`), and scroll.

Deliberately **no backdrop**. A transparent full-page backdrop swallows the first
click, and on this page every day cell is a click target that opens the day editor —
so a planner dismissing a popover would have to click twice to reach the cell
underneath. `overlayOutsideClick` gives the same dismissal without the tax.

This has to work inside a `MatDialog`, because roughly half the help lives in one. It
does: CDK appends every overlay to the same `.cdk-overlay-container`, and an overlay
opened while a dialog is up is appended after the dialog pane, so it stacks above it.
The proof is already in these two dialogs — `AssignedSiteDialogComponent` and
`WorkdayEntityDialogComponent` between them host 23 overlay-based controls
(`ngx-material-timepicker`, `matDatepicker`, `mtx-select`) that open correctly today.

Note for the implementer: `cdkConnectedOverlayUsePopover` (native top-layer rendering)
does **not** exist in the installed `@angular/cdk` **20.2.14** — it is a later addition.
Do not reach for it; the standard overlay container behaviour described above is what
this design relies on.

**Search.** A field at the top of the panel, focused when the panel is opened from the
`?` button. With no query the panel shows its normal grouped browse view; search is
additive, never a replacement for browsing.

Matching is case-insensitive and diacritic-insensitive — Danish `å æ ø` must fold, or
a user typing `laege` finds nothing — across `title`, `keywords`, `short`, `detail` and
`steps`. It runs against **both the active locale and the English fallback**, so a
Danish user who types an English term still finds the entry, and vice versa. Both are
already loaded, so this costs nothing.

Matching is substring, not fuzzy. Across ~48 entries fuzzy matching adds noise rather
than recall.

Results are ordered: **tasks before controls** — someone typing into a help box wants a
how-to, not a definition — then by where the match landed, title before keyword before
body. Each result shows its `title` and `short`; expanding a task reveals its `steps`.

When nothing matches, the panel names the query and lists the tasks rather than showing
an empty result — a dead end is the one outcome a help search must not produce.

`keywords` is what makes this work at all. A Danish planner types *ferie*, *sygdom*,
*fri*, *afspadsering* or *barsel*, and none of those strings appear anywhere in the
English prose. Keywords carry the synonyms, per locale, and are authored as
deliberately as the prose.

**Side panel.** A right-side slide-in opened by a single `?` button placed in the
container toolbar after the last `div.line-vert`
(`time-plannings-container.component.html:78`), styled
`btn-secondary--icon-rounded-border` to match the five icon buttons already there.
Entries render grouped by section in registry order. Opening the panel from an ⓘ's
"More" link scrolls to that entry.

**Tour.** `HelpTourService` walks entries with a `tourStep`, positioning the same CDK
overlay against `[data-tp-help="<id>"]`. Runs once automatically per user
(`localStorage` key `tp.planning.tour.v1`) and is replayable from the panel. **A step
whose anchor is absent from the DOM is skipped, not treated as an error** — this is
required, because the worker select only renders when `availableSites.length > 1` and
the payroll button only renders for admins.

**Inline hint.** Renders the plugin's existing `div.help-text` pattern — a
`mat-icon>info` beside a span. It is used in exactly two places today
(`pay-day-rule-form.component.html:105-108` and
`day-type-rule-dialog.component.html:161`), so this work both reuses and
standardises it. Used where the page currently explains
nothing:

- Fields disabled because the date is in the future
- The "Total planned hours cannot exceed 24" validation
- An empty grid when filters match no workers
- **The worker column**, which packs four unlabelled things into one cell — name,
  agreed weekly hours, tags, and a strip of status icons for the rules that apply to
  that worker. The hint names what is being shown.

## Anchoring

Anchors are `data-tp-help="<id>"` **attributes added to templates**, never CSS
selectors. `mtx-grid` regenerates DOM and its class names are shared across columns, so
selector-based anchoring would be silently fragile. This adds roughly 35 attributes
across the container, table and workday-dialog templates. It is the only invasive part
of the change, and it is purely additive — attributes and ⓘ elements, with no logic
touched.

**Two tours, not one.** A tour cannot span the page and a modal in a single run. The
`page` tour covers the toolbar and grid and ends by inviting the user to open a day.
The `dialog` tour is offered from inside `WorkdayEntityDialogComponent`.

## Entry inventory

Approximately 48 entries — 36 controls and 12 tasks.

### `task` (12)

`registerVacation`, `registerSickness`, `registerDayOff`, `correctRegisteredTime`,
`addMissingRegistration`, `addExtraShift`, `changePlannedHours`, `payOutFlex`,
`exportForPayroll`, `whoChangedThis`, `whereWasThisRegistered`, `filterToOneTeam`.

Each carries `steps` and a `related` list of the controls it touches, so a task ends by
pointing at the control entries that explain the fields it just told the user to fill.

Three of these describe the day flags, and they carry a rule the UI actively hides.
The flags render as checkboxes but are **mutually exclusive** — ticking one unticks the
rest — and ticking one rewrites netto hours
(`workday-entity-dialog.component.ts:1263-1290`):

| Flag | Resulting netto hours |
|---|---|
| `DayOff`, `VacationDayOff` | `0` |
| `Vacation`, `Sick`, `Course`, `LeaveOfAbsence`, `Maternity`, `Holiday`, and the rest | the day's planned hours |

The trap is **name versus behaviour**, not adjacency. In render order the types are
`DayOff`, `Vacation`, `Sick`, `Course`, `LeaveOfAbsence`, `Children1stSick`,
`Children2stSick`, `TimeOff`, `Maternity`, `VacationDayOff`, `Holiday`,
`PregnancyLeave` — so `Vacation` (2nd) and `VacationDayOff` (10th) are nowhere near
each other. What actually catches people is that **`TimeOff` keeps the planned hours**
while the similarly-named `DayOff` and `VacationDayOff` zero them.

It is worse in Danish, where the shipped labels are `Fridag` (DayOff, zero),
`Afspadsering` (VacationDayOff, zero) and **`Ferie fridag` (TimeOff, keeps the hours
despite being named a fridag)**.

`registerVacation`, `registerDayOff` and `dayCell.flags` must state which types count
as worked time; this is the single most valuable thing the help system can say.

The full flag set is `TimePlanningMessagesEnum`: `DayOff`, `Vacation`, `Sick`,
`Course`, `LeaveOfAbsence`, `Children1stSick`, `Children2stSick`, `TimeOff`,
`Maternity`, `VacationDayOff`, `Holiday`, `PregnancyLeave`. `Blank` and `Care` are
excluded from the UI (`:242`) and get no entries. Note that `Care` is not a member of
`TimePlanningMessagesEnum` at all — that half of the check is dead defensive code.

### `toolbar` — controls (9)

`showResigned`, `navBackward`, `navForward`, `workerFilter`, `tagFilter`, `dateRange`,
`downloadExcel`, `payrollExport` (adminOnly), `reload`.

### `grid` — controls (8)

`nameColumn`, `tagChips` (click-to-filter), `settingsStrip` (the pay-rule /
mobile-registration / over-midnight / auto-break / one-minute / extra-shifts status
badges), `dayCellAnatomy` (planned versus actual, and the icon legend),
`weeklyPlannedHours`, `messageIcons`, `sortName`, `openDay`.

`weeklyPlannedHours` deserves care: the page renders **two different** `plannedHours`
elements — a weekly total and a per-day-cell value. The help text must name which one
it describes, or it will actively mislead.

### `dayCell` — controls (16)

`versionHistory`, `plannedTimes`, `actualTimes`, `shiftCount`, `resetField`,
`resetPauseToRecorded`, `gps`, `snapshot`, `futureDisabled`, `planHours`,
`nettoOverride`, `paidOutFlex`, `flags`, `commentOffice`, `save`,
`oneMinuteIntervals` (the timepicker's `minutesGap` is 1 or 5 depending on the
worker's setting).

### `flex` — controls (3)

`whatIsFlex`, `sumFlex`, `paidOutFlexRelation`.

The flex entries must describe **what the user sees and controls**, not restate the
server's arithmetic. The `SumFlexEnd - PaiedOutFlex` calculation is duplicated in
roughly five places in the backend and `PaiedOutFlexInSeconds` is frequently
unpopulated, so help text asserting a precise formula risks contradicting what the
page actually displays for a given worker.

## Admin-awareness

Exactly one entry carries `adminOnly`: `toolbar.payrollExport`. The panel and both
tours filter it through `selectAuthIsAdmin$` (`auth.selector.ts:17-18`).

`grid.nameColumn` is not `adminOnly` — it describes what the column *displays*, which
every user sees. It says nothing about the dialog behind it.

Every other entry is shown to all users, which matches how the page is actually gated.

## Copy rules

**"Admin" means Microting, not a customer role.** Help copy therefore never mentions
administrator capabilities, never explains what someone with more access could do, and
never accounts for why a control did nothing. An entry describes what the reader sees
and what the reader can do — nothing else.

This rules out a whole tempting category of text. The worker column is the clearest
case: clicking it opens a settings dialog for Microting staff and silently does nothing
for everyone else, and the entry must still confine itself to describing the four
things the column displays. "This opens worker settings for administrators" is exactly
the sentence not to write.

Two further rules, both already exercised above:

- Name which value is meant when a label is reused. `plannedHours` renders as both a
  weekly total and a per-day-cell value.
- Describe what the screen shows, not how the server computes it — see the flex note
  under the entry inventory.

## Testing

- **`HelpContentService` fallback** — an entry missing from `da.ts` resolves to the
  English text; a present entry does not.
- **Registry integrity** — every entry with a `tourStep` also declares a `tour` and an
  `anchor`; no `task` entry has an `anchor` or a `tourStep`; every `related` id resolves;
  every entry has at least one keyword in `enUS`; `tourStep` values are unique within each tour; every `helpId` referenced in
  a template exists in the registry; every registry id has prose in `enUS`. This is the
  test that prevents rot: it fails when someone typos an id, or deletes a control and
  leaves its help entry behind.
- **Search** — a Danish query folds diacritics, asserted against a term that actually
  appears in the content (`lon` finds the entries keyworded *løn*) and asserting a
  non-empty result, since two queries that both match nothing compare equal regardless
  of whether folding works; an English query
  finds a Danish-only entry through the English fallback; tasks sort above controls; a
  query matching nothing returns the task list rather than an empty result.
- **Admin filtering** — a non-admin sees neither `adminOnly` entry in the panel, and
  the page tour skips the payroll step.

No end-to-end tests. Per the project's standing practice, changes are pushed and
verified in CI rather than run locally.

## Out of scope

- Any backend change, API, or migration
- Any new npm dependency — no tour library exists in either repo, and the tour is
  roughly 200 lines over the CDK overlay that Angular Material 20.2.14 already provides
- Any change to `eform-angular-frontend` or `eform-shared`. The plugin repo contains
  only `src/app/plugins/`, with no `src/app/common`, so the help system lives entirely
  inside `time-planning-pn/` — which is also correct, since it then ships and versions
  with the plugin
- Any change to the 25 existing locale files
- The admin-only settings pages (pay rule sets, break policies) — separate pages,
  separate scope

## Implementation note: repository state

The host-app mirror at
`eform-angular-frontend/eform-client/src/app/plugins/modules/time-planning-pn/` is
**behind** this repository by 68 files, including the tag-chip click-to-filter feature,
the settings-strip status icons, and all 20 shared i18n files. Running
`devgetchanges.sh` would therefore copy stale host files over this repo and delete
those features.

All work for this design happens **directly in this repository**. The host mirror is
not edited, and `devgetchanges.sh` is not run.
