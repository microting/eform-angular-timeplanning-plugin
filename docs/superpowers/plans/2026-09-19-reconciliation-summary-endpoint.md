# Reconciliation Summary Endpoint Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add `GET /api/time-planning-pn/reconciliation/summary`, a read-only endpoint that reports how well a customer keeps its last closed payroll period reconciled ("Afstem").

**Architecture:** A pure `PayrollPeriod.LastClosed(todayUtc, cutoffDay)` picks the most recent closed monthly period. `ReconciliationSummaryService` reads `CutoffDay` from `PayrollIntegrationSettings`, finds the workers with planned or worked hours in that period, and gets their lock boundaries from the existing `DayLockHelper.LockedThroughForSitesAsync` (one query). It does not re-implement any lock rule. A thin `ReconciliationSummaryController` returns the result inside `OperationDataResult<T>`, like every other endpoint in the plugin.

**Tech Stack:** C# / .NET 10, ASP.NET Core MVC (host serializes with Newtonsoft + `CamelCasePropertyNamesContractResolver`), EF Core + Pomelo MariaDB, NUnit 4 + NSubstitute + Testcontainers (`mariadb:11`).

**Spec:** `/home/rene/Documents/workspace/microting/docker/angular-my-microting-plugin/.claude/worktrees/afstem-stats/docs/superpowers/specs/2026-09-19-afstem-stats-design.md`. This plan covers §3 and the timeplanning half of §7. §4–§6 (my-microting) are a separate plan, and that plan consumes the **Wire contract** section below.

## Global Constraints

- Repo/worktree: `/home/rene/Documents/workspace/microting/eform-angular-timeplanning-plugin-summary`, branch `feat/reconciliation-summary` (off `origin/stable`). The PR targets `stable`. Never commit to `stable`.
- Not in dev mode: edit the source plugin repo directly. Do not run `devinstall.sh`.
- All paths below are relative to the repo root. `P` = `eFormAPI/Plugins/TimePlanning.Pn`.
- Route: `GET /api/time-planning-pn/reconciliation/summary`, `[Authorize]`, read-only. It never writes.
- `CutoffDay` comes from the first non-removed `PayrollIntegrationSettings` row. **Default is 19** when there is no such row. Values outside 1..31 are clamped into 1..31.
- Evaluated period: the most recent closed period, meaning the latest `periodEnd < DateTime.UtcNow.Date` with `periodEnd.Day == min(CutoffDay, DaysInMonth)`. `periodStart` is the day after the previous period's end. This uses the same clock as `DayLockHelper.CanReconcile`.
- A worker counts in the period when it has ≥1 non-removed `PlanRegistration` with `Date` in `[periodStart, periodEnd]` and planned or worked hours. (See the Spec deviations section for the widened hours predicate.)
- Boundaries come only from `DayLockHelper.LockedThroughForSitesAsync`. "Locked through period" means `DayLockHelper.IsLocked(boundary, periodEnd)`.
- Dates on the wire are `yyyy-MM-dd` strings. `lastReconciledAt` is a `DateTime` with `Kind = Utc`, so Newtonsoft writes a trailing `Z`. This follows the `PlanRegistrationHelper` ReconciledAt convention.
- No EF migrations; the base package is not touched. `DayLockHelper` gets one visibility change (`BoundaryRows` private → internal) and no logic change, so its twin in `eform-service-timeplanning-plugin` does not need to change.
- Every new test class must appear in the shard filters of **both** `.github/workflows/dotnet-core-pr.yml` and `.github/workflows/dotnet-core-master.yml`. If it doesn't, `ShardCoverageTests` fails.
- Stage files by name only. Never `git add .` or `git commit -a`. Every commit message ends with `Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>`.

## Wire contract (for the my-microting consumer)

HTTP 200 with the standard `OperationDataResult<T>` envelope. Property names are camelCase and the order is not guaranteed:

```json
{
  "model": {
    "cutoffDay": 19,
    "periodStart": "2026-07-20",
    "periodEnd": "2026-08-19",
    "workersInPeriod": 10,
    "workersLockedThroughPeriod": 7,
    "workersNeverReconciled": 1,
    "coveragePercent": 70.0,
    "oldestBoundary": "2026-06-30",
    "workersWithAnyReconciled": 9,
    "lastReconciledAt": "2026-09-18T12:32:00.123456Z"
  },
  "success": true,
  "message": "Success"
}
```

- `cutoffDay`: int, the clamped value that was actually used (1..31).
- `periodStart`, `periodEnd`: string `yyyy-MM-dd`, never null.
- `workersInPeriod`, `workersLockedThroughPeriod`, `workersNeverReconciled`, `workersWithAnyReconciled`: int, never null.
- `coveragePercent`: number rounded to 1 decimal (`MidpointRounding.AwayFromZero`), or `null` when `workersInPeriod == 0`. Whole values are written as `70.0`.
- `oldestBoundary`: string `yyyy-MM-dd`, or `null` when no worker in the period has a boundary.
- `lastReconciledAt`: ISO-8601 UTC ending in `Z`, or `null`. The column is `datetime(6)`, so there are **0–7 fractional-second digits** (Newtonsoft trims trailing zeros, for example `...12:32:00Z` or `...12:32:00.123456Z`). The consumer must parse with fractions allowed, e.g. `DateTime.Parse(..., RoundtripKind)` / `DateTimeOffset.Parse`.
- `workersBehind` is not sent. It equals `workersInPeriod - workersLockedThroughPeriod - workersNeverReconciled`.
- **Failure:** if the service catches an exception, the response is still **HTTP 200** with `{"model": null, "success": false, "message": "ErrorWhileReadingReconciliationSummary"}`. The consumer must check `success` and map `false` to `Failed(message)`.
- 401 when unauthenticated. 404 when the plugin or endpoint is absent (older plugin version, or timeplanning not installed).

## Spec deviations / clarifications found in the real code

1. **Hours predicate: one-minute mode only (user decision 2026-09-19).** Every customer is forced to `UseOneMinuteIntervals=true`, so the predicate is `PlanHoursInSeconds > 0 || NettoHoursInSeconds > 0 || Start1StartedAt != null`. The legacy 5-minute `PlanHours`/`NettoHours` doubles are deliberately not consulted; tests pin both directions.
2. **`coveragePercent` rounding** is not specified. This plan uses 1 decimal, away from zero.
3. **Failure is HTTP 200 + `success:false`**, not a 5xx. That is the plugin-wide convention (see `PayrollExportService`). The spec's `Failed(message)` should cover it (see the Wire contract).
4. **"First" settings row** is taken as ordered by `Id`. `PayrollExportService` uses an unordered `FirstOrDefaultAsync`, which is nondeterministic if there are ever two rows.
5. **Authorization** is `[Authorize]` only, as the spec says. The payroll controller uses `[Authorize(Roles = EformRole.Admin)]`. The my-microting service login is an admin, so either works. Keep `[Authorize]` unless review asks otherwise.

---

## File Structure

| File | Responsibility |
|---|---|
| Create `P/TimePlanning.Pn/Infrastructure/Helpers/PayrollPeriod.cs` | Pure period arithmetic. No database. |
| Create `P/TimePlanning.Pn/Infrastructure/Models/Reconciliation/ReconciliationSummaryModel.cs` | Response DTO (wire shape). |
| Create `P/TimePlanning.Pn/Services/ReconciliationSummaryService/IReconciliationSummaryService.cs` | Service interface. |
| Create `P/TimePlanning.Pn/Services/ReconciliationSummaryService/ReconciliationSummaryService.cs` | Queries and aggregation. |
| Create `P/TimePlanning.Pn/Controllers/ReconciliationSummaryController.cs` | Route + auth, delegates to the service. |
| Modify `P/TimePlanning.Pn/Infrastructure/Helpers/DayLockHelper.cs` (`BoundaryRows`, near the end) | `private` → `internal`, so the service reuses the one definition of a boundary row. |
| Modify `P/TimePlanning.Pn/EformTimePlanningPlugin.cs` (usings ~l.41–43, DI ~l.114–116) | Register the service. |
| Create `P/TimePlanning.Pn.Test/PayrollPeriodTests.cs` | Pure tests (no DB). |
| Create `P/TimePlanning.Pn.Test/ReconciliationSummaryServiceTests.cs` | DB tests (Testcontainers MariaDB via `TestBaseSetup`). |
| Create `P/TimePlanning.Pn.Test/ReconciliationSummaryContractTests.cs` | Wire-shape + controller attribute tests (no DB). |
| Modify `.github/workflows/dotnet-core-pr.yml`, `.github/workflows/dotnet-core-master.yml` | Add the 3 new classes to shard filters. |

