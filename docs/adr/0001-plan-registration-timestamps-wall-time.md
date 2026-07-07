# ADR 0001: PlanRegistration exact timestamps store user-local wall time

Date: 2026-07-07
Status: Accepted

## Context

The `PlanRegistration` exact-time columns (`Start{1..5}StartedAt`,
`Stop{1..5}StoppedAt`, `Pause*StartedAt/StoppedAt`) were introduced for
second-precision time tracking on `AssignedSite.UseOneMinuteIntervals=true`
sites, alongside the legacy 5-minute interval ids (`Start1Id=79` ↔ 06:30).
No storage-timezone convention was ever written down, and a July 2026
production incident (a worker's 06:30 shift displayed as 04:30) forced the
question: do these columns hold UTC or local wall time?

A read-only classifier was run across all production tenants, comparing each
row's exact stamps against its own interval ids (which have always encoded
Danish wall time). Result:

- Customer rows are ~100% LOCAL WALL-TIME digits (2746 observations).
- True-UTC rows exist only in a small recent cluster; a version audit on
  tenant 986 found 390 local-wall rows vs 10 UTC rows, and zero UTC rows on
  tenants 1116/1124/1079.
- All UTC rows date from July 4–7 2026 and were written by ONE writer: the
  mobile app's edit screen ("register workday", used on
  `UseOneMinuteIntervals=1` + `UsePunchClock=0` sites), whose
  `.toUtc().toIso8601String()` path started shipping Z-suffixed strings in
  the June 25 Android release. The server's gRPC `ParseDateTime`
  (`AdjustToUniversal`) honored the Z and let UTC digits through to storage.
- Every other writer produces wall digits: the punch-clock flows stamp local
  wall time, the kiosk flow sends naive local digits, flag-off sites are
  id-driven, and `TimePlanningPlanningService.EnsureTimestampsFromIds`
  backfills the columns from interval ids — wall time by construction.

So the de-facto (and now codified) convention of historical data is wall
time; the UTC rows are a three-day-old regression from a single client path.

## Decision

**The PlanRegistration exact-time columns store USER-LOCAL WALL TIME with
`DateTimeKind.Unspecified` — the digits the worker saw on the clock —
consistent with the interval ids and `EnsureTimestampsFromIds`.**

Enforcement (server-side, `WallTimeNormalizer` in
`TimePlanning.Pn/Infrastructure/Helpers/WallTimeNormalizer.cs`):

- Incoming timestamp strings are parsed with `DateTimeStyles.RoundtripKind`.
- Naive digits are stored verbatim (already wall time).
- Z-suffixed or offset-carrying input is converted through its UTC instant
  into the current user's timezone
  (`IUserService.GetCurrentUserTimeZoneInfo()`, IANA id, default
  `Europe/Copenhagen`) and stored as `Kind=Unspecified` wall digits.
- Applied at every timestamp-string write seam:
  `TimePlanningPlanningsGrpcService.ParseDateTime` (the RPC the app edit
  screen calls: `UpdatePlanningByCurrentUser`, plus `UpdatePlanning` for
  symmetry) and both `TimePlanningWorkingHoursService.UpdateWorkingHour`
  overloads (create + update branches). The kiosk overload authenticates via
  a device token — no per-user zone exists, so it uses the documented default
  `Europe/Copenhagen`.
- The normalization stays in place even after the app-side cleanup ships, so
  no client can corrupt storage again.
- **No conversion on any read path.** Stored digits are the display truth.
- Flag-true read projections (`PlanRegistrationHelper.ReadBySiteAndDate`,
  `UpdatePlanRegistrationsInPeriod`) fall back to the id-derived wall time
  (`midnight + (id-1)*5 min`, ids > 0) when a stamp is null, so a
  false→true `UseOneMinuteIntervals` flip never blanks recorded times.

## Consequences

- Pay-band attribution, the Grundlovsdag after-noon rule, Excel exports and
  all `HH:mm` formatting are correct by construction — they already operate
  on wall digits.
- Supporting users in timezones other than the site's wall-clock zone (true
  per-user timezone rendering) would require a storage migration and is a
  SEPARATE future project; this ADR intentionally does not attempt it.
- DST: converting FROM an instant is deterministic — during the fall-back
  overlap (e.g. 2026-10-25 02:00–03:00 in Copenhagen) two distinct instants
  store the same wall digits; the stored value is ambiguous only in reverse.
  This limitation is shared with the 5-minute interval ids and accepted.
- The ~10 corrupted UTC rows on affected tenants (July 4–7 2026) are a data
  repair concern outside this ADR.
- Contract locked by tests: `WallTimeNormalizerTests`,
  `WallTimeGrpcWritePathTests`, `WallTimeWorkingHoursWritePathTests`,
  `WallTimeProjectionIdFallbackTests`, `WallTimeShiftFormattingTests`,
  `WallTimePayBandTests`.
