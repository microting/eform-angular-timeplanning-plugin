# Reconciled ("Afstemt") day lock — design

**Date:** 2026-09-12
**Status:** Accepted. Backend implemented (PR #1711); service repo, host styles and frontend follow. See §13 for the rulings taken during implementation.
**Scope:** `eform-angular-timeplanning-plugin` (web UI + `TimePlanning.Pn` API),
`eform-service-timeplanning-plugin` (background jobs). **No change to
`eform-timeplanning-base`.**

---

## 1. Problem

Payroll periods need to be closed. Once a period's hours are agreed, nothing
should be able to change them — not a web edit, not a mobile registration, and
not a background recalculation that quietly rewrites a flex balance three months
after the fact.

Today the plugin has no such mechanism. It has four partial approximations, none
of which closes a day:

| Mechanism | Scope | Enforced where |
|---|---|---|
| `MaxDaysEditable` (default 45) | tenant-wide | **read path + browser only** — a crafted POST bypasses it |
| `AllowEditOfRegistrations` / `DaysBackInTimeAllowedEditingEnabled` | per site | server-side, **mobile write path only**, boolean-only (the numeric day count is never compared) |
| `DayOfPayment` | tenant-wide | **commented out** in `PlanRegistrationHelper` (two places) |
| `TransferredToPayroll` | per registration | informational in the export preview; **blocks nothing** |

Two per-site date watermarks exist (`FlexChainComputedThrough`,
`UseOneMinuteIntervalsFrom`) but both gate *calculation semantics*, not editing.
Notably `UseOneMinuteIntervalsFrom`'s own doc comment already refers to
"already-closed periods" — a concept that has never existed in the schema.

## 2. Goals

1. A web user can mark a worker's day as **Afstemt** (reconciled), recording
   when it happened.
2. A reconciled day, **and every earlier day for that worker**, becomes
   immutable: no edit from web or mobile, no modification by any recalculation.
3. Earlier days are locked **without** being marked `Reconciled` themselves.
4. Unlocking is possible only in strict reverse order — to free a day, every
   day after it must be freed first.
5. The three resulting day states are legible in the grid without relying on
   colour alone.

## 3. Non-goals

- Fixing the `MaxDaysEditable` server-side enforcement hole. Noted as a known
  gap; it is the reason this design does not rely on call-site guards alone.
- Reconciling at any granularity other than per worker per day.
- A `ReconciledBy` column. Who performed the action is recoverable from
  `PlanRegistrationVersion.UpdatedByUserId`; adding a column would require a
  base-repo migration, which this design otherwise avoids entirely.
- Changing `TransferredToPayroll` semantics, or coupling reconcile to payroll
  export.

---

## 4. Data model — no schema change required

`Reconciled` and `ReconciledAt` **already exist** on both `PlanRegistrations`
and `PlanRegistrationVersions`, added by migration
`20260127060748_AddReconciledAndTransferredToPayrollToPlanRegistration`:

```csharp
public bool Reconciled { get; set; }          // tinyint(1) NOT NULL DEFAULT 0
public DateTime? ReconciledAt { get; set; }   // datetime(6) NULL
```

They are verified present in the currently pinned package
(`Microting.TimePlanningBase` **10.0.62**; implementation pinned 10.0.63, which still has them). Nothing writes them today; exactly
one place reads them — the admin-only version diff in
`TimePlanningPlanningService.CompareVersions`.

An unused translation key already ships in all 24 locale files:
`'Reconciled in accounting': 'Afstemt i regnskabet'`.

**Consequence: no base-repo change, no migration, no NuGet bump.**

### 4.1 The lock predicate — derived, never stored

```
lockedThrough(siteId) = MAX(Date)
                        WHERE SdkSitId = siteId
                          AND Reconciled = 1
                          AND WorkflowState != 'Removed'
                        -- null when the worker has no reconciled day

isLocked(siteId, date) ⟺ lockedThrough(siteId) != null
                          AND date <= lockedThrough(siteId)
```

One row carries the mark. Every earlier day is locked by derivation and keeps
`Reconciled = false`, which is requirement (3) satisfied by construction rather
than by a background job that must keep flags in sync.

Unlocking is therefore *only* meaningful at `lockedThrough` itself: clearing
that day's flag moves the boundary back to the next-newest reconciled day, or
to null. Requirement (4) is a property of the model, not a rule that has to be
separately enforced — a day below the boundary cannot be unlocked because
unlocking it would not change `MAX(Date)`.

### 4.2 Invariants

- **I1.** `ReconciledAt` is non-null exactly when `Reconciled` is true. Both are
  written in the same operation; unlock clears both.
- **I2.** `Reconciled` may only be set for `date < today`. Reconciling today or
  a future day is rejected **server-side**, not merely hidden in the UI.
- **I3.** No write of any kind may modify, create or delete a `PlanRegistration`
  whose `Date <= lockedThrough(SdkSitId)`.

**I2 is load-bearing beyond its obvious purpose.** See §6.2.

### 4.3 Query cost

`PlanRegistrations` has a unique index on `(SdkSitId, Date, WorkflowState)`.

- `isLocked` for a known `(site, date)` is an exact seek — optimal.
- `lockedThrough(site)` is a prefix scan on `SdkSitId` plus a range on `Date`;
  `Reconciled` is not in the index, so candidate rows are table lookups. For a
  single site over a viewed window this is negligible. The dashboard resolves
  it **once per site per request**, not per day, and passes the value down.

No new index is proposed. If profiling later shows it matters, the narrow fix
is a filtered index on `(SdkSitId, Reconciled, Date)` — but note this repo has
already reverted speculative indexes once (base commit `f086394`), so it should
be added only against a measured query.

---

## 5. Write-path inventory

There are **32 `PlanRegistration.Create/Update/Delete` call sites across 11
files in 2 repositories**, and **no choke point**. `PnBase.Create/Update/Delete`
live in the base NuGet package; most services call them directly rather than
through any plugin-owned helper.

Categories:

- **Web/admin:** `PlanningService.Update`, `UpdateByCurrentUserNam`, both
  `Index` gap-fills, `WorkingHoursService.CreateUpdate` (bulk, multi-day),
  `FlexService.UpdateCreate` (+ an estate-wide re-save loop),
  `TimeSettingService.UpdateAssignedSite`, `GoogleSheetHelper`, `Import`,
  `AbsenceRequestService`, `ContentHandoverService`, `PayrollExportService`.
- **Mobile/gRPC:** thin adapters delegating to the same services, plus a
  **kiosk** `UpdateWorkingHour` overload with *no date guard at all*.
- **Recalculation:** `UpdatePlanRegistrationsInPeriod` (4 write sites; runs on
  **every dashboard load and every mobile fetch**), `UpdatePlanRegistration`,
  and the forward flex cascades.
- **Background (service repo):** `SearchListJob` (8×/day sheet pull; nightly
  per-site walk that can **delete** rows), `FlexChainCatchUpJob` (off by
  default), `eFormCompletedHandler` (device submissions, can resurrect a
  `Removed` row).
- **Startup:** `CorruptedPauseIdRepair` — notable as the **only** path that
  already implements a date lock of its own.

Two behaviours matter more than the raw count:

**Reading writes.** `POST plannings/index` — the dashboard load — creates a
`PlanRegistration` for every missing date in the requested range, then
`UpdatePlanRegistrationsInPeriod` writes to all of them. Opening a report over
a closed month would, today, create and write rows inside it.

**Cascades write days nobody edited.** Forward flex cascades run from an edited
day through later days: one bounded at today, one **180 days into the future**
(`WorkingHoursService.CreateUpdate`), and one in the service repo with **no
upper bound at all** (`eFormCompletedHandler`).

**Every write goes through EF change tracking.** No production path uses
`ExecuteSqlRaw`, `ExecuteUpdate`, `ExecuteDelete` or a bulk insert against
`PlanRegistration`. (A grep finds two `ExecuteSqlRaw` hits in
`TimePlanning.Pn.Test/TestBaseSetup.cs`; both replay a SQL dump into the **SDK**
database during test setup and touch neither the plugin DB nor
`PlanRegistration`.) A `SaveChanges` interceptor can therefore see all 32 sites.

---

## 6. Enforcement

### 6.1 Three layers

**Layer 1 — `SaveChangesInterceptor` (the guarantee).**
Rejects any tracked `PlanRegistration` entry that violates **I3**: `Modified` or
`Deleted` with `Date <= lockedThrough`, or `Added` in that range. This is the
only layer that is *complete*: it covers all 32 sites and any site added later.

It must be registered **in both repositories** wherever a
`TimePlanningPnDbContext` is constructed. Registering it in only one leaves the
background jobs unguarded — a lock with a hole, which is worse than no lock
because it invites trust.

The interceptor resolves `lockedThrough` per distinct `SdkSitId` in the change
set, once per `SaveChanges`, not per row.

**Layer 2 — guards on user-facing write paths (the message).**
`PlanningService.Update`, `UpdateByCurrentUserNam`,
`WorkingHoursService.CreateUpdate`, and both `UpdateWorkingHour` overloads
check the predicate first and return a localized failure. Without this the user
sees a 500 from the interceptor instead of "this day is reconciled".

**Layer 3 — recalculation paths skip, they do not throw.**
`UpdatePlanRegistrationsInPeriod` legitimately spans the boundary on every
dashboard load. It filters locked days out of its working set rather than
failing. Same for `UpdatePlanRegistration` and the service-repo jobs.

Rationale for all three: guards alone repeat the `MaxDaysEditable` mistake
(bypassable); the interceptor alone turns an ordinary dashboard load into an
exception.

### 6.2 Why the cascades need no special handling

Given **I2** (nothing at or after today may be reconciled):

- every locked day satisfies `date <= lockedThrough < today`;
- every editable day satisfies `date > lockedThrough`;
- a forward cascade starts from an edited day and walks *forward*.

Therefore every day a cascade touches is `> lockedThrough` and unlocked. **The
cascades provably cannot reach a locked day.** Reading the boundary day to seed
the chain is unaffected — that is a read.

This is the single reason the 180-day and unbounded cascades do not need to be
re-plumbed. **If I2 is ever relaxed, they become live hazards immediately.**
Any future change permitting a future-dated reconcile must revisit §5's cascade
list first. Layer 1 would catch the violation, but as a 500 rather than a
designed behaviour.

### 6.3 Gap-fill inside a locked range

Gap-fill **stops at the boundary**: no row creation, no recomputation for
`date <= lockedThrough`. A locked period is frozen exactly as it stands.

Consequence, accepted deliberately: a day inside a locked range that never had
a registration gets no row. Nothing was registered that day, and not creating
one is what makes "reconciled" mean byte-stable rather than merely read-only.

**Revised during implementation (ruling F17):** the dashboard grid reads days
*by position* (`field = index`), so a missing day would shift every later day
one column left. The read model therefore carries a **non-persisted
placeholder day** (`Id = 0`) for each locked missing date, keeping one entry
per date. Nothing is written to the database; the cell renders empty.

### 6.4 Timezone

Two separate questions, with two different answers.

**The I2 predicate ("is this day still today?")** — `DayLockHelper.CanReconcile`
compares against **`DateTime.UtcNow.Date`**. `PlanRegistration.Date` is a
calendar-day *label* with the time zeroed, not an instant in any zone, so there
is no local midnight to be consistent with; pinning the comparison to one clock
beats following the writers, which do not agree among themselves. At the
positive offset this product runs at, `UtcNow.Date` *lags* the local date during
the first hours **after local midnight** — at local 00:30 on the 16th at +02:00
it is still the 15th in UTC, so reconciling the 15th is refused until the offset
elapses; later in the local day the two agree and the clock makes no difference.
That window is the conservative side, and for a rule that means "this day can no
longer be written", refusing too much beats allowing too much. (An earlier draft
of this section chose `DateTime.Now.Date` and described the window as a
late-evening one; the shipped code does neither — see the comment on
`CanReconcile`.)

