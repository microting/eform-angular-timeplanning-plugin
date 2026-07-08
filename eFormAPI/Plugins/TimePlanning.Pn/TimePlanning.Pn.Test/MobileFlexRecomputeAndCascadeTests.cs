using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microting.eFormApi.BasePn.Abstractions;
using Microting.eFormApi.BasePn.Infrastructure.Database.Entities;
using Microting.eFormApi.BasePn.Infrastructure.Helpers.PluginDbOptions;
using Microting.EformAngularFrontendBase.Infrastructure.Data;
using Microting.eForm.Infrastructure.Constants;
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
using SdkSite = Microting.eForm.Infrastructure.Data.Entities.Site;
using SdkSiteWorker = Microting.eForm.Infrastructure.Data.Entities.SiteWorker;
using SdkWorker = Microting.eForm.Infrastructure.Data.Entities.Worker;

namespace TimePlanning.Pn.Test;

/// <summary>
/// Pins the mobile-vs-web flex divergence on UseOneMinuteIntervals sites.
///
/// Root cause: the web grid (<see cref="TimePlanningWorkingHoursService.Index"/>)
/// recomputes the running flex balance from the second-precision columns, but the
/// mobile period-status hero
/// (<see cref="TimePlanningWorkingHoursService.CalculateHoursSummary"/>) used to read
/// the <c>double</c> <c>SumFlexEnd</c> column verbatim. That double is rewritten —
/// and left inconsistent with the <c>*InSeconds</c> source of truth — by
/// <c>CreateUpdate</c>'s forward cascade, which recomputed only the doubles and
/// ignored <c>NettoHoursOverrideActive</c>. So after editing an early day the
/// mobile summary disagreed with the web grid.
///
/// The two tests here FAIL on the pre-fix code (documenting the bug) and PASS once
/// (1) <c>CalculateHoursSummary</c> recomputes via the shared chain and
/// (2) the cascade routes UseOneMinuteIntervals rows through
/// <c>ApplyNettoFlexChainSecondPrecision</c>.
/// </summary>
[TestFixture]
public class MobileFlexRecomputeAndCascadeTests : TestBaseSetup
{
    private const int SiteUid = 8123;
    private const string UserEmail = "flexuser@example.com";

    private TimePlanningWorkingHoursService _service = null!;

