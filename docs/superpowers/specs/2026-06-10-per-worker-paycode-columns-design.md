# Design: Per-worker pay-code columns in the all-workers Excel export

**Date:** 2026-06-10
**Plugin:** eform-angular-timeplanning-plugin (`TimePlanning.Pn`)
**Dev mode:** Base dev mode — edit in the `eform-angular-frontend` host-app mirror, then
`devgetchanges.sh` back to the source repo for committing.

## Summary

In the **all-workers** Excel export, each per-site (per-worker) sheet currently shows pay-code
columns for the **global union** of pay codes across *all* workers — so whenever any worker has
an active pay-rule-set, every worker's sheet gets every pay code (filled `0` for codes that
aren't theirs). Fix this so each per-site sheet shows only the pay codes **declared in that
worker's own pay-rule-set**. A worker with no active pay-rule-set gets **no** pay-code columns.

The **Total** summary sheet is **completely unchanged** (it keeps the global-union columns,
regardless of which pay-rule-sets are active). The single-worker export is already correct and
is untouched.

## Background (current behaviour — verified)

In `GenerateExcelDashboard(TimePlanningWorkingHoursReportForAllWorkersRequestModel)`
(`Services/TimePlanningWorkingHoursService/TimePlanningWorkingHoursService.cs`):

- A pre-pass loop (~lines 2901–2971) builds a single `List<string> allPayCodes` = the union of
  pay codes that appeared in the **computed** pay lines across **all** sites. It also caches per
  site, in `AllWorkersSiteCache` (`PayRuleSet`, `PayLinesByDate`, …).
- `allPayCodes` is then used as the pay-code column set in **four** places:
  - **Total** sheet header (~3061–3065) and **Total** row fill (~3402–3406). *(Total sheet)*
  - **Per-site** sheet header (~3193–3197), per-day cell fill (~3267–3271), per-site totals row
    (~3346–3349). *(Per-site sheets — the bug)*
- `siteTotalsByPayCode` (seeded from `allPayCodes` at ~3252) is accumulated per site and consumed
  by **both** the per-site totals row and the Total-sheet row.

The single-worker `GenerateExcelDashboard(TimePlanningWorkingHoursRequestModel)` (~2390–2419)
already scopes its `allPayCodes` to the one site — it is the correct model and is not changed.

Pay codes are plain strings. A `PayRuleSet` declares its codes via:
`DayRules[].Tiers[].PayCode`, `DayTypeRules[].DefaultPayCode`,
`DayTypeRules[].TimeBandRules[].PayCode`, and `HolidayPaidOffPayCode`
(all in `eform-timeplanning-base/.../Entities/`). The all-workers pre-pass already
`.Include`s `DayRules.Tiers` and `DayTypeRules.TimeBandRules` when loading each site's
`PayRuleSet`, so the declared codes are available on the cached entity without extra queries.

## Decisions (from brainstorming)

| # | Decision |
|---|----------|
| 1 | Change **per-site sheets only**. The **Total sheet is completely unchanged** (keeps the global union, regardless of active pay-rule-sets). |
| 2 | Per-site columns = the pay codes **declared** in that worker's pay-rule-set (not just codes that had hours this period). |
| 3 | A worker with **no active pay-rule-set** → **no** pay-code columns on their sheet. |
| 4 | Column **order** = the rule-set's structural order: day-rule tiers → day-type default codes → time-band codes → holiday code; de-duplicated (first-seen wins); null/whitespace codes skipped. |
| 5 | The global `allPayCodes`, the single-worker export, and `siteTotalsByPayCode` (which feeds the Total sheet) are **not** modified. |

## Architecture

### New unit: `GetDeclaredPayCodes(PayRuleSet payRuleSet) : List<string>`

A pure, side-effect-free helper on `TimePlanningWorkingHoursService` (placed near
`CalculatePayLinesForDay`). Returns the ordered, de-duplicated, non-empty pay codes declared by
the rule-set, collected in this order:

1. For each `DayRule` in `payRuleSet.DayRules`: for each `Tier` in `DayRule.Tiers` → `Tier.PayCode`.
2. For each `DayTypeRule` in `payRuleSet.DayTypeRules`: `DayTypeRule.DefaultPayCode`, then for each
   `TimeBandRule` in `DayTypeRule.TimeBandRules` → `TimeBandRule.PayCode`.
3. `payRuleSet.HolidayPaidOffPayCode`.

Rules: skip `null`/whitespace codes; de-duplicate preserving first-seen order; return an **empty
list** when `payRuleSet` is `null`. Collection-navigation order follows the loaded entity
(the existing `.Include` order); within tiers, use `Tier.Order` if the existing code already
orders by it (match existing convention — verify in the plan).

> What it does: turns a pay-rule-set into the exact, stable set of pay-code column names for a
> worker. How it's used: called once per site in the per-site loop. Depends on: only the
> `PayRuleSet` entity graph already loaded into `AllWorkersSiteCache`.

