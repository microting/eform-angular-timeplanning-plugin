# Dagsoversigt Export Sheet Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a new "Dagsoversigt" (Day overview) worksheet as the first tab in both the single-worker and all-workers timeplanning Excel exports, replicating the layout and formatting of the reference file.

**Architecture:** A single shared private builder `BuildDayOverviewWorksheet` produces a fully-referenced, banded Excel-Table worksheet from a flat list of `DayOverviewRow` items. Both export methods construct the row list (single-worker from its `timePlannings`; all-workers by flattening `perSiteCache`, sorted by date then employee no.) and create the sheet as relationship `rId1`, shifting the existing parts' relationship ids accordingly. The sheet name is localized via a new `Translations.DayOverview` resx key; all 21 column headers reuse existing resx keys.

**Tech Stack:** C# / .NET 10, DocumentFormat.OpenXml 3.5.1 (raw SDK), NUnit 4 + Testcontainers.MariaDb test project. Dev mode: edit in host-app mirror `eform-angular-frontend/eFormAPI/Plugins/TimePlanning.Pn/`, then `devgetchanges.sh` back to the source repo before committing.

**Key file (all line numbers below refer to it unless stated):**
`eform-angular-frontend/eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn/Services/TimePlanningWorkingHoursService/TimePlanningWorkingHoursService.cs`

**Reference fixed 21-column layout** (always all 5 shifts):
`Employee no` · `Worker` · `DayOfWeek` · `Date` · `Week number` · `Shift 1: start/end/pause` … `Shift 5: start/end/pause` · `NettoHours`. All map to existing resx keys (verified: `Employee no`→"Medarbejder nr.", `Shift 1: end`→"Skift 1: stop", `NettoHours`→"Timer netto").

---

## Task 1: Add the `DayOverview` translation key (sheet name)

**Files (host-app mirror `…/TimePlanning.Pn/TimePlanning.Pn/Resources/`):**
- Modify: `Translations.resx` (neutral / English)
- Modify: `Translations.Designer.cs` (add strongly-typed accessor)
- Modify: `Translations.da.resx` … and all 24 other culture `.resx` files

Existing keys use natural-language `data name` strings with spaces/colons; the neutral
`Translations.Designer.cs` accessor calls `ResourceManager.GetString("<data name>", resourceCulture)`.
For the new key use a space-free name `DayOverview` so the accessor name equals the key.

- [ ] **Step 1: Add the neutral entry to `Translations.resx`**

Insert alongside the other `<data>` elements:

```xml
  <data name="DayOverview" xml:space="preserve">
    <value>Day overview</value>
  </data>
```

- [ ] **Step 2: Add the strongly-typed accessor to `Translations.Designer.cs`**

Add this property inside the `internal class Translations { … }` body (match the existing block style, e.g. next to `NettoHours`):

```csharp
        internal static string DayOverview {
            get {
                return ResourceManager.GetString("DayOverview", resourceCulture);
            }
        }
```

- [ ] **Step 3: Add the translated value to every culture `.resx` file**

Add a `<data name="DayOverview" xml:space="preserve"><value>…</value></data>` entry to each file, using these values (the `data name` MUST be identical across all files):

| File | value |
|------|-------|
| `Translations.da.resx` | Dagsoversigt |
| `Translations.de.resx` | Tagesübersicht |
| `Translations.nl-NL.resx` | Dagoverzicht |
| `Translations.sv-SE.resx` | Dagsöversikt |
| `Translations.no-NO.resx` | Dagsoversikt |
| `Translations.fi-FI.resx` | Päiväkatsaus |
| `Translations.fr-FR.resx` | Aperçu de la journée |
| `Translations.es-ES.resx` | Resumen diario |
| `Translations.it-IT.resx` | Riepilogo giornaliero |
| `Translations.pt-BR.resx` | Resumo diário |
| `Translations.pt-PT.resx` | Resumo diário |
| `Translations.pl-PL.resx` | Przegląd dnia |
| `Translations.et-EE.resx` | Päevaülevaade |
| `Translations.lv-LV.resx` | Dienas pārskats |
| `Translations.lt-LT.resx` | Dienos apžvalga |
| `Translations.ro-RO.resx` | Rezumat zilnic |
| `Translations.hr-HR.resx` | Dnevni pregled |
| `Translations.sl-SI.resx` | Dnevni pregled |
| `Translations.cs-CZ.resx` | Denní přehled |
| `Translations.sk-SK.resx` | Denný prehľad |
| `Translations.hu-HU.resx` | Napi áttekintés |
| `Translations.bg-BG.resx` | Дневен преглед |
| `Translations.el-GR.resx` | Ημερήσια επισκόπηση |
| `Translations.is-IS.resx` | Dagsyfirlit |
| `Translations.uk-UA.resx` | Огляд за день |

> Flag for user review (lower confidence): `fi-FI`, `ro-RO`, `uk-UA`, `el-GR`. All values are < 31 chars and contain no Excel-illegal sheet-name chars (`: \ / ? * [ ]`).

- [ ] **Step 4: Verify the project still builds**

