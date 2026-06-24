# Planning Timepicker 5-Minute Labels Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** On the web-admin planning page, when an AssignedSite has `UseOneMinuteIntervals=true`, the shift-time `ngx-material-timepicker` clock shows only the 5-minute labels (0,5,…,55) but still allows selecting any minute.

**Architecture:** Keep `minutesGap=1` (all 60 minutes stay selectable), tag the flag-on pickers with a marker class via the library's `timepickerClass` input, and hide the non-multiple-of-5 minute **labels** with a global CSS rule scoped to that class (`visibility:hidden`, so click targets remain).

**Tech Stack:** Angular (eform-client), ngx-material-timepicker v13.1.1, SCSS, Playwright (e2e).

**Reference spec:** `docs/superpowers/specs/2026-06-24-planning-timepicker-5min-labels-design.md`

**Project rules:** edit the plugin source repo only (not the host copy); no full local test runs — Angular build/lint locally for a smoke check, Playwright runs in CI; pre-commit dual-subagent gate (code-review + code-simplifier in parallel) before commit; PR toward `stable`; CI watch; the known-flaky `pn-playwright-test` shards may need one rerun.

**Repo:** `eform-angular-timeplanning-plugin/eform-client`. Branch already created: `feat/planning-timepicker-5min-labels` (off `stable`).

---

## File structure

- Modify: `eform-client/src/app/plugins/modules/time-planning-pn/components/plannings/time-planning-actions/workday-entity/workday-entity-dialog.component.html` — add `[timepickerClass]` to the 6 `<ngx-material-timepicker>` instances.
- Modify: the eform-client **global** stylesheet that holds app-wide timepicker/overlay overrides (locate in Task 1) — add the scoped hide rule. (Global because the picker overlay is appended to `<body>`.)
- Create/Modify: a Playwright spec under `eform-client/playwright/e2e/plugins/time-planning-pn/...` asserting the flag-on behavior (Task 3).

---

### Task 1: Add the scoped global CSS rule

**Files:**
- Modify: the eform-client global stylesheet (find it — see Step 1).

- [ ] **Step 1: Locate the global stylesheet**

Run (in `eform-client`):
```bash
grep -rn "ngx-material-timepicker\|\.timepicker\b\|clock-face" src/styles* src/**/*.scss 2>/dev/null | head
cat angular.json | grep -A3 '"styles"'
```
Pick the global stylesheet listed in `angular.json` `styles` (e.g. `src/styles.scss`) — or, if there's an existing global timepicker-overrides partial, use that. This rule MUST be global (the picker overlay is appended to `<body>`, outside component-encapsulated styles). Note the chosen file path.

- [ ] **Step 2: Add the rule**

Append to the chosen global stylesheet:
```scss
/* When the planning workday picker runs in one-minute mode (minutesGap=1, all 60
   minutes selectable), show only the 5-minute marks on the clock face. The marker
   class is set via [timepickerClass] only on the flag-on pickers, so other
   timepickers (and the minutesGap=5 pickers) are unaffected. visibility:hidden keeps
   each minute's click target + layout, so any minute is still selectable. */
.timepicker.timepicker--hide-non-multiples-of-5 .clock-face__number--outer:not(:nth-child(5n+1)) > span {
  visibility: hidden;
}
```

- [ ] **Step 3: Build smoke-check (compile only)**

Run (in `eform-client`):
```bash
npx ng lint --files src/styles.scss 2>/dev/null || true
```
(Stylelint may not be configured; the real verification is the e2e in Task 3 + CI. Ensure no SCSS syntax error — a build in Task 2 will catch it.)

### Task 2: Tag the flag-on pickers with the marker class

**Files:**
- Modify: `.../workday-entity/workday-entity-dialog.component.html`

- [ ] **Step 1: Find the 6 timepicker instances**

Run:
```bash
grep -n "ngx-material-timepicker\|minutesGap" src/app/plugins/modules/time-planning-pn/components/plannings/time-planning-actions/workday-entity/workday-entity-dialog.component.html
```
Expected: 6 `<ngx-material-timepicker ...>` with `[minutesGap]="useOneMinuteIntervals ? 1 : 5"` (planned + actual × start/stop/break).

- [ ] **Step 2: Add `[timepickerClass]` to every instance**

On EACH of the 6 `<ngx-material-timepicker>` opening tags, add (next to the existing `[minutesGap]` line):
```html
[timepickerClass]="useOneMinuteIntervals ? 'timepicker--hide-non-multiples-of-5' : ''"
```
Do NOT change `[minutesGap]` — it stays `useOneMinuteIntervals ? 1 : 5` (flag-on must remain `1` so all 60 minutes are selectable). Apply to all 6; missing one leaves that field cluttered.

- [ ] **Step 3: Build the client (compile check)**

Run (in `eform-client`):
```bash
npx ng build --configuration development 2>&1 | tail -20
```
Expected: build succeeds (AOT template compile validates the `timepickerClass` binding exists on the component). If `timepickerClass` is not a recognized input, STOP — re-verify the library version is 13.1.1 (`grep ngx-material-timepicker package.json`).

- [ ] **Step 4: Pre-commit dual-subagent gate**

Dispatch in parallel on the diff (template + global scss): `pr-review-toolkit:code-reviewer` (focus: rule scoped to the marker class only; `nth-child(5n+1)` maps to minutes 0,5,…,55 for gap=1; `visibility:hidden` not `display:none`; all 6 instances tagged; `minutesGap` unchanged; global stylesheet is the right place given body-appended overlay) and `code-simplifier:code-simplifier`. Resolve/justify findings.

