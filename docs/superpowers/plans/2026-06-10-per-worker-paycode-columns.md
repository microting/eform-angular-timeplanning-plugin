# Per-Worker Pay-Code Columns Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** In the all-workers Excel export, make each per-site (per-worker) sheet show only the pay-code columns declared in that worker's own pay-rule-set (none if the worker has no rule-set), leaving the Total summary sheet and the single-worker export unchanged.

**Architecture:** Add a pure `GetDeclaredPayCodes(PayRuleSet)` helper that returns a rule-set's declared pay codes. In the all-workers per-site loop, compute `sitePayCodes` from the cached per-site `PayRuleSet` and use it (instead of the global `allPayCodes`) for the per-site sheet's pay-code header, per-day cells, and totals row. The global `allPayCodes` and `siteTotalsByPayCode` stay as-is so the Total sheet is byte-identical.

**Tech Stack:** C# / .NET 10, DocumentFormat.OpenXml, NUnit 4 + Testcontainers.MariaDb. Base dev mode: edit in host-app mirror `eform-angular-frontend/eFormAPI/Plugins/TimePlanning.Pn/`, then `devgetchanges.sh` to the source repo.

**Spec:** `docs/superpowers/specs/2026-06-10-per-worker-paycode-columns-design.md`

**Key file:** `…/TimePlanning.Pn/TimePlanning.Pn/Services/TimePlanningWorkingHoursService/TimePlanningWorkingHoursService.cs`

**Verified facts:**
- All-workers per-site loop emits pay codes at 3 points, all using global `allPayCodes`:
  header (~line 3194), per-day cells (~3267), per-site totals row (~3346).
- `siteTotalsByPayCode = allPayCodes.ToDictionary(p => p, p => 0.0)` (~3252) is accumulated
  (~3287–3290, guarded by `ContainsKey`) and consumed by BOTH the per-site totals row AND the
  Total-sheet row (~3403). It must stay keyed by `allPayCodes` for the Total sheet.
- The per-site pay-code header (~3194) is built BEFORE `perSiteCache.TryGetValue(... out var cache)`
  (~3258). So `sitePayCodes` must be computed before the header loop (use a separate lookup var to
  avoid touching the existing `cache` declaration).
- `AutoFilter` uses `GetColumnLetter(headerStrings.Count)` (~3353) — auto-adjusts to the per-site
  column count once the header uses `sitePayCodes`. No change needed there.
- `PayRuleSet` entity (eform-timeplanning-base): `DayRules: ICollection<PayDayRule>`,
  `DayTypeRules: ICollection<PayDayTypeRule>`, `HolidayPaidOffPayCode: string`.
  `PayDayRule.Tiers: ICollection<PayTierRule>`; `PayTierRule.PayCode: string`, `.Order: int`.
  `PayDayTypeRule.DefaultPayCode: string`, `.TimeBandRules: ICollection<PayTimeBandRule>`;
  `PayTimeBandRule.PayCode: string`.
- `CalculatePayLinesForDay` is `internal static` (~line 4168); `MergeByPayCode` static (~3922).
  `InternalsVisibleTo("TimePlanning.Pn.Test")` is configured, so `internal static` helpers are
  unit-testable directly without a service instance.
- `CalculatePayLinesForDayTests.cs` is NOT in any CI shard — do NOT add CI-critical tests there.
  `PlanRegistrationHelperTests.cs` is in shard **f** and already hosts static-helper tests
  (GetShiftTime). `DagsoversigtWorksheetExportTests.cs` is in shard **g** and exercises the
  all-workers export.

---

## Task 1: Add `GetDeclaredPayCodes` (TDD)

**Files:**
- Modify: `…/TimePlanningWorkingHoursService.cs` (add static helper near `MergeByPayCode`, ~line 3922)
- Test: `…/TimePlanning.Pn.Test/PlanRegistrationHelperTests.cs`

- [ ] **Step 1: Write the failing unit tests**