**The `ReconciledAt` stamp** — the column **holds UTC**: `SetReconciledAsync`
writes `DateTime.UtcNow`. The `datetime(6)` column carries no offset, so EF
materialises it as `Kind.Unspecified`, and Newtonsoft (RoundtripKind) would then
emit naked digits that every browser reads as its *own* wall clock — the stamp
reading 1–2 hours early in Denmark, year-round. `PlanRegistrationHelper`
therefore re-tags the projected value `DateTimeKind.Utc`, so the JSON carries a
trailing `Z`, and the client passes **no** timezone argument to `DatePipe`: each
viewer sees the instant on their own clock. The dialog's optimistic stand-in
uses `toISOString()` for the same reason — same shape as the value that replaces
it, so the stamp does not jump when the grid reloads.

*Existing data needs no backfill:* the container was already UTC (no `TZ`, and
the aspnet base image defaults to it), so rows written before this change hold
UTC digits too and the new `Z` is retroactively true for them.

*Before ever setting `TZ` on the container, read this.* Going forward it is
harmless: the writer is `UtcNow` and no longer follows the host clock. The
hazard is historical and conditional — rows written **before** this PR were
stamped with `DateTime.Now`, so they hold local digits for whatever `TZ` the
host had **at the time of writing**. No known deployment ever set one, which is
why no backfill is needed. But nothing in the data marks which host wrote which
row, so a deployment that cannot rule out a past `TZ` cannot fix it after the
fact either: its whole back-catalogue would read an offset late under the new
`Z`, and the only remedy would be a dated cutoff.

