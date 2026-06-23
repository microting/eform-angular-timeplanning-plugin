using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microting.eFormApi.BasePn.Abstractions;
using Microting.eFormApi.BasePn.Infrastructure.Helpers.PluginDbOptions;
using Microting.eFormApi.BasePn.Infrastructure.Database.Entities;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using AssignedSiteEntity = Microting.TimePlanningBase.Infrastructure.Data.Entities.AssignedSite;
using SdkSite = Microting.eForm.Infrastructure.Data.Entities.Site;
using NSubstitute;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Helpers;
using TimePlanning.Pn.Infrastructure.Models.Planning;
using TimePlanning.Pn.Infrastructure.Models.Settings;
using TimePlanning.Pn.Services.TimePlanningLocalizationService;
using TimePlanning.Pn.Services.TimePlanningPlanningService;

namespace TimePlanning.Pn.Test;

/// <summary>
/// Failing-test-first reproduction for the confirmed symptom: a message/status
/// (e.g. MessageId=2 Vacation, MessageId=1 DayOff) set for an assignedSite IS
/// persisted on PlanRegistration.MessageId and shows on the WEB, but the MOBILE
/// APP schedule (gRPC GetPlanningsByUser) does NOT show it.
///
/// READ PATH under test (closest reliable seam to GetPlanningsByUser):
///   TimePlanningPlanningsGrpcService.GetPlanningsByUser
///     -> TimePlanningPlanningService.IndexByCurrentUserName
///       -> PlanRegistrationHelper.UpdatePlanRegistrationsInPeriod   <-- builds the
///          per-day TimePlanningPlanningPrDayModel with `int? Message`.
///
/// IndexByCurrentUserName additionally requires real BaseDbContext.Users +
/// sdkDbContext.Sites + Worker/SiteWorker wiring that the rest of this suite
/// documents as the currently-[Ignore]d fixture gap (see
/// PlanningServiceMultiShiftTests.Index_OneMinuteInterval_* and
/// SettingsServiceExtendedTests). UpdatePlanRegistrationsInPeriod is the
/// smallest unit that still constructs the exact day model the gRPC mapper
/// (TimePlanningPlanningsGrpcService.MapDayToGrpc) reads `Message` from, so it
/// is the seam exercised here — identical to how the existing
/// Index_OneMinuteInterval_* tests pin the per-day model fields.
///
/// EXPECTED TO FAIL on current code if the read path drops Message. Two
/// separate cases (with-hours day vs message-only day) so CI tells us WHICH
/// scenario fails.
/// </summary>
[TestFixture]
public class ScheduleMessageReadTests : TestBaseSetup
{
    private IUserService _userService;
    private ITimePlanningLocalizationService _localizationService;
    private IPluginDbOptions<TimePlanningBaseSettings> _options;

    // MessageId values as the web write sets them (PlanRegistration.MessageId).
    private const int MessageVacation = 2; // Vacation
    private const int MessageDayOff = 1;   // DayOff

    [SetUp]
    public async Task SetUpTest()
    {
        await base.Setup();

        _userService = Substitute.For<IUserService>();
        _userService.UserId.Returns(1);
        _userService.GetCurrentUserAsync().Returns(new EformUser { Id = 1 });

        _localizationService = Substitute.For<ITimePlanningLocalizationService>();
        _localizationService.GetString(Arg.Any<string>()).Returns(x => x[0]?.ToString());

        _options = Substitute.For<IPluginDbOptions<TimePlanningBaseSettings>>();
        _options.Value.Returns(new TimePlanningBaseSettings
        {
            AutoBreakCalculationActive = "0",
            DayOfPayment = 20,
            GpsEnabled = "0",
            SnapshotEnabled = "0"
        });
    }