Run: `cd /home/rene/Documents/workspace/microting/eform-angular-frontend/eFormAPI && dotnet build Plugins/TimePlanning.Pn/TimePlanning.Pn/TimePlanning.Pn.csproj`
Expected: Build succeeded (the resx files compile into satellite assemblies; the new accessor resolves).

- [ ] **Step 5: Commit** (in the source repo after `devgetchanges.sh`; see Task 7. For now, do not commit — accumulate changes and commit once at the end of the cycle.)

---

## Task 2: Add the new cell styles to the stylesheet

**Files:**
- Modify: `…/TimePlanning.Pn/TimePlanning.Pn/Infrastructure/Helpers/OpenXMLHelper.cs` — `GenerateWorkbookStylesPart1Content`

Current state (verified): no `NumberingFormats` element exists; `CellFormats.Count = 3U` with
StyleIndex 0 = default, 1 = bold header, 2 = date (built-in numFmtId 14, locale short date).
Adding new styles at indices 3/4/5 leaves existing indices untouched, so the other sheets are
unaffected. Schema order requires `NumberingFormats` to be appended **before** `Fonts`.

- [ ] **Step 1: Add a `NumberingFormats` element with custom formats**

Find the line that creates `CellFormats cellFormats1 = new CellFormats(){ Count = (UInt32Value)3U };` (line ~128). Immediately before the existing `Fonts`/stylesheet construction, add:

```csharp
            NumberingFormats numberingFormats1 = new NumberingFormats() { Count = (UInt32Value)2U };
            numberingFormats1.Append(new NumberingFormat() { NumberFormatId = (UInt32Value)164U, FormatCode = "dd/mm/yyyy" });
            numberingFormats1.Append(new NumberingFormat() { NumberFormatId = (UInt32Value)165U, FormatCode = "hh:mm" });
```

(Place this declaration wherever the other style-element declarations are; it gets appended to the stylesheet in Step 3.)

- [ ] **Step 2: Add three new `CellFormat` entries and bump the count**

Change the count and append three formats after the existing `cellFormats1.Append(cellFormat4);`:

```csharp
            CellFormats cellFormats1 = new CellFormats(){ Count = (UInt32Value)6U };
            CellFormat cellFormat2 = new CellFormat(){ NumberFormatId = (UInt32Value)0U, FontId = (UInt32Value)0U, FillId = (UInt32Value)0U, BorderId = (UInt32Value)0U, FormatId = (UInt32Value)0U };
            CellFormat cellFormat3 = new CellFormat(){ NumberFormatId = (UInt32Value)0U, FontId = (UInt32Value)1U, FillId = (UInt32Value)0U, BorderId = (UInt32Value)0U, FormatId = (UInt32Value)0U, ApplyFont = true };
            CellFormat cellFormat4 = new CellFormat(){ NumberFormatId = (UInt32Value)14U, FontId = (UInt32Value)0U, FillId = (UInt32Value)0U, BorderId = (UInt32Value)0U, FormatId = (UInt32Value)0U, ApplyNumberFormat = true };
            // New: StyleIndex 3 = time hh:mm
            CellFormat cellFormat5 = new CellFormat(){ NumberFormatId = (UInt32Value)165U, FontId = (UInt32Value)0U, FillId = (UInt32Value)0U, BorderId = (UInt32Value)0U, FormatId = (UInt32Value)0U, ApplyNumberFormat = true };
            // New: StyleIndex 4 = number 0.00 (built-in numFmtId 2)
            CellFormat cellFormat6 = new CellFormat(){ NumberFormatId = (UInt32Value)2U, FontId = (UInt32Value)0U, FillId = (UInt32Value)0U, BorderId = (UInt32Value)0U, FormatId = (UInt32Value)0U, ApplyNumberFormat = true };
            // New: StyleIndex 5 = date dd/mm/yyyy (custom numFmtId 164)
            CellFormat cellFormat7 = new CellFormat(){ NumberFormatId = (UInt32Value)164U, FontId = (UInt32Value)0U, FillId = (UInt32Value)0U, BorderId = (UInt32Value)0U, FormatId = (UInt32Value)0U, ApplyNumberFormat = true };

            cellFormats1.Append(cellFormat2);
            cellFormats1.Append(cellFormat3);
            cellFormats1.Append(cellFormat4);
            cellFormats1.Append(cellFormat5);
            cellFormats1.Append(cellFormat6);
            cellFormats1.Append(cellFormat7);
```

- [ ] **Step 3: Append `NumberingFormats` first in the stylesheet append block**

In the `stylesheet1.Append(...)` sequence (lines ~161-169), add as the **first** append (before the fonts append):

```csharp
            stylesheet1.Append(numberingFormats1);
```

- [ ] **Step 4: Build to verify**

Run: `cd /home/rene/Documents/workspace/microting/eform-angular-frontend/eFormAPI && dotnet build Plugins/TimePlanning.Pn/TimePlanning.Pn/TimePlanning.Pn.csproj`
Expected: Build succeeded.

---

## Task 3: Add the builder, DTO, and helpers

