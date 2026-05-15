# Test Coverage for Non-5-Min Interval Calculations — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:executing-plans` to implement this plan task-by-task in this session, with checkpoints for review. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add comprehensive test coverage for every calculation and Excel-export path that consumes `Start{N}StartedAt`/`Stop{N}StoppedAt` precise stamps with **non-5-min-divisible** minute components (e.g. 08:04→10:10 = 2h06m), closing all gaps identified by the audit dated 2026-05-15.

**Architecture:** Add `[assembly: InternalsVisibleTo("TimePlanning.Pn.Test")]` so the existing private pay-line helpers become unit-testable. Add unit tests under `TimePlanning.Pn.Test/` for the pure helpers; add integration tests inheriting `TestBaseSetup` (Testcontainers MariaDB) for gRPC and admin write paths and one end-to-end Excel export assertion. Lock today's user-reported example (`08:04→10:10 = 7560s`) as a regression test.

**Tech Stack:** NUnit, C# / .NET 10, Testcontainers MariaDB (already wired in `TestBaseSetup`), DocumentFormat.OpenXml (already used for export), `Microting.eForm` SDK (already wired).

**Dev mode:** ALL edits land in the host app at `/home/rene/Documents/workspace/microting/eform-angular-frontend/eFormAPI/Plugins/TimePlanning.Pn/...`. After all tests pass, run `devgetchanges.sh` from the source plugin repo and commit there.

---

## File Structure

**Modify (host app):**
- `eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn/TimePlanning.Pn.csproj` — add `InternalsVisibleTo` ItemGroup
- `eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn/Services/TimePlanningWorkingHoursService/TimePlanningWorkingHoursService.cs` — change `private static` → `internal static` on `CalculatePayLinesForDay`, `EnumerateShiftSegments`, `ResolveShiftSeconds`, `GetDayCodeForDate`, `TryGetDayType`; change `private` → `internal` on the 4-arg `GetShiftTime` overload (and 2-arg sibling needed to construct return tuples)

**Create (host app, under `TimePlanning.Pn.Test/`):**
- `ResolveShiftSecondsTests.cs` — pure-function tests for the seconds-of-day resolver
- `EnumerateShiftSegmentsTests.cs` — multi-shift enumeration with non-5-min stamps + null cases
- `CalculatePayLinesForDayTests.cs` — both tier and time-band branches with non-5-min inputs
- `WorkingHoursGrpcKioskNonRoundMinutesTests.cs` — integration test for kiosk create/update with 08:04 stamps
- `PlanningServiceAdminEditNonRoundMinutesTests.cs` — integration test for admin web edit with 08:04 stamps
- `WorkingHoursExcelExportE2ETests.cs` — end-to-end export → open xlsx → assert cell values

**Modify (existing test file):**
- `PlanRegistrationHelperTests.cs` — un-ignore + live-ify `GetShiftTime_FlagOnWithActualStamp_ReturnsHHmm`; add `ComputeNettoSecondsFromDateTimeShifts_Exact_08_04_to_10_10_Returns7560`; add `RecalculatePlanHoursFromShifts_NonRoundDateTimeStamps_RoundsConsistently` (covers the `PARTIAL` finding)

**Out of scope:**
- The other five Phase 0/1/2 `[Ignore]`'d carve-outs that depend on heavier fixture work (ReadSimple SDK fixture, UpdatePlanRegistrationsInPeriod end-to-end, Index_FlagOn_DerivedFieldsConsistent). They remain ignored. This plan only un-ignores the Phase 4 GetShiftTime carve-out since `InternalsVisibleTo` resolves its blocker.

---

## Task 1: Wire `InternalsVisibleTo` and broaden private static helpers

**Files:**
- Modify: `eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn/TimePlanning.Pn.csproj`
- Modify: `eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn/Services/TimePlanningWorkingHoursService/TimePlanningWorkingHoursService.cs`

- [ ] **Step 1.1: Add `InternalsVisibleTo` ItemGroup to csproj**

Add inside `<Project>` after the existing top `<PropertyGroup>` blocks:

```xml
    <ItemGroup>
        <InternalsVisibleTo Include="TimePlanning.Pn.Test" />
    </ItemGroup>
```

- [ ] **Step 1.2: Change visibility of four private static helpers**

In `TimePlanningWorkingHoursService.cs`, change these signatures from `private static` to `internal static`:
- `CalculatePayLinesForDay` (line ~3886)
- `EnumerateShiftSegments` (line ~3803)
- `ResolveShiftSeconds` (line ~3827)
- `GetDayCodeForDate` (line ~3733)
- `TryGetDayType` (line ~3765)

Also change the 4-arg `GetShiftTime` overload (line ~2790) from `private` to `internal` and the 2-arg overload (line ~2772) from `private` to `internal`.

- [ ] **Step 1.3: Build to confirm no public-API regressions**

```bash
cd /home/rene/Documents/workspace/microting/eform-angular-frontend/eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn && dotnet build --nologo 2>&1 | tail -5
```
Expected: `0 Error(s)` (warnings unchanged from prior session: 31 pre-existing).

---

## Task 2: Unit tests for `ResolveShiftSeconds`