---

## 7. API surface

Three operations on `TimePlanningPlanningController`:

| Verb | Route | Body | Returns |
|---|---|---|---|
| `PUT` | `plannings/{id}/reconcile` | — | `OperationResult` |
| `PUT` | `plannings/{id}/unreconcile` | — | `OperationResult` |
| `PUT` | `plannings/reconcile-through` | `{ date, siteIds[] }` | `OperationResult` with per-site outcome |

Rules enforced server-side:

- Reconcile rejects `date >= DateTime.UtcNow.Date` (**I2**).
- Reconcile is idempotent: re-reconciling an already-reconciled day is a no-op
  success, not an error.
- Unreconcile rejects any day that is not exactly `lockedThrough` for that
  worker, with a message naming the day that must be freed first.
- `reconcile-through` sets `Reconciled` on **one** day per listed worker: the
  latest day at or before the given date that has a registration. Everything
  earlier locks by derivation. If a worker has no registration on or before the
  date, that worker is skipped and reported.

The read model gains two fields on the existing per-day DTO:

```csharp
public bool Reconciled { get; set; }
public DateTime? ReconciledAt { get; set; }
```

and one per-row field, so the client can render the staircase without computing
it per cell:

```csharp
public DateTime? LockedThrough { get; set; }
```

`isLocked` is then a pure client-side comparison, consistent with how the grid
already derives cell classes.