## How to build and test locally

From the repo root:

```bash
# Build (works offline once restored)
dotnet build eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn.sln

# Pure tests: no Docker needed, run locally
dotnet test eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn.Test/TimePlanning.Pn.Test.csproj \
  --filter "FullyQualifiedName=TimePlanning.Pn.Test.PayrollPeriodTests|FullyQualifiedName=TimePlanning.Pn.Test.ReconciliationSummaryContractTests|FullyQualifiedName=TimePlanning.Pn.Test.ShardCoverageTests"

# DB tests: need a running Docker daemon (Testcontainers pulls mariadb:11). CI is authoritative.
dotnet test eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn.Test/TimePlanning.Pn.Test.csproj \
  --settings eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn.Test/test.runsettings \
  --filter "FullyQualifiedName=TimePlanning.Pn.Test.ReconciliationSummaryServiceTests"
```

Only CI is guaranteed to run: `ReconciliationSummaryServiceTests` (Testcontainers). Run it locally only if Docker is available, and report plainly if it was not run. `PayrollPeriodTests`, `ReconciliationSummaryContractTests` and `ShardCoverageTests` run anywhere. `ShardCoverageTests` must be run from this full checkout, not from a dev-mode copy.

---

### Task 1: `PayrollPeriod.LastClosed`, a pure function

**Files:**
- Create: `P/TimePlanning.Pn/Infrastructure/Helpers/PayrollPeriod.cs`
- Test: `P/TimePlanning.Pn.Test/PayrollPeriodTests.cs`
- Modify: `.github/workflows/dotnet-core-pr.yml`, `.github/workflows/dotnet-core-master.yml` (shard `g`)

**Interfaces:**
- Consumes: nothing.
- Produces: `public readonly record struct PayrollPeriod(DateTime Start, DateTime End)` in namespace `TimePlanning.Pn.Infrastructure.Helpers`, with `public static PayrollPeriod LastClosed(DateTime todayUtc, int cutoffDay)`. `Start`/`End` are date-only (`Kind = Unspecified`, the same as `PlanRegistration.Date`), inclusive, and `End < todayUtc.Date`.

- [ ] **Step 1: Write the failing test**

Create `P/TimePlanning.Pn.Test/PayrollPeriodTests.cs`:

```csharp
using System;
using System.Globalization;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Helpers;

namespace TimePlanning.Pn.Test;

/// <summary>
/// Spec §3 "Period rule". Pure arithmetic, so this fixture does NOT derive
/// TestBaseSetup and starts no database container.
/// </summary>
[TestFixture]
public class PayrollPeriodTests
{
    private static DateTime D(string s) => DateTime.ParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    [TestCase("2026-09-18", 19, "2026-07-20", "2026-08-19", TestName = "Cutoff19_DayBeforeCutoff")]
    [TestCase("2026-09-19", 19, "2026-07-20", "2026-08-19", TestName = "Cutoff19_OnCutoffDay_PeriodNotYetClosed")]
    [TestCase("2026-09-20", 19, "2026-08-20", "2026-09-19", TestName = "Cutoff19_DayAfterCutoff")]
    [TestCase("2026-03-01", 31, "2026-02-01", "2026-02-28", TestName = "Cutoff31_ClampsToFeb28")]
    [TestCase("2026-03-31", 31, "2026-02-01", "2026-02-28", TestName = "Cutoff31_OnMarch31_StillFebruary")]
    [TestCase("2026-04-01", 31, "2026-03-01", "2026-03-31", TestName = "Cutoff31_AfterMarch31")]
    [TestCase("2028-03-01", 31, "2028-02-01", "2028-02-29", TestName = "Cutoff31_LeapFebruary")]
    [TestCase("2028-03-15", 30, "2028-01-31", "2028-02-29", TestName = "Cutoff30_LeapFebruary_StartAfterJan30")]
    [TestCase("2026-09-01", 1, "2026-07-02", "2026-08-01", TestName = "Cutoff1_OnCutoffDay")]
    [TestCase("2026-09-02", 1, "2026-08-02", "2026-09-01", TestName = "Cutoff1_DayAfter")]
    [TestCase("2026-01-19", 19, "2025-11-20", "2025-12-19", TestName = "YearBoundary_JanuaryOnCutoff")]
    [TestCase("2026-01-20", 19, "2025-12-20", "2026-01-19", TestName = "YearBoundary_JanuaryAfterCutoff")]
    [TestCase("2026-01-10", 31, "2025-12-01", "2025-12-31", TestName = "YearBoundary_Cutoff31")]
    public void LastClosed_KnownDates(string today, int cutoff, string expectedStart, string expectedEnd)
    {
        var period = PayrollPeriod.LastClosed(D(today), cutoff);

        Assert.Multiple(() =>
        {
            Assert.That(period.Start, Is.EqualTo(D(expectedStart)), "start");
            Assert.That(period.End, Is.EqualTo(D(expectedEnd)), "end");
        });
    }

    [TestCase(0, 1)]
    [TestCase(-5, 1)]
    [TestCase(32, 31)]
    [TestCase(45, 31)]
    public void LastClosed_OutOfRangeCutoff_IsClamped(int given, int clampedTo)
    {
        var today = D("2026-09-20");

        Assert.That(PayrollPeriod.LastClosed(today, given),
            Is.EqualTo(PayrollPeriod.LastClosed(today, clampedTo)));
    }

    [Test]
    public void LastClosed_IgnoresTimeOfDay()
    {
        var midnight = PayrollPeriod.LastClosed(D("2026-09-20"), 19);
        var lateEvening = PayrollPeriod.LastClosed(D("2026-09-20").AddHours(23).AddMinutes(59), 19);

        Assert.That(lateEvening, Is.EqualTo(midnight));
    }

    /// <summary>
    /// Brute-force oracle straight from the spec's wording: End is the LATEST
    /// d &lt; today with d.Day == min(cutoff, DaysInMonth(d)); Start is the day
    /// after the previous such d. Covers every day of 2027-2028 (a leap year)
    /// for every cutoff, so no month-length corner is left to a hand-picked case.
    /// </summary>
    [Test]
    public void LastClosed_MatchesSpecDefinition_ForEveryDayAndCutoff()
    {
        static bool IsCutoffDay(DateTime d, int c) => d.Day == Math.Min(c, DateTime.DaysInMonth(d.Year, d.Month));

        static DateTime LatestCutoffBefore(DateTime exclusive, int c)
        {
            var d = exclusive.AddDays(-1);
            while (!IsCutoffDay(d, c)) d = d.AddDays(-1);
            return d;
        }

        for (var today = D("2027-01-01"); today <= D("2028-12-31"); today = today.AddDays(1))
        {
            for (var cutoff = 1; cutoff <= 31; cutoff++)
            {
                var expectedEnd = LatestCutoffBefore(today, cutoff);
                var expectedStart = LatestCutoffBefore(expectedEnd, cutoff).AddDays(1);

                var actual = PayrollPeriod.LastClosed(today, cutoff);

                if (actual.End != expectedEnd || actual.Start != expectedStart)
                {
                    Assert.Fail($"today {today:yyyy-MM-dd} cutoff {cutoff}: expected " +
                                $"{expectedStart:yyyy-MM-dd}..{expectedEnd:yyyy-MM-dd}, got " +
                                $"{actual.Start:yyyy-MM-dd}..{actual.End:yyyy-MM-dd}");
                }
            }
        }
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn.Test/TimePlanning.Pn.Test.csproj --filter "FullyQualifiedName=TimePlanning.Pn.Test.PayrollPeriodTests"`
Expected: build FAILS with `CS0246: The type or namespace name 'PayrollPeriod' could not be found`.

