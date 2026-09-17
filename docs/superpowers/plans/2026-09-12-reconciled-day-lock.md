# Reconciled ("Afstemt") Day Lock Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let a web user mark a worker's day "Afstemt", which freezes that day and every earlier day for that worker against edits and recalculation, unlockable only in reverse order.

**Architecture:** The lock is *derived*, never stored: a day is locked when that worker has any `Reconciled` day at or after it. `Reconciled`/`ReconciledAt` already exist in the DB. Enforcement is three layers — a `SaveChangesInterceptor` that makes the rule unbypassable, friendly guards on user-facing write paths so blocked edits get a message instead of a 500, and recalculation paths that skip locked days silently.

**Tech Stack:** C# / .NET 10 / EF Core 10 / MySQL via the **Microting.EntityFrameworkCore.MySql** fork (NOT Pomelo — see Task 2); Angular 20.3 NgModule (`standalone: false`), Angular Material + CDK 20.2.14, `mtx-grid` 20.4.2, ngx-translate 17; NUnit + NSubstitute + Testcontainers; Playwright.

**Spec:** `docs/superpowers/specs/2026-09-12-reconciled-day-lock-design.md`

## Global Constraints

- **Dev mode: NONE — edit the source repos directly.** Every path in this plan is a
  source-repo path and every task ends in a commit there. Do **not** run
  `devgetchanges.sh`, and do not edit the host-app mirror under
  `eform-angular-frontend/eFormAPI/Plugins/` — that mirror is stale (dated Aug 14)
  and syncing from it would overwrite this work with old code. The one exception is
  Task 10, which edits `eform-angular-frontend`'s own SCSS because CLAUDE.md puts all
  SCSS there.

- **No base-repo change.** `Reconciled` (`tinyint(1) NOT NULL DEFAULT 0`) and `ReconciledAt` (`datetime(6) NULL`) already exist on `PlanRegistrations` **and** `PlanRegistrationVersions` (migration `20260127060748`), and ship in the pinned `Microting.TimePlanningBase` **10.0.62**. Do not add a migration. Do not bump the package.
- **Invariant I1:** `ReconciledAt` is non-null exactly when `Reconciled` is true. Both written together; unlock clears both.
- **Invariant I2:** `Reconciled` may only be set for `date < DateTime.Now.Date`. Server-enforced, not merely hidden in the UI.
- **Invariant I3:** No write may modify, create or delete a `PlanRegistration` whose `Date <= lockedThrough(SdkSitId)`.
- **I2 is load-bearing beyond its own purpose.** Because the boundary is always in the past and edits are only allowed above it, forward flex cascades (one runs 180 days ahead, one is unbounded) provably cannot reach a locked day. If I2 is ever relaxed, those cascades must be revisited first.
- **Timezone:** compare against `DateTime.Now.Date` (server local), matching `PlanRegistrationHelper` and the existing mobile guard. Never `UtcNow`.
- **Copy rule:** user-facing text states what the day *is*. It never explains a restriction by referring to what an administrator may do. ("admin = Microting".)
- **Permissions:** only the first user may reconcile, unlock, or bulk-reconcile — see spec §8.6.
- **Payroll:** `Reconciled` and `TransferredToPayroll` are independent in both directions. Do not couple them.
- **Mobile:** rejects the write with the same localized failure as web. No mobile UI work.
- **Tests run only in CI.** Never run `dotnet test`, `playwright test`, `jest` or `npm test` locally — a hook blocks them. `dotnet build` is allowed and expected. Push and watch `gh pr checks <n>`.
- **Never commit to `stable` directly.** Branch, PR into `stable`.
- **SCSS lives in `eform-angular-frontend`**, never per-plugin (CLAUDE.md). Task 10 is a separate repo and a separate PR.
- **New C# test classes must be added to the shard filters in BOTH** `.github/workflows/dotnet-core-pr.yml` and `dotnet-core-master.yml`, or they silently never run.

---

## File Structure

**New files**

| Path | Responsibility |
|---|---|
| `eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn/Infrastructure/Helpers/DayLockHelper.cs` | The lock predicate. Sole source of truth for `lockedThrough` and `IsLocked`. |
| `eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn/Infrastructure/Interceptors/ReconciledDayLockInterceptor.cs` | Stateless `SaveChangesInterceptor` enforcing I3 across every write path. |
| `eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn/Infrastructure/Models/Planning/ReconcileThroughRequestModel.cs` | Bulk request body. |
| `eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn/Infrastructure/Models/Planning/ReconcileThroughResultModel.cs` | Bulk result (applied / skipped / landed-on). |
| `eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn.Test/DayLockHelperTests.cs` | Predicate unit tests. |
| `eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn.Test/DayLockInterceptorTests.cs` | Interceptor enforcement tests. |
| `eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn.Test/ReconcileServiceTests.cs` | Endpoint behaviour tests. |
| `eform-client/playwright/e2e/plugins/time-planning-pn/s/reconcile-day-lock.spec.ts` | E2E. |

**Modified — backend**

| Path | Change |
|---|---|
| `.../Infrastructure/Helpers/TimePlanningDbContextHelper.cs` | Build options locally; attach interceptor. |
| `.../EformTimePlanningPlugin.cs:183-189` | `.AddInterceptors(...)` on the pooled context. |
| `.../Services/TimePlanningPlanningService/TimePlanningPlanningService.cs` | Guards; three new service methods; `LockedThrough` on the row model. |
| `.../Services/TimePlanningPlanningService/ITimePlanningPlanningService.cs` | Three new signatures. |
| `.../Controllers/TimePlanningPlanningController.cs` | Three new routes. |
| `.../Services/TimePlanningWorkingHoursService/TimePlanningWorkingHoursService.cs` | Guards on `CreateUpdate` and both `UpdateWorkingHour` overloads. |
| `.../Infrastructure/Helpers/PlanRegistrationHelper.cs` | Skip locked days; project the two DTO fields. |
| `.../Infrastructure/Models/Planning/TimePlanningPlanningPrDayModel.cs` | `+Reconciled`, `+ReconciledAt`. |
| `.../Infrastructure/Models/Planning/TimePlanningPlanningModel.cs` | `+LockedThrough`. |
| `.../Resources/Translations.resx` + `Translations.da.resx` | Six new keys. |
| `.../TimePlanning.Pn.Test/TestBaseSetup.cs` | Attach the interceptor so tests exercise it. |

**Modified — frontend (plugin repo)**

`models/plannings/planning-pr-day.model.ts`, `models/plannings/time-planning.model.ts`, `services/time-planning-pn-plannings.service.ts`, `components/plannings/time-plannings-table/time-plannings-table.component.{ts,html}`, `components/plannings/time-plannings-container/time-plannings-container.component.{ts,html}`, `components/plannings/time-planning-actions/workday-entity/workday-entity-dialog.component.{ts,html}`, `help/help.model.ts`, `help/planning-help.registry.ts`, `help/i18n/{da,enUS}.ts`, `i18n/{da,enUS}.ts`.

**Modified — host repo (separate PR):** `eform-client/src/scss/styles.scss`.

---

## Task 1: The lock predicate

**Files:**
- Create: `eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn/Infrastructure/Helpers/DayLockHelper.cs`
- Test: `eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn.Test/DayLockHelperTests.cs`
- Modify: `.github/workflows/dotnet-core-pr.yml:251`, `.github/workflows/dotnet-core-master.yml:262`

**Interfaces:**
- Consumes: nothing.
- Produces:
  - `DayLockHelper.LockedThroughAsync(TimePlanningPnDbContext db, int sdkSitId) -> Task<DateTime?>`
  - `DayLockHelper.LockedThroughForSitesAsync(TimePlanningPnDbContext db, IReadOnlyCollection<int> sdkSitIds) -> Task<Dictionary<int, DateTime?>>`
  - `DayLockHelper.IsLocked(DateTime? lockedThrough, DateTime date) -> bool`
  - `DayLockHelper.CanReconcile(DateTime date) -> bool`

- [ ] **Step 1: Write the failing test**

Create `TimePlanning.Pn.Test/DayLockHelperTests.cs`:

```csharp
using System;
using System.Threading.Tasks;
using Microting.eForm.Infrastructure.Constants;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Helpers;
using PlanRegistrationEntity = Microting.TimePlanningBase.Infrastructure.Data.Entities.PlanRegistration;

namespace TimePlanning.Pn.Test;

/// <summary>
/// The lock is derived, never stored: a day is locked when the worker has any
/// Reconciled day at or after it. These tests pin that derivation, because
/// every enforcement layer downstream trusts it.
/// </summary>
[TestFixture]
public class DayLockHelperTests : TestBaseSetup
{
    [SetUp]
    public async Task SetUpTest() => await base.Setup();

    // No workflowState parameter: PnBase.Create overwrites it with "created"
    // regardless of what is set here.
    private async Task Seed(int site, DateTime date, bool reconciled)
    {
        await new PlanRegistrationEntity
        {
            SdkSitId = site,
            Date = date,
            Reconciled = reconciled,
            ReconciledAt = reconciled ? new DateTime(2026, 1, 20, 9, 12, 0) : (DateTime?)null,
            PlanText = "",
            CommentOffice = "",
            CommentOfficeAll = "",
            WorkflowState = Constants.WorkflowStates.Created,
            CreatedByUserId = 1,
            UpdatedByUserId = 1,
        }.Create(TimePlanningPnDbContext!);
    }

    [Test]
    public async Task LockedThrough_NoReconciledDay_IsNull()
    {
        await Seed(700, new DateTime(2026, 1, 12), reconciled: false);

        var result = await DayLockHelper.LockedThroughAsync(TimePlanningPnDbContext!, 700);

        Assert.That(result, Is.Null, "a worker with nothing reconciled has no boundary");
    }

    [Test]
    public async Task LockedThrough_SeveralReconciled_IsTheLatest()
    {
        await Seed(701, new DateTime(2026, 1, 12), reconciled: true);
        await Seed(701, new DateTime(2026, 1, 16), reconciled: true);
        await Seed(701, new DateTime(2026, 1, 14), reconciled: false);

        var result = await DayLockHelper.LockedThroughAsync(TimePlanningPnDbContext!, 701);

        Assert.That(result, Is.EqualTo(new DateTime(2026, 1, 16)));
    }

    [Test]
    public async Task LockedThrough_IgnoresRemovedRows()
    {
        // PnBase.Create OVERWRITES WorkflowState with "created" unconditionally
        // (PnBase.cs:16), so a row cannot be seeded as Removed -- it has to be
        // created and then soft-deleted. Note this test must run BEFORE the
        // interceptor exists (Task 2) or the Delete below lands on the boundary
        // day and is rejected; from Task 2 onward, delete the LATER row first
        // while the earlier one still holds no boundary.
        await Seed(702, new DateTime(2026, 1, 12), reconciled: true);
        await Seed(702, new DateTime(2026, 1, 18), reconciled: true);

        var later = await TimePlanningPnDbContext!.PlanRegistrations
            .FirstAsync(x => x.SdkSitId == 702 && x.Date == new DateTime(2026, 1, 18));
        await later.Delete(TimePlanningPnDbContext!);

        var result = await DayLockHelper.LockedThroughAsync(TimePlanningPnDbContext!, 702);

        Assert.That(result, Is.EqualTo(new DateTime(2026, 1, 12)),
            "a soft-deleted reconciled row must not hold the boundary");
    }

    [Test]
    public async Task LockedThrough_IsPerWorker()
    {
        await Seed(703, new DateTime(2026, 1, 16), reconciled: true);
        await Seed(704, new DateTime(2026, 1, 12), reconciled: false);

        // Await FIRST, then assert synchronously. Assert.Multiple(async () => ...)
        // binds to the Action overload, making the lambda async void: its
        // assertions can run after the block exits, and a failure is then lost
        // or blamed on the wrong test. NUnit.Analyzers flags this.
        var seven03 = await DayLockHelper.LockedThroughAsync(TimePlanningPnDbContext!, 703);
        var seven04 = await DayLockHelper.LockedThroughAsync(TimePlanningPnDbContext!, 704);

        Assert.Multiple(() =>
        {
            Assert.That(seven03, Is.EqualTo(new DateTime(2026, 1, 16)));
            Assert.That(seven04, Is.Null, "one worker's boundary must not leak onto another");
        });
    }

    [Test]
    public async Task LockedThroughForSites_ResolvesManyInOneQuery()
    {
        await Seed(705, new DateTime(2026, 1, 16), reconciled: true);
        await Seed(706, new DateTime(2026, 1, 13), reconciled: true);
        await Seed(707, new DateTime(2026, 1, 13), reconciled: false);

        var map = await DayLockHelper.LockedThroughForSitesAsync(
            TimePlanningPnDbContext!, new[] { 705, 706, 707 });

        Assert.Multiple(() =>
        {
            Assert.That(map[705], Is.EqualTo(new DateTime(2026, 1, 16)));
            Assert.That(map[706], Is.EqualTo(new DateTime(2026, 1, 13)));
            Assert.That(map[707], Is.Null);
            Assert.That(map.Count, Is.EqualTo(3), "every requested site gets an entry");
        });
    }

    [TestCase("2026-01-10", false, TestName = "IsLocked_BeforeBoundary_True")]
    [TestCase("2026-01-16", false, TestName = "IsLocked_AtBoundary_True")]
    [TestCase("2026-01-17", true,  TestName = "IsLocked_AfterBoundary_False")]
    public void IsLocked_RelativeToBoundary(string date, bool expectedOpen)
    {
        var boundary = new DateTime(2026, 1, 16);

        var locked = DayLockHelper.IsLocked(boundary, DateTime.Parse(date));

        Assert.That(locked, Is.EqualTo(!expectedOpen));
    }

    [Test]
    public void IsLocked_NoBoundary_NothingIsLocked()
    {
        Assert.That(DayLockHelper.IsLocked(null, new DateTime(2020, 1, 1)), Is.False);
    }

    [Test]
    public void IsLocked_IgnoresTimeOfDay()
    {
        var boundary = new DateTime(2026, 1, 16);

        Assert.That(DayLockHelper.IsLocked(boundary, new DateTime(2026, 1, 16, 23, 59, 59)),
            Is.True, "the boundary day is locked for its whole length");
    }

    [Test]
    public void CanReconcile_TodayAndFuture_False_Past_True()
    {
        Assert.Multiple(() =>
        {
            Assert.That(DayLockHelper.CanReconcile(DateTime.Now.Date), Is.False,
                "today must stay open so time can still be registered");
            Assert.That(DayLockHelper.CanReconcile(DateTime.Now.Date.AddDays(1)), Is.False);
            Assert.That(DayLockHelper.CanReconcile(DateTime.Now.Date.AddDays(-1)), Is.True);
            Assert.That(DayLockHelper.CanReconcile(DateTime.Now.Date.AddHours(23)), Is.False,
                "a time-of-day on today is still today");
        });
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

This repo runs tests only in CI. Verify the failure shape locally with a build instead:

Run: `cd eFormAPI/Plugins/TimePlanning.Pn && dotnet build TimePlanning.Pn.sln -v q`
Expected: FAIL — `error CS0103: The name 'DayLockHelper' does not exist in the current context`

- [ ] **Step 3: Write minimal implementation**

Create `TimePlanning.Pn/Infrastructure/Helpers/DayLockHelper.cs`:

```csharp
#nullable enable
namespace TimePlanning.Pn.Infrastructure.Helpers;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microting.eForm.Infrastructure.Constants;
using Microting.TimePlanningBase.Infrastructure.Data;

/// <summary>
/// The single source of truth for "is this day locked".
///
/// The lock is DERIVED, never stored. A worker's boundary is the latest date
/// they have a Reconciled registration on; every day at or before it is locked.
/// Earlier days are therefore locked WITHOUT being marked Reconciled, and a day
/// below the boundary cannot be unlocked because unlocking it would not move
/// MAX(Date) -- both requirements fall out of the model instead of needing a
/// job to keep flags in sync.
/// </summary>
public static class DayLockHelper
{
    /// <summary>
    /// The latest reconciled date for one worker, or null when they have none.
    /// Soft-deleted rows never hold the boundary.
    /// </summary>
    public static async Task<DateTime?> LockedThroughAsync(TimePlanningPnDbContext db, int sdkSitId)
    {
        return await db.PlanRegistrations
            .Where(x => x.SdkSitId == sdkSitId)
            .Where(x => x.Reconciled)
            .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
            .MaxAsync(x => (DateTime?)x.Date)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Boundaries for many workers in ONE query. Callers that render a grid
    /// resolve this once per request rather than once per day cell.
    /// Every requested site gets an entry; sites with no reconciled day map to null.
    /// </summary>
    public static async Task<Dictionary<int, DateTime?>> LockedThroughForSitesAsync(
        TimePlanningPnDbContext db, IReadOnlyCollection<int> sdkSitIds)
    {
        var found = await db.PlanRegistrations
            .Where(x => sdkSitIds.Contains(x.SdkSitId))
            .Where(x => x.Reconciled)
            .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
            .GroupBy(x => x.SdkSitId)
            .Select(g => new { SdkSitId = g.Key, Max = g.Max(x => x.Date) })
            .ToListAsync()
            .ConfigureAwait(false);

        var map = found.ToDictionary(x => x.SdkSitId, x => (DateTime?)x.Max);
        foreach (var id in sdkSitIds)
        {
            map.TryAdd(id, null);
        }
        return map;
    }

    /// <summary>
    /// Pure predicate, so callers can resolve the boundary once and test many
    /// dates against it without touching the database again.
    /// </summary>
    public static bool IsLocked(DateTime? lockedThrough, DateTime date)
        => lockedThrough.HasValue && date.Date <= lockedThrough.Value.Date;

    /// <summary>
    /// Invariant I2: today and future days must stay open so time can still be
    /// registered. This is also what makes the forward flex cascades unable to
    /// reach a locked day -- see the design doc before relaxing it.
    ///
    /// DateTime.Now, not UtcNow: PlanRegistration.Date is a local midnight, and
    /// the existing mobile guard compares the same way.
    /// </summary>
    public static bool CanReconcile(DateTime date) => date.Date < DateTime.Now.Date;
}
```

- [ ] **Step 4: Register the test class in BOTH CI shard filters**

Without this the tests silently never run. In `.github/workflows/dotnet-core-pr.yml` line 251 and `.github/workflows/dotnet-core-master.yml` line 262, append to the shard `c` filter string:

```
|FullyQualifiedName=TimePlanning.Pn.Test.DayLockHelperTests
```

- [ ] **Step 5: Build**

Run: `cd eFormAPI/Plugins/TimePlanning.Pn && dotnet build TimePlanning.Pn.sln -v q`
Expected: `0 Error(s)`

- [ ] **Step 6: Commit**

```bash
git add eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn/Infrastructure/Helpers/DayLockHelper.cs \
        eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn.Test/DayLockHelperTests.cs \
        .github/workflows/dotnet-core-pr.yml .github/workflows/dotnet-core-master.yml
git commit -m "feat(lock): derive the reconciled-day boundary from the data"
```

---

## Task 2: The enforcement interceptor

**Files:**
- Create: `.../TimePlanning.Pn/Infrastructure/Interceptors/ReconciledDayLockInterceptor.cs`
- Modify: `.../TimePlanning.Pn/Infrastructure/Helpers/TimePlanningDbContextHelper.cs` (whole file)
- Modify: `.../TimePlanning.Pn/EformTimePlanningPlugin.cs:183-189`
- Modify: `.../TimePlanning.Pn.Test/TestBaseSetup.cs:32-42` and `:113-122`
- Test: `.../TimePlanning.Pn.Test/DayLockInterceptorTests.cs`
- Modify: both workflow files

**Interfaces:**
- Consumes: `DayLockHelper.LockedThroughForSitesAsync`, `DayLockHelper.IsLocked` (Task 1).
- Produces: `ReconciledDayLockInterceptor` (stateless, parameterless ctor); `DayLockedException : InvalidOperationException`.

**Why an interceptor at all:** there are 32 `PlanRegistration` write sites across 11 files in 2 repos and no choke point — `PnBase.Create/Update/Delete` live in the base NuGet. Guarding call sites alone would repeat the `MaxDaysEditable` mistake, which is enforced only on the read path and in the browser and is bypassable by a crafted POST today. Every write does go through EF change tracking (no raw SQL, no `ExecuteUpdate`), so one interceptor sees all of them.

- [ ] **Step 1: Write the failing test**

Create `TimePlanning.Pn.Test/DayLockInterceptorTests.cs`:

```csharp
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microting.eForm.Infrastructure.Constants;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Interceptors;
using PlanRegistrationEntity = Microting.TimePlanningBase.Infrastructure.Data.Entities.PlanRegistration;

namespace TimePlanning.Pn.Test;

/// <summary>
/// The interceptor is the only layer that is COMPLETE -- it covers all 32
/// PlanRegistration write sites and anything added later. These tests go
/// through a context built exactly as production builds it (interceptor
/// attached), so they prove the wiring, not just the class.
/// </summary>
[TestFixture]
public class DayLockInterceptorTests : TestBaseSetup
{
    [SetUp]
    public async Task SetUpTest() => await base.Setup();

    private async Task<PlanRegistrationEntity> SeedReconciled(int site, DateTime date)
    {
        var row = new PlanRegistrationEntity
        {
            SdkSitId = site, Date = date, Reconciled = true,
            ReconciledAt = new DateTime(2026, 1, 20, 9, 12, 0),
            PlanText = "", CommentOffice = "", CommentOfficeAll = "",
            WorkflowState = Constants.WorkflowStates.Created,
            CreatedByUserId = 1, UpdatedByUserId = 1,
        };
        await row.Create(TimePlanningPnDbContext!);
        return row;
    }

    private async Task<PlanRegistrationEntity> SeedPlain(int site, DateTime date)
    {
        var row = new PlanRegistrationEntity
        {
            SdkSitId = site, Date = date,
            PlanText = "", CommentOffice = "", CommentOfficeAll = "",
            WorkflowState = Constants.WorkflowStates.Created,
            CreatedByUserId = 1, UpdatedByUserId = 1,
        };
        await row.Create(TimePlanningPnDbContext!);
        return row;
    }

    [Test]
    public async Task Modifying_ADayBelowTheBoundary_IsRejected()
    {
        // Order matters: create the earlier row FIRST. Seeding the boundary
        // first would put this Create inside the locked range, and the arrange
        // step would throw before the assertion was ever reached.
        var earlier = await SeedPlain(800, new DateTime(2026, 1, 13));
        await SeedReconciled(800, new DateTime(2026, 1, 16));

        earlier.PlanHours = 9;

        Assert.ThrowsAsync<DayLockedException>(async () =>
            await earlier.Update(TimePlanningPnDbContext!));
    }

    [Test]
    public async Task Modifying_TheBoundaryDayItself_IsRejected()
    {
        var boundary = await SeedReconciled(801, new DateTime(2026, 1, 16));

        boundary.PlanHours = 9;

        Assert.ThrowsAsync<DayLockedException>(async () =>
            await boundary.Update(TimePlanningPnDbContext!));
    }

    [Test]
    public async Task Creating_ARowInsideALockedRange_IsRejected()
    {
        await SeedReconciled(802, new DateTime(2026, 1, 16));

        Assert.ThrowsAsync<DayLockedException>(async () =>
            await SeedPlain(802, new DateTime(2026, 1, 14)));
    }

    [Test]
    public async Task SoftDeleting_ALockedRow_IsRejected()
    {
        // PnBase.Delete is a soft delete -- it sets WorkflowState = Removed via
        // UpdateInternal, so this arrives at the interceptor as Modified, not
        // Deleted. The name says SoftDeleting so nobody reads a pass here as
        // proof that the EntityState.Deleted arm works.
        var earlier = await SeedPlain(803, new DateTime(2026, 1, 13));
        await SeedReconciled(803, new DateTime(2026, 1, 16));

        Assert.ThrowsAsync<DayLockedException>(async () =>
            await earlier.Delete(TimePlanningPnDbContext!));
    }

    [Test]
    public async Task Modifying_ADayAboveTheBoundary_IsAllowed()
    {
        var later = await SeedPlain(804, new DateTime(2026, 1, 18));
        await SeedReconciled(804, new DateTime(2026, 1, 16));

        later.PlanHours = 9;
        await later.Update(TimePlanningPnDbContext!);

        var reloaded = await TimePlanningPnDbContext!.PlanRegistrations
            .FirstAsync(x => x.Id == later.Id);
        Assert.That(reloaded.PlanHours, Is.EqualTo(9),
            "days after the boundary must stay fully editable");
    }

    [Test]
    public async Task ANotherWorkersBoundary_DoesNotLockThisWorker()
    {
        var other = await SeedPlain(806, new DateTime(2026, 1, 13));
        await SeedReconciled(805, new DateTime(2026, 1, 16));

        other.PlanHours = 9;
        await other.Update(TimePlanningPnDbContext!);

        var reloaded = await TimePlanningPnDbContext!.PlanRegistrations
            .FirstAsync(x => x.Id == other.Id);
        Assert.That(reloaded.PlanHours, Is.EqualTo(9),
            "the boundary is per worker; it must not leak across sites");
    }

    [Test]
    public async Task SettingReconciled_OnTheBoundaryDay_IsAllowed()
    {
        // Unlocking must not be blocked by the very lock it is clearing.
        var boundary = await SeedReconciled(807, new DateTime(2026, 1, 16));

        boundary.Reconciled = false;
        boundary.ReconciledAt = null;
        await boundary.Update(TimePlanningPnDbContext!);

        var reloaded = await TimePlanningPnDbContext!.PlanRegistrations
            .FirstAsync(x => x.Id == boundary.Id);
        Assert.That(reloaded.Reconciled, Is.False,
            "clearing the flag on the boundary day is how unlocking works");
    }
}
```

- [ ] **Step 2: Run build to verify it fails**

Run: `cd eFormAPI/Plugins/TimePlanning.Pn && dotnet build TimePlanning.Pn.sln -v q`
Expected: FAIL — `error CS0246: The type or namespace name 'DayLockedException' could not be found`

- [ ] **Step 3: Write the interceptor**

Create `TimePlanning.Pn/Infrastructure/Interceptors/ReconciledDayLockInterceptor.cs`:

```csharp
#nullable enable
namespace TimePlanning.Pn.Infrastructure.Interceptors;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microting.TimePlanningBase.Infrastructure.Data;
using PlanRegistrationEntity = Microting.TimePlanningBase.Infrastructure.Data.Entities.PlanRegistration;

/// <summary>
/// Thrown when a write would touch a day at or before the worker's reconciled
/// boundary. Distinct from a generic InvalidOperationException so the friendly
/// guards in the services can tell "the lock stopped this" from "something else
/// broke" -- and so a stray one is legible in Sentry.
/// </summary>
public class DayLockedException(int sdkSitId, DateTime date, DateTime lockedThrough)
    : InvalidOperationException(
        $"Day {date:yyyy-MM-dd} for site {sdkSitId} is locked: reconciled through {lockedThrough:yyyy-MM-dd}.")
{
    public int SdkSitId { get; } = sdkSitId;
    public DateTime Date { get; } = date;
    public DateTime LockedThrough { get; } = lockedThrough;
}

/// <summary>
/// Enforces invariant I3 across every PlanRegistration write path.
///
/// STATELESS BY DESIGN. The pooled context registration
/// (EformTimePlanningPlugin.AddDbContextPool) reuses context instances, so an
/// interceptor holding per-request state would leak it between requests.
/// Everything this needs is read from the change tracker on each call.
///
/// The boundary is resolved ONCE per SaveChanges for the distinct sites in the
/// change set, not once per row.
/// </summary>
public class ReconciledDayLockInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        GuardAsync(eventData.Context, CancellationToken.None).GetAwaiter().GetResult();
        return base.SavingChanges(eventData, result);
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        await GuardAsync(eventData.Context, cancellationToken).ConfigureAwait(false);
        return await base.SavingChangesAsync(eventData, result, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task GuardAsync(DbContext? context, CancellationToken ct)
    {
        if (context is not TimePlanningPnDbContext db)
        {
            return;
        }

        // PnBase.Delete is a SOFT delete: it sets WorkflowState = Removed and
        // routes through UpdateInternal, so a delete reaches here as Modified,
        // never Deleted. The Deleted arm is kept as insurance against a future
        // hard delete; do not "simplify" it away on the grounds that it never
        // fires today.
        var touched = db.ChangeTracker.Entries<PlanRegistrationEntity>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        if (touched.Count == 0)
        {
            return;
        }

        var siteIds = touched.Select(e => e.Entity.SdkSitId).Distinct().ToList();

        // Query `db` itself. An earlier draft opened a second context here;
        // that is a production outage, because ServerVersion.AutoDetect opens a
        // connection and runs a version query, and this guard runs on EVERY
        // save -- including the four Update calls inside the per-day loop of
        // UpdatePlanRegistrationsInPeriod, which runs on every dashboard load.
        // 50 workers x 31 days would mean thousands of extra connections per
        // page view.
        //
        // Querying `db` is safe: LockedThroughForSitesAsync projects into an
        // anonymous type so it tracks nothing and cannot pollute the change
        // tracker; a query never re-enters SaveChanges so there is no
        // recursion; and it reuses the open connection and any ambient
        // transaction, which a separate context could not see.
        var boundaries = await DayLockHelper
            .LockedThroughForSitesAsync(db, siteIds)
            .ConfigureAwait(false);

        foreach (var entry in touched)
        {
            var siteId = entry.Entity.SdkSitId;
            if (!boundaries.TryGetValue(siteId, out var boundary) || boundary is null)
            {
                continue;
            }

            if (!DayLockHelper.IsLocked(boundary, entry.Entity.Date))
            {
                continue;
            }

            // The one permitted write inside the locked range: clearing the flag
            // on the boundary day itself. That is what unlocking IS, and it must
            // not be blocked by the lock it is removing.
            if (IsUnlockOfBoundaryDay(entry, boundary.Value))
            {
                continue;
            }

            throw new DayLockedException(siteId, entry.Entity.Date, boundary.Value);
        }
    }

    private static bool IsUnlockOfBoundaryDay(
        EntityEntry<PlanRegistrationEntity> entry, DateTime boundary)
    {
        if (entry.State != EntityState.Modified)
        {
            return false;
        }
        if (entry.Entity.Date.Date != boundary.Date)
        {
            return false;
        }
        // Reconciled must be going true -> false, and nothing else about the row
        // may be changing in the same save.
        var reconciled = entry.Property(x => x.Reconciled);
        if (!reconciled.IsModified || (bool)reconciled.CurrentValue! )
        {
            return false;
        }
        var changedOthers = entry.Properties
            .Where(p => p.IsModified)
            .Select(p => p.Metadata.Name)
            .Where(n => n is not (nameof(PlanRegistrationEntity.Reconciled)
                or nameof(PlanRegistrationEntity.ReconciledAt)
                or nameof(PlanRegistrationEntity.UpdatedAt)
                or nameof(PlanRegistrationEntity.Version)
                or nameof(PlanRegistrationEntity.UpdatedByUserId)))
            .ToList();

        return changedOthers.Count == 0;
    }
}
```

- [ ] **Step 4: Wire it into the web helper**

Replace the body of `TimePlanning.Pn/Infrastructure/Helpers/TimePlanningDbContextHelper.cs` entirely:

```csharp
using System;
using Microsoft.EntityFrameworkCore;
using Microting.TimePlanningBase.Infrastructure.Data;
using TimePlanning.Pn.Infrastructure.Interceptors;
// NB: no Pomelo using. This repo uses the Microting.EntityFrameworkCore.MySql
// fork, and MariaDbServerVersion/ServerVersion come from
// Microsoft.EntityFrameworkCore -- which is why EformTimePlanningPlugin.cs
// needs no provider-specific using either. Adding a Pomelo PackageReference
// would introduce a second, conflicting provider.

namespace TimePlanning.Pn.Infrastructure.Helpers;

/// <summary>
/// Builds plugin DbContexts with the day-lock interceptor attached.
///
/// This no longer delegates to TimePlanningPnContextFactory: that factory
/// builds its DbContextOptionsBuilder in a method-local and exposes no hook, so
/// there is no way to attach an interceptor through it. The context's public
/// options constructor is the supported seam, and it needs no base-package change.
/// </summary>
public class TimePlanningDbContextHelper(string connectionString) : ITimePlanningDbContextHelper
{
    private string ConnectionString { get; } = connectionString;