---

## 8. UI

### 8.1 Three states

Because the mark is per worker per day, the boundary is a **staircase** down
the grid, not one vertical line. Each worker has their own reconciled-through
date.

Four independent channels, so colour is never load-bearing:

| State | Texture | Glyph | Cursor | Tooltip (da) |
|---|---|---|---|---|
| Open | none | — | `pointer` | — |
| Locked (cascade) | diagonal hatch | outline `lock`, bottom-left | `not-allowed` | `Låst · ligger før en afstemt dag` |
| Afstemt (boundary) | flat tint, 3px right border | filled `verified`, top-right | `default` | `Afstemt <dato> kl. <tid>` |

Constraints from the existing code:

- `getCellClass(row, field)` returns a **single** string today and has no
  composition mechanism. It must be extended to return multiple classes.
- Styles must be **theme-agnostic**: `body.theme-eform` rules do not apply under
  `theme-workspace`. Use `--tp-td-bg` / `--tp-border` / `--tp-text`, and define
  new `--tp-locked-*` tokens at `:root` with dark overrides.
- `outline` + yellow is already taken by `.highlight-cell`; blue by
  `.setting-ico.active`. The lock palette must avoid both.
- Day-cell icons use `fontSet="material-symbols-outlined"` + `class="neutral-icon"`.
  `lock` is already in use elsewhere (pay-rule-set banner) and is the right
  precedent.

A legend sits under the grid whenever any locked day is in range — otherwise
nobody learns what the hatch means.

### 8.2 Single day

A third action in the workday dialog footer, with a two-step inline confirm
(the footer morphs; no second modal). After saving, the dialog reopens
read-only with a provenance line where the actions were.

The dialog is chosen over a cell hover affordance deliberately: it is the only
place the user already sees *whose day, which date, which hours* before
committing, and the day cell is a dense click target where a misfire would
freeze a period.

### 8.3 Multi-day

Because one date plus the cascade already locks everything before it,
"multi-day" means **multi-worker**. Every bulk action is therefore the same
operation with two axes:

```
scope = target date  ×  set of workers
```

- **The date** comes from clicking a day-column header, or from the toolbar's
  date field. One date, never a range — the cascade supplies the range.
- **The worker set** comes from row selection; with nothing selected it
  defaults to **every worker currently visible** under the active filters.

That single mechanism covers all three requested modes without three separate
features:

| Requested mode | How it is expressed |
|---|---|
| Selected rows | tick rows, then pick a date |
| Selected column | click a day header; no row selection ⇒ all visible workers |
| All visible in the period | pick a date with nothing selected |

**The affected region is previewed before it is committed** — the cells that
will be locked are highlighted in place, so the cascade is visible rather than
inferred. The confirm step states the two counts that matter: how many workers,
and the date the boundary lands on.