**Files:**
- Modify: `…/TimePlanningWorkingHoursService/TimePlanningWorkingHoursService.cs`
- Test: `…/TimePlanning.Pn.Test/DagsoversigtWorksheetExportTests.cs` (new, added in Task 6)

Add a private nested DTO, a pure time-fraction helper, referenced-cell helpers, and the
worksheet builder. `GetColumnLetter(int)` (existing, 1-based, returns "A" for 1 … "U" for 21) is reused.

- [ ] **Step 1: Add the `DayOverviewRow` DTO**

Add next to the existing `private sealed class AllWorkersSiteCache` (line ~3901):

```csharp
    private sealed class DayOverviewRow
    {
        public string EmployeeNo { get; set; }
        public string WorkerName { get; set; }
        public DateTime Date { get; set; }
        public TimePlanningWorkingHoursModel Planning { get; set; }
        public bool UseOneMinuteIntervals { get; set; }
    }
```

- [ ] **Step 2: Add the pure time-fraction helper**

The shift int fields (`Shift1Start` etc.) are a 1-based index into a 288-entry 5-minute grid
(`(index-1)*5` minutes; `289` is the special 24:00). When `useOneMinuteIntervals` and a stamp
exist, use the stamp's time-of-day. Returns `null` for an absent shift (→ empty, formatted cell).

```csharp
    internal double? GetShiftTimeFraction(int? shift, DateTime? actualStamp, bool useOneMinuteIntervals)
    {
        if (useOneMinuteIntervals && actualStamp.HasValue)
        {
            return actualStamp.Value.TimeOfDay.TotalMinutes / 1440.0;
        }
        if (!shift.HasValue || shift.Value <= 0)
        {
            return null;
        }
        if (shift.Value == 289)
        {
            return 1.0; // 24:00 — end of day; renders as 00:00 under hh:mm (known minor edge)
        }
        return (shift.Value - 1) * 5 / 1440.0;
    }
```

- [ ] **Step 3: Add referenced-cell helpers**

These set `CellReference` (required for cells inside an Excel Table):

```csharp
    private Cell DayOverviewStringCell(int col, uint rowIdx, string value)
    {
        return new Cell
        {
            CellReference = $"{GetColumnLetter(col)}{rowIdx}",
            CellValue = new CellValue(value ?? string.Empty),
            DataType = CellValues.String
        };
    }

    private Cell DayOverviewNumberCell(int col, uint rowIdx, double value, uint? styleIndex)
    {
        var cell = new Cell
        {
            CellReference = $"{GetColumnLetter(col)}{rowIdx}",
            CellValue = new CellValue(value.ToString(CultureInfo.InvariantCulture)),
            DataType = CellValues.Number
        };
        if (styleIndex.HasValue)
        {
            cell.StyleIndex = styleIndex.Value;
        }
        return cell;
    }

    private Cell DayOverviewDateCell(int col, uint rowIdx, DateTime date)
    {
        return new Cell
        {
            CellReference = $"{GetColumnLetter(col)}{rowIdx}",
            CellValue = new CellValue(date.ToOADate().ToString(CultureInfo.InvariantCulture)),
            DataType = CellValues.Number,
            StyleIndex = (UInt32Value)5U // dd/mm/yyyy
        };
    }

    private Cell DayOverviewTimeCell(int col, uint rowIdx, double? fraction)
    {
        var cell = new Cell
        {
            CellReference = $"{GetColumnLetter(col)}{rowIdx}",
            DataType = CellValues.Number,
            StyleIndex = (UInt32Value)3U // hh:mm
        };
        if (fraction.HasValue)
        {
            cell.CellValue = new CellValue(fraction.Value.ToString(CultureInfo.InvariantCulture));
        }
        return cell;
    }
```

- [ ] **Step 4: Add the `BuildDayOverviewWorksheet` builder**

