# HANDOFF — Reconciled ("Afstemt") day lock

**Parked 2026-09-13. This file is the entry point — read it before the spec or the plan.**

**State (updated 2026-09-15):** execution under way on `feat/reconciled-day-lock-backend`
(PR #1711). "THE OPEN DECISION" section below is settled: §8.1–8.4 are all in scope, with tasks in
the plan's Addendum B. Rulings taken during execution are summarised in spec §13.

**Original state (2026-09-13):** design and plan complete and reviewed. No implementation code written.

**Branch:** `docs/reconciled-day-lock-spec` — 3 commits, **never pushed, no PR**, working tree clean.

```
da5cd018  docs: implementation plan for the Reconciled day lock
145cd2cb  docs: settle bulk scope, payroll independence and mobile handling
8f2f9fdf  docs: design for the Reconciled ("Afstemt") day lock
```

**Artefacts**
- Spec: `docs/superpowers/specs/2026-09-12-reconciled-day-lock-design.md`
- Plan: `docs/superpowers/plans/2026-09-12-reconciled-day-lock.md` (15 tasks, 3 repos)
- Interactive mockups: https://claude.ai/code/artifact/30122702-dd2e-4826-b7f3-ee03c6ae218d

---

## To pick this up

**Announce the dev-mode gate first** (CLAUDE.md requires it), then:

1. Read this file, then the spec, then the plan. The plan's Global Constraints section
   carries every project-wide rule; each task is self-contained from there.
2. Settle the open decision below with the user.
3. Decide whether to push this docs branch and PR it, or fold the docs into the
   implementation branch. Note the dependency-alignment map (`3f33a08e`) rode along on the
   same branch and is unrelated work — split it out if this becomes a PR.
4. `stable` has moved since parking (`9e2ae84f`, `82348e71`, `1ef699be` — none touch the
   lock). Rebase or merge before starting.
5. Execute with **`superpowers:subagent-driven-development`** — one fresh subagent per
   task, review between tasks.

**Follow the normal development cycle for every task** (CLAUDE.md): branch off `stable`,
write or update tests, verify what can be verified locally (`dotnet build` only — see
below), run the **dual review gate** (`superpowers:requesting-code-review` AND a
`code-simplifier` subagent, dispatched in parallel) before committing, stage files by
name, PR into `stable`, then watch CI to a verdict.

**Natural stopping point:** after Task 6 the backend is complete and enforceable. Tasks
7-14 (service repo, frontend, styles, e2e) can land separately.

### What this feature does

Marking a worker's day **Afstemt** freezes that day and every earlier day for that worker
against web edits, mobile registrations and background recalculation. Earlier days lock
**without** being marked `Reconciled`. A day can only be unlocked once every day after it
is unlocked — like a Chinese ring puzzle.

### House rules that bite here

- **Dev mode: NONE — edit the source repos directly.** Do **not** run `devgetchanges.sh`;
  the host-app mirror is stale (Aug 14) and syncing from it would overwrite recent work.
- **Tests run ONLY in CI.** Never run `dotnet test`, `playwright test`, `jest` or
  `npm test` locally — a PreToolUse hook blocks them. `dotnet build` is allowed and
  expected. Push and watch `gh pr checks <n>`.
- **New C# test classes must be added to the shard filters in BOTH**
  `.github/workflows/dotnet-core-pr.yml` **and** `dotnet-core-master.yml`, or they
  silently never run.
- **Never commit to `stable`.** Branch, PR in.
- **SCSS lives in `eform-angular-frontend`**, never per-plugin — Task 10 is a separate
  repo and a separate PR. Expect three FOSSA checks to fail on that repo's PRs; they are
  non-gating.
- **User-facing copy must never explain a restriction by referring to what an
  administrator may do** ("admin = Microting"). State what the day *is*.

## THE OPEN DECISION — needed before Task 9

Spec §8.1–8.4 requirements with **no task in the plan**. Either write tasks for them or
move them to non-goals explicitly; do not let them lapse silently.

| Spec ref | Requirement | Why it matters |
|---|---|---|
| §8.1 | `lock` / `verified` glyphs, tooltips, the legend under the grid | Three of four visual channels survive without them, so "never colour alone" still holds — but nobody learns what the hatch means without the legend |
| §8.2 | Two-step inline confirm in the dialog footer; provenance line after | Reconciling is near-irreversible and any web user can do it |
| §8.3 | Preview of the affected region before commit; confirm naming worker count and landing date | The cascade is the part people get wrong in their heads |
| §8.4 | **Typed-word confirmation to unlock** | This is the *only* safeguard on a feature any web user can trigger. Dropping it is a real weakening, not a trim |

Recommendation: keep §8.4 at minimum.

---

## What the design settled (so it is not re-litigated)

- **No base-repo change.** `Reconciled` / `ReconciledAt` already exist on
  `PlanRegistrations` and `PlanRegistrationVersions` (migration `20260127060748`) and ship
  in the pinned `Microting.TimePlanningBase` **10.0.62**. Nothing writes them today.
- **The lock is derived, never stored:** `lockedThrough(site) = MAX(Date) WHERE Reconciled`.
  "Earlier days locked but not marked" and "unlock only in reverse order" fall out of the
  model rather than needing enforcement.
- **Scope:** per worker per day. Bulk = one date × a set of workers (selected rows, a
  selected column, or all visible).
- **Permissions:** any web user may reconcile. No admin gate.
- **I2 — nothing at or after today may be reconciled** — is load-bearing beyond the
  obvious: it is what makes the forward flex cascades (one 180 days ahead, one unbounded)
  provably unable to reach a locked day. **Do not relax it without revisiting them.**
- **No payroll link.** `Reconciled` and `TransferredToPayroll` are independent both ways.
- **Mobile** rejects the write; no mobile UI.
- Enforcement is a `SaveChangesInterceptor` (complete, unbypassable) + friendly guards on
  user-facing paths + silent skipping on recalculation paths. Guards alone would repeat the
  `MaxDaysEditable` mistake, which is enforced only on the read path and in the browser and
  is bypassable by a crafted POST today.

## Traps the plan review already caught — do not reintroduce

- **Never call `ServerVersion.AutoDetect` in the interceptor or in `GetDbContext()`.** It
  opens a connection and runs a version query; both run per-save / per-site on every
  dashboard load. The factory hardcodes `new MariaDbServerVersion(new Version(10, 5, 0))` —
  match it.
- **Do not filter the collection in `UpdatePlanRegistrationsInPeriod`.** Its single
  715-line loop fuses writing and projection; filtering it drops locked days out of the
  grid. Guard the four `Update` calls instead.
- **Set `siteModel.LockedThrough` before the loop**, or a worker with no rows in the
  window renders as fully editable.
- **`TestBaseSetup` builds its own contexts** — attach the interceptor there or every
  enforcement test passes through an unguarded context.
- **`PnBase.Create` overwrites `WorkflowState`**; `PnBase.Delete` is a *soft* delete
  arriving as `Modified`, never `Deleted`.
- **No Pomelo.** This repo uses the `Microting.EntityFrameworkCore.MySql` fork.
- The interceptor must **permit** clearing the flag on the boundary day, or unlocking is
  blocked by the lock it removes.

---

## Unrelated items parked alongside

- **Column M (Saturday hours)** in the Excel export still reads bare `NettoHours`, ignoring
  the override — the same bug fixed for column J in PR #1704. On `stable` now.
- **Overridden Grundlovsdag** ignores the override, because an override carries no
  information about which hours fell after noon. A payroll policy decision, documented at
  the call site.
- **`b/activate-plugin.spec.ts:30`** has a hardcoded `waitForTimeout(100000)` inside a 180s
  budget, in the setup path for all nine `1m` shards. Highest-leverage flakiness fix
  available — one change covers nine shards.
- **Flakiest Playwright shards** measured over 40 runs (recovering failures that re-runs had
  masked): `k` 5, `b` 3, `r` 3. `e1m` was 1.
- **The host-app mirror is stale** (Aug 14). `devgetchanges.sh` against it would overwrite
  recent work with old code.