**Files:**
- Create: `eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn.Test/ResolveShiftSecondsTests.cs`

- [ ] **Step 2.1: Write the test file**

```csharp
using System;
using NUnit.Framework;
using TimePlanning.Pn.Services.TimePlanningWorkingHoursService;

namespace TimePlanning.Pn.Test;

[TestFixture]
public class ResolveShiftSecondsTests
{
    private static (int Start, int Stop)? Resolve(DateTime? start, DateTime? stop) =>
        TimePlanningWorkingHoursService.ResolveShiftSeconds(start, stop);

    [Test]
    public void NonRoundMinutes_08_04_To_10_10_ReturnsExactSecondsOfDay()
    {
        // 08:04:00 → 29040, 10:10:00 → 36600. Worked = 7560 s = 2h06m.
        var date = new DateTime(2026, 5, 15);
        var res = Resolve(date.AddHours(8).AddMinutes(4), date.AddHours(10).AddMinutes(10));
        Assert.That(res, Is.Not.Null);
        Assert.That(res!.Value.Start, Is.EqualTo(29040));
        Assert.That(res.Value.Stop, Is.EqualTo(36600));
        Assert.That(res.Value.Stop - res.Value.Start, Is.EqualTo(7560));
    }

    [Test]
    public void NullStart_ReturnsNull()
    {
        Assert.That(Resolve(null, new DateTime(2026, 5, 15, 10, 0, 0)), Is.Null);
    }

    [Test]
    public void NullStop_ReturnsNull()
    {
        Assert.That(Resolve(new DateTime(2026, 5, 15, 8, 0, 0), null), Is.Null);
    }

    [Test]
    public void StopEqualsStart_ReturnsNull()
    {
        var t = new DateTime(2026, 5, 15, 8, 4, 0);
        Assert.That(Resolve(t, t), Is.Null);
    }

    [Test]
    public void StopBeforeStart_ReturnsNull()
    {
        var s = new DateTime(2026, 5, 15, 10, 10, 0);
        var t = new DateTime(2026, 5, 15, 8, 4, 0);
        Assert.That(Resolve(s, t), Is.Null);
    }

    [Test]
    public void StopCrossesMidnight_ClampedTo86400()
    {
        // Start day-1 22:13, stop day-2 06:47 → stop clamped to 86400 (24:00).
        var start = new DateTime(2026, 5, 15, 22, 13, 0);
        var stop  = new DateTime(2026, 5, 16,  6, 47, 0);
        var res = Resolve(start, stop);
        Assert.That(res, Is.Not.Null);
        Assert.That(res!.Value.Start, Is.EqualTo(22 * 3600 + 13 * 60));
        Assert.That(res.Value.Stop, Is.EqualTo(86400));
    }

    [Test]
    public void SubSecondStamps_TruncatedToSeconds()
    {
        // 08:04:00.500 → integer seconds-of-day = 29040 (truncation, not rounding).
        var start = new DateTime(2026, 5, 15, 8, 4, 0).AddMilliseconds(500);
        var stop = new DateTime(2026, 5, 15, 10, 10, 30);
        var res = Resolve(start, stop);
        Assert.That(res!.Value.Stop - res.Value.Start, Is.EqualTo(7590)); // 10:10:30 - 08:04:00 = 2h06m30s
    }
}
```

- [ ] **Step 2.2: Run the test class**

```bash
cd /home/rene/Documents/workspace/microting/eform-angular-frontend/eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn.Test && dotnet test --filter "FullyQualifiedName~ResolveShiftSecondsTests" --nologo 2>&1 | tail -10
```
Expected: 6 passed, 0 failed.

---

## Task 3: Unit tests for `EnumerateShiftSegments`

**Files:**
- Create: `eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn.Test/EnumerateShiftSegmentsTests.cs`

- [ ] **Step 3.1: Write the test file**

```csharp
using System;
using System.Linq;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Models.WorkingHours.Index;
using TimePlanning.Pn.Services.TimePlanningWorkingHoursService;

namespace TimePlanning.Pn.Test;

[TestFixture]
public class EnumerateShiftSegmentsTests
{
    private static DateTime D(int hour, int min) => new DateTime(2026, 5, 15, hour, min, 0);

    [Test]
    public void TwoNonRoundShifts_BothYielded()
    {
        var model = new TimePlanningWorkingHoursModel
        {
            Date = new DateTime(2026, 5, 15),
            Start1StartedAt = D(8, 4),
            Stop1StoppedAt = D(10, 10),
            Start2StartedAt = D(13, 7),
            Stop2StoppedAt = D(17, 21),
        };

        var segs = TimePlanningWorkingHoursService.EnumerateShiftSegments(model).ToList();
        Assert.That(segs, Has.Count.EqualTo(2));
        Assert.That(segs[0].Stop - segs[0].Start, Is.EqualTo(7560));   // 2h06m
        Assert.That(segs[1].Stop - segs[1].Start, Is.EqualTo(15240));  // 4h14m
    }

    [Test]
    public void AllStampsNull_NoSegmentsYielded()
    {
        var model = new TimePlanningWorkingHoursModel { Date = new DateTime(2026, 5, 15) };
        var segs = TimePlanningWorkingHoursService.EnumerateShiftSegments(model).ToList();
        Assert.That(segs, Is.Empty);
    }

    [Test]
    public void Shift3OnlyPopulated_OnlyShift3Yielded()
    {
        var model = new TimePlanningWorkingHoursModel
        {
            Date = new DateTime(2026, 5, 15),
            Start3StartedAt = D(22, 13),
            Stop3StoppedAt = D(23, 47),
        };
        var segs = TimePlanningWorkingHoursService.EnumerateShiftSegments(model).ToList();
        Assert.That(segs, Has.Count.EqualTo(1));
        Assert.That(segs[0].Stop - segs[0].Start, Is.EqualTo(5640)); // 1h34m
    }

    [Test]
    public void Shift1MissingStop_SkippedAsIncomplete()
    {
        var model = new TimePlanningWorkingHoursModel
        {
            Date = new DateTime(2026, 5, 15),
            Start1StartedAt = D(8, 4),
            Stop1StoppedAt = null, // never clocked out
            Start2StartedAt = D(13, 7),
            Stop2StoppedAt = D(17, 21),
        };
        var segs = TimePlanningWorkingHoursService.EnumerateShiftSegments(model).ToList();
        Assert.That(segs, Has.Count.EqualTo(1));
        Assert.That(segs[0].Stop - segs[0].Start, Is.EqualTo(15240));
    }
}
```

