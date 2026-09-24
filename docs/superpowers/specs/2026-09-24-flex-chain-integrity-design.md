# Flex chain integrity — stop new damage and carry every edit forward

**Date:** 2026-09-24
**Status:** Design approved in brainstorming; awaiting spec review
**Scope:** Sub-projects 1 + 2 of the 2026-09 flex drift programme (see §9)
**Repos:** `eform-timeplanning-base` (master), `eform-angular-timeplanning-plugin` (stable), `eform-service-timeplanning-plugin` (stable)

## 1. Background

A customer reported that historic flex balances changed after the 2026-08/09 seed-reset repair.
Investigation against production data (read-only) established:

- An independent replay of each worker's chain from their first registration, computing five-minute
  worked hours from Start/Stop/Pause ids (`(Σ(stop−start) − Σ(pause−1)) × 5 min`, matching 99.76% of
  pre-incident rows), reproduces the pre-incident balances within 0.05 h for every active worker,
  once the customer's own later edits and old chain breaks are accounted for.
- The damage came from a recompute that ran before the effective-date fix (PR #1696) was deployed: it
  re-derived historic `NettoHours` at second precision from device `Start*StartedAt`/`Stop*StoppedAt`
  stamps. Where an office user had long ago corrected the ids but the stamps were never updated, the
  hours changed by up to 8.9 h per day. Five-minute mode then reused the damaged `NettoHours` as-is,
  so nothing healed it.

Five bug classes were confirmed. This spec fixes the four that live in code:

| # | Bug | Evidence |
|---|---|---|
| B1 | Open shift (start, no stop — or stop < start) yields **negative** hours in the service five-minute path | `eFormCompletedHandler.cs:232-247`; 37 tenants carry such rows, largest −5,768 h |
| B2 | Office correction of start/stop on a one-minute row is silently replaced by hours re-derived from the old device stamps, in the same save | `TimePlanningWorkingHoursService.UpdatePlanning` :757-799 |
| B3 | An edit to day D does not carry the balance to later days on most write paths; stored chain breaks accumulate and never heal | 2,462 stored breaks across 88 tenants; 1,799 are "predecessor edited after successor was written"; 220 new in 2026-09 |
| B4 | Editing an old row stamps `RegisteredUnderOneMinuteIntervals` from the site's **current** flag, overriding `UseOneMinuteIntervalsFrom` | `TimePlanningWorkingHoursService.cs:792-795` |

Breaks are customer-visible: `ApplyRunningFlexChain` (web grid, mobile hero, Excel) recomputes only the
viewed window and anchors on the **stored** `SumFlexEnd` of the last row before it
(`TimePlanningWorkingHoursService.cs:998`).

## 2. Business rules

- **R1 — Open shift counts 0.** A shift with a start but no stop (stop id 0), or a stop before its
  start, contributes 0 worked hours, and its pause is ignored. Never negative, never guessed.
- **R2 — Hours are computed only when that day's own inputs change.** A forward walk of the balance
  never recomputes `NettoHours`/`NettoHoursInSeconds`; it only recomputes `Flex`, `SumFlexStart`,
  `SumFlexEnd` (and their `*InSeconds`) from the stored hours.
- **R3 — A day's mode is the mode at its own date**, resolved by `OneMinuteModeTimeline`, never the
  site's current flag.
- **R4 — Every write that changes a past day's inputs carries the balance forward to the worker's
  last existing row**, including rows pre-created for future dates.
- **R5 — Reconciled (locked) days are never written.** The walk passes them and continues from their
  stored balance.

## 3. Hard constraint: existing tests

Every existing test in the three repos must pass **unchanged**, except tests about open-shift hours
(none exist today). If an existing test pins a behaviour this design changes, implementation stops and
the conflict is raised with the owner — the test is not edited.

A pre-implementation scan of all three test projects found no conflicts. It produced these
implementation constraints, which are part of this design:

1. The walk does not load-and-write reconciled/locked rows and seeds from the stored boundary
   (`ReconcileServiceTests` L688, L739, L785).
2. Flex uses `NettoHoursOverride` when `NettoHoursOverrideActive`; decimal and seconds columns stay in
   step (`MobileFlexRecomputeAndCascadeTests` L288, L293).
3. The edited row itself still has its hours recomputed by its existing path (`MobileFlex…` D1).
4. The walk tolerates a missing `AssignedSite` (absence, handover and push tests seed none).
5. Batched writes still increment `Version` (`ReconcileServiceTests` L819).
6. `FlexChainCatchUpJob` and `FlexChain.ApplyNettoFlexChainSecondPrecision` are **not** changed
   (`FlexChainCatchUpJobTests` L290-295, L348-354; `PlanRegistrationHelperTests` L569…1656;
   `OneMinuteIntervalsEffectiveDateTests` L369-381). Stamp clearing (B2) is confined to the grid
   `UpdatePlanning` path; the single-day editor and kiosk write exact stamps and tests pin that
   (`PlanningServiceAdminEditNonRoundMinutesTests` L122-125, `PlanningServiceMultiShiftTests`
   L321-323, `WorkingHoursGrpcKioskNonRoundMinutesTests` ~L198).

## 4. Base package (`eform-timeplanning-base`, `Microting.TimePlanningBase.Infrastructure.Helpers`)

Purely additive; released as **10.0.65**. Nothing changes behaviour until callers switch.

### 4.1 `FlexChain.ComputeNettoMinutesFlagOff(PlanRegistration pr) → double`

The single five-minute hours computation. A straight move of the plugin's already-correct
`ComputeFlagOffNettoMinutes` (`TimePlanningWorkingHoursService.cs:1366-1403`): for shifts 1..5,
a shift counts only when `StopNId != 0 && StopNId >= StartNId`, contributing
`(StopNId − StartNId) × 5 − ComputeShiftPauseSeconds(pr, N, useOneMinuteIntervals: false) / 60`.
Returns minutes. Implements R1; its one-minute twin `ComputeNettoSecondsFromDateTimeShifts` already
does.

### 4.2 `FlexChain.CarryChain(PlanRegistration pr, PlanRegistration? predecessor, bool rowIsOneMinute, bool? predecessorIsOneMinute)`

Balance-only step implementing R2. Reads only stored hours; never stamps or ids.

- Effective hours: `NettoHoursOverride` when `NettoHoursOverrideActive`, else stored hours.
- **One-minute row:** seed = `SumFlexEndSecondsWithFallback(predecessor, predecessorIsOneMinute)`;
  netto seconds = override seconds / `NettoHoursInSeconds`, falling back to `NettoHours × 3600` when
  the seconds column is 0 and the decimal is not; plan and paid-out likewise via the existing
  seconds-or-decimal fallbacks. Writes `FlexInSeconds`, `SumFlexStartInSeconds`,
  `SumFlexEndInSeconds` and back-derives the decimals (`/3600.0`), exactly as
  `ApplyNettoFlexChainSecondPrecision` does after its netto step.
- **Five-minute row:** identical arithmetic to `ApplyNettoFlexChainDecimal` (which already uses stored
  hours as-is) including `ClearSumFlexSeconds`.

### 4.3 `FlexChainRecompute.RunForwardAsync(TimePlanningPnDbContext db, AssignedSite? site, PlanRegistration savedRow) → Task<int>`

Implements R3–R5.

- Seed = `savedRow` (the caller has just saved it). Walks every non-removed row for
  `savedRow.SdkSitId` with `Date > savedRow.Date`, ascending by `Date` then `Id`, **no upper bound**
  (includes pre-created future rows). Predecessor carried in memory, never re-queried.
- Mode per row: `OneMinuteModeTimeline` built once (`BuildAsync(db, site)`; a null site yields the
  legacy all-five-minute timeline), `WasOneMinuteForRow(row)`.
- Locked days: rule `Date <= MAX(Date WHERE Reconciled)` for the worker. Locked rows are not
  modified; the next unlocked row seeds from the locked row's stored values. To keep one definition,
  the plugin's `DayLockHelper.LockedThroughAsync(db, sdkSitId)` and `IsLocked(DateTime?, DateTime)`
  (`Infrastructure/Helpers/DayLockHelper.cs:36, 72`; base-typed only) move to a new base class
  `DayLock`; the plugin methods keep their signatures and forward to it, so callers and tests are
  unaffected. The plugin's `ReconciledDayLockInterceptor` stays as the write-side backstop; the walk
  never presents it a locked write.
- Writes only rows whose chain values actually changed (tolerance: exact compare on seconds columns,
  1e-9 on decimals). For each changed row: `Version++`, `UpdatedAt = UtcNow`, plus one
  `PlanRegistrationVersions` row mirroring `PnBase`'s version mapping. All changes saved in **two**
  `SaveChangesAsync` calls (rows, then version rows) instead of two per row.
- Does not touch `AssignedSite.FlexChainComputedThrough`.
- Returns the number of rows changed.

### 4.4 Base tests

- `FlexChainUTest` (pure): R1 cases (start-no-stop, stop<start, pause ignored on open shift, multi-shift
  with one open); `CarryChain` (override, seconds/decimal in step, zero-seconds fallback, mode
  boundary seed).
- New `DbTestFixture` class for `RunForwardAsync`: mid-history edit reaches the last future row;
  later rows' hours unchanged including one-minute rows whose stamps disagree with ids (the incident
  scenario); reconciled row untouched and the walk continues from it; five→one-minute boundary inside
  the walk; already-consistent chain → 0 writes, `Version` unchanged; changed rows get `Version + 1`
  and exactly one version row each; null site.