```csharp
    private void BuildDayOverviewWorksheet(WorksheetPart worksheetPart, List<DayOverviewRow> rows, CultureInfo culture)
    {
        const int colCount = 21;
        var headers = new[]
        {
            Translations.Employee_no, Translations.Worker, Translations.DayOfWeek,
            Translations.Date, Translations.Week_number,
            Translations.Shift_1__start, Translations.Shift_1__end, Translations.Shift_1__pause,
            Translations.Shift_2__start, Translations.Shift_2__end, Translations.Shift_2__pause,
            Translations.Shift_3__start, Translations.Shift_3__end, Translations.Shift_3__pause,
            Translations.Shift_4__start, Translations.Shift_4__end, Translations.Shift_4__pause,
            Translations.Shift_5__start, Translations.Shift_5__end, Translations.Shift_5__pause,
            Translations.NettoHours
        };

        var worksheet = new Worksheet()
            { MCAttributes = new MarkupCompatibilityAttributes() { Ignorable = "x14ac xr xr2 xr3" } };
        worksheet.AddNamespaceDeclaration("r", "http://schemas.openxmlformats.org/officeDocument/2006/relationships");
        worksheet.AddNamespaceDeclaration("mc", "http://schemas.openxmlformats.org/markup-compatibility/2006");
        worksheet.AddNamespaceDeclaration("x14ac", "http://schemas.microsoft.com/office/spreadsheetml/2009/9/ac");
        worksheet.AddNamespaceDeclaration("xr", "http://schemas.microsoft.com/office/spreadsheetml/2014/revision");
        worksheet.AddNamespaceDeclaration("xr2", "http://schemas.microsoft.com/office/spreadsheetml/2015/revision2");
        worksheet.AddNamespaceDeclaration("xr3", "http://schemas.microsoft.com/office/spreadsheetml/2016/revision3");

        var sheetFormatProperties = new SheetFormatProperties() { DefaultRowHeight = 15D, DyDescent = 0.25D };

        var columns = new Columns();
        columns.Append(new Column() { Min = 1U, Max = 1U, Width = 18D, CustomWidth = true });
        columns.Append(new Column() { Min = 2U, Max = 2U, Width = 15D, CustomWidth = true });
        columns.Append(new Column() { Min = 3U, Max = 3U, Width = 10D, CustomWidth = true });
        columns.Append(new Column() { Min = 4U, Max = 4U, Width = 11D, CustomWidth = true });
        columns.Append(new Column() { Min = 5U, Max = 5U, Width = 8D, CustomWidth = true });
        columns.Append(new Column() { Min = 6U, Max = 20U, Width = 13.5D, CustomWidth = true });
        columns.Append(new Column() { Min = 21U, Max = 21U, Width = 12D, CustomWidth = true });

        var sheetData = new SheetData();

        var headerRow = new Row() { RowIndex = (UInt32Value)1U };
        for (int c = 0; c < colCount; c++)
        {
            headerRow.Append(new Cell
            {
                CellReference = $"{GetColumnLetter(c + 1)}1",
                CellValue = new CellValue(headers[c]),
                DataType = CellValues.String,
                StyleIndex = (UInt32Value)1U
            });
        }
        sheetData.Append(headerRow);

        uint rowIndex = 2;
        foreach (var row in rows)
        {
            var dataRow = new Row() { RowIndex = rowIndex };
            int c = 1;
            dataRow.Append(DayOverviewStringCell(c++, rowIndex, row.EmployeeNo));
            dataRow.Append(DayOverviewStringCell(c++, rowIndex, row.WorkerName));
            dataRow.Append(DayOverviewStringCell(c++, rowIndex, row.Date.ToString("dddd", culture)));
            dataRow.Append(DayOverviewDateCell(c++, rowIndex, row.Date));
            var weekNumber = culture.Calendar.GetWeekOfYear(row.Date, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
            dataRow.Append(DayOverviewNumberCell(c++, rowIndex, weekNumber, null));

            var p = row.Planning;
            dataRow.Append(DayOverviewTimeCell(c++, rowIndex, GetShiftTimeFraction(p.Shift1Start, p.Start1StartedAt, row.UseOneMinuteIntervals)));
            dataRow.Append(DayOverviewTimeCell(c++, rowIndex, GetShiftTimeFraction(p.Shift1Stop, p.Stop1StoppedAt, row.UseOneMinuteIntervals)));
            dataRow.Append(DayOverviewTimeCell(c++, rowIndex, GetShiftTimeFraction(p.Shift1Pause, null, row.UseOneMinuteIntervals)));
            dataRow.Append(DayOverviewTimeCell(c++, rowIndex, GetShiftTimeFraction(p.Shift2Start, p.Start2StartedAt, row.UseOneMinuteIntervals)));
            dataRow.Append(DayOverviewTimeCell(c++, rowIndex, GetShiftTimeFraction(p.Shift2Stop, p.Stop2StoppedAt, row.UseOneMinuteIntervals)));
            dataRow.Append(DayOverviewTimeCell(c++, rowIndex, GetShiftTimeFraction(p.Shift2Pause, null, row.UseOneMinuteIntervals)));
            dataRow.Append(DayOverviewTimeCell(c++, rowIndex, GetShiftTimeFraction(p.Shift3Start, p.Start3StartedAt, row.UseOneMinuteIntervals)));
            dataRow.Append(DayOverviewTimeCell(c++, rowIndex, GetShiftTimeFraction(p.Shift3Stop, p.Stop3StoppedAt, row.UseOneMinuteIntervals)));
            dataRow.Append(DayOverviewTimeCell(c++, rowIndex, GetShiftTimeFraction(p.Shift3Pause, null, row.UseOneMinuteIntervals)));
            dataRow.Append(DayOverviewTimeCell(c++, rowIndex, GetShiftTimeFraction(p.Shift4Start, p.Start4StartedAt, row.UseOneMinuteIntervals)));
            dataRow.Append(DayOverviewTimeCell(c++, rowIndex, GetShiftTimeFraction(p.Shift4Stop, p.Stop4StoppedAt, row.UseOneMinuteIntervals)));
            dataRow.Append(DayOverviewTimeCell(c++, rowIndex, GetShiftTimeFraction(p.Shift4Pause, null, row.UseOneMinuteIntervals)));
            dataRow.Append(DayOverviewTimeCell(c++, rowIndex, GetShiftTimeFraction(p.Shift5Start, p.Start5StartedAt, row.UseOneMinuteIntervals)));
            dataRow.Append(DayOverviewTimeCell(c++, rowIndex, GetShiftTimeFraction(p.Shift5Stop, p.Stop5StoppedAt, row.UseOneMinuteIntervals)));
            dataRow.Append(DayOverviewTimeCell(c++, rowIndex, GetShiftTimeFraction(p.Shift5Pause, null, row.UseOneMinuteIntervals)));

            var netto = p.NettoHoursOverrideActive ? p.NettoHoursOverride : p.NettoHours;
            dataRow.Append(DayOverviewNumberCell(c, rowIndex, netto, (UInt32Value)4U));

            sheetData.Append(dataRow);
            rowIndex++;
        }

        uint lastRow = rowIndex - 1; // header-only when no data => 1
        string reference = $"A1:{GetColumnLetter(colCount)}{lastRow}";

        var pageMargins = new PageMargins() { Left = 0.7D, Right = 0.7D, Top = 0.75D, Bottom = 0.75D, Header = 0.3D, Footer = 0.3D };

        worksheet.Append(sheetFormatProperties);
        worksheet.Append(columns);
        worksheet.Append(sheetData);
        worksheet.Append(pageMargins);

        var tableDefinitionPart = worksheetPart.AddNewPart<TableDefinitionPart>("rIdDayOverviewTable");
        var table = new Table()
        {
            Id = (UInt32Value)1U,
            Name = "DayOverview",
            DisplayName = "DayOverview",
            Reference = reference,
            TotalsRowShown = false
        };
        table.Append(new AutoFilter() { Reference = reference });
        var tableColumns = new TableColumns() { Count = (UInt32Value)(uint)colCount };
        for (uint tc = 1; tc <= colCount; tc++)
        {
            tableColumns.Append(new TableColumn() { Id = (UInt32Value)tc, Name = headers[tc - 1] });
        }
        table.Append(tableColumns);
        table.Append(new TableStyleInfo()
        {
            Name = "TableStyleMedium2",
            ShowFirstColumn = false,
            ShowLastColumn = false,
            ShowRowStripes = true,
            ShowColumnStripes = false
        });
        tableDefinitionPart.Table = table;

        var tableParts = new TableParts() { Count = (UInt32Value)1U };
        tableParts.Append(new TablePart() { Id = "rIdDayOverviewTable" });
        worksheet.Append(tableParts);

        worksheetPart.Worksheet = worksheet;
    }
```

