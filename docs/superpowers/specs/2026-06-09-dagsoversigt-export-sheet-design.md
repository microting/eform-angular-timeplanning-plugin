# Design: "Dagsoversigt" day-overview export sheet

**Date:** 2026-06-09
**Plugin:** eform-angular-timeplanning-plugin (`TimePlanning.Pn`)
**Dev mode:** Base dev mode — edit in the `eform-angular-frontend` host-app mirror, then `devgetchanges.sh` back to the source repo for committing. No base repo involved.

## Summary

Add a new **Dagsoversigt** ("Day overview") sheet as the **first tab** in both existing
Excel exports of the timeplanning plugin:

- **All-workers** export (`GET reports/file-all-workers`) — one **combined** Dagsoversigt
  sheet containing every worker's planning days, followed by the existing Total +
  per-site sheets.
- **Single-worker** export (`GET reports/file`) — the same sheet for just that one
  worker, followed by the existing Dashboard sheet.

The sheet replicates the layout and formatting of the reference file
`/home/rene/Downloads/Dagsoversigt.xlsx`.

## Motivation

The current exports present data per-worker (Dashboard) or as a per-site breakdown
(Total + per-site). There is no single flat day-by-day overview that lists each
worker's shifts per day in one scannable table. The Dagsoversigt sheet fills that gap
and is requested as the first thing the reader sees when opening either workbook.

## Reference file analysis

`Dagsoversigt.xlsx` is a single banded Excel Table (`A1:U11`), one row per
*(employee × day)*:

| Col | Header (da) | Resx key | Notes |
|-----|-------------|----------|-------|
| A | Medarbejder nr. | `Employee no` | worker employee number |
| B | Medarbejder | `Worker` | worker / site name |
| C | Ugedag | `DayOfWeek` | localized weekday, lowercase (`torsdag`) |
| D | Dato | `Date` | real date, `dd/mm/yyyy` |
| E | Uge nr | `Week number` | ISO-ish week number |
| F–H | Skift 1: start / stop / pause | `Shift 1: start` / `Shift 1: end` / `Shift 1: pause` | real `h:mm` time values |
| I–K | Skift 2: … | `Shift 2: …` | |
| L–N | Skift 3: … | `Shift 3: …` | |
| O–Q | Skift 4: … | `Shift 4: …` | |
| R–T | Skift 5: … | `Shift 5: …` | |
| U | Timer netto | `NettoHours` | net hours, `0.00` |

Notes from the reference:
- All 5 shift blocks are always present (fixed 21-column layout), even when only
  shift 1 has data.
- Banded Excel Table with autofilter and a bold header row.
- The reference's `Timer netto` cells hold placeholder text; this design instead
  fills the column with the export's real computed net-hours value.

## Decisions (settled during brainstorming)

1. **Both exports** get the sheet, as the **first tab**.
2. **All-workers** → **one combined sheet** (not per-site), rows interleaved across all
   assigned, non-resigned, non-removed sites.
3. **Sort order**: by **date ascending, then employee number ascending** (matches the
   reference grouping: all employees for a day, then the next day).
4. **Shift columns**: **always all 5** (fixed 21 columns), regardless of each site's
   `Third/Fourth/FifthShiftActive` flags — unlike the existing Dashboard/Total sheets
   which hide inactive shift columns.
5. **Row scope**: one row per **planning day** per worker — the same day-set the
   existing `Index(...)` already returns (no new query logic; reuse it).
6. **Timer netto**: reuse the existing override-aware computed value
   (`NettoHoursOverrideActive ? NettoHoursOverride : NettoHours`). Days without a
   registration simply show `0.00`.
7. **Formatting fidelity**: **match the reference exactly** — real `h:mm` time values,
   `dd/mm/yyyy` dates, `0.00` net hours, and a banded Excel Table with autofilter +
   bold header.
8. **Translations**: reuse existing resx keys for all 21 headers; add exactly **one**
   new key for the sheet name, translated into **all 25 culture files** (+ neutral).
9. `DayOfWeek` keeps the existing lowercase localized weekday
   (`planning.Date.ToString("dddd", culture)`).

## Architecture

### Existing structure (for context)