## 5. Stopping new damage (plugin + service)

### 5.1 B1 — service `eFormCompletedHandler.cs:232-247`
Replace the inline sum with `timePlanning.NettoHours = FlexChain.ComputeNettoMinutesFlagOff(timePlanning) / 60.0;`.
The plugin's `ComputeFlagOffNettoMinutes` and the five-minute branch of
`TimePlanningPlanningService.ComputePlanningNettoMinutes` (:1811) become calls to the base function
(already correct — no behaviour change).

### 5.2 B4 — plugin `TimePlanningWorkingHoursService.UpdatePlanning` :792-795
`RegisteredUnderOneMinuteIntervals = timeline.WasOneMinuteAt(planRegistration.Date)` (the timeline is
already a parameter) instead of `assignedSite.UseOneMinuteIntervals`.

### 5.3 B2 — same method, evaluated **before** the id assignments at :757-762
Using B4's answer as `rowIsOneMinute`: when true, for shifts 1–2 (the ones this model carries), if the
posted start or stop id differs from the stored id, set that shift's `StartNStartedAt`/`StopNStoppedAt`
to null; if the posted pause id differs, set `PauseNStartedAt`/`PauseNStoppedAt` to null. Hours then
come from the office ids through `ComputeNettoSecondsFromDateTimeShifts`' existing id fallback.
Confined to this path (constraint 6).