- [ ] **Step 3.2: Run**

```bash
cd /home/rene/Documents/workspace/microting/eform-angular-frontend/eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn.Test && dotnet test --filter "FullyQualifiedName~EnumerateShiftSegmentsTests" --nologo 2>&1 | tail -10
```
Expected: 4 passed, 0 failed.

---

## Task 4: Unit tests for `CalculatePayLinesForDay` (tier + time-band)

**Files:**
- Create: `eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn.Test/CalculatePayLinesForDayTests.cs`

- [ ] **Step 4.1: Write the test file — tier-path test**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using TimePlanning.Pn.Infrastructure.Models.WorkingHours.Index;
using TimePlanning.Pn.Services.TimePlanningWorkingHoursService;

namespace TimePlanning.Pn.Test;

[TestFixture]
public class CalculatePayLinesForDayTests
{
    private static DateTime D(int hour, int min) => new DateTime(2026, 5, 15, hour, min, 0);

    [Test]
    public void NullPayRuleSet_ReturnsEmpty()
    {
        var model = new TimePlanningWorkingHoursModel
        {
            Date = new DateTime(2026, 5, 15),
            Start1StartedAt = D(8, 4),
            Stop1StoppedAt = D(10, 10),
        };
        var lines = TimePlanningWorkingHoursService.CalculatePayLinesForDay(
            planRegistrationId: 1, date: model.Date, dayModel: model,
            totalSeconds: 7560, payRuleSet: null!);
        Assert.That(lines, Is.Empty);
    }

    [Test]
    public void TierPath_NoTimeBandRules_UsesTotalSeconds_Exact7560()
    {
        // A PayRuleSet that has only tier rules (no TimeBandRules) for the day-of-week.
        // 08:04 → 10:10 on Friday (2026-05-15). Tier path consumes totalSeconds=7560.
        var payRuleSet = new PayRuleSet
        {
            DayRules = new List<PayDayRule>
            {
                new PayDayRule
                {
                    DayCode = "WEEKDAY",
                    Tiers = new List<PayTierRule>
                    {
                        new PayTierRule { UpperHoursExclusive = 24, PayCode = "WORK", PayrollCode = "100" }
                    }
                }
            },
            DayTypeRules = new List<PayDayTypeRule>() // empty -> no time-band branch
        };

        var model = new TimePlanningWorkingHoursModel
        {
            Date = new DateTime(2026, 5, 15), // Friday → WEEKDAY
            Start1StartedAt = D(8, 4),
            Stop1StoppedAt = D(10, 10),
        };

        var lines = TimePlanningWorkingHoursService.CalculatePayLinesForDay(
            planRegistrationId: 1, date: model.Date, dayModel: model,
            totalSeconds: 7560, payRuleSet: payRuleSet);

        Assert.That(lines.Sum(l => l.DurationInSeconds), Is.EqualTo(7560),
            "Tier path must consume the exact totalSeconds without 5-min rounding");
    }

    [Test]
    public void TimeBandPath_PerShiftAttribution_NonRoundStamps_PreserveExactSeconds()
    {
        // PayRuleSet with one Friday time-band rule covering 00:00-24:00 → WORK pay code.
        var payRuleSet = new PayRuleSet
        {
            DayTypeRules = new List<PayDayTypeRule>
            {
                new PayDayTypeRule
                {
                    DayType = DayType.Friday,
                    TimeBandRules = new List<PayTimeBandRule>
                    {
                        new PayTimeBandRule { StartSecondOfDay = 0, EndSecondOfDay = 86400,
                                              PayCode = "WORK", PayrollCode = "100" }
                    }
                }
            }
        };

        var model = new TimePlanningWorkingHoursModel
        {
            Date = new DateTime(2026, 5, 15),
            Start1StartedAt = D(8, 4),
            Stop1StoppedAt = D(10, 10),
        };

        var lines = TimePlanningWorkingHoursService.CalculatePayLinesForDay(
            planRegistrationId: 1, date: model.Date, dayModel: model,
            totalSeconds: 7560, payRuleSet: payRuleSet);

        Assert.That(lines.Sum(l => l.DurationInSeconds), Is.EqualTo(7560),
            "Time-band attribution must preserve sub-5-min precision (08:04→10:10 = 7560s)");
    }