    /// <summary>
    /// Builds the per-day models for a date range covering both seeded days,
    /// the way IndexByCurrentUserName does for "current user".
    /// </summary>
    private async Task<TimePlanningPlanningModel> ReadDaysByCurrentUser(
        AssignedSiteEntity assignedSite, SdkSite sdkSite, DateTime from, DateTime to,
        string? messageLanguage = null)
    {
        var planningsInPeriod = await TimePlanningPnDbContext!.PlanRegistrations
            .AsNoTracking()
            .Where(x => x.SdkSitId == assignedSite.SiteId)
            .Select(x => new PlanRegistration { Id = x.Id, Date = x.Date })
            .OrderByDescending(x => x.Date)
            .ToListAsync();

        var siteModel = new TimePlanningPlanningModel
        {
            SiteId = assignedSite.SiteId,
            SiteName = sdkSite.Name,
            UseOneMinuteIntervals = assignedSite.UseOneMinuteIntervals,
            PlanningPrDayModels = new List<TimePlanningPlanningPrDayModel>()
        };

        return await PlanRegistrationHelper.UpdatePlanRegistrationsInPeriod(
            planningsInPeriod,
            siteModel,
            TimePlanningPnDbContext,
            assignedSite,
            Substitute.For<ILogger<TimePlanningPlanningService>>(),
            sdkSite,
            from,
            to,
            _options,
            messageLanguage);
    }

    /// <summary>
    /// Inserts a Message catalog row directly, mirroring
    /// MessagesCodeFirst / AbsenceRequestDayUTest seeding. The test container
    /// does not run the plugin's startup message seed, so rows the assertions
    /// rely on are inserted explicitly in Arrange.
    /// </summary>
    private async Task SeedMessage(int id, string name, string daName, string enName, string deName)
    {
        if (await TimePlanningPnDbContext!.Messages.AnyAsync(m => m.Id == id))
        {
            return;
        }
        TimePlanningPnDbContext.Messages.Add(new Message(id, name, daName, enName, deName));
        await TimePlanningPnDbContext.SaveChangesAsync();
    }