- [ ] **Step 3: Write the minimal implementation**

Create `P/TimePlanning.Pn/Infrastructure/Helpers/PayrollPeriod.cs`:

```csharp
#nullable enable
namespace TimePlanning.Pn.Infrastructure.Helpers;

using System;

/// <summary>
/// A monthly payroll period, <see cref="Start"/>..<see cref="End"/> inclusive.
/// Both are calendar-day labels (time zeroed, Kind Unspecified), the same
/// shape as PlanRegistration.Date, so they compare directly against it.
/// </summary>
public readonly record struct PayrollPeriod(DateTime Start, DateTime End)
{
    /// <summary>
    /// The most recent CLOSED period: End is the latest day strictly before
    /// <paramref name="todayUtc"/> whose day-of-month is
    /// min(cutoffDay, days in that month), and Start is the day after the
    /// previous period's End. The cutoff day itself still belongs to the open
    /// period until it is over, which matches DayLockHelper.CanReconcile
    /// (only days before UtcNow.Date can be reconciled).
    ///
    /// Callers pass DateTime.UtcNow.Date -- the same clock as CanReconcile.
    /// <paramref name="cutoffDay"/> outside 1..31 is clamped into that range.
    /// </summary>
    public static PayrollPeriod LastClosed(DateTime todayUtc, int cutoffDay)
    {
        var cutoff = Math.Clamp(cutoffDay, 1, 31);
        var today = todayUtc.Date;

        var end = CutoffIn(today, cutoff);
        if (end >= today)
        {
            end = CutoffIn(FirstOfMonth(today).AddMonths(-1), cutoff);
        }

        var start = CutoffIn(FirstOfMonth(end).AddMonths(-1), cutoff).AddDays(1);
        return new PayrollPeriod(start, end);
    }

    private static DateTime FirstOfMonth(DateTime d) => new(d.Year, d.Month, 1);

    private static DateTime CutoffIn(DateTime anyDayOfMonth, int cutoff) =>
        new(anyDayOfMonth.Year, anyDayOfMonth.Month,
            Math.Min(cutoff, DateTime.DaysInMonth(anyDayOfMonth.Year, anyDayOfMonth.Month)));
}
```

- [ ] **Step 4: Register the fixture in both CI workflows (shard `g`)**

`PayrollPeriodTests` has no database, so it can go in any shard. Append it to the end of shard `g`'s filter. In both files that filter currently ends with `Helpers.PauseMinutesCalculatorTests"`, which is unique:

```bash
sed -i 's/FullyQualifiedName=TimePlanning\.Pn\.Test\.Helpers\.PauseMinutesCalculatorTests"$/FullyQualifiedName=TimePlanning.Pn.Test.Helpers.PauseMinutesCalculatorTests|FullyQualifiedName=TimePlanning.Pn.Test.PayrollPeriodTests"/' \
  .github/workflows/dotnet-core-pr.yml .github/workflows/dotnet-core-master.yml
grep -c "TimePlanning.Pn.Test.PayrollPeriodTests" .github/workflows/dotnet-core-pr.yml .github/workflows/dotnet-core-master.yml
```

Expected: each file reports `1`.

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn.Test/TimePlanning.Pn.Test.csproj --filter "FullyQualifiedName=TimePlanning.Pn.Test.PayrollPeriodTests|FullyQualifiedName=TimePlanning.Pn.Test.ShardCoverageTests"`
Expected: PASS. That is 13 known-date cases, 4 clamp cases, the time-of-day test, the oracle test, and `ShardCoverageTests`.

- [ ] **Step 6: Commit**

```bash
git add eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn/Infrastructure/Helpers/PayrollPeriod.cs \
        eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn.Test/PayrollPeriodTests.cs \
        .github/workflows/dotnet-core-pr.yml .github/workflows/dotnet-core-master.yml
git commit -m "$(cat <<'EOF'
feat(reconciliation): PayrollPeriod.LastClosed picks the last closed payroll period

Pure function, no database: the latest cutoff day strictly before today (UTC),
cutoff clamped to the month length and to 1..31.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 2: `ReconciliationSummaryService` and the response model

**Files:**
- Create: `P/TimePlanning.Pn/Infrastructure/Models/Reconciliation/ReconciliationSummaryModel.cs`
- Create: `P/TimePlanning.Pn/Services/ReconciliationSummaryService/IReconciliationSummaryService.cs`
- Create: `P/TimePlanning.Pn/Services/ReconciliationSummaryService/ReconciliationSummaryService.cs`
- Modify: `P/TimePlanning.Pn/Infrastructure/Helpers/DayLockHelper.cs` (the `BoundaryRows` declaration at the bottom of the class)
- Test: `P/TimePlanning.Pn.Test/ReconciliationSummaryServiceTests.cs`
- Modify: `.github/workflows/dotnet-core-pr.yml`, `.github/workflows/dotnet-core-master.yml` (shard `a`)

**Interfaces:**
- Consumes: `PayrollPeriod.LastClosed(DateTime, int)` (Task 1); `DayLockHelper.LockedThroughForSitesAsync(TimePlanningPnDbContext, IReadOnlyCollection<int>, CancellationToken)`; `DayLockHelper.IsLocked(DateTime?, DateTime)`; `DayLockHelper.BoundaryRows(TimePlanningPnDbContext)` (made `internal` here).
- Produces:
  - `TimePlanning.Pn.Infrastructure.Models.Reconciliation.ReconciliationSummaryModel`, with properties `int CutoffDay`, `string PeriodStart`, `string PeriodEnd`, `int WorkersInPeriod`, `int WorkersLockedThroughPeriod`, `int WorkersNeverReconciled`, `double? CoveragePercent`, `string? OldestBoundary`, `int WorkersWithAnyReconciled`, `DateTime? LastReconciledAt`.
  - `TimePlanning.Pn.Services.ReconciliationSummaryService.IReconciliationSummaryService`, with `Task<OperationDataResult<ReconciliationSummaryModel>> GetSummaryAsync();`
  - `ReconciliationSummaryService(TimePlanningPnDbContext dbContext, ILogger<ReconciliationSummaryService> logger)`, with the public `GetSummaryAsync()` and `internal GetSummaryAsync(DateTime todayUtc)` (test seam; the test project already has `InternalsVisibleTo`).
  - Constants: `ReconciliationSummaryService.DefaultCutoffDay = 19` and `ReconciliationSummaryService.ErrorMessage = "ErrorWhileReadingReconciliationSummary"`.

- [ ] **Step 1: Write the failing tests**

Create `P/TimePlanning.Pn.Test/ReconciliationSummaryServiceTests.cs`:

```csharp
using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microting.eForm.Infrastructure.Constants;
using Microting.TimePlanningBase.Infrastructure.Data;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using NSubstitute;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Helpers;
using TimePlanning.Pn.Services.ReconciliationSummaryService;
using PlanRegistrationEntity = Microting.TimePlanningBase.Infrastructure.Data.Entities.PlanRegistration;

namespace TimePlanning.Pn.Test;

/// <summary>
/// Spec §3/§7. Runs against a real MariaDB (TestBaseSetup), because the
/// counting is a set of EF queries and the boundaries come from DayLockHelper.
///
/// SEEDING ORDER MATTERS. The fixture context carries
/// ReconciledDayLockInterceptor: creating or changing a row at or before a
/// site's current boundary throws DayLockedException. So per site, create the
/// plain rows first, then the reconciled rows in ascending date order (see
/// DayLockHelperTests for the full reasoning).
///
/// Unless a test says otherwise, "today" is 2026-09-20 and there is no
/// settings row, so the period is 2026-08-20..2026-09-19 (cutoff 19).
/// </summary>
[TestFixture]
public class ReconciliationSummaryServiceTests : TestBaseSetup
{
    private static readonly DateTime Today = D("2026-09-20");

    [SetUp]
    public async Task SetUpTest() => await base.Setup();

    private static DateTime D(string s) => DateTime.ParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    private ReconciliationSummaryService Service(TimePlanningPnDbContext db = null) =>
        new(db ?? TimePlanningPnDbContext!, Substitute.For<ILogger<ReconciliationSummaryService>>());

    // PnBase.Create overwrites WorkflowState with "created"; soft-delete with row.Delete().
    private async Task<PlanRegistrationEntity> Seed(
        int site, string date,
        int planSeconds = 0, int nettoSeconds = 0,
        double planHours = 0, double nettoHours = 0,
        DateTime? start1StartedAt = null,
        DateTime? reconciledAt = null)
    {
        var row = new PlanRegistrationEntity
        {
            SdkSitId = site,
            Date = D(date),
            PlanHoursInSeconds = planSeconds,
            NettoHoursInSeconds = nettoSeconds,
            PlanHours = planHours,
            NettoHours = nettoHours,
            Start1StartedAt = start1StartedAt,
            Reconciled = reconciledAt.HasValue,
            ReconciledAt = reconciledAt,
            PlanText = "",
            CommentOffice = "",
            CommentOfficeAll = "",
            CreatedByUserId = 1,
            UpdatedByUserId = 1,
        };
        await row.Create(TimePlanningPnDbContext!);
        return row;
    }

    [Test]
    public async Task Counts_ClassifyLockedBehindNeverAndIgnoreOutOfScopeRows()
    {
        // 801 LOCKED: hours in period, boundary == periodEnd.
        await Seed(801, "2026-08-25", planSeconds: 27000);
        await Seed(801, "2026-09-19", reconciledAt: new DateTime(2026, 9, 20, 6, 0, 0));
        // 802 BEHIND: hours in period, boundary inside the period.
        await Seed(802, "2026-09-01", nettoSeconds: 25200);
        await Seed(802, "2026-09-05", reconciledAt: new DateTime(2026, 9, 6, 8, 0, 0));
        // 803 NEVER: hours in period, nothing reconciled.
        await Seed(803, "2026-09-10", planSeconds: 27000);
        // 804 LOCKED: boundary after periodEnd still locks the whole period.
        await Seed(804, "2026-09-03", planSeconds: 27000);
        await Seed(804, "2026-09-25", reconciledAt: new DateTime(2026, 9, 26, 7, 0, 0));
        // 805 NOT IN PERIOD (only a zero-hours row in it) but HAS reconciled (outside the period).
        await Seed(805, "2026-06-10", reconciledAt: new DateTime(2026, 6, 11, 7, 0, 0));
        await Seed(805, "2026-09-12");
        // 806 NOT IN PERIOD: its only in-period row is soft-deleted.
        var removed = await Seed(806, "2026-09-02", planSeconds: 27000);
        await removed.Delete(TimePlanningPnDbContext!);
        // 807 NOT IN PERIOD: rows one day either side of the period.
        await Seed(807, "2026-08-19", planSeconds: 27000);
        await Seed(807, "2026-09-20", planSeconds: 27000);
        // 808 NEVER: row exactly on periodStart counts (inclusive).
        await Seed(808, "2026-08-20", planSeconds: 27000);

        var result = await Service().GetSummaryAsync(Today);

        Assert.That(result.Success, Is.True, result.Message);
        var m = result.Model;
        Assert.Multiple(() =>
        {
            Assert.That(m.CutoffDay, Is.EqualTo(19));
            Assert.That(m.PeriodStart, Is.EqualTo("2026-08-20"));
            Assert.That(m.PeriodEnd, Is.EqualTo("2026-09-19"));
            Assert.That(m.WorkersInPeriod, Is.EqualTo(5), "801, 802, 803, 804, 808");
            Assert.That(m.WorkersLockedThroughPeriod, Is.EqualTo(2), "801, 804");
            Assert.That(m.WorkersNeverReconciled, Is.EqualTo(2), "803, 808");
            Assert.That(m.CoveragePercent, Is.EqualTo(40.0));
            Assert.That(m.OldestBoundary, Is.EqualTo("2026-09-05"), "802's boundary; 805 is not in the period");
            Assert.That(m.WorkersWithAnyReconciled, Is.EqualTo(4), "801, 802, 804, 805 -- not limited to the period");
        });
    }

    [Test]
    public async Task RowWithOnlyAStart1Stamp_IsCounted()
    {
        // One-minute-interval mode: an exact start stamp is registered time,
        // even before net seconds have been computed for the day.
        await Seed(830, "2026-09-01", start1StartedAt: new DateTime(2026, 9, 1, 6, 58, 0));

        var result = await Service().GetSummaryAsync(Today);

        Assert.That(result.Model.WorkersInPeriod, Is.EqualTo(1));
    }

    [Test]
    public async Task RowsWithOnlyLegacyDoubleHours_AreNotCounted()
    {
        // All customers run UseOneMinuteIntervals=true; the legacy 5-minute
        // doubles alone do not make a worker count (user decision 2026-09-19).
        await Seed(831, "2026-09-01", planHours: 7.5);
        await Seed(832, "2026-09-02", nettoHours: 6.25);

        var result = await Service().GetSummaryAsync(Today);

        Assert.That(result.Model.WorkersInPeriod, Is.Zero);
    }

    [Test]
    public async Task NoSettingsRow_FallsBackToCutoff19()
    {
        Assert.That(await TimePlanningPnDbContext!.PayrollIntegrationSettings
            .CountAsync(x => x.WorkflowState != Constants.WorkflowStates.Removed), Is.Zero,
            "precondition: the plugin seed creates no PayrollIntegrationSettings row");

        var result = await Service().GetSummaryAsync(Today);

        Assert.Multiple(() =>
        {
            Assert.That(result.Model.CutoffDay, Is.EqualTo(19));
            Assert.That(result.Model.PeriodStart, Is.EqualTo("2026-08-20"));
            Assert.That(result.Model.PeriodEnd, Is.EqualTo("2026-09-19"));
        });
    }

    [Test]
    public async Task SettingsRow_CutoffDrivesThePeriod()
    {
        await new PayrollIntegrationSettings { CutoffDay = 5, CreatedByUserId = 1, UpdatedByUserId = 1 }
            .Create(TimePlanningPnDbContext!);

        var result = await Service().GetSummaryAsync(Today);

        Assert.Multiple(() =>
        {
            Assert.That(result.Model.CutoffDay, Is.EqualTo(5));
            Assert.That(result.Model.PeriodStart, Is.EqualTo("2026-08-06"));
            Assert.That(result.Model.PeriodEnd, Is.EqualTo("2026-09-05"));
        });
    }

    [Test]
    public async Task RemovedSettingsRow_IsIgnored()
    {
        var settings = new PayrollIntegrationSettings { CutoffDay = 5, CreatedByUserId = 1, UpdatedByUserId = 1 };
        await settings.Create(TimePlanningPnDbContext!);
        await settings.Delete(TimePlanningPnDbContext!);

        var result = await Service().GetSummaryAsync(Today);

        Assert.That(result.Model.CutoffDay, Is.EqualTo(19));
    }

    [Test]
    public async Task SettingsRowOutOfRange_IsClampedAndReported()
    {
        await new PayrollIntegrationSettings { CutoffDay = 45, CreatedByUserId = 1, UpdatedByUserId = 1 }
            .Create(TimePlanningPnDbContext!);

        var result = await Service().GetSummaryAsync(Today);

        Assert.Multiple(() =>
        {
            Assert.That(result.Model.CutoffDay, Is.EqualTo(31));
            Assert.That(result.Model.PeriodEnd, Is.EqualTo("2026-08-31"));
        });
    }

    [Test]
    public async Task NoWorkers_CoverageAndBoundariesAreNull()
    {
        var result = await Service().GetSummaryAsync(Today);

        Assert.That(result.Success, Is.True, result.Message);
        Assert.Multiple(() =>
        {
            Assert.That(result.Model.WorkersInPeriod, Is.Zero);
            Assert.That(result.Model.WorkersLockedThroughPeriod, Is.Zero);
            Assert.That(result.Model.WorkersNeverReconciled, Is.Zero);
            Assert.That(result.Model.CoveragePercent, Is.Null);
            Assert.That(result.Model.OldestBoundary, Is.Null);
            Assert.That(result.Model.WorkersWithAnyReconciled, Is.Zero);
            Assert.That(result.Model.LastReconciledAt, Is.Null);
        });
    }

    [Test]
    public async Task WorkersWithAnyReconciled_SpansOutsideThePeriod()
    {
        await Seed(850, "2025-01-15", reconciledAt: new DateTime(2025, 1, 16, 9, 0, 0));

        var result = await Service().GetSummaryAsync(Today);

        Assert.Multiple(() =>
        {
            Assert.That(result.Model.WorkersInPeriod, Is.Zero);
            Assert.That(result.Model.CoveragePercent, Is.Null);
            Assert.That(result.Model.WorkersWithAnyReconciled, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task CoveragePercent_IsRoundedToOneDecimal()
    {
        await Seed(860, "2026-09-01", planSeconds: 3600);
        await Seed(860, "2026-09-19", reconciledAt: new DateTime(2026, 9, 20, 6, 0, 0));
        await Seed(861, "2026-09-01", planSeconds: 3600);
        await Seed(862, "2026-09-01", planSeconds: 3600);

        var result = await Service().GetSummaryAsync(Today);

        Assert.That(result.Model.CoveragePercent, Is.EqualTo(33.3), "1 of 3");
    }

    /// <summary>
    /// datetime(6) has no offset, so EF hands back Kind Unspecified; the
    /// service must re-tag it Utc so Newtonsoft writes the trailing "Z"
    /// (same convention as PlanRegistrationHelper's ReconciledAt projection).
    /// A soft-deleted reconciled row must not count -- it is seeded with the
    /// LATEST stamp so a missing filter would show up as the wrong max.
    /// </summary>
    [Test]
    public async Task LastReconciledAt_IsMaxOverLiveReconciledRows_TaggedUtc()
    {
        await Seed(840, "2026-09-01", reconciledAt: new DateTime(2026, 9, 2, 8, 0, 0));
        await Seed(841, "2026-09-03", reconciledAt: new DateTime(2026, 9, 18, 12, 32, 0));

        // Reconcile + soft-delete in ONE save (no boundary exists for 842, so
        // the interceptor permits it) -- the same trick as
        // DayLockHelperTests.LockedThrough_IgnoresRemovedRows.
        var ghost = await Seed(842, "2026-09-10");
        ghost.Reconciled = true;
        ghost.ReconciledAt = new DateTime(2026, 9, 30, 23, 0, 0);
        await ghost.Delete(TimePlanningPnDbContext!);

        var result = await Service().GetSummaryAsync(Today);

        Assert.Multiple(() =>
        {
            Assert.That(result.Model.LastReconciledAt, Is.EqualTo(new DateTime(2026, 9, 18, 12, 32, 0)));
            Assert.That(result.Model.LastReconciledAt!.Value.Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(result.Model.WorkersWithAnyReconciled, Is.EqualTo(2), "842's only reconciled row is removed");
        });
    }

    [Test]
    public async Task ParameterlessOverload_UsesUtcToday()
    {
        var result = await Service().GetSummaryAsync();

        var expected = PayrollPeriod.LastClosed(DateTime.UtcNow.Date, 19);
        Assert.That(result.Model.PeriodEnd, Is.EqualTo(expected.End.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
    }

    [Test]
    public async Task DatabaseFailure_ReturnsUnsuccessfulResultInsteadOfThrowing()
    {
        var broken = CreateTimePlanningPnDbContext();
        await broken.DisposeAsync();

        var result = await Service(broken).GetSummaryAsync(Today);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Is.EqualTo(ReconciliationSummaryService.ErrorMessage));
            Assert.That(result.Model, Is.Null);
        });
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet build eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn.sln`
Expected: FAILS with `CS0234`/`CS0246` for `TimePlanning.Pn.Services.ReconciliationSummaryService` / `ReconciliationSummaryService`.