    [Test]
    public void TimeBandPath_NullStamps_YieldsZeroLines()
    {
        // Documents the design intent at TimePlanningWorkingHoursService.cs:3800:
        // "If a shift has no real timestamps populated, it has no recorded clock time
        // and contributes no time-band pay lines."
        var payRuleSet = new PayRuleSet
        {
            DayTypeRules = new List<PayDayTypeRule>
            {
                new PayDayTypeRule
                {
                    DayType = DayType.Friday,
                    TimeBandRules = new List<PayTimeBandRule>
                    {
                        new PayTimeBandRule { StartSecondOfDay = 0, EndSecondOfDay = 86400,
                                              PayCode = "WORK", PayrollCode = "100" }
                    }
                }
            }
        };

        var model = new TimePlanningWorkingHoursModel
        {
            Date = new DateTime(2026, 5, 15),
            // No StartedAt/StoppedAt populated
        };

        var lines = TimePlanningWorkingHoursService.CalculatePayLinesForDay(
            planRegistrationId: 1, date: model.Date, dayModel: model,
            totalSeconds: 0, payRuleSet: payRuleSet);

        Assert.That(lines, Is.Empty);
    }

    [Test]
    public void TimeBandPath_ShiftCrossesBandBoundary_AttributedToBothBands()
    {
        // Two bands: 00:00-09:00 = NIGHT, 09:00-24:00 = WORK.
        // Shift 08:04 → 10:10 should split: 56 min in NIGHT (08:04-09:00 = 3360 s), 70 min in WORK (09:00-10:10 = 4200 s).
        var payRuleSet = new PayRuleSet
        {
            DayTypeRules = new List<PayDayTypeRule>
            {
                new PayDayTypeRule
                {
                    DayType = DayType.Friday,
                    TimeBandRules = new List<PayTimeBandRule>
                    {
                        new PayTimeBandRule { StartSecondOfDay = 0,     EndSecondOfDay = 32400,
                                              PayCode = "NIGHT", PayrollCode = "200" },
                        new PayTimeBandRule { StartSecondOfDay = 32400, EndSecondOfDay = 86400,
                                              PayCode = "WORK", PayrollCode = "100" },
                    }
                }
            }
        };

        var model = new TimePlanningWorkingHoursModel
        {
            Date = new DateTime(2026, 5, 15),
            Start1StartedAt = D(8, 4),
            Stop1StoppedAt = D(10, 10),
        };

        var lines = TimePlanningWorkingHoursService.CalculatePayLinesForDay(
            planRegistrationId: 1, date: model.Date, dayModel: model,
            totalSeconds: 7560, payRuleSet: payRuleSet);

        var night = lines.SingleOrDefault(l => l.PayCode == "NIGHT");
        var work  = lines.SingleOrDefault(l => l.PayCode == "WORK");
        Assert.That(night, Is.Not.Null, "Expected a NIGHT pay line");
        Assert.That(work, Is.Not.Null,  "Expected a WORK pay line");
        Assert.That(night!.DurationInSeconds, Is.EqualTo(3360), "08:04→09:00 = 56 min = 3360 s");
        Assert.That(work!.DurationInSeconds,  Is.EqualTo(4200), "09:00→10:10 = 70 min = 4200 s");
        Assert.That(night.DurationInSeconds + work.DurationInSeconds, Is.EqualTo(7560));
    }
}
```

- [ ] **Step 4.2: Run**

```bash
cd /home/rene/Documents/workspace/microting/eform-angular-frontend/eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn.Test && dotnet test --filter "FullyQualifiedName~CalculatePayLinesForDayTests" --nologo 2>&1 | tail -10
```
Expected: 5 passed.

**If `PayLineGenerator.GenerateTimeBandPayLines` lives in `Microting.TimePlanningBase` and the test cannot construct the right inputs**, fall back to a smaller surface: drop Test 5 (band split) and keep tests 1–4. Document the gap inline.

---

## Task 5: Un-ignore Phase 4 `GetShiftTime` carve-out

**Files:**
- Modify: `eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn.Test/PlanRegistrationHelperTests.cs` (around line 809–826)

- [ ] **Step 5.1: Replace the ignored body with a live test**

Find:
```csharp
[Test]
[Ignore("Phase 4 carve-out: GetShiftTime is private on TimePlanningWorkingHoursService and the fixture has no InternalsVisibleTo; assertion captured for future fixture work.")]
public void GetShiftTime_FlagOnWithActualStamp_ReturnsHHmm()
{
    // … existing intent comments …
    Assert.Pass("Captured for future fixture work; see XML doc above.");
}
```

Replace with:
```csharp
[Test]
public void GetShiftTime_FlagOnWithActualStamp_ReturnsHHmm()
{
    // With InternalsVisibleTo wired (TimePlanning.Pn.csproj) and the 4-arg
    // overload now internal, this carve-out becomes a live regression lock.
    // 08:04:42 must format as "08:04" (not "08:04:42") under HH:mm.
    var plr = new Microting.TimePlanningBase.Infrastructure.Data.Entities.PlanRegistration
    {
        Start1Id = 97, // 5-min idx 97 → "08:00" via Options[96]
        Start1StartedAt = new DateTime(2026, 5, 15, 8, 4, 42),
    };
    PlanRegistrationHelper.PopulateOptions(plr);

    var service = TimePlanningWorkingHoursServiceTestSeam.Build();
    var flagOn  = service.InvokeGetShiftTime(plr, plr.Start1Id, plr.Start1StartedAt, true);
    var flagOff = service.InvokeGetShiftTime(plr, plr.Start1Id, plr.Start1StartedAt, false);

    Assert.That(flagOn,  Is.EqualTo("08:04"));
    Assert.That(flagOff, Is.EqualTo("08:00"));
}
```

- [ ] **Step 5.2: Create the test seam**

The 4-arg `GetShiftTime` is an instance method. Constructing the full `TimePlanningWorkingHoursService` requires many dependencies. Create a thin seam:

Create: `eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn.Test/Helpers/TimePlanningWorkingHoursServiceTestSeam.cs`

```csharp
using System;
using Microsoft.Extensions.Logging.Abstractions;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using NSubstitute;
using TimePlanning.Pn.Services.TimePlanningWorkingHoursService;