    [Test]
    public async Task GetPlanningsByUser_WithHoursDay_CarriesMessageId()
    {
        // Arrange — assigned site + sdk site wiring the read path needs.
        var assignedSite = new AssignedSiteEntity
        {
            SiteId = 7100,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await assignedSite.Create(TimePlanningPnDbContext);
        var sdkSite = new SdkSite { Name = "Test site 7100", MicrotingUid = 7100 };

        // A day WITH planned hours AND MessageId=2 (Vacation). MessageId is set
        // exactly as the web write does — directly on PlanRegistration.MessageId.
        var date = new DateTime(2026, 4, 29, 0, 0, 0, DateTimeKind.Utc); // Wednesday
        var planning = new PlanRegistration
        {
            SdkSitId = 7100,
            Date = date,
            PlanText = "8",
            PlanHours = 8,
            PlannedStartOfShift1 = 480, // 08:00
            PlannedEndOfShift1 = 960,   // 16:00
            MessageId = MessageVacation,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await planning.Create(TimePlanningPnDbContext);

        // Act — read the period by current user (as the app would).
        var result = await ReadDaysByCurrentUser(
            assignedSite, sdkSite, date.AddDays(-1), date.AddDays(1));

        // Assert — the returned day model must carry the seeded MessageId.
        var prDay = result.PlanningPrDayModels.Single(x => x.Date.Date == date.Date);
        Assert.That(prDay.Message, Is.EqualTo(MessageVacation),
            "Read path dropped MessageId for a with-hours day (web shows it; app does not).");
    }

    [Test]
    public async Task GetPlanningsByUser_MessageOnlyDay_CarriesMessageId()
    {
        // Arrange — assigned site + sdk site.
        var assignedSite = new AssignedSiteEntity
        {
            SiteId = 7101,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await assignedSite.Create(TimePlanningPnDbContext);
        var sdkSite = new SdkSite { Name = "Test site 7101", MicrotingUid = 7101 };

        // A MESSAGE-ONLY day: no shifts/hours, only MessageId=1 (DayOff).
        var date = new DateTime(2026, 4, 30, 0, 0, 0, DateTimeKind.Utc); // Thursday
        var planning = new PlanRegistration
        {
            SdkSitId = 7101,
            Date = date,
            MessageId = MessageDayOff,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await planning.Create(TimePlanningPnDbContext);

        // Act
        var result = await ReadDaysByCurrentUser(
            assignedSite, sdkSite, date.AddDays(-1), date.AddDays(1));

        // Assert
        var prDay = result.PlanningPrDayModels.Single(x => x.Date.Date == date.Date);
        Assert.That(prDay.Message, Is.EqualTo(MessageDayOff),
            "Read path dropped MessageId for a message-only day (web shows it; app does not).");
    }

    [Test]
    public async Task GetPlanningsByUser_MappedMessage_ResolvesDanishLabel()
    {
        // Arrange — a normal, app-mapped id (2 = Vacation) in a Danish site.
        await SeedMessage(MessageVacation, "Vacation", "Ferie", "Vacation", "Ferien");

        var assignedSite = new AssignedSiteEntity
        {
            SiteId = 7200,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await assignedSite.Create(TimePlanningPnDbContext);
        var sdkSite = new SdkSite { Name = "Test site 7200", MicrotingUid = 7200 };

        var date = new DateTime(2026, 4, 29, 0, 0, 0, DateTimeKind.Utc);
        var planning = new PlanRegistration
        {
            SdkSitId = 7200,
            Date = date,
            MessageId = MessageVacation,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await planning.Create(TimePlanningPnDbContext);

        // Act — read with the site language "da".
        var result = await ReadDaysByCurrentUser(
            assignedSite, sdkSite, date.AddDays(-1), date.AddDays(1), "da");

        // Assert — the day model carries the localized label, not just the int.
        var prDay = result.PlanningPrDayModels.Single(x => x.Date.Date == date.Date);
        Assert.That(prDay.MessageLabel, Is.EqualTo("Ferie"),
            "MessageLabel not resolved for a mapped id in Danish.");
    }

    [Test]
    public async Task GetPlanningsByUser_AppUnmappedMessage_StillResolvesLabel()
    {
        // Arrange — an id the mobile app does NOT hard-code (the previously
        // invisible case). It must still resolve to its localized label.
        const int unmappedId = 42;
        await SeedMessage(unmappedId, "Time off in lieu", "Afspadsering", "Time off in lieu", "Freizeitausgleich");

        var assignedSite = new AssignedSiteEntity
        {
            SiteId = 7201,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await assignedSite.Create(TimePlanningPnDbContext);
        var sdkSite = new SdkSite { Name = "Test site 7201", MicrotingUid = 7201 };

        var date = new DateTime(2026, 4, 30, 0, 0, 0, DateTimeKind.Utc);
        var planning = new PlanRegistration
        {
            SdkSitId = 7201,
            Date = date,
            MessageId = unmappedId,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await planning.Create(TimePlanningPnDbContext);

        // Act
        var result = await ReadDaysByCurrentUser(
            assignedSite, sdkSite, date.AddDays(-1), date.AddDays(1), "da");

        // Assert — label resolved even though the app has no constant for it.
        var prDay = result.PlanningPrDayModels.Single(x => x.Date.Date == date.Date);
        Assert.That(prDay.MessageLabel, Is.EqualTo("Afspadsering"),
            "MessageLabel not resolved for an app-unmapped id (the symptom being fixed).");
    }

    [Test]
    public async Task GetPlanningsByUser_NoMessage_HasNoLabel()
    {
        // Arrange — a day with no MessageId at all.
        var assignedSite = new AssignedSiteEntity
        {
            SiteId = 7202,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await assignedSite.Create(TimePlanningPnDbContext);
        var sdkSite = new SdkSite { Name = "Test site 7202", MicrotingUid = 7202 };

        var date = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
        var planning = new PlanRegistration
        {
            SdkSitId = 7202,
            Date = date,
            PlanText = "8",
            PlanHours = 8,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await planning.Create(TimePlanningPnDbContext);

        // Act
        var result = await ReadDaysByCurrentUser(
            assignedSite, sdkSite, date.AddDays(-1), date.AddDays(1), "da");

        // Assert — null MessageId yields no label.
        var prDay = result.PlanningPrDayModels.Single(x => x.Date.Date == date.Date);
        Assert.That(prDay.Message, Is.Null);
        Assert.That(prDay.MessageLabel, Is.Null.Or.Empty,
            "MessageLabel should be null/empty when there is no MessageId.");
    }
}