- [ ] **Step 5: Commit**

```bash
cd /home/rene/Documents/workspace/microting/eform-angular-timeplanning-plugin
git add eform-client/src/app/plugins/modules/time-planning-pn/components/plannings/time-planning-actions/workday-entity/workday-entity-dialog.component.html <global-stylesheet-path>
git commit -m "feat(planning): show only 5-min labels on one-minute timepicker

Tag the UseOneMinuteIntervals pickers with timepickerClass and hide the
non-multiple-of-5 minute labels via a scoped global rule. minutesGap stays
1 so any minute is still selectable; only the labels are hidden.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

### Task 3: Playwright e2e — flag-on hides non-5 labels but any minute still selectable

**Files:**
- Create/Modify: a spec under `eform-client/playwright/e2e/plugins/time-planning-pn/` (reuse the one-minute (`*1m`) shard fixtures that already enable `UseOneMinuteIntervals` and use the position-based `pickTime()` helper).

- [ ] **Step 1: Locate the one-minute e2e shard + helpers**

Run:
```bash
ls eform-client/playwright/e2e/plugins/time-planning-pn/b1m/
sed -n '1,40p' eform-client/playwright/helpers/one-minute-times.ts
grep -rn "pickTime\|clock-face\|minutesGap\|UseOneMinuteIntervals" eform-client/playwright/helpers eform-client/playwright/e2e/plugins/time-planning-pn/b1m | head
```
Model the new assertions after the existing `b1m` dashboard-edit spec (it already opens the workday dialog with the flag on and picks off-grid minutes).

- [ ] **Step 2: Add the assertions**

In the flag-on spec (open the workday-entity dialog, open a minute clock), add checks:
```typescript
// Flag-on: only 5-minute labels are visible on the minute clock face.
const minuteLabels = page.locator('.timepicker.timepicker--hide-non-multiples-of-5 .clock-face__number--outer > span');
await expect(minuteLabels.nth(0)).toBeVisible();   // minute 0 (5n+1 #1)
await expect(minuteLabels.nth(5)).toBeVisible();   // minute 5 (#6)
await expect(minuteLabels.nth(1)).toBeHidden();    // minute 1 — label hidden
await expect(minuteLabels.nth(23)).toBeHidden();   // minute 23 — label hidden

// ...but selection of an off-grid minute still works (existing pickTime path):
await pickTime(page, '09:23');
// then assert the dialog/field shows 09:23 and it persists on save (reuse the
// existing spec's save+reload assertion pattern for the chosen field).
```
Keep the existing off-grid select/save assertions in the spec intact — they prove 1-minute selection is preserved.

- [ ] **Step 3: (Local) compile/typecheck the spec**

Run (in `eform-client`):
```bash
npx tsc -p playwright/tsconfig.json --noEmit 2>&1 | tail -20 || npx playwright test --list 2>&1 | grep -i "5min\|b1m" | head
```
Do NOT run the full Playwright suite locally (CI runs it). Just ensure the spec compiles / is discovered.

- [ ] **Step 4: Pre-commit dual-subagent gate** on the test diff (code-review + code-simplifier in parallel). Resolve findings.

- [ ] **Step 5: Commit**

```bash
git add eform-client/playwright/
git commit -m "test(planning): assert 5-min-only labels + any-minute selection on one-minute picker

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

### Task 4: Push → CI → merge

- [ ] **Step 1: Push + PR toward stable**

```bash
git push -u origin feat/planning-timepicker-5min-labels
gh pr create --base stable --title "feat(planning): show only 5-min labels on one-minute timepicker" --body "..."
```

- [ ] **Step 2: Watch CI**

```bash
gh pr checks <pr> --watch
```
The relevant gates: `build`, `angular-unit-test`, and the `pn-playwright-test` shards (the `*1m` ones exercise this). If only a known-flaky `pn-playwright-test` shard fails while build/angular/dotnet pass, rerun the failed job once (`gh run rerun <run-id> --failed`) and re-verify; if a `*1m` shard fails deterministically on THIS behavior, investigate (do not silence).

- [ ] **Step 3: Merge on green**

```bash
gh pr merge <pr> --squash --delete-branch
```

---

## Self-review (author checklist — completed)

**Spec coverage:** §3a (template `timepickerClass` on 6 pickers) → Task 2; §3b (global scoped CSS rule) → Task 1; behavior/§4 (any-minute still selectable) → `minutesGap` unchanged (Task 2 Step 2) + Task 3 off-grid select/save; §5 testing (labels hidden + off-grid selectable, playwright pickTime unaffected) → Task 3; §7 risk (nth-child ordering / v13.1.1, timepickerClass on overlay) → Task 2 Step 3 stop-condition + the rule comment. No gaps.

**Placeholder scan:** `<global-stylesheet-path>` and `<pr>` are values the executor fills from Task 1 Step 1 / the PR URL (each has an exact command to obtain it), not vague TODOs. PR `--body "..."` is a one-line description left to the executor. The Task 3 save assertion says to reuse the existing spec's save+reload pattern (concrete pattern, located in Step 1) rather than inventing a divergent one. No "handle edge cases"/"add validation" placeholders.

**Type/selector consistency:** marker class `timepicker--hide-non-multiples-of-5` is identical in Task 1 (CSS), Task 2 (binding), and Task 3 (locator); selector `.clock-face__number--outer`, `:nth-child(5n+1)`, `> span` consistent between Task 1 and Task 3; `[minutesGap]` left as `useOneMinuteIntervals ? 1 : 5` throughout.
