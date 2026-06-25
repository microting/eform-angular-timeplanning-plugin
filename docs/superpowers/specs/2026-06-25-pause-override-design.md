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

EF Core migration in the base package (no raw SQL). Bump base version, publish to NuGet. Canonical base repo: `/home/rene/Documents/workspace/microting/eform-timeplanning-base` (confirmed by owner; the `/laptop/` copy referenced in the 2026-06-19 handoff is NOT the one to use).

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

Add `Pause{N}OverrideMinutes` to `TimePlanningPlanningPrDayModel` (read+write) and the Angular model.

**Mobile / gRPC — IN SCOPE (owner requirement).** The flutter app needs the override for (a) presenting corrected pause history to all workers and (b) the back-in-time editors who can amend past registrations. So:
- Add `pause{N}_override_minutes` to the relevant gRPC message(s) in `proto/` (the PlanningPrDay/working-hours message the app reads, and the update message the app writes when editing back in time). Regenerate C# + Dart proto.
- gRPC service mapping: populate the override on read; persist it on write (same non-destructive rule — writing the override never clears sub-slots).
- Honor the gRPC-only-transport invariant: touch only the gRPC path, not the old JSON/REST oracle.

### Mobile app (flutter-time) — IN SCOPE

**Unifying principle:** the override is the canonical "manually entered / edited pause total" for a shift. EVERY manual edit surface writes it; punch-clock sub-slots are preserved as documentation; reads are override-wins-else-sum everywhere.

- **Read (all users):** history/day views display the per-shift pause using the override when present (else the computed sum from #531). One source-of-truth helper mirroring `ComputeShiftPauseSeconds`: override-wins-else-sum.
- **Write — manual time-edit (non-punch-clock) flow:** this is the primary mobile write path and MUST keep working. When a worker/editor on a non-punch-clock site enters/edits a shift's pause manually, write `pause{N}_override_minutes` via the gRPC update (non-destructive). On non-punch-clock days there are typically no sub-slots, so the override simply *is* the pause; previously this relied on the legacy `Pause{N}Id`, which post-#1626/#531 is no longer authoritative when any timestamp exists — the override fixes that uniformly.
- **Write — back-in-time editors:** the same override write applies to the role-gated flow that amends past registrations. Clearing the field reverts to null (compute-from-slots).
- Verify against the shift edit surfaces (the manual edit widget(s) / the 25-clone shift-confirm pages) so manual pause entry routes to the override, not to a now-non-authoritative `Pause{N}Id` or a destructive slot rewrite.

## Out of scope (YAGNI)
- No per-slot editing UI (Approach B).
- No collapse/destructive reset (Approach A) — explicitly rejected by the documentation requirement.
- Shifts 3–5 get the columns for uniformity but the UI primarily exercises 1–2.

## Phasing (dependency order)
1. **Base package** (`/Documents/...eform-timeplanning-base`): entities (`PlanRegistration` + `PlanRegistrationVersion`) + EF migration + version bump + publish NuGet (gate: published before plugin can consume).
2. **Plugin C#:** bump base dep; `ComputeShiftPauseSeconds` + `ComputePlanningNettoMinutes` honor override; save path writes override and stops the destructive clear; REST DTO fields; **gRPC proto + service mapping (read+write)**; `Integration.Test/SQL/420_*.sql` dump updated for the new columns; tests (override wins; slots preserved; revert-on-null).
3. **Web:** dialog writes `pause{N}OverrideMinutes`; Angular model field.
4. **Mobile (flutter-time):** regen Dart proto; read override for history display (override-wins-else-sum helper); write override in the back-in-time edit flow (role-gated, non-destructive).
5. Each phase: dual gate (code-review + simplifier) → PR → CI green → merge. Land in order; protocol/contract (base, then proto in plugin) before consumers (web, mobile).

## Verification
- Unit: `ComputeShiftPauseSeconds` returns override when set, sums slots when null; netto/display/export reflect it.
- Integration: admin edits pause on a multi-pause row → effective total changes, `Pause{N}StartedAt/StoppedAt` rows unchanged in DB (documentation intact).
- Manual: edit pause for a punch-clock day; confirm the per-pause history is still present in the data.