    public TimePlanningPnDbContext GetDbContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<TimePlanningPnDbContext>();

        // Hardcoded version, exactly as TimePlanningPnContextFactory does
        // (factory line 39). ServerVersion.AutoDetect OPENS A CONNECTION and
        // runs a version query; this method is called once per assigned site on
        // every dashboard load, so AutoDetect here would add a round-trip per
        // worker per page view that the current code does not pay.
        optionsBuilder.UseMySql(
            ConnectionString,
            new MariaDbServerVersion(new Version(10, 5, 0)),
            mySqlOptionsAction: builder => { builder.EnableRetryOnFailure(); });

        optionsBuilder.AddInterceptors(new ReconciledDayLockInterceptor());

        return new TimePlanningPnDbContext(optionsBuilder.Options);
    }
}

public interface ITimePlanningDbContextHelper
{
    TimePlanningPnDbContext GetDbContext();
}
```

- [ ] **Step 5: Wire it into the pooled registration**

In `TimePlanning.Pn/EformTimePlanningPlugin.cs`, change lines 183-189 to add the interceptor. The pool reuses instances, which is safe here only because the interceptor is stateless:

```csharp
        services.AddDbContextPool<TimePlanningPnDbContext>(o =>
            o.UseMySql(connectionString, new MariaDbServerVersion(
                ServerVersion.AutoDetect(connectionString)), mySqlOptionsAction: builder =>
            {
                builder.EnableRetryOnFailure();
                builder.MigrationsAssembly(PluginAssembly().FullName);
            })
            .AddInterceptors(new ReconciledDayLockInterceptor()));
```

Add at the top of the file: `using TimePlanning.Pn.Infrastructure.Interceptors;`

- [ ] **Step 6: Make the tests exercise the real wiring**

`TestBaseSetup` builds its own contexts, so without this the tests would bypass the interceptor entirely and the lock would look tested while being unenforced.

In `TimePlanning.Pn.Test/TestBaseSetup.cs`, add `using TimePlanning.Pn.Infrastructure.Interceptors;` and append `.AddInterceptors(new ReconciledDayLockInterceptor())` to the options in **both** `GetTimePlanningPnDbContext` (after line 40) and `CreateTimePlanningPnDbContext` (after line 120):

```csharp
        optionsBuilder.AddInterceptors(new ReconciledDayLockInterceptor());
```

- [ ] **Step 7: Register the test class in BOTH shard filters**

Append to the shard `c` filter in `dotnet-core-pr.yml:251` and `dotnet-core-master.yml:262`:

```
|FullyQualifiedName=TimePlanning.Pn.Test.DayLockInterceptorTests
```

- [ ] **Step 8: Build**

Run: `cd eFormAPI/Plugins/TimePlanning.Pn && dotnet build TimePlanning.Pn.sln -v q`
Expected: `0 Error(s)`

- [ ] **Step 9: Commit**

```bash
git add eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn/Infrastructure/Interceptors/ReconciledDayLockInterceptor.cs \
        eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn/Infrastructure/Helpers/TimePlanningDbContextHelper.cs \
        eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn/EformTimePlanningPlugin.cs \
        eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn.Test/TestBaseSetup.cs \
        eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn.Test/DayLockInterceptorTests.cs \
        .github/workflows/dotnet-core-pr.yml .github/workflows/dotnet-core-master.yml
git commit -m "feat(lock): enforce the day lock at the SaveChanges boundary"
```

---

## Task 3: Localized messages

**Files:**
- Modify: `.../TimePlanning.Pn/Resources/Translations.resx` (append before `</root>` at :204)
- Modify: `.../TimePlanning.Pn/Resources/Translations.da.resx`

**Interfaces:**
- Produces: six resx keys consumed by Tasks 4 and 5.

Note: several existing keys (`PlanningNotFound`, `ErrorWhileUpdatingPlanning`) are **not** in any resx — `IStringLocalizer` returns the key itself when unresolved. Do not follow that precedent; add these properly.

- [ ] **Step 1: Add the English entries**

In `Translations.resx`, before `</root>`:

```xml
    <data name="DayIsReconciled" xml:space="preserve">
        <value>This day is reconciled. The figures are final.</value>
    </data>
    <data name="DayIsLockedByReconciledDay" xml:space="preserve">
        <value>This day is locked because it is before a reconciled day.</value>
    </data>
    <data name="CannotReconcileTodayOrFuture" xml:space="preserve">
        <value>Only days before today can be reconciled.</value>
    </data>
    <data name="OnlyLatestReconciledDayCanBeUnlocked" xml:space="preserve">
        <value>Unlock the most recent reconciled day first.</value>
    </data>
    <data name="SuccessfullyReconciledDay" xml:space="preserve">
        <value>Day reconciled</value>
    </data>
    <data name="SuccessfullyUnlockedDay" xml:space="preserve">
        <value>Day unlocked</value>
    </data>
```

- [ ] **Step 2: Add the Danish entries**

In `Translations.da.resx`, before `</root>`:

```xml
    <data name="DayIsReconciled" xml:space="preserve">
        <value>Dagen er afstemt. Tallene er endelige.</value>
    </data>
    <data name="DayIsLockedByReconciledDay" xml:space="preserve">
        <value>Dagen er låst, fordi den ligger før en afstemt dag.</value>
    </data>
    <data name="CannotReconcileTodayOrFuture" xml:space="preserve">
        <value>Kun dage før i dag kan afstemmes.</value>
    </data>
    <data name="OnlyLatestReconciledDayCanBeUnlocked" xml:space="preserve">
        <value>Lås den seneste afstemte dag op først.</value>
    </data>
    <data name="SuccessfullyReconciledDay" xml:space="preserve">
        <value>Dagen er afstemt</value>
    </data>
    <data name="SuccessfullyUnlockedDay" xml:space="preserve">
        <value>Dagen er låst op</value>
    </data>
```

- [ ] **Step 3: Build and commit**

Run: `cd eFormAPI/Plugins/TimePlanning.Pn && dotnet build TimePlanning.Pn.sln -v q`
Expected: `0 Error(s)`

```bash
git add eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn/Resources/Translations.resx \
        eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn/Resources/Translations.da.resx
git commit -m "feat(lock): add the day-lock messages in English and Danish"
```

---

## Task 4: Reconcile / unlock / bulk endpoints

**Files:**
- Create: `.../Infrastructure/Models/Planning/ReconcileThroughRequestModel.cs`
- Create: `.../Infrastructure/Models/Planning/ReconcileThroughResultModel.cs`
- Modify: `.../Services/TimePlanningPlanningService/ITimePlanningPlanningService.cs`
- Modify: `.../Services/TimePlanningPlanningService/TimePlanningPlanningService.cs` (append three methods)
- Modify: `.../Controllers/TimePlanningPlanningController.cs`
- Test: `.../TimePlanning.Pn.Test/ReconcileServiceTests.cs`
- Modify: both workflow files

**Interfaces:**
- Consumes: `DayLockHelper.*` (Task 1); resx keys (Task 3).
- Produces:
  - `ITimePlanningPlanningService.Reconcile(int id) -> Task<OperationResult>`
  - `ITimePlanningPlanningService.Unreconcile(int id) -> Task<OperationResult>`
  - `ITimePlanningPlanningService.ReconcileThrough(ReconcileThroughRequestModel model) -> Task<OperationDataResult<ReconcileThroughResultModel>>`
  - `ReconcileThroughRequestModel { DateTime Date; List<int> SiteIds; }`
  - `ReconcileThroughResultModel { Dictionary<int,DateTime> LandedOnBySiteId; int Applied; List<int> SkippedAlreadyFurtherForward; List<int> SkippedNoRegistration; List<int> AlreadyReconciledSiteIds; }`

- [ ] **Step 1: Write the failing test**

Create `TimePlanning.Pn.Test/ReconcileServiceTests.cs`.

**Three of the four symbols these tests use are not reachable from a new fixture — you must provide them:**

| symbol | status | what to do |
|---|---|---|
| `GetBaseDbContext()` | `protected` on `TestBaseSetup:85` | inherited, use as-is |
| `_service` | `private` field of `PlanningServiceMultiShiftTests:31` | declare your own |
| `BuildAdminIndexServiceAsync` | `private`, `PlanningServiceMultiShiftTests:925-957` | copy it into this fixture |
| `OneDayRequest` | `private`, `:959-963` | copy it |
| `SeedPlain` | **does not exist anywhere** | write it (below) |

Mirror the `[SetUp]` from `PlanningServiceMultiShiftTests.cs:38-81` verbatim (same substitutes, same service construction, same `_service`/`_userService`/`_dbContextHelper`/`_options` fields), copy `BuildAdminIndexServiceAsync` and `OneDayRequest` verbatim, and add this seed helper:

```csharp
    /// <summary>
    /// One plain PlanRegistration. Returns the tracked entity so tests can
    /// reconcile it by Id. Note PnBase.Create forces WorkflowState to "created".
    /// </summary>
    private async Task<PlanRegistrationEntity> SeedPlain(int siteId, DateTime date)
    {
        var row = new PlanRegistrationEntity
        {
            SdkSitId = siteId,
            Date = date,
            PlanText = "",
            CommentOffice = "",
            CommentOfficeAll = "",
            WorkflowState = Constants.WorkflowStates.Created,
            CreatedByUserId = 1,
            UpdatedByUserId = 1,
        };
        await row.Create(TimePlanningPnDbContext!);
        return row;
    }
```

Then the tests:

```csharp
    [Test]
    public async Task Reconcile_APastDay_SetsFlagAndTimestamp()
    {
        var row = await SeedPlain(900, DateTime.Now.Date.AddDays(-5));

        var result = await _service.Reconcile(row.Id);

        Assert.That(result.Success, Is.True, result.Message);
        var reloaded = await TimePlanningPnDbContext!.PlanRegistrations.FirstAsync(x => x.Id == row.Id);
        Assert.Multiple(() =>
        {
            Assert.That(reloaded.Reconciled, Is.True);
            Assert.That(reloaded.ReconciledAt, Is.Not.Null, "I1: the timestamp is written with the flag");
        });
    }

    [Test]
    public async Task Reconcile_Today_IsRejected()
    {
        var row = await SeedPlain(901, DateTime.Now.Date);

        var result = await _service.Reconcile(row.Id);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Is.EqualTo("CannotReconcileTodayOrFuture"),
            "I2: today must stay open so time can still be registered");
    }

    [Test]
    public async Task Reconcile_AFutureDay_IsRejected()
    {
        var row = await SeedPlain(902, DateTime.Now.Date.AddDays(3));

        var result = await _service.Reconcile(row.Id);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Is.EqualTo("CannotReconcileTodayOrFuture"));
    }

    [Test]
    public async Task Reconcile_AnAlreadyReconciledDay_IsAnIdempotentSuccess()
    {
        var row = await SeedPlain(903, DateTime.Now.Date.AddDays(-5));
        await _service.Reconcile(row.Id);

        var again = await _service.Reconcile(row.Id);

        Assert.That(again.Success, Is.True, "re-reconciling is a no-op, not an error");
    }

    [Test]
    public async Task Unreconcile_BelowTheBoundary_IsRejected_AndNamesTheBlockingDay()
    {
        var earlier = await SeedPlain(904, DateTime.Now.Date.AddDays(-8));
        var later   = await SeedPlain(904, DateTime.Now.Date.AddDays(-3));
        await _service.Reconcile(earlier.Id);
        await _service.Reconcile(later.Id);

        var result = await _service.Unreconcile(earlier.Id);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Is.EqualTo("OnlyLatestReconciledDayCanBeUnlocked"),
            "the puzzle rule: free the outermost piece first");
    }

    [Test]
    public async Task Unreconcile_AtTheBoundary_MovesItBackToTheNextNewest()
    {
        var earlier = await SeedPlain(905, DateTime.Now.Date.AddDays(-8));
        var later   = await SeedPlain(905, DateTime.Now.Date.AddDays(-3));
        await _service.Reconcile(earlier.Id);
        await _service.Reconcile(later.Id);

        var result = await _service.Unreconcile(later.Id);

        Assert.That(result.Success, Is.True, result.Message);
        var boundary = await DayLockHelper.LockedThroughAsync(TimePlanningPnDbContext!, 905);
        Assert.That(boundary, Is.EqualTo(earlier.Date),
            "the boundary moves back one notch, it does not vanish");
    }

    [Test]
    public async Task ReconcileThrough_MarksOneDayPerWorker_AndSkipsThoseAlreadyFurtherForward()
    {
        var aEarly = await SeedPlain(906, DateTime.Now.Date.AddDays(-6));
        var bEarly = await SeedPlain(907, DateTime.Now.Date.AddDays(-6));
        var bLate  = await SeedPlain(907, DateTime.Now.Date.AddDays(-2));
        await _service.Reconcile(bLate.Id);   // 907's boundary is already newer

        var result = await _service.ReconcileThrough(new ReconcileThroughRequestModel
        {
            Date = DateTime.Now.Date.AddDays(-6),
            SiteIds = new List<int> { 906, 907 }
        });

        Assert.That(result.Success, Is.True, result.Message);
        Assert.Multiple(() =>
        {
            Assert.That(result.Model.Applied, Is.EqualTo(1));
            Assert.That(result.Model.SkippedAlreadyFurtherForward, Is.EquivalentTo(new[] { 907 }),
                "moving 907 backwards would be an unlock, which is a separate action");
            Assert.That(result.Model.SkippedNoRegistration, Is.Empty,
                "907 was skipped for the other reason -- the two must not be conflated");
            Assert.That(result.Model.LandedOnBySiteId[906], Is.EqualTo(aEarly.Date));
            Assert.That(result.Model.LandedOnBySiteId.ContainsKey(907), Is.False);
        });
    }

    [Test]
    public async Task ReconcileThrough_LandsOnTheLatestDayWithARegistration()
    {
        await SeedPlain(908, DateTime.Now.Date.AddDays(-9));
        // nothing on -8 or -7
        var target = DateTime.Now.Date.AddDays(-7);

        var result = await _service.ReconcileThrough(new ReconcileThroughRequestModel
        {
            Date = target, SiteIds = new List<int> { 908 }
        });

        Assert.That(result.Model.LandedOnBySiteId[908], Is.EqualTo(DateTime.Now.Date.AddDays(-9)),
            "a seal on a day with no registration would mean nothing");
    }
```

- [ ] **Step 2: Run build to verify it fails**

Run: `cd eFormAPI/Plugins/TimePlanning.Pn && dotnet build TimePlanning.Pn.sln -v q`
Expected: FAIL — `'ITimePlanningPlanningService' does not contain a definition for 'Reconcile'`

- [ ] **Step 3: Add the request/result models**

Create `Infrastructure/Models/Planning/ReconcileThroughRequestModel.cs`:

```csharp
#nullable enable
namespace TimePlanning.Pn.Infrastructure.Models.Planning;

using System;
using System.Collections.Generic;

/// <summary>
/// One date, many workers. The cascade supplies the range, so this never
/// carries a range of its own.
/// </summary>
public class ReconcileThroughRequestModel
{
    public DateTime Date { get; set; }
    public List<int> SiteIds { get; set; } = new();
}
```

Create `Infrastructure/Models/Planning/ReconcileThroughResultModel.cs`:

```csharp
#nullable enable
namespace TimePlanning.Pn.Infrastructure.Models.Planning;

using System;
using System.Collections.Generic;

public class ReconcileThroughResultModel
{
    /// <summary>Where each worker's boundary landed. Per worker, not shared:
    /// the mark falls on that worker's latest day with a registration at or
    /// before the requested date, so a staircase has no single landing date.</summary>
    public Dictionary<int, DateTime> LandedOnBySiteId { get; set; } = new();

    /// <summary>Workers whose boundary actually moved. Excludes no-ops.</summary>
    public int Applied { get; set; }

    /// <summary>Already reconciled at or past the target. Moving them back would
    /// be an unlock, which is deliberately a separate, heavier action.</summary>
    public List<int> SkippedAlreadyFurtherForward { get; set; } = new();

    /// <summary>No registration at or before the target, so there was nothing
    /// to mark. A distinct case from the above — the spec distinguishes them.</summary>
    public List<int> SkippedNoRegistration { get; set; } = new();

    /// <summary>Already marked on exactly the landing day; nothing changed.</summary>
    public List<int> AlreadyReconciledSiteIds { get; set; } = new();
}
```

- [ ] **Step 4: Add the three interface members**

In `ITimePlanningPlanningService.cs`, inside the interface body:

```csharp
    Task<OperationResult> Reconcile(int id);
    Task<OperationResult> Unreconcile(int id);
    Task<OperationDataResult<ReconcileThroughResultModel>> ReconcileThrough(ReconcileThroughRequestModel model);
```

- [ ] **Step 5: Implement the three methods**

Append to `TimePlanningPlanningService.cs`, inside the class:

```csharp
    public async Task<OperationResult> Reconcile(int id)
    {
        try
        {
            await using var db = dbContextHelper.GetDbContext();
            var planning = await db.PlanRegistrations
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (planning == null)
            {
                return new OperationResult(false, localizationService.GetString("PlanningNotFound"));
            }

            // Idempotent: re-reconciling an already reconciled day is a no-op
            // success. Clients retry; that should not read as a failure.
            if (planning.Reconciled)
            {
                return new OperationResult(true, localizationService.GetString("SuccessfullyReconciledDay"));
            }

            if (!DayLockHelper.CanReconcile(planning.Date))
            {
                return new OperationResult(false,
                    localizationService.GetString("CannotReconcileTodayOrFuture"));
            }

            // Without this, reconciling a day BELOW an existing boundary passes
            // CanReconcile, reaches Update, and the interceptor throws into the
            // generic catch -- a 500-shaped "ErrorWhileUpdatingPlanning" instead
            // of a message. That is exactly what Layer 2 exists to prevent.
            var existingBoundary = await DayLockHelper.LockedThroughAsync(db, planning.SdkSitId);
            if (DayLockHelper.IsLocked(existingBoundary, planning.Date))
            {
                return new OperationResult(false,
                    localizationService.GetString("DayIsLockedByReconciledDay"));
            }

            planning.Reconciled = true;
            // DateTime.Now, not UtcNow: the tooltip renders this verbatim as
            // "Afstemt <dato> kl. <tid>", and UTC would read 1-2 hours off in
            // Danish time. Consistent with the CanReconcile comparison.
            planning.ReconciledAt = DateTime.Now;      // I1: written together
            planning.UpdatedByUserId = userService.UserId;
            await planning.Update(db);

            return new OperationResult(true, localizationService.GetString("SuccessfullyReconciledDay"));
        }
        catch (DayLockedException)
        {
            // Expected and routine: a blocked edit is a normal outcome, not an
            // incident. Do not report it to Sentry.
            return new OperationResult(false,
                localizationService.GetString("DayIsLockedByReconciledDay"));
        }
        catch (Exception e)
        {
            SentrySdk.CaptureException(e);
            logger.LogError(e, "TimePlanningPlanningService.Reconcile failed");
            return new OperationResult(false, localizationService.GetString("ErrorWhileUpdatingPlanning"));
        }
    }

    public async Task<OperationResult> Unreconcile(int id)
    {
        try
        {
            await using var db = dbContextHelper.GetDbContext();
            var planning = await db.PlanRegistrations
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (planning == null)
            {
                return new OperationResult(false, localizationService.GetString("PlanningNotFound"));
            }
            // Boundary check FIRST. If the idempotency check came first, a user
            // clicking unlock on a cascade-locked day (Reconciled = false, deep
            // inside the range) would be told "Dagen er låst op" while nothing
            // happened.
            var boundary = await DayLockHelper.LockedThroughAsync(db, planning.SdkSitId);
            if (boundary is null || planning.Date.Date != boundary.Value.Date)
            {
                return new OperationResult(false,
                    localizationService.GetString("OnlyLatestReconciledDayCanBeUnlocked"));
            }

            if (!planning.Reconciled)
            {
                return new OperationResult(true, localizationService.GetString("SuccessfullyUnlockedDay"));
            }

            planning.Reconciled = false;
            planning.ReconciledAt = null;             // I1: cleared together
            planning.UpdatedByUserId = userService.UserId;
            await planning.Update(db);

            return new OperationResult(true, localizationService.GetString("SuccessfullyUnlockedDay"));
        }
        catch (Exception e)
        {
            SentrySdk.CaptureException(e);
            logger.LogError(e, "TimePlanningPlanningService.Unreconcile failed");
            return new OperationResult(false, localizationService.GetString("ErrorWhileUpdatingPlanning"));
        }
    }

    public async Task<OperationDataResult<ReconcileThroughResultModel>> ReconcileThrough(
        ReconcileThroughRequestModel model)
    {
        try
        {
            if (model == null || model.SiteIds.Count == 0)
            {
                return new OperationDataResult<ReconcileThroughResultModel>(false,
                    localizationService.GetString("ErrorWhileUpdatingPlanning"));
            }
            if (!DayLockHelper.CanReconcile(model.Date))
            {
                return new OperationDataResult<ReconcileThroughResultModel>(false,
                    localizationService.GetString("CannotReconcileTodayOrFuture"));
            }

            await using var db = dbContextHelper.GetDbContext();
            // Distinct: a duplicated site id in the request would otherwise be
            // counted twice.
            var siteIds = model.SiteIds.Distinct().ToList();
            var boundaries = await DayLockHelper.LockedThroughForSitesAsync(db, siteIds);
            var result = new ReconcileThroughResultModel();
            var target = model.Date.Date;

            foreach (var siteId in siteIds)
            {
                // Already at or past the target: moving the boundary BACK would
                // be an unlock, which is deliberately a separate, heavier action.
                if (boundaries.TryGetValue(siteId, out var existing)
                    && existing.HasValue && existing.Value.Date >= target)
                {
                    result.SkippedAlreadyFurtherForward.Add(siteId);
                    continue;
                }

                // The mark must land on a day that actually has a registration —
                // a seal on an empty day means nothing.
                var landing = await db.PlanRegistrations
                    .Where(x => x.SdkSitId == siteId)
                    .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                    .Where(x => x.Date <= target)
                    .OrderByDescending(x => x.Date)
                    .FirstOrDefaultAsync();

                if (landing == null)
                {
                    // A different reason from "already further forward", and the
                    // spec distinguishes them -- do not merge the two lists.
                    result.SkippedNoRegistration.Add(siteId);
                    continue;
                }

                if (landing.Reconciled)
                {
                    // Already marked on exactly this day: nothing to do, and it
                    // must not inflate Applied.
                    result.AlreadyReconciledSiteIds.Add(siteId);
                    continue;
                }

                landing.Reconciled = true;
                landing.ReconciledAt = DateTime.Now;   // see Reconcile()
                landing.UpdatedByUserId = userService.UserId;
                await landing.Update(db);

                result.Applied++;
                // Per-worker, because the boundary is a staircase: one shared
                // LandedOn would name the wrong date for most workers.
                result.LandedOnBySiteId[siteId] = landing.Date;
            }

            return new OperationDataResult<ReconcileThroughResultModel>(true, result);
        }
        catch (Exception e)
        {
            SentrySdk.CaptureException(e);
            logger.LogError(e, "TimePlanningPlanningService.ReconcileThrough failed");
            return new OperationDataResult<ReconcileThroughResultModel>(false,
                localizationService.GetString("ErrorWhileUpdatingPlanning"));
        }
    }
```

Add `using TimePlanning.Pn.Infrastructure.Helpers;` if not already present.

- [ ] **Step 6: Add the three routes**

In `Controllers/TimePlanningPlanningController.cs`, matching the file's existing separate-attribute style:

```csharp
    [HttpPut]
    [Route("{id}/reconcile")]
    public async Task<OperationResult> Reconcile(int id)
    {
        return await _planningService.Reconcile(id);
    }

    [HttpPut]
    [Route("{id}/unreconcile")]
    public async Task<OperationResult> Unreconcile(int id)
    {
        return await _planningService.Unreconcile(id);
    }

    [HttpPut]
    [Route("reconcile-through")]
    public async Task<OperationDataResult<ReconcileThroughResultModel>> ReconcileThrough(
        [FromBody] ReconcileThroughRequestModel model)
    {
        return await _planningService.ReconcileThrough(model);
    }
```

- [ ] **Step 7: Register the test class in BOTH shard filters**

Append `|FullyQualifiedName=TimePlanning.Pn.Test.ReconcileServiceTests` to shard `c` in both workflow files.

- [ ] **Step 8: Build and commit**

Run: `cd eFormAPI/Plugins/TimePlanning.Pn && dotnet build TimePlanning.Pn.sln -v q`
Expected: `0 Error(s)`

```bash
git add eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn/Infrastructure/Models/Planning/ReconcileThroughRequestModel.cs \
        eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn/Infrastructure/Models/Planning/ReconcileThroughResultModel.cs \
        eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn/Services/TimePlanningPlanningService/ITimePlanningPlanningService.cs \
        eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn/Services/TimePlanningPlanningService/TimePlanningPlanningService.cs \
        eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn/Controllers/TimePlanningPlanningController.cs \
        eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn.Test/ReconcileServiceTests.cs \
        .github/workflows/dotnet-core-pr.yml .github/workflows/dotnet-core-master.yml
git commit -m "feat(lock): add reconcile, unlock and reconcile-through endpoints"
```

---

## Task 5: Friendly guards and recalculation skipping

**Files:**
- Modify: `.../Services/TimePlanningPlanningService/TimePlanningPlanningService.cs` (`Update` :671 (the null check ends :700), `UpdateByCurrentUserNam` ~:1164, gap-fill loops :370-402 and :593-623)
- Modify: `.../Services/TimePlanningWorkingHoursService/TimePlanningWorkingHoursService.cs` (`CreateUpdate` :454, `UpdateWorkingHour` :1339 and :2012)
- Modify: `.../Infrastructure/Helpers/PlanRegistrationHelper.cs` (`UpdatePlanRegistrationsInPeriod` :394)

**Interfaces:**
- Consumes: `DayLockHelper.*`, resx keys.
- Produces: no new public API.

**Why both layers:** the interceptor guarantees correctness but surfaces as an exception. These guards turn the common, user-triggered cases into a clean message. The recalculation paths get the opposite treatment — they *skip* rather than throw, because a dashboard load legitimately spans the boundary on every single request.

- [ ] **Step 1: Guard `TimePlanningPlanningService.Update`**

Immediately after the `planning == null` check (currently ends :700), insert:

```csharp
            var lockedThrough = await DayLockHelper.LockedThroughAsync(dbContext, planning.SdkSitId);
            if (DayLockHelper.IsLocked(lockedThrough, planning.Date))
            {
                return new OperationResult(false, localizationService.GetString(
                    planning.Reconciled ? "DayIsReconciled" : "DayIsLockedByReconciledDay"));
            }
```

- [ ] **Step 2: Guard `UpdateByCurrentUserNam`**

Same block, immediately after that method's own null/lookup check for the planning row.

- [ ] **Step 3: Guard the three working-hours write paths**

In `TimePlanningWorkingHoursService.CreateUpdate` (:454), after the assigned-site lookup and before the per-day loop writes, reject the whole request if any posted day is locked:

```csharp
            var lockedThrough = await DayLockHelper.LockedThroughAsync(dbContext, model.SiteId);
            if (lockedThrough.HasValue
                && model.Plannings.Any(p => DayLockHelper.IsLocked(lockedThrough, p.Date)))
            {
                return new OperationResult(false,
                    localizationService.GetString("DayIsLockedByReconciledDay"));
            }
```

The two `UpdateWorkingHour` overloads need **different** code — their signatures differ, and neither matches a naive copy:

**Personal overload (`:1339`)** — there is no `sdkSiteId` variable in this method. The site comes from the SDK site looked up at `:1381-1383`, so place the guard after that lookup:

```csharp
            var lockedThrough = await DayLockHelper.LockedThroughAsync(
                dbContext, (int)sdkSite.MicrotingUid!);
            if (DayLockHelper.IsLocked(lockedThrough, model.Date))
            {
                return new OperationResult(false,
                    localizationService.GetString("DayIsLockedByReconciledDay"));
            }
```

**Kiosk overload (`:2012`)** — `sdkSiteId` is a parameter and is **`int?`**. It has no assigned-site lookup near the top; the lookup is duplicated deep inside two branches (`:2308-2310` and `:2591-2593`), so guarding "after the lookup" would protect one branch and leave the other open. Guard at the **top of the method**, from the parameter, before either branch:

```csharp
            if (sdkSiteId.HasValue)
            {
                var lockedThrough = await DayLockHelper.LockedThroughAsync(dbContext, sdkSiteId.Value);
                if (DayLockHelper.IsLocked(lockedThrough, model.Date))
                {
                    return new OperationResult(false,
                        localizationService.GetString("DayIsLockedByReconciledDay"));
                }
            }
```

The kiosk overload currently has **no date guard of any kind** — this is its first. Note neither overload wraps its body in try/catch (only `CreateUpdate` does, at `:456`), so a guard that throws would surface raw.

- [ ] **Step 4: Skip locked days in the bulk recompute**

`UpdatePlanRegistrationsInPeriod` has exactly **one** loop — `foreach (var plan in planningsInPeriod)` at `PlanRegistrationHelper.cs:423`, whose body runs from `:424` to `:1138`. That single body both **writes** (four `Update` calls) and **projects** (`:862` builds the DTO, `:1137` adds it to the row).

**Do NOT change what the loop iterates.** Swapping the source to a filtered list would drop locked days out of the grid entirely, which contradicts spec §6.3 — locked days must still be *displayed*, just never rewritten. That is a silent wrong result, not a build error, so it would ship.

Instead: resolve the boundary **once before** the loop, and guard **only the four writes**.

Before `foreach (var plan in planningsInPeriod)` at `:423`:

```csharp
        // A dashboard load legitimately spans the boundary, so locked days are
        // skipped rather than raising. Without this, the interceptor turns
        // every visit to a closed month into a 500.
        var lockedThrough = await DayLockHelper.LockedThroughAsync(dbContext, dbAssignedSite.SiteId);
```

Immediately after `planRegistration` is materialised at `:425`:

```csharp
            var dayIsLocked = DayLockHelper.IsLocked(lockedThrough, planRegistration.Date);
```

Then wrap **each** of the four `await planRegistration.Update(dbContext)` calls — at `:471`, `:542`, `:810` and `:834` — as:

```csharp
            if (!dayIsLocked)
            {
                await planRegistration.Update(dbContext).ConfigureAwait(false);
            }
```

Everything from `:862` down is untouched, so the projection is preserved by construction.

**Also note `:1109`:** `planningsInPeriod` is *reassigned inside the loop* (a re-query feeding the totals at `:1127-1135`). The already-taken enumerator is unaffected, but do not add any other logic that depends on that collection's identity.

- [ ] **Step 5: Stop gap-fill inside a locked range**

In `TimePlanningPlanningService.Index` (loop at **:370-402**) and `IndexByCurrentUserName` (loop at **:593-623**), skip missing dates that fall inside the lock.

**Use the right context and the right site.** `Index`'s gap-fill writes through `innerDbContext`, created per site at `:284-285` inside the `foreach (var assignedSite in assignedSites)` at `:216` — resolve `lockedThrough` inside that per-site block from `innerDbContext` and `dbAssignedSite.SiteId`, never before the outer loop (that would resolve it for the wrong worker). `IndexByCurrentUserName`'s gap-fill uses `dbContext` and has a single site, so it is simpler.

Then in each loop:

```csharp
                if (DayLockHelper.IsLocked(lockedThrough, missingDate))
                {
                    // Frozen means frozen: a locked period does not grow new rows.
                    continue;
                }