**Rows already reconciled past the target date are skipped, not moved
backwards.** Applying an earlier date to a worker whose boundary is already
later would be an *unlock*, and unlocking is deliberately a separate, heavier
action (§8.4). Those rows are reported as skipped rather than silently ignored.

Worker selection uses mtx-grid's `[rowSelectable]` / `[multiSelectable]`, which
this plugin has never used. The host's backend-configuration task-list is the
precedent and documents two gotchas that apply verbatim:

1. mtx-grid binds `(click)="_selectRow()"` on the `<tr>`; with `[rowSelectable]`
   this **clears the batch selection** when a day cell is clicked. Use mtx-grid's
   `[disableRowClickSelection]` (it still emits `rowClick`), which also covers
   clicks on the Name column; a per-cell `stopRowClick($event)` would not.
2. mtx-grid rebuilds its internal `SelectionModel` empty in `ngOnChanges`
   **without emitting** `rowSelectedChange`, so the component must re-emit an
   empty selection itself.

Both are load-bearing: the planning grid's day cells are clickable, so (1) is
guaranteed to bite.

### 8.4 Unlock

Only the boundary day offers unlock; it moves the line back one notch. A day
below the boundary shows *which* day must be freed first rather than a disabled
control with no explanation.

Confirmation is a typed word, not a checkbox — deliberately asymmetric: sealing
takes a click, unsealing takes a word.

### 8.5 Blocked feedback

A locked day's dialog **opens read-only** rather than refusing to open. People
read closed days constantly. Every field renders at full opacity, disabled, with
a banner at the top. This reuses the existing `tp-help-hint tone="warn"` pattern
already used for `dayCell.futureDisabled`.

**Copy rule:** user-facing text states what the day *is*. It never explains a
restriction by referring to what an administrator may do. ("admin = Microting"
is a standing constraint in this product.)

### 8.6 Permissions

Supersedes the original decision recorded here (any web user may reconcile)
at the user's request — see §13; not a silent rewrite. **Only the first
user — the account with the lowest `AspNetUsers` Id — may reconcile, unlock,
or bulk-reconcile.** The server is the authority: no `[Authorize]` role can
express "the first user", so the rule is enforced as a service-layer check
in `TimePlanningPlanningService`, not a controller attribute. The UI hides
the three controls from every other user rather than disabling them with an
explanation. Lock **display** is unaffected by who may act on it and stays
visible to every user regardless: hatching, seals, glyphs, tooltips, the
legend, the read-only dialog, the provenance line, and the free-first line
on unlock. The reverse-order unlock rule is still the reason unlock carries
the heavier confirmation.

---

## 9. Testing

Tests in this repo run **only in CI**.

**C# (`TimePlanning.Pn.Test`):**
- `lockedThrough` with: no reconciled day, one, several, one soft-deleted.
- `isLocked` at the boundary, either side of it, and for a worker with none.
- **I2**: reconcile rejected for today and for a future date.
- Unreconcile rejected below the boundary, accepted at it, and the boundary
  moving back to the next-newest.
- Interceptor: `Modified`, `Added` and `Deleted` in a locked range each
  rejected; each allowed above the boundary.
- Gap-fill creates nothing inside a locked range.
- `UpdatePlanRegistrationsInPeriod` leaves locked rows byte-identical —
  asserted on `Version`/`UpdatedAt`, so a no-op re-save still fails the test.
- `reconcile-through` marks exactly one day per worker and skips workers with
  no registration.

New test classes must be added to the CI shard filters in **both**
`dotnet-core-pr.yml` and `dotnet-core-master.yml`, or they silently never run.

**Playwright:** reconcile a day → cell shows the boundary treatment → earlier
cell shows the locked treatment → opening a locked day gives a read-only dialog
→ unlock below the boundary is refused → unlock at the boundary moves it back.
Anchor rows by worker identity, not by grid index.

---

## 10. Risks

| Risk | Mitigation |
|---|---|
| Interceptor registered in only one repo → silent hole | Explicit test in each repo asserting a locked write is rejected through that repo's own context construction |
| **I2** relaxed later → cascades reach locked days | Stated as an invariant here and in code comments at the cascade sites; interceptor catches it as a 500 rather than corruption |
| Any user can freeze a period | Accepted by decision; reverse-order unlock + typed confirmation |
| Row selection breaks day-cell clicks | Known mtx-grid gotchas documented in §8.3, both with existing fixes in the host repo |
| `MaxDaysEditable` remains bypassable | Out of scope, explicitly; the interceptor makes the *new* lock not share the flaw |
| Timezone off-by-one near midnight | one clock (`DateTime.UtcNow.Date`) fixed in §6.4; tests must include a just-after-local-midnight case |