- [ ] **Step 3: Make `DayLockHelper.BoundaryRows` internal**

In `P/TimePlanning.Pn/Infrastructure/Helpers/DayLockHelper.cs`, change the last member of the class from

```csharp
    /// <summary>
    /// What counts as a boundary row, in one place: Reconciled and not
    /// soft-deleted. Both public queries compose their own site predicate
    /// over this so the two never drift apart.
    /// </summary>
    private static IQueryable<PlanRegistration> BoundaryRows(TimePlanningPnDbContext db)
```

to

```csharp
    /// <summary>
    /// What counts as a boundary row, in one place: Reconciled and not
    /// soft-deleted. Both public queries compose their own site predicate
    /// over this so the two never drift apart. Internal (not private) so the
    /// reconciliation summary counts "has ever reconciled" over the SAME rows
    /// that hold boundaries; the service twin needs no change for this.
    /// </summary>
    internal static IQueryable<PlanRegistration> BoundaryRows(TimePlanningPnDbContext db)
```

(The body, `=> db.PlanRegistrations.Where(x => x.Reconciled).Where(x => x.WorkflowState != Constants.WorkflowStates.Removed);`, stays the same.)

- [ ] **Step 4: Create the response model**

Create `P/TimePlanning.Pn/Infrastructure/Models/Reconciliation/ReconciliationSummaryModel.cs`:

```csharp
#nullable enable
namespace TimePlanning.Pn.Infrastructure.Models.Reconciliation;

using System;

/// <summary>
/// GET api/time-planning-pn/reconciliation/summary. Consumed by my-microting's
/// customer-stats scan, so the wire shape is a contract: the host serialises
/// with Newtonsoft + camelCase. Dates are pre-formatted "yyyy-MM-dd" strings
/// so no serializer setting can turn them into instants.
/// </summary>
public class ReconciliationSummaryModel
{
    /// <summary>The cutoff actually used, clamped to 1..31 (19 when no settings row).</summary>
    public int CutoffDay { get; set; }

    /// <summary>yyyy-MM-dd, inclusive.</summary>
    public string PeriodStart { get; set; } = "";

    /// <summary>yyyy-MM-dd, inclusive; always before today (UTC).</summary>
    public string PeriodEnd { get; set; } = "";

    /// <summary>Workers with planned or worked hours on a live row in the period.</summary>
    public int WorkersInPeriod { get; set; }

    /// <summary>Of those, workers whose boundary is on or after PeriodEnd.</summary>
    public int WorkersLockedThroughPeriod { get; set; }

    /// <summary>Of those, workers with no boundary at all.</summary>
    public int WorkersNeverReconciled { get; set; }

    /// <summary>100 * locked / inPeriod, 1 decimal; null when WorkersInPeriod is 0.</summary>
    public double? CoveragePercent { get; set; }

    /// <summary>yyyy-MM-dd, earliest non-null boundary among WorkersInPeriod; null if none.</summary>
    public string? OldestBoundary { get; set; }

    /// <summary>Distinct workers with any live reconciled row, in any period (adoption).</summary>
    public int WorkersWithAnyReconciled { get; set; }

    /// <summary>Latest ReconciledAt over live reconciled rows; Kind Utc so the JSON ends in "Z".</summary>
    public DateTime? LastReconciledAt { get; set; }
}
```

- [ ] **Step 5: Create the interface**