namespace TimePlanning.Pn.Test;

/// <summary>
/// Construction seam for unit tests that only exercise pure-helper methods on
/// TimePlanningWorkingHoursService (no DB access). All non-required deps are
/// stubbed with NSubstitute. Do NOT use for paths that hit dbContext.
/// </summary>
internal sealed class TimePlanningWorkingHoursServiceTestSeam
{
    private readonly TimePlanningWorkingHoursService _service;
    private TimePlanningWorkingHoursServiceTestSeam(TimePlanningWorkingHoursService s) => _service = s;

    public static TimePlanningWorkingHoursServiceTestSeam Build()
    {
        // GetShiftTime does not touch any injected dependency; all stubs are safe to be null-substitutes.
        var s = (TimePlanningWorkingHoursService)System.Runtime.Serialization.FormatterServices
            .GetUninitializedObject(typeof(TimePlanningWorkingHoursService));
        return new TimePlanningWorkingHoursServiceTestSeam(s);
    }

    public string InvokeGetShiftTime(PlanRegistration plr, int? shift, DateTime? actualStamp, bool useOneMinuteIntervals)
        => _service.GetShiftTime(plr, shift, actualStamp, useOneMinuteIntervals);
}
```

**Note:** `FormatterServices.GetUninitializedObject` is obsolete in .NET 8+; if the build fails, switch to `RuntimeHelpers.GetUninitializedObject(typeof(...))` (lives in `System.Runtime.CompilerServices`).

- [ ] **Step 5.3: Confirm `PopulateOptions` helper exists**

```bash
grep -n "PopulateOptions\|public.*Options" /home/rene/Documents/workspace/microting/eform-timeplanning-base/Microting.TimePlanningBase/Infrastructure/Data/Entities/PlanRegistration.cs | head -10
```
If `PopulateOptions` does not exist, instead construct `plr.Options` inline in the test:
```csharp
plr.Options = new System.Collections.Generic.List<string>();
for (var i = 0; i < 289; i++) plr.Options.Add($"{(i*5)/60:00}:{(i*5)%60:00}");
```

- [ ] **Step 5.4: Run**

```bash
cd /home/rene/Documents/workspace/microting/eform-angular-frontend/eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn.Test && dotnet test --filter "FullyQualifiedName~GetShiftTime_FlagOnWithActualStamp_ReturnsHHmm" --nologo 2>&1 | tail -10
```
Expected: 1 passed.

---

## Task 6: Refinement tests — exact `08:04→10:10` and `RecalculatePlanHoursFromShifts`

**Files:**
- Modify: `eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn.Test/PlanRegistrationHelperTests.cs`

- [ ] **Step 6.1: Append the exact user-example regression test**

Append at the bottom of `PlanRegistrationHelperTests`:
```csharp
[Test]
public void ComputeNettoSecondsFromDateTimeShifts_Exact_08_04_to_10_10_Returns7560()
{
    // User-reported regression lock: a worker clocked in at 08:04 and out at 10:10
    // must produce exactly 2h06m = 126 min = 7560 s of netto, NOT a 5-min-snapped
    // 2h05m (legacy ID math would yield (122-97)*5 = 125 min = 7500 s).
    var pr = new Microting.TimePlanningBase.Infrastructure.Data.Entities.PlanRegistration
    {
        Date = new DateTime(2026, 5, 15),
        Start1StartedAt = new DateTime(2026, 5, 15, 8, 4, 0),
        Stop1StoppedAt = new DateTime(2026, 5, 15, 10, 10, 0),
        Start1Id = 97, Stop1Id = 122, Pause1Id = 0,
    };
    var netto = PlanRegistrationHelper.ComputeNettoSecondsFromDateTimeShifts(pr);
    Assert.That(netto, Is.EqualTo(7560), "08:04→10:10 = 2h06m = 7560 s");
}
```

- [ ] **Step 6.2: Append the `RecalculatePlanHoursFromShifts` non-5-min refinement**

```csharp
[Test]
public void RecalculatePlanHoursFromShifts_PlannedShifts_RemainMinutePrecise()
{
    // RecalculatePlanHoursFromShifts is planned-shift only (the doc comment at
    // line 351-354 explicitly says planned precision stays minute-only). This
    // test pins that contract: planned minute math must not be silently snapped
    // to a 5-min grid even when the flag is on.
    var pr = new Microting.TimePlanningBase.Infrastructure.Data.Entities.PlanRegistration
    {
        PlannedStartOfShift1 = 484, // 08:04 in minutes
        PlannedEndOfShift1   = 610, // 10:10 in minutes
        PlannedBreakOfShift1 = 0,
    };
    PlanRegistrationHelper.RecalculatePlanHoursFromShifts(pr, useOneMinuteIntervals: true);
    Assert.That(pr.PlanHours, Is.EqualTo(2.1).Within(0.001));
    Assert.That(pr.PlanHoursInSeconds, Is.EqualTo(7560));
}
```

- [ ] **Step 6.3: Run both new tests**

```bash
cd /home/rene/Documents/workspace/microting/eform-angular-frontend/eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn.Test && dotnet test --filter "FullyQualifiedName~ComputeNettoSecondsFromDateTimeShifts_Exact_08_04 | FullyQualifiedName~RecalculatePlanHoursFromShifts_PlannedShifts_RemainMinutePrecise" --nologo 2>&1 | tail -10
```
Expected: 2 passed.

---

## Task 7: Integration test — gRPC kiosk write with non-5-min stamps

**Files:**
- Create: `eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn.Test/WorkingHoursGrpcKioskNonRoundMinutesTests.cs`

- [ ] **Step 7.1: Locate the kiosk method signature**

```bash
grep -n "Kiosk\|kiosk" /home/rene/Documents/workspace/microting/eform-angular-frontend/eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn/Services/TimePlanningWorkingHoursService/TimePlanningWorkingHoursService.cs | head -20
```
Identify the public method (CreateKiosk / UpdateKiosk or whatever it's named) and its model type.

- [ ] **Step 7.2: Write the test (template — adjust types to match what 7.1 found)**

```csharp
using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Models.WorkingHours.UpdateCreate;