### Per-site loop changes (the only behavioural change)

Inside the per-site loop of the all-workers export, after retrieving
`cache = perSiteCache[siteId]`, compute:

```
var sitePayCodes = GetDeclaredPayCodes(cache?.PayRuleSet);   // empty when no rule-set
```

Then replace `allPayCodes` with `sitePayCodes` at the three **per-site** emission points only:

1. **Per-site sheet header** (~3193–3197): append one header per `sitePayCodes` (was `allPayCodes`).
2. **Per-day cell fill** (~3267–3271): for each `payCode in sitePayCodes`, append the day's
   computed pay-line hours for that code (`dayPayLines.FirstOrDefault(pl => pl.PayCode == payCode)?.Hours ?? 0`) — same matching as today, over the per-site list.
3. **Per-site totals row** (~3346–3349): iterate `sitePayCodes`, using a **new per-site totals
   dictionary** seeded from `sitePayCodes` (accumulated across the site's days), instead of the
   shared `siteTotalsByPayCode`.

**Do not change** the global `allPayCodes` pre-pass, the `siteTotalsByPayCode` accumulation
(keep it as-is so the Total-sheet row stays correct), the Total-sheet header/row, or the
single-worker export.

> Note on the two totals dictionaries: `siteTotalsByPayCode` (keyed by `allPayCodes`) stays for
> the Total sheet. A separate per-site totals dict keyed by `sitePayCodes` drives the per-site
> totals row. This avoids a `KeyNotFound` when `sitePayCodes` contains a declared code that never
> appeared in any computed pay line (so it isn't in `allPayCodes`), and keeps the two sheets
> independent.

### Column-count consistency within a per-site sheet

The per-site sheet's header, every data row, and the totals row must all emit the same number of
pay-code cells (= `sitePayCodes.Count`). Since all three now iterate the same `sitePayCodes`,
they stay aligned. The sheet's `AutoFilter`/column-count references must reflect the per-site
column count (it already derives from the built header list — verify in the plan).

## Data flow

```
per-site loop (all-workers export):
  cache = perSiteCache[siteId]                  (has PayRuleSet, PayLinesByDate)
  sitePayCodes = GetDeclaredPayCodes(cache?.PayRuleSet)   // [] if no rule-set
  header     += sitePayCodes
  for each planning day:
     for code in sitePayCodes:
        hours = cache.PayLinesByDate[day].FirstOrDefault(pl => pl.PayCode == code)?.Hours ?? 0
        cell  += hours ; perSiteTotals[code] += hours
  totals row += perSiteTotals[code] for code in sitePayCodes

  (Total sheet: unchanged — still uses allPayCodes + siteTotalsByPayCode)
```

## Edge cases

- **No pay-rule-set**: `GetDeclaredPayCodes(null)` → empty → zero pay-code columns on that sheet.
  The Total sheet still shows the union columns with `0` in that worker's row (unchanged).
- **Declared code with 0 hours this period**: appears as a column, all cells `0` (the "declared"
  choice).
- **Duplicate codes across rules**: de-duplicated to one column.
- **Different rule-sets across workers**: each sheet gets its own (possibly different-width)
  pay-code column block — exactly the goal.

## Testing

- **Unit** (`PayRuleSet…`/service test, in an existing CI shard): `GetDeclaredPayCodes` —
  collects codes from all four sources, de-dups, preserves structural order, skips null/empty,
  and returns empty for `null`.
- **Export E2E** (`DagsoversigtWorksheetExportTests` or `WorkingHoursExcelExportE2ETests`): seed
  **two sites with different pay-rule-sets** (distinct declared codes) **plus one site with no
  rule-set**, run the all-workers export, and assert:
  - each per-site sheet's pay-code header = exactly that site's declared codes (and the
    no-rule-set sheet has none);
  - the Total sheet still contains the union of codes (unchanged behaviour);
  - cell values for a declared-but-zero code are `0`.

## Out of scope

- The Total summary sheet (unchanged by explicit decision).
- The single-worker export (already correct).
- Pay-line computation (`CalculatePayLinesForDay`), pay-rule-set CRUD, or payroll-file exports.
- The pre-existing data/encoding and temp-filename issues tracked separately.

## Files to change

Host-app mirror `eform-angular-frontend/eFormAPI/Plugins/TimePlanning.Pn/`:
- `TimePlanning.Pn/Services/TimePlanningWorkingHoursService/TimePlanningWorkingHoursService.cs`
  — add `GetDeclaredPayCodes`; wire `sitePayCodes` into the three per-site emission points + a
  per-site totals dict.
- `TimePlanning.Pn.Test/...` — new unit test for `GetDeclaredPayCodes`; extend an export test
  with the multi-rule-set scenario.