- [ ] **Step 5: Confirm required usings are present**

`System.Globalization` (CultureInfo, CalendarWeekRule), `System.Linq`, the OpenXml Spreadsheet namespace, and `TimePlanning.Pn.Resources` are already imported in this file (the existing export uses all of them). No new `using` needed. Build to confirm:

Run: `cd /home/rene/Documents/workspace/microting/eform-angular-frontend/eFormAPI && dotnet build Plugins/TimePlanning.Pn/TimePlanning.Pn/TimePlanning.Pn.csproj`
Expected: Build succeeded.

---

## Task 4: Wire the single-worker export

**Files:**
- Modify: `…/TimePlanningWorkingHoursService.cs` `GenerateExcelDashboard(TimePlanningWorkingHoursRequestModel)` (lines ~2421-2433)

In-scope variables already available: `site`, `worker` (with `worker.EmployeeNo`), `assignedSite`
(with `.UseOneMinuteIntervals`), `timePlannings` (List<TimePlanningWorkingHoursModel>), `culture`.

rId remap: Dagsoversigt = rId1, Dashboard = rId2, Theme = rId3, Styles = rId4.

- [ ] **Step 1: Register two sheets and shift the part rIds**

Replace lines 2425-2433:

```csharp
                OpenXMLHelper.GenerateWorkbookPart1Content(workbookPart1,
                    [new(Translations.DayOverview, "rId1"), new("Dashboard", "rId2")]);

                WorkbookStylesPart workbookStylesPart1 = workbookPart1.AddNewPart<WorkbookStylesPart>("rId4");
                OpenXMLHelper.GenerateWorkbookStylesPart1Content(workbookStylesPart1);

                ThemePart themePart1 = workbookPart1.AddNewPart<ThemePart>("rId3");
                OpenXMLHelper.GenerateThemePart1Content(themePart1);

                // Dagsoversigt (Day overview) — first tab
                WorksheetPart dayOverviewWorksheetPart = workbookPart1.AddNewPart<WorksheetPart>("rId1");
                var dayOverviewRows = timePlannings.Select(p => new DayOverviewRow
                {
                    EmployeeNo = worker.EmployeeNo ?? string.Empty,
                    WorkerName = site.Name,
                    Date = p.Date,
                    Planning = p,
                    UseOneMinuteIntervals = assignedSite.UseOneMinuteIntervals
                }).ToList();
                BuildDayOverviewWorksheet(dayOverviewWorksheetPart, dayOverviewRows, culture);

                WorksheetPart worksheetPart1 = workbookPart1.AddNewPart<WorksheetPart>("rId2");
```

(The remainder of the method — building the Dashboard sheet on `worksheetPart1` — is unchanged.)