## 11. Resolved decisions

These were open during design and are now settled:

1. **Bulk scope** — all three: selected rows, a selected day column, or every
   worker visible in the chosen period. Expressed as one mechanism (date ×
   worker set) rather than three features; see §8.3.
2. **No link to payroll.** `Reconciled` and `TransferredToPayroll` stay
   independent. Reconciling does not affect export eligibility, and exporting
   does not reconcile. They are adjacent columns from the same migration and it
   would be easy to assume otherwise — they are not related.
3. **Mobile rejects the write and shows no lock state.** The gRPC paths return
   the same localized failure as the web paths. No mobile UI work is in scope;
   the app does not need to render the three states.

## 12. Open questions

None blocking. Two worth revisiting after the first release:

- Whether the skipped-rows report in §8.3 needs a persistent surface, or
  whether naming the count in the result toast is enough.
- Whether `ReconciledBy` is wanted on the face of the record. It is currently
  recoverable from `PlanRegistrationVersion.UpdatedByUserId` (§3), and adding
  it would require the base-repo migration this design otherwise avoids.
- **Open (from implementation):** §8.1 gives locked cells a `not-allowed`
  cursor, while §8.5 has them open a read-only dialog on click. Left as
  written; worth a look in the browser.

## 13. Revisions during implementation (2026-09-15)

Rulings taken while executing the plan. Each is recorded, with its cost if
wrong, in the SDD ledger. They supersede the sections they name.

- **§8.1–8.4 are all in scope** (user decision). Tasks for them were written
  and reviewed before the frontend phase.
- **I3 and payroll (F10).** The interceptor permits a *payroll-flag-only*
  write inside the lock (`TransferredToPayroll`/`TransferredToPayrollAt` plus
  bookkeeping columns). Without this, exporting a reconciled period fails
  midway: the file is built, some flags are set and the rest are not. This is
  what §11.2's independence requires. Hours are never writable.
- **I3 is checked on the original and the current slot.** Changing a locked
  row's `Date` or `SdkSitId` cannot move it out of the lock.
- **Unlock must clear both columns (I1 at the choke point).** Clearing
  `Reconciled` while leaving `ReconciledAt` set is rejected.
- **Recalculation reverts, it does not just skip saves (F15).** The dashboard
  recompute mutates *tracked* rows. Skipping only the save lets the next open
  day's save flush a locked row. Locked days are reverted before any later
  save, so the grid shows the stored, reconciled values.
- **Gap-fill placeholders (F17).** See §6.3.
- **Working-hours bulk save skips (F16).** That page posts every row in its
  range, so §6.1's "localized failure" would make any range touching a
  reconciled day unsaveable. The save skips locked rows, and the page shows
  them read-only through its existing `IsLocked` flag.
- **More write paths guarded (F11, F13 in the plugin; F12 in the service
  repo).** An audit found writes outside §5's list that reach locked days.
  In the plugin (PR #1711), each now skips locked days or answers with a
  message: startup pause-id repair (unguarded, it would have crashed host
  startup), Google Sheet pull, Excel import, the flex screen, and absence and
  handover requests (checked when created, approved or accepted). In the
  service repo (a separate PR), the sheet pull, the nightly recalculation and
  the flex catch-up skip locked days. Bulk re-syncs skip; a user acting on
  specific days gets a message.
- **Unlock refusals name the day to free first** (§7), and a worker with
  nothing reconciled gets a distinct message.
- **The lock's race window is documented, not closed.** The boundary query
  and the write are separate statements, so a reconcile committed between
  them can let one write through. Every guarded path still saves through the
  interceptor, which reads the boundary again.
- **Release order.** The frontend (PR4) must not reach production before the
  service-repo PR is deployed; otherwise background jobs could still write
  days the web shows as closed.
- **§8.6 permissions reversed to first-user-only, after the feature was
  complete and CI-green.** Not an admin role either: only the first user
  (lowest `AspNetUsers` Id) may reconcile, unlock, or bulk-reconcile,
  enforced as a service-layer check because no `[Authorize]` role can
  express it. The admin-role version (commit `07b598e6`) was superseded by a
  follow-up commit (`1eb5f77e`) rather than rewritten, since `07b598e6` was
  already pushed and CI-green. Release consequence: between PR1 and PR4
  merging, every user sees reconcile controls that refuse, so the two must
  merge as one sequence, not independently.
