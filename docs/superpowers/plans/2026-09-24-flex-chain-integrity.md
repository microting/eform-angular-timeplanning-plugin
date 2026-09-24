# Flex Chain Integrity Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stop the time-planning code from creating new flex-balance damage (open shifts, ignored office corrections, mode re-stamping) and make every write carry the running balance forward to the worker's last row.

**Architecture:** Three additions to the shared base package (`FlexChain.ComputeNettoMinutesFlagOff`, the balance-only step `FlexChain.CarryChain`, and `FlexChainRecompute.RunForwardAsync` plus a shared `DayLock` rule), released as 10.0.65. The plugin and the service then switch their write paths to call them. A forward walk never recomputes worked hours — it only carries Flex/SumFlexStart/SumFlexEnd from the stored hours.

**Tech Stack:** C# / .NET 10, EF Core (Pomelo MariaDB), NUnit 4, Testcontainers MariaDB (plugin, service), local MariaDB service container (base), GitHub Actions.

**Spec:** `docs/superpowers/specs/2026-09-24-flex-chain-integrity-design.md` (plugin repo)

## Global Constraints

- Dev mode: **none** — edit the source repos directly; every change on a feature branch off the repo's target (`eform-timeplanning-base` → `master`; plugin and service → `stable`), PR in, never commit to the target.
- **Every existing test must pass unchanged** (spec §3). If a change makes an existing test fail, STOP and report — never edit the test.
- `FlexChainCatchUpJob` and `FlexChain.ApplyNettoFlexChainSecondPrecision`'s observable behaviour must not change.
- Stamp clearing (B2) only in the plugin's grid `TimePlanningWorkingHoursService.UpdatePlanning`.
- Base package version for this work: **10.0.65** (plugin and service currently pin 10.0.64).
- New plugin test classes: add to exactly one shard, same letter, in BOTH `.github/workflows/dotnet-core-pr.yml` and `dotnet-core-master.yml` (enforced by `ShardCoverageTests`).
- Tests run in CI only (base needs a MariaDB on :3306; plugin/service need Docker). Locally: `dotnet build` must pass; state plainly that tests were not run locally.
- Mandatory before every commit: dual review — `superpowers:requesting-code-review` AND a `code-simplifier` subagent, dispatched in parallel; act on findings.
- Stage files by name; never `git add .` / `git commit -a`. Commit messages end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`; PR bodies end with `🤖 Generated with [Claude Code](https://claude.com/claude-code)`.
- No real people's or customers' data (names, emails, tenant ids) in code, tests, commits or PR text — use fictional values.
- After each PR: watch CI to a verdict (`gh pr checks <n>`), classify every red check (infra vs real; compare with target branch's latest run). Merging a PR and pushing a release tag are outward actions — ask the user before each.

## Review Focus

1. **A worker's first row carries an opening balance** (`SumFlexStart` set via the Flex tab) and the walk starts on it — there is no predecessor; expected: the stored opening balance is kept, not reset to 0 (pinned in Task B4).
2. **A removed row between two live rows** — must be neither walked nor used as a seed (pinned in Task B4).
3. **The start date sits on or before the reconciled boundary** — the walk must seed from the boundary row's stored balance and never write a locked row (pinned in Task B4).
4. **A one-minute predecessor whose seconds columns are 0 but whose decimal balance is not** (legacy rows) — must seed from the decimal (pinned in Task B2).
5. **Two rows on the same date / chain already consistent** — same-date rows chain in `Id` order; a consistent chain gets zero writes, `Version` unchanged, no version rows (pinned in Task B4).

---

## Part 1 — Base package (`eform-timeplanning-base`, PR 1)

Repo: `/home/rene/Documents/workspace/microting/eform-timeplanning-base`. Branch: `feat/flex-chain-forward-walk` off `origin/master`.

Setup (once, before Task B1):

```bash
cd /home/rene/Documents/workspace/microting/eform-timeplanning-base
git fetch -q origin && git checkout -b feat/flex-chain-forward-walk origin/master
dotnet build Microting.TimePlanningBase.Tests/Microting.TimePlanningBase.Tests.csproj
```
Expected: build succeeds (baseline).

### Task 1 (B1): Open-shift-safe five-minute hours — `FlexChain.ComputeNettoMinutesFlagOff`

**Files:**
- Modify: `Microting.TimePlanningBase/Infrastructure/Helpers/FlexChain.cs` (insert after `ComputeNettoSecondsFromDateTimeShifts`, i.e. after line 335)
- Test: `Microting.TimePlanningBase.Tests/FlexChainUTest.cs` (append tests inside the class)

**Interfaces:**
- Consumes: `FlexChain.ComputeShiftPauseSeconds(PlanRegistration, int, bool)` (existing).
- Produces: `public static double FlexChain.ComputeNettoMinutesFlagOff(PlanRegistration pr)` — five-minute netto in **minutes**.

- [ ] **Step 1: Write the failing tests** (append inside `FlexChainUTest`)

```csharp
    [Test]
    public void ComputeNettoMinutesFlagOff_CountsAClosedShiftMinusItsPause()
    {
        // 07:00 (id 85) to 15:00 (id 181) = 96 ticks = 480 min; Pause1Id 7 = 30 min
        var pr = new PlanRegistration { Start1Id = 85, Stop1Id = 181, Pause1Id = 7 };

        Assert.That(FlexChain.ComputeNettoMinutesFlagOff(pr), Is.EqualTo(450));
    }

    [Test]
    public void ComputeNettoMinutesFlagOff_OpenShiftCountsZeroAndIgnoresItsPause()
    {
        var pr = new PlanRegistration { Start1Id = 115, Stop1Id = 0, Pause1Id = 7 };

        Assert.That(FlexChain.ComputeNettoMinutesFlagOff(pr), Is.EqualTo(0));
    }

    [Test]
    public void ComputeNettoMinutesFlagOff_StopBeforeStartCountsZero()
    {
        var pr = new PlanRegistration { Start1Id = 181, Stop1Id = 85, Pause1Id = 1 };

        Assert.That(FlexChain.ComputeNettoMinutesFlagOff(pr), Is.EqualTo(0));
    }

    [Test]
    public void ComputeNettoMinutesFlagOff_OneOpenShiftDoesNotCancelAClosedOne()
    {
        var pr = new PlanRegistration
        {
            Start1Id = 85, Stop1Id = 133, Pause1Id = 1,   // 4 h, no pause
            Start2Id = 157, Stop2Id = 0, Pause2Id = 4     // open second shift
        };

        Assert.That(FlexChain.ComputeNettoMinutesFlagOff(pr), Is.EqualTo(240));
    }

    [Test]
    public void ComputeNettoMinutesFlagOff_SumsShiftsThreeToFive()
    {
        var pr = new PlanRegistration
        {
            Start3Id = 13, Stop3Id = 25,   // 60 min
            Start4Id = 37, Stop4Id = 43,   // 30 min
            Start5Id = 61, Stop5Id = 64    // 15 min
        };

        Assert.That(FlexChain.ComputeNettoMinutesFlagOff(pr), Is.EqualTo(105));
    }
```

- [ ] **Step 2: Build to verify it fails**

Run: `dotnet build Microting.TimePlanningBase.Tests/Microting.TimePlanningBase.Tests.csproj`
Expected: FAIL — `CS0117: 'FlexChain' does not contain a definition for 'ComputeNettoMinutesFlagOff'`.

- [ ] **Step 3: Implement** (insert after the closing brace of `ComputeNettoSecondsFromDateTimeShifts`)

```csharp
    /// <summary>
    /// The FIVE-MINUTE (flag-off) netto in MINUTES — the flag-off twin of
    /// <see cref="ComputeNettoSecondsFromDateTimeShifts"/> and the one copy the
    /// plugin and the service share. Per shift 1..5 the work span in 5-minute
    /// ticks minus the canonical shift pause (<see cref="ComputeShiftPauseSeconds"/>
    /// with the clock-tick rule).
    ///
    /// A shift with a start but no stop (StopId 0), or a stop before its start,
    /// contributes NOTHING — neither work nor pause. With no end the day's
    /// worked time cannot be known, so it counts 0, never negative.
    /// </summary>
    public static double ComputeNettoMinutesFlagOff(PlanRegistration pr)
    {
        const int minutesPerTick = 5;

        double ShiftMinutes(int shift, int startId, int stopId)
        {
            if (stopId == 0 || stopId < startId)
            {
                return 0;
            }

            return (stopId - startId) * minutesPerTick
                   - ComputeShiftPauseSeconds(pr, shift, useOneMinuteIntervals: false) / 60.0;
        }

        return ShiftMinutes(1, pr.Start1Id, pr.Stop1Id)
               + ShiftMinutes(2, pr.Start2Id, pr.Stop2Id)
               + ShiftMinutes(3, pr.Start3Id, pr.Stop3Id)
               + ShiftMinutes(4, pr.Start4Id, pr.Stop4Id)
               + ShiftMinutes(5, pr.Start5Id, pr.Stop5Id);
    }
```

- [ ] **Step 4: Build to verify it compiles**

Run: `dotnet build Microting.TimePlanningBase.Tests/Microting.TimePlanningBase.Tests.csproj`
Expected: Build succeeded. 

Tests execute in CI only (local `dotnet test` is blocked by a hook, by design). Verify with the build above; the PR's CI run is the first execution.

- [ ] **Step 5: Commit** (after the dual review gate)

```bash
git add Microting.TimePlanningBase/Infrastructure/Helpers/FlexChain.cs Microting.TimePlanningBase.Tests/FlexChainUTest.cs
git commit -m "feat(flex-chain): open-shift-safe five-minute netto shared by plugin and service

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 2 (B2): Balance-only step — `FlexChain.CarryChain`

**Files:**
- Modify: `Microting.TimePlanningBase/Infrastructure/Helpers/FlexChain.cs:226-268` (extract the seconds-chain arithmetic) and add `CarryChain` after `ApplyNettoFlexChainSecondPrecision`
- Test: `Microting.TimePlanningBase.Tests/FlexChainUTest.cs`

**Interfaces:**
- Consumes: `SecondsOrDecimalFallback`, `SumFlexEndSecondsWithFallback`, `ApplyNettoFlexChainDecimal` (existing).
- Produces: `public static void FlexChain.CarryChain(PlanRegistration pr, PlanRegistration? predecessor, bool rowIsOneMinute, bool? predecessorIsOneMinute)` — writes `Flex`, `SumFlexStart`, `SumFlexEnd` and (one-minute) `FlexInSeconds`, `SumFlexStartInSeconds`, `SumFlexEndInSeconds`; (five-minute) clears the SumFlex seconds. **Never writes `NettoHours`/`NettoHoursInSeconds`.**

- [ ] **Step 1: Write the failing tests**

```csharp
    [Test]
    public void CarryChain_OneMinute_UsesStoredSecondsAndIgnoresStamps()
    {
        var pre = new PlanRegistration { SumFlexEnd = 2.0, SumFlexEndInSeconds = 7200 };
        var pr = new PlanRegistration
        {
            NettoHoursInSeconds = 28800, NettoHours = 8.0,            // stored 8 h
            PlanHours = 7.5, PlanHoursInSeconds = 27000,
            Start1StartedAt = new System.DateTime(2026, 1, 5, 12, 0, 0),  // stale stamps: 1 h
            Stop1StoppedAt = new System.DateTime(2026, 1, 5, 13, 0, 0)
        };

        FlexChain.CarryChain(pr, pre, rowIsOneMinute: true, predecessorIsOneMinute: true);

        Assert.Multiple(() =>
        {
            Assert.That(pr.NettoHoursInSeconds, Is.EqualTo(28800), "hours are never recomputed");
            Assert.That(pr.NettoHours, Is.EqualTo(8.0));
            Assert.That(pr.FlexInSeconds, Is.EqualTo(1800));
            Assert.That(pr.SumFlexStartInSeconds, Is.EqualTo(7200));
            Assert.That(pr.SumFlexEndInSeconds, Is.EqualTo(9000));
            Assert.That(pr.SumFlexEnd, Is.EqualTo(2.5));
        });
    }

    [Test]
    public void CarryChain_OneMinute_FallsBackToDecimalsWhenSecondsAreZero()
    {
        // legacy predecessor: seconds 0, decimal balance 10 h
        var pre = new PlanRegistration { SumFlexEnd = 10.0, SumFlexEndInSeconds = 0 };
        var pr = new PlanRegistration { NettoHours = 7.0, NettoHoursInSeconds = 0, PlanHours = 7.5 };

        FlexChain.CarryChain(pr, pre, rowIsOneMinute: true, predecessorIsOneMinute: true);

        Assert.Multiple(() =>
        {
            Assert.That(pr.SumFlexStartInSeconds, Is.EqualTo(36000));
            Assert.That(pr.SumFlexEndInSeconds, Is.EqualTo(34200));
            Assert.That(pr.NettoHoursInSeconds, Is.EqualTo(0), "the fallback is read-only");
        });
    }

    [Test]
    public void CarryChain_OneMinute_UsesTheOverrideWhenActive()
    {
        var pre = new PlanRegistration { SumFlexEnd = 0, SumFlexEndInSeconds = 0 };
        var pr = new PlanRegistration
        {
            NettoHoursInSeconds = 28800, NettoHours = 8.0,
            NettoHoursOverrideActive = true, NettoHoursOverride = 10.0,
            PlanHours = 8.0
        };

        FlexChain.CarryChain(pr, pre, rowIsOneMinute: true, predecessorIsOneMinute: true);

        Assert.Multiple(() =>
        {
            Assert.That(pr.FlexInSeconds, Is.EqualTo(7200));
            Assert.That(pr.Flex, Is.EqualTo(2.0));
            Assert.That(pr.SumFlexEndInSeconds / 3600.0, Is.EqualTo(pr.SumFlexEnd).Within(1e-9),
                "decimal and seconds columns stay in step");
        });
    }

    [Test]
    public void CarryChain_FiveMinute_MatchesTheDecimalChainAndClearsSeconds()
    {
        var pre = new PlanRegistration { SumFlexEnd = 12.5, SumFlexEndInSeconds = 45000 };
        var pr = new PlanRegistration
        {
            NettoHours = 8.0, PlanHours = 7.5, PaiedOutFlex = 1.0,
            SumFlexStartInSeconds = 999, SumFlexEndInSeconds = 999
        };

        FlexChain.CarryChain(pr, pre, rowIsOneMinute: false, predecessorIsOneMinute: false);

        Assert.Multiple(() =>
        {
            Assert.That(pr.SumFlexStart, Is.EqualTo(12.5));
            Assert.That(pr.SumFlexEnd, Is.EqualTo(12.0));
            Assert.That(pr.SumFlexEndInSeconds, Is.EqualTo(0));
            Assert.That(pr.NettoHours, Is.EqualTo(8.0));
        });
    }

    [Test]
    public void CarryChain_OneMinuteAfterFiveMinute_SeedsFromTheDecimalNotStaleSeconds()
    {
        var pre = new PlanRegistration { SumFlexEnd = -3.97, SumFlexEndInSeconds = -290456 };
        var pr = new PlanRegistration { NettoHoursInSeconds = 3600, NettoHours = 1.0, PlanHours = 1.0 };

        FlexChain.CarryChain(pr, pre, rowIsOneMinute: true, predecessorIsOneMinute: false);

        Assert.That(pr.SumFlexStartInSeconds, Is.EqualTo(-14292));
    }

    [Test]
    public void CarryChain_FirstRow_StartsAtZero()
    {
        var pr = new PlanRegistration { NettoHoursInSeconds = 3600, NettoHours = 1.0, PlanHours = 0 };

        FlexChain.CarryChain(pr, null, rowIsOneMinute: true, predecessorIsOneMinute: null);

        Assert.Multiple(() =>
        {
            Assert.That(pr.SumFlexStartInSeconds, Is.EqualTo(0));
            Assert.That(pr.SumFlexEndInSeconds, Is.EqualTo(3600));
        });
    }
```

- [ ] **Step 2: Build to verify it fails**

Run: `dotnet build Microting.TimePlanningBase.Tests/Microting.TimePlanningBase.Tests.csproj`
Expected: FAIL — `'FlexChain' does not contain a definition for 'CarryChain'`.

- [ ] **Step 3: Implement.** Replace the body of `ApplyNettoFlexChainSecondPrecision(PlanRegistration pr, int sumFlexStartInSeconds, bool hasPreTimePlanning)` (lines 226-268) so its netto step stays and the chain arithmetic moves into a private helper both methods share (arithmetic unchanged):

```csharp
    public static void ApplyNettoFlexChainSecondPrecision(PlanRegistration pr,
        int sumFlexStartInSeconds, bool hasPreTimePlanning)
    {
        var nettoSeconds = ComputeNettoSecondsFromDateTimeShifts(pr);
        pr.NettoHoursInSeconds = (int)nettoSeconds;
        pr.NettoHours = nettoSeconds / 3600.0;

        WriteSecondsChain(pr, nettoSeconds, sumFlexStartInSeconds, hasPreTimePlanning);
    }

    /// <summary>
    /// The BALANCE-ONLY step of the chain: carries Flex / SumFlexStart /
    /// SumFlexEnd from the row's STORED hours. Never reads stamps or shift ids
    /// and never writes NettoHours / NettoHoursInSeconds — a forward walk that
    /// recomputed hours re-derived whole histories from stale device stamps.
    /// Hours are computed only when that day's own inputs change.
    /// </summary>
    /// <param name="rowIsOneMinute">The row's mode at its own date (OneMinuteModeTimeline).</param>
    /// <param name="predecessorIsOneMinute">
    /// The predecessor's mode, forwarded to <see cref="SumFlexEndSecondsWithFallback"/>.
    /// </param>
    public static void CarryChain(PlanRegistration pr, PlanRegistration? predecessor,
        bool rowIsOneMinute, bool? predecessorIsOneMinute)
    {
        if (!rowIsOneMinute)
        {
            ApplyNettoFlexChainDecimal(pr, predecessor);
            return;
        }

        WriteSecondsChain(
            pr,
            SecondsOrDecimalFallback(pr.NettoHoursInSeconds, pr.NettoHours),
            SumFlexEndSecondsWithFallback(predecessor, predecessorIsOneMinute),
            predecessor != null);
    }

    private static void WriteSecondsChain(PlanRegistration pr, long nettoSeconds,
        int sumFlexStartInSeconds, bool hasPreTimePlanning)
    {
        // Punch-clock / scheduled days and production writers populate only the
        // doubles; the *InSeconds siblings stay 0. See SecondsOrDecimalFallback.
        var planHoursSeconds = SecondsOrDecimalFallback(pr.PlanHoursInSeconds, pr.PlanHours);
        var paiedOutFlexSeconds =
            SecondsOrDecimalFallback(pr.PaiedOutFlexInSeconds, pr.PaiedOutFlex);

        // Mirror the flag-off override semantics:
        //   Flex      = (override ? NettoHoursOverride : NettoHours) - PlanHours
        //   SumFlexEnd uses the same numerator.
        var effectiveNettoSecondsForFlex = pr.NettoHoursOverrideActive
            ? (long)(pr.NettoHoursOverride * 3600)
            : nettoSeconds;

        var flexSeconds = effectiveNettoSecondsForFlex - planHoursSeconds;
        pr.FlexInSeconds = (int)flexSeconds;
        pr.Flex = flexSeconds / 3600.0;

        var startSeconds = hasPreTimePlanning ? sumFlexStartInSeconds : 0;
        pr.SumFlexStartInSeconds = startSeconds;
        pr.SumFlexStart = startSeconds / 3600.0;
        var sumFlexEndSeconds = (long)startSeconds
                                + effectiveNettoSecondsForFlex - planHoursSeconds
                                - paiedOutFlexSeconds;
        pr.SumFlexEndInSeconds = (int)sumFlexEndSeconds;
        pr.SumFlexEnd = sumFlexEndSeconds / 3600.0;
    }
```

(The old `if/else` on `hasPreTimePlanning` computed exactly `start = hasPre ? seed : 0` followed by the same sum — this is a behaviour-preserving merge; the existing `PlanRegistrationHelperTests`/`OneMinuteIntervalsEffectiveDateTests` pin it.)

- [ ] **Step 4: Build** (tests execute in CI only)

Run: `dotnet build Microting.TimePlanningBase.Tests/Microting.TimePlanningBase.Tests.csproj`
Expected: Build succeeded.

- [ ] **Step 5: Commit** (after the dual review gate)

```bash
git add Microting.TimePlanningBase/Infrastructure/Helpers/FlexChain.cs Microting.TimePlanningBase.Tests/FlexChainUTest.cs
git commit -m "feat(flex-chain): balance-only CarryChain that never recomputes hours

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 3 (B3): Shared reconciled-day lock — `DayLock`

**Files:**
- Create: `Microting.TimePlanningBase/Infrastructure/Helpers/DayLock.cs`
- Test: `Microting.TimePlanningBase.Tests/DayLockUTest.cs` (create; uses `DbTestFixture`)

**Interfaces:**
- Produces (namespace `Microting.TimePlanningBase.Infrastructure.Helpers`):
  - `public static Task<DateTime?> DayLock.LockedThroughAsync(TimePlanningPnDbContext db, int sdkSitId)`
  - `public static bool DayLock.IsLocked(DateTime? lockedThrough, DateTime date)`
  - `public static IQueryable<PlanRegistration> DayLock.OpenRows(IQueryable<PlanRegistration> query, DateTime? lockedThrough)` — **not** an extension method (the plugin already has a `WhereOpen` extension on the same type; two would be ambiguous).
- Semantics are copied verbatim from the plugin's `DayLockHelper` (`Infrastructure/Helpers/DayLockHelper.cs:36-42, 72-73, 93-104, 170-173`).

- [ ] **Step 1: Write the failing test** — `Microting.TimePlanningBase.Tests/DayLockUTest.cs`

```csharp
using System;
using System.Linq;
using System.Threading.Tasks;
using Microting.eForm.Infrastructure.Constants;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using Microting.TimePlanningBase.Infrastructure.Helpers;
using NUnit.Framework;

namespace Microting.TimePlanningBase.Tests;

[TestFixture]
public class DayLockUTest : DbTestFixture
{
    private async Task<PlanRegistration> Row(int sdkSitId, DateTime date, bool reconciled = false)
    {
        var pr = new PlanRegistration { SdkSitId = sdkSitId, Date = date, Reconciled = reconciled };
        await pr.Create(DbContext);
        return pr;
    }

    [Test]
    public async Task LockedThrough_IsTheLatestReconciledDateOfThatWorkerOnly()
    {
        await Row(1, new DateTime(2026, 3, 1), reconciled: true);
        await Row(1, new DateTime(2026, 3, 5), reconciled: true);
        await Row(1, new DateTime(2026, 3, 9));
        await Row(2, new DateTime(2026, 3, 20), reconciled: true);

        Assert.That(await DayLock.LockedThroughAsync(DbContext, 1), Is.EqualTo(new DateTime(2026, 3, 5)));
        Assert.That(await DayLock.LockedThroughAsync(DbContext, 3), Is.Null);
    }

    [Test]
    public async Task LockedThrough_IgnoresRemovedReconciledRows()
    {
        var removed = await Row(1, new DateTime(2026, 3, 9), reconciled: true);
        await removed.Delete(DbContext);

        Assert.That(await DayLock.LockedThroughAsync(DbContext, 1), Is.Null);
    }

    [Test]
    public async Task OpenRows_KeepsOnlyDaysAfterTheBoundary()
    {
        await Row(1, new DateTime(2026, 3, 4));
        await Row(1, new DateTime(2026, 3, 5));
        await Row(1, new DateTime(2026, 3, 6));

        var open = DayLock.OpenRows(DbContext.PlanRegistrations, new DateTime(2026, 3, 5))
            .Select(x => x.Date).ToList();

        Assert.That(open, Is.EqualTo(new[] { new DateTime(2026, 3, 6) }));
        Assert.That(DayLock.IsLocked(new DateTime(2026, 3, 5), new DateTime(2026, 3, 5, 23, 0, 0)), Is.True);
        Assert.That(DayLock.IsLocked(null, new DateTime(2026, 3, 5)), Is.False);
    }
}
```

- [ ] **Step 2: Build to verify it fails**

Run: `dotnet build Microting.TimePlanningBase.Tests/Microting.TimePlanningBase.Tests.csproj`
Expected: FAIL — `The name 'DayLock' does not exist in the current context`.

- [ ] **Step 3: Implement** — `Microting.TimePlanningBase/Infrastructure/Helpers/DayLock.cs` (add the MIT header used by the other helper files)

```csharp
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microting.eForm.Infrastructure.Constants;
using Microting.TimePlanningBase.Infrastructure.Data;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;

namespace Microting.TimePlanningBase.Infrastructure.Helpers;

/// <summary>
/// The reconciled-day ("Afstemt") lock boundary, shared by the plugin and the
/// service. Every day on or before a worker's latest reconciled day is locked.
/// The plugin's DayLockHelper forwards here so there is one definition.
/// </summary>
public static class DayLock
{
    /// <summary>The latest reconciled date for one worker, or null when they have none.</summary>
    public static async Task<DateTime?> LockedThroughAsync(TimePlanningPnDbContext db, int sdkSitId)
        => await BoundaryRows(db)
            .Where(x => x.SdkSitId == sdkSitId)
            .MaxAsync(x => (DateTime?)x.Date)
            .ConfigureAwait(false);

    public static bool IsLocked(DateTime? lockedThrough, DateTime date)
        => lockedThrough.HasValue && date.Date <= lockedThrough.Value.Date;

    /// <summary>
    /// The rows NOT locked by <paramref name="lockedThrough"/>, as a database
    /// filter — exactly <c>!IsLocked(lockedThrough, x.Date)</c>. A locked row that
    /// is never loaded is never tracked, so no later SaveChanges can flush into it.
    /// </summary>
    public static IQueryable<PlanRegistration> OpenRows(
        IQueryable<PlanRegistration> query, DateTime? lockedThrough)
    {
        if (lockedThrough is not { } boundary)
        {
            return query;
        }

        var firstOpenDay = boundary.Date.AddDays(1);
        return query.Where(x => x.Date >= firstOpenDay);
    }

    internal static IQueryable<PlanRegistration> BoundaryRows(TimePlanningPnDbContext db)
        => db.PlanRegistrations
            .Where(x => x.Reconciled)
            .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed);
}
```

- [ ] **Step 4: Build**

Run: `dotnet build Microting.TimePlanningBase.Tests/Microting.TimePlanningBase.Tests.csproj`
Expected: Build succeeded. (DB tests run in CI.)

- [ ] **Step 5: Commit** (after the dual review gate)

```bash
git add Microting.TimePlanningBase/Infrastructure/Helpers/DayLock.cs Microting.TimePlanningBase.Tests/DayLockUTest.cs
git commit -m "feat(day-lock): share the reconciled-day boundary with the service

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 4 (B4): The forward walk — `FlexChainRecompute.RunForwardAsync`

**Files:**
- Create: `Microting.TimePlanningBase/Infrastructure/Helpers/FlexChainRecompute.cs`
- Modify: `Microting.TimePlanningBase/Infrastructure/Data/Entities/PnBase.cs:62` — expose the version snapshot to the assembly (`private object MapVersion` → add `internal object CreateVersionSnapshot() => MapVersion(this);`)
- Test: `Microting.TimePlanningBase.Tests/FlexChainRecomputeTests.cs` (create; uses `DbTestFixture`)

**Interfaces:**
- Consumes: `FlexChain.CarryChain` (B2), `DayLock.LockedThroughAsync/OpenRows/IsLocked` (B3), `OneMinuteModeTimeline.BuildAsync(db, AssignedSite?)`, `WasOneMinuteForRow(PlanRegistration)`, `WasOneMinuteFor(PlanRegistration?)` (existing).
- Produces: `public static Task<int> FlexChainRecompute.RunForwardAsync(TimePlanningPnDbContext db, AssignedSite? assignedSite, int sdkSitId, DateTime fromDateInclusive)` — call **after** the caller has saved its own changes; pass the EARLIEST date the caller changed. Walks every live row of the worker dated on/after that date (the changed day itself included, so a path that changed only plan hours or paid-out flex gets that day's balance fixed too), seeding from the last live row before it. Returns the number of rows written. Flushes the context (two `SaveChangesAsync`).

- [ ] **Step 1: Write the failing tests** — `Microting.TimePlanningBase.Tests/FlexChainRecomputeTests.cs`

```csharp
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using Microting.TimePlanningBase.Infrastructure.Helpers;
using NUnit.Framework;

namespace Microting.TimePlanningBase.Tests;

[TestFixture]
public class FlexChainRecomputeTests : DbTestFixture
{
    private const int Worker = 4711;
    private static readonly DateTime D0 = new(2026, 3, 2);

    private async Task<PlanRegistration> Row(int dayOffset, double netto, double plan,
        double sumFlexStart = 0, double sumFlexEnd = 0, bool reconciled = false)
    {
        var pr = new PlanRegistration
        {
            SdkSitId = Worker, Date = D0.AddDays(dayOffset),
            NettoHours = netto, PlanHours = plan, Flex = netto - plan,
            SumFlexStart = sumFlexStart, SumFlexEnd = sumFlexEnd, Reconciled = reconciled
        };
        await pr.Create(DbContext);
        return pr;
    }

    private async Task<AssignedSite> Site(bool oneMinute, DateTime? from = null)
    {
        var site = new AssignedSite { SiteId = Worker, UseOneMinuteIntervals = oneMinute, UseOneMinuteIntervalsFrom = from };
        await site.Create(DbContext);
        return site;
    }

    private PlanRegistration[] Reload() =>
        DbContext.PlanRegistrations.AsNoTracking()
            .Where(x => x.SdkSitId == Worker).OrderBy(x => x.Date).ThenBy(x => x.Id).ToArray();

    [Test]
    public async Task EditMidHistory_CarriesThroughToTheLastPreCreatedRow()
    {
        var site = await Site(oneMinute: false);
        var edited = await Row(0, netto: 9, plan: 7.5, sumFlexEnd: 1.5);   // freshly edited: +1.5
        await Row(1, 7.5, 7.5, 0, 0);                                        // stale chain
        await Row(2, 8.0, 7.5, 0, 0);
        await Row(40, 0, 7.5, 0, 0);                                         // pre-created future row

        var written = await FlexChainRecompute.RunForwardAsync(DbContext, site, Worker, edited.Date);

        var rows = Reload();
        Assert.Multiple(() =>
        {
            Assert.That(written, Is.EqualTo(3));
            Assert.That(rows[1].SumFlexEnd, Is.EqualTo(1.5).Within(1e-9));
            Assert.That(rows[2].SumFlexEnd, Is.EqualTo(2.0).Within(1e-9));
            Assert.That(rows[3].SumFlexStart, Is.EqualTo(2.0).Within(1e-9));
            Assert.That(rows[3].SumFlexEnd, Is.EqualTo(-5.5).Within(1e-9));
        });
    }

    [Test]
    public async Task Walk_NeverRecomputesHours_EvenWithStaleStamps()
    {
        var site = await Site(oneMinute: true, from: D0.AddDays(-30));
        var edited = await Row(0, 8, 8, sumFlexEnd: 0);
        var later = new PlanRegistration
        {
            SdkSitId = Worker, Date = D0.AddDays(1), PlanHours = 7.5,
            Start1Id = 85, Stop1Id = 181,                                     // office: 07:00-15:00
            Start1StartedAt = D0.AddDays(1).AddHours(12),                     // stale device stamps: 1 h
            Stop1StoppedAt = D0.AddDays(1).AddHours(13),
            NettoHours = 8.0, NettoHoursInSeconds = 28800
        };
        await later.Create(DbContext);

        await FlexChainRecompute.RunForwardAsync(DbContext, site, Worker, edited.Date);

        var reloaded = Reload()[1];
        Assert.Multiple(() =>
        {
            Assert.That(reloaded.NettoHoursInSeconds, Is.EqualTo(28800));
            Assert.That(reloaded.NettoHours, Is.EqualTo(8.0));
            Assert.That(reloaded.SumFlexEndInSeconds, Is.EqualTo(1800));
        });
    }

    [Test]
    public async Task ReconciledDays_AreNotWritten_AndTheWalkContinuesFromTheBoundary()
    {
        var site = await Site(oneMinute: false);
        var edited = await Row(0, 9, 7.5, sumFlexEnd: 1.5);
        var locked = await Row(1, 7.5, 7.5, 0, 40.0, reconciled: true);     // stored boundary balance 40
        await Row(2, 8.0, 7.5, 0, 0);

        await FlexChainRecompute.RunForwardAsync(DbContext, site, Worker, edited.Date);

        var rows = Reload();
        Assert.Multiple(() =>
        {
            Assert.That(rows[1].Version, Is.EqualTo(locked.Version), "locked row untouched");
            Assert.That(rows[1].SumFlexEnd, Is.EqualTo(40.0));
            Assert.That(rows[2].SumFlexStart, Is.EqualTo(40.0).Within(1e-9));
            Assert.That(rows[2].SumFlexEnd, Is.EqualTo(40.5).Within(1e-9));
        });
    }

    [Test]
    public async Task ModeBoundaryInsideTheWalk_SeedsTheFirstOneMinuteRowFromTheDecimal()
    {
        var site = await Site(oneMinute: true, from: D0.AddDays(2));
        var edited = await Row(0, 9, 7.5, sumFlexEnd: 1.5);
        await Row(1, 7.5, 7.5);
        var oneMinute = new PlanRegistration
        {
            SdkSitId = Worker, Date = D0.AddDays(2), NettoHours = 8.0, NettoHoursInSeconds = 28800, PlanHours = 7.5
        };
        await oneMinute.Create(DbContext);

        await FlexChainRecompute.RunForwardAsync(DbContext, site, Worker, edited.Date);

        var rows = Reload();
        Assert.Multiple(() =>
        {
            Assert.That(rows[1].SumFlexEndInSeconds, Is.EqualTo(0), "five-minute row carries no seconds");
            Assert.That(rows[2].SumFlexStartInSeconds, Is.EqualTo(5400));
            Assert.That(rows[2].SumFlexEndInSeconds, Is.EqualTo(7200));
        });
    }

    [Test]
    public async Task ConsistentChain_WritesNothing()
    {
        var site = await Site(oneMinute: false);
        var edited = await Row(0, 9, 7.5, sumFlexEnd: 1.5);
        var next = await Row(1, 8, 7.5, sumFlexStart: 1.5, sumFlexEnd: 2.0);
        var versionsBefore = DbContext.PlanRegistrationVersions.Count();

        var written = await FlexChainRecompute.RunForwardAsync(DbContext, site, Worker, edited.Date);

        Assert.Multiple(() =>
        {
            Assert.That(written, Is.EqualTo(0));
            Assert.That(Reload()[1].Version, Is.EqualTo(next.Version));
            Assert.That(DbContext.PlanRegistrationVersions.Count(), Is.EqualTo(versionsBefore));
        });
    }

    [Test]
    public async Task ChangedRows_GetOneVersionBumpAndOneVersionRowEach()
    {
        var site = await Site(oneMinute: false);
        var edited = await Row(0, 9, 7.5, sumFlexEnd: 1.5);
        var a = await Row(1, 8, 7.5);
        var b = await Row(2, 8, 7.5);
        var versionsBefore = DbContext.PlanRegistrationVersions.Count();

        await FlexChainRecompute.RunForwardAsync(DbContext, site, Worker, edited.Date);

        var rows = Reload();
        Assert.Multiple(() =>
        {
            Assert.That(rows[1].Version, Is.EqualTo(a.Version + 1));
            Assert.That(rows[2].Version, Is.EqualTo(b.Version + 1));
            Assert.That(DbContext.PlanRegistrationVersions.Count(), Is.EqualTo(versionsBefore + 2));
            Assert.That(DbContext.PlanRegistrationVersions.AsNoTracking()
                .Where(v => v.PlanRegistrationId == a.Id).OrderByDescending(v => v.Id).First().SumFlexEnd,
                Is.EqualTo(2.0).Within(1e-9));
        });
    }

    [Test]
    public async Task RemovedRows_AreNeitherWalkedNorUsedAsSeed()
    {
        var site = await Site(oneMinute: false);
        var edited = await Row(0, 9, 7.5, sumFlexEnd: 1.5);
        var removed = await Row(1, 20, 0, 0, 99);
        await removed.Delete(DbContext);
        await Row(2, 8, 7.5);

        await FlexChainRecompute.RunForwardAsync(DbContext, site, Worker, edited.Date);

        var live = Reload().Where(x => x.WorkflowState != "removed").ToArray();
        Assert.That(live[1].SumFlexStart, Is.EqualTo(1.5).Within(1e-9));
        Assert.That(DbContext.PlanRegistrations.AsNoTracking().Single(x => x.Id == removed.Id).SumFlexEnd,
            Is.EqualTo(99));
    }

    [Test]
    public async Task SameDateRows_ChainInIdOrder()
    {
        var site = await Site(oneMinute: false);
        var edited = await Row(0, 9, 7.5, sumFlexEnd: 1.5);
        var duplicate = await Row(0, 1, 7.5, 0, 55);
        await Row(1, 7.5, 7.5);

        await FlexChainRecompute.RunForwardAsync(DbContext, site, Worker, edited.Date);

        var rows = Reload();
        Assert.That(rows.Single(x => x.Id == duplicate.Id).SumFlexEnd, Is.EqualTo(-5.0).Within(1e-9));
        Assert.That(rows.Last().SumFlexStart, Is.EqualTo(-5.0).Within(1e-9));
    }

    [Test]
    public async Task FirstRow_KeepsItsStoredOpeningBalance()
    {
        var site = await Site(oneMinute: false);
        await Row(0, 8, 7.5, sumFlexStart: 20, sumFlexEnd: 0);   // opening balance 20, end stale
        await Row(1, 7.5, 7.5);

        await FlexChainRecompute.RunForwardAsync(DbContext, site, Worker, D0);

        var rows = Reload();
        Assert.Multiple(() =>
        {
            Assert.That(rows[0].SumFlexStart, Is.EqualTo(20.0).Within(1e-9));
            Assert.That(rows[0].SumFlexEnd, Is.EqualTo(20.5).Within(1e-9));
            Assert.That(rows[1].SumFlexEnd, Is.EqualTo(20.5).Within(1e-9));
        });
    }

    [Test]
    public async Task NullSite_WalksAsFiveMinute()
    {
        var edited = await Row(0, 9, 7.5, sumFlexEnd: 1.5);
        await Row(1, 8, 7.5);

        var written = await FlexChainRecompute.RunForwardAsync(DbContext, null, Worker, edited.Date);

        Assert.That(written, Is.EqualTo(1));
        Assert.That(Reload()[1].SumFlexEnd, Is.EqualTo(2.0).Within(1e-9));
    }
}
```

- [ ] **Step 2: Build to verify it fails**

Run: `dotnet build Microting.TimePlanningBase.Tests/Microting.TimePlanningBase.Tests.csproj`
Expected: FAIL — `The name 'FlexChainRecompute' does not exist in the current context`.

- [ ] **Step 3: Implement.** In `PnBase.cs`, directly above `private object MapVersion(object obj)` add:

```csharp
    /// <summary>
    /// The version-history row for this entity's CURRENT values, for writers that
    /// batch many updates into one SaveChanges instead of calling
    /// <see cref="Update"/> per row.
    /// </summary>
    internal object CreateVersionSnapshot() => MapVersion(this);
```

Create `Microting.TimePlanningBase/Infrastructure/Helpers/FlexChainRecompute.cs` (MIT header as in the other helpers):

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microting.eForm.Infrastructure.Constants;
using Microting.TimePlanningBase.Infrastructure.Data;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;

namespace Microting.TimePlanningBase.Infrastructure.Helpers;

/// <summary>
/// Carries a worker's running flex balance forward after a day was changed.
///
/// One walk for every writer (office edit, grid save, flex tab, absence,
/// handover, sheet pull, device submission), so an edit to day D always reaches
/// the worker's LAST row — including rows pre-created for future dates.
///
/// The walk is balance-only (<see cref="FlexChain.CarryChain"/>): worked hours
/// are computed when a day's own inputs change, never by a walk.
/// </summary>
public static class FlexChainRecompute
{
    private const double DecimalTolerance = 1e-9;

    /// <summary>
    /// Walks every non-removed row of the worker dated on or after
    /// <paramref name="fromDateInclusive"/>, ascending (Date, then Id), carrying
    /// the balance in memory from the last live row before it. Reconciled
    /// (locked) days are never loaded for writing: when the boundary is on or
    /// after the start date, the walk starts the day after the boundary and seeds
    /// from the boundary row's stored balance. With no earlier row at all, the
    /// first row keeps its own stored opening balance (SumFlexStart). Only rows
    /// whose chain values change are written: Version + 1, UpdatedAt, and one
    /// version-history row each, in two SaveChanges calls in total.
    ///
    /// Call AFTER saving your own changes, passing the earliest date you changed.
    /// The context is flushed, so any other pending change on it is saved too.
    /// </summary>
    /// <returns>The number of rows written.</returns>
    public static async Task<int> RunForwardAsync(TimePlanningPnDbContext db,
        AssignedSite? assignedSite, int sdkSitId, DateTime fromDateInclusive)
    {
        var timeline = await OneMinuteModeTimeline.BuildAsync(db, assignedSite).ConfigureAwait(false);
        var lockedThrough = await DayLock.LockedThroughAsync(db, sdkSitId).ConfigureAwait(false);

        var live = db.PlanRegistrations
            .Where(x => x.SdkSitId == sdkSitId)
            .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed);

        var from = fromDateInclusive.Date;
        if (lockedThrough is { } boundary && boundary.Date >= from)
        {
            from = boundary.Date.AddDays(1);
        }

        var predecessor = await live.AsNoTracking()
            .Where(x => x.Date < from)
            .OrderByDescending(x => x.Date).ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync().ConfigureAwait(false);

        var rows = await live
            .Where(x => x.Date >= from)
            .OrderBy(x => x.Date).ThenBy(x => x.Id)
            .ToListAsync().ConfigureAwait(false);

        if (rows.Count == 0)
        {
            return 0;
        }

        bool? predecessorIsOneMinute;
        if (predecessor != null)
        {
            predecessorIsOneMinute = timeline.WasOneMinuteFor(predecessor);
        }
        else
        {
            // The worker's very first row: its stored SumFlexStart is the opening
            // balance (set via the flex tab), not something to reset to 0.
            var first = rows[0];
            predecessor = new PlanRegistration
            {
                SumFlexEnd = first.SumFlexStart,
                SumFlexEndInSeconds = first.SumFlexStartInSeconds
            };
            predecessorIsOneMinute = timeline.WasOneMinuteForRow(first);
        }

        var changed = new List<PlanRegistration>();
        foreach (var row in rows)
        {
            var before = ChainValues.Of(row);
            var rowIsOneMinute = timeline.WasOneMinuteForRow(row);
            FlexChain.CarryChain(row, predecessor, rowIsOneMinute, predecessorIsOneMinute);

            if (before.SameAs(ChainValues.Of(row)))
            {
                before.RestoreInto(row); // leave EF nothing to flush for a float-noise difference
            }
            else
            {
                changed.Add(row);
            }

            predecessor = row;
            predecessorIsOneMinute = rowIsOneMinute;
        }

        if (changed.Count == 0)
        {
            return 0;
        }

        var now = DateTime.UtcNow;
        foreach (var row in changed)
        {
            row.Version += 1;
            row.UpdatedAt = now;
        }
        await db.SaveChangesAsync().ConfigureAwait(false);

        foreach (var row in changed)
        {
            if (row.CreateVersionSnapshot() is PlanRegistrationVersion version)
            {
                db.PlanRegistrationVersions.Add(version);
            }
        }
        await db.SaveChangesAsync().ConfigureAwait(false);

        return changed.Count;
    }

    private readonly record struct ChainValues(
        double Flex, double SumFlexStart, double SumFlexEnd,
        int FlexInSeconds, int SumFlexStartInSeconds, int SumFlexEndInSeconds)
    {
        public static ChainValues Of(PlanRegistration r) => new(
            r.Flex, r.SumFlexStart, r.SumFlexEnd,
            r.FlexInSeconds, r.SumFlexStartInSeconds, r.SumFlexEndInSeconds);

        public bool SameAs(ChainValues o) =>
            Math.Abs(Flex - o.Flex) < DecimalTolerance
            && Math.Abs(SumFlexStart - o.SumFlexStart) < DecimalTolerance
            && Math.Abs(SumFlexEnd - o.SumFlexEnd) < DecimalTolerance
            && FlexInSeconds == o.FlexInSeconds
            && SumFlexStartInSeconds == o.SumFlexStartInSeconds
            && SumFlexEndInSeconds == o.SumFlexEndInSeconds;

        public void RestoreInto(PlanRegistration r)
        {
            r.Flex = Flex;
            r.SumFlexStart = SumFlexStart;
            r.SumFlexEnd = SumFlexEnd;
            r.FlexInSeconds = FlexInSeconds;
            r.SumFlexStartInSeconds = SumFlexStartInSeconds;
            r.SumFlexEndInSeconds = SumFlexEndInSeconds;
        }
    }
}
```

Note for the implementer: `OpenRows` is not needed inside the walk (starting after the boundary already excludes locked rows) but is part of the shared `DayLock` API that the plugin's `WhereOpen` forwards to.

- [ ] **Step 4: Build**

Run: `dotnet build Microting.TimePlanningBase.Tests/Microting.TimePlanningBase.Tests.csproj`
Expected: Build succeeded. (DB tests execute in CI.)

- [ ] **Step 5: Commit** (after the dual review gate)

```bash
git add Microting.TimePlanningBase/Infrastructure/Helpers/FlexChainRecompute.cs Microting.TimePlanningBase/Infrastructure/Data/Entities/PnBase.cs Microting.TimePlanningBase.Tests/FlexChainRecomputeTests.cs
git commit -m "feat(flex-chain): RunForwardAsync carries every edit to the worker's last row

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 5 (B5): Base PR, CI, release 10.0.65

- [ ] **Step 1:** `git push -u origin feat/flex-chain-forward-walk`; `gh pr create --base master --title "feat(flex-chain): shared open-shift-safe netto, balance-only walk, day lock" --body "<summary of B1-B4; Test plan; 🤖 Generated with [Claude Code](https://claude.com/claude-code)>"`.
- [ ] **Step 2:** Watch CI to a verdict: `gh pr checks <n> --watch`. For any red check: `gh api repos/microting/eform-timeplanning-base/actions/jobs/<id> --jq '.steps[]|select(.conclusion=="failure")'`, classify infra vs real; compare with master's latest run. Fix real failures (never by editing existing tests).
- [ ] **Step 3:** Ask the user to merge (or for permission to merge). After merge: ask permission, then `git tag v10.0.65 origin/master && git push origin v10.0.65`; watch `dotnet-core.yml` publish; confirm `Microting.TimePlanningBase 10.0.65` is on nuget.org (`curl -s https://api.nuget.org/v3-flatcontainer/microting.timeplanningbase/index.json | tail -c 200`).

---

## Part 2 — Plugin (`eform-angular-timeplanning-plugin`, PRs 2-3)

# Plugin tasks — flex chain integrity (PR2 + PR3)

Spec: `docs/superpowers/specs/2026-09-24-flex-chain-integrity-design.md` (§3, §5.1 plugin part, §5.2, §5.3, §6, §7).
Repo: `eform-angular-timeplanning-plugin` (target branch `stable`). All paths below are relative to the repo root;
`P = eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn`, `T = eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn.Test`.
Line numbers were verified against `origin/stable` at `f0f94a79^` (2026-09-24).

**Mode:** no dev mode — edit the source repo directly (spec §7). Every task still runs the full cycle.

**Test execution rule (project memory):** DB-backed tests (Testcontainers MariaDB) execute in **CI only**. Do not run
`# tests run in CI only (local test runs are hook-blocked)` locally for them; the local gate is `dotnet build` of the solution. The `# tests run in CI only (local test runs are hook-blocked)` commands below are
given for completeness/CI parity and are marked *optional (CI)*.

**Base API consumed (Microting.TimePlanningBase 10.0.65, namespace `Microting.TimePlanningBase.Infrastructure.Helpers`):**

| Member | Used by |
|---|---|
| `static double FlexChain.ComputeNettoMinutesFlagOff(PlanRegistration pr)` | T-P1 |
| `static class DayLock { Task<DateTime?> LockedThroughAsync(TimePlanningPnDbContext db, int sdkSitId); bool IsLocked(DateTime? lockedThrough, DateTime date); IQueryable<PlanRegistration> OpenRows(IQueryable<PlanRegistration> query, DateTime? lockedThrough); }` | T-P1 |
| `static Task<int> FlexChainRecompute.RunForwardAsync(TimePlanningPnDbContext db, AssignedSite? assignedSite, int sdkSitId, DateTime fromDateInclusive)` — walks every non-removed row of `sdkSitId` with `Date >= fromDateInclusive` (asc `Date`, `Id`), no upper bound; seeds from the last live row strictly before `fromDateInclusive` (or the reconciled boundary row when the boundary is on/after it); applies balance-only `CarryChain` to every walked row **including the first day**; never recomputes `NettoHours*`; skips locked rows; writes only changed rows (Version+1, UpdatedAt, one version row each) in two `SaveChangesAsync`; returns rows changed. Call AFTER your own save; it flushes any pending tracked changes of the context. | T-P3…T-P6 |

**Base load semantics (confirmed by the base author):** `RunForwardAsync` loads the walked rows WITH tracking (predecessor `AsNoTracking`), so rows the caller already tracks (e.g. `CreateUpdate`'s `planRegistrations`) resolve to the same instances. One-line check when implementing T-P3: glance at the released source to confirm.

---

## PR2 — `fix/flex-chain-office-edit` (off `origin/stable`)

```bash
cd /home/rene/Documents/workspace/microting/eform-angular-timeplanning-plugin
git fetch origin && git checkout -b fix/flex-chain-office-edit origin/stable
```

### Task 6 (T-P1) — Bump base to 10.0.65; DayLockHelper forwards to base `DayLock`; five-minute netto from base

**Files**
- Modify `P/TimePlanning.Pn.csproj:36` (`<PackageReference Include="Microting.TimePlanningBase" Version="10.0.64" />`).
  `T/TimePlanning.Pn.Test.csproj` has **no** direct TimePlanningBase reference (transitive via the project ref) — no change.
- Modify `P/Infrastructure/Helpers/DayLockHelper.cs:1-6` (header note), `:10-18` (usings), `:36-42` (`LockedThroughAsync`), `:72-73` (`IsLocked(DateTime?, DateTime)`), `:93-103` (`WhereOpen`).
- Modify `P/Services/TimePlanningWorkingHoursService/TimePlanningWorkingHoursService.cs:1361-1403` (delete private `ComputeFlagOffNettoMinutes`) and its 4 call sites `:1838`, `:2132`, `:2493`, `:2776`.
- **Not modified:** `P/Services/TimePlanningPlanningService/TimePlanningPlanningService.cs:1780-1822` (`ComputePlanningNettoMinutes`) — see step 1.

**Interfaces**
- Consumes: `DayLock.LockedThroughAsync`, `DayLock.IsLocked`, `DayLock.OpenRows`, `FlexChain.ComputeNettoMinutesFlagOff`.
- Produces: unchanged public signatures `DayLockHelper.LockedThroughAsync(TimePlanningPnDbContext, int)`, `DayLockHelper.IsLocked(DateTime?, DateTime)`, `DayLockHelper.WhereOpen(this IQueryable<PlanRegistration>, DateTime?)`. `LockedThroughForSitesAsync`, `IsLocked(IReadOnlyDictionary…)`, `LockedMessageKey*`, `CanReconcile`, `BoundaryRows` stay local.

**Existing tests exercising the touched code (must pass unchanged):** `DayLockHelperTests`, `DayLockInterceptorTests`,
`DayLockWiringTests`, `FirstUnlockedDateTests`, `ReconcileServiceTests`, `ReconciliationSummaryServiceTests`,
`TimePlanningFlexServiceRemovedRowTests`, `AbsenceRequestServiceTests`, `ContentHandoverServiceTests` (lock guards);
`WorkingHoursGrpcKioskNonRoundMinutesTests`, `PauseIdSelfHealGuardTests`, `GrpcServices.TimePlanningWorkingHoursGrpcServiceTests`
(`UpdateWorkingHour` legs that call the five-minute netto).

- [ ] **Step 1 — Verify behaviour identity (read-only).**
  - `ComputeFlagOffNettoMinutes` (`TimePlanningWorkingHoursService.cs:1367-1403`): per shift 1..5, `if (StopNId >= StartNId && StopNId != 0) { += (StopNId - StartNId) * 5; -= FlexChain.ComputeShiftPauseSeconds(pr, N, useOneMinuteIntervals: false) / 60.0; }`. Open the base 10.0.65 `FlexChain.ComputeNettoMinutesFlagOff` source and confirm it is the same expression (same guard, same int multiply then double subtract, no `Math.Max` clamp, no rounding). **Identical → replace (step 3).**
  - `ComputePlanningNettoMinutes` (`TimePlanningPlanningService.cs:1780-1822`) is **NOT identical** — do **not** replace it:
    1. its id branch deducts `overrideMinutes ?? (pauseId > 0 ? (pauseId - 1) * 5 : 0)` from the **primary `Pause{N}Id` only** (`:1816`); the base function deducts `ComputeShiftPauseSeconds(pr, N, false)`, which prefers the floor-to-5-min tick delta of **all timestamped pause slots** (Pause1 + Pause10..19 + Pause100..102 for shift 1, etc.) and only falls back to `Pause{N}Id` when no slot is complete. On a legacy row whose `Pause1Id` was corrected by the office while the device pause stamps remain, the base function would re-introduce the stale stamps — the B2 bug class, in the single-day editor.
    2. the id branch also runs for `useOneMinuteIntervals == true` rows whose `StartedAt`/`StoppedAt` stamps are missing (`:1793` `else`), so it is not a "flag-off branch".
    3. It already implements R1 (`stopId >= startId && stopId != 0`, pause ignored on an open shift), so replacing it fixes nothing.
    **Decision (owner, 2026-09-24): `ComputePlanningNettoMinutes` stays unchanged.** State this in the PR2 description: spec §5.1's "becomes a call to the base function" applies only to `ComputeFlagOffNettoMinutes`; the single-day editor keeps its own formula because it is not behaviour-identical and already satisfies R1.
- [ ] **Step 2 — Bump the package.** In `P/TimePlanning.Pn.csproj:36`:
  ```xml
        <PackageReference Include="Microting.TimePlanningBase" Version="10.0.65" />
  ```
  Then `dotnet restore eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn.sln` (10.0.65 must be on nuget.org — PR1 released it).
- [ ] **Step 3 — Replace `ComputeFlagOffNettoMinutes` with the base function.** Delete the whole block `TimePlanningWorkingHoursService.cs:1361-1403` (the `/// <summary> Flag-OFF netto computation …` doc comment through the closing `}` of `private static double ComputeFlagOffNettoMinutes(PlanRegistration pr)`). At each of `:1838`, `:2132`, `:2493`, `:2776` change
  ```csharp
              double nettoMinutes = ComputeFlagOffNettoMinutes(planRegistration);
  ```
  to
  ```csharp
              double nettoMinutes = FlexChain.ComputeNettoMinutesFlagOff(planRegistration);
  ```
  (`using Microting.TimePlanningBase.Infrastructure.Helpers;` already present at `:41`.) Confirm no reference remains: `grep -n "ComputeFlagOffNettoMinutes" -r eFormAPI/Plugins/TimePlanning.Pn` → no hits.
- [ ] **Step 4 — DayLockHelper forwards to base `DayLock`.** In `P/Infrastructure/Helpers/DayLockHelper.cs`:
  - Header `:1-6` — replace with:
    ```csharp
    // NOTE: the lock RULE ("locked through MAX(Date) of the worker's live Reconciled
    // rows") lives in the base package: Microting.TimePlanningBase DayLock. The three
    // members that answer "is this day locked" forward to it, so the plugin, the
    // service and the base forward walk (FlexChainRecompute.RunForwardAsync) share
    // one definition. The members that stay here (per-site map, messages, reconcile
    // gate, BoundaryRows) are plugin-only. The service repo keeps a twin of this
    // file (ServiceTimePlanningPlugin/Infrastructure/Helpers/DayLockHelper.cs); it
    // should forward the same way when it moves to 10.0.65.
    ```
  - Add after `using Microting.TimePlanningBase.Infrastructure.Data.Entities;` (`:18`):
    ```csharp
    using Microting.TimePlanningBase.Infrastructure.Helpers;
    ```
  - Replace `:36-42`:
    ```csharp
        public static Task<DateTime?> LockedThroughAsync(TimePlanningPnDbContext db, int sdkSitId)
            => DayLock.LockedThroughAsync(db, sdkSitId);
    ```
  - Replace `:72-73`:
    ```csharp
        public static bool IsLocked(DateTime? lockedThrough, DateTime date)
            => DayLock.IsLocked(lockedThrough, date);
    ```
  - Replace the body `:93-103`:
    ```csharp
        public static IQueryable<PlanRegistration> WhereOpen(
            this IQueryable<PlanRegistration> query, DateTime? lockedThrough)
            => DayLock.OpenRows(query, lockedThrough);
    ```
  Keep the existing XML doc comments above each member unchanged. Keep `BoundaryRows` (`:170-173`) — `LockedThroughForSitesAsync` and `ReconciliationSummaryService` use it. (`Microsoft.EntityFrameworkCore` using is still needed by `LockedThroughForSitesAsync`/`LockedMessageKeyAsync`.)
- [ ] **Step 5 — Build.**
  ```bash
  dotnet build eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn.sln
  ```
  Expected: 0 errors. No new tests (pure refactor; the listed existing tests are the regression net and run in CI).
- [ ] **Step 6 — Commit.**
  ```bash
  git add eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn/TimePlanning.Pn.csproj \
          eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn/Infrastructure/Helpers/DayLockHelper.cs \
          eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn/Services/TimePlanningWorkingHoursService/TimePlanningWorkingHoursService.cs
  git commit -m "$(cat <<'EOF'
  refactor(flex): take the day lock and five-minute hours from the base

  Bump Microting.TimePlanningBase to 10.0.65. DayLockHelper.LockedThroughAsync,
  IsLocked and WhereOpen now forward to the base DayLock, so the plugin and the
  base forward walk share one lock rule; signatures are unchanged. The private
  ComputeFlagOffNettoMinutes is replaced by FlexChain.ComputeNettoMinutesFlagOff,
  which is the same computation moved to the base.

  ComputePlanningNettoMinutes is deliberately left alone: its id branch deducts
  the primary Pause{N}Id, not the timestamped pause slots, so it is not
  behaviour-identical to the base function.

  Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
  EOF
  )"
  ```

---

### Task 7 (T-P2) — B4 + B2 in the grid save (`TimePlanningWorkingHoursService.UpdatePlanning`)

**Files**
- Modify `P/Services/TimePlanningWorkingHoursService/TimePlanningWorkingHoursService.cs:736-802` (`UpdatePlanning`); add one private static helper right after it (before `public async Task<OperationDataResult<TimePlanningWorkingHourSimpleModel>> ReadSimple(` at `:804`).
- Create `T/OfficeEditOneMinuteStaleStampsTests.cs`.
- Modify `.github/workflows/dotnet-core-pr.yml:268` and `.github/workflows/dotnet-core-master.yml:279` (shard **e**).

**Interfaces**
- Consumes: `OneMinuteModeTimeline.WasOneMinuteAt(DateTime)` (param `timeline`, already passed in from `CreateUpdate:567`).
- Produces: `private static void ClearStampsOfCorrectedShifts(PlanRegistration pr, TimePlanningWorkingHoursModel model)`.
- Model property names (verified): `TimePlanningWorkingHoursModel.Shift1Start/Shift1Stop/Shift1Pause/Shift2Start/Shift2Stop/Shift2Pause` (`int?`); entity `Start1Id/Stop1Id/Pause1Id/Start2Id/Stop2Id/Pause2Id`, `Start1StartedAt/Stop1StoppedAt/Start2StartedAt/Stop2StoppedAt`, `Pause1StartedAt/Pause1StoppedAt/Pause2StartedAt/Pause2StoppedAt`.

**Existing tests exercising the touched code (must pass unchanged):** `MobileFlexRecomputeAndCascadeTests` (D1 has no stamps → clearing is a no-op; marker stays true because the site has no effective date and its only version row is `true`), `ReconcileServiceTests.CreateUpdate_*` (3 tests, five-minute sites), `WorkingHoursMessagePersistenceTests` (five-minute site).

- [ ] **Step 1 — Write the failing tests** `T/OfficeEditOneMinuteStaleStampsTests.cs`:
  ```csharp
  using System;
  using System.Collections.Generic;
  using System.Threading.Tasks;
  using Microsoft.EntityFrameworkCore;
  using Microsoft.Extensions.Logging;
  using Microting.eForm.Infrastructure.Constants;
  using Microting.eFormApi.BasePn.Abstractions;
  using Microting.eFormApi.BasePn.Infrastructure.Helpers.PluginDbOptions;
  using NSubstitute;
  using NUnit.Framework;
  using TimePlanning.Pn.Infrastructure.Models.Settings;
  using TimePlanning.Pn.Infrastructure.Models.WorkingHours.Index;
  using TimePlanning.Pn.Infrastructure.Models.WorkingHours.UpdateCreate;
  using TimePlanning.Pn.Services.TimePlanningLocalizationService;
  using TimePlanning.Pn.Services.TimePlanningWorkingHoursService;
  using AssignedSiteEntity = Microting.TimePlanningBase.Infrastructure.Data.Entities.AssignedSite;
  using PlanRegistrationEntity = Microting.TimePlanningBase.Infrastructure.Data.Entities.PlanRegistration;

  namespace TimePlanning.Pn.Test;

  /// <summary>
  /// Grid save (CreateUpdate → UpdatePlanning) on a one-minute site.
  ///
  /// B2: an office correction of a shift's ids on a one-minute row used to be
  /// replaced, in the same save, by hours re-derived from the device stamps that
  /// still held the old times. The stamps of exactly the corrected parts are now
  /// dropped, so the hours come from the office's ids.
  ///
  /// B4: the row's mode marker follows the mode AT ITS OWN DATE
  /// (UseOneMinuteIntervalsFrom), not the site's current flag.
  ///
  /// Ids: id n is (n - 1) * 5 minutes after midnight — 85 = 07:00, 97 = 08:00,
  /// 193 = 16:00, 235 = 19:30.
  /// </summary>
  [TestFixture]
  public class OfficeEditOneMinuteStaleStampsTests : TestBaseSetup
  {
      private const int SiteUid = 7801;
      private static readonly DateTime OneMinuteFrom = new(2026, 1, 1);

      private TimePlanningWorkingHoursService _service = null!;

      [SetUp]
      public async Task SetUpTest()
      {
          await base.Setup();

          var userService = Substitute.For<IUserService>();
          userService.UserId.Returns(1);

          var localizationService = Substitute.For<ITimePlanningLocalizationService>();
          localizationService.GetString(Arg.Any<string>()).Returns(x => x[0]?.ToString());

          var coreService = Substitute.For<IEFormCoreService>();
          coreService.GetCore().Returns(await GetCore());

          var options = Substitute.For<IPluginDbOptions<TimePlanningBaseSettings>>();
          options.Value.Returns(new TimePlanningBaseSettings
          {
              AutoBreakCalculationActive = "0",
              DayOfPayment = 20,
              GpsEnabled = "0",
              SnapshotEnabled = "0"
          });

          _service = new TimePlanningWorkingHoursService(
              Substitute.For<ILogger<TimePlanningWorkingHoursService>>(),
              TimePlanningPnDbContext!,
              userService,
              localizationService,
              baseDbContext: null!,
              options,
              coreService);

          // One-minute site whose flag took effect on OneMinuteFrom. Weekday-plan
          // branch (not the Google-sheet one), and every row below is older than
          // one month, so UpdatePlanRegistration never rewrites PlanHours.
          await new AssignedSiteEntity
          {
              SiteId = SiteUid,
              UseOneMinuteIntervals = true,
              UseOneMinuteIntervalsFrom = OneMinuteFrom,
              UseGoogleSheetAsDefault = false,
              Resigned = false,
              WorkflowState = Constants.WorkflowStates.Created,
              CreatedByUserId = 1,
              UpdatedByUserId = 1
          }.Create(TimePlanningPnDbContext!);
      }

      /// <summary>
      /// A row as the device left it: ids 07:00-19:30 and stamps a few seconds
      /// off them, hours derived from the stamps.
      /// </summary>
      private async Task SeedDeviceRow(DateTime date, bool? marker,
          int pause1Id = 0, DateTime? pause1StartedAt = null, DateTime? pause1StoppedAt = null)
      {
          var start = date.AddHours(7).AddSeconds(13);
          var stop = date.AddHours(19).AddMinutes(30).AddSeconds(47);
          var pauseSeconds = pause1StartedAt.HasValue && pause1StoppedAt.HasValue
              ? (int)(pause1StoppedAt.Value - pause1StartedAt.Value).TotalSeconds
              : 0;
          var nettoSeconds = (int)(stop - start).TotalSeconds - pauseSeconds;
          await new PlanRegistrationEntity
          {
              SdkSitId = SiteUid,
              Date = date,
              Start1Id = 85,
              Stop1Id = 235,
              Pause1Id = pause1Id,
              Start1StartedAt = start,
              Stop1StoppedAt = stop,
              Pause1StartedAt = pause1StartedAt,
              Pause1StoppedAt = pause1StoppedAt,
              PlanHours = 8,
              PlanHoursInSeconds = 28800,
              NettoHours = nettoSeconds / 3600.0,
              NettoHoursInSeconds = nettoSeconds,
              RegisteredUnderOneMinuteIntervals = marker,
              PlanText = "",
              CommentOffice = "",
              CommentOfficeAll = "",
              WorkflowState = Constants.WorkflowStates.Created,
              CreatedByUserId = 1,
              UpdatedByUserId = 1
          }.Create(TimePlanningPnDbContext!);
      }

      private static TimePlanningWorkingHoursUpdateCreateModel OfficeEdit(
          DateTime date, int start1, int stop1, int pause1 = 0, double nettoHours = 0) => new()
      {
          SiteId = SiteUid,
          Plannings = new List<TimePlanningWorkingHoursModel>
          {
              new()
              {
                  Date = date,
                  Shift1Start = start1,
                  Shift1Stop = stop1,
                  Shift1Pause = pause1,
                  PlanHours = 8,
                  NettoHours = nettoHours,
                  FlexHours = 0,
                  PaidOutFlex = "0",
                  Message = 0,
                  PlanText = "",
                  CommentOffice = "",
                  CommentOfficeAll = "",
                  CommentWorker = ""
              }
          }
      };

      private async Task<PlanRegistrationEntity> Stored(DateTime date) =>
          await TimePlanningPnDbContext!.PlanRegistrations.AsNoTracking()
              .SingleAsync(x => x.SdkSitId == SiteUid && x.Date == date);

      [Test]
      public async Task OfficeCorrectsStartAndStop_OnOneMinuteRowWithStaleStamps_HoursFollowTheOfficeIds()
      {
          var date = new DateTime(2026, 3, 2);
          await SeedDeviceRow(date, marker: true);

          var result = await _service.CreateUpdate(OfficeEdit(date, start1: 97, stop1: 193));

          Assert.That(result.Success, Is.True, result.Message);
          var row = await Stored(date);
          Assert.Multiple(() =>
          {
              Assert.That(row.NettoHoursInSeconds, Is.EqualTo(8 * 3600),
                  "08:00-16:00 as the office entered it, not the device's 07:00:13-19:30:47");
              Assert.That(row.NettoHours, Is.EqualTo(8.0).Within(1e-9));
              Assert.That(row.Start1Id, Is.EqualTo(97));
              Assert.That(row.Stop1Id, Is.EqualTo(193));
              Assert.That(row.Start1StartedAt, Is.Null, "the corrected shift's stale start stamp is dropped");
              Assert.That(row.Stop1StoppedAt, Is.Null, "the corrected shift's stale stop stamp is dropped");
              Assert.That(row.RegisteredUnderOneMinuteIntervals, Is.True);
          });
      }

      [Test]
      public async Task OfficeCorrectsThePause_OnOneMinuteRowWithStalePauseStamps_PauseFollowsTheOfficeId()
      {
          var date = new DateTime(2026, 3, 3);
          // Device pause 12:00:05-12:41:10 (2465 s); Pause1Id 7 = 30 min.
          await SeedDeviceRow(date, marker: true, pause1Id: 7,
              pause1StartedAt: date.AddHours(12).AddSeconds(5),
              pause1StoppedAt: date.AddHours(12).AddMinutes(41).AddSeconds(10));

          // Office: 08:00-16:00 with a 15-minute pause (Pause id 4 = (4 - 1) * 5 min).
          var result = await _service.CreateUpdate(OfficeEdit(date, start1: 97, stop1: 193, pause1: 4));

          Assert.That(result.Success, Is.True, result.Message);
          var row = await Stored(date);
          Assert.Multiple(() =>
          {
              Assert.That(row.NettoHoursInSeconds, Is.EqualTo(8 * 3600 - 15 * 60));
              Assert.That(row.Pause1Id, Is.EqualTo(4));
              Assert.That(row.Pause1StartedAt, Is.Null);
              Assert.That(row.Pause1StoppedAt, Is.Null);
          });
      }

      [Test]
      public async Task OfficeSavesWithoutChangingIds_OnOneMinuteRow_KeepsDeviceStampsAndHours()
      {
          var date = new DateTime(2026, 3, 4);
          await SeedDeviceRow(date, marker: true);
          var before = await Stored(date);

          // Same ids as stored: only the unchanged-ids comment/plan save the page does on every row.
          var result = await _service.CreateUpdate(OfficeEdit(date, start1: 85, stop1: 235));

          Assert.That(result.Success, Is.True, result.Message);
          var row = await Stored(date);
          Assert.Multiple(() =>
          {
              Assert.That(row.Start1StartedAt, Is.EqualTo(before.Start1StartedAt));
              Assert.That(row.Stop1StoppedAt, Is.EqualTo(before.Stop1StoppedAt));
              Assert.That(row.NettoHoursInSeconds, Is.EqualTo(before.NettoHoursInSeconds),
                  "an unchanged shift keeps its second-precision hours from the stamps");
          });
      }

      [Test]
      public async Task OfficeEditOfARowBeforeTheEffectiveDate_MarksItFiveMinute_AndLeavesItsStampsAlone()
      {
          var date = new DateTime(2025, 12, 1); // before OneMinuteFrom
          await SeedDeviceRow(date, marker: null);
          var before = await Stored(date);

          var result = await _service.CreateUpdate(OfficeEdit(date, start1: 97, stop1: 193, nettoHours: 8));

          Assert.That(result.Success, Is.True, result.Message);
          var row = await Stored(date);
          Assert.Multiple(() =>
          {
              Assert.That(row.RegisteredUnderOneMinuteIntervals, Is.False,
                  "the day's mode is the mode at its own date, not the site's current flag");
              Assert.That(row.NettoHours, Is.EqualTo(8.0).Within(1e-9),
                  "a five-minute grid row keeps the hours the page posted");
              Assert.That(row.Start1StartedAt, Is.EqualTo(before.Start1StartedAt),
                  "stamp clearing is confined to one-minute rows");
              Assert.That(row.Stop1StoppedAt, Is.EqualTo(before.Stop1StoppedAt));
          });
      }
  }
  ```
- [ ] **Step 2 — Shard the new class (shard e) in BOTH workflows.**
  - `.github/workflows/dotnet-core-pr.yml:268` and `.github/workflows/dotnet-core-master.yml:279` (the `name: e` filter): replace the line's ending
    `|FullyQualifiedName=TimePlanning.Pn.Test.ExportTagFilterAndSiteTagsTests"`
    with
    `|FullyQualifiedName=TimePlanning.Pn.Test.ExportTagFilterAndSiteTagsTests|FullyQualifiedName=TimePlanning.Pn.Test.OfficeEditOneMinuteStaleStampsTests"`.
  - Before choosing, glance at the last green `stable` run's shard durations (`gh run list --workflow dotnet-core-master.yml -b stable -L 1`, then `gh run view <id>`); if e is the slowest of b/d/e, use the lightest of those instead — same letter in both files.
- [ ] **Step 3 — Run (optional (CI)).** Build must pass locally; the tests run in CI:
  ```bash
  dotnet build eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn.sln
  # CI parity (Docker required; skip locally per project rule):
  # tests run in CI only (local test runs are hook-blocked)
    --settings eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn.Test/test.runsettings \
    --filter "FullyQualifiedName~TimePlanning.Pn.Test.OfficeEditOneMinuteStaleStampsTests"
  ```
  Expected pre-fix: tests 1, 2 and 4 FAIL (hours from stamps: 45034 s / 42569 s; marker `True`), test 3 passes.
- [ ] **Step 4 — Implement.** In `UpdatePlanning` (`TimePlanningWorkingHoursService.cs:736-802`):
  - After `:742` (`var midnight = …;`) insert:
    ```csharp
            // The mode AT THIS ROW'S DATE (spec R3), never the site's current
            // flag: an office edit of a day before UseOneMinuteIntervalsFrom must
            // not re-register that day as one-minute (B4).
            var rowIsOneMinute = timeline.WasOneMinuteAt(planRegistration.Date);
    ```
  - Between `:756` (end of the `PaiedOutFlex = …;` statement) and `:757` (`planRegistration.Pause1Id = model.Shift1Pause ?? 0;`) insert:
    ```csharp
            // B2 — must run BEFORE the ids below are overwritten: it compares the
            // posted ids with the stored ones.
            if (rowIsOneMinute)
            {
                ClearStampsOfCorrectedShifts(planRegistration, model);
            }
    ```
  - Replace `:789-795`:
    ```csharp
        // Write-time mode marker: only the Date != midnight branch above rewrites
        // the Start/Stop ids, so only that branch re-registers the row's mode
        // (null site → marker unchanged; timeline fallback resolves it).
        if (planRegistration.Date != midnight && assignedSite != null)
        {
            planRegistration.RegisteredUnderOneMinuteIntervals = assignedSite.UseOneMinuteIntervals;
        }
    ```
    with
    ```csharp
        // Write-time mode marker: only the Date != midnight branch above rewrites
        // the Start/Stop ids, so only that branch re-registers the row's mode —
        // the mode at the row's own date (B4), not the site's current flag
        // (null site → marker unchanged; timeline fallback resolves it).
        if (planRegistration.Date != midnight && assignedSite != null)
        {
            planRegistration.RegisteredUnderOneMinuteIntervals = rowIsOneMinute;
        }
    ```
  - Insert after the closing `}` of `UpdatePlanning` (`:802`):
    ```csharp
    /// <summary>
    /// B2. A one-minute row's hours come from its device stamps
    /// (FlexChain.ComputeNettoSecondsFromDateTimeShifts). When the office
    /// corrects a shift's ids in the grid, those stamps still hold what the
    /// device recorded and would silently override the correction in the same
    /// save. Drop the stamps of exactly what the office changed; the hours then
    /// fall back to the office's ids. A changed start OR stop drops both of that
    /// shift's work stamps, so hours and displayed times come from the same ids.
    /// Shifts 1-2 only: the grid model carries no other shifts. Confined to this
    /// path — the single-day editor and the kiosk write exact stamps on purpose.
    /// </summary>
    private static void ClearStampsOfCorrectedShifts(PlanRegistration pr, TimePlanningWorkingHoursModel model)
    {
        if ((model.Shift1Start ?? 0) != pr.Start1Id || (model.Shift1Stop ?? 0) != pr.Stop1Id)
        {
            pr.Start1StartedAt = null;
            pr.Stop1StoppedAt = null;
        }

        if ((model.Shift1Pause ?? 0) != pr.Pause1Id)
        {
            pr.Pause1StartedAt = null;
            pr.Pause1StoppedAt = null;
        }

        if ((model.Shift2Start ?? 0) != pr.Start2Id || (model.Shift2Stop ?? 0) != pr.Stop2Id)
        {
            pr.Start2StartedAt = null;
            pr.Stop2StoppedAt = null;
        }

        if ((model.Shift2Pause ?? 0) != pr.Pause2Id)
        {
            pr.Pause2StartedAt = null;
            pr.Pause2StoppedAt = null;
        }
    }
    ```
    (Interpretation of spec §5.3 "set that shift's StartNStartedAt/StopNStoppedAt to null" as *both*. Pause scope is the spec's: primary `Pause{N}StartedAt/StoppedAt` only — owner decision.)
    **Known limitation (document in the PR):** the sub-slot pause stamps (Pause10..19/Pause100..102 for shift 1, Pause20..29/Pause200..202 for shift 2) are not cleared; when any of them holds a complete stamp pair, `ComputeShiftPauseSeconds` still sums the sub-slots and the office's pause id does not win for that shift.
- [ ] **Step 5 — Build.** `dotnet build eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn.sln` → 0 errors.
- [ ] **Step 6 — Commit.**
  ```bash
  git add eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn/Services/TimePlanningWorkingHoursService/TimePlanningWorkingHoursService.cs \
          eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn.Test/OfficeEditOneMinuteStaleStampsTests.cs \
          .github/workflows/dotnet-core-pr.yml .github/workflows/dotnet-core-master.yml
  git commit -m "$(cat <<'EOF'
  fix(working-hours): keep the office's correction on one-minute rows

  A grid save that corrected a shift on a one-minute row recomputed the hours
  from the device stamps, which still held the old times, so the correction
  was silently replaced in the same save (B2). The stamps of exactly the
  corrected start/stop or pause are now dropped before the ids are written,
  and the hours fall back to the office's ids.

  The row's mode marker now follows the mode at the row's own date instead of
  the site's current flag, so editing a day before UseOneMinuteIntervalsFrom
  no longer re-registers it as one-minute (B4).

  Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
  EOF
  )"
  ```

---

### Task 8 (T-P3) — Office edits carry forward through `RunForwardAsync`

**Files**
- Modify `P/Services/TimePlanningPlanningService/TimePlanningPlanningService.cs:987-989` (comment), `:1024-1060` (`Update` tail loop), `:1338-1340` (comment), `:1382-1418` (`UpdateByCurrentUserNam` tail loop).
- Modify `P/Services/TimePlanningWorkingHoursService/TimePlanningWorkingHoursService.cs:550-567` (comments only) and `:595-644` (grid tail cascade).
- Create `T/OfficeEditFlexCarryForwardTests.cs`.
- Modify `.github/workflows/dotnet-core-pr.yml:266` and `.github/workflows/dotnet-core-master.yml:277` (shard **d**).

**Interfaces**
- Consumes: `FlexChainRecompute.RunForwardAsync(TimePlanningPnDbContext, AssignedSite?, int sdkSitId, DateTime fromDateInclusive)`.
- Produces: no new members.

**Constraints kept (spec §3):**
- `ReconcileServiceTests` `:688 CreateUpdate_AcrossTheBoundary_SkipsLockedRowsAndSavesOpenOnes`, `:739 CreateUpdate_ALockedGapRowInTheMiddle_IsNotCreatedAndTheRestSaves`, `:785 CreateUpdate_PostingALockedDay_CascadesPastTheLockWithoutTouchingIt` (incl. `:819` `openAfter.Version > openBefore.Version`). Trace for `:785`: only `-8` (locked) is posted → the loop skips it → `RunForwardAsync(…, 931, -8)` walks `-8, -6, -3` (locked, not written, boundary seeds) and `-1` (open, stored `SumFlexStart = 9` → `0` = boundary `SumFlexEnd`) → changed → `Version + 1`. `:688`/`:739` post a locked first row; the walk starts on it and never writes a locked row. `CreateUpdate`'s `WhereOpen` load (`:556-560`) stays: it still keeps locked rows out of the per-row loop.
- `MobileFlexRecomputeAndCascadeTests.EarlyEdit_…` (`:288`, `:293`): walk from d1; d2 uses `NettoHoursOverride` 10 h (effective 36000 s), d3 stored 28500 s → 3600 + 14100 + 7200 − 300 = 24600 s; decimals back-derived from seconds.
- `UpdateByCurrentUserNam`'s five-minute leg is an **INTENTIONAL DIVERGENCE** (`:1353-1362`: it chains on computed `NettoHours` and ignores `NettoHoursOverrideActive`). `CarryChain` honours the override, so walking the edited day itself would rewrite what that leg just wrote. Therefore that path walks from `planning.Date.AddDays(1)` (seed = the edited row as saved). `Update`'s own chain (`ApplyNettoFlexChainDecimal` / `ApplyNettoFlexChainSecondPrecision`, pred = last row before, row marker = site flag = mode used) equals `CarryChain`'s, so it walks from `planning.Date` (no extra write).
- An edited open day cannot be followed by a locked day (lock = `Date <= MAX(reconciled Date)`), so `Update`/`UpdateByCurrentUserNam` never meet a locked row; the grid can (it posts locked rows).

**Existing tests exercising the touched code (must pass unchanged):** `ReconcileServiceTests` (all `CreateUpdate_*`, `Update_*`, `UpdateByCurrentUserNam_*`), `MobileFlexRecomputeAndCascadeTests`, `WorkingHoursMessagePersistenceTests`, `PlanningServiceMultiShiftTests`, `PlanningServiceAdminEditNonRoundMinutesTests`, `PlanRegistrationVersionHistoryTests`, `ExportTagFilterAndSiteTagsTests`, `OfficeEditOneMinuteStaleStampsTests` (T-P2).

- [ ] **Step 1 — Write the failing tests** `T/OfficeEditFlexCarryForwardTests.cs`:
  ```csharp
  using System;
  using System.Collections.Generic;
  using System.Threading.Tasks;
  using Microsoft.EntityFrameworkCore;
  using Microsoft.Extensions.Logging;
  using Microting.eForm.Infrastructure.Constants;
  using Microting.eFormApi.BasePn.Abstractions;
  using Microting.eFormApi.BasePn.Infrastructure.Database.Entities;
  using Microting.eFormApi.BasePn.Infrastructure.Helpers.PluginDbOptions;
  using NSubstitute;
  using NUnit.Framework;
  using TimePlanning.Pn.Infrastructure.Helpers;
  using TimePlanning.Pn.Infrastructure.Models.Planning;
  using TimePlanning.Pn.Infrastructure.Models.Settings;
  using TimePlanning.Pn.Infrastructure.Models.WorkingHours.Index;
  using TimePlanning.Pn.Infrastructure.Models.WorkingHours.UpdateCreate;
  using TimePlanning.Pn.Services.TimePlanningLocalizationService;
  using TimePlanning.Pn.Services.TimePlanningPlanningService;
  using TimePlanning.Pn.Services.TimePlanningWorkingHoursService;
  using AssignedSiteEntity = Microting.TimePlanningBase.Infrastructure.Data.Entities.AssignedSite;
  using PlanRegistrationEntity = Microting.TimePlanningBase.Infrastructure.Data.Entities.PlanRegistration;

  namespace TimePlanning.Pn.Test;

  /// <summary>
  /// R4: an office edit of day D carries the balance to the worker's LAST row,
  /// including rows pre-created for future dates, and (R2) never recomputes a
  /// later row's hours — it only re-chains Flex / SumFlexStart / SumFlexEnd.
  ///
  /// The later one-minute row's device stamps (07:00-17:00, 10 h) deliberately
  /// disagree with its stored hours (8 h): the old cascades re-derived them from
  /// the stamps, which is how historic hours changed in the 2026-09 incident.
  ///
  /// Ids: id n is (n - 1) * 5 minutes after midnight — 97 = 08:00, 193 = 16:00,
  /// 211 = 17:30, 217 = 18:00.
  /// </summary>
  [TestFixture]
  public class OfficeEditFlexCarryForwardTests : TestBaseSetup
  {
      private TimePlanningPlanningService _planningService = null!;
      private TimePlanningWorkingHoursService _workingHoursService = null!;

      [SetUp]
      public async Task SetUpTest()
      {
          await base.Setup();

          var userService = Substitute.For<IUserService>();
          userService.UserId.Returns(1);
          userService.GetCurrentUserAsync().Returns(new EformUser { Id = 1 });

          var localizationService = Substitute.For<ITimePlanningLocalizationService>();
          localizationService.GetString(Arg.Any<string>()).Returns(x => x[0]?.ToString());

          var coreService = Substitute.For<IEFormCoreService>();
          coreService.GetCore().Returns(await GetCore());

          var options = Substitute.For<IPluginDbOptions<TimePlanningBaseSettings>>();
          options.Value.Returns(new TimePlanningBaseSettings
          {
              AutoBreakCalculationActive = "0",
              DayOfPayment = 20,
              GpsEnabled = "0",
              SnapshotEnabled = "0"
          });

          var dbContextHelper = Substitute.For<ITimePlanningDbContextHelper>();
          dbContextHelper.GetDbContext().Returns(TimePlanningPnDbContext);

          _planningService = new TimePlanningPlanningService(
              Substitute.For<ILogger<TimePlanningPlanningService>>(),
              options,
              TimePlanningPnDbContext!,
              dbContextHelper,
              userService,
              localizationService,
              null!,
              coreService);

          _workingHoursService = new TimePlanningWorkingHoursService(
              Substitute.For<ILogger<TimePlanningWorkingHoursService>>(),
              TimePlanningPnDbContext!,
              userService,
              localizationService,
              baseDbContext: null!,
              options,
              coreService);
      }

      /// <summary>A five-minute site on the weekday-plan branch.</summary>
      private async Task SeedSite(int siteUid) =>
          await new AssignedSiteEntity
          {
              SiteId = siteUid,
              UseOneMinuteIntervals = false,
              UseGoogleSheetAsDefault = false,
              Resigned = false,
              WorkflowState = Constants.WorkflowStates.Created,
              CreatedByUserId = 1,
              UpdatedByUserId = 1
          }.Create(TimePlanningPnDbContext!);

      private async Task SeedFiveMinuteRow(int siteUid, DateTime date, int start1Id, int stop1Id,
          double planHours, double nettoHours, double sumFlexStart, double sumFlexEnd) =>
          await new PlanRegistrationEntity
          {
              SdkSitId = siteUid,
              Date = date,
              Start1Id = start1Id,
              Stop1Id = stop1Id,
              PlanHours = planHours,
              NettoHours = nettoHours,
              Flex = nettoHours - planHours,
              SumFlexStart = sumFlexStart,
              SumFlexEnd = sumFlexEnd,
              RegisteredUnderOneMinuteIntervals = false,
              PlanText = "",
              CommentOffice = "",
              CommentOfficeAll = "",
              WorkflowState = Constants.WorkflowStates.Created,
              CreatedByUserId = 1,
              UpdatedByUserId = 1
          }.Create(TimePlanningPnDbContext!);

      /// <summary>
      /// A one-minute row: ids and stored hours say 08:00-16:00 (8 h), the device
      /// stamps say 07:00-17:00 (10 h). Balance in step at <paramref name="sumFlexHours"/>.
      /// </summary>
      private async Task SeedOneMinuteRowWithDisagreeingStamps(int siteUid, DateTime date, double sumFlexHours) =>
          await new PlanRegistrationEntity
          {
              SdkSitId = siteUid,
              Date = date,
              Start1Id = 97,
              Stop1Id = 193,
              Start1StartedAt = date.AddHours(7),
              Stop1StoppedAt = date.AddHours(17),
              PlanHours = 8,
              PlanHoursInSeconds = 28800,
              NettoHours = 8,
              NettoHoursInSeconds = 28800,
              Flex = 0,
              FlexInSeconds = 0,
              SumFlexStart = sumFlexHours,
              SumFlexStartInSeconds = (int)Math.Round(sumFlexHours * 3600),
              SumFlexEnd = sumFlexHours,
              SumFlexEndInSeconds = (int)Math.Round(sumFlexHours * 3600),
              RegisteredUnderOneMinuteIntervals = true,
              PlanText = "",
              CommentOffice = "",
              CommentOfficeAll = "",
              WorkflowState = Constants.WorkflowStates.Created,
              CreatedByUserId = 1,
              UpdatedByUserId = 1
          }.Create(TimePlanningPnDbContext!);

      private async Task<PlanRegistrationEntity> Stored(int siteUid, DateTime date) =>
          await TimePlanningPnDbContext!.PlanRegistrations.AsNoTracking()
              .SingleAsync(x => x.SdkSitId == siteUid && x.Date == date);

      /// <summary>
      /// Seeds d0 (+1.5 h), d1 (0 h, balance 1.5), d2 (one-minute, stamps
      /// disagree, balance 1.5) and a row pre-created 30 days in the future
      /// (balance 1.5). The edit of d1 to 08:00-18:00 (10 h, plan 8 h) must end
      /// every later row at 3.5 h.
      /// </summary>
      private async Task SeedHistory(int siteUid, DateTime d0, DateTime d1, DateTime d2, DateTime future)
      {
          await SeedSite(siteUid);
          await SeedFiveMinuteRow(siteUid, d0, 97, 211, planHours: 8, nettoHours: 9.5, sumFlexStart: 0, sumFlexEnd: 1.5);
          await SeedFiveMinuteRow(siteUid, d1, 97, 193, planHours: 8, nettoHours: 8, sumFlexStart: 1.5, sumFlexEnd: 1.5);
          await SeedOneMinuteRowWithDisagreeingStamps(siteUid, d2, sumFlexHours: 1.5);
          await SeedFiveMinuteRow(siteUid, future, 0, 0, planHours: 0, nettoHours: 0, sumFlexStart: 1.5, sumFlexEnd: 1.5);
      }

      private async Task AssertCarriedThroughTheFutureRow(int siteUid, DateTime d1, DateTime d2, DateTime future)
      {
          var edited = await Stored(siteUid, d1);
          var later = await Stored(siteUid, d2);
          var last = await Stored(siteUid, future);
          Assert.Multiple(() =>
          {
              Assert.That(edited.NettoHours, Is.EqualTo(10.0).Within(1e-9), "the edited day's own hours");
              Assert.That(edited.SumFlexEnd, Is.EqualTo(3.5).Within(1e-9));
              Assert.That(later.SumFlexStart, Is.EqualTo(edited.SumFlexEnd).Within(1e-9),
                  "SumFlexStart(n+1) == SumFlexEnd(n)");
              Assert.That(later.SumFlexEnd, Is.EqualTo(3.5).Within(1e-9));
              Assert.That(later.SumFlexEndInSeconds, Is.EqualTo(3.5 * 3600),
                  "the one-minute row's seconds chain moves with it");
              Assert.That(later.NettoHoursInSeconds, Is.EqualTo(28800),
                  "a later row's stored hours are never re-derived from its stamps (R2)");
              Assert.That(later.NettoHours, Is.EqualTo(8.0).Within(1e-9));
              Assert.That(last.SumFlexStart, Is.EqualTo(later.SumFlexEnd).Within(1e-9),
                  "the row pre-created 30 days ahead is reached (R4)");
              Assert.That(last.SumFlexEnd, Is.EqualTo(3.5).Within(1e-9));
          });
      }

      [Test]
      public async Task Update_OfficeEditOfAPastDay_CarriesBalanceThroughAFuturePreCreatedRow_WithoutRecomputingLaterHours()
      {
          const int siteUid = 7701;
          var d0 = DateTime.Now.Date.AddDays(-10);
          var d1 = DateTime.Now.Date.AddDays(-9);
          var d2 = DateTime.Now.Date.AddDays(-8);
          var future = DateTime.Now.Date.AddDays(30);
          await SeedHistory(siteUid, d0, d1, d2, future);
          var editedRow = await Stored(siteUid, d1);

          var result = await _planningService.Update(editedRow.Id, new TimePlanningPlanningPrDayModel
          {
              Id = editedRow.Id,
              Date = d1,
              Start1Id = 97,
              Stop1Id = 217,   // 08:00-18:00 = 10 h
              PlanHours = 8,
              CommentOffice = ""
          });

          Assert.That(result.Success, Is.True, result.Message);
          await AssertCarriedThroughTheFutureRow(siteUid, d1, d2, future);
      }

      [Test]
      public async Task GridSave_OfAPastDay_CarriesBalanceThroughAFuturePreCreatedRow_WithoutRecomputingLaterHours()
      {
          const int siteUid = 7702;
          // Older than one month, so UpdatePlanRegistration never rewrites PlanHours.
          var d0 = DateTime.Now.Date.AddDays(-62);
          var d1 = DateTime.Now.Date.AddDays(-61);
          var d2 = DateTime.Now.Date.AddDays(-60);
          var future = DateTime.Now.Date.AddDays(30);
          await SeedHistory(siteUid, d0, d1, d2, future);

          var result = await _workingHoursService.CreateUpdate(new TimePlanningWorkingHoursUpdateCreateModel
          {
              SiteId = siteUid,
              Plannings = new List<TimePlanningWorkingHoursModel>
              {
                  new()
                  {
                      Date = d1,
                      Shift1Start = 97,
                      Shift1Stop = 217,
                      Shift1Pause = 0,
                      PlanHours = 8,
                      NettoHours = 10,  // a five-minute grid row saves the hours the page posts
                      FlexHours = 2,
                      PaidOutFlex = "0",
                      Message = 0,
                      PlanText = "",
                      CommentOffice = "",
                      CommentOfficeAll = "",
                      CommentWorker = ""
                  }
              }
          });

          Assert.That(result.Success, Is.True, result.Message);
          await AssertCarriedThroughTheFutureRow(siteUid, d1, d2, future);
      }
  }
  ```
  Expected pre-fix: `Update_…` fails (d2 `NettoHoursInSeconds` becomes 36000 from the stamps; the future row is not reached because the old loop stops at today). `GridSave_…` fails on d2 `NettoHoursInSeconds` (the tail cascade re-derives it from the stamps).
- [ ] **Step 2 — Shard (shard d) in BOTH workflows.** `.github/workflows/dotnet-core-pr.yml:266` and `.github/workflows/dotnet-core-master.yml:277`: replace the ending
  `|FullyQualifiedName=TimePlanning.Pn.Test.CalculatePayLinesForDayTests"`
  with
  `|FullyQualifiedName=TimePlanning.Pn.Test.CalculatePayLinesForDayTests|FullyQualifiedName=TimePlanning.Pn.Test.OfficeEditFlexCarryForwardTests"`.
- [ ] **Step 3 — Run (optional (CI)).**
  ```bash
  dotnet build eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn.sln
  # tests run in CI only (local test runs are hook-blocked)
    --settings eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn.Test/test.runsettings \
    --filter "FullyQualifiedName~TimePlanning.Pn.Test.OfficeEditFlexCarryForwardTests|FullyQualifiedName~TimePlanning.Pn.Test.ReconcileServiceTests|FullyQualifiedName~TimePlanning.Pn.Test.MobileFlexRecomputeAndCascadeTests"
  ```
- [ ] **Step 4 — Implement `TimePlanningPlanningService.Update`.**
  - `:987-989` — replace
    ```csharp
            // ONE query for this row's predecessor AND the whole forward
            // cascade below — never per row.
            var cascadeTimeline = await OneMinuteModeTimeline.BuildAsync(dbContext, assignedSite);
    ```
    with
    ```csharp
            // Resolves the predecessor's mode for this row's own chain; the
            // forward walk below builds its own timeline once.
            var cascadeTimeline = await OneMinuteModeTimeline.BuildAsync(dbContext, assignedSite);
    ```
  - Replace `:1024-1060` (from `await planning.Update(dbContext).ConfigureAwait(false);` through the closing `}` of the `foreach (var planningAfterThisPlanning in planningsAfterThisPlanning)` loop) with:
    ```csharp
            await planning.Update(dbContext).ConfigureAwait(false);

            // R4: carry the balance to the worker's LAST row — rows pre-created
            // for future dates included — re-chaining Flex/SumFlex only; later
            // rows keep their stored hours (R2). Walked from the edited day
            // itself: this path's own chain above is the same carry, so the day
            // is not written again. An open day is never followed by a locked
            // one, so the walk meets no lock here.
            await FlexChainRecompute.RunForwardAsync(dbContext, assignedSite, planning.SdkSitId, planning.Date)
                .ConfigureAwait(false);
    ```
- [ ] **Step 5 — Implement `TimePlanningPlanningService.UpdateByCurrentUserNam`.**
  - `:1338-1340` — same comment replacement as in step 4.
  - Replace `:1382-1418` (from `await planning.Update(dbContext).ConfigureAwait(false);` through the closing `}` of the `foreach`) with:
    ```csharp
            await planning.Update(dbContext).ConfigureAwait(false);

            // R4: carry the balance to the worker's LAST row (future rows
            // included), Flex/SumFlex only (R2). Starts the day AFTER the edit:
            // this path's five-minute leg keeps its intentional override-blind
            // formula (see above), which the walk's carry would otherwise
            // rewrite on the edited day.
            await FlexChainRecompute.RunForwardAsync(
                    dbContext, assignedSite, planning.SdkSitId, planning.Date.AddDays(1))
                .ConfigureAwait(false);
    ```
- [ ] **Step 6 — Implement the grid tail (`TimePlanningWorkingHoursService.CreateUpdate`).**
  - Comments only, `:551-555`: replace
    ```csharp
            // Locked rows are never even loaded, so nothing below can mutate
            // one and have a later save (which saves the whole context) flush
            // it. The forward cascade walks this same list, so it skips them
            // too. Not redundant with the loop's skip: only this keeps the
            // cascade out of the lock.
    ```
    with
    ```csharp
            // Locked rows are never even loaded, so nothing below can mutate
            // one and have a later save (which saves the whole context) flush
            // it. The forward walk at the end skips locked rows on its own.
    ```
    and `:561-562` + `:566`:
    ```csharp
            // Site-level one-minute flag drives the forward-cascade recompute below
            // so the double AND *InSeconds SumFlex columns are written consistently.
    ```
    → `// UpdatePlanning dereferences it; the forward walk below takes it too.`;
    `// ONE query for the whole cascade below — never per row.` → `// ONE query for every row saved below — never per row.`
  - Replace `:595-644` (from `// Check if there are any plannings after the last planning in the model` through the closing `}` of `if (lastPlanning != null)`) with:
    ```csharp
            // R4: carry the balance from the EARLIEST posted day to the worker's
            // LAST row — future pre-created rows included — re-chaining
            // Flex/SumFlex only; no later row's hours are recomputed (R2). Rows
            // the loop created are chained here too. Locked rows are passed,
            // never written, and the first open row after them seeds from the
            // lock boundary. (Posted dates were normalised to midnight above.)
            if (model.Plannings.Count > 0)
            {
                var earliestPostedDate = model.Plannings.Min(x => x.Date);
                await FlexChainRecompute.RunForwardAsync(dbContext, assignedSite, model.SiteId, earliestPostedDate);
            }
    ```
    (`System.Linq` and `Microting.TimePlanningBase.Infrastructure.Helpers` are already imported.)
- [ ] **Step 7 — Build.** `dotnet build eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn.sln` → 0 errors. Confirm the old loops are gone:
  `grep -n "planningsAfterThisPlanning\|planRegistrationsAfterLastPlanning" -r eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn` → no hits.
- [ ] **Step 8 — Commit.**
  ```bash
  git add eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn/Services/TimePlanningPlanningService/TimePlanningPlanningService.cs \
          eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn/Services/TimePlanningWorkingHoursService/TimePlanningWorkingHoursService.cs \
          eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn.Test/OfficeEditFlexCarryForwardTests.cs \
          .github/workflows/dotnet-core-pr.yml .github/workflows/dotnet-core-master.yml
  git commit -m "$(cat <<'EOF'
  fix(flex): carry office edits forward to the worker's last row

  The single-day editor (both copies) stopped its forward cascade at today,
  so rows pre-created for future dates kept the old balance, and it
  re-derived every later one-minute row's hours from its device stamps. The
  grid save walked only the rows after the last posted day, in load order,
  with the same hours recompute.

  All three now call the base FlexChainRecompute.RunForwardAsync once after
  their own save: from the edited day (grid: the earliest posted day) to the
  worker's last row, re-chaining Flex/SumFlex only, skipping reconciled days
  and writing only rows that change. The personal editor starts the day after
  the edit so its intentionally override-blind five-minute leg is kept.

  Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
  EOF
  )"
  ```

**PR2 wrap-up:** dual review gate (code-review + code-simplifier, in parallel) before each commit; push; `gh pr create --base stable`; watch `gh pr checks` to a verdict and classify every red shard against the latest `stable` run (CLAUDE.md step 11). PR description must state that `ComputePlanningNettoMinutes` is deliberately unchanged (owner decision, T-P1 step 1), the B2 start/stop interpretation, and the B2 pause-slot known limitation.

---

## PR3 — `fix/flex-chain-other-writers` (off `origin/stable` after PR2 is merged)

```bash
git fetch origin && git checkout -b fix/flex-chain-other-writers origin/stable
```
Shard anchors below assume PR2's lines: `d` ends with `OfficeEditFlexCarryForwardTests"`, `e` ends with `OfficeEditOneMinuteStaleStampsTests"`. Re-verify the line numbers (`grep -n "name: [bde]$" .github/workflows/dotnet-core-*.yml`) — PR2 does not add lines, so they should still be pr `262/266/268`, master `273/277/279`.

### Task 9 (T-P4) — Flex tab (`TimePlanningFlexService`) walks from the day after each written row

**Decision (owner, 2026-09-24):** both `CreatePlanning` and `UpdatePlanning` walk from the day AFTER the created/edited row
(`RunForwardAsync(db, site, sdkSitId, row.Date.AddDays(1))`). The row itself keeps the balance the flex tab wrote — including
an office-entered start balance on a created row (`SumFlexEnd = model.SumFlexStart - model.PaidOutFlex`, `:267`) — and every
later row carries from its `SumFlexEnd`.

**Files**
- Modify `P/Services/TimePlanningFlexService/TimePlanningFlexService.cs:259-274` (`CreatePlanning`) and `:276-333` (`UpdatePlanning`).
- Create `T/FlexTabPaidOutCarryForwardTests.cs`.
- Modify `.github/workflows/dotnet-core-pr.yml:262`, `.github/workflows/dotnet-core-master.yml:273` (shard **b**).

**Interfaces**
- Consumes: `FlexChainRecompute.RunForwardAsync` (`using Microting.TimePlanningBase.Infrastructure.Helpers;` already at `:45`).
- Produces: none.

**Notes**
- Every posted day was checked unlocked at `:159-170` before any write; an open day is never followed by a locked one.
- One walk per written row, in posted order. When one batch posts several days for the same worker, a walk started by an
  earlier-dated row re-carries a later posted row that was already written in the same batch (i.e. a later posted created
  row's office start balance is then replaced by the carried one). The UI posts one row per worker; accepted as-is — mention in the PR.

**Existing tests exercising the touched code:** `TimePlanningFlexServiceRemovedRowTests` (asserts comments / paid-out only;
no `SumFlex*` assertions; no AssignedSite → five-minute walk; its lock-refusal cases return before any write).

- [ ] **Step 1 — Failing tests** `T/FlexTabPaidOutCarryForwardTests.cs`:
  ```csharp
  using System;
  using System.Collections.Generic;
  using System.Threading.Tasks;
  using Microsoft.EntityFrameworkCore;
  using Microsoft.Extensions.Logging;
  using Microting.eForm.Infrastructure.Constants;
  using Microting.eFormApi.BasePn.Abstractions;
  using Microting.eFormApi.BasePn.Infrastructure.Helpers.PluginDbOptions;
  using Microting.eFormApi.BasePn.Infrastructure.Models.Common;
  using Microting.TimePlanningBase.Infrastructure.Data.Entities;
  using NSubstitute;
  using NUnit.Framework;
  using TimePlanning.Pn.Infrastructure.Models.Flex.Update;
  using TimePlanning.Pn.Infrastructure.Models.Settings;
  using TimePlanning.Pn.Services.TimePlanningFlexService;
  using TimePlanning.Pn.Services.TimePlanningLocalizationService;

  namespace TimePlanning.Pn.Test;

  /// <summary>
  /// R4 on the flex tab: the row the flex tab writes keeps exactly the balance
  /// the flex tab gave it (an office-entered start balance included), and every
  /// later row — including one pre-created for a future date — carries from
  /// that row's SumFlexEnd.
  /// </summary>
  [TestFixture]
  public class FlexTabPaidOutCarryForwardTests : TestBaseSetup
  {
      private const int SdkSitId = 7901;
      private ITimePlanningFlexService _service = null!;

      [SetUp]
      public async Task SetUpTest()
      {
          await base.Setup();

          var userService = Substitute.For<IUserService>();
          userService.UserId.Returns(1);
          var localizationService = Substitute.For<ITimePlanningLocalizationService>();
          localizationService.GetString(Arg.Any<string>()).Returns(x => x[0]?.ToString());
          var coreService = Substitute.For<IEFormCoreService>();
          coreService.GetCore().Returns(await GetCore());
          var options = Substitute.For<IPluginDbOptions<TimePlanningBaseSettings>>();
          options.Value.Returns(new TimePlanningBaseSettings());

          _service = new TimePlanningFlexService(
              Substitute.For<ILogger<TimePlanningFlexService>>(),
              TimePlanningPnDbContext!,
              userService,
              localizationService,
              coreService,
              options);
      }

      private async Task Seed(DateTime date, double planHours, double nettoHours, double sumFlexStart, double sumFlexEnd) =>
          await new PlanRegistration
          {
              SdkSitId = SdkSitId,
              Date = date,
              PlanHours = planHours,
              NettoHours = nettoHours,
              Flex = nettoHours - planHours,
              SumFlexStart = sumFlexStart,
              SumFlexEnd = sumFlexEnd,
              StatusCaseId = 0,
              CommentOffice = "",
              CommentOfficeAll = "",
              CreatedByUserId = 1,
              UpdatedByUserId = 1
          }.Create(TimePlanningPnDbContext!);

      private async Task<PlanRegistration> Stored(DateTime date) =>
          await TimePlanningPnDbContext!.PlanRegistrations.AsNoTracking()
              .SingleAsync(x => x.SdkSitId == SdkSitId && x.Date == date
                                && x.WorkflowState != Constants.WorkflowStates.Removed);

      private static TimePlanningFlexUpdateModel Entry(DateTime date, double paidOut, double sumFlexStart) => new()
      {
          Date = date,
          Worker = new CommonDictionaryModel { Id = SdkSitId },
          PaidOutFlex = paidOut,
          SumFlexStart = sumFlexStart,
          CommentOffice = "flex tab",
          CommentOfficeAll = "flex tab"
      };

      [Test]
      public async Task PayingOutFlexOnAnExistingDay_KeepsThatDaysBalance_AndCarriesItThroughAFutureRow()
      {
          var d0 = DateTime.Now.Date.AddDays(-6);
          var d1 = DateTime.Now.Date.AddDays(-5);
          var future = DateTime.Now.Date.AddDays(25);
          await Seed(d0, planHours: 8, nettoHours: 8, sumFlexStart: 2, sumFlexEnd: 2);
          await Seed(d1, planHours: 7, nettoHours: 8, sumFlexStart: 2, sumFlexEnd: 3);
          await Seed(future, planHours: 0, nettoHours: 0, sumFlexStart: 3, sumFlexEnd: 3);

          var result = await _service.UpdateCreate(new List<TimePlanningFlexUpdateModel> { Entry(d1, paidOut: 1, sumFlexStart: 2) });

          Assert.That(result.Success, Is.True, result.Message);
          var paid = await Stored(d1);
          var last = await Stored(future);
          Assert.Multiple(() =>
          {
              Assert.That(paid.SumFlexStart, Is.EqualTo(2.0).Within(1e-9), "the edited row's start is left as it was");
              Assert.That(paid.SumFlexEnd, Is.EqualTo(2.0).Within(1e-9), "3 h minus the 1 h paid out, as the flex tab wrote it");
              Assert.That(last.SumFlexStart, Is.EqualTo(paid.SumFlexEnd).Within(1e-9),
                  "SumFlexStart(n+1) == SumFlexEnd(n) through the future row");
              Assert.That(last.SumFlexEnd, Is.EqualTo(2.0).Within(1e-9));
          });
      }

      [Test]
      public async Task CreatingAFlexRowWithAnOfficeStartBalance_KeepsIt_AndCarriesItThroughAFutureRow()
      {
          var d0 = DateTime.Now.Date.AddDays(-6);
          var d1 = DateTime.Now.Date.AddDays(-5); // no row yet: the flex tab creates it
          var future = DateTime.Now.Date.AddDays(25);
          await Seed(d0, planHours: 8, nettoHours: 8, sumFlexStart: 2, sumFlexEnd: 2);
          await Seed(future, planHours: 0, nettoHours: 0, sumFlexStart: 2, sumFlexEnd: 2);

          // The office enters a start balance of 10 h (differs from d0's 2 h) and pays out 1 h.
          var result = await _service.UpdateCreate(new List<TimePlanningFlexUpdateModel> { Entry(d1, paidOut: 1, sumFlexStart: 10) });

          Assert.That(result.Success, Is.True, result.Message);
          var created = await Stored(d1);
          var last = await Stored(future);
          Assert.Multiple(() =>
          {
              Assert.That(created.SumFlexEnd, Is.EqualTo(9.0).Within(1e-9),
                  "the office-entered start balance minus the payout is preserved, not re-carried from d0");
              Assert.That(created.PaiedOutFlex, Is.EqualTo(1.0).Within(1e-9));
              Assert.That(last.SumFlexStart, Is.EqualTo(created.SumFlexEnd).Within(1e-9),
                  "later rows carry from the created row");
              Assert.That(last.SumFlexEnd, Is.EqualTo(9.0).Within(1e-9));
          });
      }
  }
  ```
  Pre-fix: in both tests `last.SumFlexStart` keeps its seeded value (3 / 2) → fails.
- [ ] **Step 2 — Shard b** (`dotnet-core-pr.yml:262`, `dotnet-core-master.yml:273`): replace ending
  `|FullyQualifiedName=TimePlanning.Pn.Test.PauseOverrideInferenceTests"` with
  `|FullyQualifiedName=TimePlanning.Pn.Test.PauseOverrideInferenceTests|FullyQualifiedName=TimePlanning.Pn.Test.FlexTabPaidOutCarryForwardTests"`.
- [ ] **Step 3 — Run (optional (CI)):** `dotnet build eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn.sln`; CI filter `FullyQualifiedName~TimePlanning.Pn.Test.FlexTabPaidOutCarryForwardTests|FullyQualifiedName~TimePlanning.Pn.Test.TimePlanningFlexServiceRemovedRowTests`.
- [ ] **Step 4 — Implement.**
  - `CreatePlanning` — replace `:273` `        await planning.Create(dbContext);` with:
    ```csharp
        await planning.Create(dbContext);

        // R4: carry this row's balance to the worker's later rows (future rows
        // included). From the NEXT day: the office-entered start balance on
        // this row is kept as written.
        var assignedSite = await dbContext.AssignedSites
            .AsNoTracking()
            .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
            .FirstOrDefaultAsync(x => x.SiteId == sdkSiteId);
        await FlexChainRecompute.RunForwardAsync(dbContext, assignedSite, sdkSiteId, planning.Date.AddDays(1));
    ```
  - `UpdatePlanning` — replace `:332` `        await planRegistration.Update(dbContext);` with:
    ```csharp
        await planRegistration.Update(dbContext);

        // R4: carry the adjusted balance to the worker's later rows (future
        // rows included). From the NEXT day: this row keeps the balance the
        // flex tab just wrote.
        await FlexChainRecompute.RunForwardAsync(
            dbContext, assignedSite, planRegistration.SdkSitId, planRegistration.Date.AddDays(1));
    ```
    (`assignedSite` is the `AsNoTracking` lookup already at `:282-285`; `RunForwardAsync` only reads it.)
- [ ] **Step 5 — Build.** `dotnet build …sln` → 0 errors.
- [ ] **Step 6 — Commit.**
  ```bash
  git add eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn/Services/TimePlanningFlexService/TimePlanningFlexService.cs \
          eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn.Test/FlexTabPaidOutCarryForwardTests.cs \
          .github/workflows/dotnet-core-pr.yml .github/workflows/dotnet-core-master.yml
  git commit -m "$(cat <<'EOF'
  fix(flex-tab): carry a flex-tab change to the worker's later days

  Paying out flex on a past day, or creating a flex row with an office start
  balance, changed that day's balance only, so every later row kept the old
  balance and the chain broke at the next day. After each write the flex tab
  now walks the worker's balance forward with the base RunForwardAsync from
  the following day; the written row keeps the balance the flex tab gave it.

  Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
  EOF
  )"
  ```

### Task 10 (T-P5) — Absence approval walks from the earliest approved day

**Files**
- Modify `P/Services/AbsenceRequestService/AbsenceRequestService.cs` usings (after `using Microting.TimePlanningBase.Infrastructure.Data.Entities;`, ~`:44`) and `ApproveAsync` `:283-287`.
- Create `T/AbsenceApprovalFlexCarryForwardTests.cs`.
- Modify `.github/workflows/dotnet-core-pr.yml:268`, `.github/workflows/dotnet-core-master.yml:279` (shard **e**).

**Interfaces**
- Consumes: `FlexChainRecompute.RunForwardAsync` with `AssignedSite?` = null when the worker has none (the absence tests seed none; the base builds the all-five-minute timeline for null).
- Produces: none. `ApplyAbsenceToPlanRegistration` (`:573-686`) is not changed — the walk runs once after the loop.

**Existing tests exercising the touched code:** `AbsenceRequestServiceTests` (incl. `ApproveAsync_UpdatesPlanRegistrations_AndSetsAbsenceFlags`, lock-refusal cases — refused before the walk), `AbsenceRequestRemovedRowTests`, `PushNotificationIntegrationTests`, `GrpcServices.TimePlanningAbsenceRequestGrpcServiceTests`.

- [ ] **Step 1 — Failing test** `T/AbsenceApprovalFlexCarryForwardTests.cs`:
  ```csharp
  using System;
  using System.Threading.Tasks;
  using Microsoft.EntityFrameworkCore;
  using Microting.EformAngularFrontendBase.Infrastructure.Data;
  using Microting.eForm.Infrastructure.Constants;
  using Microting.eFormApi.BasePn.Abstractions;
  using Microting.TimePlanningBase.Infrastructure.Data.Entities;
  using NSubstitute;
  using NUnit.Framework;
  using TimePlanning.Pn.Infrastructure.Models.AbsenceRequest;
  using TimePlanning.Pn.Services.AbsenceRequestService;
  using TimePlanning.Pn.Services.TimePlanningLocalizationService;

  namespace TimePlanning.Pn.Test;

  /// <summary>
  /// R4 on absence approval: approving creates rows for days that had none,
  /// with an empty balance. The worker's balance must be carried through those
  /// rows and on to the last row, including one pre-created for a future date.
  /// No AssignedSite is seeded: the walk must tolerate its absence.
  /// </summary>
  [TestFixture]
  public class AbsenceApprovalFlexCarryForwardTests : TestBaseSetup
  {
      private const int SdkSitId = 7951;
      private IAbsenceRequestService _service = null!;

      [SetUp]
      public async Task SetUpTest()
      {
          await base.Setup();
          var userService = Substitute.For<IUserService>();
          userService.UserId.Returns(1);
          var localizationService = Substitute.For<ITimePlanningLocalizationService>();
          localizationService.GetString(Arg.Any<string>()).Returns(x => x[0]?.ToString());
          var coreService = Substitute.For<IEFormCoreService>();
          var core = await GetCore();
          coreService.GetCore().Returns(Task.FromResult(core));

          _service = new AbsenceRequestService(
              Substitute.For<Microsoft.Extensions.Logging.ILogger<AbsenceRequestService>>(),
              TimePlanningPnDbContext!,
              userService,
              localizationService,
              coreService,
              Substitute.For<BaseDbContext>(new DbContextOptions<BaseDbContext>()),
              Substitute.For<TimePlanning.Pn.Services.PushNotificationService.IPushNotificationService>());
      }

      private async Task SeedRow(DateTime date, double planHours, double nettoHours, double sumFlexStart, double sumFlexEnd) =>
          await new PlanRegistration
          {
              SdkSitId = SdkSitId,
              Date = date,
              PlanHours = planHours,
              NettoHours = nettoHours,
              Flex = nettoHours - planHours,
              SumFlexStart = sumFlexStart,
              SumFlexEnd = sumFlexEnd,
              CreatedByUserId = 1,
              UpdatedByUserId = 1
          }.Create(TimePlanningPnDbContext!);

      private async Task<PlanRegistration> Stored(DateTime date) =>
          await TimePlanningPnDbContext!.PlanRegistrations.AsNoTracking()
              .SingleAsync(x => x.SdkSitId == SdkSitId && x.Date == date
                                && x.WorkflowState != Constants.WorkflowStates.Removed);

      [Test]
      public async Task ApprovingAbsenceOnDaysWithoutRows_ChainsTheNewRows_AndCarriesToAFutureRow()
      {
          var before = DateTime.Now.Date.AddDays(-12);
          var day1 = DateTime.Now.Date.AddDays(-10);
          var day2 = DateTime.Now.Date.AddDays(-9);
          var future = DateTime.Now.Date.AddDays(20);
          await SeedRow(before, planHours: 0, nettoHours: 0, sumFlexStart: 5, sumFlexEnd: 5);
          // Stale on purpose: plan 7.5, worked 8 → +0.5 on top of 5 is 5.5.
          await SeedRow(future, planHours: 7.5, nettoHours: 8, sumFlexStart: 99, sumFlexEnd: 99.5);

          var request = new AbsenceRequest
          {
              RequestedBySdkSitId = SdkSitId,
              DateFrom = day1,
              DateTo = day2,
              Status = AbsenceRequestStatus.Pending,
              RequestedAtUtc = DateTime.UtcNow,
              CreatedByUserId = 1,
              UpdatedByUserId = 1
          };
          await request.Create(TimePlanningPnDbContext!);
          foreach (var date in new[] { day1, day2 })
          {
              await new AbsenceRequestDay
              {
                  AbsenceRequestId = request.Id,
                  Date = date,
                  MessageId = 2, // Vacation (seeded)
                  CreatedByUserId = 1,
                  UpdatedByUserId = 1
              }.Create(TimePlanningPnDbContext!);
          }

          var result = await _service.ApproveAsync(request.Id,
              new AbsenceRequestDecisionModel { ManagerSdkSitId = 2, DecisionComment = "ok" });

          Assert.That(result.Success, Is.True, result.Message);
          var r0 = await Stored(before);
          var r1 = await Stored(day1);
          var r2 = await Stored(day2);
          var last = await Stored(future);
          Assert.Multiple(() =>
          {
              Assert.That(r1.OnVacation, Is.True);
              Assert.That(r1.SumFlexStart, Is.EqualTo(r0.SumFlexEnd).Within(1e-9), "created row chained");
              Assert.That(r1.SumFlexEnd, Is.EqualTo(5.0).Within(1e-9));
              Assert.That(r2.SumFlexStart, Is.EqualTo(r1.SumFlexEnd).Within(1e-9));
              Assert.That(last.SumFlexStart, Is.EqualTo(r2.SumFlexEnd).Within(1e-9),
                  "carried through the future pre-created row");
              Assert.That(last.SumFlexEnd, Is.EqualTo(5.5).Within(1e-9));
              Assert.That(last.NettoHours, Is.EqualTo(8.0).Within(1e-9), "hours untouched (R2)");
          });
      }
  }
  ```
  Pre-fix: `r1.SumFlexStart` is 0 → fails.
- [ ] **Step 2 — Shard e** (`dotnet-core-pr.yml:268`, `dotnet-core-master.yml:279`): replace ending
  `|FullyQualifiedName=TimePlanning.Pn.Test.OfficeEditOneMinuteStaleStampsTests"` with
  `|FullyQualifiedName=TimePlanning.Pn.Test.OfficeEditOneMinuteStaleStampsTests|FullyQualifiedName=TimePlanning.Pn.Test.AbsenceApprovalFlexCarryForwardTests"`.
- [ ] **Step 3 — Run (optional (CI)):** build; CI filter `FullyQualifiedName~TimePlanning.Pn.Test.AbsenceApprovalFlexCarryForwardTests|FullyQualifiedName~TimePlanning.Pn.Test.AbsenceRequestServiceTests|FullyQualifiedName~TimePlanning.Pn.Test.AbsenceRequestRemovedRowTests`.
- [ ] **Step 4 — Implement.** Add `using Microting.TimePlanningBase.Infrastructure.Helpers;` after `using Microting.TimePlanningBase.Infrastructure.Data.Entities;`. In `ApproveAsync` replace `:283-287`:
  ```csharp
            // Apply absence to each day's PlanRegistration
            foreach (var day in request.Days!)
            {
                await ApplyAbsenceToPlanRegistration(request, day);
            }
  ```
  with
  ```csharp
            // Apply absence to each day's PlanRegistration
            foreach (var day in request.Days!)
            {
                await ApplyAbsenceToPlanRegistration(request, day);
            }

            // R4: approval can create rows mid-history with an empty balance.
            // Carry the worker's balance from the earliest approved day to their
            // last row, the new rows included. Every day was checked unlocked
            // above. Tolerates a worker without an AssignedSite.
            if (request.Days!.Count > 0)
            {
                var assignedSite = await _dbContext.AssignedSites
                    .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                    .FirstOrDefaultAsync(x => x.SiteId == request.RequestedBySdkSitId);
                await FlexChainRecompute.RunForwardAsync(
                    _dbContext, assignedSite, request.RequestedBySdkSitId, request.Days.Min(d => d.Date));
            }
  ```
  (`AbsenceRequest.Days` is a non-null `ICollection<AbsenceRequestDay>` in the base; the `!` mirrors the surrounding code.)
- [ ] **Step 5 — Build.** → 0 errors.
- [ ] **Step 6 — Commit** (files: `AbsenceRequestService.cs`, `AbsenceApprovalFlexCarryForwardTests.cs`, both workflows):
  ```
  fix(absence): chain approved days into the worker's flex balance

  Approving an absence created rows for days without one with an empty
  balance, breaking the chain at that day and every day after. Approval now
  walks the worker's balance forward with the base RunForwardAsync from the
  earliest approved day.

  Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
  ```

### Task 11 (T-P6) — Content handover accept walks both workers

**Files**
- Modify `P/Services/ContentHandoverService/ContentHandoverService.cs:950-953` (after the "status -> Accepted" log, before `// 8. Fire-and-forget push to sender.` at `:954`). Usings unchanged (`Microting.TimePlanningBase.Infrastructure.Helpers` at `:44`).
- Create `T/HandoverAcceptFlexCarryForwardTests.cs`.
- Modify `.github/workflows/dotnet-core-pr.yml:266`, `.github/workflows/dotnet-core-master.yml:277` (shard **d**).

**Interfaces**
- Consumes: `FlexChainRecompute.RunForwardAsync`; `fromAssignedSite`/`toAssignedSite` already resolved at `:685-690` (may be null).
- Produces: none.

**Why walk from the handover day itself:** both paths change `PlanHours` (partial `:747`, `:786` via `RecalculatePlanHoursFromShifts`; full-day `:847`, `:891`) without re-chaining the row, so the day's own Flex/SumFlexEnd is stale; `RunForwardAsync` carries the first day too. Placed after the request is saved: a walk failure then cannot leave content moved with the request still Pending.

**Existing tests exercising the touched code:** `ContentHandoverServiceTests` (Accept tests, lock refusals before any write), `ContentHandoverRemovedRowTests`, `GrpcServices.TimePlanningContentHandoverGrpcServiceTests`.

- [ ] **Step 1 — Failing test** `T/HandoverAcceptFlexCarryForwardTests.cs`:
  ```csharp
  using System;
  using System.Threading.Tasks;
  using Microsoft.EntityFrameworkCore;
  using Microsoft.Extensions.DependencyInjection;
  using Microting.EformAngularFrontendBase.Infrastructure.Data;
  using Microting.eFormApi.BasePn.Abstractions;
  using Microting.TimePlanningBase.Infrastructure.Data.Entities;
  using NSubstitute;
  using NUnit.Framework;
  using TimePlanning.Pn.Infrastructure.Models.ContentHandover;
  using TimePlanning.Pn.Services.ContentHandoverService;
  using TimePlanning.Pn.Services.PushNotificationService;
  using TimePlanning.Pn.Services.TimePlanningLocalizationService;

  namespace TimePlanning.Pn.Test;

  /// <summary>
  /// R4 on handover accept: moving a day's plan changes both workers' PlanHours
  /// on that day, so both balances must be re-chained from that day through
  /// each worker's last row. No AssignedSites are seeded (five-minute walk).
  /// </summary>
  [TestFixture]
  public class HandoverAcceptFlexCarryForwardTests : TestBaseSetup
  {
      private const int Sender = 7961;
      private const int Receiver = 7962;
      private IContentHandoverService _service = null!;

      [SetUp]
      public async Task SetUpTest()
      {
          await base.Setup();
          var userService = Substitute.For<IUserService>();
          userService.UserId.Returns(1);
          var localizationService = Substitute.For<ITimePlanningLocalizationService>();
          localizationService.GetString(Arg.Any<string>()).Returns(x => x[0]?.ToString());

          var push = Substitute.For<IPushNotificationService>();
          var scopeProvider = Substitute.For<IServiceProvider>();
          scopeProvider.GetService(typeof(IPushNotificationService)).Returns(push);
          var scope = Substitute.For<IServiceScope>();
          scope.ServiceProvider.Returns(scopeProvider);
          var scopeFactory = Substitute.For<IServiceScopeFactory>();
          scopeFactory.CreateScope().Returns(scope);

          _service = new ContentHandoverService(
              Substitute.For<Microsoft.Extensions.Logging.ILogger<ContentHandoverService>>(),
              TimePlanningPnDbContext!,
              userService,
              localizationService,
              Substitute.For<IEFormCoreService>(),
              Substitute.For<BaseDbContext>(new DbContextOptions<BaseDbContext>()),
              scopeFactory);
      }

      private async Task<PlanRegistration> Seed(int sdkSitId, DateTime date, double planHours, double nettoHours,
          double sumFlexStart, double sumFlexEnd, string? planText = null)
      {
          var row = new PlanRegistration
          {
              SdkSitId = sdkSitId,
              Date = date,
              PlanHours = planHours,
              PlanHoursInSeconds = (int)Math.Round(planHours * 3600),
              NettoHours = nettoHours,
              Flex = nettoHours - planHours,
              SumFlexStart = sumFlexStart,
              SumFlexEnd = sumFlexEnd,
              PlanText = planText,
              CreatedByUserId = 1,
              UpdatedByUserId = 1
          };
          await row.Create(TimePlanningPnDbContext!);
          return row;
      }

      private async Task<PlanRegistration> Stored(int sdkSitId, DateTime date) =>
          await TimePlanningPnDbContext!.PlanRegistrations.AsNoTracking()
              .SingleAsync(x => x.SdkSitId == sdkSitId && x.Date == date);

      [Test]
      public async Task AcceptingAFullDayHandover_RechainsBothWorkersFromTheDayThroughTheirLastRows()
      {
          var date = new DateTime(2024, 1, 10);
          var later = date.AddDays(30);
          await Seed(Sender, date.AddDays(-1), 0, 0, 3, 3);
          var source = await Seed(Sender, date, planHours: 8, nettoHours: 8, sumFlexStart: 3, sumFlexEnd: 3,
              planText: "Important work");
          await Seed(Sender, later, 0, 0, 3, 3);
          await Seed(Receiver, date.AddDays(-1), 0, 0, 1, 1);
          var target = await Seed(Receiver, date, planHours: 0, nettoHours: 0, sumFlexStart: 1, sumFlexEnd: 1);
          await Seed(Receiver, later, 0, 0, 1, 1);

          var request = new PlanRegistrationContentHandoverRequest
          {
              FromSdkSitId = Sender,
              ToSdkSitId = Receiver,
              Date = date,
              FromPlanRegistrationId = source.Id,
              ToPlanRegistrationId = target.Id,
              Status = HandoverRequestStatus.Pending,
              RequestedAtUtc = DateTime.UtcNow,
              CreatedByUserId = 1,
              UpdatedByUserId = 1
          };
          await request.Create(TimePlanningPnDbContext!);

          var result = await _service.AcceptAsync(request.Id, Receiver,
              new ContentHandoverDecisionModel { DecisionComment = "ok" });

          Assert.That(result.Success, Is.True, result.Message);
          var senderDay = await Stored(Sender, date);
          var senderLater = await Stored(Sender, later);
          var receiverDay = await Stored(Receiver, date);
          var receiverLater = await Stored(Receiver, later);
          Assert.Multiple(() =>
          {
              // Sender: plan 8 → 0 with 8 h worked → +8 on top of 3.
              Assert.That(senderDay.Flex, Is.EqualTo(8.0).Within(1e-9));
              Assert.That(senderDay.SumFlexEnd, Is.EqualTo(11.0).Within(1e-9));
              Assert.That(senderLater.SumFlexStart, Is.EqualTo(senderDay.SumFlexEnd).Within(1e-9));
              // Receiver: plan 0 → 8 with nothing worked → −8 on top of 1.
              Assert.That(receiverDay.Flex, Is.EqualTo(-8.0).Within(1e-9));
              Assert.That(receiverDay.SumFlexEnd, Is.EqualTo(-7.0).Within(1e-9));
              Assert.That(receiverLater.SumFlexStart, Is.EqualTo(receiverDay.SumFlexEnd).Within(1e-9));
          });
      }
  }
  ```
  Pre-fix: `senderDay.SumFlexEnd` stays 3 → fails.
- [ ] **Step 2 — Shard d** (`dotnet-core-pr.yml:266`, `dotnet-core-master.yml:277`): replace ending
  `|FullyQualifiedName=TimePlanning.Pn.Test.OfficeEditFlexCarryForwardTests"` with
  `|FullyQualifiedName=TimePlanning.Pn.Test.OfficeEditFlexCarryForwardTests|FullyQualifiedName=TimePlanning.Pn.Test.HandoverAcceptFlexCarryForwardTests"`.
- [ ] **Step 3 — Run (optional (CI)):** build; CI filter `FullyQualifiedName~TimePlanning.Pn.Test.HandoverAcceptFlexCarryForwardTests|FullyQualifiedName~TimePlanning.Pn.Test.ContentHandoverServiceTests|FullyQualifiedName~TimePlanning.Pn.Test.ContentHandoverRemovedRowTests`.
- [ ] **Step 4 — Implement.** In `AcceptAsync`, after
  ```csharp
            _logger.LogInformation(
                "[Handover] Accept request {RequestId}: status -> Accepted, RespondedAtUtc={RespondedAt:O}",
                requestId, request.RespondedAtUtc);
  ```
  (`:950-952`) insert:
  ```csharp

            // 7b. R4: the move changed PlanHours on both days without re-chaining
            // them. Carry each worker's balance from the handover day through
            // their last row (one walk per worker; both days were checked
            // unlocked above). After the status save, so a failure here cannot
            // leave moved content with a Pending request.
            var walks = new[]
                {
                    (SdkSitId: fromPR.SdkSitId, Date: fromPR.Date, Site: fromAssignedSite),
                    (SdkSitId: toPR.SdkSitId, Date: toPR.Date, Site: toAssignedSite)
                }
                .GroupBy(x => x.SdkSitId)
                .Select(g => (SdkSitId: g.Key, From: g.Min(x => x.Date), Site: g.First().Site));
            foreach (var walk in walks)
            {
                var changed = await FlexChainRecompute.RunForwardAsync(
                    _dbContext, walk.Site, walk.SdkSitId, walk.From);
                _logger.LogInformation(
                    "[Handover] Accept request {RequestId}: flex carried for sdkSitId={SdkSitId} from {From:yyyy-MM-dd}, {Changed} row(s) changed",
                    requestId, walk.SdkSitId, walk.From, changed);
            }
  ```
- [ ] **Step 5 — Build.** → 0 errors.
- [ ] **Step 6 — Commit** (files: `ContentHandoverService.cs`, `HandoverAcceptFlexCarryForwardTests.cs`, both workflows):
  ```
  fix(handover): re-chain both workers' balances after an accepted handover

  Accepting a handover moved PlanHours between two workers' days without
  re-chaining either day, so both days and everything after them kept a stale
  balance. Accept now walks each worker's balance forward with the base
  RunForwardAsync from the handover day.

  Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
  ```

### Moved out of this plan — Google Sheet pull walk (was T-P7)

Spec §6 listed "Google Sheet pull: one walk per worker from the earliest pulled date". **Owner decision (2026-09-24): it moves to
sub-project 5**, together with the INVERTED-SUMFLEX-SIGN fix and the `PlanHoursInSeconds` issue, because:
- `GoogleSheetHelper.PullEverythingFromGoogleSheet`'s update leg (`P/Infrastructure/Helpers/GoogleSheetHelper.cs:505-521`, tag
  `INVERTED-SUMFLEX-SIGN`) deliberately writes `SumFlexEnd = SumFlexStart + PlanHours - NettoHours - PaiedOutFlex`. A walk from
  the earliest pulled date re-carries every pulled row with the correct sign, i.e. it silently fixes that bug and restates the
  balances of every Google-Sheet tenant on the next pull — which §6 says stays unchanged here.
- On one-minute rows the pull updates `PlanHours` but not `PlanHoursInSeconds`; `CarryChain` prefers a non-zero
  `PlanHoursInSeconds`, so a walk would ignore the pulled plan change in the balance.
- There is no test harness for the pull (it needs live Google credentials); the fix belongs with the restatement work that can
  be verified against tenant data.
`GoogleSheetHelper.cs` is not touched by PR3.

**PR3 wrap-up:** dual review gate before each commit; push; `gh pr create --base stable`; watch CI to a verdict; classify
red shards against the latest `stable` run.


---

## Part 3 — Service (`eform-service-timeplanning-plugin`, PR 4)

# Service PR — flex chain integrity (`eform-service-timeplanning-plugin`)

Spec: `eform-angular-timeplanning-plugin-flex994/docs/superpowers/specs/2026-09-24-flex-chain-integrity-design.md`
§3 (hard constraint), §5.1 (B1), §6 (R4 walks), §7 item 4.

- Repo: `/home/rene/Documents/workspace/microting/eform-service-timeplanning-plugin`
- Target branch: `stable`. Working branch: `fix/flex-chain-service` off `origin/stable`
  (verified head `0b35f19`, 2026-09-24).
- Test project: `ServiceTimePlanningPlugin.Integration.Test` (NUnit 4.6, Testcontainers MariaDB 10.8,
  `TestBaseSetup` drops and re-migrates the DB per test, contexts come from the production
  `DbContextHelper`, so the reconciled-day interceptor is attached). CI (`dotnet-core-pr.yml` L51) runs the
  **whole** project, no shard allowlist, so new test classes need no workflow change.
- Tests need Docker. Run them locally if Docker is available; otherwise push and watch CI, which is where they
  run reliably.
- **Precondition:** `Microting.TimePlanningBase` **10.0.65** is on nuget.org and provides:
  - `FlexChain.ComputeNettoMinutesFlagOff`
  - `FlexChainRecompute.RunForwardAsync(db, assignedSite, sdkSitId, fromDateInclusive)`
  - `DayLock.LockedThroughAsync`, `DayLock.IsLocked` and `DayLock.OpenRows`

  On 2026-09-24 the base repo's latest tag is `v10.0.64`, so T-S1 cannot restore until the base PR ships.
- **`RunForwardAsync` as used below:**
  - It walks every live row of the worker with `Date >= fromDateInclusive`, ordered by `Date`, then `Id`, and
    applies the balance-only `CarryChain` to each one, **the start day included**.
  - It seeds from the last live row before the start date. When the reconciled boundary is on or after the
    start date, it starts the day after the boundary and seeds from the boundary row.
  - When the worker has no earlier row, the first row keeps its own stored `SumFlexStart`.
  - It never recomputes `NettoHours` and writes only the rows that changed.

## What the existing tests cover

| File changed | Existing tests that exercise it |
|---|---|
| `ServiceTimePlanningPlugin/ServiceTimePlanningPlugin.csproj` (T-S1) | All of them: `CanaryInAColeMine`, `DayLockInterceptorTests` (8), `FlexChainCatchUpJobTests` (9), `PlanRegistrationHelperPlanTextTests` (≈11 cases), `PlanTimerSheetColumnsTests`, `SearchListJobTests` (1) |
| `Handlers/eFormCompletedHandler.cs` (T-S2, T-S3) | **None.** Nothing builds `EFormCompletedHandler` today |
| `Infrastructure/Helpers/PlanRegistrationDeviceSubmission.cs` (new) | None (new) |
| `Infrastructure/Helpers/DayLockHelper.cs` (T-S1, forwards to base `DayLock`) | `DayLockInterceptorTests` (8), `SearchListJobTests` (1, `WhereOpen`), `FlexChainCatchUpJobTests.ReconciledBoundary_WalkStartsAfterIt_AndCursorAdvances`, and indirectly every test that saves a row, through the interceptor |
| `SearchListJobCarryForwardTests.cs` (new, T-S4) | None (new) |
| `Scheduler/Jobs/SearchListJob.cs` (T-S4, nightly `RecalculateRecentRegistrations` only; `Execute()` untouched) | `SearchListJobTests.NightlyRecalculation_SkipsReconciledDays_AndStillUpdatesTheOpenOnes`. Traced below: still passes |
| `Scheduler/Jobs/FlexChainCatchUpJob.cs` | **Not changed** (spec §3 constraint 6, §10). `FlexChainCatchUpJobTests` still covers it |
| `Infrastructure/Helpers/PlanRegistrationHelper.cs` | **Not changed** |

## Why the handler has to be split out before it can be tested

`EFormCompletedHandler.Handle` needs an `eFormCore.Core`. It reads `sdkDbContext.Sites`, `Cases` and `Fields` by
hard-coded `OriginalId`s (`373285`…`373295`), then `_sdkCore.Advanced_FieldValueReadList`. `TestBaseSetup` has
no SDK database; its own doc comment says it does not need one. Setting up an SDK `Core`, an eForm template and
field values in a test would take more code than the fix itself. So T-S2 first moves the block that computes
the plan, `eFormCompletedHandler.cs` L209-247 plus L269-300, into a public static helper that takes
`(TimePlanningPnDbContext, AssignedSite?, PlanRegistration)`. The handler then calls it. Nothing else changes:
the same queries, the same order of saves and the same walk. The public, static shape matches
`PlanRegistrationHelper`. The repo has no `InternalsVisibleTo`, so `internal` would need a new assembly
attribute.

---

### Task 12 (T-S1): Bump `Microting.TimePlanningBase` to 10.0.65 and forward the service's `DayLockHelper` to the base `DayLock`

**Files**
- Modify `ServiceTimePlanningPlugin/ServiceTimePlanningPlugin.csproj` L17. This is the only reference. The
  test csproj gets the package through its `ProjectReference` at L10.
- Modify `ServiceTimePlanningPlugin/Infrastructure/Helpers/DayLockHelper.cs`: the header L1-11,
  `LockedThroughAsync` L41-47, `IsLocked(DateTime?, DateTime)` L77-78 and `WhereOpen` L98-108.

**Interfaces.** The public signatures stay exactly as they are, because the callers depend on them:
`eFormCompletedHandler.cs` L147-148, `FlexChainCatchUpJob.cs` L167-168 (the file itself is untouched),
`SearchListJob.cs` L144/177/350/356 and `ReconciledDayLockInterceptor.cs` L179/191.
- `Task<DateTime?> LockedThroughAsync(TimePlanningPnDbContext, int)` forwards to `DayLock.LockedThroughAsync`.
- `bool IsLocked(DateTime?, DateTime)` forwards to `DayLock.IsLocked`.
- `IQueryable<PlanRegistration> WhereOpen(this IQueryable<PlanRegistration>, DateTime?)` forwards to
  `DayLock.OpenRows(query, lockedThrough)`. The base method is a static, non-extension method.
- `LockedThroughForSitesAsync`, `IsLocked(IReadOnlyDictionary<int, DateTime?>, int, DateTime)` and the private
  `BoundaryRows` stay local, because the base exposes no multi-site variant.

- [ ] **Step 1: Create the branch**
  ```bash
  cd /home/rene/Documents/workspace/microting/eform-service-timeplanning-plugin
  git fetch -q && git checkout -b fix/flex-chain-service origin/stable
  ```
- [ ] **Step 2: Bump the package.** In `ServiceTimePlanningPlugin/ServiceTimePlanningPlugin.csproj` L17,
  change `Version="10.0.64"` to `Version="10.0.65"`:
  ```xml
      <PackageReference Include="Microting.TimePlanningBase" Version="10.0.65" />
  ```
- [ ] **Step 3: Forward the three lock methods.** In `DayLockHelper.cs`:
  - Replace the header comment L1-11 with:
    ```csharp
    // The single-worker lock rule (LockedThroughAsync, IsLocked, WhereOpen) lives
    // in the base package's DayLock, shared with eform-angular-timeplanning-plugin
    // and with FlexChainRecompute's walk, so every writer agrees on which days are
    // locked. What stays here is the multi-site boundary lookup the jobs and the
    // interceptor need; its BoundaryRows definition must keep matching DayLock's
    // (Reconciled and not soft-deleted).
    ```
  - Add `using Microting.TimePlanningBase.Infrastructure.Helpers;` to the usings (L15-23). Confirm that
    `DayLock` lives in this namespace in 10.0.65.
  - Change `LockedThroughAsync` (L41-47):
    ```csharp
    // before
    public static async Task<DateTime?> LockedThroughAsync(TimePlanningPnDbContext db, int sdkSitId)
    {
        return await BoundaryRows(db)
            .Where(x => x.SdkSitId == sdkSitId)
            .MaxAsync(x => (DateTime?)x.Date)
            .ConfigureAwait(false);
    }
    // after
    public static Task<DateTime?> LockedThroughAsync(TimePlanningPnDbContext db, int sdkSitId)
        => DayLock.LockedThroughAsync(db, sdkSitId);
    ```
  - Change `IsLocked(DateTime?, DateTime)` (L77-78):
    ```csharp
    // before
    public static bool IsLocked(DateTime? lockedThrough, DateTime date)
        => lockedThrough.HasValue && date.Date <= lockedThrough.Value.Date;
    // after
    public static bool IsLocked(DateTime? lockedThrough, DateTime date)
        => DayLock.IsLocked(lockedThrough, date);
    ```
  - Change `WhereOpen` (L98-108):
    ```csharp
    // before
    public static IQueryable<PlanRegistration> WhereOpen(
        this IQueryable<PlanRegistration> query, DateTime? lockedThrough)
    {
        if (lockedThrough is not { } boundary)
        {
            return query;
        }

        var firstOpenDay = boundary.Date.AddDays(1);
        return query.Where(x => x.Date >= firstOpenDay);
    }
    // after
    public static IQueryable<PlanRegistration> WhereOpen(
        this IQueryable<PlanRegistration> query, DateTime? lockedThrough)
        => DayLock.OpenRows(query, lockedThrough);
    ```
  Keep the XML doc comments on each method. Behaviour is unchanged as long as the base logic is the plugin's,
  moved: the plugin and service copies are documented as byte-identical apart from their headers.
- [ ] **Step 4: Build**
  ```bash
  dotnet build ServiceTimePlanningPlugin.sln
  ```
- [ ] **Step 5: Push and let CI run the whole existing suite.** Expected: every existing test passes unchanged.
  The ones that cover the forwarding are all 8 `DayLockInterceptorTests`,
  `SearchListJobTests.NightlyRecalculation_SkipsReconciledDays_…` (`WhereOpen`) and
  `FlexChainCatchUpJobTests.ReconciledBoundary_WalkStartsAfterIt_AndCursorAdvances` (`LockedThroughAsync`,
  `IsLocked`). `FlexChainCatchUpJobTests` also pins `ApplyNettoFlexChain*`.
- [ ] **Step 6: Commit**
  ```bash
  git add ServiceTimePlanningPlugin/ServiceTimePlanningPlugin.csproj \
          ServiceTimePlanningPlugin/Infrastructure/Helpers/DayLockHelper.cs
  git commit -m "chore(deps): bump Microting.TimePlanningBase to 10.0.65 and share its day lock

  The single-worker lock rule now comes from the base's DayLock, the same
  one the plugin and FlexChainRecompute use. Signatures are unchanged.

  Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
  ```

---

### Task 13 (T-S2): B1, open shift counts 0 on the five-minute device path

Two commits. **2a** extracts the code with identical behaviour and adds a characterization test. **2b** adds
the failing open-shift tests and switches the formula.

**Files**
- Create `ServiceTimePlanningPlugin/Infrastructure/Helpers/PlanRegistrationDeviceSubmission.cs`
- Modify `ServiceTimePlanningPlugin/Handlers/eFormCompletedHandler.cs` L209-300 (from `var dbAssignedSite = …` through the end of the cascade `if`)
- Create `ServiceTimePlanningPlugin.Integration.Test/PlanRegistrationDeviceSubmissionTests.cs`

**Interfaces**
```csharp
namespace ServiceTimePlanningPlugin.Infrastructure.Helpers;
public static class PlanRegistrationDeviceSubmission
{
    // Computes the submitted day's hours and flex, saves the row, then carries the balance forward.
    // `timePlanning` must already be persisted (Create'd or loaded) and tracked by `dbContext`.
    public static Task ApplyAsync(TimePlanningPnDbContext dbContext, AssignedSite? assignedSite, PlanRegistration timePlanning);
}
```

#### T-S2a: Extract with identical behaviour

- [ ] **Step 1: Write the characterization test.** Create
  `ServiceTimePlanningPlugin.Integration.Test/PlanRegistrationDeviceSubmissionTests.cs`:

```csharp
/*
The MIT License (MIT)

Copyright (c) 2007 - 2026 Microting A/S

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microting.eForm.Infrastructure.Constants;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using NUnit.Framework;
using ServiceTimePlanningPlugin.Infrastructure.Helpers;

namespace ServiceTimePlanningPlugin.Integration.Test;

/// <summary>
/// Covers what the device eForm handler does once it has the submitted row:
/// the day's hours, its flex, and carrying the balance to later rows.
/// EFormCompletedHandler itself needs the eForm SDK (cases, fields, field
/// values), so these tests call PlanRegistrationDeviceSubmission.ApplyAsync,
/// which is exactly the part of the handler that touches the plan.
///
/// Each test submits through its OWN context, loading the row by date as the
/// handler does, and reads results back untracked from the database.
/// </summary>
[TestFixture]
public class PlanRegistrationDeviceSubmissionTests : TestBaseSetup
{
    private static int _nextSiteId = 300000;

    private static int NextSiteId() => _nextSiteId++;

    private async Task SeedAssignedSite(int siteId, bool useOneMinute)
    {
        await new AssignedSite
        {
            SiteId = siteId,
            UseOneMinuteIntervals = useOneMinute,
            UseOneMinuteIntervalsFrom = useOneMinute ? DateTime.Today.AddYears(-1) : null,
            WorkflowState = Constants.WorkflowStates.Created,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        }.Create(TimePlanningPnDbContext);
    }

    private async Task SeedRow(int siteId, DateTime date, Action<PlanRegistration> configure)
    {
        var pr = new PlanRegistration
        {
            SdkSitId = siteId,
            Date = date,
            PlanText = "",
            CommentOffice = "",
            CommentOfficeAll = "",
            WorkflowState = Constants.WorkflowStates.Created,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        configure(pr);
        await pr.Create(TimePlanningPnDbContext);
    }

    private async Task Submit(int siteId, DateTime date)
    {
        await using var db = DbContextHelper.GetDbContext();
        var assignedSite = await db.AssignedSites.FirstOrDefaultAsync(x => x.SiteId == siteId);
        var row = await db.PlanRegistrations.FirstAsync(x => x.SdkSitId == siteId && x.Date == date);
        await PlanRegistrationDeviceSubmission.ApplyAsync(db, assignedSite, row);
    }

    private async Task<PlanRegistration> Reload(int siteId, DateTime date)
        => await TimePlanningPnDbContext.PlanRegistrations
            .AsNoTracking()
            .SingleAsync(x => x.SdkSitId == siteId && x.Date == date);

    /// <summary>A closed five-minute predecessor with the given closing balance.</summary>
    private Task SeedFiveMinuteAnchor(int siteId, DateTime date, double sumFlexEnd)
        => SeedRow(siteId, date, pr =>
        {
            pr.PlanHours = 8;
            pr.NettoHours = 8;
            pr.Flex = 0;
            pr.SumFlexStart = sumFlexEnd;
            pr.SumFlexEnd = sumFlexEnd;
        });

    // ------------------------------------------------------------------
    // B1: the five-minute netto of the submitted day
    // ------------------------------------------------------------------

    /// <summary>
    /// Two closed shifts: 08:00-16:00 with a 30-minute break (7.5 h) and
    /// 17:00-20:00 with no break (3 h). The old inline formula and
    /// FlexChain.ComputeNettoMinutesFlagOff agree on this row. It pins that
    /// a normal day does not move when the formula changes.
    /// </summary>
    [Test]
    public async Task ClosedShifts_FiveMinute_NettoIsTheIdSpanMinusBreaks()
    {
        var siteId = NextSiteId();
        var today = DateTime.Today;
        await SeedAssignedSite(siteId, useOneMinute: false);
        await SeedFiveMinuteAnchor(siteId, today.AddDays(-3), sumFlexEnd: 2.0);
        await SeedRow(siteId, today.AddDays(-2), pr =>
        {
            pr.PlanHours = 8;
            pr.Start1Id = 97;  // 08:00
            pr.Stop1Id = 193;  // 16:00
            pr.Pause1Id = 7;   // 30 min
            pr.Start2Id = 205; // 17:00
            pr.Stop2Id = 241;  // 20:00
            pr.Pause2Id = 1;   // 0 min
        });

        await Submit(siteId, today.AddDays(-2));

        var row = await Reload(siteId, today.AddDays(-2));
        Assert.Multiple(() =>
        {
            Assert.That(row.NettoHours, Is.EqualTo(10.5).Within(1e-9));
            Assert.That(row.Flex, Is.EqualTo(2.5).Within(1e-9));
            Assert.That(row.SumFlexStart, Is.EqualTo(2.0).Within(1e-9));
            Assert.That(row.SumFlexEnd, Is.EqualTo(4.5).Within(1e-9));
        });
    }
}
```

- [ ] **Step 2: Run it. Expect a compile failure** because `PlanRegistrationDeviceSubmission` does not exist yet.
  ```bash
  # runs in CI (local test runs are hook-blocked): class PlanRegistrationDeviceSubmissionTests
  ```
- [ ] **Step 3: Create the helper by moving the current code across unchanged.** Create
  `ServiceTimePlanningPlugin/Infrastructure/Helpers/PlanRegistrationDeviceSubmission.cs`. Its body is
  `eFormCompletedHandler.cs` L211-247 and L269-300 moved as-is. The only substitution is
  `site.MicrotingUid` → `timePlanning.SdkSitId`. These are equal, because the handler loads the row with
  `SdkSitId == site.MicrotingUid` (L156) or creates it with `SdkSitId = (int)site.MicrotingUid!` (L165).

```csharp
#nullable enable
namespace ServiceTimePlanningPlugin.Infrastructure.Helpers;

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microting.eForm.Infrastructure.Constants;
using Microting.TimePlanningBase.Infrastructure.Data;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using Microting.TimePlanningBase.Infrastructure.Helpers;

/// <summary>
/// The part of EFormCompletedHandler that touches the plan: once the device
/// submission has been written onto the day's row, compute that day's hours
/// and flex, save it, and carry the balance to the worker's later rows.
///
/// Split out of the handler so it can be tested without the eForm SDK: the
/// handler needs an SDK Core (cases, fields, field values) only to READ the
/// submission, never to compute anything from it.
/// </summary>
public static class PlanRegistrationDeviceSubmission
{
    /// <param name="timePlanning">
    /// The submitted day's row, already persisted and tracked by
    /// <paramref name="dbContext"/>.
    /// </param>
    /// <param name="assignedSite">The worker's AssignedSite, or null when none exists.</param>
    public static async Task ApplyAsync(
        TimePlanningPnDbContext dbContext, AssignedSite? assignedSite, PlanRegistration timePlanning)
    {
        // ONE query, in-memory lookups: resolves the mode that was in force
        // when each row was REGISTERED. Built ONCE here, before the cascade
        // loop below, and reused for every row in it -- every mode fork in
        // this helper reads the resulting per-row mode, never the site's
        // CURRENT flag. See OneMinuteModeTimeline.
        var oneMinuteTimeline = await OneMinuteModeTimeline.BuildAsync(dbContext, assignedSite);
        var rowIsOneMinute = oneMinuteTimeline.WasOneMinuteForRow(timePlanning);

        var preTimePlanning =
            await dbContext.PlanRegistrations.AsNoTracking()
                .Where(x => x.Date < timePlanning.Date && x.SdkSitId == timePlanning.SdkSitId)
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .OrderByDescending(x => x.Date).FirstOrDefaultAsync();

        if (rowIsOneMinute)
        {
            FlexChain.ApplyNettoFlexChainSecondPrecision(
                timePlanning, preTimePlanning, oneMinuteTimeline.WasOneMinuteFor(preTimePlanning));
        }
        else
        {
            var minutesMultiplier = 5;

            double nettoMinutes = timePlanning.Stop1Id - timePlanning.Start1Id;
            nettoMinutes -= timePlanning.Pause1Id > 0 ? timePlanning.Pause1Id - 1 : 0;
            if (timePlanning.Stop2Id != 0)
            {
                nettoMinutes = nettoMinutes + timePlanning.Stop2Id - timePlanning.Start2Id;
                nettoMinutes -= timePlanning.Pause2Id > 0 ? timePlanning.Pause2Id - 1 : 0;
            }

            nettoMinutes *= minutesMultiplier;

            timePlanning.NettoHours = nettoMinutes / 60;

            FlexChain.ApplyNettoFlexChainDecimal(timePlanning, preTimePlanning);
        }

        await timePlanning.Update(dbContext);
        if (dbContext.PlanRegistrations.Any(x => x.Date >= timePlanning.Date && x.SdkSitId == timePlanning.SdkSitId && x.Id != timePlanning.Id && x.WorkflowState != Constants.WorkflowStates.Removed))
        {
            // Ascending, unbounded walk carrying its running seed IN
            // MEMORY (the just-updated preceding row, not a re-query).
            PlanRegistration previousRegistration = timePlanning;
            var list = await dbContext.PlanRegistrations
                .Where(x => x.Date > timePlanning.Date && x.SdkSitId == timePlanning.SdkSitId && x.Id != timePlanning.Id)
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .OrderBy(x => x.Date).ToListAsync();
            foreach (PlanRegistration planRegistration in list)
            {
                Console.WriteLine($"Updating planRegistration {planRegistration.Id} for date {planRegistration.Date}");
                // Fork on the mode AT REGISTRATION, not the site's current
                // flag -- see OneMinuteModeTimeline for why.
                if (oneMinuteTimeline.WasOneMinuteForRow(planRegistration))
                {
                    FlexChain.ApplyNettoFlexChainSecondPrecision(
                        planRegistration, previousRegistration,
                        oneMinuteTimeline.WasOneMinuteFor(previousRegistration));
                }
                else
                {
                    FlexChain.ApplyNettoFlexChainDecimal(planRegistration, previousRegistration);
                }
                await planRegistration.Update(dbContext);
                previousRegistration = planRegistration;
            }
        }
    }
}
```

- [ ] **Step 4: Make the handler call it.** In `eFormCompletedHandler.cs`, replace L209-300 (from
  `var dbAssignedSite = await dbContext.AssignedSites` through the closing `}` of the cascade `if` on L300)
  with the block below. The two message and registration-device reads (old L249-267) move up, above the call.
  They only read, and neither depends on the computed values: `timePlanning.MessageId` is never written in
  between. So "reads first, then save and walk" is kept. Both values are unused today, as they were before.
  They are kept so this commit stays a pure move.

```csharp
                var dbAssignedSite = await dbContext.AssignedSites
                    .FirstOrDefaultAsync(x => x.SiteId == site.MicrotingUid);

                Message theMessage =
                    await dbContext.Messages.FirstOrDefaultAsync(x => x.Id == timePlanning.MessageId);
                string messageText;
                switch (language.LanguageCode)
                {
                    case "da":
                        messageText = theMessage != null ? theMessage.DaName : "";
                        break;
                    case "de":
                        messageText = theMessage != null ? theMessage.DeName : "";
                        break;
                    default:
                        messageText = theMessage != null ? theMessage.EnName : "";
                        break;
                }

                var registrationDevices = await dbContext.RegistrationDevices
                    .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                    .ToListAsync().ConfigureAwait(false);

                // Hours, flex, save and the forward walk live in one testable
                // helper; this handler only reads the submission off the SDK.
                await PlanRegistrationDeviceSubmission.ApplyAsync(dbContext, dbAssignedSite, timePlanning);
```
  `Infrastructure.Helpers` is already imported at L40. Once nothing in the handler uses
  `OneMinuteModeTimeline` or `FlexChain`, `using Microting.TimePlanningBase.Infrastructure.Helpers;` (L45) is
  unused. Remove it only if the build or IDE flags it.

- [ ] **Step 5: Build, then run the new test (expect PASS)**
  ```bash
  dotnet build ServiceTimePlanningPlugin.sln
  # runs in CI (local test runs are hook-blocked): class PlanRegistrationDeviceSubmissionTests
  ```
- [ ] **Step 6: Commit**
  ```bash
  git add ServiceTimePlanningPlugin/Infrastructure/Helpers/PlanRegistrationDeviceSubmission.cs \
          ServiceTimePlanningPlugin/Handlers/eFormCompletedHandler.cs \
          ServiceTimePlanningPlugin.Integration.Test/PlanRegistrationDeviceSubmissionTests.cs
  git commit -m "refactor(handler): move the device submission's plan math into a testable helper

  EFormCompletedHandler needs the eForm SDK only to read the submission.
  The hours, flex, save and forward walk move unchanged into
  PlanRegistrationDeviceSubmission.ApplyAsync, with the first test of it.

  Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
  ```

#### T-S2b: Open shift → 0 hours

- [ ] **Step 1: Add the failing tests** to `PlanRegistrationDeviceSubmissionTests` (after the characterization test):

```csharp
    /// <summary>
    /// R1: a shift the worker started but never stopped counts 0 hours, and
    /// its break is ignored. The old formula took Stop1Id - Start1Id with
    /// Stop1Id = 0, i.e. (0 - 97 - 6) * 5 min = -8.58 h.
    /// </summary>
    [Test]
    public async Task StartWithoutStop_FiveMinute_CountsZeroHours_AndFlexIsMinusPlanHours()
    {
        var siteId = NextSiteId();
        var today = DateTime.Today;
        await SeedAssignedSite(siteId, useOneMinute: false);
        await SeedFiveMinuteAnchor(siteId, today.AddDays(-3), sumFlexEnd: 2.0);
        await SeedRow(siteId, today.AddDays(-2), pr =>
        {
            pr.PlanHours = 7.5;
            pr.Start1Id = 97; // 08:00
            pr.Stop1Id = 0;   // never stopped
            pr.Pause1Id = 7;  // 30 min, ignored on an open shift
        });

        await Submit(siteId, today.AddDays(-2));

        var row = await Reload(siteId, today.AddDays(-2));
        Assert.Multiple(() =>
        {
            Assert.That(row.NettoHours, Is.EqualTo(0).Within(1e-9));
            Assert.That(row.Flex, Is.EqualTo(-7.5).Within(1e-9));
            Assert.That(row.SumFlexStart, Is.EqualTo(2.0).Within(1e-9));
            Assert.That(row.SumFlexEnd, Is.EqualTo(-5.5).Within(1e-9));
        });
    }

    /// <summary>R1: a stop before its start counts 0 hours, never negative.</summary>
    [Test]
    public async Task StopBeforeStart_FiveMinute_CountsZeroHours()
    {
        var siteId = NextSiteId();
        var today = DateTime.Today;
        await SeedAssignedSite(siteId, useOneMinute: false);
        await SeedFiveMinuteAnchor(siteId, today.AddDays(-3), sumFlexEnd: 2.0);
        await SeedRow(siteId, today.AddDays(-2), pr =>
        {
            pr.PlanHours = 7.5;
            pr.Start1Id = 193; // 16:00
            pr.Stop1Id = 97;   // 08:00
            pr.Pause1Id = 1;
        });

        await Submit(siteId, today.AddDays(-2));

        var row = await Reload(siteId, today.AddDays(-2));
        Assert.Multiple(() =>
        {
            Assert.That(row.NettoHours, Is.EqualTo(0).Within(1e-9));
            Assert.That(row.Flex, Is.EqualTo(-7.5).Within(1e-9));
            Assert.That(row.SumFlexEnd, Is.EqualTo(-5.5).Within(1e-9));
        });
    }
```
- [ ] **Step 2: Run. Expect the two new tests to FAIL** (NettoHours -8.583 and -8.0) and the characterization test to pass.
  ```bash
  # runs in CI (local test runs are hook-blocked): class PlanRegistrationDeviceSubmissionTests
  ```
- [ ] **Step 3: Switch to the base function.** In `PlanRegistrationDeviceSubmission.cs`, replace the whole
  `else` branch:
```csharp
        // before
        else
        {
            var minutesMultiplier = 5;

            double nettoMinutes = timePlanning.Stop1Id - timePlanning.Start1Id;
            nettoMinutes -= timePlanning.Pause1Id > 0 ? timePlanning.Pause1Id - 1 : 0;
            if (timePlanning.Stop2Id != 0)
            {
                nettoMinutes = nettoMinutes + timePlanning.Stop2Id - timePlanning.Start2Id;
                nettoMinutes -= timePlanning.Pause2Id > 0 ? timePlanning.Pause2Id - 1 : 0;
            }

            nettoMinutes *= minutesMultiplier;

            timePlanning.NettoHours = nettoMinutes / 60;

            FlexChain.ApplyNettoFlexChainDecimal(timePlanning, preTimePlanning);
        }
        // after
        else
        {
            // The one five-minute hours computation (shifts 1-5). A shift
            // counts only once it has a stop at or after its start, so an
            // open shift is 0 hours, never negative (R1).
            timePlanning.NettoHours = FlexChain.ComputeNettoMinutesFlagOff(timePlanning) / 60.0;

            FlexChain.ApplyNettoFlexChainDecimal(timePlanning, preTimePlanning);
        }
```
- [ ] **Step 4: Build and run. Expect all 3 to PASS**
  ```bash
  dotnet build ServiceTimePlanningPlugin.sln
  # runs in CI (local test runs are hook-blocked): class PlanRegistrationDeviceSubmissionTests
  ```
- [ ] **Step 5: Commit**
  ```bash
  git add ServiceTimePlanningPlugin/Infrastructure/Helpers/PlanRegistrationDeviceSubmission.cs \
          ServiceTimePlanningPlugin.Integration.Test/PlanRegistrationDeviceSubmissionTests.cs
  git commit -m "fix(handler): an open shift counts 0 hours on five-minute sites

  A device submission with a start and no stop, or a stop before its start,
  produced negative hours. The day's hours now come from the base's
  FlexChain.ComputeNettoMinutesFlagOff, the same computation the plugin uses.

  Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
  ```

#### Other results the B1 change can move (not only open shifts)

These are rows whose hours on the service five-minute path **change** with the switch from the old inline
formula (shifts 1-2, pause `(PauseNId−1)×5`) to `ComputeNettoMinutesFlagOff` (shifts 1-5,
`ComputeShiftPauseSeconds(…, false)`). **No existing test covers this path**, so none can break.

| # | Row shape (five-minute row, device submission) | Old | New | Inside R1? |
|---|---|---|---|---|
| 1 | Shift 1 started, `Stop1Id == 0` | negative | 0, pause ignored | yes |
| 2 | Shift 1 `Stop1Id < Start1Id` (both ≠ 0) | negative | 0 | yes |
| 3 | Shift 2 `Stop2Id != 0 && Stop2Id < Start2Id` | shift 2 adds a negative span | shift 2 is 0 | yes |
| 4 | **No shift 1 at all** (`Start1Id == Stop1Id == 0`) but `Pause1Id > 1` | subtracts `(Pause1Id−1)×5` min, so negative | 0 | Arguably no. R1 speaks of a started shift. The result is the same family (never negative) |
| 5 | **Shifts 3-5 carry ids** (written by the office editor, kiosk or app on the same date; the device eForm only has shifts 1-2) | shifts 3-5 **dropped**, and the row's NettoHours is overwritten with shifts 1-2 only on every submission | shifts 3-5 counted | **No.** Hours go **up** to what the plugin already computes for the same row |
| 6 | **Pause stamps present** on a five-minute row (`PauseNStartedAt/StoppedAt`, including sub-slots `Pause10..19`, `Pause100..102`, `Pause20..29`, `Pause200..202`) | pause = `(PauseNId−1)×5` only | pause = sum of each complete stamp pair's floor-to-5-min tick delta; `PauseNId` is ignored once any complete pair exists | **No.** It differs whenever the stamps disagree with the id or when there are several pauses per shift |
| 7 | **`PauseNOverrideMinutes` set** (office pause override) | ignored | the override wins | **No** |
| 8 | Shift 2 has `Stop2Id == 0` with `Pause2Id > 1` | pause ignored (inside the `Stop2Id != 0` guard) | ignored | same |
| 9 | Closed shift whose pause is longer than its span | negative | still negative (no clamp in either) | not addressed by R1 |
| 10 | Plain closed shifts 1-2, ids only, no stamps or override | — | **identical** (the characterization test pins it) | — |

Rows 5-7 bring the service in line with the plugin's five-minute paths (`ComputeFlagOffNettoMinutes`), so the
service stops overwriting correct plugin-computed hours with a shifts-1-2-only figure. They are still
behaviour changes beyond the open-shift rule. **The owner has ACCEPTED rows 4-7 (2026-09-24)** because they
align the service with the plugin. Record them in the PR body. The one-minute branch
(`ApplyNettoFlexChainSecondPrecision`) is untouched.

---

### Task 14 (T-S3): Replace the handler's hand-rolled walk with `RunForwardAsync`

**Files**
- Modify `ServiceTimePlanningPlugin/Infrastructure/Helpers/PlanRegistrationDeviceSubmission.cs`: the tail
  that was moved from handler L269-300
- Modify `ServiceTimePlanningPlugin.Integration.Test/PlanRegistrationDeviceSubmissionTests.cs`: add 3 tests

**Interfaces:** consumes `FlexChainRecompute.RunForwardAsync(TimePlanningPnDbContext, AssignedSite?, int sdkSitId, DateTime fromDateInclusive) → Task<int>`.

- [ ] **Step 1: Add the tests**

```csharp
    // ------------------------------------------------------------------
    // R4: the walk after the submitted day
    // ------------------------------------------------------------------

    /// <summary>
    /// The balance reaches the worker's LAST row, a pre-created future day
    /// included. (The old unbounded walk already did this; this guards it
    /// through the switch to RunForwardAsync.)
    /// </summary>
    [Test]
    public async Task LaterRows_CarryTheBalance_ThroughAFuturePreCreatedRow()
    {
        var siteId = NextSiteId();
        var today = DateTime.Today;
        await SeedAssignedSite(siteId, useOneMinute: false);
        await SeedFiveMinuteAnchor(siteId, today.AddDays(-3), sumFlexEnd: 2.0);
        await SeedRow(siteId, today.AddDays(-2), pr =>
        {
            pr.PlanHours = 7;
            pr.Start1Id = 97;  // 08:00
            pr.Stop1Id = 193;  // 16:00
            pr.Pause1Id = 7;   // 30 min -> 7.5 h, flex +0.5
        });
        // Stale chain on both later rows: never carried forward.
        await SeedRow(siteId, today.AddDays(-1), pr =>
        {
            pr.PlanHours = 7;
            pr.NettoHours = 8;
        });
        await SeedRow(siteId, today.AddDays(10), pr =>
        {
            pr.PlanHours = 7;
        });

        await Submit(siteId, today.AddDays(-2));

        var submitted = await Reload(siteId, today.AddDays(-2));
        var dMinus1 = await Reload(siteId, today.AddDays(-1));
        var future = await Reload(siteId, today.AddDays(10));
        Assert.Multiple(() =>
        {
            Assert.That(submitted.SumFlexEnd, Is.EqualTo(2.5).Within(1e-9));
            Assert.That(dMinus1.SumFlexStart, Is.EqualTo(submitted.SumFlexEnd).Within(1e-9));
            Assert.That(dMinus1.Flex, Is.EqualTo(1.0).Within(1e-9));
            Assert.That(dMinus1.SumFlexEnd, Is.EqualTo(3.5).Within(1e-9));
            Assert.That(dMinus1.NettoHours, Is.EqualTo(8).Within(1e-9), "a later row's hours are never recomputed");
            Assert.That(future.SumFlexStart, Is.EqualTo(dMinus1.SumFlexEnd).Within(1e-9));
            Assert.That(future.SumFlexEnd, Is.EqualTo(-3.5).Within(1e-9));
        });
    }

    /// <summary>
    /// R2, the incident scenario: a later one-minute row whose device stamps
    /// (08:00-17:00, 9 h) disagree with the hours an office user set (8 h,
    /// stored). The old walk ran ApplyNettoFlexChainSecondPrecision on it and
    /// re-derived 9 h from the stamps. The walk must keep the stored hours
    /// and only move the balance.
    /// </summary>
    [Test]
    public async Task LaterOneMinuteRow_KeepsItsStoredHours_WhenItsStampsDisagree()
    {
        var siteId = NextSiteId();
        var today = DateTime.Today;
        await SeedAssignedSite(siteId, useOneMinute: true);
        await SeedRow(siteId, today.AddDays(-3), pr =>
        {
            pr.RegisteredUnderOneMinuteIntervals = true;
            pr.PlanHours = 8;
            pr.PlanHoursInSeconds = 28800;
            pr.NettoHours = 8;
            pr.NettoHoursInSeconds = 28800;
            pr.SumFlexStart = 1.0;
            pr.SumFlexStartInSeconds = 3600;
            pr.SumFlexEnd = 1.0;
            pr.SumFlexEndInSeconds = 3600;
        });
        await SeedRow(siteId, today.AddDays(-2), pr =>
        {
            pr.RegisteredUnderOneMinuteIntervals = true;
            pr.PlanHours = 7.5;
            pr.PlanHoursInSeconds = 27000;
            pr.Start1Id = 97;  // ids only: 08:00-16:00, 30 min break -> 27000 s
            pr.Stop1Id = 193;
            pr.Pause1Id = 7;
        });
        var laterDate = today.AddDays(-1);
        await SeedRow(siteId, laterDate, pr =>
        {
            pr.RegisteredUnderOneMinuteIntervals = true;
            pr.PlanHours = 8;
            pr.PlanHoursInSeconds = 28800;
            pr.Start1StartedAt = laterDate.AddHours(8);
            pr.Stop1StoppedAt = laterDate.AddHours(17); // stamps say 9 h
            pr.NettoHours = 8;                          // office says 8 h
            pr.NettoHoursInSeconds = 28800;
        });

        await Submit(siteId, today.AddDays(-2));

        var later = await Reload(siteId, laterDate);
        Assert.Multiple(() =>
        {
            Assert.That(later.NettoHoursInSeconds, Is.EqualTo(28800), "stored hours must survive the walk");
            Assert.That(later.NettoHours, Is.EqualTo(8).Within(1e-9));
            Assert.That(later.SumFlexStartInSeconds, Is.EqualTo(3600));
            Assert.That(later.FlexInSeconds, Is.EqualTo(0));
            Assert.That(later.SumFlexEndInSeconds, Is.EqualTo(3600));
        });
    }

    /// <summary>
    /// A resubmission that leaves the day's closing balance unchanged must not
    /// rewrite later rows: no Version bump and no version row. The old walk
    /// called PnBase.Update on every later row, which always bumps Version.
    /// </summary>
    [Test]
    public async Task LaterRowsWhoseBalanceIsUnchanged_AreNotRewritten()
    {
        var siteId = NextSiteId();
        var today = DateTime.Today;
        await SeedAssignedSite(siteId, useOneMinute: false);
        await SeedFiveMinuteAnchor(siteId, today.AddDays(-3), sumFlexEnd: 1.0);
        await SeedRow(siteId, today.AddDays(-2), pr =>
        {
            pr.PlanHours = 7.5;
            pr.Start1Id = 97;
            pr.Stop1Id = 193;
            pr.Pause1Id = 7; // 7.5 h, flex 0
            pr.NettoHours = 7.5;
            pr.SumFlexStart = 1.0;
            pr.SumFlexEnd = 1.0;
        });
        await SeedRow(siteId, today.AddDays(-1), pr =>
        {
            pr.PlanHours = 8;
            pr.NettoHours = 8;
            pr.SumFlexStart = 1.0;
            pr.SumFlexEnd = 1.0;
        });
        var before = await Reload(siteId, today.AddDays(-1));
        var versionRowsBefore = await TimePlanningPnDbContext.PlanRegistrationVersions
            .CountAsync(x => x.PlanRegistrationId == before.Id);

        await Submit(siteId, today.AddDays(-2));

        var after = await Reload(siteId, today.AddDays(-1));
        var versionRowsAfter = await TimePlanningPnDbContext.PlanRegistrationVersions
            .CountAsync(x => x.PlanRegistrationId == before.Id);
        Assert.Multiple(() =>
        {
            Assert.That(after.Version, Is.EqualTo(before.Version));
            Assert.That(versionRowsAfter, Is.EqualTo(versionRowsBefore));
            Assert.That(after.SumFlexStart, Is.EqualTo(1.0).Within(1e-9));
        });
    }
```
  `PlanRegistrationVersion.PlanRegistrationId` is confirmed in base `origin/master` (entity L67).

- [ ] **Step 2: Run. Expected:** `LaterRows_CarryTheBalance_…` **passes** (the old walk already did this;
  it is a regression guard). `LaterOneMinuteRow_KeepsItsStoredHours_…` **fails**: NettoHoursInSeconds
  32400 ≠ 28800. `LaterRowsWhoseBalanceIsUnchanged_…` **fails**: Version bumped.
  ```bash
  # runs in CI (local test runs are hook-blocked): class PlanRegistrationDeviceSubmissionTests
  ```
- [ ] **Step 3: Replace the walk.** In `PlanRegistrationDeviceSubmission.cs`, replace everything from
  `await timePlanning.Update(dbContext);` to the end of the method:
```csharp
        // before
        await timePlanning.Update(dbContext);
        if (dbContext.PlanRegistrations.Any(x => x.Date >= timePlanning.Date && …))
        {
            … hand-rolled loop calling ApplyNettoFlexChainSecondPrecision / ApplyNettoFlexChainDecimal
              and planRegistration.Update on every later row …
        }
        // after
        await timePlanning.Update(dbContext);

        // R4: carry the balance from the submitted day to the worker's last
        // row (future pre-created days included). The walk starts AT the
        // submitted day: it re-chains that day from its stored hours, which
        // were just computed above, so the day is normally left unchanged and
        // not re-written. It never recomputes a day's hours (R2); the old
        // loop here re-derived later one-minute rows' hours from their device
        // stamps. It skips reconciled days (R5) and writes only rows whose
        // balance actually changed.
        var changed = await FlexChainRecompute.RunForwardAsync(
            dbContext, assignedSite, timePlanning.SdkSitId, timePlanning.Date);
        Console.WriteLine(
            $"info: carried the flex balance forward over {changed} registration(s) for site {timePlanning.SdkSitId} from {timePlanning.Date:yyyy-MM-dd}");
```
  The comment block above `var oneMinuteTimeline` that mentions "the cascade loop below" should now read
  "Built ONCE here and used for the submitted row's mode", because the timeline no longer serves a loop.
- [ ] **Step 4: Build and run. Expect all 6 to PASS**
  ```bash
  dotnet build ServiceTimePlanningPlugin.sln
  # runs in CI (local test runs are hook-blocked): class PlanRegistrationDeviceSubmissionTests
  ```
- [ ] **Step 5: Commit**
  ```bash
  git add ServiceTimePlanningPlugin/Infrastructure/Helpers/PlanRegistrationDeviceSubmission.cs \
          ServiceTimePlanningPlugin.Integration.Test/PlanRegistrationDeviceSubmissionTests.cs
  git commit -m "fix(handler): carry a device submission forward without recomputing later days

  The walk after a device submission re-derived every later one-minute day's
  hours from its device stamps, overwriting office corrections. It now uses
  the base's FlexChainRecompute.RunForwardAsync, which moves only the balance,
  skips reconciled days and writes only rows that changed.

  Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
  ```

None of the three tests needs changing for the inclusive start date. The walk re-chains the submitted day
from the same predecessor that `ApplyNettoFlexChain*` just used, and from the hours it just stored. So the
submitted day comes out identical: 7.5 h and a balance of 2.5 h in the first test, 27000 s and a balance of
3600 s in the second. In the one-minute test the seed is `SumFlexEndSecondsWithFallback(d-3, true)`, which is
3600.

What the walk swap changes, beyond R2:
- A later row whose balance is unchanged is no longer re-saved. Its `Version` does not move and it gets no
  `PlanRegistrationVersions` row. Before, every later row got both on every submission.
- Rows are ordered by `Date` then `Id` (was `Date` only). This only matters when a worker has two non-removed
  rows on one date.
- The walk heals any stored break downstream of the submitted day. The old walk did too, so nothing new.
- Locked rows: the handler rejects a submission on a locked day (L147-153), and the lock boundary is the
  worker's latest reconciled date. So every row after the submitted day is unlocked. The skip inside
  `RunForwardAsync` only matters in the accepted reconcile race.

---

### Task 15 (T-S4): Carry the balance forward after the nightly recalculation

The nightly `RecalculateRecentRegistrations` (`SearchListJob.cs` L317-407, per-site loop L361-397) calls
`UpdatePlanRegistration`, which re-chains a row only when its `PlanHours` changed. The helper's `tainted` flag
is a local that resets on every call (PlanRegistrationHelper L29), so a changed day leaves the later days'
balances behind. `RunForwardAsync(db, site, sdkSitId, fromDateInclusive)` re-chains the start day itself and
every live row after it, so no seed handling is needed here.

> **Google Sheet pull: deferred (owner decision, 2026-09-24).** The walk after the Google Sheet pull in
> `SearchListJob.Execute()` (L33-298) is **not** part of this PR. It moves to **sub-project 5**, together with
> the plugin's `GoogleSheetHelper` pull, the Google Sheet inverted-sign fix and the `PlanHoursInSeconds`
> issue. Both pulls then change behaviour in one reviewed step. This PR leaves `Execute()` untouched.

**Files**
- Modify `ServiceTimePlanningPlugin/Scheduler/Jobs/SearchListJob.cs` at L361, L382-395 and L397
  (`RecalculateRecentRegistrations` only).
- Create `ServiceTimePlanningPlugin.Integration.Test/SearchListJobCarryForwardTests.cs`. It is a new file so
  the existing `SearchListJobTests` stays untouched.

**Interfaces:** consumes `FlexChainRecompute.RunForwardAsync(TimePlanningPnDbContext, AssignedSite?, int sdkSitId, DateTime fromDateInclusive) → Task<int>`.
`Microting.TimePlanningBase.Infrastructure.Helpers` is already imported at L12.

Steps:


- [ ] **Step 1: Write the failing test.** Create
  `ServiceTimePlanningPlugin.Integration.Test/SearchListJobCarryForwardTests.cs` (same MIT header as the other
  test files):
```csharp
using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microting.eForm.Infrastructure.Constants;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using NUnit.Framework;
using ServiceTimePlanningPlugin.Scheduler.Jobs;

namespace ServiceTimePlanningPlugin.Integration.Test;

/// <summary>
/// The nightly recalculation changes one day's plan hours; the days after it
/// must carry the new balance (R4), a pre-created future day included.
/// Called through RecalculateRecentRegistrations(), the entry point without
/// the hourly gate, as SearchListJobTests does.
/// </summary>
[TestFixture]
public class SearchListJobCarryForwardTests : TestBaseSetup
{
    private async Task SeedRow(int siteId, DateTime date, double planHours, double nettoHours,
        double flex, double sumFlexStart, double sumFlexEnd)
    {
        await new PlanRegistration
        {
            SdkSitId = siteId,
            Date = date,
            PlanHours = planHours,
            NettoHours = nettoHours,
            Flex = flex,
            SumFlexStart = sumFlexStart,
            SumFlexEnd = sumFlexEnd,
            PlanText = "",
            CommentOffice = "",
            CommentOfficeAll = "",
            WorkflowState = Constants.WorkflowStates.Created,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        }.Create(TimePlanningPnDbContext);
    }

    private async Task<PlanRegistration> Reload(int siteId, DateTime date)
        => await TimePlanningPnDbContext.PlanRegistrations.AsNoTracking()
            .SingleAsync(x => x.SdkSitId == siteId && x.Date == date);

    [Test]
    public async Task NightlyRecalculation_CarriesAChangedDayForward_ToTheLastRow()
    {
        const int siteId = 961;
        var today = DateTime.Today;
        await new AssignedSite
        {
            SiteId = siteId,
            UseGoogleSheetAsDefault = false,
            UseOnlyPlanHours = true,
            MondayPlanHours = 480, TuesdayPlanHours = 480, WednesdayPlanHours = 480,
            ThursdayPlanHours = 480, FridayPlanHours = 480, SaturdayPlanHours = 480,
            SundayPlanHours = 480,
            WorkflowState = Constants.WorkflowStates.Created,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        }.Create(TimePlanningPnDbContext);

        // A consistent chain built on d-3 having 7 plan hours. Overnight d-3
        // becomes 8 h (weekday default), so its balance drops by 1 h. d-2 and
        // the future day already have 8 h, so the recalculation leaves them alone.
        await SeedRow(siteId, today.AddDays(-3), 7, 8, 1, 0, 1);
        await SeedRow(siteId, today.AddDays(-2), 8, 8, 0, 1, 1);
        await SeedRow(siteId, today.AddDays(3), 8, 0, -8, 1, -7);

        await new SearchListJob(DbContextHelper, null!).RecalculateRecentRegistrations();

        var dMinus3 = await Reload(siteId, today.AddDays(-3));
        var dMinus2 = await Reload(siteId, today.AddDays(-2));
        var future = await Reload(siteId, today.AddDays(3));
        Assert.Multiple(() =>
        {
            Assert.That(dMinus3.PlanHours, Is.EqualTo(8));
            Assert.That(dMinus3.SumFlexEnd, Is.EqualTo(0).Within(1e-9));
            Assert.That(dMinus2.SumFlexStart, Is.EqualTo(dMinus3.SumFlexEnd).Within(1e-9));
            Assert.That(dMinus2.SumFlexEnd, Is.EqualTo(0).Within(1e-9));
            Assert.That(dMinus2.NettoHours, Is.EqualTo(8).Within(1e-9), "the walk never recomputes hours");
            Assert.That(future.SumFlexStart, Is.EqualTo(dMinus2.SumFlexEnd).Within(1e-9));
            Assert.That(future.SumFlexEnd, Is.EqualTo(-8).Within(1e-9));
        });
    }
}
```
- [ ] **Step 2: Run it (CI, or locally with Docker) and expect a FAIL.** `dMinus2.SumFlexStart` stays at 1
  because nothing carries d-3's change forward. Test filter: `FullyQualifiedName~SearchListJobCarryForwardTests`.
- [ ] **Step 3: Implement** in `RecalculateRecentRegistrations`.
  - Before `foreach (var planRegistrationId in planRegistrationIdsForSite)` (L361):
```csharp
                // The earliest day whose balance this run changed; the days
                // after it are carried forward once, after the loop (R4).
                DateTime? earliestChanged = null;
```
  - Inside the existing `if (originalPlanRegistration.SumFlexEnd != planRegistration.SumFlexEnd || originalPlanRegistration.Flex != planRegistration.Flex)`
    block (L382-395), after `planRegistration.Update(innerDbContext).GetAwaiter().GetResult();` (L394):
```csharp
                            // Ids are ordered by date (L357), so the first change is the earliest.
                            earliestChanged ??= planRegistration.Date;
```
  - After L397, where the foreach closes, and still inside the site's `try`:
```csharp
                if (earliestChanged is { } fromDate)
                {
                    FlexChainRecompute
                        .RunForwardAsync(innerDbContext, assignedSite, siteId, fromDate)
                        .GetAwaiter().GetResult();
                }
```
  "Earliest recalculated date" here means the earliest day whose `Flex` or `SumFlexEnd` the run actually
  changed, not the first day it visited. That way a night with no changes starts no walk. It also means the
  whole last month is not re-chained every night, which would heal old breaks without anyone noticing.
- [ ] **Step 4: Run the tests** with filter `FullyQualifiedName~SearchListJob`. Both the new test and the
  existing `SearchListJobTests.NightlyRecalculation_SkipsReconciledDays_AndStillUpdatesTheOpenOnes` must pass.
  Why the existing test still passes:
  - d-3 changes from 7 h to 8 h, so the walk starts at d-3.
  - The last live row before d-3 is d-5, the reconciled boundary, which is before the start date. So the walk
    seeds from d-5 and only reads it.
  - The walk re-chains d-3 and d-2. Both are unlocked, and only their Flex and SumFlex columns change.
  - `PlanHours == 8` still holds, and the `Version` of the locked rows d-6 and d-5 does not move.
- [ ] **Step 5: Build and commit**
  ```bash
  dotnet build ServiceTimePlanningPlugin.sln
  git add ServiceTimePlanningPlugin/Scheduler/Jobs/SearchListJob.cs \
          ServiceTimePlanningPlugin.Integration.Test/SearchListJobCarryForwardTests.cs
  git commit -m "fix(nightly): carry a recalculated day's balance to the days after it

  The nightly recalculation re-chained only the days whose plan hours
  changed, so the days after them kept a stale opening balance. Each site
  is now walked once from the earliest day the run changed.

  Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
  ```

---

## Final checks for the PR

- [ ] `dotnet build ServiceTimePlanningPlugin.sln` is clean.
- [ ] `git diff origin/stable --stat` shows no change to `Scheduler/Jobs/FlexChainCatchUpJob.cs`,
  `Infrastructure/Helpers/PlanRegistrationHelper.cs` or any existing test file.
- [ ] Dual review (code-review + code-simplifier, in parallel) before each commit, per CLAUDE.md.
- [ ] Push `fix/flex-chain-service` and open a PR against `stable`.
  - Put the accepted B1 changes (T-S2 table, rows 4-7) in the PR body.
  - Say that the Google Sheet pull walk is deferred to sub-project 5, together with the plugin's pull.
  - Watch CI to a verdict. The integration tests run for the first time there.

## Remaining notes and open questions

1. **Check the base 10.0.65 API names:** that `DayLock` lives in
   `Microting.TimePlanningBase.Infrastructure.Helpers`, and that `OpenRows` is
   `(IQueryable<PlanRegistration>, DateTime?)`. T-S1 assumes both.
2. **`LockedThroughForSitesAsync` stays local.** The base exposes no multi-site variant, so its private
   `BoundaryRows` is a second copy of "Reconciled and not soft-deleted". If the base adds a sites variant,
   forward to it as well.
3. **`UpdatePlanRegistration` still recomputes one-minute hours from device stamps** (PlanRegistrationHelper
   L85-89 and L364-368 call `ApplyNettoFlexChainSecondPrecision`) whenever plan hours change. That happens in
   both the sheet pull and the nightly job. It is the recompute R2 forbids on the walk, on a path the spec does
   not name. It is left unchanged here; it needs a follow-up decision.
4. **Walks heal older breaks downstream.** Every R4 walk re-carries all later rows, so a stored break after an
   edited day disappears and later balances move by its size. The old handler walk already did this. Spec §8's
   "existing breaks remain" only holds for workers nobody edits.
5. **Dead reads in the handler.** `messageText` and `registrationDevices` are computed and never used. They are
   kept, moved above the helper call, so that T-S2a stays a pure move.


---

## Part 4 — Deploy and verify

- [ ] After PRs 1-4 are merged and the base is released: deploy plugin and service (user-run; ask).
- [ ] Re-run the read-only stored-break scan (`breaks.py` in the investigation scratchpad; one light query per tenant, sequential, 0.3 s pacing — production cluster) and confirm no break whose predecessor was edited after the deploy time.
- [ ] Re-run the tenant replay (`replay.py`) as the baseline for sub-project 3.
- [ ] Out of scope, carried to sub-project 5: Google Sheet pull walks (plugin `GoogleSheetHelper`, service `SearchListJob.Execute`), INVERTED-SUMFLEX-SIGN, `PlanHoursInSeconds` on pulled one-minute rows; `UpdatePlanRegistration` recomputing one-minute hours from stamps when plan hours change; B2 extra pause slots.