## 6. Carrying every edit forward (R4)

Each path calls `RunForwardAsync` once, after its own save, from the **earliest** day it changed:

| Path | Location | Change |
|---|---|---|
| Single-day office edit (two copies) | `TimePlanningPlanningService.cs:1025-1060`, `:1383-1409` | Replace both hand-rolled loops (today-capped) |
| Grid save | `TimePlanningWorkingHoursService.cs:595-644` | Remove unordered tail loop; walk from earliest posted changed date |
| Flex tab paid-out | `TimePlanningFlexService.cs:276-333` | After the row delta, walk |
| Absence approval | `AbsenceRequestService` | Walk from earliest affected day |
| Content handover | `ContentHandoverService.cs:747, 786` | Walk from the earlier of source/target date |
| Google Sheet pull | plugin `GoogleSheetHelper`, service `SearchListJob` | One walk per worker from the earliest pulled date |
| Device eForm | service `eFormCompletedHandler.cs:270-300` | Replace the hand-rolled walk (which recomputes later one-minute rows' hours from stamps) |

Unchanged: read path `UpdatePlanRegistrationsInPeriod`; `FlexChainCatchUpJob` (stays disabled — it
must not be enabled until moved onto `RunForwardAsync`, a separate decision); Google Sheet inverted
sign (sub-project 5).

## 7. Delivery

Each PR follows the full cycle: branch off target, tests, `dotnet build`, dual review
(code-review + code-simplifier) before commit, PR, CI watched to a verdict.

1. **base** — §4 (incl. `DayLock` move) + tests → merge to master → release 10.0.65.
2. **plugin** — bump base; §5.2, §5.3, §5.1 plugin part; §6 office edit + grid save.
3. **plugin** — §6 Flex tab, absence, handover, Google Sheet.
4. **service** — bump base; §5.1; §6 device eForm + `SearchListJob`.
5. Deploy plugin and service.

New plugin test classes are added to exactly one shard in **both** `dotnet-core-pr.yml` and
`dotnet-core-master.yml` (enforced by `ShardCoverageTests`). Service and base run their whole test
project; no allowlist.

### New tests per PR
- **Plugin:** B2 (office correction on a one-minute row with stale stamps keeps office hours); B4
  (old row's marker follows its own date); per write path — edit day D, assert
  `SumFlexStart(n+1) == SumFlexEnd(n)` through the last pre-created row.
- **Service:** first `eFormCompletedHandler` tests — open shift → 0 h; walk carries forward; later
  rows' hours unchanged.

## 8. Verification after deploy

- Re-run the read-only stored-break scan across tenants: edits made after deploy create no new breaks
  (existing breaks remain until sub-projects 3–4).
- Re-run the replay on the affected tenant as the baseline for sub-project 3.

## 9. Programme context (not in this spec)

- **3 — Repair the affected tenant:** restore pre-incident hours on ~1,120 recomputed rows, set its
  open-shift day to 0, walk forward with `RunForwardAsync`, verify each worker against the replay.
  Starts only after this spec is deployed.
- **4 — Repair other tenants:** open-shift rows (~37 tenants), 2 recomputed rows on one other tenant,
  and all stored breaks; customer communication where balances move.
- **5 — Leftovers:** a 35-minute shift stored as 32.5 h, a 24 h row (midnight crossing), Google Sheet
  inverted sign.

## 10. Out of scope / non-goals

- Changing `FlexChainCatchUpJob` or enabling it.
- Changing how the read path or `ApplyRunningFlexChain` compute displayed balances.
- Any data repair (sub-projects 3–4).
