using System;
using System.Collections.Generic;
using System.Linq;
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
using SdkLanguage = Microting.eForm.Infrastructure.Data.Entities.Language;

namespace TimePlanning.Pn.Test;

/// <summary>
/// Regression coverage for the message-id persistence fix in the working-hours
/// update path (<c>CreateUpdate</c> → <c>UpdatePlanning</c>):
/// <c>planRegistration.MessageId = model.Message == 0 ? null : model.Message;</c>
/// The sentinel used to be <c>== 10</c>, which silently DISCARDED the Maternity
/// message (Id 10) on every save. Tested through the public
/// <c>CreateUpdate(TimePlanningWorkingHoursUpdateCreateModel)</c> API, asserting
/// the persisted <c>PlanRegistration.MessageId</c> from a FRESH DbContext.
/// Also asserts the seeded Messages table carries the new Id 13
/// "PregnancyLeave" row after TimePlanningPluginSeed.SeedData (run by
/// TestBaseSetup for every test).
/// </summary>
[TestFixture]
public class WorkingHoursMessagePersistenceTests : TestBaseSetup
{
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
        var core = await GetCore();
        coreService.GetCore().Returns(core);

        var sdkDb = core.DbContextHelper.GetDbContext();
        var language = await sdkDb.Languages.FirstOrDefaultAsync(l => l.LanguageCode == "da");
        if (language == null)
        {
            language = new SdkLanguage { LanguageCode = "da", Name = "Danish" };
            await language.Create(sdkDb);
        }
        userService.GetCurrentUserLanguage().Returns(language);

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
    }

    // ------------------------------------------------------------------
    // 1. THE bug regression: Message = 10 (Maternity) must persist. The old
    //    "model.Message == 10 ? null : ..." sentinel silently discarded it.
    // ------------------------------------------------------------------

    [Test]
    public async Task CreateUpdate_Message10_PersistsMaternityMessageId()
    {
        var date = UpdateDate();
        await SeedSiteAndPlanRegistration(siteUid: 9451, date: date, initialMessageId: null);

        var result = await _service.CreateUpdate(BuildUpdateModel(9451, date, message: 10));
        Assert.That(result.Success, Is.True, result.Message);

        Assert.That(await ReadPersistedMessageId(9451, date), Is.EqualTo(10),
            "Message 10 (Maternity) must persist — the old '== 10' sentinel discarded it");
    }

    // ------------------------------------------------------------------
    // 2. Message = 0 is the real "no message" sentinel: clears MessageId.
    // ------------------------------------------------------------------

    [Test]
    public async Task CreateUpdate_Message0_ClearsMessageId()
    {
        var date = UpdateDate();
        await SeedSiteAndPlanRegistration(siteUid: 9452, date: date, initialMessageId: 10);

        var result = await _service.CreateUpdate(BuildUpdateModel(9452, date, message: 0));
        Assert.That(result.Success, Is.True, result.Message);

        Assert.That(await ReadPersistedMessageId(9452, date), Is.Null,
            "Message 0 is the 'no message' sentinel and must clear MessageId");
    }

    // ------------------------------------------------------------------
    // 3. The new seed message Id 13 (PregnancyLeave) round-trips.
    // ------------------------------------------------------------------

    [Test]
    public async Task CreateUpdate_Message13_PersistsPregnancyLeaveMessageId()
    {
        var date = UpdateDate();
        await SeedSiteAndPlanRegistration(siteUid: 9453, date: date, initialMessageId: null);

        var result = await _service.CreateUpdate(BuildUpdateModel(9453, date, message: 13));
        Assert.That(result.Success, Is.True, result.Message);

        Assert.That(await ReadPersistedMessageId(9453, date), Is.EqualTo(13),
            "The new PregnancyLeave message (Id 13) must round-trip through the update path");
    }

    // ------------------------------------------------------------------
    // 4. Control: an ordinary message id (3 = Sick) persists as before.
    // ------------------------------------------------------------------

    [Test]
    public async Task CreateUpdate_Message3_PersistsSickMessageId()
    {
        var date = UpdateDate();
        await SeedSiteAndPlanRegistration(siteUid: 9454, date: date, initialMessageId: null);

        var result = await _service.CreateUpdate(BuildUpdateModel(9454, date, message: 3));
        Assert.That(result.Success, Is.True, result.Message);

        Assert.That(await ReadPersistedMessageId(9454, date), Is.EqualTo(3),
            "Control: an ordinary message id must persist unchanged");
    }

    // ------------------------------------------------------------------
    // 5. Seeded Messages table: TimePlanningPluginSeed.SeedData (run by
    //    TestBaseSetup) must have inserted the new Id 13 PregnancyLeave row.
    // ------------------------------------------------------------------

    [Test]
    public void SeededMessagesTable_ContainsPregnancyLeaveRowWithId13()
    {
        var messages = TimePlanningPnDbContext!.Messages
            .Where(x => x.Id == 13)
            .ToList();

        Assert.That(messages.Count, Is.EqualTo(1),
            "Exactly one seeded message with Id 13");
        Assert.That(messages[0].Name, Is.EqualTo("PregnancyLeave"));
        Assert.That(messages[0].DaName, Is.EqualTo("Graviditetsbetinget fravær"));
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    /// <summary>
    /// A midnight date relative to now, 10 days out. It must NOT be today:
    /// UpdatePlanning skips the whole field-copy block (including MessageId)
    /// for today's row ("Date != midnight" guard). Well below the 180-day
    /// horizon of CreateUpdate's forward-cascade.
    /// </summary>
    private static DateTime UpdateDate() => DateTime.Now.Date.AddDays(10);

    private static TimePlanningWorkingHoursUpdateCreateModel BuildUpdateModel(
        int siteUid, DateTime date, int message)
    {
        return new TimePlanningWorkingHoursUpdateCreateModel
        {
            SiteId = siteUid,
            Plannings = new List<TimePlanningWorkingHoursModel>
            {
                new()
                {
                    Date = date,
                    Message = message,
                    PlanText = "",
                    PlanHours = 0,
                    NettoHours = 0,
                    PaidOutFlex = "0",
                    CommentOffice = "",
                    CommentOfficeAll = ""
                }
            }
        };
    }

    /// <summary>
    /// Re-reads the persisted MessageId from a FRESH DbContext so the assert
    /// cannot be satisfied by the service's tracked (unsaved) entity state.
    /// </summary>
    private async Task<int?> ReadPersistedMessageId(int siteUid, DateTime date)
    {
        await using var freshContext = CreateTimePlanningPnDbContext();
        var planRegistration = await freshContext.PlanRegistrations
            .AsNoTracking()
            .SingleAsync(x => x.SdkSitId == siteUid && x.Date == date);
        return planRegistration.MessageId;
    }

    /// <summary>
    /// Seeds the plugin-side entities the update path needs: an AssignedSite
    /// (UpdatePlanning dereferences it inside PlanRegistrationHelper.
    /// UpdatePlanRegistration) and one existing PlanRegistration on
    /// <paramref name="date"/> for CreateUpdate to match and update. No SDK
    /// entities are required — CreateUpdate only touches the plugin DbContext.
    /// </summary>
    private async Task SeedSiteAndPlanRegistration(int siteUid, DateTime date, int? initialMessageId)
    {
        await new AssignedSiteEntity
        {
            SiteId = siteUid,
            UseOneMinuteIntervals = false,
            Resigned = false,
            WorkflowState = Constants.WorkflowStates.Created,
            CreatedByUserId = 1,
            UpdatedByUserId = 1,
        }.Create(TimePlanningPnDbContext!);

        await new PlanRegistrationEntity
        {
            SdkSitId = siteUid,
            Date = date,
            Start1Id = 0,
            Stop1Id = 0,
            Pause1Id = 0,
            MessageId = initialMessageId,
            PlanText = "",
            CommentOffice = "",
            CommentOfficeAll = "",
            WorkflowState = Constants.WorkflowStates.Created,
            CreatedByUserId = 1,
            UpdatedByUserId = 1,
        }.Create(TimePlanningPnDbContext!);
    }
}