Create `P/TimePlanning.Pn/Services/ReconciliationSummaryService/IReconciliationSummaryService.cs`:

```csharp
#nullable enable
namespace TimePlanning.Pn.Services.ReconciliationSummaryService;

using System.Threading.Tasks;
using Infrastructure.Models.Reconciliation;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;

public interface IReconciliationSummaryService
{
    /// <summary>Summary for the last closed payroll period as of DateTime.UtcNow.Date.</summary>
    Task<OperationDataResult<ReconciliationSummaryModel>> GetSummaryAsync();
}
```

- [ ] **Step 6: Create the service**

Create `P/TimePlanning.Pn/Services/ReconciliationSummaryService/ReconciliationSummaryService.cs`:

```csharp
#nullable enable
namespace TimePlanning.Pn.Services.ReconciliationSummaryService;

using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Infrastructure.Helpers;
using Infrastructure.Models.Reconciliation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microting.eForm.Infrastructure.Constants;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;
using Microting.TimePlanningBase.Infrastructure.Data;

/// <summary>
/// Read-only Afstem statistics for the last closed payroll period (spec §3).
/// Lock semantics come exclusively from DayLockHelper; nothing here decides
/// what "locked" means.
/// </summary>
public class ReconciliationSummaryService(
    TimePlanningPnDbContext dbContext,
    ILogger<ReconciliationSummaryService> logger) : IReconciliationSummaryService
{
    /// <summary>Matches the PayrollIntegrationSettings.CutoffDay entity default.</summary>
    public const int DefaultCutoffDay = 19;

    public const string ErrorMessage = "ErrorWhileReadingReconciliationSummary";

    private const string DateFormat = "yyyy-MM-dd";

    public Task<OperationDataResult<ReconciliationSummaryModel>> GetSummaryAsync()
        => GetSummaryAsync(DateTime.UtcNow.Date);

    /// <summary>Test seam: "today" injected so the period is deterministic.</summary>
    internal async Task<OperationDataResult<ReconciliationSummaryModel>> GetSummaryAsync(DateTime todayUtc)
    {
        try
        {
            var configuredCutoff = await dbContext.PayrollIntegrationSettings
                .AsNoTracking()
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .OrderBy(x => x.Id)
                .Select(x => (int?)x.CutoffDay)
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);
            var cutoffDay = Math.Clamp(configuredCutoff ?? DefaultCutoffDay, 1, 31);

            var period = PayrollPeriod.LastClosed(todayUtc, cutoffDay);
            var dayAfterEnd = period.End.AddDays(1);

            // Planned OR worked, in the one-minute-interval representation only:
            // every customer runs UseOneMinuteIntervals=true, so the seconds
            // columns and the exact Start1StartedAt stamp are authoritative. The
            // legacy 5-minute doubles (PlanHours/NettoHours) are deliberately
            // NOT consulted.
            var siteIds = await dbContext.PlanRegistrations
                .AsNoTracking()
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .Where(x => x.Date >= period.Start && x.Date < dayAfterEnd)
                .Where(x => x.PlanHoursInSeconds > 0 || x.NettoHoursInSeconds > 0
                            || x.Start1StartedAt != null)
                .Select(x => x.SdkSitId)
                .Distinct()
                .ToListAsync()
                .ConfigureAwait(false);

            var boundaries = await DayLockHelper.LockedThroughForSitesAsync(dbContext, siteIds)
                .ConfigureAwait(false);

            var locked = boundaries.Values.Count(b => DayLockHelper.IsLocked(b, period.End));
            var never = boundaries.Values.Count(b => b is null);
            var oldest = boundaries.Values.Min(); // Min over DateTime? skips nulls; null when none.

            var reconciledRows = DayLockHelper.BoundaryRows(dbContext);
            var withAny = await reconciledRows
                .Select(x => x.SdkSitId)
                .Distinct()
                .CountAsync()
                .ConfigureAwait(false);
            var lastReconciledAt = await reconciledRows
                .MaxAsync(x => x.ReconciledAt)
                .ConfigureAwait(false);

            var model = new ReconciliationSummaryModel
            {
                CutoffDay = cutoffDay,
                PeriodStart = period.Start.ToString(DateFormat, CultureInfo.InvariantCulture),
                PeriodEnd = period.End.ToString(DateFormat, CultureInfo.InvariantCulture),
                WorkersInPeriod = siteIds.Count,
                WorkersLockedThroughPeriod = locked,
                WorkersNeverReconciled = never,
                CoveragePercent = siteIds.Count == 0
                    ? null
                    : Math.Round(100.0 * locked / siteIds.Count, 1, MidpointRounding.AwayFromZero),
                OldestBoundary = oldest?.ToString(DateFormat, CultureInfo.InvariantCulture),
                WorkersWithAnyReconciled = withAny,
                // datetime(6) has no offset, so EF returns Kind Unspecified;
                // SetReconciledAsync writes UtcNow, so re-tag it Utc to put the
                // "Z" on the wire (same as PlanRegistrationHelper's ReconciledAt).
                LastReconciledAt = lastReconciledAt is { } at
                    ? DateTime.SpecifyKind(at, DateTimeKind.Utc)
                    : null,
            };

            return new OperationDataResult<ReconciliationSummaryModel>(true, model);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ReconciliationSummaryService.GetSummaryAsync: catch");
            return new OperationDataResult<ReconciliationSummaryModel>(false, ErrorMessage);
        }
    }
}
```

- [ ] **Step 7: Register the fixture in both CI workflows (shard `a`)**

This is a DB-backed fixture, so keep it out of shard `c`, which is DB-dense. Before running the command, check the per-shard durations of the last green `stable` run (`gh run list --workflow dotnet-core-master.yml --branch stable --limit 1`, then `gh run view <id>`). If `a` is not among the lighter shards, swap `a` for a lighter shard's last entry. Shard `a` ends with `PauseIdCorrectionTests"` in both files:

```bash
sed -i 's/FullyQualifiedName=TimePlanning\.Pn\.Test\.PauseIdCorrectionTests"$/FullyQualifiedName=TimePlanning.Pn.Test.PauseIdCorrectionTests|FullyQualifiedName=TimePlanning.Pn.Test.ReconciliationSummaryServiceTests"/' \
  .github/workflows/dotnet-core-pr.yml .github/workflows/dotnet-core-master.yml
grep -c "TimePlanning.Pn.Test.ReconciliationSummaryServiceTests" .github/workflows/dotnet-core-pr.yml .github/workflows/dotnet-core-master.yml
```

Expected: each file reports `1`.

- [ ] **Step 8: Build, then run the tests**

Run: `dotnet build eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn.sln`
Expected: Build succeeded, no new warnings in the new files.

Run (Docker required): `dotnet test eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn.Test/TimePlanning.Pn.Test.csproj --settings eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn.Test/test.runsettings --filter "FullyQualifiedName=TimePlanning.Pn.Test.ReconciliationSummaryServiceTests|FullyQualifiedName=TimePlanning.Pn.Test.DayLockHelperTests|FullyQualifiedName=TimePlanning.Pn.Test.ShardCoverageTests"`
Expected: all PASS (12 new tests; `DayLockHelperTests` stay green after the visibility change).
If there is no Docker, run only `ShardCoverageTests` and record "ReconciliationSummaryServiceTests: not run locally, CI only".

- [ ] **Step 9: Commit**