- Service: `Services/TimePlanningWorkingHoursService/TimePlanningWorkingHoursService.cs`
  - Single worker: `GenerateExcelDashboard(TimePlanningWorkingHoursRequestModel)` (~line 2343)
    — creates one `"Dashboard"` worksheet.
  - All workers: `GenerateExcelDashboard(TimePlanningWorkingHoursReportForAllWorkersRequestModel)`
    (~line 2847) — creates a `"Total"` sheet + one sheet per site; pre-computes a
    `perSiteCache` (`AllWorkersSiteCache`) of working-hours/pay data.
  - Shared cell builders: `CreateCell`, `CreateNumericCell`, `CreateDateCell`
    (StyleIndex 2), `CreateWeekNumberCell`; row builder `FillDataRow`.
  - `CurrentUICulture` is set before headers are read (lines ~2375 and ~2877), so
    `Translations.X` resolves in the user's language.
- Low-level OpenXML helper: `Infrastructure/Helpers/OpenXMLHelper.cs`
  - `GenerateWorkbookPart1Content(WorkbookPart, List<KeyValuePair<string,string>> sheets)`
    registers the workbook's `Sheet` elements (name → relationship id).
  - `GenerateWorkbookStylesPart1Content(...)` builds the stylesheet (`CellFormats`);
    date format is `StyleIndex 2`.
- Library: `DocumentFormat.OpenXml` 3.5.1 (transitive via `Microting.eFormApi.BasePn`).
- Translations: strongly-typed `Translations` (neutral `Resources/Translations.Designer.cs`)
  + per-culture `Resources/Translations.<culture>.resx` satellite assemblies. The
  per-culture `Translations_xx` Designer classes are unused by the export.

### New units

**1. `BuildDayOverviewWorksheet(...)` — shared private method (new)**

Single, isolated builder responsible for the entire Dagsoversigt worksheet:

- **Input**: an ordered list of row items, each carrying the data needed for one row:
  `(EmployeeNo, WorkerOrSiteName, Date, planning)` — enough to emit all 21 cells.
  Both export methods construct this list and pass it in.
- **Output**: appends a fully-built `WorksheetPart` (worksheet + sheet data + table +
  autofilter + page margins) for the Dagsoversigt sheet.
- **Responsibilities**:
  - Header row from the existing resx keys (fixed 21 columns).
  - One data row per item: employee no (string), worker/site name (string), localized
    weekday (string), date (real date cell, `dd/mm/yyyy`), week number (numeric), five
    shift blocks as real `h:mm` time cells, net hours (numeric, `0.00`).
  - The banded Excel `TableDefinitionPart` over the full range with autofilter and
    header row.
- **Why isolated**: one clear purpose (render the overview), well-defined input, no
  dependency on the surrounding export method's local state beyond the row list and
  culture/language. Independently reasoned about and testable.

**2. Time-value helper (new, small)**

Convert a shift time into an OADate time-of-day fraction (minutes-since-midnight ÷ 1440)
so cells are real `h:mm` time values rather than strings. Sourced from the same
underlying shift data the existing `GetShiftTime(...)` reads; empty/absent shift times
produce an empty cell (not `0:00`), matching the reference where unused shifts are blank.

> Implementation note: confirm the unit of `planning.ShiftNStart/Stop/Pause` and how
> `GetShiftTime` derives its `HH:mm` string, then convert from that same source to the
> fraction. This is the main implementation-time investigation.

**3. New cell styles in `OpenXMLHelper` stylesheet**

Add (or reuse, if already present) the `CellFormats` needed:
- `h:mm` time format (for shift start/stop/pause cells),
- `0.00` number format (for net hours),
- `dd/mm/yyyy` date format (verify whether existing `StyleIndex 2` already is `dd/mm/yyyy`;
  if not, add a dedicated style and use it for the Dagsoversigt Date column).

New `CellFormat` entries get new `StyleIndex` values; the builder references those
indices. Existing styles/indices are left unchanged to avoid disturbing the other
sheets.

**4. Sheet registration changes**

- Single-worker export: change the workbook sheet list from `[("Dashboard","rId1")]`
  to `[(Translations.DayOverview,"rId1"), ("Dashboard","rId2")]`, create the Dagsoversigt
  worksheet part as `rId1` and the Dashboard part as `rId2`.
- All-workers export: prepend `(Translations.DayOverview, "rId?")` as the first sheet and
  shift the existing relationship ids (Total + per-site) accordingly.
- Excel tab-name limit (31 chars) and illegal chars (`: \ / ? * [ ]`): "Dagsoversigt"
  (and the chosen translations) must be validated/sanitized the same way existing
  per-site names are truncated. "Dagsoversigt" is 12 chars and clean.