- [ ] **Step 2: Build to verify**

Run: `cd /home/rene/Documents/workspace/microting/eform-angular-frontend/eFormAPI && dotnet build Plugins/TimePlanning.Pn/TimePlanning.Pn/TimePlanning.Pn.csproj`
Expected: Build succeeded.

---

## Task 5: Wire the all-workers export

**Files:**
- Modify: `…/TimePlanningWorkingHoursService.cs` `GenerateExcelDashboard(TimePlanningWorkingHoursReportForAllWorkersRequestModel)` (lines ~2955-3081)

rId remap: Dagsoversigt = rId1, Total = rId2, per-site = rId{i+3}, Theme = rId{count+3}, Styles = rId{count+4}.

- [ ] **Step 1: Insert Dagsoversigt into `worksheetNames` and shift the rest**

Replace lines 2960-2972:

```csharp
                var worksheetNames = new List<KeyValuePair<string, string>>();
                worksheetNames.Add(new KeyValuePair<string, string>(Translations.DayOverview, "rId1"));
                worksheetNames.Add(new KeyValuePair<string, string>("Total", "rId2"));

                for (int i = 0; i < siteIdCount; i++)
                {
                    var site = await sdkContext.Sites.SingleOrDefaultAsync(x =>
                        x.MicrotingUid == siteIds[i] && x.WorkflowState != Constants.WorkflowStates.Removed);
                    if (site == null) continue;
                    worksheetNames.Add(
                        new KeyValuePair<string, string>($"{site.Name.Substring(0, Math.Min(31, site.Name.Length))}",
                            $"rId{i + 3}"));
                }
```

- [ ] **Step 2: Shift Theme/Styles part rIds**

Replace lines 2977-2982:

```csharp
                WorkbookStylesPart workbookStylesPart1 =
                    workbookPart1.AddNewPart<WorkbookStylesPart>($"rId{siteIdCount + 4}");
                OpenXMLHelper.GenerateWorkbookStylesPart1Content(workbookStylesPart1);

                ThemePart themePart1 = workbookPart1.AddNewPart<ThemePart>($"rId{siteIdCount + 3}");
                OpenXMLHelper.GenerateThemePart1Content(themePart1);
```

- [ ] **Step 3: Build the Dagsoversigt sheet (rId1) right after the Theme part**

Insert immediately after the `GenerateThemePart1Content(themePart1);` line from Step 2 and before `#region TotalSheetSetup`:

```csharp
                #region DayOverviewSheetSetup

                WorksheetPart dayOverviewWorksheetPart = workbookPart1.AddNewPart<WorksheetPart>("rId1");
                var dayOverviewRows = new List<DayOverviewRow>();
                for (int i = 0; i < siteIdCount; i++)
                {
                    var doSite = await sdkContext.Sites.FirstOrDefaultAsync(x => x.MicrotingUid == siteIds[i]);
                    if (doSite == null) continue;
                    var doSiteWorker = await sdkContext.SiteWorkers.FirstAsync(x => x.SiteId == doSite.Id);
                    var doWorker = await sdkContext.Workers.FirstAsync(x => x.Id == doSiteWorker.WorkerId);
                    perSiteCache.TryGetValue(siteIds[i], out var doCache);
                    var doPlannings = doCache?.TimePlannings ?? new List<TimePlanningWorkingHoursModel>();
                    var doUseOneMinute = doCache?.AssignedSite?.UseOneMinuteIntervals ?? false;
                    foreach (var planning in doPlannings)
                    {
                        dayOverviewRows.Add(new DayOverviewRow
                        {
                            EmployeeNo = doWorker.EmployeeNo ?? string.Empty,
                            WorkerName = doSite.Name,
                            Date = planning.Date,
                            Planning = planning,
                            UseOneMinuteIntervals = doUseOneMinute
                        });
                    }
                }
                dayOverviewRows = dayOverviewRows
                    .OrderBy(r => r.Date)
                    .ThenBy(r => int.TryParse(r.EmployeeNo, out var n) ? n : int.MaxValue)
                    .ThenBy(r => r.EmployeeNo)
                    .ToList();
                BuildDayOverviewWorksheet(dayOverviewWorksheetPart, dayOverviewRows, culture);

                #endregion
```

- [ ] **Step 4: Shift the Total and per-site worksheet part rIds**

- Line 2986: `WorksheetPart totalWorksheetPart1 = workbookPart1.AddNewPart<WorksheetPart>($"rId1");` → `$"rId2"`.
- Line 3081: `WorksheetPart worksheetPart1 = workbookPart1.AddNewPart<WorksheetPart>($"rId{i + 2}");` → `$"rId{i + 3}"`.

- [ ] **Step 5: Build to verify**

Run: `cd /home/rene/Documents/workspace/microting/eform-angular-frontend/eFormAPI && dotnet build Plugins/TimePlanning.Pn/TimePlanning.Pn/TimePlanning.Pn.csproj`
Expected: Build succeeded.

---

## Task 6: Tests

**Files:**
- Test: `…/TimePlanning.Pn/TimePlanning.Pn.Test/DagsoversigtWorksheetExportTests.cs` (new)