namespace TimePlanning.Pn.Test;

[TestFixture]
public class WorkingHoursGrpcKioskNonRoundMinutesTests : TestBaseSetup
{
    [Test]
    public async Task KioskUpdate_FlagOn_NonRoundMinutes_PersistsExactStampsAndNetto7560()
    {
        // Arrange: site with UseOneMinuteIntervals = true.
        var assignedSite = new AssignedSite
        {
            SiteId = 9501,
            SiteName = "Test 3 Skift",
            UseOneMinuteIntervals = true,
            WorkflowState = Microting.eForm.Infrastructure.Constants.Constants.WorkflowStates.Created,
        };
        await assignedSite.Create(TimePlanningPnDbContext!);

        // Act: send a kiosk update with precise non-5-min stamps.
        var model = new TimePlanningWorkingHoursUpdateModel
        {
            SdkSiteId = 9501,
            DateAsString = "2026-05-15",
            Shift1Start = 97,
            Shift1Stop = 122,
            Shift1Pause = 0,
            Start1StartedAt = "2026-05-15T08:04:00",
            Stop1StoppedAt  = "2026-05-15T10:10:00",
            PlanText = "",
            PlanHours = 2.1,
        };

        // TODO: invoke the kiosk write entry point as identified in Step 7.1.
        // Adjust the method name and signature to match.

        // Assert: persisted PlanRegistration carries the exact stamps and netto.
        var pr = await TimePlanningPnDbContext!.PlanRegistrations
            .Where(x => x.SdkSitId == 9501 && x.Date == new DateTime(2026, 5, 15))
            .SingleAsync();
        Assert.That(pr.Start1StartedAt, Is.EqualTo(new DateTime(2026, 5, 15, 8, 4, 0)));
        Assert.That(pr.Stop1StoppedAt,  Is.EqualTo(new DateTime(2026, 5, 15, 10, 10, 0)));
        Assert.That(pr.NettoHoursInSeconds, Is.EqualTo(7560),
            "Kiosk write must persist exact second-precision netto for non-5-min stamps");
    }
}
```

- [ ] **Step 7.3: Replace the TODO with the actual kiosk entry point**

Adjust based on what Step 7.1 surfaced. Most likely the method takes the model directly; mirror the pattern in `PlanningServiceMultiShiftTests.cs`.

- [ ] **Step 7.4: Run**

```bash
cd /home/rene/Documents/workspace/microting/eform-angular-frontend/eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn.Test && dotnet test --filter "FullyQualifiedName~WorkingHoursGrpcKioskNonRoundMinutesTests" --nologo 2>&1 | tail -10
```
Expected: 1 passed.

---

## Task 8: Integration test — admin web edit path with non-5-min stamps

**Files:**
- Create: `eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn.Test/PlanningServiceAdminEditNonRoundMinutesTests.cs`

- [ ] **Step 8.1: Find the admin web edit entry point**

```bash
grep -n "public.*Task.*Update\|public.*Update.*TimePlanningPlanningPrDayModel" /home/rene/Documents/workspace/microting/eform-angular-frontend/eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn/Services/TimePlanningPlanningService/TimePlanningPlanningService.cs | head -10
```

- [ ] **Step 8.2: Write the test (template — mirror `PlanningServiceMultiShiftTests` for SUT construction)**

```csharp
using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Models.Planning;

