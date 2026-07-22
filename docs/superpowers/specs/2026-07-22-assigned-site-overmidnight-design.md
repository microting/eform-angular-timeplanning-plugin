# Assigned-Site Modal: OverMidnight Toggle — Design

**Date:** 2026-07-22
**Repos:** `/home/rene/Documents/workspace/microting/eform-angular-timeplanning-plugin` (all changes; base repo untouched)

## Background

`AssignedSites.OverMidnight` (bool, default false) has existed since migration
`20260619120000_AddOverMidnightToAssignedSite` in
`/home/rene/Documents/workspace/microting/eform-timeplanning-base`. The read side is
fully wired: entity → REST DTO (`Infrastructure/Models/Settings/AssignedSite.cs:59`)
→ gRPC contract to the mobile app. The mobile app consumes the flag and implements
the behavior: when enabled, a punch-clock shift that spans midnight is automatically
closed at 00:00 and continued as a new registration on the next day
(day 1: 22:00–00:00, day 2: 00:00–06:00). No web-backend logic consumes the flag.

Two gaps remain, which this feature closes:

1. **No UI** — the flag is not present in the assigned-site modal, the Angular
   `AssignedSiteModel`, or the dialog form.
2. **Write path drops it** — `TimeSettingService.UpdateAssignedSite`
   (`.../Services/TimePlanningSettingService/TimeSettingService.cs:961–1114`) copies
   every other field explicitly but never assigns `OverMidnight`, so a PUT carrying
   the field is silently ignored.

No functionality change in the app or web backend beyond exposing and persisting
the flag.

## UI

- The checkbox renders **only when the punch-clock entry method is selected**
  (Approach B — the flag has no effect for manual or accept-planned entry), inside
  the existing punch-clock conditional block in
  `assigned-site-dialog.component.html`, directly under the existing sub-option
  `usePunchClockWithAllowRegisteringInHistory`, with identical markup conventions:
  `mat-checkbox` + `<small class="checkbox-description">` help line. No new SCSS.
- Label (en): **"Shifts across midnight"** — da: **"Vagter over midnat"**.
- Help text (en): *"The shift is automatically closed at 00:00 and continues as a
  new registration on the next day (e.g. day 1: 22:00–00:00, day 2: 00:00–06:00).
  The app handles this automatically."* — da: *"Vagten lukkes automatisk kl. 00:00
  og fortsætter som en ny registrering næste dag (fx dag 1: 22:00–00:00, dag 2:
  00:00–06:00). Appen håndterer dette automatisk."* — plus the remaining 24 locales.
- Two new i18n keys (`'Shifts across midnight'`, and the help-text sentence as its
  own key) added to all 26 locale files, duplicate-checked in both `'Key':` and
  `Key:` forms per file.
- Switching the entry method away from punch clock hides the checkbox but retains
  its value (same behavior as the existing punch-clock sub-option). Nothing resets
  silently.

## Data flow

- `overMidnight: boolean` added to the TS `AssignedSiteModel`
  (`models/assigned-sites/assigned-site.model.ts`) and bound in the dialog
  component. GET already delivers the stored value (DTO carries it), so the modal
  opens correctly populated.
- Backend: one assignment added to `UpdateAssignedSite` —
  `dbAssignedSite.OverMidnight = site.OverMidnight;` — alongside the other boolean
  copies. Versioning is automatic (`PnBase.MapVersion` copies by reflection).
- No EF migration, no gRPC change, no controller change, no new endpoint.

## Tests

- **Backend** (runs in CI only — never locally): a new test in the existing
  settings-service test suite (`TimePlanning.Pn.Test`, SettingsService* pattern)
  asserting a round-trip: `UpdateAssignedSite` with `OverMidnight = true` persists
  `true` on the entity, and a follow-up update with `false` persists `false`.
  Existing tests untouched.
- **Frontend**: extend `assigned-site-dialog.component.spec.ts` — the checkbox is
  present when the entry method is punch clock, absent otherwise, and reflects /
  writes the model value. Existing tests untouched.
- No new Playwright e2e lane — this is a plain form field; the C# build must pass
  locally before push, tests run only in CI.

## Ship flow

Edit plugin repo → mirror changed files to the host app (targeted `cp`, never
`devinstall.sh`) → live verification in the browser against the real DB (checkbox
visibility follows entry method; toggle → save → reopen → persisted; DB column
updated) → dual review gate (code-reviewer + code-simplifier in parallel) →
`feat/assigned-site-over-midnight` branch → PR to `stable` → CI watch → merge only
on green.
