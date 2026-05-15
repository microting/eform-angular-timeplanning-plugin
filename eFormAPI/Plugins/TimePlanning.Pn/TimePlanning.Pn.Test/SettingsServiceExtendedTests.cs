using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microting.eForm.Infrastructure.Constants;
using Microting.eFormApi.BasePn.Abstractions;
using Microting.eFormApi.BasePn.Infrastructure.Helpers.PluginDbOptions;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using NSubstitute;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Models.Settings;
using TimePlanning.Pn.Services.TimePlanningLocalizationService;
using TimePlanning.Pn.Services.TimePlanningSettingService;
using AssignedSiteEntity = Microting.TimePlanningBase.Infrastructure.Data.Entities.AssignedSite;

namespace TimePlanning.Pn.Test;

[TestFixture]
public class SettingsServiceExtendedTests : TestBaseSetup
{
    private ISettingService _settingsService;
    private IUserService _userService;
    private ITimePlanningLocalizationService _localizationService;
    private IEFormCoreService _coreService;
    private IPluginDbOptions<TimePlanningBaseSettings> _options;

    [SetUp]
    public async Task SetUp()
    {
        await base.Setup();
        _userService = Substitute.For<IUserService>();
        _userService.UserId.Returns(1);

        _localizationService = Substitute.For<ITimePlanningLocalizationService>();
        _localizationService.GetString(Arg.Any<string>()).Returns(x => x[0]?.ToString());

        _coreService = Substitute.For<IEFormCoreService>();
        var core = await GetCore();
        _coreService.GetCore().Returns(core);

        _options = Substitute.For<IPluginDbOptions<TimePlanningBaseSettings>>();
        _options.Value.Returns(new TimePlanningBaseSettings
        {
            AutoBreakCalculationActive = "0",
            DayOfPayment = 20,
            GpsEnabled = "0",
            SnapshotEnabled = "0"
        });

        // Pass null for baseDbContext — avatar lookup will be skipped
        // when no matching user is found in SDK worker data
        _settingsService = new TimeSettingService(
            _options,
            TimePlanningPnDbContext,
            Substitute.For<ILogger<TimeSettingService>>(),
            _userService,
            _localizationService,
            null,
            _coreService);
    }

    // --- GetAvailableSites tests ---