```bash
git add eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn/Infrastructure/Models/Reconciliation/ReconciliationSummaryModel.cs \
        eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn/Services/ReconciliationSummaryService/IReconciliationSummaryService.cs \
        eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn/Services/ReconciliationSummaryService/ReconciliationSummaryService.cs \
        eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn/Infrastructure/Helpers/DayLockHelper.cs \
        eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn.Test/ReconciliationSummaryServiceTests.cs \
        .github/workflows/dotnet-core-pr.yml .github/workflows/dotnet-core-master.yml
git commit -m "$(cat <<'EOF'
feat(reconciliation): summary service for the last closed payroll period

Counts workers with hours in the period, splits them into locked / behind /
never via DayLockHelper.LockedThroughForSitesAsync, and reports adoption
(workers with any reconciled day, latest ReconciledAt as UTC).

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 3: Controller, DI registration, and wire-contract tests

**Files:**
- Create: `P/TimePlanning.Pn/Controllers/ReconciliationSummaryController.cs`
- Modify: `P/TimePlanning.Pn/EformTimePlanningPlugin.cs` (the using block around l.41–43; `ConfigureServices` around l.114–116)
- Test: `P/TimePlanning.Pn.Test/ReconciliationSummaryContractTests.cs`
- Modify: `.github/workflows/dotnet-core-pr.yml`, `.github/workflows/dotnet-core-master.yml` (shard `g`)

**Interfaces:**
- Consumes: `IReconciliationSummaryService.GetSummaryAsync()` and `ReconciliationSummaryModel` (Task 2).
- Produces: `ReconciliationSummaryController` with `[Authorize]`, `[Route("api/time-planning-pn/reconciliation")]`, and `[HttpGet("summary")] Task<OperationDataResult<ReconciliationSummaryModel>> Summary()`.

- [ ] **Step 1: Write the failing tests**

Create `P/TimePlanning.Pn.Test/ReconciliationSummaryContractTests.cs`:

```csharp
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NSubstitute;
using NUnit.Framework;
using TimePlanning.Pn.Controllers;
using TimePlanning.Pn.Infrastructure.Models.Reconciliation;
using TimePlanning.Pn.Services.ReconciliationSummaryService;

namespace TimePlanning.Pn.Test;