namespace TimePlanning.Pn.Test;

[TestFixture]
public class PlanningServiceAdminEditNonRoundMinutesTests : TestBaseSetup
{
    [Test]
    public async Task AdminUpdate_FlagOn_NonRoundMinutes_PersistsExactStamps()
    {
        var assignedSite = new AssignedSite
        {
            SiteId = 9502,
            SiteName = "Admin Edit Site",
            UseOneMinuteIntervals = true,
            WorkflowState = Microting.eForm.Infrastructure.Constants.Constants.WorkflowStates.Created,
        };
        await assignedSite.Create(TimePlanningPnDbContext!);

        var pr = new PlanRegistration
        {
            SdkSitId = 9502,
            Date = new DateTime(2026, 5, 15),
            WorkflowState = Microting.eForm.Infrastructure.Constants.Constants.WorkflowStates.Created,
        };
        await pr.Create(TimePlanningPnDbContext!);

        var dayModel = new TimePlanningPlanningPrDayModel
        {
            Date = new DateTime(2026, 5, 15),
            Shift1Start = 97,
            Shift1Stop = 122,
            Shift1Pause = 0,
            Start1StartedAt = new DateTime(2026, 5, 15, 8, 4, 0),
            Stop1StoppedAt  = new DateTime(2026, 5, 15, 10, 10, 0),
            PlanText = "",
            PlanHours = 2.1,
        };

        // TODO: build TimePlanningPlanningService SUT (mirror PlanningServiceMultiShiftTests
        // construction); invoke Update; await result.

        var reloaded = await TimePlanningPnDbContext!.PlanRegistrations
            .Where(x => x.SdkSitId == 9502 && x.Date == new DateTime(2026, 5, 15))
            .SingleAsync();
        Assert.That(reloaded.Start1StartedAt, Is.EqualTo(new DateTime(2026, 5, 15, 8, 4, 0)));
        Assert.That(reloaded.Stop1StoppedAt,  Is.EqualTo(new DateTime(2026, 5, 15, 10, 10, 0)));
        Assert.That(reloaded.NettoHoursInSeconds, Is.EqualTo(7560));
    }
}
```

- [ ] **Step 8.3: Run**

```bash
cd /home/rene/Documents/workspace/microting/eform-angular-frontend/eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn.Test && dotnet test --filter "FullyQualifiedName~PlanningServiceAdminEditNonRoundMinutesTests" --nologo 2>&1 | tail -10
```
Expected: 1 passed.

---

## Task 9: End-to-end Excel export test

**Files:**
- Create: `eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn.Test/WorkingHoursExcelExportE2ETests.cs`

This is the load-bearing regression lock for today's `HH:mm:ss` → `HH:mm` fix. Without this, the same bug could ship again.

- [ ] **Step 9.1: Locate the AllWorkers public entry**

```bash
grep -n "GenerateExcelDashboard\|GenerateReportFileByAllWorkers" /home/rene/Documents/workspace/microting/eform-angular-frontend/eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn/Services/TimePlanningWorkingHoursService/TimePlanningWorkingHoursService.cs | head -5
```

- [ ] **Step 9.2: Write the end-to-end test**

```csharp
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.EntityFrameworkCore;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Models.WorkingHours.Index;

namespace TimePlanning.Pn.Test;