Follow the existing `WorkingHoursExcelExportE2ETests` conventions: NUnit 4 (`[TestFixture]`,
`[Test]`, `Assert.That`), extend the same base setup, seed via the same helpers, open the produced
xlsx with `SpreadsheetDocument.Open`, and assert positionally. The localization substitute returns
the key as the value, so under that fixture the sheet name resolves to the resx-backed value via
`Translations.DayOverview` + `CurrentUICulture` (set inside the export). Note `ValidateExcel`
swallows errors, so assertions must read the file directly.

> Before writing, open `WorkingHoursExcelExportE2ETests.cs` and copy its `[SetUp]`, the seed
> helper (`SeedSiteAndPlanRegistration`), and the cell-reading helper pattern so the new fixture
> matches exactly (constructor wiring of `IUserService`, `ITimePlanningLocalizationService`,
> `IEFormCoreService`, `TimePlanningPnDbContext`, `IPluginDbOptions`).

- [ ] **Step 1: Unit test the pure time-fraction helper**

`GetShiftTimeFraction` is `internal`. If the test project already has `InternalsVisibleTo` for it (check `TimePlanning.Pn.csproj` / any `AssemblyInfo`), test it directly; otherwise add `[assembly: InternalsVisibleTo("TimePlanning.Pn.Test")]` to the plugin (e.g. in `EformTimePlanningPlugin.cs` top or a new `AssemblyInfo.cs`). Test:

```csharp
[Test]
public void GetShiftTimeFraction_FiveMinuteIndex_Returns_TimeOfDayFraction()
{
    var service = /* the same _service instance the fixture builds */;
    // index 97 => (97-1)*5 = 480 min = 08:00 => 480/1440 = 0.3333...
    Assert.That(service.GetShiftTimeFraction(97, null, false), Is.EqualTo(480.0 / 1440.0).Within(1e-9));
    Assert.That(service.GetShiftTimeFraction(null, null, false), Is.Null);
    Assert.That(service.GetShiftTimeFraction(0, null, false), Is.Null);
    Assert.That(service.GetShiftTimeFraction(289, null, false), Is.EqualTo(1.0));
    // one-minute stamp path: 08:04 => (8*60+4)/1440
    Assert.That(service.GetShiftTimeFraction(0, new DateTime(2026, 5, 15, 8, 4, 0), true),
        Is.EqualTo((8 * 60 + 4) / 1440.0).Within(1e-9));
}
```

Run: `cd /home/rene/Documents/workspace/microting/eform-angular-frontend/eFormAPI && dotnet test Plugins/TimePlanning.Pn/TimePlanning.Pn.Test/TimePlanning.Pn.Test.csproj --filter GetShiftTimeFraction`
Expected: PASS.

- [ ] **Step 2: Single-worker — Dagsoversigt is the first sheet with the right header**

```csharp
[Test]
public async Task SingleWorker_FirstSheet_IsDagsoversigt_WithFixedHeader()
{
    await SeedSiteAndPlanRegistration(
        siteUid: 9801, date: new DateTime(2026, 5, 14),
        useOneMinuteIntervals: false, start1: null, stop1: null, start1Id: 85, stop1Id: 181);

    var result = await _service.GenerateExcelDashboard(new TimePlanningWorkingHoursRequestModel
    {
        SiteId = 9801, DateFrom = new DateTime(2026, 5, 14), DateTo = new DateTime(2026, 5, 14),
    });

    Assert.That(result.Success, Is.True, result.Message);
    using var doc = SpreadsheetDocument.Open(result.Model!, false);
    var sheets = doc.WorkbookPart!.Workbook.Sheets!.Elements<Sheet>().ToList();
    Assert.That(sheets.First().Name!.Value, Is.EqualTo("Dagsoversigt").Or.EqualTo("DayOverview"),
        "first tab must be the localized Day-overview sheet");

    // Resolve the first sheet's worksheet part and read header row (positionally).
    var firstPart = (WorksheetPart)doc.WorkbookPart.GetPartById(sheets.First().Id!);
    var headerCells = firstPart.Worksheet.Descendants<Row>().First().Elements<Cell>().ToList();
    Assert.That(headerCells.Count, Is.EqualTo(21));
    // Header values are localized (key == value under the test fixture localizer is bypassed —
    // these come from Translations.X). Assert positions A and U map to the expected keys' da values.
    // (Adjust expected strings to match the test culture; under da: "Medarbejder nr." and "Timer netto".)
}
```

> When writing, set the seeded user's language so `CurrentUICulture` is deterministic, and assert
> the concrete header strings for that culture (e.g. da → A1 "Medarbejder nr.", U1 "Timer netto").
> Verify a `TableDefinitionPart` exists: `Assert.That(firstPart.TableDefinitionParts.Count(), Is.EqualTo(1));`
> and its `Table.Reference` equals `"A1:U2"` for one data row.

Run the single-worker test; Expected: PASS.

- [ ] **Step 3: Single-worker — date/time/netto cells carry the new styles**