/// <summary>
/// Pins the HTTP contract my-microting's scan depends on. No database.
/// The serializer settings mirror the host (eFormAPI.Web ServiceCollectionExtensions:
/// AddNewtonsoftJson with only CamelCasePropertyNamesContractResolver set,
/// so DateTimeZoneHandling stays RoundtripKind and dates are ISO).
/// </summary>
[TestFixture]
public class ReconciliationSummaryContractTests
{
    private static string Serialize(object value) => JsonConvert.SerializeObject(value,
        new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() });

    [Test]
    public void WireShape_FullModel()
    {
        var model = new ReconciliationSummaryModel
        {
            CutoffDay = 19,
            PeriodStart = "2026-07-20",
            PeriodEnd = "2026-08-19",
            WorkersInPeriod = 10,
            WorkersLockedThroughPeriod = 7,
            WorkersNeverReconciled = 1,
            CoveragePercent = 70.0,
            OldestBoundary = "2026-06-30",
            WorkersWithAnyReconciled = 9,
            LastReconciledAt = DateTime.SpecifyKind(new DateTime(2026, 9, 18, 12, 32, 0), DateTimeKind.Utc),
        };

        var json = Serialize(new OperationDataResult<ReconciliationSummaryModel>(true, model));

        Assert.Multiple(() =>
        {
            Assert.That(json, Does.Contain("\"success\":true"));
            Assert.That(json, Does.Contain("\"message\":\"Success\""));
            Assert.That(json, Does.Contain("\"model\":{"));
            Assert.That(json, Does.Contain("\"cutoffDay\":19"));
            Assert.That(json, Does.Contain("\"periodStart\":\"2026-07-20\""));
            Assert.That(json, Does.Contain("\"periodEnd\":\"2026-08-19\""));
            Assert.That(json, Does.Contain("\"workersInPeriod\":10"));
            Assert.That(json, Does.Contain("\"workersLockedThroughPeriod\":7"));
            Assert.That(json, Does.Contain("\"workersNeverReconciled\":1"));
            Assert.That(json, Does.Contain("\"coveragePercent\":70.0"));
            Assert.That(json, Does.Contain("\"oldestBoundary\":\"2026-06-30\""));
            Assert.That(json, Does.Contain("\"workersWithAnyReconciled\":9"));
            Assert.That(json, Does.Contain("\"lastReconciledAt\":\"2026-09-18T12:32:00Z\""));
            Assert.That(json, Does.Not.Contain("workersBehind"), "derived by the consumer, not sent");
        });
    }

    [Test]
    public void WireShape_LastReconciledAtKeepsFractionalSecondsAndZ()
    {
        var model = new ReconciliationSummaryModel
        {
            LastReconciledAt = DateTime.SpecifyKind(
                new DateTime(2026, 9, 18, 12, 32, 0).AddTicks(1_234_560), DateTimeKind.Utc),
        };

        Assert.That(Serialize(model), Does.Contain("\"lastReconciledAt\":\"2026-09-18T12:32:00.123456Z\""));
    }

    [Test]
    public void WireShape_NullsAreWrittenExplicitly()
    {
        var json = Serialize(new ReconciliationSummaryModel { PeriodStart = "2026-08-20", PeriodEnd = "2026-09-19" });

        Assert.Multiple(() =>
        {
            Assert.That(json, Does.Contain("\"coveragePercent\":null"));
            Assert.That(json, Does.Contain("\"oldestBoundary\":null"));
            Assert.That(json, Does.Contain("\"lastReconciledAt\":null"));
        });
    }

    [Test]
    public void WireShape_Failure()
    {
        var json = Serialize(new OperationDataResult<ReconciliationSummaryModel>(
            false, ReconciliationSummaryService.ErrorMessage));

        Assert.Multiple(() =>
        {
            Assert.That(json, Does.Contain("\"success\":false"));
            Assert.That(json, Does.Contain("\"message\":\"ErrorWhileReadingReconciliationSummary\""));
            Assert.That(json, Does.Contain("\"model\":null"));
        });
    }

    [Test]
    public void Controller_RouteAndAuthorization()
    {
        var type = typeof(ReconciliationSummaryController);
        var route = type.GetCustomAttribute<RouteAttribute>();
        var authorize = type.GetCustomAttribute<AuthorizeAttribute>();
        var get = type.GetMethod(nameof(ReconciliationSummaryController.Summary))!
            .GetCustomAttribute<HttpGetAttribute>();

        Assert.Multiple(() =>
        {
            Assert.That(route?.Template, Is.EqualTo("api/time-planning-pn/reconciliation"));
            Assert.That(authorize, Is.Not.Null, "must require an authenticated caller");
            Assert.That(get?.Template, Is.EqualTo("summary"));
            Assert.That(type.GetMethods().Count(m => m.GetCustomAttributes<HttpMethodAttribute>().Any(a =>
                a.HttpMethods.Any(h => h != "GET"))), Is.Zero, "read-only: no write verbs");
        });
    }

    [Test]
    public async Task Controller_ReturnsTheServiceResultUnchanged()
    {
        var expected = new OperationDataResult<ReconciliationSummaryModel>(true, new ReconciliationSummaryModel());
        var service = Substitute.For<IReconciliationSummaryService>();
        service.GetSummaryAsync().Returns(expected);

        var actual = await new ReconciliationSummaryController(service).Summary();

        Assert.That(actual, Is.SameAs(expected));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet build eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn.sln`
Expected: FAILS with `CS0246: The type or namespace name 'ReconciliationSummaryController' could not be found`.
(If it instead fails on `Newtonsoft.Json` not found, add `<PackageReference Include="Newtonsoft.Json" Version="13.0.4" />` to `P/TimePlanning.Pn.Test/TimePlanning.Pn.Test.csproj`. It currently arrives transitively as 13.0.4. Then stage that csproj in Step 7.)

- [ ] **Step 3: Create the controller**

Create `P/TimePlanning.Pn/Controllers/ReconciliationSummaryController.cs`:

```csharp
#nullable enable
namespace TimePlanning.Pn.Controllers;

using System.Threading.Tasks;
using Infrastructure.Models.Reconciliation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;
using Services.ReconciliationSummaryService;

/// <summary>
/// Read-only Afstem statistics, polled by my-microting's customer-stats scan
/// with the service login. See ReconciliationSummaryModel for the wire shape.
/// </summary>
[Authorize]
[Route("api/time-planning-pn/reconciliation")]
public class ReconciliationSummaryController(IReconciliationSummaryService reconciliationSummaryService) : Controller
{
    [HttpGet("summary")]
    public Task<OperationDataResult<ReconciliationSummaryModel>> Summary()
        => reconciliationSummaryService.GetSummaryAsync();
}
```

- [ ] **Step 4: Register the service in DI**

In `P/TimePlanning.Pn/EformTimePlanningPlugin.cs`, add this using next to the other `TimePlanning.Pn.Services.*` usings (after `using TimePlanning.Pn.Services.PushNotificationService;`):

```csharp
using TimePlanning.Pn.Services.ReconciliationSummaryService;
```

and in `ConfigureServices`, directly after `services.AddTransient<IPayrollExportService, PayrollExportService>();`:

```csharp
        services.AddTransient<IReconciliationSummaryService, ReconciliationSummaryService>();
```

(Transient, the same lifetime as `PayrollExportService`. `TimePlanningPnDbContext` comes from the existing `AddDbContextPool` registration.)

- [ ] **Step 5: Register the fixture in both CI workflows (shard `g`)**

After Task 1, shard `g` ends with `PayrollPeriodTests"`:

```bash
sed -i 's/FullyQualifiedName=TimePlanning\.Pn\.Test\.PayrollPeriodTests"$/FullyQualifiedName=TimePlanning.Pn.Test.PayrollPeriodTests|FullyQualifiedName=TimePlanning.Pn.Test.ReconciliationSummaryContractTests"/' \
  .github/workflows/dotnet-core-pr.yml .github/workflows/dotnet-core-master.yml
grep -c "TimePlanning.Pn.Test.ReconciliationSummaryContractTests" .github/workflows/dotnet-core-pr.yml .github/workflows/dotnet-core-master.yml
```

Expected: each file reports `1`.

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn.Test/TimePlanning.Pn.Test.csproj --filter "FullyQualifiedName=TimePlanning.Pn.Test.ReconciliationSummaryContractTests|FullyQualifiedName=TimePlanning.Pn.Test.PayrollPeriodTests|FullyQualifiedName=TimePlanning.Pn.Test.ShardCoverageTests"`
Expected: all PASS (6 contract tests + Task 1's tests + the shard guard).

- [ ] **Step 7: Commit**

```bash
git add eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn/Controllers/ReconciliationSummaryController.cs \
        eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn/EformTimePlanningPlugin.cs \
        eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn.Test/ReconciliationSummaryContractTests.cs \
        .github/workflows/dotnet-core-pr.yml .github/workflows/dotnet-core-master.yml
git commit -m "$(cat <<'EOF'
feat(reconciliation): GET api/time-planning-pn/reconciliation/summary

Authorized, read-only controller over IReconciliationSummaryService, wrapped
in OperationDataResult like every other endpoint. Contract tests pin the
camelCase wire shape, yyyy-MM-dd dates and the UTC "Z" on lastReconciledAt.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 4: Verify, review, PR, and CI

**Files:** none are new. Review fixes land in the files from Tasks 1–3.

**Interfaces:**
- Consumes: everything from Tasks 1–3.
- Produces: an open PR toward `stable` with every check green or classified.

- [ ] **Step 1: Full build and local test pass**

```bash
dotnet build eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn.sln
dotnet test eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn.Test/TimePlanning.Pn.Test.csproj \
  --filter "FullyQualifiedName=TimePlanning.Pn.Test.PayrollPeriodTests|FullyQualifiedName=TimePlanning.Pn.Test.ReconciliationSummaryContractTests|FullyQualifiedName=TimePlanning.Pn.Test.ShardCoverageTests"
```

Expected: build succeeds and all tests pass. Write down explicitly whether `ReconciliationSummaryServiceTests` ran locally (Docker) or is CI-only.

- [ ] **Step 2: Optional manual smoke test (a dev host is running)**

```bash
curl -s -H "Authorization: Bearer <token from POST /api/auth/token>" \
  http://localhost:5000/api/time-planning-pn/reconciliation/summary
```

Expected: JSON matching the Wire contract section. Tell the user this needs a browser/host check. It is not verified by the automated tests.

- [ ] **Step 3: Dual review gate (mandatory, in parallel)**

Dispatch `superpowers:requesting-code-review` and a `code-simplifier` subagent in the same message, over `git diff origin/stable...HEAD`. Act on their findings. Commit fixes by file name, with a message ending in `Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>`.

- [ ] **Step 4: Push and open the PR**

```bash
git push -u origin feat/reconciliation-summary
gh pr create --base stable --head feat/reconciliation-summary \
  --title "feat(reconciliation): GET reconciliation/summary for Afstem stats" \
  --body "$(cat <<'EOF'
Adds `GET /api/time-planning-pn/reconciliation/summary` (spec: my-microting `docs/superpowers/specs/2026-09-19-afstem-stats-design.md` §3).

- `PayrollPeriod.LastClosed` (pure) picks the last closed monthly period from `PayrollIntegrationSettings.CutoffDay` (default 19).
- Workers with planned or worked hours in the period are split into locked / behind / never via `DayLockHelper.LockedThroughForSitesAsync`.
- Adoption: workers with any reconciled day, and the latest `ReconciledAt` (UTC, `Z`).
- Hours filter: one-minute-interval fields only (`*InSeconds` > 0 or a `Start1StartedAt` stamp); the legacy doubles are not consulted (user decision).

Tests: `PayrollPeriodTests`, `ReconciliationSummaryContractTests` (no DB), `ReconciliationSummaryServiceTests` (Testcontainers, CI). All are added to both workflows' shard filters.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

- [ ] **Step 5: Watch CI to a verdict**

Run `gh pr checks <n>` until every check has finished. For each red check, find the step that failed (`gh api repos/microting/eform-angular-timeplanning-plugin/actions/jobs/<id> --jq '.steps[]|select(.conclusion=="failure")'`) and classify it as infrastructure or a real test failure. Compare it with the latest `stable` run: a shard that is also red on `stable` is not caused by this PR. Pay particular attention to the shard that now holds `ReconciliationSummaryServiceTests` (`a`, or whichever you picked), because that is the first time those tests execute. Do not report the PR as ready while any check is red or unclassified.

---

## Self-review

- **Spec coverage (§3):**
  - Route, `[Authorize]`, read-only: Task 3.
  - New controller + service: Tasks 2 and 3.
  - Settings row / default 19 / clamp: Task 2 (tests `NoSettingsRow…`, `SettingsRow…`, `RemovedSettingsRow…`, `…OutOfRange…`).
  - Period rule and all examples: Task 1.
  - Worker selection (removed ignored, no-hours ignored, inclusive edges): Task 2 `Counts_…`.
  - Every response field: Task 2 plus the Task 3 wire tests.
  - Boundaries from `LockedThroughForSitesAsync`: Task 2.
  - No `workersBehind`: Task 3 test.
  - Date formats: Task 3 tests.
- **Spec coverage (§7 timeplanning):**
  - `LastClosed` cases (either side of 19, Feb/leap Feb, cutoff 1, out-of-range, January): Task 1.
  - Counts per worker, locked/behind/never, removed rows, no-hours rows, fallback to 19, 0 workers → null coverage, `lastReconciledAt` max and UTC, `workersWithAnyReconciled` outside the period: Task 2.
- **Placeholders:** none. The one decision left to the executor is the shard choice in Task 2 Step 7, which comes with a default (`a`) and a check to run.
- **Type consistency:** `ReconciliationSummaryModel` property names and types match across Tasks 2 and 3 and the Wire contract. `GetSummaryAsync()` / `GetSummaryAsync(DateTime)`, `ErrorMessage`, and `DefaultCutoffDay` are used consistently. `PayrollPeriod.Start`/`End` match between Tasks 1 and 2.
