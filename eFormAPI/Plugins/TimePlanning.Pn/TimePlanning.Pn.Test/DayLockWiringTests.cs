using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microting.eForm.Infrastructure.Constants;
using Microting.TimePlanningBase.Infrastructure.Data;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Helpers;
using TimePlanning.Pn.Infrastructure.Interceptors;
using PlanRegistrationEntity = Microting.TimePlanningBase.Infrastructure.Data.Entities.PlanRegistration;

namespace TimePlanning.Pn.Test;

/// <summary>
/// The fixture attaches the day-lock interceptor to its own contexts, so every
/// other lock test would stay green if production stopped attaching it. These
/// tests go through production's two context builders instead: deleting the
/// interceptor from either one switches the lock off, and must fail here.
/// </summary>
[TestFixture]
public class DayLockWiringTests : TestBaseSetup
{
    [SetUp]
    public async Task SetUpTest() => await base.Setup();

    [Test]
    public async Task TheContextHelper_AttachesTheLock()
    {
        // Seeded through the fixture context, in lock-safe order: the earlier
        // row first, then the reconciled boundary that locks it.
        var earlier = new PlanRegistrationEntity
        {
            SdkSitId = 820, Date = new DateTime(2026, 1, 13),
            PlanText = "", CommentOffice = "", CommentOfficeAll = "",
            WorkflowState = Constants.WorkflowStates.Created,
            CreatedByUserId = 1, UpdatedByUserId = 1,
        };
        await earlier.Create(TimePlanningPnDbContext!);
        await new PlanRegistrationEntity
        {
            SdkSitId = 820, Date = new DateTime(2026, 1, 16), Reconciled = true,
            ReconciledAt = new DateTime(2026, 1, 20, 9, 12, 0),
            PlanText = "", CommentOffice = "", CommentOfficeAll = "",
            WorkflowState = Constants.WorkflowStates.Created,
            CreatedByUserId = 1, UpdatedByUserId = 1,
        }.Create(TimePlanningPnDbContext!);

        await using var productionContext =
            new TimePlanningDbContextHelper(PluginConnectionString).GetDbContext();
        var locked = await productionContext.PlanRegistrations
            .AsTracking()
            .FirstAsync(x => x.Id == earlier.Id);
        locked.PlanHours = 9;

        Assert.ThrowsAsync<DayLockedException>(async () => await locked.Update(productionContext));
    }

    [Test]
    public void ThePooledRegistration_AttachesTheLock()
    {
        // An explicit server version, so building the options opens no
        // connection; production passes ServerVersion.AutoDetect here.
        var builder = new DbContextOptionsBuilder<TimePlanningPnDbContext>();
        EformTimePlanningPlugin.ConfigureTimePlanningDbContext(
            builder, PluginConnectionString, new MariaDbServerVersion(new Version(10, 5, 0)));

        // Null-safe, so a missing interceptor fails the assertion below rather
        // than throwing: without AddInterceptors there may be no interceptor
        // list, or no core extension at all.
        var interceptors = builder.Options.FindExtension<CoreOptionsExtension>()?.Interceptors ?? [];

        Assert.That(interceptors, Does.Contain(ReconciledDayLockInterceptor.Instance));
    }
}