Read a data-row cell and assert: Date cell `StyleIndex == 5` & `DataType == Number`; a populated
shift cell `StyleIndex == 3`; the netto cell `StyleIndex == 4`. Assert the shift cell's `CellValue`
equals the expected OADate fraction for the seeded index.

Run; Expected: PASS.

- [ ] **Step 4: All-workers — first sheet is Dagsoversigt, rows combined & sorted**

Seed two sites with overlapping dates (e.g. site 9802 emp "1" and site 9803 emp "2", each with a
day on 2026-05-14 and 2026-05-15). Call `GenerateExcelDashboard(new TimePlanningWorkingHoursReportForAllWorkersRequestModel { DateFrom, DateTo })`. Assert:
- first sheet is the Day-overview sheet, then "Total", then the two site sheets (4 sheets total);
- the Dagsoversigt data rows are ordered by date then employee no.: (14th, emp1), (14th, emp2), (15th, emp1), (15th, emp2) — assert the EmployeeNo column (A) and Date column (D) sequence.

Run; Expected: PASS.

- [ ] **Step 5: Run the full TimePlanning test project**

Run: `cd /home/rene/Documents/workspace/microting/eform-angular-frontend/eFormAPI && dotnet test Plugins/TimePlanning.Pn/TimePlanning.Pn.Test/TimePlanning.Pn.Test.csproj`
Expected: All tests pass (the existing export tests must still pass — existing sheets/styles are unchanged; the single-worker file now has Dagsoversigt as the first sheet, so any existing test that assumed the first sheet was "Dashboard" must be updated to select the Dashboard sheet by name. Check `WorkingHoursExcelExportE2ETests` `ReadShift1Cells`, which does `WorksheetParts.First()` — update it to select the Dashboard sheet by its `Sheet.Id` rather than `.First()`).

> Important regression note: `WorkingHoursExcelExportE2ETests.ReadShift1Cells` (line ~226) uses
> `WorkbookPart.WorksheetParts.First().Worksheet`. After this change the first worksheet is
> Dagsoversigt, not Dashboard. Update that helper to resolve the "Dashboard" sheet by name/id so the
> existing assertions keep targeting the Dashboard sheet.

---

## Task 7: Verification, code review, and sync back (normal cycle)

- [ ] **Step 1: Full build + test pass (evidence)**

Run: `cd /home/rene/Documents/workspace/microting/eform-angular-frontend/eFormAPI && dotnet build Plugins/TimePlanning.Pn/TimePlanning.Pn/TimePlanning.Pn.csproj && dotnet test Plugins/TimePlanning.Pn/TimePlanning.Pn.Test/TimePlanning.Pn.Test.csproj`
Expected: build succeeded; all tests pass. Capture the summary line.

- [ ] **Step 2: Manual browser check**

Remind the user to trigger both report downloads in the running app and open the files: confirm
Dagsoversigt is the first tab, banded table with autofilter, `hh:mm` times, `dd/mm/yyyy` dates,
`0.00` net hours, combined+sorted rows in the all-workers file, and that the existing
Dashboard/Total/per-site sheets are unchanged.

- [ ] **Step 3: Code review**

Use `superpowers:requesting-code-review` on the diff (service + OpenXMLHelper + resx + Designer + tests).

- [ ] **Step 4: Sync to source repo**

From the source plugin repo `/home/rene/Documents/workspace/microting/eform-angular-timeplanning-plugin`, run `./devgetchanges.sh`, then `git checkout *.csproj *.conf.ts *.xlsx *.docx` to discard build/config artifacts, then `git status` and verify only the intended files changed (service, OpenXMLHelper, Resources/*.resx, Translations.Designer.cs, the new test file). `git checkout` any unintended files.

- [ ] **Step 5: Commit (only after user confirms the final `git status`)**

Stage specific files by name (never `git add .`) on the `stable` branch and commit. End the commit message with the required `Co-Authored-By` trailer.

---

## Self-Review notes

- **Spec coverage:** sheet in both exports as first tab (Tasks 4,5) ✓; combined all-workers sheet sorted by date then employee (Task 5 Step 3) ✓; always-all-5-shifts fixed 21 columns (Task 3 builder) ✓; rows = planning days from `Index()`/`perSiteCache` (Tasks 4,5) ✓; NettoHours reused override-aware (Task 3 builder) ✓; match-example formatting via hh:mm/dd-mm-yyyy/0.00 styles + banded Table (Tasks 2,3) ✓; one new translation key `DayOverview` in all 26 resx + accessor, all 25 languages filled (Task 1) ✓; existing header keys reused (Task 3 builder) ✓.
- **Known minor edge:** a shift ending exactly at 24:00 (index 289) stores fraction 1.0, which renders as `00:00` under `hh:mm`. Documented; acceptable given rarity.
- **Regression guard:** existing `ReadShift1Cells` `.First()` worksheet assumption must be updated to select Dashboard by name (Task 6 Step 5).
- **Type consistency:** `BuildDayOverviewWorksheet(WorksheetPart, List<DayOverviewRow>, CultureInfo)`, `DayOverviewRow` fields, `GetShiftTimeFraction(int?, DateTime?, bool)`, and the cell helpers are used consistently across Tasks 3-6.