[TestFixture]
public class WorkingHoursExcelExportE2ETests : TestBaseSetup
{
    [Test]
    public async Task AllWorkers_FlagOn_NonRoundMinutes_CellsShowHHmm_NettoExact()
    {
        // Seed: flag-on site + plan registration 08:04 → 10:10 on 2026-05-15.
        var assignedSite = new AssignedSite
        {
            SiteId = 9503,
            SiteName = "E2E 3 Skift",
            UseOneMinuteIntervals = true,
            WorkflowState = Microting.eForm.Infrastructure.Constants.Constants.WorkflowStates.Created,
        };
        await assignedSite.Create(TimePlanningPnDbContext!);

        var pr = new PlanRegistration
        {
            SdkSitId = 9503,
            Date = new DateTime(2026, 5, 15),
            Start1Id = 97, Stop1Id = 122, Pause1Id = 0,
            Start1StartedAt = new DateTime(2026, 5, 15, 8, 4, 0),
            Stop1StoppedAt  = new DateTime(2026, 5, 15, 10, 10, 0),
            NettoHours = 2.1,
            NettoHoursInSeconds = 7560,
            PlanHours = 2.1,
            WorkflowState = Microting.eForm.Infrastructure.Constants.Constants.WorkflowStates.Created,
        };
        await pr.Create(TimePlanningPnDbContext!);

        // TODO: build TimePlanningWorkingHoursService SUT (mirror PlanningServiceMultiShiftTests);
        // invoke GenerateExcelDashboard for AllWorkers route with DateFrom/To = 2026-05-15;
        // copy returned Stream into a MemoryStream.

        using var stream = new MemoryStream(/* …populated above… */);
        using var doc = SpreadsheetDocument.Open(stream, false);
        var sheet = doc.WorkbookPart!.WorksheetParts.First().Worksheet;
        var sst   = doc.WorkbookPart.SharedStringTablePart?.SharedStringTable;

        string CellText(string addr)
        {
            var c = sheet.Descendants<Cell>().Single(x => x.CellReference == addr);
            if (c.DataType?.Value == CellValues.SharedString)
                return sst!.ElementAt(int.Parse(c.CellValue!.Text)).InnerText;
            return c.CellValue?.Text ?? "";
        }

        // Column layout from FillDataRow: H=Shift1 start, I=Shift1 stop, J=Shift1 pause
        // (1-indexed: EmployeeNo, SiteName, WeekDay, Date, WeekNumber, PlanText, PlanHours, Shift1Start, …)
        // Data row 2 (row 1 = header).
        Assert.That(CellText("H2"), Is.EqualTo("08:04"),
            "Shift1 start must be HH:mm precise (no :ss); regression-locks the 2026-05-15 HH:mm fix");
        Assert.That(CellText("I2"), Is.EqualTo("10:10"),
            "Shift1 stop must be HH:mm precise");
    }
}
```

- [ ] **Step 9.3: Resolve the TODO (build SUT, get Stream)**

Mirror `PlanningServiceMultiShiftTests` for service construction. The exact SUT wiring depends on what Step 9.1 surfaced. Use `await using` for the stream lifecycle.

- [ ] **Step 9.4: Run**

```bash
cd /home/rene/Documents/workspace/microting/eform-angular-frontend/eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn.Test && dotnet test --filter "FullyQualifiedName~WorkingHoursExcelExportE2ETests" --nologo 2>&1 | tail -10
```
Expected: 1 passed.

---

## Task 10: Run the full suite, code-review, sync, commit, push

- [ ] **Step 10.1: Full test suite in host app**

```bash
cd /home/rene/Documents/workspace/microting/eform-angular-frontend/eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn.Test && dotnet test --nologo 2>&1 | tail -15
```
Expected: 0 failures. Note any new failures and back-pedal.

- [ ] **Step 10.2: Dual subagent gate (per `flutter-time-eform-cycle`)**

Dispatch `pr-review-toolkit:code-reviewer` and `code-simplifier:code-simplifier` in parallel against the unstaged diff in the host app. Fix any high-confidence issues; ignore noise.

- [ ] **Step 10.3: Sync changes from host app back to source repo**

```bash
cd /home/rene/Documents/workspace/microting/eform-angular-timeplanning-plugin && ./devgetchanges.sh
git checkout '*.csproj' '*.conf.ts' '*.xlsx' '*.docx' 2>/dev/null
git status
```

Compare the changed files against the plan. `git checkout` any unintended files.

- [ ] **Step 10.4: Commit and push**

```bash
cd /home/rene/Documents/workspace/microting/eform-angular-timeplanning-plugin
git add <specific-files-by-name>
git commit -m "$(cat <<'EOF'
test(timeplanning): comprehensive coverage for non-5-min stamp calculations

Locks the user-reported 08:04→10:10 = 2h06m (7560 s) example as a
regression test across every layer:
- ResolveShiftSeconds / EnumerateShiftSegments (pure helpers)
- CalculatePayLinesForDay tier and time-band branches
- ComputeNettoSecondsFromDateTimeShifts (exact-example regression)
- RecalculatePlanHoursFromShifts (minute-precise planned shifts)
- GetShiftTime 4-arg overload (Phase 4 carve-out un-ignored)
- gRPC kiosk write path (integration)
- TimePlanningPlanningService admin web edit (integration)
- GenerateExcelDashboard AllWorkers end-to-end (cell-level assertion)

Adds [assembly: InternalsVisibleTo] on TimePlanning.Pn so the
existing private static helpers (ResolveShiftSeconds,
EnumerateShiftSegments, CalculatePayLinesForDay, GetDayCodeForDate,
TryGetDayType) become unit-testable without a public-API change.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
git push origin stable
```

- [ ] **Step 10.5: Watch CI**

```bash
gh run list --branch stable --limit 1
gh run watch <id> --exit-status
```
Expected: all jobs green.

---

## Verification (end-to-end)

After Task 10 completes:
1. `git log --oneline -2 origin/stable` shows the new test commit.
2. `gh run view <latest>` shows all jobs ✓.
3. The 6 previously-`[Ignore]`'d Phase 4 GetShiftTime test now runs and passes; the other 5 carve-outs remain `[Ignore]`'d (documented out-of-scope).
4. Total new tests: ~16 across 6 new files + 2 appended to `PlanRegistrationHelperTests.cs` + 1 un-ignored. Every previously-uncovered gap from the 2026-05-15 audit has at least one assertion lock.