Add to `PlanRegistrationHelperTests.cs` (inside the top-level `[TestFixture]`). The helper is
`internal static`, so call it directly on the type — no instance/DB needed. Match the file's
existing NUnit 4 style (`using` for the entities may be needed — `Microting.TimePlanningBase.Infrastructure.Data.Entities`).

```csharp
[Test]
public void GetDeclaredPayCodes_CollectsFromAllSources_DedupsAndSkipsEmpty_OrdersTiersByOrder()
{
    var ruleSet = new PayRuleSet
    {
        DayRules = new List<PayDayRule>
        {
            new PayDayRule { Tiers = new List<PayTierRule>
            {
                new PayTierRule { PayCode = "B", Order = 2 },
                new PayTierRule { PayCode = "A", Order = 1 },
            }},
            new PayDayRule { Tiers = new List<PayTierRule>
            {
                new PayTierRule { PayCode = "A", Order = 1 }, // duplicate
            }},
        },
        DayTypeRules = new List<PayDayTypeRule>
        {
            new PayDayTypeRule { DefaultPayCode = "C", TimeBandRules = new List<PayTimeBandRule>
            {
                new PayTimeBandRule { PayCode = "D" },
                new PayTimeBandRule { PayCode = "" },   // skipped
            }},
            new PayDayTypeRule { DefaultPayCode = null, TimeBandRules = new List<PayTimeBandRule>() }, // skipped
        },
        HolidayPaidOffPayCode = "E",
    };

    var codes = TimePlanningWorkingHoursService.GetDeclaredPayCodes(ruleSet);

    // tiers ordered by Order (A before B) within the first day rule, then C, D, then holiday E;
    // duplicate A and empty/null skipped.
    Assert.That(codes, Is.EqualTo(new List<string> { "A", "B", "C", "D", "E" }));
}

[Test]
public void GetDeclaredPayCodes_NullRuleSet_ReturnsEmpty()
{
    Assert.That(TimePlanningWorkingHoursService.GetDeclaredPayCodes(null), Is.Empty);
}

[Test]
public void GetDeclaredPayCodes_EmptyRuleSet_ReturnsEmpty()
{
    Assert.That(TimePlanningWorkingHoursService.GetDeclaredPayCodes(new PayRuleSet()), Is.Empty);
}
```

- [ ] **Step 2: Run the tests to verify they FAIL**