    [Test]
    public async Task GetAvailableSites_InvalidToken_ReturnsTokenNotFound()
    {
        // Act
        var result = await _settingsService.GetAvailableSites("bad-token");

        // Assert
        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Is.EqualTo("Token not found"));
    }

    [Test]
    public async Task GetAvailableSites_NullToken_ReturnsTokenNotFound()
    {
        // Act — after 0ad1af89, GetAvailableSites is strictly device-token-only
        // (browser/personal mode now uses GetAvailableSitesByCurrentUser).
        // A null token falls through the RegistrationDevices lookup and matches no device.
        var result = await _settingsService.GetAvailableSites(null);

        // Assert — null token is treated like any other unknown token
        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Is.EqualTo("Token not found"));
    }

    [Test]
    public async Task GetAvailableSites_ValidToken_ReturnsSuccess()
    {
        // Arrange — create a registration device with a known token
        var device = new RegistrationDevice
        {
            Token = "valid-test-token",
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await device.Create(TimePlanningPnDbContext);

        // Act
        var result = await _settingsService.GetAvailableSites("valid-test-token");

        // Assert — token is valid, returns empty site list
        Assert.That(result.Success, Is.True);
        Assert.That(result.Model, Is.Not.Null);
    }

    // --- GetAvailableSitesByCurrentUser tests ---

    [Test]
    public async Task GetAvailableSitesByCurrentUser_NoSites_ReturnsEmptyList()
    {
        // Act
        var result = await _settingsService.GetAvailableSitesByCurrentUser();

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.Model, Is.Not.Null);
        Assert.That(result.Model.Count, Is.EqualTo(0));
    }

    [Test]
    public async Task GetAvailableSitesByCurrentUser_ExcludesResignedSites()
    {
        // Arrange — create one active and one resigned AssignedSite
        var core = await _coreService.GetCore();
        var sdkDbContext = core.DbContextHelper.GetDbContext();

        // Create SDK entities for active site
        var site1 = new Microting.eForm.Infrastructure.Data.Entities.Site
        {
            Name = "Active Worker",
            MicrotingUid = 100
        };
        await site1.Create(sdkDbContext);

        var worker1 = new Microting.eForm.Infrastructure.Data.Entities.Worker
        {
            FirstName = "Active",
            LastName = "Worker",
            Email = "active@test.com",
            MicrotingUid = 200
        };
        await worker1.Create(sdkDbContext);

        var siteWorker1 = new Microting.eForm.Infrastructure.Data.Entities.SiteWorker
        {
            SiteId = site1.Id,
            WorkerId = worker1.Id,
            MicrotingUid = 300
        };
        await siteWorker1.Create(sdkDbContext);

        var unit1 = new Microting.eForm.Infrastructure.Data.Entities.Unit
        {
            SiteId = site1.Id,
            MicrotingUid = 400,
            CustomerNo = 1,
            OtpCode = 1234
        };
        await unit1.Create(sdkDbContext);

        // Ensure language exists
        var language = await sdkDbContext.Languages.FirstOrDefaultAsync();
        if (language == null)
        {
            language = new Microting.eForm.Infrastructure.Data.Entities.Language
            {
                LanguageCode = "en",
                Name = "English"
            };
            await language.Create(sdkDbContext);
        }
        site1.LanguageId = language.Id;
        await site1.Update(sdkDbContext);

        // Create SDK entities for resigned site
        var site2 = new Microting.eForm.Infrastructure.Data.Entities.Site
        {
            Name = "Resigned Worker",
            MicrotingUid = 101
        };
        await site2.Create(sdkDbContext);

        // TimePlanning AssignedSites
        var activeSite = new AssignedSiteEntity
        {
            SiteId = 100,
            Resigned = false,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await activeSite.Create(TimePlanningPnDbContext);

        var resignedSite = new AssignedSiteEntity
        {
            SiteId = 101,
            Resigned = true,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await resignedSite.Create(TimePlanningPnDbContext);

        // Act
        var result = await _settingsService.GetAvailableSitesByCurrentUser();

        // Assert — only the active site should be returned
        Assert.That(result.Success, Is.True);
        Assert.That(result.Model, Is.Not.Null);
        Assert.That(result.Model.Count, Is.EqualTo(1));
        Assert.That(result.Model[0].SiteName, Is.EqualTo("Active Worker"));
        Assert.That(result.Model[0].SiteId, Is.EqualTo(100));
    }

    [Test]
    public async Task GetAvailableSitesByCurrentUser_ReturnsSiteWithCorrectFields()
    {
        // Arrange
        var core = await _coreService.GetCore();
        var sdkDbContext = core.DbContextHelper.GetDbContext();

        var site = new Microting.eForm.Infrastructure.Data.Entities.Site
        {
            Name = "Test Worker",
            MicrotingUid = 500
        };
        await site.Create(sdkDbContext);

        var worker = new Microting.eForm.Infrastructure.Data.Entities.Worker
        {
            FirstName = "Test",
            LastName = "Worker",
            Email = "test@test.com",
            PinCode = "1234",
            MicrotingUid = 600
        };
        await worker.Create(sdkDbContext);

        var siteWorker = new Microting.eForm.Infrastructure.Data.Entities.SiteWorker
        {
            SiteId = site.Id,
            WorkerId = worker.Id,
            MicrotingUid = 700
        };
        await siteWorker.Create(sdkDbContext);

        var unit = new Microting.eForm.Infrastructure.Data.Entities.Unit
        {
            SiteId = site.Id,
            MicrotingUid = 800,
            CustomerNo = 42,
            OtpCode = 5678
        };
        await unit.Create(sdkDbContext);

        var language = await sdkDbContext.Languages.FirstOrDefaultAsync();
        if (language == null)
        {
            language = new Microting.eForm.Infrastructure.Data.Entities.Language
            {
                LanguageCode = "da",
                Name = "Danish"
            };
            await language.Create(sdkDbContext);
        }
        site.LanguageId = language.Id;
        await site.Update(sdkDbContext);

        var assignedSite = new AssignedSiteEntity
        {
            SiteId = 500,
            Resigned = false,
            AutoBreakCalculationActive = true,
            ThirdShiftActive = true,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await assignedSite.Create(TimePlanningPnDbContext);

        // Act
        var result = await _settingsService.GetAvailableSitesByCurrentUser();

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.Model, Is.Not.Null);
        Assert.That(result.Model.Count, Is.EqualTo(1));

        var s = result.Model[0];
        Assert.That(s.SiteId, Is.EqualTo(500));
        Assert.That(s.SiteName, Is.EqualTo("Test Worker"));
        Assert.That(s.FirstName, Is.EqualTo("Test"));
        Assert.That(s.LastName, Is.EqualTo("Worker"));
        Assert.That(s.Email, Is.EqualTo("test@test.com"));
        Assert.That(s.PinCode, Is.EqualTo("1234"));
        Assert.That(s.CustomerNo, Is.EqualTo(42));
        Assert.That(s.AutoBreakCalculationActive, Is.True);
        Assert.That(s.ThirdShiftActive, Is.True);
        Assert.That(s.HoursStarted, Is.False);
        Assert.That(s.PauseStarted, Is.False);
    }

    // --- GetAllRegistrationSitesByCurrentUser tests ---
    // Mobile gRPC entry point: must return the complete unfiltered coworker
    // list. The web admin JSON path keeps the manager-tag filter; this method
    // ignores it. Resigned/removed/workflow-state guards still apply.

    [Test]
    public async Task GetAllRegistrationSitesByCurrentUser_ReturnsAllSites_IgnoringManagerTagFilter()
    {
        // Arrange — seed 4 candidate sites with different tag associations.
        // Manager-tag filtering is *out of scope* for this entry point, so all
        // four must come back regardless of which (if any) tag they carry.
        var core = await _coreService.GetCore();
        var sdkDbContext = core.DbContextHelper.GetDbContext();

        var language = await sdkDbContext.Languages.FirstOrDefaultAsync();
        if (language == null)
        {
            language = new Microting.eForm.Infrastructure.Data.Entities.Language
            {
                LanguageCode = "en",
                Name = "English"
            };
            await language.Create(sdkDbContext);
        }

        for (var i = 0; i < 4; i++)
        {
            var siteUid = 1000 + i;
            var workerUid = 2000 + i;
            var siteWorkerUid = 3000 + i;
            var unitUid = 4000 + i;

            var site = new Microting.eForm.Infrastructure.Data.Entities.Site
            {
                Name = $"Coworker {i}",
                MicrotingUid = siteUid,
                LanguageId = language.Id
            };
            await site.Create(sdkDbContext);

            var worker = new Microting.eForm.Infrastructure.Data.Entities.Worker
            {
                FirstName = $"First{i}",
                LastName = $"Last{i}",
                Email = $"coworker{i}@test.com",
                MicrotingUid = workerUid
            };
            await worker.Create(sdkDbContext);

            var siteWorker = new Microting.eForm.Infrastructure.Data.Entities.SiteWorker
            {
                SiteId = site.Id,
                WorkerId = worker.Id,
                MicrotingUid = siteWorkerUid
            };
            await siteWorker.Create(sdkDbContext);

            var unit = new Microting.eForm.Infrastructure.Data.Entities.Unit
            {
                SiteId = site.Id,
                MicrotingUid = unitUid,
                CustomerNo = 1,
                OtpCode = 1
            };
            await unit.Create(sdkDbContext);

            var assigned = new AssignedSiteEntity
            {
                SiteId = siteUid,
                Resigned = false,
                CreatedByUserId = 1,
                UpdatedByUserId = 1
            };
            await assigned.Create(TimePlanningPnDbContext);
        }

        // Act
        var result = await _settingsService.GetAllRegistrationSitesByCurrentUser();

        // Assert — all 4 sites returned, no manager-tag culling.
        Assert.That(result.Success, Is.True);
        Assert.That(result.Model, Is.Not.Null);
        Assert.That(result.Model.Count, Is.EqualTo(4));
        var siteIds = result.Model.Select(x => x.SiteId).OrderBy(x => x).ToList();
        Assert.That(siteIds, Is.EqualTo(new[] { 1000, 1001, 1002, 1003 }));
    }

    [Test]
    public async Task GetAllRegistrationSitesByCurrentUser_StillExcludesResignedAndRemoved()
    {
        // Arrange — 2 active + 1 resigned + 1 workflow-state-removed.
        // The unfiltered mobile path must still respect those guards.
        var core = await _coreService.GetCore();
        var sdkDbContext = core.DbContextHelper.GetDbContext();

        var language = await sdkDbContext.Languages.FirstOrDefaultAsync();
        if (language == null)
        {
            language = new Microting.eForm.Infrastructure.Data.Entities.Language
            {
                LanguageCode = "en",
                Name = "English"
            };
            await language.Create(sdkDbContext);
        }

        async Task SeedSdkSiteAsync(int siteUid, int workerUid, int siteWorkerUid, int unitUid, string label)
        {
            var site = new Microting.eForm.Infrastructure.Data.Entities.Site
            {
                Name = label,
                MicrotingUid = siteUid,
                LanguageId = language.Id
            };
            await site.Create(sdkDbContext);

            var worker = new Microting.eForm.Infrastructure.Data.Entities.Worker
            {
                FirstName = label,
                LastName = "Worker",
                Email = $"{label.ToLower().Replace(' ', '_')}@test.com",
                MicrotingUid = workerUid
            };
            await worker.Create(sdkDbContext);

            var siteWorker = new Microting.eForm.Infrastructure.Data.Entities.SiteWorker
            {
                SiteId = site.Id,
                WorkerId = worker.Id,
                MicrotingUid = siteWorkerUid
            };
            await siteWorker.Create(sdkDbContext);

            var unit = new Microting.eForm.Infrastructure.Data.Entities.Unit
            {
                SiteId = site.Id,
                MicrotingUid = unitUid,
                CustomerNo = 1,
                OtpCode = 1
            };
            await unit.Create(sdkDbContext);
        }

        await SeedSdkSiteAsync(5000, 6000, 7000, 8000, "Active1");
        await SeedSdkSiteAsync(5001, 6001, 7001, 8001, "Active2");
        await SeedSdkSiteAsync(5002, 6002, 7002, 8002, "Resigned1");
        await SeedSdkSiteAsync(5003, 6003, 7003, 8003, "Removed1");

        var active1 = new AssignedSiteEntity
        {
            SiteId = 5000,
            Resigned = false,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await active1.Create(TimePlanningPnDbContext);

        var active2 = new AssignedSiteEntity
        {
            SiteId = 5001,
            Resigned = false,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await active2.Create(TimePlanningPnDbContext);

        var resigned = new AssignedSiteEntity
        {
            SiteId = 5002,
            Resigned = true,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await resigned.Create(TimePlanningPnDbContext);

        var removed = new AssignedSiteEntity
        {
            SiteId = 5003,
            Resigned = false,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await removed.Create(TimePlanningPnDbContext);
        // Soft-delete via Delete() so WorkflowState becomes Removed.
        await removed.Delete(TimePlanningPnDbContext);

        // Act
        var result = await _settingsService.GetAllRegistrationSitesByCurrentUser();

        // Assert — only the two active sites come back.
        Assert.That(result.Success, Is.True);
        Assert.That(result.Model, Is.Not.Null);
        Assert.That(result.Model.Count, Is.EqualTo(2));
        var siteIds = result.Model.Select(x => x.SiteId).OrderBy(x => x).ToList();
        Assert.That(siteIds, Is.EqualTo(new[] { 5000, 5001 }));
    }

    [Test]
    public async Task GetAllRegistrationSitesByCurrentUser_NonManager_StillReceivesAllPeers()
    {
        // The manager-only restriction lives in GetAvailableSitesByCurrentUser
        // (web admin JSON path). For the mobile gRPC entry point there is no
        // such gate: even a non-manager caller (no AssignedSite.IsManager flag
        // set on any row) must receive every peer's site.
        var core = await _coreService.GetCore();
        var sdkDbContext = core.DbContextHelper.GetDbContext();

        var language = await sdkDbContext.Languages.FirstOrDefaultAsync();
        if (language == null)
        {
            language = new Microting.eForm.Infrastructure.Data.Entities.Language
            {
                LanguageCode = "en",
                Name = "English"
            };
            await language.Create(sdkDbContext);
        }

        for (var i = 0; i < 3; i++)
        {
            var siteUid = 9000 + i;
            var workerUid = 9100 + i;
            var siteWorkerUid = 9200 + i;
            var unitUid = 9300 + i;

            var site = new Microting.eForm.Infrastructure.Data.Entities.Site
            {
                Name = $"Peer {i}",
                MicrotingUid = siteUid,
                LanguageId = language.Id
            };
            await site.Create(sdkDbContext);

            var worker = new Microting.eForm.Infrastructure.Data.Entities.Worker
            {
                FirstName = $"Peer{i}",
                LastName = "Worker",
                Email = $"peer{i}@test.com",
                MicrotingUid = workerUid
            };
            await worker.Create(sdkDbContext);

            var siteWorker = new Microting.eForm.Infrastructure.Data.Entities.SiteWorker
            {
                SiteId = site.Id,
                WorkerId = worker.Id,
                MicrotingUid = siteWorkerUid
            };
            await siteWorker.Create(sdkDbContext);

            var unit = new Microting.eForm.Infrastructure.Data.Entities.Unit
            {
                SiteId = site.Id,
                MicrotingUid = unitUid,
                CustomerNo = 1,
                OtpCode = 1
            };
            await unit.Create(sdkDbContext);

            // No IsManager flag — every assigned site is a regular non-manager row.
            var assigned = new AssignedSiteEntity
            {
                SiteId = siteUid,
                Resigned = false,
                IsManager = false,
                CreatedByUserId = 1,
                UpdatedByUserId = 1
            };
            await assigned.Create(TimePlanningPnDbContext);
        }

        // Act
        var result = await _settingsService.GetAllRegistrationSitesByCurrentUser();

        // Assert — non-manager caller still receives every peer's site.
        Assert.That(result.Success, Is.True);
        Assert.That(result.Model, Is.Not.Null);
        Assert.That(result.Model.Count, Is.EqualTo(3));
        var siteIds = result.Model.Select(x => x.SiteId).OrderBy(x => x).ToList();
        Assert.That(siteIds, Is.EqualTo(new[] { 9000, 9001, 9002 }));
    }

    // --- "no managers anywhere" fallback for GetAvailableSitesByCurrentUser ---
    // Mirror of the TimePlanningPlanningService.Index() fallback that PR #1490
    // introduced. Both manager-tag-filter tests below need a real BaseDbContext
    // with seeded ApplicationUser + UserRoles + SecurityGroups so the
    // (!isAdmin) branch actually runs against a known caller. The current test
    // fixture passes null for baseDbContext, which skips the filter block
    // entirely and makes the manager-vs-non-manager branches unobservable
    // here. Same fixture gap that ContentHandoverService tests carve out via
    // [Ignore]. Behavior is exercised end-to-end by the
    // eform-backendconfiguration-plugin playwright test
    // (time-registration-dashboard-visibility.spec.ts) for the
    // manager-exists path.

    [Test]
    [Ignore("Follow-up: wire real BaseDbContext seeding (ApplicationUser + UserRoles + SecurityGroups) so the (!isAdmin) branch in GetAvailableSitesByCurrentUser can be exercised. The fix itself (anyManagerExists predicate) mirrors TimePlanningPlanningService.Index() (PR #1490) and is observed end-to-end by the planning page when no AssignedSite has IsManager=true.")]
    public async Task GetAvailableSitesByCurrentUser_NoManagersAnywhere_ReturnsAllSites()
    {
        // Arrange — three AssignedSites, all IsManager=false. The caller is
        // one of the non-managers. With no manager configured anywhere in
        // the system, the manager-tag filter should be bypassed and the full
        // non-resigned site list should come back.
        var core = await _coreService.GetCore();
        var sdkDbContext = core.DbContextHelper.GetDbContext();

        var language = await sdkDbContext.Languages.FirstOrDefaultAsync();
        if (language == null)
        {
            language = new Microting.eForm.Infrastructure.Data.Entities.Language
            {
                LanguageCode = "en",
                Name = "English"
            };
            await language.Create(sdkDbContext);
        }

        for (var i = 0; i < 3; i++)
        {
            var siteUid = 11000 + i;
            var workerUid = 11100 + i;
            var siteWorkerUid = 11200 + i;
            var unitUid = 11300 + i;

            var site = new Microting.eForm.Infrastructure.Data.Entities.Site
            {
                Name = $"NoMgr {i}",
                MicrotingUid = siteUid,
                LanguageId = language.Id
            };
            await site.Create(sdkDbContext);

            var worker = new Microting.eForm.Infrastructure.Data.Entities.Worker
            {
                FirstName = $"NoMgr{i}",
                LastName = "Worker",
                Email = $"nomgr{i}@test.com",
                MicrotingUid = workerUid
            };
            await worker.Create(sdkDbContext);

            var siteWorker = new Microting.eForm.Infrastructure.Data.Entities.SiteWorker
            {
                SiteId = site.Id,
                WorkerId = worker.Id,
                MicrotingUid = siteWorkerUid
            };
            await siteWorker.Create(sdkDbContext);

            var unit = new Microting.eForm.Infrastructure.Data.Entities.Unit
            {
                SiteId = site.Id,
                MicrotingUid = unitUid,
                CustomerNo = 1,
                OtpCode = 1
            };
            await unit.Create(sdkDbContext);

            var assigned = new AssignedSiteEntity
            {
                SiteId = siteUid,
                Resigned = false,
                IsManager = false,
                CreatedByUserId = 1,
                UpdatedByUserId = 1
            };
            await assigned.Create(TimePlanningPnDbContext);
        }

        // Act — call as one of the non-managers (e.g. nomgr0@test.com).
        var result = await _settingsService.GetAvailableSitesByCurrentUser();

        // Assert — all three sites come back, not just the caller's own.
        Assert.That(result.Success, Is.True);
        Assert.That(result.Model, Is.Not.Null);
        Assert.That(result.Model.Count, Is.EqualTo(3));
        var siteIds = result.Model.Select(x => x.SiteId).OrderBy(x => x).ToList();
        Assert.That(siteIds, Is.EqualTo(new[] { 11000, 11001, 11002 }));
    }

    [Test]
    [Ignore("Follow-up: wire real BaseDbContext seeding (ApplicationUser + UserRoles + SecurityGroups) so the (!isAdmin) branch in GetAvailableSitesByCurrentUser can be exercised. Manager-exists path is covered end-to-end by eform-backendconfiguration-plugin's time-registration-dashboard-visibility.spec.ts.")]
    public async Task GetAvailableSitesByCurrentUser_ManagerExists_NonManagerSeesOnlySelf()
    {
        // Arrange — three AssignedSites, one of them IsManager=true. The
        // caller is a non-manager and should be filtered down to only its
        // own site (existing pre-fix behavior, must remain unchanged).
        var core = await _coreService.GetCore();
        var sdkDbContext = core.DbContextHelper.GetDbContext();

        var language = await sdkDbContext.Languages.FirstOrDefaultAsync();
        if (language == null)
        {
            language = new Microting.eForm.Infrastructure.Data.Entities.Language
            {
                LanguageCode = "en",
                Name = "English"
            };
            await language.Create(sdkDbContext);
        }

        for (var i = 0; i < 3; i++)
        {
            var siteUid = 12000 + i;
            var workerUid = 12100 + i;
            var siteWorkerUid = 12200 + i;
            var unitUid = 12300 + i;

            var site = new Microting.eForm.Infrastructure.Data.Entities.Site
            {
                Name = $"WithMgr {i}",
                MicrotingUid = siteUid,
                LanguageId = language.Id
            };
            await site.Create(sdkDbContext);

            var worker = new Microting.eForm.Infrastructure.Data.Entities.Worker
            {
                FirstName = $"WithMgr{i}",
                LastName = "Worker",
                Email = $"withmgr{i}@test.com",
                MicrotingUid = workerUid
            };
            await worker.Create(sdkDbContext);

            var siteWorker = new Microting.eForm.Infrastructure.Data.Entities.SiteWorker
            {
                SiteId = site.Id,
                WorkerId = worker.Id,
                MicrotingUid = siteWorkerUid
            };
            await siteWorker.Create(sdkDbContext);

            var unit = new Microting.eForm.Infrastructure.Data.Entities.Unit
            {
                SiteId = site.Id,
                MicrotingUid = unitUid,
                CustomerNo = 1,
                OtpCode = 1
            };
            await unit.Create(sdkDbContext);

            var assigned = new AssignedSiteEntity
            {
                SiteId = siteUid,
                Resigned = false,
                IsManager = i == 0, // first row is the manager
                CreatedByUserId = 1,
                UpdatedByUserId = 1
            };
            await assigned.Create(TimePlanningPnDbContext);
        }

        // Act — call as a non-manager (e.g. withmgr1@test.com).
        var result = await _settingsService.GetAvailableSitesByCurrentUser();

        // Assert — non-manager caller is filtered down to its own site.
        Assert.That(result.Success, Is.True);
        Assert.That(result.Model, Is.Not.Null);
        Assert.That(result.Model[0].SiteId, Is.EqualTo(12001));
        Assert.That(result.Model.Count, Is.EqualTo(1));
    }

    // --- Manager-in-own-tag duplicate regression ---
    // Reproducer for the bug where a manager who is the only manager and is
    // listed inside their own managed tag (AssignedSiteManagingTags(TagId=10)
    // + SiteTags(SiteId=ownSite.MicrotingUid, TagId=10)) appeared TWICE on
    // /plugins/time-planning-pn/planning. Root cause was
    // TimePlanningPlanningService.Index() calling assignedSites.Add(assignedSite)
    // unconditionally after the manager-tag filter — see line ~156 — which
    // produced two rows when the tag-filter already kept the manager's own
    // site. The same shape of bug could surface in
    // GetAvailableSitesByCurrentUser if the dedup at lines 429-432 ever
    // missed a path, so we add a defensive GroupBy(Id) sweep at the end of
    // the method and assert here that the dropdown stays unique.
    //
    // These tests follow the same [Ignore] carve-out as the manager-tag
    // tests above: the current fixture passes null for baseDbContext, which
    // skips the (!isAdmin) branch entirely. The defensive dedup runs even
    // under null baseDbContext but can't be observed without seeded
    // duplicates produced by that branch. Tests are written for future
    // fixture work that wires real BaseDbContext seeding.

    [Test]
    [Ignore("Follow-up: wire real BaseDbContext seeding (ApplicationUser + UserRoles + SecurityGroups) so the (!isAdmin) branch in GetAvailableSitesByCurrentUser can be exercised. Reproduces the planning-page double-row bug for the dropdown path; the underlying duplicate originates in TimePlanningPlanningService.Index() (now also fixed). End-to-end coverage is via the playwright dashboard test.")]
    public async Task GetAvailableSitesByCurrentUser_ManagerOwnSiteInOwnTag_ReturnsManagerOnce()
    {
        // Arrange — single manager whose own site is tagged with the same
        // tag the manager manages. Pre-fix this caused two AssignedSite
        // rows to reach BuildSitesFromAssignedSitesAsync and emitted the
        // manager twice in the response.
        var core = await _coreService.GetCore();
        var sdkDbContext = core.DbContextHelper.GetDbContext();

        var language = await sdkDbContext.Languages.FirstOrDefaultAsync();
        if (language == null)
        {
            language = new Microting.eForm.Infrastructure.Data.Entities.Language
            {
                LanguageCode = "en",
                Name = "English"
            };
            await language.Create(sdkDbContext);
        }

        // Manager's site
        var site = new Microting.eForm.Infrastructure.Data.Entities.Site
        {
            Name = "Solo Manager",
            MicrotingUid = 13000,
            LanguageId = language.Id
        };
        await site.Create(sdkDbContext);

        var worker = new Microting.eForm.Infrastructure.Data.Entities.Worker
        {
            FirstName = "Solo",
            LastName = "Manager",
            Email = "solo@test.com",
            MicrotingUid = 13100
        };
        await worker.Create(sdkDbContext);

        var siteWorker = new Microting.eForm.Infrastructure.Data.Entities.SiteWorker
        {
            SiteId = site.Id,
            WorkerId = worker.Id,
            MicrotingUid = 13200
        };
        await siteWorker.Create(sdkDbContext);

        var unit = new Microting.eForm.Infrastructure.Data.Entities.Unit
        {
            SiteId = site.Id,
            MicrotingUid = 13300,
            CustomerNo = 1,
            OtpCode = 1
        };
        await unit.Create(sdkDbContext);

        var assigned = new AssignedSiteEntity
        {
            SiteId = 13000,
            Resigned = false,
            IsManager = true,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await assigned.Create(TimePlanningPnDbContext);

        // Manager manages tag 10
        var managingTag = new AssignedSiteManagingTag
        {
            AssignedSiteId = assigned.Id,
            TagId = 10,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await managingTag.Create(TimePlanningPnDbContext);

        // The manager's OWN site also carries tag 10 — this is the trigger.
        var siteTag = new Microting.eForm.Infrastructure.Data.Entities.SiteTag
        {
            SiteId = site.Id,
            TagId = 10
        };
        await siteTag.Create(sdkDbContext);

        // Act — call as the solo manager.
        var result = await _settingsService.GetAvailableSitesByCurrentUser();

        // Assert — manager appears exactly once, not twice.
        Assert.That(result.Success, Is.True);
        Assert.That(result.Model, Is.Not.Null);
        Assert.That(result.Model.Count, Is.EqualTo(1));
        Assert.That(result.Model.Single().SiteId, Is.EqualTo(13000));
    }

    [Test]
    [Ignore("Follow-up: wire real BaseDbContext seeding (ApplicationUser + UserRoles + SecurityGroups) so the (!isAdmin) branch in GetAvailableSitesByCurrentUser can be exercised. Same fixture gap as the other manager-tag tests in this file.")]
    public async Task GetAvailableSitesByCurrentUser_ManagerWithMultipleTagsOnOwnSite_ReturnsOnce()
    {
        // Arrange — manager manages tags 10 and 20; the manager's own site
        // carries BOTH tags. Without dedup the SiteTags query would return
        // the same SiteId twice (one row per tag) — Distinct() in the EF
        // query already collapses that to one — and the manager's own
        // AssignedSite would still be added an extra time by the
        // unconditional Add() shape (now fixed).
        var core = await _coreService.GetCore();
        var sdkDbContext = core.DbContextHelper.GetDbContext();

        var language = await sdkDbContext.Languages.FirstOrDefaultAsync();
        if (language == null)
        {
            language = new Microting.eForm.Infrastructure.Data.Entities.Language
            {
                LanguageCode = "en",
                Name = "English"
            };
            await language.Create(sdkDbContext);
        }

        var site = new Microting.eForm.Infrastructure.Data.Entities.Site
        {
            Name = "Multi-Tag Manager",
            MicrotingUid = 14000,
            LanguageId = language.Id
        };
        await site.Create(sdkDbContext);

        var worker = new Microting.eForm.Infrastructure.Data.Entities.Worker
        {
            FirstName = "Multi",
            LastName = "Manager",
            Email = "multi@test.com",
            MicrotingUid = 14100
        };
        await worker.Create(sdkDbContext);

        var siteWorker = new Microting.eForm.Infrastructure.Data.Entities.SiteWorker
        {
            SiteId = site.Id,
            WorkerId = worker.Id,
            MicrotingUid = 14200
        };
        await siteWorker.Create(sdkDbContext);

        var unit = new Microting.eForm.Infrastructure.Data.Entities.Unit
        {
            SiteId = site.Id,
            MicrotingUid = 14300,
            CustomerNo = 1,
            OtpCode = 1
        };
        await unit.Create(sdkDbContext);

        var assigned = new AssignedSiteEntity
        {
            SiteId = 14000,
            Resigned = false,
            IsManager = true,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await assigned.Create(TimePlanningPnDbContext);

        foreach (var tagId in new[] { 10, 20 })
        {
            var managingTag = new AssignedSiteManagingTag
            {
                AssignedSiteId = assigned.Id,
                TagId = tagId,
                CreatedByUserId = 1,
                UpdatedByUserId = 1
            };
            await managingTag.Create(TimePlanningPnDbContext);

            var siteTag = new Microting.eForm.Infrastructure.Data.Entities.SiteTag
            {
                SiteId = site.Id,
                TagId = tagId
            };
            await siteTag.Create(sdkDbContext);
        }

        // Act
        var result = await _settingsService.GetAvailableSitesByCurrentUser();

        // Assert — still exactly one entry, even with multiple matching tags.
        Assert.That(result.Success, Is.True);
        Assert.That(result.Model, Is.Not.Null);
        Assert.That(result.Model.Count, Is.EqualTo(1));
        Assert.That(result.Model.Single().SiteId, Is.EqualTo(14000));
    }
}