```

with `lockedThrough` resolved once before the loop.

- [ ] **Step 6: Add tests for the skipping behaviour**

Append to `ReconcileServiceTests.cs`:

```csharp
    [Test]
    public async Task Index_OverALockedRange_LeavesLockedRowsByteIdentical()
    {
        var row = await SeedPlain(910, DateTime.Now.Date.AddDays(-5));
        await _service.Reconcile(row.Id);
        var before = await TimePlanningPnDbContext!.PlanRegistrations.AsNoTracking()
            .FirstAsync(x => x.Id == row.Id);

        await using var baseDbContext = GetBaseDbContext();
        var svc = await BuildAdminIndexServiceAsync(baseDbContext);
        await svc.Index(new TimePlanningPlanningRequestModel
        {
            DateFrom = DateTime.Now.Date.AddDays(-10),
            DateTo = DateTime.Now.Date
        });

        var after = await TimePlanningPnDbContext!.PlanRegistrations.AsNoTracking()
            .FirstAsync(x => x.Id == row.Id);
        Assert.Multiple(() =>
        {
            Assert.That(after.Version, Is.EqualTo(before.Version),
                "a no-op re-save still bumps Version — this catches silent rewrites");
            Assert.That(after.UpdatedAt, Is.EqualTo(before.UpdatedAt));
        });
    }

    [Test]
    public async Task Index_OverALockedRange_CreatesNoNewRows()
    {
        var row = await SeedPlain(911, DateTime.Now.Date.AddDays(-5));
        await _service.Reconcile(row.Id);
        var countBefore = await TimePlanningPnDbContext!.PlanRegistrations
            .CountAsync(x => x.SdkSitId == 911);

        await using var baseDbContext = GetBaseDbContext();
        var svc = await BuildAdminIndexServiceAsync(baseDbContext);
        await svc.Index(new TimePlanningPlanningRequestModel
        {
            DateFrom = DateTime.Now.Date.AddDays(-10),
            DateTo = DateTime.Now.Date
        });

        var countAfter = await TimePlanningPnDbContext!.PlanRegistrations
            .CountAsync(x => x.SdkSitId == 911);
        Assert.That(countAfter, Is.EqualTo(countBefore),
            "gap-fill must not materialise rows inside a frozen period");
    }
```

- [ ] **Step 7: Build and commit**

Run: `cd eFormAPI/Plugins/TimePlanning.Pn && dotnet build TimePlanning.Pn.sln -v q`
Expected: `0 Error(s)`

```bash
git add eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn/Services/TimePlanningPlanningService/TimePlanningPlanningService.cs \
        eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn/Services/TimePlanningWorkingHoursService/TimePlanningWorkingHoursService.cs \
        eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn/Infrastructure/Helpers/PlanRegistrationHelper.cs \
        eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn.Test/ReconcileServiceTests.cs
git commit -m "fix(lock): return a message on blocked writes and skip locked days when recalculating"
```

---

## Task 6: Expose the lock state on the read model

**Files:**
- Modify: `.../Infrastructure/Models/Planning/TimePlanningPlanningPrDayModel.cs:235-237`
- Modify: `.../Infrastructure/Models/Planning/TimePlanningPlanningModel.cs:78`
- Modify: `.../Infrastructure/Helpers/PlanRegistrationHelper.cs:862` (projection) and where `siteModel` is built
- Test: append to `ReconcileServiceTests.cs`

**Interfaces:**
- Produces (consumed by Tasks 7-11):
  - `TimePlanningPlanningPrDayModel.Reconciled : bool`
  - `TimePlanningPlanningPrDayModel.ReconciledAt : DateTime?`
  - `TimePlanningPlanningModel.LockedThrough : DateTime?`

- [ ] **Step 1: Add the per-day fields**

In `TimePlanningPlanningPrDayModel.cs`, after `NettoHoursOverrideActive` (:236):

```csharp
    /// <summary>True only on the boundary day itself. Earlier days are locked
    /// by derivation and keep this false — see LockedThrough on the row.</summary>
    public bool Reconciled { get; set; }

    public DateTime? ReconciledAt { get; set; }
```

- [ ] **Step 2: Add the row field**

In `TimePlanningPlanningModel.cs`, before `PlanningPrDayModels` (:78):

```csharp
    /// <summary>The worker's reconciled boundary: every day at or before this is
    /// locked. Null when nothing is reconciled. Sent once per row so the client
    /// compares dates instead of scanning cells.</summary>
    public DateTime? LockedThrough { get; set; }
```

- [ ] **Step 3: Populate them**

In `PlanRegistrationHelper.cs` at the `planningModel` initializer (:862), add:

```csharp
                Reconciled = planRegistration.Reconciled,
                ReconciledAt = planRegistration.ReconciledAt,
```

And set the row-level value **once, BEFORE the loop at `:423`** — reuse the `lockedThrough` local that Task 5 Step 4 already resolves there:

```csharp
        siteModel.LockedThrough = lockedThrough;
```

**Not** alongside the totals at `:1131-1135`: those are inside the loop, so that would re-query once per day per site, and — worse — a worker with **no rows in the window never enters the loop at all**, leaving `LockedThrough` null. A worker whose entire visible month is locked would then render as fully editable.

- [ ] **Step 4: Test the projection**

```csharp
    [Test]
    public async Task Index_ProjectsReconciledStateOntoTheReadModel()
    {
        var row = await SeedPlain(912, DateTime.Now.Date.AddDays(-4));
        await _service.Reconcile(row.Id);

        await using var baseDbContext = GetBaseDbContext();
        var svc = await BuildAdminIndexServiceAsync(baseDbContext);
        var result = await svc.Index(new TimePlanningPlanningRequestModel
        {
            DateFrom = DateTime.Now.Date.AddDays(-6),
            DateTo = DateTime.Now.Date
        });

        var siteRow = result.Model.Single(x => x.SiteId == 912);
        var day = siteRow.PlanningPrDayModels.Single(d => d.Date.Date == row.Date.Date);
        Assert.Multiple(() =>
        {
            Assert.That(siteRow.LockedThrough, Is.EqualTo(row.Date),
                "the client needs the boundary once per row, not per cell");
            Assert.That(day.Reconciled, Is.True);
            Assert.That(day.ReconciledAt, Is.Not.Null);
        });
    }
```

- [ ] **Step 5: Build and commit**

```bash
git add eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn/Infrastructure/Models/Planning/ \
        eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn/Infrastructure/Helpers/PlanRegistrationHelper.cs \
        eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn.Test/ReconcileServiceTests.cs
git commit -m "feat(lock): send the reconciled state and boundary to the client"
```

**→ Natural phase boundary. The backend is complete and enforceable here. Open a PR, watch CI green, and merge before starting Task 7 if you want to land this incrementally.**

---

## Task 7: The interceptor in the service repo

**Files (different repository):** `/home/rene/Documents/workspace/microting/eform-service-timeplanning-plugin`
- Create: `ServiceTimePlanningPlugin/Infrastructure/Interceptors/ReconciledDayLockInterceptor.cs` (copy of Task 2's, namespace changed)
- Create: `ServiceTimePlanningPlugin/Infrastructure/Helpers/DayLockHelper.cs` (copy of Task 1's, namespace changed)
- Modify: `ServiceTimePlanningPlugin/Infrastructure/Helpers/DbContextHelper.cs:39-44`

**Why duplicated rather than shared:** the two repos share no code except the base NuGet, and this design adds nothing to that package. Duplicating ~90 lines is the smaller cost. **A divergence between the copies is a lock with a hole**, so both files carry a header comment naming their twin.

- [ ] **Step 1: Copy the helper and interceptor**

Copy both files, change `namespace TimePlanning.Pn.Infrastructure.*` to `namespace ServiceTimePlanningPlugin.Infrastructure.*`, and add at the top of each:

```csharp
// NOTE: this is a deliberate copy of the same file in
// eform-angular-timeplanning-plugin (TimePlanning.Pn/Infrastructure/...).
// The two repos share only the base NuGet package, which this design does not
// modify. If you change one, change the other: a divergence means background
// jobs can write days the web refuses to, which is worse than no lock at all.
```

- [ ] **Step 2: Wire it into the service helper**

Replace `DbContextHelper.GetDbContext()`:

```csharp
    public TimePlanningPnDbContext GetDbContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<TimePlanningPnDbContext>();

        // Hardcoded version, exactly as TimePlanningPnContextFactory does
        // (factory line 39). ServerVersion.AutoDetect OPENS A CONNECTION and
        // runs a version query; this method is called once per assigned site on
        // every dashboard load, so AutoDetect here would add a round-trip per
        // worker per page view that the current code does not pay.
        optionsBuilder.UseMySql(
            ConnectionString,
            new MariaDbServerVersion(new Version(10, 5, 0)),
            mySqlOptionsAction: builder => { builder.EnableRetryOnFailure(); });

        optionsBuilder.AddInterceptors(new ReconciledDayLockInterceptor());

        return new TimePlanningPnDbContext(optionsBuilder.Options);
    }
```

This covers `eFormCompletedHandler`, `SearchListJob` (including its nightly soft-**delete** path), `FlexChainCatchUpJob` and `Core.cs:257`.

**One documented exception.** `Core.cs:146-148` builds a context straight from
`TimePlanningPnContextFactory`, bypassing this helper, so it is **not** guarded. That
context is read-only in practice — migrations plus reading `PluginConfigurationValues`
at `:170`/`:174` — and writes no `PlanRegistration`. Leave it, but add a comment at
`:146` saying so, because the spec's own risk table calls an unguarded construction
site "a lock with a hole … worse than no lock", and the next reader deserves to know
this one was considered rather than missed.

- [ ] **Step 3: Add the enforcement test this repo requires**

Spec §10 requires *"an explicit test in each repo asserting a locked write is rejected
through that repo's own context construction"* — otherwise a divergence between the two
copies is invisible. The service repo has `ServiceTimePlanningPlugin.Integration.Test`
(NUnit 4.6.1 + Testcontainers MariaDb) and, unlike the plugin repo, **no shard
allowlist**, so a new class runs automatically.

Create `ServiceTimePlanningPlugin.Integration.Test/DayLockInterceptorTests.cs` with one
test proving the wiring — build the context through `DbContextHelper.GetDbContext()`,
not by hand, or it proves nothing:

```csharp
    [Test]
    public async Task AWriteToALockedDay_IsRejected_ThroughThisReposOwnHelper()
    {
        var helper = new DbContextHelper(ConnectionString);
        await using var db = helper.GetDbContext();

        var earlier = new PlanRegistration
        {
            SdkSitId = 950, Date = new DateTime(2026, 1, 13),
            PlanText = "", CommentOffice = "", CommentOfficeAll = "",
            WorkflowState = Constants.WorkflowStates.Created,
            CreatedByUserId = 1, UpdatedByUserId = 1,
        };
        await earlier.Create(db);

        await new PlanRegistration
        {
            SdkSitId = 950, Date = new DateTime(2026, 1, 16),
            Reconciled = true, ReconciledAt = DateTime.Now,
            PlanText = "", CommentOffice = "", CommentOfficeAll = "",
            WorkflowState = Constants.WorkflowStates.Created,
            CreatedByUserId = 1, UpdatedByUserId = 1,
        }.Create(db);

        earlier.PlanHours = 9;

        Assert.ThrowsAsync<DayLockedException>(async () => await earlier.Update(db),
            "background jobs must be as locked out as the web is -- if this fails, the "
            + "two copies of the interceptor have diverged");
    }
```

- [ ] **Step 4: Build and commit (in the service repo)**

```bash
cd /home/rene/Documents/workspace/microting/eform-service-timeplanning-plugin
dotnet build -v q
git add ServiceTimePlanningPlugin/Infrastructure/
git commit -m "feat(lock): enforce the reconciled-day lock in background jobs"
```

---

## Task 8: Frontend models and service methods

**Files:**
- Modify: `eform-client/src/app/plugins/modules/time-planning-pn/models/plannings/planning-pr-day.model.ts`
- Modify: `.../models/plannings/time-planning.model.ts`
- Modify: `.../services/time-planning-pn-plannings.service.ts`

**Interfaces:**
- Consumes: the three DTO fields from Task 6.
- Produces:
  - `PlanningPrDayModel.reconciled: boolean`, `.reconciledAt: string | null`
  - `TimePlanningModel.lockedThrough: string | null`
  - `TimePlanningPnPlanningsService.reconcileDay(id: number)`, `.unreconcileDay(id: number)`, `.reconcileThrough(date: string, siteIds: number[])`

- [ ] **Step 1: Add the model fields**

In `planning-pr-day.model.ts`, before the closing brace:

```ts
  /** True only on the boundary day. Earlier days are locked by derivation. */
  reconciled: boolean;
  reconciledAt: string | null;
```

In `time-planning.model.ts`, before the closing brace:

```ts
  /** Every day at or before this is locked for this worker. Null = nothing locked. */
  lockedThrough: string | null;
```

- [ ] **Step 2: Add the service methods**

In `time-planning-pn-plannings.service.ts`, inside the class:

```ts
  reconcileDay(id: number): Observable<OperationResult> {
    return this.apiBaseService.put(
      TimePlanningPnPlanningsMethods.Plannings + '/' + id + '/reconcile', {}
    );
  }

  unreconcileDay(id: number): Observable<OperationResult> {
    return this.apiBaseService.put(
      TimePlanningPnPlanningsMethods.Plannings + '/' + id + '/unreconcile', {}
    );
  }

  reconcileThrough(
    date: string, siteIds: number[]
  ): Observable<OperationDataResult<ReconcileThroughResultModel>> {
    return this.apiBaseService.put(
      TimePlanningPnPlanningsMethods.Plannings + '/reconcile-through',
      {date, siteIds}
    );
  }
```

Create `models/plannings/reconcile-through-result.model.ts`:

```ts
export class ReconcileThroughResultModel {
  landedOn: string | null;
  applied: number;
  skippedSiteIds: number[];
}
```

and export it from `models/plannings/index.ts`.

- [ ] **Step 3: Commit**

```bash
git add eform-client/src/app/plugins/modules/time-planning-pn/models/ \
        eform-client/src/app/plugins/modules/time-planning-pn/services/time-planning-pn-plannings.service.ts
git commit -m "feat(lock): add the lock fields and reconcile calls to the client"
```

---

## Task 9: Three states in the grid

**Files:**
- Modify: `.../time-plannings-table/time-plannings-table.component.ts` (`getCellClass` :201-245, `onDayColumnClick` :458-485)

**Interfaces:**
- Consumes: `TimePlanningModel.lockedThrough`, `PlanningPrDayModel.reconciled` (Task 8); CSS classes from Task 10.
- Produces: `isDayLocked(row, field) -> boolean`, `isDayReconciled(row, field) -> boolean`.

**The signature problem:** `getCellClass` returns a **single** string today and mtx-grid stamps it onto the `<td>`. Three lock states layer *on top of* the existing four backgrounds, so it must return composed classes.

- [ ] **Step 1: Add the predicates and compose the classes**

Add to the class:

```ts
  /** The worker's boundary, parsed once per call. Null when nothing is locked. */
  private lockedThrough(row: any): number | null {
    return row?.lockedThrough ? new Date(row.lockedThrough).setHours(0, 0, 0, 0) : null;
  }

  isDayLocked(row: any, field: string): boolean {
    const boundary = this.lockedThrough(row);
    const date = row?.planningPrDayModels?.[field]?.date;
    if (boundary === null || !date) {
      return false;
    }
    return new Date(date).setHours(0, 0, 0, 0) <= boundary;
  }

  isDayReconciled(row: any, field: string): boolean {
    return row?.planningPrDayModels?.[field]?.reconciled === true;
  }
```

Then change `getCellClass` to append a lock class to whatever it already returns. Replace the method's `return` statements with a single composed exit by wrapping the existing body:

```ts
  getCellClass(row: any, field: string): string {
    const base = this.getCellStateClass(row, field);
    if (this.isDayReconciled(row, field)) {
      return `${base} reconciled-background`;
    }
    if (this.isDayLocked(row, field)) {
      return `${base} locked-background`;
    }
    return base;
  }
