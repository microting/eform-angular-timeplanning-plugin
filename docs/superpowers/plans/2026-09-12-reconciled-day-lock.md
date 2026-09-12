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
- **Permissions:** any web user may reconcile. No admin gate on reconcile or unlock.
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

**Spec coverage.** §4 data model → Task 1. §4.2 I1 → Tasks 4 (write) and 1 (test). I2 → Tasks 1, 4, 12. I3 → Task 2. §4.3 query cost → Task 1 (`LockedThroughForSitesAsync`). §5 write inventory → Tasks 2, 5, 7. §6.1 three layers → Tasks 2 (L1), 5 (L2, L3). §6.2 cascades → Global Constraints + Task 1 doc comment. §6.3 gap-fill → Task 5 Step 5. §6.4 timezone → Task 1 `CanReconcile`. §7 API → Task 4; read model → Task 6. §8.1 three states → Tasks 9, 10. §8.2 single day → Task 11. §8.3 bulk → Tasks 4 (`ReconcileThrough`), 12. §8.4 unlock → Tasks 4, 11. §8.5 blocked feedback → Task 11. §8.6 permissions → no admin gate anywhere (verified: no `[Authorize]` added in Task 4). §9 testing → Tasks 1, 2, 4, 5, 6, 14.

**Gaps found and closed while reviewing:**
- The interceptor must permit the unlock write on the boundary day, or unlocking would be blocked by the lock it removes. Added `IsUnlockOfBoundaryDay` (Task 2 Step 3) and a test for it.
- `TestBaseSetup` builds its own contexts and would bypass the interceptor — the lock would appear tested while being unenforced. Added Task 2 Step 6.
- `[mat-dialog-close]="data"` fires regardless of `(click)`, so a disabled Save still closes with data. Save is removed from the DOM on a locked day, not disabled (Task 11 Step 4).

**Type consistency.** `lockedThrough` is `DateTime?` in C# and `string | null` in TS (JSON). `LockedThroughAsync` / `LockedThroughForSitesAsync` / `IsLocked` / `CanReconcile` are used with those exact names in Tasks 2, 4, 5, 6. `reconciled` / `reconciledAt` match the C# `Reconciled` / `ReconciledAt` under default camelCase JSON. CSS classes `locked-background` / `reconciled-background` match between Task 9 (emitted) and Tasks 10, 14 (styled, asserted).

**Known risk carried forward:** Task 7 duplicates ~90 lines into the service repo. A divergence is a lock with a hole. Both copies carry a header comment naming the twin; there is no shared package to put it in without modifying the base.