### Data flow

```
Single-worker GenerateExcelDashboard(model)
  Index(model)  ──►  per-day plannings (Skip(1))
                       └─► build row list (one worker)  ──►  BuildDayOverviewWorksheet
                       └─► existing Dashboard sheet (unchanged)

All-workers GenerateExcelDashboard(model)
  perSiteCache (existing pre-pass over all sites)
     └─► flatten to row list (all workers × their planning days)
            └─► sort by (Date asc, EmployeeNo asc)  ──►  BuildDayOverviewWorksheet
     └─► existing Total + per-site sheets (unchanged)
```

The all-workers builder reuses the already-computed `perSiteCache` so no extra DB query
is introduced. The single-worker builder reuses its existing `Index(...)` result.

### Translation

Add one key, **`DayOverview`**:

1. Neutral `Resources/Translations.resx`: `<data name="DayOverview"><value>Day overview</value></data>`.
2. `Resources/Translations.da.resx`: `<value>Dagsoversigt</value>`.
3. All other 24 culture `.resx` files: a translated value each (filled for every
   language; any lower-confidence translations flagged for review).
4. Neutral `Resources/Translations.Designer.cs`: add accessor
   ```csharp
   internal static string DayOverview {
       get { return ResourceManager.GetString("DayOverview", resourceCulture); }
   }
   ```
   (key string `"DayOverview"`, no spaces, so accessor name == key.)
5. Use `Translations.DayOverview` as the sheet name in both export methods.
   `CurrentUICulture` is already set before this point in both methods.

The per-culture `Translations_xx` Designer classes are unused by the export and are not
edited. The `localization.json` / `ITimePlanningLocalizationService` path is not needed
(headers/sheet names use the resx path).

## Error handling

- The builder follows the existing pattern: wrap row construction in try/catch,
  `SentrySdk.CaptureException`, log, rethrow (consistent with `FillDataRow`).
- Both export methods already finalize with `ValidateExcel(...)` (`OpenXmlValidator`);
  the added worksheet + table part must pass validation. The Excel Table part is the
  highest-risk element for validation — it must declare correct `ref`, column count,
  unique table id/name, and matching autofilter range.
- Empty shift times render as empty cells (not `0:00`).
- A worker/day with no data still renders a row with `0.00` net hours (decision 6).

## Testing

- **Unit-style / focused**: a test that feeds `BuildDayOverviewWorksheet` a small known
  row list and asserts the produced sheet has 21 headers in the right order, the right
  number of data rows, correct date/time/number style indices, and a valid table part
  (passes `OpenXmlValidator`).
- **Sort**: assert combined all-workers rows come out ordered by (date, employee no).
- **Translation**: assert the sheet name resolves to "Dagsoversigt" under `da` and
  "Day overview" under the neutral/`en` culture.
- **Integration**: run both endpoints against seed data and open the resulting files to
  confirm Dagsoversigt is the first tab, the table is banded with autofilter, times show
  as `h:mm`, dates as `dd/mm/yyyy`, net hours as `0.00`, and the existing sheets are
  unchanged.
- Follow the plugin's existing test conventions (verify how the current export is
  tested, if at all, during implementation and match it).

## Out of scope

- No changes to the existing Dashboard / Total / per-site sheets' content or layout.
- No new DB queries or schema changes (no base repo).
- No frontend/Angular changes (the export is triggered by existing endpoints/buttons).
- No new column data beyond the reference's 21 columns (PlanText, Flex, Message,
  Comments, pay-codes, etc. are intentionally excluded from this sheet).

## Files to change

All in the host-app mirror `eform-angular-frontend/eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn/`
(mirrored back to the source repo via `devgetchanges.sh`):

- `Services/TimePlanningWorkingHoursService/TimePlanningWorkingHoursService.cs`
  — new `BuildDayOverviewWorksheet` + time-value helper; wire both export methods to add
  the sheet as first tab and adjust relationship ids.
- `Infrastructure/Helpers/OpenXMLHelper.cs` — add `h:mm`, `0.00`, and (if needed)
  `dd/mm/yyyy` cell styles.
- `Resources/Translations.resx` + `Resources/Translations.<25 cultures>.resx` — new
  `DayOverview` key.
- `Resources/Translations.Designer.cs` — new `DayOverview` accessor.