    [SetUp]
    public async Task SetUpTest()
    {
        await base.Setup();

        var core = await GetCore();
        var sdkDb = core.DbContextHelper.GetDbContext();

        // --- SDK graph: site + worker + siteworker, keyed by the user's email ---
        var language = await sdkDb.Languages.FirstOrDefaultAsync(l => l.LanguageCode == "da");
        if (language == null)
        {
            language = new SdkLanguage { LanguageCode = "da", Name = "Danish" };
            await language.Create(sdkDb);
        }

        var site = new SdkSite { Name = $"Site {SiteUid}", MicrotingUid = SiteUid };
        await site.Create(sdkDb);
        var worker = new SdkWorker
        {
            FirstName = "Flex",
            LastName = "User",
            Email = UserEmail,
            MicrotingUid = 1000 + SiteUid
        };
        await worker.Create(sdkDb);
        await new SdkSiteWorker
        {
            SiteId = site.Id,
            WorkerId = worker.Id,
            MicrotingUid = 2000 + SiteUid
        }.Create(sdkDb);

        // --- Base frontend context: one EformUser matched by email ---
        var sdkConn = sdkDb.Database.GetConnectionString()!;
        var baseConn = sdkConn.Replace("420_SDK", "420_baseflextest");
        var baseOptions = new DbContextOptionsBuilder<BaseDbContext>()
            .UseMySql(baseConn, new MariaDbServerVersion(ServerVersion.AutoDetect(sdkConn)))
            .Options;
        var baseDbContext = new BaseDbContext(baseOptions);
        baseDbContext.Database.EnsureDeleted();
        baseDbContext.Database.EnsureCreated();
        var eformUser = new EformUser
        {
            FirstName = "Flex",
            LastName = "User",
            UserName = UserEmail,
            NormalizedUserName = UserEmail.ToUpperInvariant(),
            Email = UserEmail,
            NormalizedEmail = UserEmail.ToUpperInvariant(),
            SecurityStamp = Guid.NewGuid().ToString(),
            ConcurrencyStamp = Guid.NewGuid().ToString()
        };
        baseDbContext.Users.Add(eformUser);
        await baseDbContext.SaveChangesAsync();

        // --- AssignedSite: UseOneMinuteIntervals ON (the affected population) ---
        await new AssignedSiteEntity
        {
            SiteId = SiteUid,
            UseOneMinuteIntervals = true,
            WorkflowState = Constants.WorkflowStates.Created,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        }.Create(TimePlanningPnDbContext!);

        // --- Service under test with real base context + mocked user service ---
        var userService = Substitute.For<IUserService>();
        userService.UserId.Returns(1);
        userService.GetCurrentUserLanguage().Returns(language);
        userService.GetCurrentUserAsync().Returns(eformUser);

        var localizationService = Substitute.For<ITimePlanningLocalizationService>();
        localizationService.GetString(Arg.Any<string>()).Returns(x => x[0]?.ToString());

        var coreService = Substitute.For<IEFormCoreService>();
        coreService.GetCore().Returns(core);

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
            baseDbContext,
            options,
            coreService);
    }

    /// <summary>
    /// Seeds a UseOneMinuteIntervals row whose double and *InSeconds flex columns
    /// are given explicitly, so a test can start them in sync (or deliberately
    /// out of sync).
    /// </summary>
    private async Task SeedRow(
        DateTime date,
        int start1Id, int stop1Id,
        int nettoSeconds, int flexSeconds,
        int sumFlexStartSeconds, int sumFlexEndSeconds,
        int planHoursSeconds,
        double doubleSumFlexEndOverride,
        bool overrideActive = false, double nettoHoursOverride = 0)
    {
        await new PlanRegistrationEntity
        {
            SdkSitId = SiteUid,
            Date = date,
            Start1Id = start1Id,
            Stop1Id = stop1Id,
            PlanHours = planHoursSeconds / 3600.0,
            PlanHoursInSeconds = planHoursSeconds,
            NettoHours = nettoSeconds / 3600.0,
            NettoHoursInSeconds = nettoSeconds,
            Flex = flexSeconds / 3600.0,
            FlexInSeconds = flexSeconds,
            SumFlexStart = sumFlexStartSeconds / 3600.0,
            SumFlexStartInSeconds = sumFlexStartSeconds,
            SumFlexEnd = doubleSumFlexEndOverride,
            SumFlexEndInSeconds = sumFlexEndSeconds,
            NettoHoursOverrideActive = overrideActive,
            NettoHoursOverride = nettoHoursOverride,
            RegisteredUnderOneMinuteIntervals = true,
            PlanText = "",
            CommentOffice = "",
            CommentOfficeAll = "",
            WorkflowState = Constants.WorkflowStates.Created,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        }.Create(TimePlanningPnDbContext!);
    }

    // ------------------------------------------------------------------
    // (a) End-to-end: an early-day edit drives the forward cascade; the web
    //     grid and the mobile summary must agree afterwards, and the cascade
    //     must not leave the double SumFlexEnd inconsistent with the seconds.
    // ------------------------------------------------------------------
    [Test]
    public async Task EarlyEdit_Cascade_WebGridAndMobileSummaryAgree_AndDoubleMatchesSeconds()
    {
        var anchorDate = new DateTime(2026, 3, 2); // opening balance carrier
        var d1 = new DateTime(2026, 3, 3);         // EARLY day to edit
        var d2 = new DateTime(2026, 3, 4);         // later, NettoHoursOverride active
        var d3 = new DateTime(2026, 3, 5);         // later, normal

        // Opening balance = +1h (3600s), everything in sync.
        await SeedRow(anchorDate, 98, 205, nettoSeconds: 32100, flexSeconds: 3600,
            sumFlexStartSeconds: 0, sumFlexEndSeconds: 3600, planHoursSeconds: 28800,
            doubleSumFlexEndOverride: 3600 / 3600.0);
        // D1: 08:05-16:00 = 28500s, plan 8h => flex -300. SumFlex 3600 -> 3300.
        await SeedRow(d1, 98, 193, nettoSeconds: 28500, flexSeconds: -300,
            sumFlexStartSeconds: 3600, sumFlexEndSeconds: 3300, planHoursSeconds: 28800,
            doubleSumFlexEndOverride: 3300 / 3600.0);
        // D2: worked 28500s but NettoHoursOverride=10h => flex uses override: 36000-28800=7200.
        await SeedRow(d2, 98, 193, nettoSeconds: 28500, flexSeconds: 7200,
            sumFlexStartSeconds: 3300, sumFlexEndSeconds: 10500, planHoursSeconds: 28800,
            doubleSumFlexEndOverride: 10500 / 3600.0,
            overrideActive: true, nettoHoursOverride: 10.0);
        // D3: 28500s, plan 8h => flex -300. SumFlex 10500 -> 10200.
        await SeedRow(d3, 98, 193, nettoSeconds: 28500, flexSeconds: -300,
            sumFlexStartSeconds: 10500, sumFlexEndSeconds: 10200, planHoursSeconds: 28800,
            doubleSumFlexEndOverride: 10200 / 3600.0);

        // Edit ONLY the early day D1: extend Stop1 to 20:00 (Id 241) => 42900s worked.
        // This triggers CreateUpdate's forward cascade over D2 and D3.
        var edit = new TimePlanningWorkingHoursUpdateCreateModel
        {
            SiteId = SiteUid,
            Plannings = new System.Collections.Generic.List<TimePlanningWorkingHoursModel>
            {
                new()
                {
                    Date = d1,
                    Shift1Start = 98,
                    Shift1Stop = 241,
                    Shift1Pause = 0,
                    PlanHours = 8.0,
                    NettoHours = 0,   // recomputed from shifts on a one-minute site
                    FlexHours = 0,    // recomputed
                    PaidOutFlex = "0",
                    Message = 10,     // 10 => MessageId null
                    PlanText = "",
                    CommentOffice = "",
                    CommentOfficeAll = "",
                    CommentWorker = ""
                }
            }
        };
        var updateResult = await _service.CreateUpdate(edit);
        Assert.That(updateResult.Success, Is.True, updateResult.Message);

        // Web grid recompute (correct oracle).
        var index = await _service.Index(new TimePlanningWorkingHoursRequestModel
        {
            SiteId = SiteUid,
            DateFrom = d1,
            DateTo = d3
        });
        Assert.That(index.Success, Is.True, index.Message);
        var webLastDayFlex = index.Model.Single(x => x.Date == d3).SumFlexEnd;

        // Mobile period-status hero.
        var summary = await _service.CalculateHoursSummary(d1, d3, null, null, null, null);
        Assert.That(summary.Success, Is.True, summary.Message);
        var mobileDifference = summary.Model.Difference;

        // Inspect the cascaded later day's stored columns.
        var d3Row = await TimePlanningPnDbContext!.PlanRegistrations
            .AsNoTracking().SingleAsync(x => x.SdkSitId == SiteUid && x.Date == d3);

        Assert.Multiple(() =>
        {
            // (1) The mobile summary must agree with the web grid. Pre-fix the mobile
            //     read of the (override-blind, cascade-rewritten) double diverges.
            Assert.That(mobileDifference, Is.EqualTo(webLastDayFlex).Within(1e-6),
                "Mobile CalculateHoursSummary.Difference must match the web grid's recomputed last-day flex.");

            // (2) The cascade must leave the double SumFlexEnd consistent with the
            //     seconds source of truth. Pre-fix the override-blind double drifts.
            Assert.That(d3Row.SumFlexEndInSeconds / 3600.0, Is.EqualTo(d3Row.SumFlexEnd).Within(1e-6),
                "Cascaded row's double SumFlexEnd must equal SumFlexEndInSeconds/3600.");

            // And the agreed value is the seconds-chain truth:
            //   opening 3600 + D1 14100 + D2 override 7200 + D3 -300 = 24600s = 6.8333h
            Assert.That(mobileDifference, Is.EqualTo(24600 / 3600.0).Within(1e-6));
        });
    }

    // ------------------------------------------------------------------
    // (b) Direct: a stale double SumFlexEnd with correct seconds columns —
    //     CalculateHoursSummary must return the seconds-derived value.
    // ------------------------------------------------------------------
    [Test]
    public async Task CalculateHoursSummary_OneMinuteSite_UsesSecondsDerivedFlex_NotStaleDouble()
    {
        var d1 = new DateTime(2026, 4, 1);
        var d2 = new DateTime(2026, 4, 2);

        // Seconds columns correct (each day +1h): end-of-period = 2h = 7200s.
        // Double SumFlexEnd deliberately garbage to prove it is not read.
        await SeedRow(d1, 98, 205, nettoSeconds: 32400, flexSeconds: 3600,
            sumFlexStartSeconds: 0, sumFlexEndSeconds: 3600, planHoursSeconds: 28800,
            doubleSumFlexEndOverride: 99.0);
        await SeedRow(d2, 98, 205, nettoSeconds: 32400, flexSeconds: 3600,
            sumFlexStartSeconds: 3600, sumFlexEndSeconds: 7200, planHoursSeconds: 28800,
            doubleSumFlexEndOverride: 88.0);

        var summary = await _service.CalculateHoursSummary(d1, d2, null, null, null, null);
        Assert.That(summary.Success, Is.True, summary.Message);

        Assert.Multiple(() =>
        {
            Assert.That(summary.Model.Difference, Is.EqualTo(7200 / 3600.0).Within(1e-6),
                "Difference must be the seconds-derived 2.0h, not the stale double 88.0.");
            Assert.That(summary.Model.Difference, Is.Not.EqualTo(88.0).Within(1e-6),
                "The stale double SumFlexEnd column must never be surfaced.");
        });
    }
}
