# Admin pause override (non-destructive) — design

**Date:** 2026-06-25
**Status:** design approved (approach), pending spec review → plan → implementation
**Branch:** `feat/pause-override` off `stable` (plugin); base changes in `eform-timeplanning-base`.

## Problem

After #1626 made pause timestamp sub-slots the source of truth (`ComputeShiftPauseSeconds` sums all slots; legacy `Pause{N}Id` is only a fallback when a shift has no timestamped slot), the admin web workday dialog can no longer change the effective total pause for a shift that has punch-clock sub-slots:

- On the common config (`UseDetailedPauseEditing=false`, `UseOneMinuteIntervals=false`) the admin's typed value is written to `Pause1Id` but then **ignored** — `ComputeTimeTrackingFields → ComputeShiftPauseSeconds` sees the surviving sub-slot timestamps (`hasTimestampedSlot=true`) and sums them.
- On `UseOneMinuteIntervals=true` sites the edit "works" only because `ApplyExactMinutePause()` **destroys** the sub-slots (clears them, writes one synthesized pause).

**Requirement (product owner):** the admin must be able to override the per-shift total pause, AND the worker's individual pause start/stop times must be **preserved** for documentation of what the worker actually did. So the destructive collapse (Approach A) is rejected; the one-minute path's existing clear-on-edit must also stop destroying the record.

## Approach C — override layer (preserve all start/stops)

Keep every recorded `Pause{N}StartedAt/StoppedAt` (and sub-slots) untouched. Store the admin's per-shift total as a separate **override** that the single pause-computation chokepoint honors.

### Data model (base package `eform-timeplanning-base`)

Add to entity `PlanRegistration` **and** `PlanRegistrationVersion` (both — the versioned/audit entity must match or the EF model-diff CI check fails, same lesson as the OverMidnight `AssignedSiteVersion` catch):

- `Pause1OverrideMinutes` … `Pause5OverrideMinutes` — `int?` (nullable). `null` = no override (compute from slots as today); non-null = authoritative total minutes for that shift. Nullable doubles as the "active" flag, so no separate bool column.

EF Core migration in the base package (no raw SQL). Bump base version, publish to NuGet. Canonical base repo: `/home/rene/laptop/Documents/workspace/microting/eform-timeplanning-base` (the `/Documents/` copy is stale — confirm at implementation time).

### Computation (plugin `PlanRegistrationHelper.cs`)

`ComputeShiftPauseSeconds(r, shift, useOneMinuteIntervals)`: **first** check `Pause{shift}OverrideMinutes`; if non-null, return `value * 60` (seconds). Otherwise the current all-slots sum / legacy fallback. This single chokepoint means netto (`ComputeNettoSecondsFromDateTimeShifts`, `ComputeTimeTrackingFields`), the display field (`AggregatePauseMinutes`), and the Excel export all honor the override automatically. The legacy `ComputePlanningNettoMinutes` flag-off path must also honor the override for `NettoHours` consistency.

### Save path (plugin `TimePlanningPlanningService.cs`)

- `Update` / `UpdateByCurrentUserNam`: when the admin sets a per-shift pause, write `Pause{N}OverrideMinutes` (from the exact minutes for one-minute sites; from `(Pause{N}Id-1)*5` for flag-off sites — sentinel-aware). **Do not** clear or synthesize sub-slot timestamps.
- The `UseOneMinuteIntervals` branch must **stop calling** the destructive `ApplyExactMinutePause()`/`ClearPauseTimestamps()` and set the override instead — so one-minute sites also retain the worker's recorded pauses.
- Clearing the field in the dialog (empty pause) sets the override back to `null` (revert to computed-from-slots). A worker re-syncing new pauses from the device is unaffected unless an override is active; define precedence as override-wins-while-set (admin intent is explicit).

### Web (`workday-entity-dialog.component.ts`)

- The per-shift pause edit writes `pause{N}OverrideMinutes` on the model (new DTO field) instead of relying on `pause1Id`. Raw sub-slot timestamps continue to round-trip untouched.
- Display already sums slots (post-#1626); when an override is present the served model carries it and the field shows the override value.

### Transport / DTO

Add `Pause{N}OverrideMinutes` to `TimePlanningPlanningPrDayModel` (read+write) and the Angular model. gRPC/mobile transport: out of scope unless the mobile app needs to read it (it doesn't edit admin overrides) — confirm at implementation; if the proto carries pause fields, add the override there too for parity, else skip.

## Out of scope (YAGNI)
- No per-slot editing UI (Approach B).
- No collapse/destructive reset (Approach A) — explicitly rejected by the documentation requirement.
- Shifts 3–5 get the columns for uniformity but the UI primarily exercises 1–2.

## Phasing (dependency order)
1. **Base package:** entities + migration + version bump + publish NuGet (gate: published before plugin can consume).
2. **Plugin C#:** bump base dep; `ComputeShiftPauseSeconds` + `ComputePlanningNettoMinutes` honor override; save path writes override and stops the destructive clear; DTO fields; `Integration.Test/SQL/420_*.sql` dump updated for the new columns; tests (override wins; slots preserved; revert-on-null).
3. **Web:** dialog writes `pause{N}OverrideMinutes`; Angular model field.
4. Each phase: dual gate (code-review + simplifier) → PR → CI green → merge. Land in order.

## Verification
- Unit: `ComputeShiftPauseSeconds` returns override when set, sums slots when null; netto/display/export reflect it.
- Integration: admin edits pause on a multi-pause row → effective total changes, `Pause{N}StartedAt/StoppedAt` rows unchanged in DB (documentation intact).
- Manual: edit pause for a punch-clock day; confirm the per-pause history is still present in the data.