Run: `cd /home/rene/Documents/workspace/microting/eform-angular-frontend/eFormAPI && dotnet test Plugins/TimePlanning.Pn/TimePlanning.Pn.Test/TimePlanning.Pn.Test.csproj --filter GetDeclaredPayCodes`
Expected: FAIL — `GetDeclaredPayCodes` does not exist (compile error). (These tests are pure/in-memory; if the fixture needs a DB container that can't start here, report DONE_WITH_CONCERNS — CI shard f runs them.)

- [ ] **Step 3: Implement `GetDeclaredPayCodes`**

Add near `MergeByPayCode` (~line 3922) in `TimePlanningWorkingHoursService.cs`:

```csharp
    /// <summary>
    /// Returns the pay codes DECLARED by a pay-rule-set, in structural order
    /// (day-rule tiers by Order, then day-type default codes and their time-band codes,
    /// then the holiday code), de-duplicated (first-seen wins), skipping null/empty codes.
    /// Returns an empty list when payRuleSet is null. Used to scope per-worker pay-code
    /// columns in the all-workers export to each worker's own rule-set.
    /// </summary>
    internal static List<string> GetDeclaredPayCodes(PayRuleSet payRuleSet)
    {
        var codes = new List<string>();
        if (payRuleSet == null)
        {
            return codes;
        }

        void Add(string code)
        {
            if (!string.IsNullOrWhiteSpace(code) && !codes.Contains(code))
            {
                codes.Add(code);
            }
        }

        if (payRuleSet.DayRules != null)
        {
            foreach (var dayRule in payRuleSet.DayRules)
            {
                if (dayRule.Tiers == null) continue;
                foreach (var tier in dayRule.Tiers.OrderBy(t => t.Order))
                {
                    Add(tier.PayCode);
                }
            }
        }

        if (payRuleSet.DayTypeRules != null)
        {
            foreach (var dayTypeRule in payRuleSet.DayTypeRules)
            {
                Add(dayTypeRule.DefaultPayCode);
                if (dayTypeRule.TimeBandRules == null) continue;
                foreach (var timeBand in dayTypeRule.TimeBandRules)
                {
                    Add(timeBand.PayCode);
                }
            }
        }

        Add(payRuleSet.HolidayPaidOffPayCode);

        return codes;
    }
```

(`System.Linq` and the `PayRuleSet` type are already in scope in this file.)

- [ ] **Step 4: Run the tests to verify they PASS**

Run: `cd /home/rene/Documents/workspace/microting/eform-angular-frontend/eFormAPI && dotnet test Plugins/TimePlanning.Pn/TimePlanning.Pn.Test/TimePlanning.Pn.Test.csproj --filter GetDeclaredPayCodes`
Expected: all 3 pass.

- [ ] **Step 5: Do not commit** (base dev mode — commit once at the end, Task 4).

---

## Task 2: Use per-site codes in the all-workers per-site sheet

**Files:**
- Modify: `…/TimePlanningWorkingHoursService.cs` — all-workers `GenerateExcelDashboard(TimePlanningWorkingHoursReportForAllWorkersRequestModel)` per-site loop.

Read the per-site loop first to anchor on exact current text (the three `foreach (var payCode in allPayCodes)` blocks and the header). Make these four edits. Do NOT touch the global `allPayCodes` pre-pass, `siteTotalsByPayCode`'s seed/accumulation, the Total-sheet header/row, or the single-worker export.

- [ ] **Step 1: Compute `sitePayCodes` before the per-site pay-code header**

Find the per-site header pay-code loop:
```csharp
                    // Append one column header per pay code discovered across all sites
                    foreach (var payCode in allPayCodes)
                    {
                        headerStrings.Add(payCode);
                    }
```
Replace it with (compute `sitePayCodes` from the cached per-site rule-set, then use it):
```csharp
                    // Per-worker pay-code columns: only the codes declared in THIS site's
                    // pay-rule-set (empty when the site has no rule-set). The global allPayCodes
                    // is still used for the Total sheet, which is intentionally unchanged.
                    perSiteCache.TryGetValue(siteIds[i], out var siteCacheForCodes);
                    var sitePayCodes = GetDeclaredPayCodes(siteCacheForCodes?.PayRuleSet);
                    foreach (var payCode in sitePayCodes)
                    {
                        headerStrings.Add(payCode);
                    }
```
(`siteCacheForCodes` is a new local distinct from the existing `cache` declared later — no conflict.)

- [ ] **Step 2: Per-day pay-code cells use `sitePayCodes`**

Find:
```csharp
                            foreach (var payCode in allPayCodes)
                            {
                                var payLine = dayPayLines.FirstOrDefault(pl => pl.PayCode == payCode);
                                dataRow.Append(CreateNumericCell(payLine?.Hours ?? 0));
                            }
```
Change the loop source to `sitePayCodes`:
```csharp
                            foreach (var payCode in sitePayCodes)
                            {
                                var payLine = dayPayLines.FirstOrDefault(pl => pl.PayCode == payCode);
                                dataRow.Append(CreateNumericCell(payLine?.Hours ?? 0));
                            }
```
Leave the `siteTotalsByPayCode` accumulation block (the `foreach (var pl in dayPayLines) { if (siteTotalsByPayCode.ContainsKey(pl.PayCode)) ... }`) UNCHANGED — it feeds the Total sheet.

- [ ] **Step 3: Per-site totals row uses `sitePayCodes` (safe lookup)**

Find:
```csharp
                    foreach (var payCode in allPayCodes)
                    {
                        siteTotalsRow.Append(CreateNumericCell(siteTotalsByPayCode[payCode]));
                    }
```
Change to iterate `sitePayCodes` and read totals safely (a declared code with 0 hours everywhere is not a key in `siteTotalsByPayCode`):
```csharp
                    foreach (var payCode in sitePayCodes)
                    {
                        siteTotalsRow.Append(CreateNumericCell(siteTotalsByPayCode.GetValueOrDefault(payCode, 0)));
                    }
```

- [ ] **Step 4: Confirm the column counts stay aligned & build**

The per-site header, each data row, and the totals row now all iterate `sitePayCodes` → equal pay-code cell counts. `AutoFilter` uses `headerStrings.Count` so it auto-adjusts. Build:

Run: `cd /home/rene/Documents/workspace/microting/eform-angular-frontend/eFormAPI && dotnet build Plugins/TimePlanning.Pn/TimePlanning.Pn/TimePlanning.Pn.csproj`
Expected: Build succeeded.

- [ ] **Step 5: Do not commit.**

---

## Task 3: Export regression/coverage test (different rule-sets per worker)

**Files:**
- Modify: `…/TimePlanning.Pn.Test/DagsoversigtWorksheetExportTests.cs` (shard g; exercises the all-workers export)

Read the file's `SeedSiteAndPlanRegistration`/seed helpers and `TestBaseSetup` first to learn how sites, assigned sites, and (if present) pay-rule-sets are created. Look at `PayRuleSetServiceTests.cs` for how a `PayRuleSet` with `DayRules`/`Tiers` is constructed/persisted, and reuse that pattern to seed rule-sets in the plugin DbContext.

- [ ] **Step 1: Write the test**

Add `[Test] AllWorkers_PerSiteSheets_UseEachWorkersOwnPayRuleSetCodes` that:
- Seeds **site A** with an `AssignedSite.PayRuleSetId` → a `PayRuleSet` whose declared codes are e.g. `{ "AAA" }` (one `PayDayRule` with one `PayTierRule { PayCode = "AAA" }`), plus a PlanRegistration in the period.
- Seeds **site B** with a different `PayRuleSet` declaring `{ "BBB" }`, plus a PlanRegistration.
- Seeds **site C** with NO `PayRuleSetId`, plus a PlanRegistration.
- Calls the all-workers export `GenerateExcelDashboard(new TimePlanningWorkingHoursReportForAllWorkersRequestModel { DateFrom, DateTo })`.
- Opens the produced xlsx and, for each per-site sheet (named `site.Name`, i.e. "Site A"/"Site B"/"Site C"), reads the header row and asserts:
  - site A's sheet header contains `"AAA"` and NOT `"BBB"`.
  - site B's sheet header contains `"BBB"` and NOT `"AAA"`.
  - site C's sheet header contains neither `"AAA"` nor `"BBB"` (no pay-code columns).
  - the **Total** sheet header still contains BOTH `"AAA"` and `"BBB"` (union unchanged).

Read sheet headers by resolving each `Sheet` by name via the workbook `Sheets` and `GetPartById`, then reading row 1 cells (reuse/extend the file's existing header-reading helper; pay-code headers are plain string cells appended after the fixed columns). Dispose each returned stream as the existing tests do (the export returns an open FileStream; the file-collision pattern from the prior fix applies if multiple exports run in the same second — call the all-workers export once here).

- [ ] **Step 2: Build + run**

Run: `cd /home/rene/Documents/workspace/microting/eform-angular-frontend/eFormAPI && dotnet test Plugins/TimePlanning.Pn/TimePlanning.Pn.Test/TimePlanning.Pn.Test.csproj --filter AllWorkers_PerSiteSheets_UseEachWorkersOwnPayRuleSetCodes`
Expected: PASS (with Task 2 applied). Needs Docker/Testcontainers; if unavailable here, report DONE_WITH_CONCERNS with the test written — CI shard g runs it.

- [ ] **Step 3: Do not commit.**

> If seeding a full `PayRuleSet` graph in the fixture proves disproportionately complex (e.g. the
> seed helpers don't support assigned-site → rule-set linkage and constructing it inline is
> heavy), STOP and report NEEDS_CONTEXT with what you found, rather than writing a test that
> doesn't actually exercise per-site codes. The Task 1 unit test + the manual verification in
> Task 4 still cover correctness; this integration test is the ideal but not worth a brittle hack.

---

## Task 4: Verify, code-review, sync, PR, CI

- [ ] **Step 1: Full build + targeted tests**

Run: `cd /home/rene/Documents/workspace/microting/eform-angular-frontend/eFormAPI && dotnet build Plugins/TimePlanning.Pn/TimePlanning.Pn/TimePlanning.Pn.csproj && dotnet test Plugins/TimePlanning.Pn/TimePlanning.Pn.Test/TimePlanning.Pn.Test.csproj --filter "GetDeclaredPayCodes|AllWorkers_PerSiteSheets_UseEachWorkersOwnPayRuleSetCodes"`
Expected: build succeeds; the new tests pass.

- [ ] **Step 2: Code review** — use `superpowers:requesting-code-review` on the diff (service helper + per-site wiring + tests). Confirm the Total sheet and single-worker export are untouched.

- [ ] **Step 3: Sync to source repo**

From `/home/rene/Documents/workspace/microting/eform-angular-timeplanning-plugin`: create branch `fix/per-worker-paycode-columns` off `stable`, run `./devgetchanges.sh`, then `git checkout -- '*.csproj' '*.conf.ts' '*.xlsx' '*.docx'`, then `git status`. Confirm only intended files changed: `…/TimePlanningWorkingHoursService.cs`, `…/TimePlanning.Pn.Test/PlanRegistrationHelperTests.cs`, `…/TimePlanning.Pn.Test/DagsoversigtWorksheetExportTests.cs`, plus the spec/plan docs. `git checkout` anything unintended.

- [ ] **Step 4: Commit + push + PR + watch CI**

Stage only the intended files by name, commit (end message with `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`), push, open a PR toward `stable`, and watch CI green. The unit test runs in shard **f** (`PlanRegistrationHelperTests`) and the export test in shard **g** (`DagsoversigtWorksheetExportTests`) — both already in the matrix, so no workflow change is needed. Treat any known-flaky playwright shard failures as flaky (re-run); fix genuine failures.

---

## Self-Review

- **Spec coverage:** per-site sheets use declared per-site codes (Task 2 Steps 1–3) ✓; declared-code helper from all four sources, dedup, order, null→empty (Task 1) ✓; no rule-set → no columns (`GetDeclaredPayCodes(null)`→empty drives empty header — Task 1 + Task 2 Step 1) ✓; Total sheet unchanged (`allPayCodes`/`siteTotalsByPayCode` untouched; per-site totals row reads via `GetValueOrDefault` — Task 2 Steps 2–3) ✓; single-worker untouched ✓; tests (unit + export E2E) ✓.
- **Placeholder scan:** none — concrete code/commands throughout. The Task 3 fallback is an explicit escalation condition, not a vague placeholder.
- **Type/name consistency:** `GetDeclaredPayCodes(PayRuleSet) : List<string>` (internal static) defined in Task 1, called identically in Task 2 Step 1 and the Task 1 tests; `sitePayCodes`/`siteCacheForCodes` locals introduced in Task 2 Step 1 and reused in Steps 2–3; `siteTotalsByPayCode.GetValueOrDefault(payCode, 0)` matches the existing `Dictionary<string,double>` type.
- **Total-sheet safety:** the only read of `siteTotalsByPayCode` that changes is the per-site totals row (now `sitePayCodes` + `GetValueOrDefault`); the Total-sheet row (`foreach allPayCodes … siteTotalsByPayCode[payCode]`) and the seed/accumulation are untouched, so the Total sheet stays byte-identical.