```

and rename the existing `getCellClass` body (lines 201-245) to `private getCellStateClass(row: any, field: string): string` with no other change. The four existing background classes keep working untouched.

- [ ] **Step 2: Open locked days read-only**

In `onDayColumnClick` (:458), pass the lock state into the dialog so it can render read-only — the dialog still **opens**, because people read closed days constantly:

```ts
        this.dialog.open(WorkdayEntityDialogComponent, {
          data: {
            planningPrDayModels: cellData,
            assignedSiteModel: result.model,
            tags: row.tags ?? [],
            isLocked: this.isDayLocked(row, field),
            isReconciled: this.isDayReconciled(row, field),
            lockedThrough: row.lockedThrough ?? null,
          },
          ...
```

- [ ] **Step 3: Commit**

```bash
git add eform-client/src/app/plugins/modules/time-planning-pn/components/plannings/time-plannings-table/time-plannings-table.component.ts
git commit -m "feat(lock): mark locked and reconciled days in the grid"
```

---

## Task 10: The cell styles (HOST REPO — separate PR)

**Files (different repository):** `/home/rene/Documents/workspace/microting/eform-angular-frontend`
- Modify: `eform-client/src/scss/styles.scss` (append after the `.red-background .plan-container` block ending :358)

Per CLAUDE.md, **all SCSS lives in `eform-angular-frontend`** — this is a second repo, a second branch and a second PR. The four existing `*-background` classes live here (`:196`, `:239`, `:280`, `:320`) and the new ones must sit beside them.

- [ ] **Step 1: Add the two state classes**

Mirroring the exact selector shape of the existing four (bare class, then a `.plan-container` descendant, `!important` throughout, `var(--token, #fallback)`):

```scss
/* Reconciled / locked day cells.
   Four independent channels carry the state — texture, glyph, cursor and the
   tooltip text — so colour is never load-bearing. The 3px right border on a
   reconciled cell is what draws the boundary line down the grid.
   Theme-agnostic on purpose: body.theme-eform rules do not apply under
   theme-workspace, and these must read on both. */
:root {
  --tp-locked-bg: #E4E7E4;
  --tp-locked-hatch: rgba(22, 33, 30, 0.055);
  --tp-seal-ink: #2F5D50;
}

@media (prefers-color-scheme: dark) {
  :root:not([data-theme="light"]) {
    --tp-locked-bg: #262B29;
    --tp-locked-hatch: rgba(230, 234, 232, 0.06);
    --tp-seal-ink: #7FD1B9;
  }
}

.locked-background .plan-container {
  background: repeating-linear-gradient(135deg,
      var(--tp-locked-hatch) 0 2px, transparent 2px 6px),
      var(--tp-locked-bg) !important;
  /* beats the shared rule `.plan-container, .progress-container` (selector at
     styles.scss:360, declaration :361). Scoping by .locked-background means the
     avatar/progress circle is unaffected. */
  cursor: not-allowed !important;
}

.locked-background .plan-content {
  opacity: 0.72;
}

.reconciled-background .plan-container {
  background: var(--tp-locked-bg) !important;
  border-right: 3px solid var(--tp-seal-ink) !important;
  cursor: default !important;
}
```

- [ ] **Step 2: Commit in the host repo**

```bash
cd /home/rene/Documents/workspace/microting/eform-angular-frontend
git checkout -b feat/reconciled-day-lock-styles
git add eform-client/src/scss/styles.scss
git commit -m "feat(timeplanning): add locked and reconciled day-cell styles"
```

Open a PR against that repo's target branch. Note from project memory: three FOSSA checks fail on every `eform-angular-frontend` PR and are non-gating.

---

## Task 11: Read-only dialog, reconcile and unlock

**Files:**
- Modify: `.../workday-entity/workday-entity-dialog.component.ts` (data type :50-54, `isInTheFuture` assignment :249, the `updateDisabledStates()` cascade :865-1075)
- Modify: `.../workday-entity/workday-entity-dialog.component.html` (hint ~:461, actions :494-514)

**Interfaces:**
- Consumes: the dialog data fields added in Task 9; service methods from Task 8.
- Produces: nothing downstream.

**The trap — and check the premise, because a wrong one invites skipping the step.** `updateDisabledStates()` (`:865-1075`) makes 74 `setDisabled` calls, 38 of them enabling. Each enable *is* value-guarded (`if (isSet(p1Start))`, `if (thirdShiftActive)`…). What none of them is guarded by is **`isInTheFuture`** — so the cascade already silently re-enables controls the constructor disabled for future dates. That is a pre-existing bug, and a day-lock implemented only at construction inherits it: one keystroke reopens a locked form. Guarding inside `setDisabled` fixes both at once.

- [ ] **Step 1: Accept the new data fields**

Extend the injected data type at `:50-54`:

```ts
  public data = inject<{
      planningPrDayModels: PlanningPrDayModel,
      assignedSiteModel: AssignedSiteModel,
      tags?: SharedTagModel[],
      isLocked?: boolean,
      isReconciled?: boolean,
      lockedThrough?: string | null
    }>(MAT_DIALOG_DATA);
```

Add a field and set it beside the `isInTheFuture` assignment at `:249`:

```ts
  /** A reconciled or cascade-locked day opens read-only. */
  isLocked = false;
```

```ts
    this.isLocked = this.data.isLocked === true;
```

- [ ] **Step 2: Make the cascade honour the lock**

This is the load-bearing change. Modify `setDisabled` itself (`:481-492`; note `getCtrl` already exists at `:477-479` and `setDisabled` already calls it — you are changing one line inside an existing method, not introducing either) so no caller can re-enable a control on a locked day:

```ts
  private setDisabled(path: string, disabled: boolean) {
    const c = this.getCtrl(path);
    if (!c) {
      return;
    }
    // A locked day can never re-enable a control. The progressive-enable
    // cascade calls setDisabled(path, false) unconditionally in many branches;
    // without this guard, touching any field would reopen the form.
    const effective = disabled || this.isLocked;
    if (effective && c.enabled) {
      c.disable({emitEvent: false});
    }
    if (!effective && c.disabled) {
      c.enable({emitEvent: false});
    }
  }
```

Then disable the whole form once, after the form is built:

```ts
    if (this.isLocked) {
      this.workdayForm.disable({emitEvent: false});
    }
```

- [ ] **Step 3: Add the banner**

In the .html, beside the existing future hint (`:461-465`):

```html
          <tp-help-hint
            *ngIf="isLocked"
            [helpId]="data.isReconciled ? 'dayCell.reconciled' : 'dayCell.lockedByReconciled'"
            [attr.data-tp-help]="data.isReconciled ? 'dayCell.reconciled' : 'dayCell.lockedByReconciled'"
            tone="warn"></tp-help-hint>
```

- [ ] **Step 4: Replace the actions on a locked day**

In `mat-dialog-actions` (:494-514), hide Save and offer the reverse action. Note `[mat-dialog-close]="data"` fires regardless of `(click)`, so Save must be removed from the DOM, not merely disabled:

```html
    <button
      *ngIf="!isLocked"
      class="btn-primary btn-primary--icon-left"
      id="saveButton"
      data-tp-help="dayCell.save"
      (click)="onUpdateWorkDayEntity()"
      [disabled]="workdayForm.invalid"
      [mat-dialog-close]="data"
    >
      <span>{{ 'Save' | translate }}</span>
    </button>

    <button
      *ngIf="!isLocked && canReconcile"
      class="btn-secondary"
      id="reconcileButton"
      (click)="onReconcile()">
      {{ 'Reconcile day' | translate }}
    </button>

    <button
      *ngIf="isLocked && data.isReconciled"
      class="btn-secondary"
      id="unlockButton"
      (click)="onUnlock()">
      {{ 'Unlock' | translate }}
    </button>
```

- [ ] **Step 5: Add the handlers**

```ts
  /** I2: today and future days stay open so time can still be registered. */
  get canReconcile(): boolean {
    const d = new Date(this.data.planningPrDayModels.date);
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    d.setHours(0, 0, 0, 0);
    return d < today;
  }

  onReconcile(): void {
    this.planningsService.reconcileDay(this.data.planningPrDayModels.id)
      .subscribe(result => {
        if (result && result.success) {
          this.dialogRef.close(this.data);
        }
      });
  }

  onUnlock(): void {
    this.planningsService.unreconcileDay(this.data.planningPrDayModels.id)
      .subscribe(result => {
        if (result && result.success) {
          this.dialogRef.close(this.data);
        }
      });
  }
```

- [ ] **Step 6: Commit**

```bash
git add eform-client/src/app/plugins/modules/time-planning-pn/components/plannings/time-planning-actions/workday-entity/
git commit -m "feat(lock): open locked days read-only, with reconcile and unlock"
```

---

## Task 12: Bulk reconcile in the toolbar

**Files:**
- Modify: `.../time-plannings-table/time-plannings-table.component.{ts,html}` (grid selection)
- Modify: `.../time-plannings-container/time-plannings-container.component.{ts,html}` (toolbar + scope bar)

**Interfaces:**
- Consumes: `reconcileThrough` (Task 8).
- Produces: nothing downstream.

**Two mtx-grid gotchas, both documented in the host's backend-configuration task-list and both guaranteed to bite here:**
1. mtx-grid binds `(click)="_selectRow()"` on the `<tr>`. With `[rowSelectable]`, clicking a **day cell** clears the batch selection. The cell must call `stopRowClick($event)`.
2. mtx-grid rebuilds its internal `SelectionModel` empty in `ngOnChanges` **without emitting** `rowSelectedChange`, so the component must re-emit an empty selection itself.

- [ ] **Step 1: Enable row selection**

In the table .html, on `<mtx-grid>` (:19-30):

```html
  [rowSelectable]="true"
  [multiSelectable]="true"
  (rowSelectedChange)="onRowSelected($event)"
```

In the table .ts:

```ts
  @Output() selectionChanged: EventEmitter<number[]> = new EventEmitter<number[]>();

  onRowSelected(rows: any[]): void {
    this.selectionChanged.emit((rows ?? []).map(r => r.siteId));
  }

  /** mtx-grid selects the row on any click inside it; the day cell must not. */
  stopRowClick(event: Event): void {
    event.stopPropagation();
  }
```

In the day-cell template (:238), add `(click)="stopRowClick($event)"` to the `.plan-container` **before** the existing handler so the row-select is suppressed but the dialog still opens:

```html
  <div class="plan-container" data-tp-help="grid.openDay"
       (click)="stopRowClick($event); onDayColumnClick(row, col.field)"
       id="cell{{index}}_{{col.field}}">
```

And re-emit an empty selection in `ngOnChanges` when `timePlannings` changes, because the grid will not:

```ts
    if (changes.timePlannings) {
      this.selectionChanged.emit([]);
    }
```

- [ ] **Step 2: Add the toolbar control**

In the container .html, after the reload button (`:100-108`):

```html
      <input
        class="tp-reconcile-date"
        id="reconcileThroughDate"
        type="date"
        [max]="maxReconcileDate"
        [(ngModel)]="reconcileThroughDate">
      <button
        class="btn-secondary btn-secondary--icon-rounded-border"
        id="reconcileThrough"
        [disabled]="!reconcileThroughDate"
        [matTooltip]="'Reconcile through' | translate"
        (click)="onReconcileThrough()">
        <mat-icon>lock</mat-icon>
      </button>
```

- [ ] **Step 3: Add the container logic**

```ts
  selectedSiteIds: number[] = [];
  reconcileThroughDate: string | null = null;

  /** I2: nothing at or after today may be reconciled. */
  get maxReconcileDate(): string {
    const d = new Date();
    d.setDate(d.getDate() - 1);
    return d.toISOString().slice(0, 10);
  }

  onSelectionChanged(siteIds: number[]): void {
    this.selectedSiteIds = siteIds;
  }

  onReconcileThrough(): void {
    if (!this.reconcileThroughDate) {
      return;
    }
    // No selection means every worker currently visible under the active
    // filters — the month-end case.
    const siteIds = this.selectedSiteIds.length
      ? this.selectedSiteIds
      : this.timePlannings.map(x => x.siteId);

    this.planningsService.reconcileThrough(this.reconcileThroughDate, siteIds)
      .subscribe(result => {
        if (result && result.success) {
          this.reconcileThroughDate = null;
          this.selectedSiteIds = [];
          this.getPlannings();
        }
      });
  }
```

Bind the table's output in the container template: `(selectionChanged)="onSelectionChanged($event)"`.

- [ ] **Step 4: Commit**

```bash
git add eform-client/src/app/plugins/modules/time-planning-pn/components/plannings/
git commit -m "feat(lock): reconcile many workers through a date from the toolbar"
```

---

## Task 13: Help entries and translations

**Files:**
- Modify: `.../help/help.model.ts` (the `HELP_IDS` array)
- Modify: `.../help/planning-help.registry.ts`
- Modify: `.../help/i18n/da.ts` and `.../help/i18n/enUS.ts`
- Modify: `.../i18n/da.ts` and `.../i18n/enUS.ts`

`HelpEntryId` is a closed union off `HELP_IDS`, and prose is `Record<HelpEntryId, HelpProse>` — adding an id **forces** both help i18n files to gain the entry or the build fails.

- [ ] **Step 1: Add the ids**

In `help/help.model.ts`, add to `HELP_IDS` in the day-cell block: `'dayCell.reconciled'`, `'dayCell.lockedByReconciled'`, and in the toolbar block: `'toolbar.reconcileThrough'`.

- [ ] **Step 2: Register the entries**

In `help/planning-help.registry.ts`, in the toolbar block (after `:44`):

```ts
  { id: 'toolbar.reconcileThrough', kind: 'control', section: 'toolbar', anchor: 'toolbar.reconcileThrough' },
```

in the day-cell block:

```ts
  { id: 'dayCell.reconciled', kind: 'control', section: 'dayCell', anchor: 'dayCell.reconciled' },
  { id: 'dayCell.lockedByReconciled', kind: 'control', section: 'dayCell', anchor: 'dayCell.lockedByReconciled' },
```

Do **not** assign a `tour` step — page tour steps 1-8 are taken and the tour order is deliberate.

- [ ] **Step 3: Add the prose (Danish)**

In `help/i18n/da.ts`:

```ts
  'dayCell.reconciled': {
    title: 'Afstemt',
    short: 'Dagens tal er endelige.',
    detail: 'Dagen er afstemt, og tallene ændres ikke længere — heller ikke af en efterberegning. Alle dage før denne er samtidig låst.',
    keywords: ['afstemt', 'låst', 'endelig', 'afslutning'],
  },
  'dayCell.lockedByReconciled': {
    title: 'Låst',
    short: 'Dagen ligger før en afstemt dag.',
    detail: 'Dagen kan ikke ændres, fordi en senere dag er afstemt. Lås den seneste afstemte dag op først.',
    keywords: ['låst', 'afstemt', 'tidligere'],
  },
  'toolbar.reconcileThrough': {
    title: 'Afstem til og med',
    short: 'Sæt grænsen for flere medarbejdere på én gang.',
    detail: 'Vælg en dato. Er ingen rækker markeret, gælder den alle synlige medarbejdere. Kun grænsedagen markeres som afstemt — alt før låses automatisk.',
    keywords: ['afstem', 'flere', 'månedsafslutning', 'lås'],
  },
```

- [ ] **Step 4: Add the prose (English)**

Same three keys in `help/i18n/enUS.ts` with English values matching the copy rule — describe what the day *is*, never who may change it.

- [ ] **Step 5: Add the UI strings**

In `i18n/da.ts`, before the closing `};`:

```ts
  'Reconcile day': 'Afstem dag',
  'Reconcile through': 'Afstem til og med',
  Unlock: 'Lås op',
  Reconciled: 'Afstemt',
  Locked: 'Låst',
```

In `i18n/enUS.ts`, the same keys with identity values.

- [ ] **Step 6: Commit**

```bash
git add eform-client/src/app/plugins/modules/time-planning-pn/help/ \
        eform-client/src/app/plugins/modules/time-planning-pn/i18n/
git commit -m "feat(lock): add help entries and translations for the day lock"
```

---

## Task 14: End-to-end test

**Files:**
- Create: `eform-client/playwright/e2e/plugins/time-planning-pn/s/reconcile-day-lock.spec.ts`
- Create: `eform-client/playwright/e2e/plugins/time-planning-pn/s/` seed files (copy `a/`'s)
- Modify: `.github/workflows/dotnet-core-pr.yml:95` and `dotnet-core-master.yml:102` matrix — add `s`

**Anchor rows by worker identity, never by grid index.** The `#cellN_M` ids are positional; a row shift silently addresses a different worker. Read the worker from the dialog title and assert it stays constant, as `e1m/dashboard-edit-multishift.spec.ts` now does.

- [ ] **Step 1: Write the spec**

```ts
import { test, expect, Page } from '@playwright/test';
import { LoginPage } from '../../../Page objects/Login.page';

async function waitForSpinner(page: Page) {
  if (await page.locator('.overlay-spinner').count() > 0) {
    await page.locator('.overlay-spinner').waitFor({ state: 'hidden', timeout: 30000 });
  }
}

async function dialogWorker(page: Page): Promise<string> {
  const title = page.locator('mat-dialog-container [mat-dialog-title]');
  await expect(title).toBeVisible({ timeout: 10000 });
  const raw = (await title.innerText()).replace(/\s+/g, ' ').trim();
  return raw.split(/\s+-\s+/)[0].trim();
}

test.describe('Reconciled day lock', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('http://localhost:4200');
    await new LoginPage(page).login();
  });

  test('reconciling a day locks it and every earlier day for that worker', async ({ page }) => {
    await page.locator('mat-nested-tree-node').filter({ hasText: 'Timeregistrering' }).click();
    const indexPromise = page.waitForResponse(r =>
      r.url().includes('/api/time-planning-pn/plannings/index') && r.request().method() === 'POST');
    await page.locator('mat-tree-node').filter({ hasText: 'Dashboard' }).click();
    await indexPromise;
    await waitForSpinner(page);

    // Open a past day and reconcile it.
    await page.locator('#cell3_1').click();
    const worker = await dialogWorker(page);
    const reconcilePromise = page.waitForResponse(r =>
      r.url().includes('/reconcile') && r.request().method() === 'PUT');
    await page.locator('#reconcileButton').click();
    await reconcilePromise;
    await waitForSpinner(page);

    // The cell now carries the boundary treatment.
    await expect(page.locator('#cell3_1').locator('..'))
      .toHaveClass(/reconciled-background/);

    // The day before it is locked, but NOT reconciled.
    await expect(page.locator('#cell3_0').locator('..'))
      .toHaveClass(/locked-background/);

    // Opening the locked day gives a read-only dialog for the same worker.
    await page.locator('#cell3_0').click();
    expect(await dialogWorker(page)).toBe(worker);
    await expect(page.locator('#saveButton')).toHaveCount(0);
    await expect(page.locator('#unlockButton')).toHaveCount(0);
    await page.locator('#cancelButton').click();

    // The boundary day offers unlock; using it frees both days.
    await page.locator('#cell3_1').click();
    expect(await dialogWorker(page)).toBe(worker);
    const unlockPromise = page.waitForResponse(r =>
      r.url().includes('/unreconcile') && r.request().method() === 'PUT');
    await page.locator('#unlockButton').click();
    await unlockPromise;
    await waitForSpinner(page);

    await expect(page.locator('#cell3_0').locator('..'))
      .not.toHaveClass(/locked-background/);
  });
});
```

- [ ] **Step 2: Add the shard**

Add `s` to the `matrix.test` array in `.github/workflows/dotnet-core-pr.yml:95` and `dotnet-core-master.yml:102`.

**Do not copy seed SQL.** The seed step (`dotnet-core-pr.yml:156-171`) falls back to `a/`'s dumps when a shard has none — `b`, `c`, `p`, `q` and `r` already rely on that, and copying the 19 MB `420_SDK.sql` plus the 3.2 MB plugin dump would be 22 MB for nothing. What *is* required: the `s/` directory must contain at least one spec, because the test step runs `npx playwright test .../${{ matrix.test }}/` and exits non-zero when it finds none.

- [ ] **Step 3: Commit**

```bash
git add eform-client/playwright/e2e/plugins/time-planning-pn/s/ \
        .github/workflows/dotnet-core-pr.yml .github/workflows/dotnet-core-master.yml
git commit -m "test(lock): end-to-end reconcile, cascade and unlock"
```

---

## Task 15: Open the PR and watch CI

- [ ] **Step 1: Dual review gate (MANDATORY before the PR)**

Dispatch `superpowers:requesting-code-review` **and** a `code-simplifier` subagent in parallel. Act on the findings; do not merge around them.

- [ ] **Step 2: Push and open the PR**

```bash
git push -u origin feat/reconciled-day-lock
gh pr create --base stable --title "feat(lock): reconciled ('Afstemt') day lock" --body "<see spec>"
```

- [ ] **Step 3: Watch CI to a verdict**

`gh pr checks <n>` until every check has finished. For each red check, find the failed step and classify it as infrastructure or a real failure. Compare against `stable`'s latest run — a shard also red there is not yours.

Two known flake sources in this repo, neither caused by this change:
- `b/activate-plugin.spec.ts:30` has a hardcoded `waitForTimeout(100000)` inside a 180s budget; it runs in setup for every `1m` shard.
- MariaDB container start fails intermittently. If a job dies at "Start MariaDB" with every test step skipped, that shard ran nothing — re-run it.

Re-running a failed job **overwrites** its conclusion, so a green run can hide a first-attempt failure. To audit honestly: `gh api repos/<o>/<r>/actions/runs/<id>/attempts/1/jobs`.

- [ ] **Step 4: Report the verdict, do not merge without approval**

---

## Self-Review

**Spec coverage.** §4 data model → Task 1. §4.2 I1 → Tasks 4 (write) and 1 (test). I2 → Tasks 1, 4, 12. I3 → Task 2. §4.3 query cost → Task 1 (`LockedThroughForSitesAsync`). §5 write inventory → Tasks 2, 5, 7. §6.1 three layers → Tasks 2 (L1), 5 (L2, L3). §6.2 cascades → Global Constraints + Task 1 doc comment. §6.3 gap-fill → Task 5 Step 5. §6.4 timezone → Task 1 `CanReconcile`. §7 API → Task 4; read model → Task 6. §8.1 three states → Tasks 9, 10. §8.2 single day → Task 11. §8.3 bulk → Tasks 4 (`ReconcileThrough`), 12. §8.4 unlock → Tasks 4, 11. §8.5 blocked feedback → Task 11. §8.6 permissions → first-user-only, enforced in the service layer, not a controller role (see spec §8.6; verified by `ReconcileServiceTests`'s first-user behaviour tests). §9 testing → Tasks 1, 2, 4, 5, 6, 14.

**Gaps found and closed while reviewing:**
- The interceptor must permit the unlock write on the boundary day, or unlocking would be blocked by the lock it removes. Added `IsUnlockOfBoundaryDay` (Task 2 Step 3) and a test for it.
- `TestBaseSetup` builds its own contexts and would bypass the interceptor — the lock would appear tested while being unenforced. Added Task 2 Step 6.
- `[mat-dialog-close]="data"` fires regardless of `(click)`, so a disabled Save still closes with data. Save is removed from the DOM on a locked day, not disabled (Task 11 Step 4).

**Type consistency.** `lockedThrough` is `DateTime?` in C# and `string | null` in TS (JSON). `LockedThroughAsync` / `LockedThroughForSitesAsync` / `IsLocked` / `CanReconcile` are used with those exact names in Tasks 2, 4, 5, 6. `reconciled` / `reconciledAt` match the C# `Reconciled` / `ReconciledAt` under default camelCase JSON. CSS classes `locked-background` / `reconciled-background` match between Task 9 (emitted) and Tasks 10, 14 (styled, asserted).

**Known risk carried forward:** Task 7 duplicates ~90 lines into the service repo. A divergence is a lock with a hole. Both copies carry a header comment naming the twin; there is no shared package to put it in without modifying the base.

---

# Addendum A (2026-09-15): tasks added during execution

These tasks were written and reviewed after the pre-flight scan and the write-path audits found gaps in the plan. Where they conflict with an earlier task, they supersede it; each says what it supersedes.

## Task 5B: Guard the remaining plugin write paths (controller-authored; not in the original plan)

**Why this task exists.** A write-path audit (`write-path-audit.md` in this directory, read it first) found PlanRegistration writes outside Task 5's scope that reach locked (past) days. With the interceptor live, each one either crashes something or leaves partial state. Rulings F10, F11 and F13 in `progress.md` settle the treatment. The rule is: **bulk or background re-syncs SKIP locked rows (frozen means frozen); a user acting on specific days gets a MESSAGE.**

**Files (verify line numbers; they are from the audit):**
- `TimePlanning.Pn/Infrastructure/Helpers/CorruptedPauseIdRepair.cs` (`Run`)
- `TimePlanning.Pn/EformTimePlanningPlugin.cs` (`RepairCorruptedPauseIds` ~917 and its call in ConfigureServices ~208)
- `TimePlanning.Pn/Infrastructure/Helpers/GoogleSheetHelper.cs` (`PullEverythingFromGoogleSheet` ~161-473)
- `TimePlanning.Pn/Services/TimePlanningWorkingHoursService/TimePlanningWorkingHoursService.cs` (`Import` ~3927-4126)
- `TimePlanning.Pn/Services/TimePlanningFlexService/TimePlanningFlexService.cs` (`UpdateCreate` ~146-228, plus its follow-up loop ~168-211)
- `TimePlanning.Pn/Services/AbsenceRequestService/AbsenceRequestService.cs` (`ApproveAsync` ~223-291)
- `TimePlanning.Pn/Services/ContentHandoverService/ContentHandoverService.cs` (`AcceptAsync` ~584-921)
- Tests: extend the existing fixtures `CorruptedPauseIdRepairTests.cs`, `TimePlanningFlexServiceRemovedRowTests.cs` (or a sibling in the same shard), `AbsenceRequestServiceTests.cs`, `ContentHandoverServiceTests.cs` and `WorkingHoursImportRemovedRowTests.cs`; mirror each one's existing seeding. Put new cases in the EXISTING classes where possible, so no shard edit is needed. If you must add a class, add it to a shard filter in BOTH workflow files.

**Interfaces consumed:** `DayLockHelper.LockedThroughAsync / LockedThroughForSitesAsync / IsLocked`, `DayLockedException`, `ReconciledDayLockInterceptor.Instance`, resx key `DayIsLockedByReconciledDay`.

### THE TRAP (it applies to every SKIP below)
`PnBase.Create/Update/Delete` each call SaveChanges, which flushes EVERY dirty tracked entity on the context. "Skipping" a locked row therefore means: **do not mutate a tracked locked entity at all** (decide before mutating), or revert it (`entry.CurrentValues.SetValues(entry.OriginalValues); entry.State = Unchanged`) before the next save on that context. Skipping only the `.Update()` call while leaving the mutated entity tracked makes the NEXT save throw. For each skip, state in your report which of the two you used and why it is safe.

Resolve boundaries ONCE per call or run (for many sites, `LockedThroughForSitesAsync` once), never per row.

### Steps

1. **CorruptedPauseIdRepair (startup, CRITICAL, ruling F13: defense in depth).**
   a. In `Run`, resolve boundaries for the sites in the scan set once. Exclude or skip every row with `IsLocked(boundary, row.Date)` before mutating it.
   b. `RepairCorruptedPauseIds` builds its own context via `TimePlanningPnContextFactory` (unguarded). Build it with the interceptor attached instead: reuse `TimePlanningDbContextHelper(connectionString).GetDbContext()` if that fits, otherwise the same options it uses (MariaDbServerVersion 10.5.0, EnableRetryOnFailure, `ReconciledDayLockInterceptor.Instance`). **Never ServerVersion.AutoDetect.**
   c. Around the repair call in ConfigureServices, catch ONLY `DayLockedException`: log it + `SentrySdk.CaptureException`, and continue startup. Other exceptions keep today's behaviour. Comment why: a skip bug must never take the whole host down.
   Test (CorruptedPauseIdRepairTests): a worker with a reconciled boundary and a corrupted-pause row inside the repair window AT OR BELOW the boundary, plus one above it. `Run` completes without throwing, the locked row is byte-identical (Version/UpdatedAt via AsNoTracking), and the unlocked row IS repaired.

2. **Payroll export**: nothing to do. Ruling F10 exempts payroll-flag-only writes in the interceptor. Add ONE test proving it end to end if the PayrollExport fixture allows it cheaply (export a period containing a reconciled day, then assert the flag is set and there is no error). Otherwise say so.

3. **GoogleSheetHelper.PullEverythingFromGoogleSheet**: SKIP rows whose date is locked for that worker. Resolve boundaries once for the mapped sites. No test is expected (it needs the Sheets API); say so.

4. **WorkingHoursService.Import**: SKIP locked rows (dates are >= yesterday, so only a reconciled yesterday can hit this). Test in WorkingHoursImportRemovedRowTests style if its fixture drives Import; otherwise say so.

5. **FlexService.UpdateCreate**:
   a. Before ANY write, if any entry in the posted batch is locked for its site, return `OperationResult(false, localizationService.GetString("DayIsLockedByReconciledDay"))` (a message: a user is editing specific days).
   b. The follow-up loop over rows with `Date > Now.AddDays(-2)` touches YESTERDAY, which may be reconciled, and it is NOT a forward cascade from an edited day, so spec §6.2 does not cover it. SKIP locked rows there.
   Tests: the batch reject returns the message and writes nothing (a row above the boundary in the same batch stays unchanged); the follow-up loop with a reconciled yesterday does not throw.

6. **AbsenceRequestService.ApproveAsync**: BEFORE persisting `Status = Approved`, check every requested day against the worker's boundary. If any is locked, return the localized failure (DayIsLockedByReconciledDay) and persist nothing. Test: approving a request covering a locked day leaves the request NOT approved and no day flagged.

7. **ContentHandoverService.AcceptAsync**: BEFORE any write, check BOTH the sender's and the receiver's row for that date against each worker's own boundary. If either is locked, return the localized failure and write nothing. Test: accepting a handover where the sender's day is locked leaves both rows and the request status unchanged.

8. `PlanRegistrationHelper.UpdatePlanRegistration` (swallowing catch): no code change. CreateUpdate's Task 5 entry guard rejects any request containing a locked day, and cascades stay above the boundary (I2). Confirm by reading, and record it in the report.

9. Build: `cd eFormAPI/Plugins/TimePlanning.Pn && dotnet build TimePlanning.Pn.sln -v q`, 0 errors, no new warnings in touched files.

Commit message (when told to): `fix(lock): keep startup repair, sheet pull, import, flex, absence and handover inside the lock`

---

# Addendum B (2026-09-15): UI tasks for spec §8.1–8.4

Written after the user put §8.1–8.4 in scope, then reviewed twice against the real code (the second verdict: ready). Task *N*A/*N*B extends Task *N*, and the brief extraction for Task *N* includes them.


These tasks slot into `docs/superpowers/plans/2026-09-12-reconciled-day-lock.md` beside
the frontend tasks they extend. The plan's Global Constraints apply unchanged: dev mode
NONE, tests run in CI only, staging by file name, SCSS only in `eform-angular-frontend`,
and the copy rule (a string says what the day *is* and never mentions administrators).

*Revision 2 applies the plan review: 0 critical, 3 important, 12 minor.*

| Task | Follows | Spec | Repo |
|---|---|---|---|
| 8A | Task 8 | §7, correction | plugin |
| 9A | Task 9 | §8.1 glyphs, tooltips, legend; shared helpers | plugin |
| 10A | Task 10 | §8.1-8.4 styles | **eform-angular-frontend** (Task 10's branch and PR) |
| 11A | Task 11 | §8.2 inline confirm, read-only in place, provenance | plugin |
| 11B | 11A | §8.4 typed-word unlock, "free this day first" | plugin |
| 12A | Task 12 | §8.3 preview, header click, scope bar, per-site toast | plugin |
| 13A | Task 13 | strings for all of the above, help-wiring spec | plugin |
| 14A | Task 14 | shard `s` bootstrap, Task 14's spec rebuilt on the shared helpers | plugin |

**Execution order.** 8 → 8A → 9 → 9A → 10 → 10A → 11 → 11A → 11B → 12 → 12A → 13 → 13A → 14 → 14A.
Where a lettered task supersedes a step of its parent, an implementer who has not run
the parent yet should skip that step and apply the lettered version. Every supersession
is listed at the top of the task.

## Conflicts with plan Tasks 8-14

The plan review checked these against the code. #4 and #6 are corrected here.

1. **Task 8:** the TS `ReconcileThroughResultModel` has the wrong shape, and the
   service uses the type without importing it. Fixed in 8A.
2. **Task 10, dark mode:** the dark overrides key off
   `@media (prefers-color-scheme: dark) :root:not([data-theme="light"])`. This app
   switches dark mode with `body.theme-dark`, so the tokens would follow the OS setting,
   not the app setting. Fixed in 10A.
3. **Task 10, legibility:** in dark mode the lock ground `#262B29` sits under the state
   classes' hard-coded dark text. That covers `.plan-text` and also `.comment`, which
   inherits `#0F1316 !important` from `.X-background .plan-container`. Task 10 also dims
   the lock glyph along with the cell. Fixed in 10A.
4. **§8.1 filled seal (corrected):** a filled `verified` cannot come from the Outlined
   face, for two reasons.
   - `index.html:15` loads Outlined with FILL fixed at 0.
   - Under `body.theme-workspace`, `_workspace-mat-overrides.scss:27,36-44` forces
     *every* `.mat-icon` onto Outlined with `!important`.

   `fontSet="material-symbols-rounded"` with the class `.filled` is therefore not
   enough on its own. 9A/10A add a `tp-seal` class and a rule that out-specifies the
   workspace override.
5. **Task 11 Step 4:** `btn-secondary` inside `mat-dialog-actions` fails the plugin
   CI's "Button conventions" step. That step runs in the `build` job, which gates the
   PR. Fixed in 11A and 11B.
6. **Task 11 Step 3 (rationale corrected):** the bound `[helpId]` and
   `[attr.data-tp-help]` attributes do **not** fail `help-wiring.spec.ts`. They slip
   past it: its regexes read only literal `helpId=` and `data-tp-help=` attributes. The
   two banner ids would then sit outside the registry checks and outside the exhaustive
   hint list, so a typo in either id would never be caught. The fix stays: two hints
   with fixed ids, plus the hint-list update in 13A.
7. **Task 11 Step 3 / §8.5:** `tp-help-hint` renders only for admins
   (`HelpVisibilityService`). Non-admin users can reconcile (§8.6), so they would see
   no locked banner. The copy that matters goes in the footer as plain translated
   text.
8. **Task 11 Step 5:** reconcile and unlock close with `this.data`. The table's
   `afterClosed` treats that as a save and calls `updatePlanning` on a day that is now
   locked, which is refused. Fixed with the `lockStateChanged` close contract (11A).
9. **Tasks 11-12:** every new literal template key fails `help-wiring.spec.ts`'s frozen
   `TEMPLATE_TRANSLATE_KEYS`. Task 13 adds keys to `da` and `enUS` only. Fixed in 13A.
10. **Task 12:** `maxReconcileDate` uses `toISOString()`, which is UTC, so the max is
    one day early between 00:00 and 02:00 Danish time.
11. **Task 12:** the toolbar commits directly, with no preview, and ignores the
    per-site outcome.
12. **Task 12, gotcha 1:** the fix covers only the day cell. A click on the Name
    column still clears the batch selection. `[disableRowClickSelection]` fixes both
    at the source.
13. **Task 12, gotcha 2:** the re-emit happens only when `timePlannings` changes. The
    grid also drops the selection when `[columns]` or `[headerTemplate]` change.
14. **Task 12, housekeeping:** its commit uses a directory `git add`. The container
    spec needs a `ToastrService` provider once the container injects it.
15. **Task 13:** it registers the anchor `toolbar.reconcileThrough`, but Task 12's
    markup never carries it.
16. **Task 14, bootstrap:** shard `s` has no `activate-plugin.spec.ts` or
    `assert-true.spec.ts` bootstrap, which shards `q` and `r` have. The plugin never
    loads, and every `s` spec fails. Fixed in 14A.
17. **Task 14, the spec itself:**
    - It uses the current week. On a Monday or a Tuesday, `#cell3_1` is in the future
      or is today, so the reconcile button is not there.
    - It addresses rows by the positional `#cellN_M` ids.
    - It clicks reconcile and unlock once each, which no longer matches the two-step
      and typed-word flows.

    Fixed in 14A.
18. **Directory `git add`s:** Task 8 Step 3, Task 11 Step 6, Task 13 Step 6 and Task 14
    Step 3 stage directories. 8A, 11A, 13A and 14A replace each with an explicit list
    of files.
19. **Spec tension, left as written:** §8.1 gives a locked cell the cursor
    `not-allowed`, but §8.5 says the cell still opens a read-only dialog when clicked.

## Spec edits for the controller

Apply these to `docs/superpowers/specs/2026-09-12-reconciled-day-lock-design.md`:

1. **§8.3, gotcha 1.** Replace "The day cell must call `stopRowClick($event)`" with:
   "Set `[disableRowClickSelection]="true"` on the grid. `_selectRow` then leaves the
   selection alone for any click on the row (the day cell *and* the Name column),
   while still emitting `rowClick`. Rows are selected by their checkbox only."
2. **§8.1 constraint list.** Add: "The filled `verified` seal uses
   `fontSet="material-symbols-rounded"` with the class `filled tp-seal`. Outlined is
   loaded with FILL fixed at 0, and theme-workspace forces every `.mat-icon` onto
   Outlined, so the host styles carry a `tp-seal` rule that restores Rounded with
   FILL 1."

## Facts these tasks rely on

Each fact was checked against the code on 2026-09-15.

1. **mtx-grid 20.4.2 stamps the `<td>` class through a *pure* pipe:**
   `[class]="col | colClass: row: rowChangeRecord: ..."`. The pipe re-runs only when
   the row object changes, so `getCellClass` cannot show a preview that is driven by
   container state. The preview classes go on `.plan-container` in the cell template,
   which is evaluated on every change-detection pass.
2. **mtx-grid rebuilds `rowSelection` in `ngOnChanges` on *any* input change**
   (`new SelectionModel(this.multiSelectable, this.rowSelected)`). That includes
   `[columns]` and `[headerTemplate]`, not only `[data]`, and it emits nothing.
3. **`_selectRow()` skips the selection when `disableRowClickSelection` is set, but
   still emits `rowClick`** (`mtxGrid.mjs:1226-1238`).
4. **mtx-grid's `headerTemplate` input accepts a `{[field]: TemplateRef}` map.**
   Columns missing from the map keep the default header.
5. **`MatDialogRef.componentInstance` is set to `null` in `_finishDialogClose()`.**
   Read it at open time, not after close.
6. **`index.html:15` loads `Material Symbols Outlined` as a static instance with FILL
   fixed at 0.** On top of that, under `body.theme-workspace`,
   `_workspace-mat-overrides.scss:27,36-44` sets
   `.mat-icon { font-family: 'Material Symbols Outlined' !important }` with selector
   specificity (0,3,1), for every icon. `Material Symbols Rounded` is loaded with
   FILL 0..1. A filled glyph therefore needs Rounded **and** a rule above (0,3,1): the
   `tp-seal` rule in 10A, at (0,4,1).
7. **This app switches dark mode with `body.theme-dark`** (`full-layout.component.ts:94-100`).
   It does not use `prefers-color-scheme` or `data-theme`.
8. **The "Button conventions" step (`check-button-conventions.js`) runs in the plugin
   PR's `build` job**, which is not `continue-on-error`. In `mat-dialog-actions`, every
   button must be `.btn-primary`, `.btn-cancel`, `.btn-delete` or `.btn-quiet`, and the
   first one in source order must be `.btn-cancel`. The script reads the whole row and
   ignores `*ngIf`.
9. **`help/help-wiring.spec.ts` builds its `MARKUP` from the raw container, table and
   dialog templates, comments included** (:37). It then checks four things:
   - **Frozen keys:** the set of literal `'key' | translate` keys must equal
     `TEMPLATE_TRANSLATE_KEYS`.
   - **Exhaustive hints:** its list of `tp-help-hint` ids is complete.
   - **Registry:** every `helpId=` and `data-tp-help=` value found by literal regex
     (:69, :78) must exist in the registry.
   - **Help icons:** it counts `<tp-help-icon` against `(openInPanel)=`.

   Two consequences:
   - A *bound* `[helpId]` escapes every one of these checks.
   - A literal pattern written inside an HTML *comment* is checked as if it were
     markup. So template comments in these tasks never spell out `helpId=` or
     `data-tp-help=` followed by a quoted value.
10. **`tp-help-hint` renders nothing for non-admins.**
    `HelpEntryChromeBase.prose` checks `HelpVisibilityService`, which is admin-only.
11. **The index endpoint creates a `PlanRegistration` for every visible day that is
    missing one** (`TimePlanningPlanningService.cs:583-617`). A visible day therefore
    has a registration id, unless Task 5 skipped creating it inside the lock.
12. **Shards `r` and `q` run `activate-plugin.spec.ts` first** (alphabetical order,
    `workers: 1`). Without it, the plugin never loads in that shard.
13. **mtx-grid adds its checkbox column in front of the columns but never makes it
    sticky.** The checkbox `matColumnDef` has no `[sticky]`. The pinned Name column
    sits at `left: 0` (`_countPinnedPosition`, `mtxGrid.mjs:1184`), so on a sideways
    scroll the checkboxes slide under Name. `.mtx-grid-checkbox-cell` is 60px wide
    (`grid.scss:125-129`).

## Playwright conventions for shard `s`

- **Last week only.** The grid always opens on last week, so every visible day is in
  the past (I2). The wait for the last-week load checks that the request's `dateFrom`
  is last week's Monday.
- **Rows by name.** A grid row index is read once to *pick* a worker. After that,
  rows are always found by name.
- **One worker per spec.** Each spec uses its own worker and unlocks what it locked.
  The shard shares one database and runs its files in this order: `activate-plugin`,
  `assert-true`, `reconcile-bulk-preview`, `reconcile-day-lock`,
  `reconcile-dialog-confirm`, `reconcile-glyphs`, `reconcile-unlock-word`.
- **Worker rows** (index at the start of each test):
  - Task 14: row 3
  - 9A: row 5
  - 11A: row 6
  - 12A: rows 7, 8 and 10
  - 11B: row 9

  The `a` seed has 16 workers.

---

## Task 8A: Correct the bulk result model

**Follows:** Task 8.
**Supersedes:**
- Task 8 Step 2: only the contents of `reconcile-through-result.model.ts`. The three
  service methods stand.
- Task 8 Step 3: the directory `git add`.

**Files:**
- Modify: `eform-client/src/app/plugins/modules/time-planning-pn/models/plannings/reconcile-through-result.model.ts` (created by Task 8)
- Modify: `eform-client/src/app/plugins/modules/time-planning-pn/services/time-planning-pn-plannings.service.ts` (import list)

**Interfaces:**
- Consumes: the C# `ReconcileThroughResultModel` from Task 4:
  `Dictionary<int,DateTime> LandedOnBySiteId; int Applied; List<int> SkippedAlreadyFurtherForward; List<int> SkippedNoRegistration; List<int> AlreadyReconciledSiteIds`.
- Produces: `ReconcileThroughResultModel { landedOnBySiteId, applied, skippedAlreadyFurtherForward, skippedNoRegistration, alreadyReconciledSiteIds }`.
  12A consumes it.

Task 8's model had `{landedOn, applied, skippedSiteIds}`. The server never sends those
names, so every read in 12A would be `undefined`. The model must mirror the C# model
field for field. The host's Newtonsoft contract resolver camelCases the names, and
dictionary keys arrive as strings.

- [ ] **Step 1: Replace the model**

`models/plannings/reconcile-through-result.model.ts`:

```ts
/**
 * Mirrors the C# ReconcileThroughResultModel (plan Task 4) field for field.
 *
 * Per-worker landing, because the boundary is a staircase: the mark falls on each
 * worker's own latest registered day at or before the requested date, so one shared
 * "landedOn" would name the wrong date for most of them.
 */
export class ReconcileThroughResultModel {
  /** siteId -> the day the mark landed on. JSON object keys arrive as strings. */
  landedOnBySiteId: { [siteId: number]: string } = {};
  /** Workers whose boundary actually moved. Excludes no-ops. */
  applied: number;
  /** Already reconciled at or past the target. Moving them back would be an unlock. */
  skippedAlreadyFurtherForward: number[] = [];
  /** No registration at or before the target, so there was nothing to mark. */
  skippedNoRegistration: number[] = [];
  /** Already marked on exactly the landing day; nothing changed. */
  alreadyReconciledSiteIds: number[] = [];
}
```

Task 8 already exports it from `models/plannings/index.ts`. Check that the export line
is there.

- [ ] **Step 2: Import the model where the service uses it**

Task 8's `reconcileThrough()` names the type, but Task 8 never adds the import. Extend
the existing `from '../models'` import in `time-planning-pn-plannings.service.ts`:

```ts
import {
  PlanningPrDayModel,
  ReconcileThroughResultModel,
  TimeFlexesModel,
  TimeFlexesUpdateModel,
  TimePlanningModel,
  TimePlanningsRequestModel,
  TimePlanningsUpdateModel,
  TimePlanningUpdateModel,
  PlanRegistrationVersionHistoryModel,
} from '../models';
```

- [ ] **Step 3: Task 8's own commit, staged by name** (replaces Task 8 Step 3)

Run this only if Task 8's commit has not been made yet:

```bash
cd /home/rene/Documents/workspace/microting/eform-angular-timeplanning-plugin
git add eform-client/src/app/plugins/modules/time-planning-pn/models/plannings/planning-pr-day.model.ts \
        eform-client/src/app/plugins/modules/time-planning-pn/models/plannings/time-planning.model.ts \
        eform-client/src/app/plugins/modules/time-planning-pn/models/plannings/reconcile-through-result.model.ts \
        eform-client/src/app/plugins/modules/time-planning-pn/models/plannings/index.ts \
        eform-client/src/app/plugins/modules/time-planning-pn/services/time-planning-pn-plannings.service.ts
git commit -m "feat(lock): add the lock fields and reconcile calls to the client"
```

- [ ] **Step 4: Commit 8A**

```bash
cd /home/rene/Documents/workspace/microting/eform-angular-timeplanning-plugin
git add eform-client/src/app/plugins/modules/time-planning-pn/models/plannings/reconcile-through-result.model.ts \
        eform-client/src/app/plugins/modules/time-planning-pn/services/time-planning-pn-plannings.service.ts
git commit -m "fix(lock): mirror the server's per-site reconcile-through result"
```

---

## Task 9A: Glyphs, tooltips, the legend, and the shared helpers (§8.1)

**Follows:** Task 9. It needs `isDayLocked` and `isDayReconciled` from there.
**Supersedes:** nothing. It adds to Task 9.

**Files:**
- Create: `eform-client/src/app/plugins/modules/time-planning-pn/components/plannings/day-lock.util.ts` (final, including the bulk preview builder that 12A uses)
- Create: `eform-client/src/app/plugins/modules/time-planning-pn/components/plannings/day-lock.util.spec.ts` (final)
- Modify: `.../components/plannings/time-plannings-table/time-plannings-table.component.ts`
- Modify: `.../components/plannings/time-plannings-table/time-plannings-table.component.html` (day-cell template :237-434; below `</mtx-grid>` :30)
- Create: `eform-client/playwright/e2e/plugins/time-planning-pn/s/reconcile-helpers.ts`
- Create: `eform-client/playwright/e2e/plugins/time-planning-pn/s/reconcile-glyphs.spec.ts`

**Interfaces:**
- Consumes:
  - `PlanningPrDayModel.reconciled` and `.reconciledAt`, `TimePlanningModel.lockedThrough` (Task 8)
  - `isDayLocked` and `isDayReconciled` (Task 9)
  - classes from 10A: `tp-day-glyph*`, `tp-seal`, `tp-lock-legend*`
  - keys from 13A: `lockedTooltip`, `reconciledLegend`, `reconciledProvenance`, `Reconciled`
- Produces:
  - `dayKey`, `formatReconciledProvenance`, `buildReconcilePreview`, `ReconcilePreview` and
    `ReconcileRowOutcome`. 11A, 11B and 12A use them, and none of them rewrites this file.
  - Table: `hasLockedDayInView` and `reconciledTooltip(row, field)`.
  - Playwright: `s/reconcile-helpers.ts`, used by every later spec.

**The two glyphs.**
- **`lock`:** an outline lock at the bottom-left marks a cascade-locked day. It uses
  `fontSet="material-symbols-outlined" class="neutral-icon"`, like every other
  day-cell icon.
- **`verified`:** a filled seal at the top-right marks the reconciled boundary day. It
  uses `fontSet="material-symbols-rounded" class="neutral-icon filled tp-seal"` for
  three reasons (fact 6):
  - Outlined is loaded with FILL fixed at 0, so the glyph must come from Rounded.
  - Theme-workspace pushes every `.mat-icon` back onto Outlined, and the `tp-seal`
    rule in 10A overrides that.
  - `neutral-icon` keeps it the same size and offset as its neighbours.

**Positioning without fighting `.neutral-icon`.** That class forces
`position: relative !important; top: 5px !important`, so neither glyph is absolutely
positioned.
- **The lock** is the **last child of `.plan-content`**. 10A makes `.plan-content` a
  flex column in a locked cell and gives the glyph `margin-top: auto`, which puts it
  bottom-left.
- **The seal** is the **last child of `.plan-icons`**, which puts it at the top-right.
  It never overlaps the message icons, because it takes a place in their row.

- [ ] **Step 1: The shared helper, written once**

`components/plannings/day-lock.util.ts`. The preview builder lives here from the start,
so 12A only consumes it:

```ts
import {DatePipe} from '@angular/common';
import {TranslateService} from '@ngx-translate/core';
import {format} from 'date-fns';
import {TimePlanningModel} from '../../models';

/**
 * Calendar-day key 'yyyy-MM-dd', for comparing days with no timezone involved.
 *
 * Server dates (PlanningPrDayModel.date, TimePlanningModel.lockedThrough) arrive as
 * server-local midnight with no offset, e.g. '2026-09-07T00:00:00', so the first ten
 * characters ARE the calendar day. Turning them into a Date and comparing instants is
 * how a boundary ends up a day off for part of every evening. A Date is formatted in
 * local time.
 */
export function dayKey(value: string | Date | null | undefined): string | null {
  if (!value) {
    return null;
  }
  if (value instanceof Date) {
    return isNaN(value.getTime()) ? null : format(value, 'yyyy-MM-dd');
  }
  return /^\d{4}-\d{2}-\d{2}/.test(value) ? value.slice(0, 10) : null;
}

/**
 * "Afstemt 14.09.2026 kl. 10:32" (spec §8.1, §8.2). There is no "by whom":
 * ReconciledBy is a declared non-goal.
 *
 * ReconciledAt is written as DateTime.Now (Task 4) and read back from a datetime(6)
 * column, so EF materialises it as DateTimeKind.Unspecified. Newtonsoft
 * (RoundtripKind) serialises it with NO offset, and the browser reads it as local
 * wall-clock time, which is the server's clock on a Danish deployment. Do NOT pass
 * 'UTC' here, the way formatStamp does for the shift stamps.
 */
export function formatReconciledProvenance(
  reconciledAt: string | null | undefined,
  datePipe: DatePipe,
  translate: TranslateService,
): string {
  if (!reconciledAt) {
    // I1 says a reconciled day always has a timestamp; this covers a malformed row.
    return translate.instant('Reconciled');
  }
  return translate.instant('reconciledProvenance', {
    date: datePipe.transform(reconciledAt, 'dd.MM.yyyy'),
    time: datePipe.transform(reconciledAt, 'HH:mm'),
  });
}

/** 'lock' = the boundary moves to a day on screen. 'skip' = already at or past the target. */
export type ReconcileRowOutcome = 'lock' | 'skip';

export interface ReconcilePreview {
  /** 'yyyy-MM-dd'. Sent to reconcile-through as it is. */
  target: string;
  /**
   * Workers sent to reconcile-through: every row in scope that the preview drew,
   * as 'lock' or as 'skip', each once. Skipped rows are sent too, so that the server
   * reports them. A row the preview could not draw is never sent (§8.3: the region
   * is previewed before it is committed).
   */
  siteIds: number[];
  /** Per worker: the day the mark lands on. */
  landingBySiteId: Record<number, string>;
  /** Per worker: the boundary they already have, so already-locked cells are not re-highlighted. */
  existingBySiteId: Record<number, string | null>;
  outcomeBySiteId: Record<number, ReconcileRowOutcome>;
  /** Workers whose boundary will move. */
  willReconcileCount: number;
  skipCount: number;
}

/**
 * The region a reconcile-through WILL lock, per worker (spec §8.3). It mirrors the
 * server rules in Task 4:
 * - A worker already reconciled at or past the target is skipped, never moved back.
 *   Moving back would be an unlock, which is a separate, heavier action (§8.4).
 * - Otherwise the mark lands on the latest day at or before the target that has a
 *   registration.
 *
 * Only days on screen are known here. A row with no registered day on screen
 * between its boundary and the target has nothing to draw, so it is left out of the
 * commit. The date field is limited to days on screen (12A) and the index creates a
 * registration for every visible day (fact 11), so this only happens when a visible
 * day failed to materialise.
 */
export function buildReconcilePreview(
  rows: TimePlanningModel[],
  scopeSiteIds: number[],
  target: string,
): ReconcilePreview {
  const inScope = new Set(scopeSiteIds);
  const preview: ReconcilePreview = {
    target,
    siteIds: [],
    landingBySiteId: {},
    existingBySiteId: {},
    outcomeBySiteId: {},
    willReconcileCount: 0,
    skipCount: 0,
  };

  for (const row of rows) {
    if (!inScope.has(row.siteId) || row.siteId in preview.outcomeBySiteId) {
      continue;
    }
    const existing = dayKey(row.lockedThrough);
    preview.existingBySiteId[row.siteId] = existing;

    if (existing !== null && existing >= target) {
      preview.outcomeBySiteId[row.siteId] = 'skip';
      preview.siteIds.push(row.siteId);
      preview.skipCount++;
      continue;
    }

    const landing = (row.planningPrDayModels ?? [])
      .filter(day => !!day?.id)
      .map(day => dayKey(day.date))
      .filter((key): key is string => key !== null && key <= target)
      .sort()
      .pop();
    if (!landing || (existing !== null && landing <= existing)) {
      continue;
    }

    preview.landingBySiteId[row.siteId] = landing;
    preview.outcomeBySiteId[row.siteId] = 'lock';
    preview.siteIds.push(row.siteId);
    preview.willReconcileCount++;
  }
  return preview;
}
```

- [ ] **Step 2: Unit tests, written once**

`components/plannings/day-lock.util.spec.ts`. It runs in CI's `angular-unit-test` job,
which is `continue-on-error` and so does not gate the PR. Keep it green anyway.

```ts
import {DatePipe} from '@angular/common';
import {buildReconcilePreview, dayKey, formatReconciledProvenance} from './day-lock.util';

describe('day-lock util', () => {
  describe('dayKey', () => {
    it('takes the calendar day from a server date without parsing it', () => {
      expect(dayKey('2026-09-07T00:00:00')).toBe('2026-09-07');
    });

    it('formats a Date in local time', () => {
      expect(dayKey(new Date(2026, 8, 7, 23, 30))).toBe('2026-09-07');
    });

    it('returns null for missing or malformed input', () => {
      expect(dayKey(null)).toBeNull();
      expect(dayKey(undefined)).toBeNull();
      expect(dayKey('not a date')).toBeNull();
    });
  });

  describe('formatReconciledProvenance', () => {
    const translate = {instant: jest.fn((key: string, params?: object) => key)} as any;

    it('passes the wall-clock date and time through unshifted', () => {
      formatReconciledProvenance('2026-09-14T10:32:11', new DatePipe('en-US'), translate);
      expect(translate.instant)
        .toHaveBeenLastCalledWith('reconciledProvenance', {date: '14.09.2026', time: '10:32'});
    });

    it('falls back to the bare state name when there is no timestamp', () => {
      formatReconciledProvenance(null, new DatePipe('en-US'), translate);
      expect(translate.instant).toHaveBeenLastCalledWith('Reconciled');
    });
  });

  describe('buildReconcilePreview', () => {
    const day = (date: string, id = 1) => ({id, date: `${date}T00:00:00`}) as any;
    const row = (siteId: number, lockedThrough: string | null, days: any[]) => ({
      siteId,
      lockedThrough: lockedThrough ? `${lockedThrough}T00:00:00` : null,
      planningPrDayModels: days,
    }) as any;
    const week = ['2026-09-07', '2026-09-08', '2026-09-09', '2026-09-10'].map(d => day(d));

    it('lands on the latest registered day at or before the target', () => {
      const preview = buildReconcilePreview([row(1, null, week)], [1], '2026-09-09');
      expect(preview.landingBySiteId[1]).toBe('2026-09-09');
      expect(preview.outcomeBySiteId[1]).toBe('lock');
      expect(preview.siteIds).toEqual([1]);
      expect(preview.willReconcileCount).toBe(1);
    });

    it('skips a worker at or past the target, never moves the line back, and still sends it', () => {
      const preview = buildReconcilePreview(
        [row(1, '2026-09-10', week), row(2, '2026-09-09', week)], [1, 2], '2026-09-09');
      expect(preview.outcomeBySiteId[1]).toBe('skip');
      expect(preview.outcomeBySiteId[2]).toBe('skip');
      expect(preview.siteIds).toEqual([1, 2]);
      expect(preview.skipCount).toBe(2);
      expect(preview.willReconcileCount).toBe(0);
    });

    it('ignores days without a registration id', () => {
      const days = [day('2026-09-07'), day('2026-09-08', 0), day('2026-09-09', 0)];
      const preview = buildReconcilePreview([row(1, null, days)], [1], '2026-09-09');
      expect(preview.landingBySiteId[1]).toBe('2026-09-07');
    });

    it('never sends a row it could not draw', () => {
      const unregistered = ['2026-09-07', '2026-09-08'].map(d => day(d, 0));
      const preview = buildReconcilePreview([row(1, null, unregistered)], [1], '2026-09-08');
      expect(preview.siteIds).toEqual([]);
      expect(preview.outcomeBySiteId[1]).toBeUndefined();
      expect(preview.willReconcileCount).toBe(0);
    });

    it('keeps the existing boundary so already-locked days are not highlighted again', () => {
      const preview = buildReconcilePreview([row(1, '2026-09-07', week)], [1], '2026-09-09');
      expect(preview.existingBySiteId[1]).toBe('2026-09-07');
      expect(preview.landingBySiteId[1]).toBe('2026-09-09');
    });

    it('previews only the workers in scope, and sends each once', () => {
      const preview = buildReconcilePreview([row(1, null, week), row(2, null, week)], [2, 2], '2026-09-08');
      expect(preview.siteIds).toEqual([2]);
      expect(preview.outcomeBySiteId[1]).toBeUndefined();
    });
  });
});
```

- [ ] **Step 3: Table component, tooltip and legend switch**

In `time-plannings-table.component.ts`, add the import. 12A extends this line:

```ts
import {dayKey, formatReconciledProvenance} from '../day-lock.util';
```

Add to the class, beside Task 9's `isDayLocked` and `isDayReconciled`:

```ts
  /**
   * Legend switch (spec §8.1): shown whenever a locked day is on screen, because
   * otherwise nobody learns what the hatch means. Recomputed when rows arrive, never
   * on every change-detection pass.
   */
  hasLockedDayInView = false;

  /** Seal tooltip: "Afstemt 14.09.2026 kl. 10:32". */
  reconciledTooltip(row: any, field: string): string {
    return formatReconciledProvenance(
      row?.planningPrDayModels?.[field]?.reconciledAt, this.datePipe, this.translateService);
  }

  private computeHasLockedDayInView(): boolean {
    return (this.timePlannings ?? []).some(row => {
      const boundary = dayKey(row.lockedThrough);
      return boundary !== null
        && (row.planningPrDayModels ?? []).some(day => {
          const key = dayKey(day?.date);
          return key !== null && key <= boundary;
        });
    });
  }
```

At the end of `ngOnChanges`, after the existing `waitingForFreshData` block (:81-85):

```ts
    if (changes.timePlannings) {
      this.hasLockedDayInView = this.computeHasLockedDayInView();
    }
```

- [ ] **Step 4: The glyphs in the day cell**

In `#dayColumnTemplate` in `time-plannings-table.component.html`:

**(a)** Add the lock immediately before the `</div>` that closes `.plan-content`. That is
the line after the `(id: …)` block, :417-418:

```html
      <!--
        Lock glyph (spec §8.1): a cascade-locked day. It is the last item of the
        content column, which Task 10A turns into a flex column with this glyph at
        margin-top:auto, so it sits bottom-left. The reconciled day itself carries
        the seal instead.
      -->
      <span class="tp-day-glyph tp-day-glyph--lock"
            *ngIf="isDayLocked(row, col.field) && !isDayReconciled(row, col.field)"
            id="lockGlyph{{index}}_{{col.field}}"
            [matTooltip]="'lockedTooltip' | translate">
        <mat-icon fontSet="material-symbols-outlined" class="neutral-icon">lock</mat-icon>
      </span>
```

**(b)** Add the seal immediately before the `</div>` that closes `.plan-icons`, after
the `message === 13` icon, :431-432:

```html
      <!--
        Seal glyph (spec §8.1): the reconciled boundary day. It is the last item of the
        icon column, so it sits top-right. It uses Rounded, because Outlined is loaded
        with FILL fixed at 0. tp-seal is the host rule that keeps it Rounded and filled
        under theme-workspace, which forces every mat-icon onto Outlined.
      -->
      <span class="tp-day-glyph tp-day-glyph--seal"
            *ngIf="isDayReconciled(row, col.field)"
            id="sealGlyph{{index}}_{{col.field}}"
            [matTooltip]="reconciledTooltip(row, col.field)">
        <mat-icon fontSet="material-symbols-rounded" class="neutral-icon filled tp-seal">verified</mat-icon>
      </span>
```

- [ ] **Step 5: The legend under the grid**

In the same template, directly after `</mtx-grid>` (:30) and before
`<ng-template #noWorkersTemplate>`:

```html
<!--
  Legend (spec §8.1). It shows whenever a locked day is on screen, because otherwise
  nobody learns what the hatch means. It is absent otherwise, so tenants who never
  reconcile see no new chrome. It is plain translated text rather than a help hint:
  help chrome is admin-only, and any user can meet a locked day.
-->
<div class="tp-lock-legend" id="lockLegend" *ngIf="hasLockedDayInView">
  <span class="tp-lock-legend__item" id="lockLegendLocked">
    <span class="tp-lock-legend__swatch tp-lock-legend__swatch--locked">
      <mat-icon fontSet="material-symbols-outlined" class="neutral-icon">lock</mat-icon>
    </span>
    {{ 'lockedTooltip' | translate }}
  </span>
  <span class="tp-lock-legend__item" id="lockLegendReconciled">
    <span class="tp-lock-legend__swatch tp-lock-legend__swatch--reconciled">
      <mat-icon fontSet="material-symbols-rounded" class="neutral-icon filled tp-seal">verified</mat-icon>
    </span>
    {{ 'reconciledLegend' | translate }}
  </span>
</div>
```

The legend reuses `lockedTooltip`, so the tooltip and the legend say the same words.
13A adds the keys. Until then they render as bare keys.

- [ ] **Step 6: Shared Playwright helpers**

`eform-client/playwright/e2e/plugins/time-planning-pn/s/reconcile-helpers.ts`.
Playwright does not collect it as a test, because the name does not match `*.spec.ts`.
The helpers target the finished UI, including the 11A and 11B ids. They run only once
Task 14 adds shard `s` to the matrix, and by then every one of those ids exists.

```ts
import { expect, Locator, Page, Response } from '@playwright/test';

/**
 * Shared by every spec in shard s. Two rules are enforced here so no spec can forget
 * them:
 *
 *  1. Rows are found by WORKER NAME, never by grid position. The `#cell{row}_{day}`
 *     ids are positional, and a row shift silently addresses another worker. That is
 *     exactly how e1m/dashboard-edit-multishift.spec.ts once failed. A position is
 *     read once per spec, to PICK a worker, and never again.
 *  2. The grid is always opened on LAST week, so every visible day is in the past.
 *     Reconcile is refused for today and the future (I2), and the current week has
 *     no past day at all on a Monday.
 *
 * Every spec uses its own worker and unlocks what it locked, because the shard shares
 * one database and runs its specs in file order (workers: 1).
 */

const INDEX_PATH = '/api/time-planning-pn/plannings/index';
export const RECONCILE_PATH = /\/api\/time-planning-pn\/plannings\/\d+\/reconcile$/;
export const UNRECONCILE_PATH = /\/api\/time-planning-pn\/plannings\/\d+\/unreconcile$/;
export const RECONCILE_THROUGH_PATH = /\/api\/time-planning-pn\/plannings\/reconcile-through$/;
/** Any write to a day: a save (PUT plannings/{id}) as well as reconcile and unlock. */
export const PLANNING_PUT_PATH = /\/api\/time-planning-pn\/plannings\//;
/** Danish, like the rest of the suite: CI runs the UI in Danish. */
export const UNLOCK_WORD = 'LÅS OP';
export const LOCKED_TOOLTIP = 'Låst · ligger før en afstemt dag';
export const PROVENANCE = /^Afstemt \d{2}\.\d{2}\.\d{4} kl\. \d{2}:\d{2}$/;

export async function waitForSpinner(page: Page): Promise<void> {
  if (await page.locator('.overlay-spinner').count() > 0) {
    await page.locator('.overlay-spinner').waitFor({ state: 'hidden', timeout: 30000 });
  }
}

/** The grid's index POST. With `dateFrom` it only accepts a load of that period. */
export function waitForIndex(page: Page, dateFrom?: string): Promise<Response> {
  return page.waitForResponse(r =>
    r.url().includes(INDEX_PATH)
    && r.request().method() === 'POST'
    && (dateFrom === undefined || `${r.request().postDataJSON()?.dateFrom ?? ''}`.startsWith(dateFrom)));
}

/** Matches on the pathname: a bare includes('/reconcile') also matches /unreconcile. */
export function waitForPut(page: Page, path: RegExp): Promise<Response> {
  return page.waitForResponse(r =>
    r.request().method() === 'PUT' && path.test(new URL(r.url()).pathname));
}

/** Counts the PUTs matching `path` from now on. Read `.count` when needed. */
export function countPuts(page: Page, path: RegExp): { count: number } {
  const counter = { count: 0 };
  page.on('request', req => {
    if (req.method() === 'PUT' && path.test(new URL(req.url()).pathname)) {
      counter.count++;
    }
  });
  return counter;
}

/** OperationResult failures come back as HTTP 200 with success:false, so check both. */
export async function expectSuccess(response: Response): Promise<any> {
  expect(response.status(), `${response.url()} HTTP status`).toBeLessThan(400);
  const body = await response.json();
  expect(body.success, `${response.url()} failed: ${body.message}`).toBe(true);
  return body;
}

/** Last week's Monday as yyyy-MM-dd, in the same local calendar the grid's dateFrom uses. */
export function lastWeekMonday(): string {
  const d = new Date();
  d.setHours(0, 0, 0, 0);
  const sinceMonday = (d.getDay() + 6) % 7;
  d.setDate(d.getDate() - sinceMonday - 7);
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
}

export async function openDashboardLastWeek(page: Page): Promise<void> {
  await page.locator('mat-nested-tree-node').filter({ hasText: 'Timeregistrering' }).click();
  const initial = waitForIndex(page);
  await page.locator('mat-tree-node').filter({ hasText: 'Dashboard' }).click();
  await initial;
  await waitForSpinner(page);
  // Filtered on dateFrom, so a late current-week response cannot satisfy the wait.
  const monday = lastWeekMonday();
  const lastWeek = waitForIndex(page, monday);
  await page.locator('#backwards').click();
  const response = await lastWeek;
  expect(response.request().postDataJSON().dateFrom, 'the grid must be on last week').toMatch(new RegExp(`^${monday}`));
  await waitForSpinner(page);
}

/** The worker rendered at a grid position. Call it once per spec, to pick a worker. */
export async function workerAtRow(page: Page, rowIndex: number): Promise<string> {
  const name = (await page.locator(`#firstColumn${rowIndex} .hours-info strong`).innerText()).trim();
  // A blank name would make every later lookup match vacuously.
  expect(name, `row ${rowIndex} must name a worker`).toMatch(/^[^\s-]/);
  return name;
}

function escapeRegExp(value: string): string {
  return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

/** The grid row for a worker, found by name, never by position. */
export function rowOf(page: Page, worker: string): Locator {
  return page.locator('#main-header-text tr.mat-mdc-row').filter({
    has: page.locator('.hours-info strong', {
      hasText: new RegExp(`^\\s*${escapeRegExp(worker)}\\s*$`),
    }),
  });
}

export function rowCheckbox(page: Page, worker: string): Locator {
  return rowOf(page, worker).locator('td.mtx-grid-checkbox-cell input[type="checkbox"]');
}

/** A worker's day cell (.plan-container). day = column index, 0 = first visible day. */
export function cellOf(page: Page, worker: string, day: number): Locator {
  return rowOf(page, worker).locator(`.plan-container[id^="cell"][id$="_${day}"]`);
}

/** The <td> mtx-grid stamps getCellClass onto. */
export function tdOf(page: Page, worker: string, day: number): Locator {
  return cellOf(page, worker, day).locator('xpath=..');
}

export async function dialogTitle(page: Page): Promise<{ worker: string; date: string }> {
  const title = page.locator('mat-dialog-container [mat-dialog-title]');
  await expect(title).toBeVisible({ timeout: 10000 });
  // "<name> - <dd.MM.yyyy> (<id>)", with the date on its own line.
  const raw = (await title.innerText()).replace(/\s+/g, ' ').trim();
  return {
    worker: raw.split(/\s+-\s+/)[0].trim(),
    date: /(\d{2}\.\d{2}\.\d{4})/.exec(raw)?.[1] ?? '',
  };
}

/** Opens a day, asserts the dialog is that worker's, and returns the date as dd.MM.yyyy. */
export async function openDay(page: Page, worker: string, day: number): Promise<string> {
  const cell = cellOf(page, worker, day);
  await expect(cell).toHaveCount(1);
  await cell.scrollIntoViewIfNeeded();
  await cell.click();
  const title = await dialogTitle(page);
  expect(title.worker, 'the dialog must belong to the worker the row was found by').toBe(worker);
  expect(title.date).toMatch(/^\d{2}\.\d{2}\.\d{4}$/);
  return title.date;
}

/** Cancel with nothing persisted: no save and no reload. */
export async function closeDayWithoutChange(page: Page): Promise<void> {
  await page.locator('#cancelButton').click();
  await expect(page.locator('mat-dialog-container')).toHaveCount(0);
}

/** Close after a reconcile made in the dialog: the table reloads the grid instead of saving. */
export async function closeDayAfterLockChange(page: Page): Promise<void> {
  const reload = waitForIndex(page, lastWeekMonday());
  await page.locator('#cancelButton').click();
  await reload;
  await waitForSpinner(page);
}

/** Two-step reconcile from the open dialog (§8.2). The dialog stays open, read-only. */
export async function reconcileOpenDay(page: Page): Promise<void> {
  await page.locator('#reconcileButton').click();
  await expect(page.locator('#reconcileConfirmButton')).toBeVisible();
  const put = waitForPut(page, RECONCILE_PATH);
  await page.locator('#reconcileConfirmButton').click();
  await expectSuccess(await put);
  await expect(page.locator('#reconciledProvenanceText')).toHaveText(PROVENANCE);
}

/** Reconciles one day and returns its date (dd.MM.yyyy), with the grid reloaded. */
export async function reconcileDay(page: Page, worker: string, day: number): Promise<string> {
  const date = await openDay(page, worker, day);
  await reconcileOpenDay(page);
  await closeDayAfterLockChange(page);
  return date;
}

/** Typed-word unlock of the open boundary day (§8.4). The dialog closes and the grid reloads. */
export async function unlockOpenDay(page: Page): Promise<void> {
  await page.locator('#unlockButton').click();
  await page.locator('#unlockWordInput').fill(UNLOCK_WORD);
  const put = waitForPut(page, UNRECONCILE_PATH);
  const reload = waitForIndex(page, lastWeekMonday());
  await page.locator('#unlockConfirmButton').click();
  await expectSuccess(await put);
  await reload;
  await waitForSpinner(page);
}

export async function unlockDay(page: Page, worker: string, day: number): Promise<void> {
  await openDay(page, worker, day);
  await unlockOpenDay(page);
}
```

- [ ] **Step 7: Playwright, glyphs and legend**

`eform-client/playwright/e2e/plugins/time-planning-pn/s/reconcile-glyphs.spec.ts`:

```ts
import { test, expect } from '@playwright/test';
import { LoginPage } from '../../../Page objects/Login.page';
import {
  cellOf, LOCKED_TOOLTIP, openDashboardLastWeek, PROVENANCE, reconcileDay, unlockDay, workerAtRow,
} from './reconcile-helpers';

/**
 * Spec §8.1: the lock and seal glyphs, their tooltips, and the legend under the
 * grid. The worker is grid row 5 at the start and is found by name after that.
 * Day 4 (last week's Friday) becomes the boundary, so day 2 is cascade-locked and
 * day 5 stays open.
 */
test.describe('Reconciled day lock: glyphs and legend', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('http://localhost:4200');
    await new LoginPage(page).login();
    await openDashboardLastWeek(page);
  });

  test('the boundary shows the seal, earlier days the lock, and the legend explains both', async ({ page }) => {
    // Precondition, and a leak detector for the specs before this one: nothing
    // locked is in view, so there is no legend.
    await expect(page.locator('#lockLegend')).toHaveCount(0);

    const worker = await workerAtRow(page, 5);
    await reconcileDay(page, worker, 4);

    // Boundary: the seal and no lock. Its tooltip is the provenance line.
    const seal = cellOf(page, worker, 4).locator('.tp-day-glyph--seal');
    await expect(seal).toBeVisible();
    await expect(seal.locator('mat-icon')).toHaveClass(/tp-seal/);
    await expect(cellOf(page, worker, 4).locator('.tp-day-glyph--lock')).toHaveCount(0);
    await seal.hover();
    await expect(page.locator('.cdk-overlay-container .mat-mdc-tooltip-surface')
      .filter({ hasText: PROVENANCE })).toBeVisible({ timeout: 10000 });

    // Cascade: the lock and no seal. Its tooltip says what the day is.
    const lock = cellOf(page, worker, 2).locator('.tp-day-glyph--lock');
    await expect(lock).toBeVisible();
    await expect(cellOf(page, worker, 2).locator('.tp-day-glyph--seal')).toHaveCount(0);
    await lock.hover();
    await expect(page.locator('.cdk-overlay-container .mat-mdc-tooltip-surface')
      .filter({ hasText: LOCKED_TOOLTIP })).toBeVisible({ timeout: 10000 });

    // Above the boundary: no glyph at all.
    await expect(cellOf(page, worker, 5).locator('.tp-day-glyph')).toHaveCount(0);

    // The legend appears with the first locked day and names both states.
    await expect(page.locator('#lockLegend')).toBeVisible();
    await expect(page.locator('#lockLegendLocked')).toContainText(LOCKED_TOOLTIP);
    await expect(page.locator('#lockLegendReconciled')).toContainText('Afstemt');

    // Cleanup. With nothing locked, the legend goes away again.
    await unlockDay(page, worker, 4);
    await expect(cellOf(page, worker, 2).locator('.tp-day-glyph')).toHaveCount(0);
    await expect(page.locator('#lockLegend')).toHaveCount(0);
  });
});
```

- [ ] **Step 8: Verify what can be verified**

Tests run only in CI. The plugin has no standalone Angular build either, because it
compiles inside the host. Two checks:
- `git diff` touches only the lines named above.
- `grep -c 'tp-day-glyph--\|lockLegend' eform-client/src/app/plugins/modules/time-planning-pn/components/plannings/time-plannings-table/time-plannings-table.component.html`
  prints **5**. The five lines are the lock span, the seal span, the legend wrapper and
  its two items. `grep -c` counts lines, and the glyph comments do not contain either
  pattern. **Do not add markup to reach any other number.**

- [ ] **Step 9: Commit**

```bash
cd /home/rene/Documents/workspace/microting/eform-angular-timeplanning-plugin
git add eform-client/src/app/plugins/modules/time-planning-pn/components/plannings/day-lock.util.ts \
        eform-client/src/app/plugins/modules/time-planning-pn/components/plannings/day-lock.util.spec.ts \
        eform-client/src/app/plugins/modules/time-planning-pn/components/plannings/time-plannings-table/time-plannings-table.component.ts \
        eform-client/src/app/plugins/modules/time-planning-pn/components/plannings/time-plannings-table/time-plannings-table.component.html \
        eform-client/playwright/e2e/plugins/time-planning-pn/s/reconcile-helpers.ts \
        eform-client/playwright/e2e/plugins/time-planning-pn/s/reconcile-glyphs.spec.ts
git commit -m "feat(lock): lock and seal glyphs, tooltips and a legend in the grid"
```

---

## Task 10A: All lock styles in the host stylesheet (HOST REPO, same PR as Task 10)

**Follows:** Task 10.
**Supersedes:** Task 10 Step 1 in full. The block below replaces the block Task 10
appended. If Task 10 has not run yet, append this block instead. Task 10 Step 2 (the
branch) stands, and Step 3 here commits on that branch.

**Files (different repository):** `/home/rene/Documents/workspace/microting/eform-angular-frontend`
- Modify: `eform-client/src/scss/styles.scss`. Place the block after the
  `.red-background .plan-container` block, which ends at :358, where Task 10 put its
  own block.

**Interfaces:**
- Consumes: the class names written by 9A, 11A, 11B and 12A.
- Produces: the tokens `--tp-locked-bg`, `--tp-locked-hatch`, `--tp-preview-hatch`,
  `--tp-seal-ink` and `--tp-select-col-width`, and every class in the table below.

**Where each rule lives.** The plugin adds **no** SCSS. Every rule below extends Task
10's host PR. The plugin does have component `.scss` files with
`ViewEncapsulation.None`, but per CLAUDE.md and this plan none of these rules go there.

| Rule group | Extends Task 10 by | Used by |
|---|---|---|
| Tokens (`:root` + `body.theme-dark`) | **correcting** Task 10's dark block; adding `--tp-preview-hatch` | all |
| `.locked-background` / `.reconciled-background` | Task 10's rules plus text colour (`.plan-text` *and* `.comment`) and a glyph-aware dim | Task 9 |
| `.tp-day-glyph*` | new | 9A |
| `.tp-seal` | new: filled Rounded seal on every theme | 9A, 11A, 11B |
| `.tp-lock-legend*` | new | 9A |
| `.tp-day-header-btn` | new | 12A |
| `.tp-preview-*`, `.tp-reconcile-scope*` | new | 12A |
| `.time-dashboard` selection column | new: sticky checkbox column; Name shifted right | 12A |
| `.tp-dialog-footer`, `.tp-footer-*`, `.tp-unlock-word` | new | 11A, 11B |

**What this block changes in Task 10's rules.**
1. **The dark selector.** It keys off `body.theme-dark` (fact 7). Task 10 used
   `prefers-color-scheme` and `data-theme`, which this app does not use.
2. **Text in dark mode.** The locked ground `#262B29` sits under the state classes'
   hard-coded text colour: `#0F1316` on green, and `--text-header` on grey and white.
   That applies to `.plan-text` and also to `.comment`, which inherits
   `#0F1316 !important` from `.X-background .plan-container`. On the neutral lock
   ground the state text colour is only decoration anyway, because colour is never
   load-bearing (§8.1). The text becomes `--tp-text`, set on the container, on
   `.plan-text` and on `.comment`.
3. **The dim.** Task 10 dims the whole `.plan-content`, which would also dim the lock
   glyph. The dim now skips the glyph.

**The seal on every theme (fact 6).** Theme-workspace's rule
`body.theme-workspace .mat-icon.filled` has specificity (0,3,1) and forces Outlined,
which is loaded with FILL 0. `body .mat-icon.material-symbols-rounded.filled.tp-seal`
has specificity (0,4,1). It wins regardless of source order, and both sides use
`!important`.

**The selection column (fact 13).** The checkbox column is made sticky, and the pinned
Name column moves right by the checkbox column's 60px. I did not unpin Name instead.
In a grid a week or a month wide, Name is the only thing on screen that says whose row
a cell belongs to, and 12A's preview is read row by row.

**Palette check.**
- No yellow outline, which belongs to `.highlight-cell`.
- No blue, which belongs to `.setting-ico.active`.
- The preview uses a *dashed* outline in `--tp-seal-ink` (green-teal). That is the
  same channel as `.highlight-cell`, but in another colour and texture, and it sits on
  `.plan-container` rather than on the `<td>`.

**Known spec tension, left as the spec says.** §8.1 gives a locked cell the cursor
`not-allowed`, but §8.5 says the cell still opens a read-only dialog when clicked.

- [ ] **Step 1: Replace Task 10's block with this one**

```scss
/* ---------------------------------------------------------------------------
   Time planning: reconciled ("Afstemt") day lock. Plugin templates:
   time-plannings-table, time-plannings-container, workday-entity-dialog.

   Four independent channels carry the state (texture, glyph, cursor and tooltip
   text), so colour is never load-bearing. The 3px right border on a reconciled
   cell draws the staircase boundary down the grid.

   Theme-agnostic on purpose: body.theme-eform rules do not apply under
   body.theme-workspace, and these must read on both. Dark mode in this app is the
   body.theme-dark class (full-layout.component.ts), NOT prefers-color-scheme.
   Palette: no yellow outline (.highlight-cell) and no blue (.setting-ico.active).
   --------------------------------------------------------------------------- */
:root {
  --tp-locked-bg: #E4E7E4;
  --tp-locked-hatch: rgba(22, 33, 30, 0.055);
  --tp-preview-hatch: rgba(47, 93, 80, 0.16);
  --tp-seal-ink: #2F5D50;
}

body.theme-dark {
  --tp-locked-bg: #262B29;
  --tp-locked-hatch: rgba(230, 234, 232, 0.06);
  --tp-preview-hatch: rgba(127, 209, 185, 0.18);
  --tp-seal-ink: #7FD1B9;
}

// A mixin, not a custom property holding the gradient. A custom property that
// contains var() is resolved where it is declared (:root), so the body.theme-dark
// hatch override would never reach it.
@mixin tp-locked-texture {
  background: repeating-linear-gradient(135deg,
      var(--tp-locked-hatch) 0 2px, transparent 2px 6px),
      var(--tp-locked-bg) !important;
}

.locked-background .plan-container {
  @include tp-locked-texture;
  /* Beats the shared `.plan-container, .progress-container { cursor: pointer }`
     rule by specificity. Scoped by .locked-background, so the avatar and progress
     circle are unaffected. */
  cursor: not-allowed !important;
}

.reconciled-background .plan-container {
  background: var(--tp-locked-bg) !important;
  border-right: 3px solid var(--tp-seal-ink) !important;
  cursor: default !important;
}

/* The state classes (green, red, grey, white) hard-code a text colour for their own
   light grounds, on .plan-text and on the container itself, which .comment
   inherits. On the neutral lock ground that colour is decoration, and in dark mode
   #0F1316 on --tp-locked-bg cannot be read. Same specificity as the state rules and
   later in the file, so these win. */
.locked-background .plan-container,
.reconciled-background .plan-container {
  color: var(--tp-text) !important;

  .plan-text,
  .plan-text strong,
  .plan-text span,
  .plan-text mat-icon,
  .comment {
    color: var(--tp-text) !important;
  }
}

/* Dimmed, not hidden: people read closed days constantly. The glyph is excluded,
   so the one element that explains the dimming is not dimmed with it. */
.locked-background .plan-content > :not(.tp-day-glyph) {
  opacity: 0.72;
}

/* Glyphs (9A). The lock is the last child of .plan-content; a flex column plus
   margin-top:auto puts it bottom-left. The seal is the last .plan-icons item,
   top-right. Neither is absolutely positioned: .neutral-icon forces
   position:relative and top:5px with !important. */
.locked-background .plan-content {
  display: flex;
  flex-direction: column;
}

.tp-day-glyph {
  display: inline-flex;
  align-self: flex-start;

  mat-icon {
    color: var(--tp-seal-ink) !important;
  }
}

.tp-day-glyph--lock {
  margin-top: auto;
}

/* The filled seal, on every theme. Outlined is loaded with FILL fixed at 0
   (index.html), and body.theme-workspace forces every .mat-icon onto Outlined with
   !important (_workspace-mat-overrides.scss). At (0,4,1) this beats that rule's
   (0,3,1) whatever the source order. */
body .mat-icon.material-symbols-rounded.filled.tp-seal {
  font-family: 'Material Symbols Rounded' !important;
  font-variation-settings: 'FILL' 1, 'wght' 400, 'GRAD' 0, 'opsz' 24 !important;
}

/* Legend under the grid (9A). Shown only while a locked day is on screen. */
.tp-lock-legend {
  display: flex;
  flex-wrap: wrap;
  gap: 8px 24px;
  margin-top: 8px;
  padding: 8px 4px;
  color: var(--tp-text);
  font-size: 13px;
}

.tp-lock-legend__item {
  display: inline-flex;
  align-items: center;
  gap: 8px;
}

.tp-lock-legend__swatch {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 32px;
  height: 22px;
  border: 1px solid var(--tp-border);
  border-radius: 3px;

  .neutral-icon {
    top: 0 !important;
    font-size: 16px !important;
    width: 16px;
    height: 16px;
    color: var(--tp-seal-ink) !important;
  }
}

.tp-lock-legend__swatch--locked {
  @include tp-locked-texture;
}

.tp-lock-legend__swatch--reconciled {
  background: var(--tp-locked-bg);
  box-shadow: inset -3px 0 0 var(--tp-seal-ink);
}

/* Clickable header of a past day column (12A). It only starts a preview, so the
   affordance stays quiet: a dotted underline on hover or keyboard focus. */
.tp-day-header-btn {
  padding: 0;
  border: 0;
  background: transparent;
  color: inherit;
  font: inherit;
  cursor: pointer;
  text-decoration: underline dotted transparent;
  text-underline-offset: 3px;

  &:hover,
  &:focus-visible {
    text-decoration-color: var(--tp-seal-ink);
  }
}

/* Bulk reconcile preview (12A): what WILL lock, drawn in place before the commit.
   Dashed because it is not true yet. It is layered over the cell's own state colour
   (background-image only), so the day stays recognisable underneath. Same
   specificity as `.X-background .plan-container`, placed later, so it wins. */
.plan-container.tp-preview-lock {
  background-image: repeating-linear-gradient(135deg,
      var(--tp-preview-hatch) 0 2px, transparent 2px 6px) !important;
  outline: 2px dashed var(--tp-seal-ink);
  outline-offset: -3px;
}

.plan-container.tp-preview-boundary {
  box-shadow: inset -3px 0 0 var(--tp-seal-ink);
}

.plan-container.tp-preview-skip {
  opacity: 0.55;
}

.tp-preview-skip-label {
  display: inline-block;
  margin-top: 2px;
  padding: 0 6px;
  border-radius: 4px;
  background: var(--tp-locked-bg);
  color: var(--tp-text);
  font-size: 11px;
  font-weight: 600;
}

/* Bulk reconcile scope bar (12A), between the toolbar and the grid. */
.tp-reconcile-scope {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 12px;
  margin: 0 0 12px;
  padding: 8px 12px;
  border: 1px solid var(--tp-border);
  border-left: 3px solid var(--tp-seal-ink);
  border-radius: 8px;
  background: var(--tp-td-bg);
  color: var(--tp-text);

  .neutral-icon {
    top: 0 !important;
    color: var(--tp-seal-ink) !important;
  }
}

.tp-reconcile-scope__text {
  display: flex;
  flex: 1 1 auto;
  flex-direction: column;
  gap: 2px;
}

/* Bulk selection column (12A). mtx-grid puts its checkbox column in front of the
   pinned Name column but never makes it sticky, so on a sideways scroll the
   checkboxes would slide under Name. Pin it too, and move Name right by its width.
   Unpinning Name instead was rejected: in a wide grid, Name is the only thing that
   says whose row a cell belongs to. */
.time-dashboard {
  --tp-select-col-width: 60px; /* mtx-grid's own .mtx-grid-checkbox-cell width */

  th.mtx-grid-checkbox-cell,
  td.mtx-grid-checkbox-cell {
    position: sticky;
    left: 0;
    z-index: 2;
    box-sizing: border-box;
    width: var(--tp-select-col-width);
    min-width: var(--tp-select-col-width);
    max-width: var(--tp-select-col-width);
    background: var(--tp-td-bg, #FFF);
  }

  /* CDK and mtx-grid write an inline left:0 on the sticky Name cells. A stylesheet
     !important beats a non-important inline style. */
  .mat-column-siteName.mat-table-sticky-left {
    left: var(--tp-select-col-width) !important;
  }
}

/* Day dialog footer (11A, 11B). It morphs in place between modes. */
.tp-dialog-footer {
  flex-wrap: wrap;
  gap: 12px;
}

.tp-footer-status,
.tp-footer-confirm,
.tp-footer-note {
  flex: 1 1 auto;
  color: var(--tp-text);
}

.tp-footer-status {
  display: flex;
  flex-direction: column;
  gap: 2px;

  > span {
    display: inline-flex;
    align-items: center;
    gap: 6px;
  }

  .neutral-icon {
    top: 0 !important;
    color: var(--tp-seal-ink) !important;
  }
}

.tp-footer-note {
  font-size: 13px;
  opacity: 0.8;
}

.tp-unlock-word {
  width: 12ch;
  height: 40px;
  padding: 0 12px;
  border: 1px solid var(--tp-border);
  border-radius: var(--rounded-full);
  background: var(--tp-td-bg);
  color: var(--tp-text);
  font: inherit;
  letter-spacing: 0.08em;
  text-transform: uppercase; /* display only; the comparison ignores case */
}
```

- [ ] **Step 2: Verify**

This SCSS compiles only inside `ng build`, which CI runs. Locally, check that:
- `git diff eform-client/src/scss/styles.scss` shows one contiguous block after :358;
- `grep -n "prefers-color-scheme" eform-client/src/scss/styles.scss` prints nothing.

In the browser (step 6 of the development cycle), check on **both** `theme-eform` and
`theme-workspace`:
- the seal renders filled;
- the checkbox column stays put while the grid scrolls sideways.

- [ ] **Step 3: Commit on Task 10's branch**

```bash
cd /home/rene/Documents/workspace/microting/eform-angular-frontend
git checkout feat/reconciled-day-lock-styles
git add eform-client/src/scss/styles.scss
git commit -m "feat(timeplanning): lock glyphs, legend, bulk preview, selection column and dialog footer styles"
```

This commit goes into Task 10's PR. Three FOSSA checks fail on every
`eform-angular-frontend` PR and do not gate the merge.

**Merge order.** The plugin's CI checks out host `stable`. The Playwright specs assert
classes, ids and text, not visuals, so they pass either way. Merge this host PR before
the plugin PR anyway, so that the browser check at step 6 of the development cycle
shows the real look.

---

## Task 11A: Two-step reconcile in the dialog footer, read-only in place (§8.2)

**Follows:** Task 11.
**Supersedes:**
- **Task 11 Step 3, the banner.** It is replaced by two hints with fixed ids. The
  bound `[helpId]` version does not fail the wiring spec; it escapes it, so its ids
  would never be checked against the registry or the exhaustive hint list (fact 9).
- **Task 11 Step 4, the footer.** `btn-secondary` fails CI's "Button conventions"
  step (fact 8). The footer also **drops Task 11's direct `#unlockButton`**: unlock
  returns in 11B with the typed word. Nothing is added here only to be deleted.
- **Task 11 Step 5, the handlers.** They close the dialog with `this.data`, which the
  table then saves on a day that is now locked.
- **Task 11 Step 6, the directory `git add`.**
- **Task 9 Step 2, `onDayColumnClick`,** merged below.

**Files:**
- Modify: `.../time-planning-actions/workday-entity/workday-entity-dialog.component.ts`
- Modify: `.../time-planning-actions/workday-entity/workday-entity-dialog.component.html` (the hint block ~:461 and `mat-dialog-actions` :494-514)
- Modify: `.../time-plannings-table/time-plannings-table.component.ts` (`onDayColumnClick` :458-485)
- Create: `eform-client/playwright/e2e/plugins/time-planning-pn/s/reconcile-dialog-confirm.spec.ts`

**Interfaces:**
- Consumes:
  - `reconcileDay(id)` (Task 8)
  - the dialog data fields and `isLocked` (Task 11 Steps 1-2)
  - `dayKey` and `formatReconciledProvenance` (9A)
  - the footer classes and `tp-seal` (10A)
  - the keys `Reconcile day`, `reconcileDayConfirm`, `reconcileNeedsSave`, `Cancel` and `Save` (13A)
- Produces:
  - `footerMode`, `lockStateChanged`, `lockRequestInFlight` and `setLockRequestInFlight()`
  - the **close contract:** after any close, the table reads `lockStateChanged` and
    reloads the grid instead of saving

**Design.**
- **Why the dialog.** It is the one place that already shows whose day, which date
  and which hours. The footer morphs in place instead of opening a second modal,
  which would cover exactly that context.
- **After a successful PUT the dialog stays open.** The same dialog turns read-only,
  and the provenance line `Afstemt <dato> kl. <tid>` sits where the actions were.
  There is no "by whom", because ReconciledBy is a declared non-goal. Until the dialog
  closes, the client clock stands in for `ReconciledAt`. The reload after the close
  brings the stored server value, which is what every later open shows.
- **Unsaved edits block reconcile** (kept at review). Reconcile writes only the flag,
  not the form, so reconciling a dirty form would freeze a view of unsaved values as
  if they were the sealed figures, and the edits would be lost. A dirty form shows a
  note in place of the button.
- **Closing mid-PUT is blocked.** While the PUT is in flight, `dialogRef.disableClose`
  is `true`. Otherwise Esc or a backdrop click could close the dialog before
  `lockStateChanged` is set, and the grid would draw a sealed day as open.
- **Every close path reloads.** The dialog can close through Cancel, Esc or the
  backdrop. Whichever path ends a dialog in which a reconcile happened, the grid must
  reload and must not save. The table reads `lockStateChanged` from the component
  instance it captured when it opened the dialog (fact 5).

- [ ] **Step 1: The banner** (replaces Task 11 Step 3)

Beside the `dayCell.futureDisabled` hint (:461-465):

```html
          <!--
            Two hints with fixed ids rather than one hint with a bound id: the help
            wiring spec only sees literal id attributes, so a bound id would escape
            its registry checks and its exhaustive hint list (Task 13A adds these two
            ids to that list). Help chrome is admin-only, so these are extras. The
            footer lines carry the copy every user sees.
          -->
          <tp-help-hint
            *ngIf="isLocked && data.isReconciled"
            helpId="dayCell.reconciled"
            data-tp-help="dayCell.reconciled"
            tone="warn"></tp-help-hint>
          <tp-help-hint
            *ngIf="isLocked && !data.isReconciled"
            helpId="dayCell.lockedByReconciled"
            data-tp-help="dayCell.lockedByReconciled"
            tone="warn"></tp-help-hint>
```

The comment deliberately never writes an id attribute with a quoted value. The wiring
spec reads comments as markup (fact 9), and 13A Step 5 greps for exactly that.

- [ ] **Step 2: The footer** (replaces Task 11 Step 4 and the original :494-514; 11B replaces it again)

```html
  <!--
    The footer morphs in place (spec §8.2): one row, several modes, no second modal.
    The dialog is the only place that shows whose day, which date and which hours,
    and a stacked confirm would cover exactly that.
    Source order keeps a .btn-cancel first. check-button-conventions.js (CI "Button
    conventions", in the build job) reads the whole row regardless of *ngIf, allows
    only btn-primary / btn-cancel / btn-delete / btn-quiet, and fails the build
    otherwise. Every new button is type="button": this row sits inside <form>, and
    an implicit submit would click the first button in the form, which is the
    history button in the title.
  -->
  <div mat-dialog-actions class="d-flex flex-row justify-content-end align-items-center tp-dialog-footer">
    <ng-container *ngIf="footerMode === 'actions'">
      <div class="tp-footer-status" *ngIf="isLocked">
        <span id="reconciledProvenance" *ngIf="data.isReconciled">
          <mat-icon fontSet="material-symbols-rounded" class="neutral-icon filled tp-seal">verified</mat-icon>
          <span id="reconciledProvenanceText">{{ reconciledProvenance }}</span>
        </span>
      </div>
      <span class="tp-footer-note" id="reconcileNeedsSave"
            *ngIf="!isLocked && canReconcile && workdayForm.dirty">
        {{ 'reconcileNeedsSave' | translate }}
      </span>

      <button
        type="button"
        class="btn-cancel"
        mat-dialog-close
        id="cancelButton"
        (click)="onCancel()">
        {{ 'Cancel' | translate }}
      </button>

      <button
        type="button"
        *ngIf="!isLocked && canReconcile && !workdayForm.dirty"
        class="btn-quiet"
        id="reconcileButton"
        (click)="onReconcileStart()">
        {{ 'Reconcile day' | translate }}
      </button>

      <tp-help-icon *ngIf="!isLocked" helpId="dayCell.save" (openInPanel)="openHelp($event)"></tp-help-icon>
      <!-- [mat-dialog-close] fires regardless of (click), so on a locked day Save must
           be out of the DOM, not merely disabled. -->
      <button
        *ngIf="!isLocked"
        class="btn-primary btn-primary--icon-left"
        id="saveButton"
        data-tp-help="dayCell.save"
        (click)="onUpdateWorkDayEntity()"
        [disabled]="workdayForm.invalid"
        [mat-dialog-close]="data"
      >
        <span>{{ 'Save' | translate }}</span>
      </button>
    </ng-container>

    <ng-container *ngIf="footerMode === 'confirmReconcile'">
      <span class="tp-footer-confirm" id="reconcileConfirmText">
        {{ 'reconcileDayConfirm' | translate: {worker: data.planningPrDayModels.siteName, date: dayLabel} }}
      </span>
      <button
        type="button"
        class="btn-cancel"
        id="reconcileCancelButton"
        [disabled]="lockRequestInFlight"
        (click)="onReconcileCancel()">
        {{ 'Cancel' | translate }}
      </button>
      <button
        type="button"
        class="btn-primary"
        id="reconcileConfirmButton"
        [disabled]="lockRequestInFlight"
        (click)="onReconcileConfirm()">
        {{ 'Reconcile day' | translate }}
      </button>
    </ng-container>
  </div>
```

The buttons run Cancel, then Reconcile (quiet), then Save (primary). Save stays at the
right edge, under the cursor, where it has always been.

- [ ] **Step 3: The handlers** (replace Task 11 Step 5)

In `workday-entity-dialog.component.ts`, add these imports:

```ts
import {format} from 'date-fns';
import {dayKey, formatReconciledProvenance} from '../../day-lock.util';
```

Do **not** add Task 11's `canReconcile`, `onReconcile()` or `onUnlock()`. Add:

```ts
  // ---- Reconcile footer (spec §8.2) ----------------------------------------

  /** The footer morphs in place instead of opening a second modal. */
  footerMode: 'actions' | 'confirmReconcile' | 'confirmUnlock' = 'actions';

  /**
   * Set once a reconcile or unlock has reached the server. After the close, whatever
   * its path (Cancel, Esc, backdrop), the table reads this and reloads the grid
   * instead of treating the close payload as a save.
   */
  lockStateChanged = false;

  /** True while a reconcile or unlock PUT is in flight. */
  lockRequestInFlight = false;

  /** Captured at construction so it can be restored after the PUT. */
  private readonly defaultDisableClose = this.dialogRef.disableClose;

  /**
   * While the PUT is in flight the dialog cannot be dismissed. Otherwise Esc or a
   * backdrop click would close it before lockStateChanged is set, and the grid would
   * go on drawing a day the server has just sealed as open.
   */
  private setLockRequestInFlight(inFlight: boolean): void {
    this.lockRequestInFlight = inFlight;
    this.dialogRef.disableClose = inFlight || this.defaultDisableClose;
  }

  /** I2: today and future days stay open so time can still be registered. */
  get canReconcile(): boolean {
    const day = dayKey(this.data.planningPrDayModels.date);
    return !!this.data.planningPrDayModels.id && day !== null && day < dayKey(new Date());
  }

  /** "Afstemt 14.09.2026 kl. 10:32", shown where the actions were. */
  get reconciledProvenance(): string {
    return formatReconciledProvenance(
      this.data.planningPrDayModels.reconciledAt, this.datePipe, this.translateService);
  }

  get dayLabel(): string {
    return this.datePipe.transform(this.data.planningPrDayModels.date, 'dd.MM.yyyy') ?? '';
  }

  onReconcileStart(): void {
    if (!this.isLocked && this.canReconcile && !this.workdayForm.dirty) {
      this.footerMode = 'confirmReconcile';
    }
  }

  onReconcileCancel(): void {
    this.footerMode = 'actions';
  }

  onReconcileConfirm(): void {
    if (this.lockRequestInFlight) {
      return;
    }
    // An edit made while the confirm was showing would be frozen unsaved. Go back to
    // the actions, where the "save first" note explains why.
    if (this.workdayForm.dirty) {
      this.footerMode = 'actions';
      return;
    }
    this.setLockRequestInFlight(true);
    this.planningsService.reconcileDay(this.data.planningPrDayModels.id).subscribe({
      next: result => {
        if (result && result.success) {
          this.applyReconciledInPlace();
        } else {
          // ApiBaseService has already shown the server's message as a toast.
          this.footerMode = 'actions';
        }
        this.setLockRequestInFlight(false);
      },
      error: () => {
        this.setLockRequestInFlight(false);
        this.footerMode = 'actions';
      },
    });
  }

  /**
   * Turns this open dialog read-only instead of closing it (spec §8.2). The server
   * stamped ReconciledAt a moment ago. The client clock stands in for it until the
   * close, and the reload that follows brings the stored value for every later open.
   */
  private applyReconciledInPlace(): void {
    const day = this.data.planningPrDayModels;
    day.reconciled = true;
    day.reconciledAt = format(new Date(), "yyyy-MM-dd'T'HH:mm:ss");
    this.data.isReconciled = true;
    // Only open days above the boundary offer reconcile, so this day IS the new boundary.
    this.data.lockedThrough = day.date;
    this.isLocked = true;
    // Task 11's setDisabled guard stops every later cascade call from re-enabling a control.
    this.workdayForm.disable({emitEvent: false});
    this.lockStateChanged = true;
    this.footerMode = 'actions';
  }
```

`lockStateChanged` is set **before** `setLockRequestInFlight(false)` re-enables Esc and
the backdrop. That ordering is what the guard exists for.

- [ ] **Step 4: The table's side of the close contract** (replaces Task 9 Step 2)

The final `onDayColumnClick` in `time-plannings-table.component.ts`:

```ts
  onDayColumnClick(row: any, field: string): void {
    const siteId = row.siteId;
    const cellData = R.clone(row.planningPrDayModels[field]);
    this.timePlanningPnSettingsService.getAssignedSite(siteId).subscribe(result => {
      if (result && result.success) {
        const dialogRef = this.dialog.open(WorkdayEntityDialogComponent, {
          data: {
            planningPrDayModels: cellData,
            assignedSiteModel: result.model,
            tags: row.tags ?? [],
            isLocked: this.isDayLocked(row, field),
            isReconciled: this.isDayReconciled(row, field),
            lockedThrough: row.lockedThrough ?? null,
          },
          minWidth: 1024,
          minHeight: 500,
          maxWidth: '95vw',
          maxHeight: '95vh',
          panelClass: 'time-planning-dialog'
        });
        // Captured now, because MatDialogRef sets componentInstance to null on close,
        // and every close path must be able to report a reconcile or unlock that has
        // already reached the server.
        const dialog = dialogRef.componentInstance;
        dialogRef.afterClosed().subscribe((data: any) => {
          if (dialog?.lockStateChanged) {
            // Already persisted by the dialog's own PUT. Never fall through to
            // updatePlanning: the day is now locked, and the save would be refused.
            this.pendingHighlight = { siteId, field };
            this.highlightApplied = false;
            this.waitingForFreshData = true;
            this.timePlanningChanged.emit(null);
            return;
          }
          if (data !== '' && data !== undefined) {
            this.pendingHighlight = { siteId, field };
            this.highlightApplied = false;
            this.waitingForFreshData = true;
            this.planningsService.updatePlanning(data.planningPrDayModels, data.planningPrDayModels.id).subscribe(result => {
              if (result && result.success) {
                this.timePlanningChanged.emit(data);
              }
            });
          }
        });
      }
    });
  }
```

The container's `onTimePlanningChanged` ignores its argument and calls
`getPlannings()`, so emitting `null` is enough.

- [ ] **Step 5: Playwright, two-step confirm and read-only in place**

`eform-client/playwright/e2e/plugins/time-planning-pn/s/reconcile-dialog-confirm.spec.ts`:

```ts
import { test, expect } from '@playwright/test';
import { LoginPage } from '../../../Page objects/Login.page';
import {
  closeDayAfterLockChange, closeDayWithoutChange, countPuts, openDashboardLastWeek, openDay,
  PLANNING_PUT_PATH, PROVENANCE, RECONCILE_PATH, reconcileOpenDay, tdOf, unlockDay, workerAtRow,
} from './reconcile-helpers';

/** Spec §8.2. The worker is grid row 6 at the start and is found by name after that. */
test.describe('Reconciled day lock: dialog confirm', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('http://localhost:4200');
    await new LoginPage(page).login();
    await openDashboardLastWeek(page);
  });

  test('reconcile takes a second click in the same footer, then the dialog stays open read-only', async ({ page }) => {
    const worker = await workerAtRow(page, 6);
    const reconciles = countPuts(page, RECONCILE_PATH);

    const date = await openDay(page, worker, 3);
    await expect(page.locator('#saveButton')).toBeVisible();
    await expect(page.locator('#CommentOffice')).toBeEnabled();

    // The first click morphs the footer. It commits nothing and opens no second modal.
    await page.locator('#reconcileButton').click();
    await expect(page.locator('#reconcileConfirmText')).toContainText(worker);
    await expect(page.locator('#reconcileConfirmText')).toContainText(date);
    await expect(page.locator('#saveButton')).toHaveCount(0);
    await expect(page.locator('mat-dialog-container')).toHaveCount(1);
    expect(reconciles.count).toBe(0);

    // Backing out restores the normal footer, and still nothing is sent.
    await page.locator('#reconcileCancelButton').click();
    await expect(page.locator('#saveButton')).toBeVisible();
    expect(reconciles.count).toBe(0);

    // The second click commits. The SAME dialog turns read-only, with the provenance
    // line where the actions were.
    await reconcileOpenDay(page);
    expect(reconciles.count).toBe(1);
    await expect(page.locator('mat-dialog-container')).toHaveCount(1);
    await expect(page.locator('#saveButton')).toHaveCount(0);
    await expect(page.locator('#reconcileButton')).toHaveCount(0);
    await expect(page.locator('#reconciledProvenanceText')).toHaveText(PROVENANCE);
    await expect(page.locator('#CommentOffice')).toBeDisabled();

    // Closing reloads the grid and does not save.
    await closeDayAfterLockChange(page);
    await expect(tdOf(page, worker, 3)).toHaveClass(/reconciled-background/);
    await expect(tdOf(page, worker, 2)).toHaveClass(/locked-background/);
    await expect(tdOf(page, worker, 4)).not.toHaveClass(/locked-background|reconciled-background/);

    // A later open shows the server's stored ReconciledAt.
    await openDay(page, worker, 3);
    await expect(page.locator('#reconciledProvenanceText')).toHaveText(PROVENANCE);
    await closeDayWithoutChange(page);

    // Cleanup.
    await unlockDay(page, worker, 3);
    await expect(tdOf(page, worker, 3)).not.toHaveClass(/reconciled-background/);
  });

  test('a day with unsaved edits offers no reconcile, and Cancel writes nothing', async ({ page }) => {
    const worker = await workerAtRow(page, 6);
    await openDay(page, worker, 1);
    await expect(page.locator('#reconcileButton')).toBeVisible();

    await page.locator('#CommentOffice').fill('reconcile-guard');
    await expect(page.locator('#reconcileButton')).toHaveCount(0);
    await expect(page.locator('#reconcileNeedsSave')).toBeVisible();

    // Cancel closes with '': no save, no reconcile, no write of any kind.
    const writes = countPuts(page, PLANNING_PUT_PATH);
    await closeDayWithoutChange(page);
    await page.waitForTimeout(1000);
    expect(writes.count, 'Cancel must not send a save or a reconcile').toBe(0);
  });
});
```

- [ ] **Step 6: Verify the button row locally**

This is the same checker CI runs, pointed at the source tree. It is a lint script, not
a test runner:

```bash
node /home/rene/Documents/workspace/microting/eform-angular-frontend/eform-client/scripts/check-button-conventions.js \
  /home/rene/Documents/workspace/microting/eform-angular-timeplanning-plugin/eform-client/src/app/plugins/modules/time-planning-pn
```

Expected output: `Button conventions: OK`.

- [ ] **Step 7: Task 11's own commit, staged by name** (replaces Task 11 Step 6)

Run this only if Task 11's commit has not been made yet:

```bash
cd /home/rene/Documents/workspace/microting/eform-angular-timeplanning-plugin
git add eform-client/src/app/plugins/modules/time-planning-pn/components/plannings/time-planning-actions/workday-entity/workday-entity-dialog.component.ts \
        eform-client/src/app/plugins/modules/time-planning-pn/components/plannings/time-planning-actions/workday-entity/workday-entity-dialog.component.html
git commit -m "feat(lock): open locked days read-only, with reconcile and unlock"
```

- [ ] **Step 8: Commit 11A**

```bash
cd /home/rene/Documents/workspace/microting/eform-angular-timeplanning-plugin
git add eform-client/src/app/plugins/modules/time-planning-pn/components/plannings/time-planning-actions/workday-entity/workday-entity-dialog.component.ts \
        eform-client/src/app/plugins/modules/time-planning-pn/components/plannings/time-planning-actions/workday-entity/workday-entity-dialog.component.html \
        eform-client/src/app/plugins/modules/time-planning-pn/components/plannings/time-plannings-table/time-plannings-table.component.ts \
        eform-client/playwright/e2e/plugins/time-planning-pn/s/reconcile-dialog-confirm.spec.ts
git commit -m "feat(lock): two-step reconcile in the day dialog, read-only in place"
```

---

## Task 11B: Typed-word unlock and "free this day first" (§8.4)

**Follows:** Task 11A.
**Supersedes:** 11A Step 2 in full. This task has the final `mat-dialog-actions`.

**Files:**
- Modify: `.../workday-entity/workday-entity-dialog.component.ts`
- Modify: `.../workday-entity/workday-entity-dialog.component.html` (`mat-dialog-actions`)
- Create: `eform-client/playwright/e2e/plugins/time-planning-pn/s/reconcile-unlock-word.spec.ts`

**Interfaces:**
- Consumes:
  - `unreconcileDay(id)` (Task 8)
  - `data.lockedThrough` (Task 9, merged in 11A)
  - `dayKey` (9A)
  - `footerMode`, `lockStateChanged`, `lockRequestInFlight` and `setLockRequestInFlight()` (11A)
  - the keys `UNLOCK`, `unlockTypeWordPrompt`, `unlockFreeFirst` and `Unlock` (13A)
- Produces: `isBoundaryDay`, `freeFirstDate`, `unlockWord`, `unlockWordMatches`,
  `onUnlockStart()`, `onUnlockCancel()` and `onUnlockConfirm()`.

**Only the boundary day offers unlock.**
- **The boundary** is the row's `lockedThrough`. Unlocking it moves the line back one
  notch.
- **Why no other day.** The lock is derived: every day at or before `lockedThrough` is
  locked. Unlocking a lower day would need an editable day under a later sealed one,
  and a derived lock cannot represent that. The server rejects it (Task 4).
- **What a lower day shows instead.** It names the day to free first:
  `Låst, fordi <dato> er afstemt. Lås <dato> op først.` That replaces a disabled
  control with no explanation.
- **A reconciled day that is not the boundary** (an older month-end below a newer
  one) shows both its provenance line and that "free first" line.

**Why sealing takes a click and unsealing takes a word.** The two directions do not
carry the same risk.
- **Sealing is cheap to undo and fails safe.** At worst a period is frozen a day
  early, and one unlock moves the line back.
- **Unsealing reopens settled figures.** They have usually already gone to payroll or
  been checked against it. Unsealing exposes them again to edits, and to the
  recalculation paths that Task 5 made skip locked days.
- **Nothing else guards the reverse direction.** Any web user may reconcile (§8.6).
  The reverse-order rule plus this confirmation is the only safeguard on unlocking.
- **A click can be made by reflex; a word cannot.** A second click can be made without
  thinking, because the confirm appears where the first button was. Typing a word
  cannot, and it makes the user read the sentence that says what is about to happen.

**How the word is checked.**
- **Case and whitespace are ignored.** The friction should be the word, not the Shift
  key.
- **The word is localized:** `LÅS OP` in Danish, `UNLOCK` in English.
- **Untranslated locales still work.** The key is `UNLOCK`, and its value in those
  locales is `UNLOCK`, so a missing translation still gives a word the user can type.

- [ ] **Step 1: The final footer** (replaces 11A Step 2)

```html
  <!--
    The footer morphs in place (spec §8.2, §8.4): one row, three modes, no second
    modal. Source order keeps a .btn-cancel first, as check-button-conventions.js
    requires for the whole row regardless of *ngIf. Every new button is
    type="button" because this row sits inside <form>.
  -->
  <div mat-dialog-actions class="d-flex flex-row justify-content-end align-items-center tp-dialog-footer">
    <ng-container *ngIf="footerMode === 'actions'">
      <!-- What the day IS, in plain text every user sees (help hints are admin-only). -->
      <div class="tp-footer-status" *ngIf="isLocked">
        <span id="reconciledProvenance" *ngIf="data.isReconciled">
          <mat-icon fontSet="material-symbols-rounded" class="neutral-icon filled tp-seal">verified</mat-icon>
          <span id="reconciledProvenanceText">{{ reconciledProvenance }}</span>
        </span>
        <span id="lockedFreeFirst" *ngIf="!isBoundaryDay">
          <mat-icon fontSet="material-symbols-outlined" class="neutral-icon">lock</mat-icon>
          <span id="lockedFreeFirstText">{{ 'unlockFreeFirst' | translate: {date: freeFirstDate} }}</span>
        </span>
      </div>
      <span class="tp-footer-note" id="reconcileNeedsSave"
            *ngIf="!isLocked && canReconcile && workdayForm.dirty">
        {{ 'reconcileNeedsSave' | translate }}
      </span>

      <button
        type="button"
        class="btn-cancel"
        mat-dialog-close
        id="cancelButton"
        (click)="onCancel()">
        {{ 'Cancel' | translate }}
      </button>

      <button
        type="button"
        *ngIf="!isLocked && canReconcile && !workdayForm.dirty"
        class="btn-quiet"
        id="reconcileButton"
        (click)="onReconcileStart()">
        {{ 'Reconcile day' | translate }}
      </button>

      <button
        type="button"
        *ngIf="isBoundaryDay"
        class="btn-quiet"
        id="unlockButton"
        (click)="onUnlockStart()">
        {{ 'Unlock' | translate }}
      </button>

      <tp-help-icon *ngIf="!isLocked" helpId="dayCell.save" (openInPanel)="openHelp($event)"></tp-help-icon>
      <!-- [mat-dialog-close] fires regardless of (click), so on a locked day Save must
           be out of the DOM, not merely disabled. -->
      <button
        *ngIf="!isLocked"
        class="btn-primary btn-primary--icon-left"
        id="saveButton"
        data-tp-help="dayCell.save"
        (click)="onUpdateWorkDayEntity()"
        [disabled]="workdayForm.invalid"
        [mat-dialog-close]="data"
      >
        <span>{{ 'Save' | translate }}</span>
      </button>
    </ng-container>

    <ng-container *ngIf="footerMode === 'confirmReconcile'">
      <span class="tp-footer-confirm" id="reconcileConfirmText">
        {{ 'reconcileDayConfirm' | translate: {worker: data.planningPrDayModels.siteName, date: dayLabel} }}
      </span>
      <button
        type="button"
        class="btn-cancel"
        id="reconcileCancelButton"
        [disabled]="lockRequestInFlight"
        (click)="onReconcileCancel()">
        {{ 'Cancel' | translate }}
      </button>
      <button
        type="button"
        class="btn-primary"
        id="reconcileConfirmButton"
        [disabled]="lockRequestInFlight"
        (click)="onReconcileConfirm()">
        {{ 'Reconcile day' | translate }}
      </button>
    </ng-container>

    <ng-container *ngIf="footerMode === 'confirmUnlock'">
      <label class="tp-footer-confirm" for="unlockWordInput" id="unlockPrompt">
        {{ 'unlockTypeWordPrompt' | translate: {word: unlockWord} }}
      </label>
      <!-- [formControl] is standalone: it can sit inside the [formGroup] without
           registering in it, and workdayForm.disable() does not reach it.
           preventDefault on Enter stops the implicit form submit. -->
      <input
        #unlockWordInput
        id="unlockWordInput"
        class="tp-unlock-word"
        type="text"
        autocomplete="off"
        spellcheck="false"
        [formControl]="unlockWordCtrl"
        (keydown.enter)="$event.preventDefault(); onUnlockConfirm()">
      <button
        type="button"
        class="btn-cancel"
        id="unlockCancelButton"
        [disabled]="lockRequestInFlight"
        (click)="onUnlockCancel()">
        {{ 'Cancel' | translate }}
      </button>
      <button
        type="button"
        class="btn-delete"
        id="unlockConfirmButton"
        [disabled]="!unlockWordMatches || lockRequestInFlight"
        (click)="onUnlockConfirm()">
        {{ 'Unlock' | translate }}
      </button>
    </ng-container>
  </div>
```

- [ ] **Step 2: The handlers**

In `workday-entity-dialog.component.ts`, add `ElementRef` to the `@angular/core`
import. `ViewChild` is already there:

```ts
import {Component, ElementRef, OnInit, TemplateRef, ViewChild,
  inject, OnDestroy
} from '@angular/core';
```

Add:

```ts
  // ---- Unlock (spec §8.4) ------------------------------------------------------

  readonly unlockWordCtrl = new FormControl<string>('', {nonNullable: true});

  @ViewChild('unlockWordInput') private unlockWordInput?: ElementRef<HTMLInputElement>;

  /** Only the boundary day offers unlock. It moves the line back one notch. */
  get isBoundaryDay(): boolean {
    return this.isLocked
      && this.data.isReconciled === true
      && dayKey(this.data.planningPrDayModels.date) === dayKey(this.data.lockedThrough);
  }

  /** The day to free first, for every other locked day: the row's boundary. */
  get freeFirstDate(): string {
    return this.datePipe.transform(this.data.lockedThrough, 'dd.MM.yyyy') ?? '';
  }

  /** The localized word to type. The key doubles as its own fallback in untranslated locales. */
  get unlockWord(): string {
    return this.translateService.instant('UNLOCK');
  }

  /** Case and spacing are not the friction; the word is. */
  get unlockWordMatches(): boolean {
    const norm = (value: string) => value.trim().replace(/\s+/g, ' ').toLocaleUpperCase();
    return norm(this.unlockWordCtrl.value) === norm(this.unlockWord);
  }

  onUnlockStart(): void {
    if (!this.isBoundaryDay) {
      return;
    }
    this.unlockWordCtrl.setValue('');
    this.footerMode = 'confirmUnlock';
    // The input exists only once this change-detection pass has rendered the mode.
    setTimeout(() => this.unlockWordInput?.nativeElement.focus());
  }

  onUnlockCancel(): void {
    this.unlockWordCtrl.setValue('');
    this.footerMode = 'actions';
  }

  onUnlockConfirm(): void {
    if (!this.unlockWordMatches || this.lockRequestInFlight) {
      return;
    }
    this.setLockRequestInFlight(true);
    this.planningsService.unreconcileDay(this.data.planningPrDayModels.id).subscribe({
      next: result => {
        if (result && result.success) {
          // The day is editable again, but this form was built locked. The only way
          // back to a form whose enable/disable cascade ran from a clean start is to
          // close and reopen from a reloaded grid. lockStateChanged is set before
          // the guard lifts, so no close path can miss it.
          this.lockStateChanged = true;
          this.setLockRequestInFlight(false);
          this.dialogRef.close();
          return;
        }
        // On failure (a newer boundary appeared meanwhile, say) stay in this mode.
        // ApiBaseService has already toasted the server's message, which names the
        // day to free first.
        this.setLockRequestInFlight(false);
      },
      error: () => {
        this.setLockRequestInFlight(false);
      },
    });
  }
```

- [ ] **Step 3: Playwright, typed-word unlock**

`eform-client/playwright/e2e/plugins/time-planning-pn/s/reconcile-unlock-word.spec.ts`:

```ts
import { test, expect } from '@playwright/test';
import { LoginPage } from '../../../Page objects/Login.page';
import {
  closeDayWithoutChange, expectSuccess, lastWeekMonday, openDashboardLastWeek, openDay, reconcileDay,
  tdOf, UNLOCK_WORD, UNRECONCILE_PATH, waitForIndex, waitForPut, waitForSpinner, workerAtRow,
} from './reconcile-helpers';

/** Spec §8.4. The worker is grid row 9 at the start and is found by name after that. */
test.describe('Reconciled day lock: unlock', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('http://localhost:4200');
    await new LoginPage(page).login();
    await openDashboardLastWeek(page);
  });

  test('only the boundary offers unlock, earlier days name it, and unlocking takes the word', async ({ page }) => {
    const worker = await workerAtRow(page, 9);
    const boundaryDate = await reconcileDay(page, worker, 3);

    // A day below the boundary names the day to free first and offers no unlock.
    await openDay(page, worker, 1);
    await expect(page.locator('#lockedFreeFirstText'))
      .toHaveText(`Låst, fordi ${boundaryDate} er afstemt. Lås ${boundaryDate} op først.`);
    await expect(page.locator('#unlockButton')).toHaveCount(0);
    await expect(page.locator('#saveButton')).toHaveCount(0);
    await closeDayWithoutChange(page);

    // The boundary: its provenance, no "free first" line, and an unlock gated by the word.
    await openDay(page, worker, 3);
    await expect(page.locator('#reconciledProvenanceText')).toBeVisible();
    await expect(page.locator('#lockedFreeFirst')).toHaveCount(0);

    await page.locator('#unlockButton').click();
    await expect(page.locator('#unlockPrompt')).toContainText(UNLOCK_WORD);
    await expect(page.locator('#unlockWordInput')).toBeFocused();
    const confirm = page.locator('#unlockConfirmButton');
    await expect(confirm).toBeDisabled();
    await page.locator('#unlockWordInput').fill('LÅS');
    await expect(confirm).toBeDisabled();
    await page.locator('#unlockWordInput').fill('  lås   op ');
    await expect(confirm).toBeEnabled();

    // Backing out keeps the day reconciled.
    await page.locator('#unlockCancelButton').click();
    await expect(page.locator('#unlockWordInput')).toHaveCount(0);
    await expect(page.locator('#reconciledProvenanceText')).toBeVisible();

    // Enter in the field confirms. It must not submit the surrounding form, whose first
    // button is the version-history button in the title.
    await page.locator('#unlockButton').click();
    await page.locator('#unlockWordInput').fill(UNLOCK_WORD);
    const put = waitForPut(page, UNRECONCILE_PATH);
    const reload = waitForIndex(page, lastWeekMonday());
    await page.locator('#unlockWordInput').press('Enter');
    await expectSuccess(await put);
    await reload;
    await waitForSpinner(page);
    await expect(page.locator('app-version-history-modal')).toHaveCount(0);

    // The line moved back: both days are open again.
    await expect(tdOf(page, worker, 3)).not.toHaveClass(/reconciled-background/);
    await expect(tdOf(page, worker, 1)).not.toHaveClass(/locked-background/);
  });
});
```

- [ ] **Step 4: Verify the button row locally**

```bash
node /home/rene/Documents/workspace/microting/eform-angular-frontend/eform-client/scripts/check-button-conventions.js \
  /home/rene/Documents/workspace/microting/eform-angular-timeplanning-plugin/eform-client/src/app/plugins/modules/time-planning-pn
```

Expected output: `Button conventions: OK`.

- [ ] **Step 5: Commit**

```bash
cd /home/rene/Documents/workspace/microting/eform-angular-timeplanning-plugin
git add eform-client/src/app/plugins/modules/time-planning-pn/components/plannings/time-planning-actions/workday-entity/workday-entity-dialog.component.ts \
        eform-client/src/app/plugins/modules/time-planning-pn/components/plannings/time-planning-actions/workday-entity/workday-entity-dialog.component.html \
        eform-client/playwright/e2e/plugins/time-planning-pn/s/reconcile-unlock-word.spec.ts
git commit -m "feat(lock): unlock the boundary day with a typed word; name the day to free first"
```

---

## Task 12A: Bulk reconcile with an in-place preview (§8.3)

**Follows:** Task 12.
**Supersedes:**
- **Task 12 Step 1** in full: the grid inputs, `onRowSelected`, `stopRowClick`, the
  day-cell line, and the `ngOnChanges` re-emit.
- **Task 12 Step 2:** the toolbar markup.
- **Task 12 Step 3:** the container logic.
- **Task 12 Step 4:** the directory `git add`.
- **9A Step 3:** its `ngOnChanges` addition, merged below.

**Files:**
- Modify: `.../time-plannings-table/time-plannings-table.component.ts`
- Modify: `.../time-plannings-table/time-plannings-table.component.html`
- Modify: `.../time-plannings-container/time-plannings-container.component.ts`
- Modify: `.../time-plannings-container/time-plannings-container.component.html`
- Modify: `.../time-plannings-container/time-plannings-container.component.spec.ts`
- Create: `eform-client/playwright/e2e/plugins/time-planning-pn/s/reconcile-bulk-preview.spec.ts`

**Interfaces:**
- Consumes:
  - `reconcileThrough(date, siteIds)` (Task 8)
  - `ReconcileThroughResultModel` (8A)
  - `isDayLocked` (Task 9)
  - `dayKey`, `buildReconcilePreview` and `ReconcilePreview` (9A, unchanged)
  - classes from 10A
  - keys from 13A
- Produces:
  - table: `@Input reconcilePreview`, plus the outputs `selectionChanged` (Task 12's
    name, kept), `selectionReset` and `reconcileDateRequested`
  - container: `onReconcileDateChanged`, `onSelectionChanged`, `onSelectionReset`,
    `confirmReconcileThrough` and `cancelReconcilePreview`

**Scope = target date × set of workers.**
- **The date** comes from clicking a past day-column header (spec §8.3, "Selected
  column") or from the toolbar date field. Both only **preview**.
- **The commit** is one button, in a scope bar under the toolbar. The bar names the
  worker count and the target date.
- **The workers** are the ticked rows. With nothing ticked, they are every worker
  currently visible.

**The preview.**
- **The rules** mirror Task 4, per worker, from `row.planningPrDayModels` and
  `row.lockedThrough` (see `buildReconcilePreview`, 9A).
- **A row already at or past the target is shown as skipped.** It gets a
  "Springes over" label and is dimmed.
- **A locked row is drawn from its boundary up to its landing day.** Every cell after
  its existing boundary, up to and including that day, gets `tp-preview-lock`, and the
  landing day also gets `tp-preview-boundary`.
- **A row the preview cannot draw is not sent.**
- **The preview must be on screen** (kept at review). The date field's `min` is the
  first visible day. Its `max` is the earlier of the last visible day and yesterday
  (I2). §8.3's preview needs the cells on screen.
- **The classes go on the template, not `getCellClass`**, because mtx-grid's `colClass`
  pipe is pure (fact 1).

**A preview never widens silently.** An earlier revision cleared the ticked rows on any
reload and then rebuilt the preview. The empty selection then fell back to "every
visible worker", so a day save, the assigned-site dialog, Reload, a tag filter, the
resigned toggle or a language switch would quietly move the scope from N ticked
workers to all of them. A mistaken commit would then cost one typed-word unlock per
worker.
- **Nothing ticked:** the preview is rebuilt on the new rows. The scope is still
  "everyone visible", so it does not widen.
- **Rows were ticked:** when the grid drops the ticked rows, the preview is
  **cancelled**, never rebuilt.
- **Reloads** (`applyPlannings`) decide from `hadSelection`.
- **The grid's own resets** (language switch, new columns) arrive as a separate
  `selectionReset` output, and the container cancels.
- **Unticking the last row by hand** is the one case that goes back to "everyone
  visible". It is a deliberate spec rule, and the scope bar's count changes where the
  user can see it.

**The two mtx-grid gotchas, handled at the source.**
1. **A click selects the row.** `[disableRowClickSelection]="true"` makes `_selectRow`
   skip the selection while still emitting `rowClick` (fact 3). Task 12 fixed only the
   day cell; the Name column's click (the assigned-site dialog) would still have
   cleared the batch. Rows are selected by their checkbox only. 10A makes that column
   sticky.
2. **The selection is rebuilt empty, silently.** mtx-grid does this on *any* input
   change (fact 2).
   - The table emits `selectionReset` from `ngOnChanges` (`timePlannings`) and from
     `updateTableHeaders` (`[columns]` and `[headerTemplate]`), but only when a
     selection was active.
   - The container clears its scope itself inside `applyPlannings`, before change
     detection runs. The scope bar therefore never changes after it has been checked,
     so there is no NG0100 in a dev build.

**The result toast.** It reads the corrected per-site model:
- `applied`;
- skipped = `skippedAlreadyFurtherForward.length + skippedNoRegistration.length`;
- an "Uændret: n" suffix when `alreadyReconciledSiteIds` is not empty. This is kept
  at review, because it reports AlreadyReconciled.

It is a success toast when `applied > 0` and a warning otherwise.

**Header affordance** (scope ruling). The past-day header is a plain text button with a
dotted underline on hover. The hover lock icon from the earlier revision is dropped,
because nobody asked for it.

- [ ] **Step 1: Table component TS**

In `time-plannings-table.component.ts`, extend 9A's util import:

```ts
import {dayKey, formatReconciledProvenance, ReconcilePreview} from '../day-lock.util';
```

Add these members. Do **not** add Task 12's `stopRowClick`:

```ts
  /** The bulk preview from the container (spec §8.3). Null when none is on screen. */
  @Input() reconcilePreview: ReconcilePreview | null = null;
  /** Task 12: the ticked rows as site ids, emitted when the user ticks or unticks. */
  @Output() selectionChanged: EventEmitter<number[]> = new EventEmitter<number[]>();
  /** The grid dropped a non-empty selection by itself (mtx-grid gotcha 2). */
  @Output() selectionReset: EventEmitter<void> = new EventEmitter<void>();
  /** A past day header was clicked: preview a reconcile through that day. Commits nothing. */
  @Output() reconcileDateRequested: EventEmitter<Date> = new EventEmitter<Date>();

  @ViewChild('reconcileDayHeaderTemplate', {static: true}) reconcileDayHeaderTemplate!: TemplateRef<any>;

  /**
   * The mtx-grid [headerTemplate] map. Only past day columns get the clickable header
   * (I2). Columns missing from the map keep mtx-grid's default header.
   */
  dayHeaderTemplates: {[field: string]: TemplateRef<any>} = {};
  private columnDates: {[field: string]: Date} = {};
  private selectionActive = false;

  onRowSelected(rows: any[]): void {
    const siteIds = (rows ?? []).map(r => r.siteId);
    this.selectionActive = siteIds.length > 0;
    this.selectionChanged.emit(siteIds);
  }

  /**
   * mtx-grid gotcha 2: the grid rebuilds its SelectionModel empty in ngOnChanges on
   * ANY input change ([data], [columns], [headerTemplate]) and emits nothing. This
   * reports that, and only when rows were ticked, as a RESET, not as an empty
   * selection. An empty selection would mean "everyone visible" and would widen a
   * previewed scope. The container cancels the preview instead.
   */
  private resetSelection(): void {
    if (this.selectionActive) {
      this.selectionActive = false;
      this.selectionReset.emit();
    }
  }

  onDayHeaderClick(field: string): void {
    const date = this.columnDates[field];
    if (date) {
      this.reconcileDateRequested.emit(new Date(date));
    }
  }

  /** A cell that WILL lock if the preview is committed. Cells already locked are left alone. */
  isPreviewLocked(row: any, field: string): boolean {
    const landing = this.reconcilePreview?.landingBySiteId[row?.siteId];
    const day = dayKey(row?.planningPrDayModels?.[field]?.date);
    if (!landing || !day) {
      return false;
    }
    const existing = this.reconcilePreview.existingBySiteId[row.siteId];
    return day <= landing && (!existing || day > existing);
  }

  /** The cell the mark will land on: the new boundary. */
  isPreviewBoundary(row: any, field: string): boolean {
    const landing = this.reconcilePreview?.landingBySiteId[row?.siteId];
    return !!landing && dayKey(row?.planningPrDayModels?.[field]?.date) === landing;
  }

  /** Already reconciled at or past the target: shown as skipped (§8.3). */
  isPreviewSkipped(row: any): boolean {
    return this.reconcilePreview?.outcomeBySiteId[row?.siteId] === 'skip';
  }
```

The final `ngOnChanges` merges the existing body, 9A's legend line and gotcha 2:

```ts
  ngOnChanges(changes: SimpleChanges): void {
    if (changes.dateFrom || changes.dateTo) {
      if (changes.dateFrom !== undefined) {
        this.dateFrom = changes.dateFrom.currentValue;
      }
      if (changes.dateTo !== undefined) {
        this.dateTo = changes.dateTo.currentValue;
        this.updateTableHeaders();
      }
    }
    if (changes.timePlannings && this.waitingForFreshData && this.pendingHighlight) {
      // Fresh data has arrived after highlight was requested — now we can scroll
      this.waitingForFreshData = false;
      this.highlightApplied = false;
    }
    if (changes.timePlannings) {
      this.hasLockedDayInView = this.computeHasLockedDayInView();
      this.resetSelection();
    }
  }
```

The final `updateTableHeaders()` replaces :138-175:

```ts
  private updateTableHeaders(): void {
    this.tableHeaders = [];
    this.dayHeaderTemplates = {};
    this.columnDates = {};
    this.cdr.detectChanges();
    const startDate = new Date(this.dateFrom);
    const endDate = new Date(this.dateTo);
    const today = new Date();
    const todayMidnight = new Date();
    todayMidnight.setHours(0, 0, 0, 0);
    const tempEndDate = new Date(endDate);
    tempEndDate.setHours(0, 0, 0, 0);
    const diff = (tempEndDate.getTime() - startDate.getTime()) / (1000 * 3600 * 24);
    let daysCount = Math.floor(diff) +1;
    let todayTranslated = this.translateService.stream('Today');
    const headerTemplates: {[field: string]: TemplateRef<any>} = {};

    this.tableHeaders = [
      {
        cellTemplate: this.firstColumnTemplate,
        header: this.translateService.stream('Name'),
        pinned: 'left',
        field: 'siteName',
        sortable: true,
      },
      ...Array.from({length: daysCount}).map((_, index) => {
        const currentDate = new Date(startDate);
        currentDate.setDate(startDate.getDate() + index);
        const field = index.toString();
        this.columnDates[field] = currentDate;
        // Only past days can be reconciled (I2), so only their headers become
        // buttons. A past header is always a plain string: the Observable header is
        // today's, and today never gets this template.
        if (currentDate < todayMidnight) {
          headerTemplates[field] = this.reconcileDayHeaderTemplate;
        }
        const isToday = currentDate.toDateString() === today.toDateString();
        const formattedDate = isToday
          ? todayTranslated
          : this.datePipe.transform(currentDate, 'E dd/MM', undefined, this.currentLocale) || '';
        return {
          cellTemplate: this.dayColumnTemplate,
          header: formattedDate,
          field,
          sortable: false,
          class: (row: any) => this.getCellClass(row, field),
        };
      }),
    ];
    this.dayHeaderTemplates = headerTemplates;
    // New [columns] and [headerTemplate] make mtx-grid drop its selection silently.
    this.resetSelection();
    this.cdr.detectChanges();
  }
```

- [ ] **Step 2: Table template**

**(a)** The final `<mtx-grid>` tag, replacing :19-30:

```html
<!--
  Row selection is the worker set for a bulk reconcile (spec §8.3).
  disableRowClickSelection is mtx-grid gotcha 1 handled at the source: without it,
  _selectRow() on the <tr> clears the batch selection whenever a day cell or the Name
  column is clicked (rowClick is still emitted). Rows are selected by their checkbox
  only, and the host styles keep that column sticky next to the pinned Name.
-->
<mtx-grid
  id="main-header-text"
  [data]="timePlannings"
  [columns]="tableHeaders"
  [showPaginator]="false"
  [pageOnFront]="false"
  [rowStriped]="false"
  [showToolbar]="false"
  [noResultTemplate]="noWorkersTemplate"
  [headerTemplate]="dayHeaderTemplates"
  [rowSelectable]="true"
  [multiSelectable]="true"
  [disableRowClickSelection]="true"
  (rowSelectedChange)="onRowSelected($event)"
  class="time-dashboard"
>
</mtx-grid>
```

**(b)** The final opening tag of `.plan-container` in `#dayColumnTemplate`, replacing
:238 and Task 12's version of it:

```html
  <!--
    Preview classes live here, not in getCellClass: mtx-grid applies the td class
    through a PURE pipe that re-runs only when the row object changes, so it cannot
    follow the container's preview. This element is re-evaluated on every pass.
  -->
  <div class="plan-container" data-tp-help="grid.openDay"
       [class.tp-preview-lock]="isPreviewLocked(row, col.field)"
       [class.tp-preview-boundary]="isPreviewBoundary(row, col.field)"
       [class.tp-preview-skip]="isPreviewSkipped(row)"
       (click)="onDayColumnClick(row, col.field)"
       id="cell{{index}}_{{col.field}}">
```

**(c)** In **both** branches of `#firstColumnTemplate`, directly after
`<strong>{{ row[col.field] }}</strong>` (:63 and :162):

```html
          <span class="tp-preview-skip-label" id="previewSkipped{{index}}"
                *ngIf="isPreviewSkipped(row)">{{ 'reconcileRowSkipped' | translate }}</span>
```

**(d)** A new template, after the closing `</ng-template>` of `#dayColumnTemplate`
(:434):

```html
<!--
  Header of a past day column (spec §8.3, "Selected column"). Clicking it only
  PREVIEWS a reconcile through that day. The commit lives in the container's scope
  bar, so a misclick on a header is harmless.
-->
<ng-template #reconcileDayHeaderTemplate let-col>
  <button type="button" class="tp-day-header-btn" id="dayHeader{{col.field}}"
          [matTooltip]="'reconcileHeaderTooltip' | translate"
          (click)="onDayHeaderClick(col.field)">{{ col.header }}</button>
</ng-template>
```

- [ ] **Step 3: Container TS**

In `time-plannings-container.component.ts`, change the imports to:

```ts
import {startOfWeek, endOfWeek, format, startOfDay, subDays} from 'date-fns';
import {ToastrService} from 'ngx-toastr';
import {TranslateService} from '@ngx-translate/core';
import {ReconcileThroughResultModel} from '../../../models';
import {buildReconcilePreview, ReconcilePreview} from '../day-lock.util';
```

Add the injections beside the existing ones:

```ts
  private toastrService = inject(ToastrService);
  private translateService = inject(TranslateService);
```

Add the fields. They replace Task 12's `selectedSiteIds`, `reconcileThroughDate` and
`maxReconcileDate`. Task 12's getter used `toISOString()`, which is UTC, so it gave a
max one day early between 00:00 and 02:00 Danish time.

```ts
  /** Ticked rows (site ids). Empty means every visible worker. */
  selectedSiteIds: number[] = [];
  /** The bulk target, always a day on screen. See refreshReconcileBounds. */
  reconcileThroughDate: Date | null = null;
  reconcilePreview: ReconcilePreview | null = null;
  reconcileInFlight = false;
  reconcileMinDate: Date | null = null;
  reconcileMaxDate: Date | null = null;
```

The final `getPlannings()` replaces :142-152:

```ts
  getPlannings() {
    this.buildTimePlanningsRequest();
    this.getTimePlannings$ = this.planningsService
      .getPlannings(this.timePlanningsRequest)
      .subscribe((data) => {
        if (data && data.success) {
          this.applyPlannings(data.model);
        }
        this.startPageTourOnce();
      });
    }
```

In `onShowResignedSitesChanged` (:298-300), replace
`this.timePlannings = planningsResult.model;` with:

```ts
        this.applyPlannings(planningsResult.model);
```

Add:

```ts
  /**
   * Every path that replaces the rows comes through here: a day save, the
   * assigned-site dialog, Reload, a filter, the resigned toggle. The grid drops the
   * ticked rows on new data (mtx-grid gotcha 2).
   * - If rows were ticked, the preview is CANCELLED, never rebuilt. A rebuild would
   *   fall back to "every visible worker" and silently widen the scope from the
   *   ticked rows to all of them.
   * - If nothing was ticked, the scope was already "everyone visible", so the preview
   *   is redrawn on the new rows.
   * Both happen before change detection, so the scope bar never changes after it
   * has been checked.
   */
  private applyPlannings(model: TimePlanningModel[]): void {
    const hadSelection = this.selectedSiteIds.length > 0;
    this.timePlannings = model;
    this.selectedSiteIds = [];
    this.refreshReconcileBounds();
    if (hadSelection) {
      this.cancelReconcilePreview();
    } else {
      this.rebuildReconcilePreview();
    }
  }

  /**
   * The bulk target must be on screen, because the preview can only draw what is on
   * screen (§8.3). I2 caps it at yesterday. A null max means nothing in view can be
   * reconciled.
   */
  private refreshReconcileBounds(): void {
    const from = startOfDay(this.dateFrom);
    const yesterday = startOfDay(subDays(new Date(), 1));
    const lastVisible = startOfDay(this.dateTo);
    const max = lastVisible < yesterday ? lastVisible : yesterday;
    this.reconcileMinDate = from;
    this.reconcileMaxDate = max < from ? null : max;
  }

  get reconcileTargetLabel(): string {
    return this.reconcileThroughDate ? format(this.reconcileThroughDate, 'dd.MM.yyyy') : '';
  }

  /**
   * The user ticked or unticked a row. Unticking the last one returns the scope to
   * "every visible worker", as the spec defines, and the scope bar's count changes
   * in plain sight.
   */
  onSelectionChanged(siteIds: number[]): void {
    this.selectedSiteIds = siteIds;
    this.rebuildReconcilePreview();
  }

  /** The grid dropped the ticked rows by itself (new columns). Cancel rather than widen. */
  onSelectionReset(): void {
    if (this.selectedSiteIds.length === 0) {
      return; // applyPlannings has already handled a reload
    }
    this.selectedSiteIds = [];
    this.cancelReconcilePreview();
  }

  /** From the toolbar field or a day-column header. Starts the preview and commits nothing. */
  onReconcileDateChanged(date: Date | null): void {
    this.reconcileThroughDate = date ? startOfDay(date) : null;
    this.rebuildReconcilePreview();
  }

  cancelReconcilePreview(): void {
    this.reconcileThroughDate = null;
    this.reconcilePreview = null;
  }

  private rebuildReconcilePreview(): void {
    const target = this.reconcileThroughDate;
    const onScreen = !!target && !!this.reconcileMinDate && !!this.reconcileMaxDate
      && target >= this.reconcileMinDate && target <= this.reconcileMaxDate;
    if (!onScreen) {
      // Also covers navigating away from the target's week: the preview leaves with it.
      this.cancelReconcilePreview();
      return;
    }
    // No selection means every worker currently visible under the active filters.
    const scope = this.selectedSiteIds.length
      ? this.selectedSiteIds
      : this.timePlannings.map(x => x.siteId);
    this.reconcilePreview = buildReconcilePreview(this.timePlannings, scope, format(target, 'yyyy-MM-dd'));
  }

  confirmReconcileThrough(): void {
    const preview = this.reconcilePreview;
    if (!preview || this.reconcileInFlight || preview.willReconcileCount === 0) {
      return;
    }
    this.reconcileInFlight = true;
    // Exactly the rows the preview drew. Skipped rows go too, so that the toast reports
    // what the server decided rather than what the client predicted.
    this.planningsService.reconcileThrough(preview.target, preview.siteIds).subscribe({
      next: result => {
        this.reconcileInFlight = false;
        if (result && result.success && result.model) {
          this.reportReconcileThrough(result.model);
          this.cancelReconcilePreview();
          this.getPlannings();
        }
      },
      error: () => {
        this.reconcileInFlight = false;
      },
    });
  }

  /** Applied and skipped counts, from the per-site result (Task 4, 8A). */
  private reportReconcileThrough(model: ReconcileThroughResultModel): void {
    const skipped = model.skippedAlreadyFurtherForward.length + model.skippedNoRegistration.length;
    let message = this.translateService.instant('reconcileThroughResult', {applied: model.applied, skipped});
    if (model.alreadyReconciledSiteIds.length) {
      message += ' · ' + this.translateService.instant('reconcileThroughUnchanged',
        {count: model.alreadyReconciledSiteIds.length});
    }
    if (model.applied > 0) {
      this.toastrService.success(message);
    } else {
      this.toastrService.warning(message);
    }
  }
```

- [ ] **Step 4: Container template**

**(a)** The toolbar field goes after the `#workingHoursReload` button (:100-108). It
replaces Task 12 Step 2's `<input type="date">` and its `#reconcileThrough` button. The
field previews directly, so it needs no button:

```html
    <!--
      Bulk reconcile (spec §8.3). Picking a date only PREVIEWS: the cells that will
      lock are drawn in the grid, and the scope bar below the toolbar holds the
      commit. Limited to days on screen, because the preview cannot draw what is off
      screen, and to yesterday at the latest (I2).
    -->
    <div class="text-field--rounded" data-tp-help="toolbar.reconcileThrough">
      <mat-form-field id="reconcileThroughField">
        <mat-label>{{ 'Reconcile through' | translate }}</mat-label>
        <mat-datepicker-toggle matPrefix [for]="reconcilePicker"></mat-datepicker-toggle>
        <input
          matInput
          readonly
          id="reconcileThroughDate"
          [matDatepicker]="reconcilePicker"
          [min]="reconcileMinDate"
          [max]="reconcileMaxDate"
          [disabled]="!reconcileMaxDate"
          [value]="reconcileThroughDate"
          (click)="reconcilePicker.open()"
          (dateChange)="onReconcileDateChanged($event.value)">
        <mat-datepicker #reconcilePicker></mat-datepicker>
      </mat-form-field>
    </div>
```

**(b)** The scope bar goes directly after `</eform-new-subheader>` (:124):

```html
<!--
  Bulk reconcile scope (spec §8.3). It appears only while a preview is on screen,
  and it is the only place the bulk operation commits. It names the two numbers that
  matter: how many workers, and the day the boundary lands on.
-->
<div class="tp-reconcile-scope" id="reconcileScopeBar" *ngIf="reconcilePreview as preview">
  <mat-icon fontSet="material-symbols-outlined" class="neutral-icon">lock</mat-icon>
  <div class="tp-reconcile-scope__text">
    <span id="reconcileScopeSummary">{{ 'reconcileScopeSummary' | translate: {date: reconcileTargetLabel, count: preview.willReconcileCount} }}</span>
    <span id="reconcileScopeSkipped" *ngIf="preview.skipCount > 0">{{ 'reconcileScopeSkipped' | translate: {count: preview.skipCount} }}</span>
  </div>
  <button type="button" class="btn-cancel" id="reconcileScopeCancel"
          [disabled]="reconcileInFlight" (click)="cancelReconcilePreview()">
    {{ 'Cancel' | translate }}
  </button>
  <button type="button" class="btn-primary" id="reconcileScopeConfirm"
          [disabled]="reconcileInFlight || preview.willReconcileCount === 0"
          (click)="confirmReconcileThrough()">
    {{ 'Reconcile' | translate }}
  </button>
</div>
```

**(c)** The final table tag, replacing :136-144:

```html
<app-time-plannings-table
  [timePlannings]="timePlannings"
  [dateFrom]="dateFrom"
  [dateTo]="dateTo"
  [reconcilePreview]="reconcilePreview"
  (timePlanningChanged)="onTimePlanningChanged($event)"
  (assignedSiteChanged)="onAssignedSiteChanged($event)"
  (tagSelected)="onTagSelectedFromRow($event)"
  (highlightedRowRendered)="onHighlightedRowRendered()"
  (selectionChanged)="onSelectionChanged($event)"
  (selectionReset)="onSelectionReset()"
  (reconcileDateRequested)="onReconcileDateChanged($event)"
></app-time-plannings-table>
```

- [ ] **Step 5: Container unit spec**

The container now injects `ToastrService`, which the spec's TestBed does not provide,
so every test in the file would fail with `NullInjectorError`. In
`time-plannings-container.component.spec.ts`:

Add the imports:

```ts
import { ToastrService } from 'ngx-toastr';
import { addDays, endOfWeek, format, startOfWeek, subDays } from 'date-fns';
```

Add `reconcileThrough` to the plannings mock:

```ts
    mockPlanningsService = {
      getPlannings: jest.fn(),
      updatePlanning: jest.fn(),
      reconcileThrough: jest.fn(),
    } as any;
```

Add the provider:

```ts
        { provide: Store, useValue: mockStore },
        { provide: ToastrService, useValue: { success: jest.fn(), warning: jest.fn() } },
```

Add these blocks at the end of the outer `describe`:

```ts
  describe('Bulk reconcile preview', () => {
    const lastWeekStart = startOfWeek(subDays(new Date(), 7), { weekStartsOn: 1 });
    const lastWeekEnd = endOfWeek(lastWeekStart, { weekStartsOn: 1 });
    const rowFor = (siteId: number) => ({
      siteId,
      lockedThrough: null,
      planningPrDayModels: Array.from({ length: 7 }, (_, i) => ({
        id: siteId * 10 + i + 1,
        date: `${format(addDays(lastWeekStart, i), 'yyyy-MM-dd')}T00:00:00`,
      })),
    }) as any;

    beforeEach(() => {
      component.dateFrom = lastWeekStart;
      component.dateTo = lastWeekEnd;
      mockPlanningsService.getPlannings.mockReturnValue(
        of({ success: true, model: [rowFor(1), rowFor(2)] }) as any);
      component.getPlannings();
    });

    it('previews every visible worker when no row is ticked', () => {
      component.onReconcileDateChanged(lastWeekStart);
      expect(component.reconcilePreview?.siteIds).toEqual([1, 2]);
    });

    it('drops a target outside the days on screen', () => {
      component.onReconcileDateChanged(subDays(lastWeekStart, 1));
      expect(component.reconcilePreview).toBeNull();
      expect(component.reconcileThroughDate).toBeNull();
    });

    it('cancels, never widens, the preview when a reload drops the ticked rows', () => {
      component.onSelectionChanged([1]);
      component.onReconcileDateChanged(lastWeekStart);
      expect(component.reconcilePreview?.siteIds).toEqual([1]);

      component.getPlannings();

      expect(component.selectedSiteIds).toEqual([]);
      expect(component.reconcilePreview).toBeNull();
    });

    it('cancels the preview when the grid drops the ticked rows by itself', () => {
      component.onSelectionChanged([1]);
      component.onReconcileDateChanged(lastWeekStart);

      component.onSelectionReset();

      expect(component.reconcilePreview).toBeNull();
    });
  });

  describe('Reconcile through', () => {
    it('sends the previewed rows and toasts the counts the server decided', () => {
      const toastr = TestBed.inject(ToastrService) as any;
      mockPlanningsService.reconcileThrough.mockReturnValue(of({
        success: true,
        model: {
          landedOnBySiteId: { 1: '2026-09-07T00:00:00' },
          applied: 1,
          skippedAlreadyFurtherForward: [2],
          skippedNoRegistration: [],
          alreadyReconciledSiteIds: [],
        },
      }) as any);
      component.reconcilePreview = {
        target: '2026-09-07',
        siteIds: [1, 2],
        landingBySiteId: { 1: '2026-09-07' },
        existingBySiteId: { 1: null, 2: '2026-09-09' },
        outcomeBySiteId: { 1: 'lock', 2: 'skip' },
        willReconcileCount: 1,
        skipCount: 1,
      };

      component.confirmReconcileThrough();

      expect(mockPlanningsService.reconcileThrough).toHaveBeenCalledWith('2026-09-07', [1, 2]);
      // TranslateModule.forRoot() has no catalogue loaded, so instant() echoes the key.
      expect(toastr.success).toHaveBeenCalledWith('reconcileThroughResult');
      expect(component.reconcilePreview).toBeNull();
    });
  });
```

- [ ] **Step 6: Playwright, preview, skip and counts**

`eform-client/playwright/e2e/plugins/time-planning-pn/s/reconcile-bulk-preview.spec.ts`:

```ts
import { test, expect } from '@playwright/test';
import { LoginPage } from '../../../Page objects/Login.page';
import {
  cellOf, closeDayWithoutChange, countPuts, expectSuccess, lastWeekMonday, openDashboardLastWeek,
  openDay, RECONCILE_THROUGH_PATH, reconcileDay, rowCheckbox, rowOf, tdOf, unlockDay,
  waitForIndex, waitForPut, waitForSpinner, workerAtRow,
} from './reconcile-helpers';

/**
 * Spec §8.3. The workers are picked from grid rows 7 (A), 8 (B) and 10 (outsider) at
 * the start and are found by name after that. B is reconciled further forward (day 5)
 * than the bulk target (day 3), so it must be shown as skipped and left where it is.
 */
test.describe('Reconciled day lock: bulk reconcile', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('http://localhost:4200');
    await new LoginPage(page).login();
    await openDashboardLastWeek(page);
  });

  test('a header click previews per worker, skips rows already further, and toasts counts', async ({ page }) => {
    const a = await workerAtRow(page, 7);
    const b = await workerAtRow(page, 8);
    const outsider = await workerAtRow(page, 10);
    const throughs = countPuts(page, RECONCILE_THROUGH_PATH);

    await reconcileDay(page, b, 5);

    await rowCheckbox(page, a).check();
    await rowCheckbox(page, b).check();

    // mtx-grid gotcha 1: opening a day must not clear the batch selection.
    await openDay(page, a, 0);
    await closeDayWithoutChange(page);
    await expect(rowCheckbox(page, a)).toBeChecked();
    await expect(rowCheckbox(page, b)).toBeChecked();

    // Preview: the region is drawn in place, and nothing is written.
    await page.locator('#dayHeader3').click();
    for (const day of [0, 1, 2, 3]) {
      await expect(cellOf(page, a, day)).toHaveClass(/tp-preview-lock/);
    }
    await expect(cellOf(page, a, 3)).toHaveClass(/tp-preview-boundary/);
    await expect(cellOf(page, a, 4)).not.toHaveClass(/tp-preview-lock/);
    await expect(rowOf(page, b).locator('.tp-preview-skip-label')).toBeVisible();
    await expect(cellOf(page, b, 3)).toHaveClass(/tp-preview-skip/);
    await expect(cellOf(page, b, 3)).not.toHaveClass(/tp-preview-lock/);
    // The selection is the scope: an unticked worker is not previewed.
    await expect(cellOf(page, outsider, 0)).not.toHaveClass(/tp-preview-lock/);
    const summary = page.locator('#reconcileScopeSummary');
    await expect(summary).toHaveText(/^Afstem til og med \d{2}\.\d{2}\.\d{4} · Medarbejdere: 1$/);
    await expect(page.locator('#reconcileScopeSkipped')).toContainText('Springes over: 1');
    expect(throughs.count).toBe(0);

    // Cancel drops the preview and still writes nothing.
    await page.locator('#reconcileScopeCancel').click();
    await expect(page.locator('#reconcileScopeBar')).toHaveCount(0);
    await expect(page.locator('.tp-preview-lock')).toHaveCount(0);
    expect(throughs.count).toBe(0);

    // Commit.
    await page.locator('#dayHeader3').click();
    const [, dd, mm, yyyy] = /(\d{2})\.(\d{2})\.(\d{4})/.exec(await summary.innerText())!;
    const put = waitForPut(page, RECONCILE_THROUGH_PATH);
    const reload = waitForIndex(page, lastWeekMonday());
    await page.locator('#reconcileScopeConfirm').click();
    const response = await put;
    const body = await expectSuccess(response);
    const sent = response.request().postDataJSON();
    expect(sent.date, 'the date sent is the date the scope bar named').toBe(`${yyyy}-${mm}-${dd}`);
    expect(sent.siteIds).toHaveLength(2);
    expect(body.model.applied).toBe(1);
    expect(body.model.skippedAlreadyFurtherForward).toHaveLength(1);
    await expect(page.locator('#toast-container')).toContainText('Afstemt: 1 · Sprunget over: 1');
    await reload;
    await waitForSpinner(page);

    // The staircase: A at day 3, B untouched at day 5; the selection and the preview are gone.
    await expect(tdOf(page, a, 3)).toHaveClass(/reconciled-background/);
    await expect(tdOf(page, a, 2)).toHaveClass(/locked-background/);
    await expect(tdOf(page, a, 4)).not.toHaveClass(/locked-background|reconciled-background/);
    await expect(tdOf(page, b, 5)).toHaveClass(/reconciled-background/);
    await expect(page.locator('#reconcileScopeBar')).toHaveCount(0);
    await expect(rowCheckbox(page, a)).not.toBeChecked();

    // Cleanup.
    await unlockDay(page, a, 3);
    await unlockDay(page, b, 5);
  });

  test('a reload while ticked rows are previewed cancels the preview instead of widening it', async ({ page }) => {
    const a = await workerAtRow(page, 7);
    const outsider = await workerAtRow(page, 10);

    await rowCheckbox(page, a).check();
    await page.locator('#dayHeader3').click();
    await expect(page.locator('#reconcileScopeSummary')).toContainText('Medarbejdere: 1');

    const reload = waitForIndex(page, lastWeekMonday());
    await page.locator('#workingHoursReload').click();
    await reload;
    await waitForSpinner(page);

    // Silently becoming "every visible worker" would put the outsider in scope. Instead the preview is gone.
    await expect(page.locator('#reconcileScopeBar')).toHaveCount(0);
    await expect(cellOf(page, outsider, 0)).not.toHaveClass(/tp-preview-lock/);
    await expect(page.locator('.tp-preview-lock')).toHaveCount(0);
  });
});
```

- [ ] **Step 7: Commit** (replaces Task 12 Step 4's directory add)

```bash
cd /home/rene/Documents/workspace/microting/eform-angular-timeplanning-plugin
git add eform-client/src/app/plugins/modules/time-planning-pn/components/plannings/time-plannings-table/time-plannings-table.component.ts \
        eform-client/src/app/plugins/modules/time-planning-pn/components/plannings/time-plannings-table/time-plannings-table.component.html \
        eform-client/src/app/plugins/modules/time-planning-pn/components/plannings/time-plannings-container/time-plannings-container.component.ts \
        eform-client/src/app/plugins/modules/time-planning-pn/components/plannings/time-plannings-container/time-plannings-container.component.html \
        eform-client/src/app/plugins/modules/time-planning-pn/components/plannings/time-plannings-container/time-plannings-container.component.spec.ts \
        eform-client/playwright/e2e/plugins/time-planning-pn/s/reconcile-bulk-preview.spec.ts
git commit -m "feat(lock): preview a bulk reconcile in place and report per-site counts"
```

If Task 12's own commit has not been made yet, it stages the same four component files
by name. It does not stage the spec or the Playwright file.

---

## Task 13A: Strings in every locale, and the help-wiring contract

**Follows:** Task 13.
**Supersedes:**
- Task 13 Step 6, the directory `git add`.

Otherwise this task only adds: keys after Task 13 Step 5's block, and Task 13's five
keys in the locale files Task 13 did not touch.

**Files:**
- Modify: `.../time-planning-pn/i18n/da.ts` and `.../i18n/enUS.ts`
- Modify: the 24 other locale files in `.../time-planning-pn/i18n/`, through the script in Step 3
- Modify: `.../time-planning-pn/help/help-wiring.spec.ts`

**Interfaces:**
- Consumes: the keys used in 9A, 11A, 11B and 12A.
- Produces: every key, present in all 26 locale files.

**Why all locales.** `help-wiring.spec.ts` freezes the set of literal template
translate keys. Its comment gives the contract: a new key "forces a deliberate update
of all 25 shared locale files". The directory actually holds 26, next to
`translates.ts`.
- Identifier keys such as `reconcileScopeSummary` would render as raw keys in German,
  Norwegian and every other locale.
- The 24 other files therefore get the English values.
- `UNLOCK` is `UNLOCK` in those files, so the typed-word check still works in every
  language.

- [ ] **Step 1: Danish**

In `i18n/da.ts`, directly after Task 13 Step 5's five keys and before `};`:

```ts
  // Reconciled day lock: grid, dialog footer and bulk scope (Tasks 9A-12A).
  Reconcile: 'Afstem',
  lockedTooltip: 'Låst · ligger før en afstemt dag',
  reconciledLegend: 'Afstemt · dagens tal er endelige',
  reconciledProvenance: 'Afstemt {{date}} kl. {{time}}',
  reconcileHeaderTooltip: 'Afstem til og med denne dag',
  reconcileRowSkipped: 'Springes over · allerede afstemt længere frem',
  reconcileScopeSummary: 'Afstem til og med {{date}} · Medarbejdere: {{count}}',
  reconcileScopeSkipped: 'Springes over: {{count}} (allerede afstemt længere frem)',
  reconcileThroughResult: 'Afstemt: {{applied}} · Sprunget over: {{skipped}}',
  reconcileThroughUnchanged: 'Uændret: {{count}}',
  reconcileDayConfirm: 'Afstem {{worker}} {{date}}? Dagen og alle dage før den bliver låst.',
  reconcileNeedsSave: 'Gem ændringerne, før dagen afstemmes',
  unlockFreeFirst: 'Låst, fordi {{date}} er afstemt. Lås {{date}} op først.',
  unlockTypeWordPrompt: 'Afstemningen fjernes, og dagen kan redigeres igen. Skriv {{word}} for at bekræfte.',
  UNLOCK: 'LÅS OP',
```

- [ ] **Step 2: English**

In `i18n/enUS.ts`, directly after Task 13 Step 5's five keys and before `};`:

```ts
  // Reconciled day lock: grid, dialog footer and bulk scope (Tasks 9A-12A).
  Reconcile: 'Reconcile',
  lockedTooltip: 'Locked · falls before a reconciled day',
  reconciledLegend: 'Reconciled · the figures for the day are final',
  reconciledProvenance: 'Reconciled {{date}} at {{time}}',
  reconcileHeaderTooltip: 'Reconcile through this day',
  reconcileRowSkipped: 'Skipped · already reconciled further ahead',
  reconcileScopeSummary: 'Reconcile through {{date}} · Workers: {{count}}',
  reconcileScopeSkipped: 'Skipped: {{count}} (already reconciled further ahead)',
  reconcileThroughResult: 'Reconciled: {{applied}} · Skipped: {{skipped}}',
  reconcileThroughUnchanged: 'Unchanged: {{count}}',
  reconcileDayConfirm: 'Reconcile {{worker}} on {{date}}? This day and every day before it become locked.',
  reconcileNeedsSave: 'Save your changes before reconciling the day',
  unlockFreeFirst: 'Locked because {{date}} is reconciled. Unlock {{date}} first.',
  unlockTypeWordPrompt: 'The reconciliation is removed and the day can be edited again. Type {{word}} to confirm.',
  UNLOCK: 'UNLOCK',
```

No string mentions who may do what. Each one says what the day is, or what is about to
happen to it.

- [ ] **Step 3: The other 24 locales, English until translated**

Every locale file ends with a property that has a trailing comma, then `};`. This was
checked for all 26. The script inserts one block before that final `};` and is
idempotent:

```bash
cd /home/rene/Documents/workspace/microting/eform-angular-timeplanning-plugin
node - <<'EOF'
const fs = require('fs');
const path = require('path');
const dir = 'eform-client/src/app/plugins/modules/time-planning-pn/i18n';
const block = `  // Reconciled day lock (Tasks 11-13A). English until translated.
  'Reconcile day': 'Reconcile day',
  'Reconcile through': 'Reconcile through',
  Unlock: 'Unlock',
  Reconciled: 'Reconciled',
  Locked: 'Locked',
  Reconcile: 'Reconcile',
  lockedTooltip: 'Locked · falls before a reconciled day',
  reconciledLegend: 'Reconciled · the figures for the day are final',
  reconciledProvenance: 'Reconciled {{date}} at {{time}}',
  reconcileHeaderTooltip: 'Reconcile through this day',
  reconcileRowSkipped: 'Skipped · already reconciled further ahead',
  reconcileScopeSummary: 'Reconcile through {{date}} · Workers: {{count}}',
  reconcileScopeSkipped: 'Skipped: {{count}} (already reconciled further ahead)',
  reconcileThroughResult: 'Reconciled: {{applied}} · Skipped: {{skipped}}',
  reconcileThroughUnchanged: 'Unchanged: {{count}}',
  reconcileDayConfirm: 'Reconcile {{worker}} on {{date}}? This day and every day before it become locked.',
  reconcileNeedsSave: 'Save your changes before reconciling the day',
  unlockFreeFirst: 'Locked because {{date}} is reconciled. Unlock {{date}} first.',
  unlockTypeWordPrompt: 'The reconciliation is removed and the day can be edited again. Type {{word}} to confirm.',
  UNLOCK: 'UNLOCK',
`;
const skip = new Set(['da.ts', 'enUS.ts', 'translates.ts']);
let changed = 0;
for (const name of fs.readdirSync(dir).sort()) {
  if (!name.endsWith('.ts') || skip.has(name)) continue;
  const file = path.join(dir, name);
  const src = fs.readFileSync(file, 'utf8');
  if (src.includes('reconciledProvenance:')) continue;
  const at = src.lastIndexOf('};');
  if (at < 0) throw new Error(`${name}: no closing '};'`);
  fs.writeFileSync(file, src.slice(0, at) + block + src.slice(at));
  changed++;
}
console.log(`updated ${changed} locale files`);
EOF
```

Expected output: `updated 24 locale files`.

- [ ] **Step 4: Update the help-wiring contract**

In `help/help-wiring.spec.ts`, replace `TEMPLATE_TRANSLATE_KEYS` (:44-53):

```ts
const TEMPLATE_TRANSLATE_KEYS = [
  'Actual', 'Auto break calculation', 'Cancel', 'CommentOffice', 'CommentWorker', 'Date range',
  'Download Excel', 'Export to payroll', 'Flex', 'Flex balance at start of day',
  'Flex balance to date', 'keyboard_tab', 'keyboard_tab_rtl', 'Needs update!', 'NettoHours',
  'NettoHours override', 'No pay rule set selected', 'PaidOutFlex', 'Pause', 'Plan hours',
  'Planned working hours', 'Reload table', 'Reset pause to recorded', 'Save',
  'Shift not stopped by user!', 'Shifts across midnight', 'Show resigned', 'Start', 'Stop',
  'Tags', 'Total breaktime', 'Total working hours', 'Use 1-minute intervals',
  'View GPS Location', 'View history', 'View Snapshot', 'Worker', 'Worktime start',
  'Worktime stop',
  // Reconciled day lock (Tasks 11-12A). Every one is in all 26 locale files (Task 13A).
  'Reconcile', 'Reconcile day', 'Reconcile through', 'Unlock',
  'lockedTooltip', 'reconciledLegend', 'reconcileHeaderTooltip', 'reconcileRowSkipped',
  'reconcileScopeSummary', 'reconcileScopeSkipped', 'reconcileDayConfirm', 'reconcileNeedsSave',
  'unlockFreeFirst', 'unlockTypeWordPrompt',
];
```

In the test `places an inline hint only where something conditional has just
happened`, replace the expected list (:147-149) to add the two banners from 11A Step 1:

```ts
    expect(hintIds.sort()).toEqual([
      'dayCell.futureDisabled', 'dayCell.lockedByReconciled', 'dayCell.planHoursLimit',
      'dayCell.reconciled', 'grid.noWorkers',
    ]);
```

- [ ] **Step 5: Verify the contract by hand**

The spec itself runs only in CI, so the same checks are done with grep.

```bash
cd /home/rene/Documents/workspace/microting/eform-angular-timeplanning-plugin/eform-client/src/app/plugins/modules/time-planning-pn
T="components/plannings/time-plannings-container/time-plannings-container.component.html
components/plannings/time-plannings-table/time-plannings-table.component.html
components/plannings/time-planning-actions/workday-entity/workday-entity-dialog.component.html"
```

**(a)** The literal template keys. The output must equal the new list exactly:

```bash
grep -ohE "'[^']+'\s*\|\s*translate" $T | sed -E "s/^'([^']+)'.*/\1/" | sort -u
```

**(b)** No placeholder id in any template, comments included. The wiring spec reads
comments as markup (fact 9). Expect no output:

```bash
grep -n 'helpId="…"\|data-tp-help="…"' $T
```

**(c)** Every literal help id or anchor in the templates has the `section.name` shape.
This catches any other placeholder written in a comment. Expect no output:

```bash
grep -ohE '(helpId|data-tp-help)="[^"]*"' $T | grep -vE '="[a-zA-Z]+\.[a-zA-Z]+"'
```

**(d)** Every new key is in every locale file. Expect no output:

```bash
for k in reconciledProvenance reconcileScopeSummary unlockTypeWordPrompt UNLOCK "'Reconcile day'" "'Reconcile through'"; do
  for f in i18n/*.ts; do
    [ "$(basename "$f")" = translates.ts ] && continue
    grep -q "$k" "$f" || echo "MISSING $k in $f"
  done
done
```

- [ ] **Step 6: Task 13's own commit, staged by name** (replaces Task 13 Step 6)

Run this only if Task 13's commit has not been made yet:

```bash
cd /home/rene/Documents/workspace/microting/eform-angular-timeplanning-plugin
P=eform-client/src/app/plugins/modules/time-planning-pn
git add $P/help/help.model.ts \
        $P/help/planning-help.registry.ts \
        $P/help/i18n/da.ts \
        $P/help/i18n/enUS.ts \
        $P/i18n/da.ts \
        $P/i18n/enUS.ts
git commit -m "feat(lock): add help entries and translations for the day lock"
```

- [ ] **Step 7: Commit 13A, every file by name**

```bash
cd /home/rene/Documents/workspace/microting/eform-angular-timeplanning-plugin
I=eform-client/src/app/plugins/modules/time-planning-pn/i18n
git add $I/bgBG.ts $I/csCZ.ts $I/da.ts   $I/deDE.ts $I/elGR.ts $I/enUS.ts \
        $I/esES.ts $I/etET.ts $I/fiFI.ts $I/frFR.ts $I/hrHR.ts $I/huHU.ts \
        $I/isIS.ts $I/itIT.ts $I/ltLT.ts $I/lvLV.ts $I/nlNL.ts $I/noNO.ts \
        $I/plPL.ts $I/ptBR.ts $I/ptPT.ts $I/roRO.ts $I/skSK.ts $I/slSL.ts \
        $I/svSE.ts $I/ukUA.ts \
        eform-client/src/app/plugins/modules/time-planning-pn/help/help-wiring.spec.ts
git status --short   # expect exactly these 27 files, and translates.ts untouched
git commit -m "feat(lock): translate the day-lock strings into every locale"
```

---

## Task 14A: Shard `s` bootstrap and Task 14's spec on the shared helpers

**Follows:** Task 14.
**Supersedes:**
- **Task 14 Step 1:** the contents of `s/reconcile-day-lock.spec.ts`.
- **Task 14 Step 3:** the directory `git add`.

Task 14 Step 2 (the matrix entry `s`) stands.

**Files:**
- Create: `eform-client/playwright/e2e/plugins/time-planning-pn/s/activate-plugin.spec.ts` (copied from `r/`)
- Create: `eform-client/playwright/e2e/plugins/time-planning-pn/s/assert-true.spec.ts` (copied from `r/`)
- Modify: `eform-client/playwright/e2e/plugins/time-planning-pn/s/reconcile-day-lock.spec.ts`

**Interfaces:**
- Consumes: `s/reconcile-helpers.ts` (9A) and every UI id from 9A-12A.
- Produces: a shard that loads the plugin before any spec runs.

**Three defects in Task 14 as written.**
1. **The plugin never loads in shard `s`.** Lanes `q` and `r` run on `a`'s seed with no
   seed of their own, and they first run `activate-plugin.spec.ts` and
   `assert-true.spec.ts`. Alphabetical order and `workers: 1` put those two first.
   Setting `EformPlugins.Status = 1` in the seed does not load the plugin. Without the
   bootstrap, the first `/plannings/index` wait in every `s` spec times out.
2. **The spec uses the current week.** On a Monday, `#cell3_1` (Tuesday) is in the
   future; on a Tuesday it is today. Either way I2 hides `#reconcileButton`.
3. **The spec addresses rows by position and uses the old flows.** Its rows are
   `#cell3_N` ids, and it clicks reconcile and unlock once each. After 11A and 11B,
   reconcile takes two clicks and keeps the dialog open, and unlock takes a word.

- [ ] **Step 1: The bootstrap**

```bash
cd /home/rene/Documents/workspace/microting/eform-angular-timeplanning-plugin/eform-client/playwright/e2e/plugins/time-planning-pn
cp r/activate-plugin.spec.ts s/activate-plugin.spec.ts
cp r/assert-true.spec.ts s/assert-true.spec.ts
```

- [ ] **Step 2: Rewrite Task 14's spec on the helpers**

`s/reconcile-day-lock.spec.ts`:

```ts
import { test, expect } from '@playwright/test';
import { LoginPage } from '../../../Page objects/Login.page';
import {
  closeDayWithoutChange, openDashboardLastWeek, openDay, reconcileDay, tdOf, unlockDay, workerAtRow,
} from './reconcile-helpers';

/**
 * The end-to-end lock: reconcile, cascade, read-only, unlock. The worker is grid
 * row 3 at the start and is found by name after that. Last week, so both days are
 * in the past (I2) whatever weekday CI runs on.
 */
test.describe('Reconciled day lock', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('http://localhost:4200');
    await new LoginPage(page).login();
    await openDashboardLastWeek(page);
  });

  test('reconciling a day locks it and every earlier day for that worker', async ({ page }) => {
    const worker = await workerAtRow(page, 3);

    await reconcileDay(page, worker, 1);

    // The boundary treatment, and the day before it locked but not reconciled.
    await expect(tdOf(page, worker, 1)).toHaveClass(/reconciled-background/);
    await expect(tdOf(page, worker, 0)).toHaveClass(/locked-background/);
    await expect(tdOf(page, worker, 0)).not.toHaveClass(/reconciled-background/);

    // The locked day opens read-only, for the same worker, with no save and no unlock.
    await openDay(page, worker, 0);
    await expect(page.locator('#saveButton')).toHaveCount(0);
    await expect(page.locator('#unlockButton')).toHaveCount(0);
    await closeDayWithoutChange(page);

    // The boundary day offers unlock, and using it frees both days.
    await unlockDay(page, worker, 1);
    await expect(tdOf(page, worker, 1)).not.toHaveClass(/reconciled-background/);
    await expect(tdOf(page, worker, 0)).not.toHaveClass(/locked-background/);
  });
});
```

- [ ] **Step 3: Task 14's own commit, staged by name** (replaces Task 14 Step 3)

Run this only if Task 14's commit has not been made yet:

```bash
cd /home/rene/Documents/workspace/microting/eform-angular-timeplanning-plugin
git add eform-client/playwright/e2e/plugins/time-planning-pn/s/reconcile-day-lock.spec.ts \
        .github/workflows/dotnet-core-pr.yml \
        .github/workflows/dotnet-core-master.yml
git commit -m "test(lock): end-to-end reconcile, cascade and unlock"
```

- [ ] **Step 4: Commit 14A**

```bash
cd /home/rene/Documents/workspace/microting/eform-angular-timeplanning-plugin
git add eform-client/playwright/e2e/plugins/time-planning-pn/s/activate-plugin.spec.ts \
        eform-client/playwright/e2e/plugins/time-planning-pn/s/assert-true.spec.ts \
        eform-client/playwright/e2e/plugins/time-planning-pn/s/reconcile-day-lock.spec.ts
git commit -m "test(lock): bootstrap shard s and anchor the lock e2e by worker"
```

**Watching CI (Task 15).**
- **File order.** The `s` job runs `activate-plugin`, `assert-true`,
  `reconcile-bulk-preview`, `reconcile-day-lock`, `reconcile-dialog-confirm`,
  `reconcile-glyphs`, `reconcile-unlock-word`, in that order.
- **A failure can leave a worker locked.** Each later spec uses its own worker, so the
  specs after it still run.
- **The canary.** The glyph spec's precondition, `#lockLegend` count 0, fails when an
  earlier spec leaked a lock. A red there is a leak, not a glyph bug: fix the first
  failure first.
