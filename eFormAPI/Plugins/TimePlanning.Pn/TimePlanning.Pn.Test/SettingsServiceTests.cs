using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microting.eForm.Infrastructure.Constants;
using Microting.eFormApi.BasePn.Abstractions;
using Microting.eFormApi.BasePn.Infrastructure.Helpers.PluginDbOptions;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using NSubstitute;
using NUnit.Framework;
using Microting.EformAngularFrontendBase.Infrastructure.Const;
using Microting.EformAngularFrontendBase.Infrastructure.Data;
using Microting.EformAngularFrontendBase.Infrastructure.Data.Entities.Permissions;
using Microting.TimePlanningBase.Infrastructure.Const;
using TimePlanning.Pn.Controllers;
using Microting.eFormApi.BasePn.Infrastructure.Database.Entities;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;
using TimePlanning.Pn.Infrastructure.Models.Settings;
using TimePlanning.Pn.Services.TimePlanningLocalizationService;
using TimePlanning.Pn.Services.TimePlanningSettingService;
using AssignedSiteModel = TimePlanning.Pn.Infrastructure.Models.Settings.AssignedSite;
using AssignedSiteEntity = Microting.TimePlanningBase.Infrastructure.Data.Entities.AssignedSite;

namespace TimePlanning.Pn.Test;

[TestFixture]
public class SettingsServiceTests : TestBaseSetup
{
    private ISettingService _settingsService;
    private IUserService _userService;
    private ITimePlanningLocalizationService _localizationService;
    private IEFormCoreService _coreService;
    private IPluginDbOptions<TimePlanningBaseSettings> _options;
    private TimePlanning.Pn.Services.PushNotificationService.IPushNotificationService _pushNotificationService;

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

        _pushNotificationService =
            Substitute.For<TimePlanning.Pn.Services.PushNotificationService.IPushNotificationService>();

        // UpdateAssignedSite checks the caller, so the fixture signs in as an
        // admin; the access tests below repoint the caller.
        await SeedAdminCallerAsync(_userService);

        _settingsService = new TimeSettingService(
            _options,
            TimePlanningPnDbContext,
            Substitute.For<ILogger<TimeSettingService>>(),
            _userService,
            _localizationService,
            SeededBaseDbContext,
            _coreService,
            _pushNotificationService);
    }

    [Test]
    public async Task GetAssignedSite_ReturnsAssignedSite_WithGpsAndSnapshotFlags()
    {
        // Business-logic change: on the PERSONAL path GPS follows the GLOBAL
        // (per-customer) setting, never the per-site AssignedSite column, and Snapshot is
        // ALWAYS off (snapshot is kiosk-only; never taken in personal registration). The
        // per-site column below is set to the OPPOSITE of the global to prove the served
        // GPS value follows the global. Global defaults from SetUp are GpsEnabled="0" and
        // SnapshotEnabled="0", so GpsEnabled is served false (global) and SnapshotEnabled
        // is served false (always-off in personal mode) even though the per-site column
        // has GpsEnabled=true.
        // Arrange
        var assignedSite = new AssignedSiteEntity
        {
            SiteId = 1,
            GpsEnabled = true,
            SnapshotEnabled = false,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await assignedSite.Create(TimePlanningPnDbContext);

        var core = await _coreService.GetCore();
        var sdkDbContext = core.DbContextHelper.GetDbContext();
        var site = new Microting.eForm.Infrastructure.Data.Entities.Site
        {
            Name = "Test Site",
            MicrotingUid = 1
        };
        await site.Create(sdkDbContext);

        // Act
        var result = await _settingsService.GetAssignedSite(1);

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.Model, Is.Not.Null);
        Assert.That(result.Model.SiteId, Is.EqualTo(1));
        // GPS served from GLOBAL ("0" from SetUp), not from the per-site column.
        Assert.That(result.Model.GpsEnabled, Is.False);
        // Snapshot is always off on the personal path (kiosk-only).
        Assert.That(result.Model.SnapshotEnabled, Is.False);
    }

    [Test]
    public async Task GetAssignedSite_PersonalServesGlobalGps_ButSnapshotAlwaysOff()
    {
        // Spec change: on the PERSONAL path (GetAssignedSite) GPS follows the GLOBAL
        // (per-customer) setting, never the per-site AssignedSite column, while Snapshot is
        // ALWAYS off because snapshot is kiosk-only and is never taken in personal
        // registration. Here global snapshot is "1" but the served value must still be
        // false. The per-site column is the OPPOSITE of the global to prove GPS follows
        // the global:
        //   global   -> GpsEnabled=true (served true),  SnapshotEnabled=true (served FALSE: always-off)
        //   per-site -> GpsEnabled=false, SnapshotEnabled=false
        // Arrange
        _options.Value.Returns(new TimePlanningBaseSettings
        {
            AutoBreakCalculationActive = "0",
            DayOfPayment = 20,
            GpsEnabled = "1",
            SnapshotEnabled = "1"
        });

        var assignedSite = new AssignedSiteEntity
        {
            SiteId = 7,
            GpsEnabled = false,
            SnapshotEnabled = false,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await assignedSite.Create(TimePlanningPnDbContext);

        var core = await _coreService.GetCore();
        var sdkDbContext = core.DbContextHelper.GetDbContext();
        var site = new Microting.eForm.Infrastructure.Data.Entities.Site
        {
            Name = "Test Site Global",
            MicrotingUid = 7
        };
        await site.Create(sdkDbContext);

        // Act
        var result = await _settingsService.GetAssignedSite(7);

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.Model, Is.Not.Null);
        Assert.That(result.Model.SiteId, Is.EqualTo(7));
        // GPS served from GLOBAL ("1"), opposite of the per-site column (false).
        Assert.That(result.Model.GpsEnabled, Is.True);
        // Snapshot is always off on the personal path, even though global is "1".
        Assert.That(result.Model.SnapshotEnabled, Is.False);
    }

    [Test]
    public async Task UpdateAssignedSite_UpdatesGpsEnabled_Successfully()
    {
        // Arrange
        var assignedSite = new AssignedSiteEntity
        {
            SiteId = 2,
            GpsEnabled = false,
            SnapshotEnabled = false,
            UseGoogleSheetAsDefault = true,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await assignedSite.Create(TimePlanningPnDbContext);

        var updateModel = new AssignedSiteModel
        {
            Id = assignedSite.Id,
            SiteId = 2,
            GpsEnabled = true,
            SnapshotEnabled = false,
            UseGoogleSheetAsDefault = true
        };

        // Act
        var result = await _settingsService.UpdateAssignedSite(updateModel);

        // Assert
        Assert.That(result.Success, Is.True);

        var updatedSite = await TimePlanningPnDbContext.AssignedSites
            .FirstOrDefaultAsync(x => x.Id == assignedSite.Id);
        Assert.That(updatedSite, Is.Not.Null);
        Assert.That(updatedSite.GpsEnabled, Is.False);
        Assert.That(updatedSite.SnapshotEnabled, Is.False);
    }

    // A managing tag that was removed must be addable again: the unique index on
    // (AssignedSiteId, TagId) also covers soft-deleted rows, so inserting a second
    // row for the tag failed with a duplicate key and the settings save returned 500.
    [Test]
    public async Task UpdateAssignedSite_ManagingTags_RemovedTagCanBeAddedAgain()
    {
        var assignedSite = new AssignedSiteEntity
        {
            SiteId = 61,
            IsManager = true,
            UseGoogleSheetAsDefault = true,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await assignedSite.Create(TimePlanningPnDbContext);

        // Seeded by another user (2), so the saves below must stamp the acting user (1).
        const int otherUserId = 2;
        var removedTag = new AssignedSiteManagingTag
        {
            AssignedSiteId = assignedSite.Id, TagId = 3, CreatedByUserId = otherUserId, UpdatedByUserId = otherUserId
        };
        await removedTag.Create(TimePlanningPnDbContext);
        await removedTag.Delete(TimePlanningPnDbContext);
        await new AssignedSiteManagingTag
        {
            AssignedSiteId = assignedSite.Id, TagId = 5, CreatedByUserId = otherUserId, UpdatedByUserId = otherUserId
        }.Create(TimePlanningPnDbContext);

        Task<OperationResult> Save(params int[] tagIds) =>
            _settingsService.UpdateAssignedSite(new AssignedSiteModel
            {
                Id = assignedSite.Id,
                SiteId = 61,
                IsManager = true,
                UseGoogleSheetAsDefault = true,
                ManagingTagIds = tagIds.ToList()
            });

        Task<List<AssignedSiteManagingTag>> Rows() =>
            TimePlanningPnDbContext.AssignedSiteManagingTags.AsNoTracking()
                .Where(x => x.AssignedSiteId == assignedSite.Id)
                .ToListAsync();

        static int[] ActiveTagIds(List<AssignedSiteManagingTag> rows) =>
            rows.Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .Select(x => x.TagId).OrderBy(x => x).ToArray();

        // Give back the removed tag 3, keep 5, add the new tag 10 (twice in the request).
        Assert.That((await Save(3, 5, 10, 10)).Success, Is.True);
        var rows = await Rows();
        Assert.That(ActiveTagIds(rows), Is.EqualTo(new[] { 3, 5, 10 }));
        Assert.That(rows.Single(x => x.TagId == 3).Id, Is.EqualTo(removedTag.Id), "tag 3 must reuse its old row");
        Assert.That(rows.Single(x => x.TagId == 3).UpdatedByUserId, Is.EqualTo(1), "reviving must record the acting user");
        Assert.That(rows.Single(x => x.TagId == 5).UpdatedByUserId, Is.EqualTo(otherUserId), "an untouched row keeps its updater");

        // Remove 3 and 10 again (soft delete keeps the rows), then give 3 back once more.
        Assert.That((await Save(5)).Success, Is.True);
        rows = await Rows();
        Assert.That(ActiveTagIds(rows), Is.EqualTo(new[] { 5 }));
        Assert.That(rows.Count, Is.EqualTo(3));

        Assert.That((await Save(3)).Success, Is.True);
        rows = await Rows();
        Assert.That(ActiveTagIds(rows), Is.EqualTo(new[] { 3 }));
        Assert.That(rows.Single(x => x.TagId == 3).Id, Is.EqualTo(removedTag.Id));
        Assert.That(rows.Single(x => x.TagId == 5).UpdatedByUserId, Is.EqualTo(1), "removing must record the acting user");
    }

    // #1740: every site runs on 1-minute intervals and the settings checkbox is
    // gone, so UpdateAssignedSite must ignore whatever a (stale) client sends for
    // UseOneMinuteIntervals — in both directions — and never touch the recorded
    // cut-over date the flex chain uses to keep older days on 5-minute rules.
    [Test]
    public async Task UpdateAssignedSite_IgnoresClientFalse_KeepsOneMinuteAndCutOverDate()
    {
        var cutOver = new DateTime(2026, 10, 3, 0, 0, 0, DateTimeKind.Utc);
        var assignedSite = new AssignedSiteEntity
        {
            SiteId = 51,
            UseOneMinuteIntervals = true,
            UseOneMinuteIntervalsFrom = cutOver,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await assignedSite.Create(TimePlanningPnDbContext);

        var versionsBefore = await TimePlanningPnDbContext.AssignedSiteVersions
            .CountAsync(x => x.AssignedSiteId == assignedSite.Id);

        // OverMidnight carries a real change: PnBase.Update only writes a version
        // row when something changed, and the audit row is what is checked below.
        var result = await _settingsService.UpdateAssignedSite(new AssignedSiteModel
        {
            Id = assignedSite.Id,
            SiteId = 51,
            UseOneMinuteIntervals = false,
            OverMidnight = true
        });

        Assert.That(result.Success, Is.True);
        var stored = await TimePlanningPnDbContext.AssignedSites.AsNoTracking()
            .FirstAsync(x => x.Id == assignedSite.Id);
        Assert.That(stored.UseOneMinuteIntervals, Is.True);
        Assert.That(stored.UseOneMinuteIntervalsFrom, Is.EqualTo(cutOver));

        Assert.That(stored.OverMidnight, Is.True, "the save itself must have gone through");

        // The save writes its audit row, and that row must not record a flip
        // either, or OneMinuteModeTimeline would read one from the trail.
        Assert.That(await TimePlanningPnDbContext.AssignedSiteVersions
                .CountAsync(x => x.AssignedSiteId == assignedSite.Id),
            Is.EqualTo(versionsBefore + 1));
        var lastVersion = await TimePlanningPnDbContext.AssignedSiteVersions.AsNoTracking()
            .Where(x => x.AssignedSiteId == assignedSite.Id)
            .OrderByDescending(x => x.Id)
            .FirstAsync();
        Assert.That(lastVersion.UseOneMinuteIntervals, Is.True);
        Assert.That(lastVersion.UseOneMinuteIntervalsFrom, Is.EqualTo(cutOver));
    }

    [Test]
    public async Task UpdateAssignedSite_IgnoresClientTrue_DoesNotSwitchOrStampDate()
    {
        // A site not yet switched is moved by the estate-wide switch with a
        // chosen cut-over date, never by a settings save stamping "now".
        var assignedSite = new AssignedSiteEntity
        {
            SiteId = 52,
            UseOneMinuteIntervals = false,
            UseOneMinuteIntervalsFrom = null,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await assignedSite.Create(TimePlanningPnDbContext);

        var result = await _settingsService.UpdateAssignedSite(new AssignedSiteModel
        {
            Id = assignedSite.Id,
            SiteId = 52,
            UseOneMinuteIntervals = true
        });

        Assert.That(result.Success, Is.True);
        var stored = await TimePlanningPnDbContext.AssignedSites.AsNoTracking()
            .FirstAsync(x => x.Id == assignedSite.Id);
        Assert.That(stored.UseOneMinuteIntervals, Is.False);
        Assert.That(stored.UseOneMinuteIntervalsFrom, Is.Null);
    }

    [Test]
    public async Task UpdateAssignedSite_StillSucceeds_WhenPushThrows()
    {
        // The settings_changed push is fire-and-forget: a push failure must
        // never fail the settings update.
        _pushNotificationService
            .SendToSiteAsync(
                Arg.Any<int>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<Dictionary<string, string>?>())
            .Returns(_ => Task.FromException(new Exception("boom")));

        var assignedSite = new AssignedSiteEntity
        {
            SiteId = 42,
            UseGoogleSheetAsDefault = true,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await assignedSite.Create(TimePlanningPnDbContext);

        var updateModel = new AssignedSiteModel
        {
            Id = assignedSite.Id,
            SiteId = 42,
            UseGoogleSheetAsDefault = true
        };

        var result = await _settingsService.UpdateAssignedSite(updateModel);

        Assert.That(result.Success, Is.True,
            "a push failure must not fail the settings update");
    }

    [Test]
    public async Task UpdateAssignedSite_UpdatesGlobalGpsEnabled_Successfully()
    {
        // Arrange
        var assignedSite = new AssignedSiteEntity
        {
            SiteId = 2,
            GpsEnabled = false,
            SnapshotEnabled = false,
            UseGoogleSheetAsDefault = true,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await assignedSite.Create(TimePlanningPnDbContext);

        var updateModel = new AssignedSiteModel
        {
            Id = assignedSite.Id,
            SiteId = 2,
            GpsEnabled = true,
            SnapshotEnabled = false,
            UseGoogleSheetAsDefault = true
        };

        var pluginConfigurationValue = await TimePlanningPnDbContext!.PluginConfigurationValues
            .FirstOrDefaultAsync(x => x.Name == "TimePlanningBaseSettings:GpsEnabled" && x.WorkflowState != Constants.WorkflowStates.Removed);

        if (pluginConfigurationValue == null)
        {
            pluginConfigurationValue = new PluginConfigurationValue
            {
                Name = "TimePlanningBaseSettings:GpsEnabled",
                Value = "1",
                WorkflowState = Constants.WorkflowStates.Created,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedByUserId = 1,
                UpdatedByUserId = 1
            };
            TimePlanningPnDbContext.Add(pluginConfigurationValue);
            await TimePlanningPnDbContext.SaveChangesAsync().ConfigureAwait(false);
        }
        else
        {
            pluginConfigurationValue!.Value = "1";
            await TimePlanningPnDbContext.SaveChangesAsync().ConfigureAwait(false);
        }

        // Act
        var result = await _settingsService.UpdateAssignedSite(updateModel);

        // Assert
        Assert.That(result.Success, Is.True);

        var updatedSite = await TimePlanningPnDbContext.AssignedSites
            .FirstOrDefaultAsync(x => x.Id == assignedSite.Id);
        Assert.That(updatedSite, Is.Not.Null);
        Assert.That(updatedSite.GpsEnabled, Is.True);
        Assert.That(updatedSite.SnapshotEnabled, Is.False);
    }

    [Test]
    public async Task UpdateAssignedSite_UpdatesSnapshotEnabled_Successfully()
    {
        // Arrange
        var assignedSite = new AssignedSiteEntity
        {
            SiteId = 3,
            GpsEnabled = false,
            SnapshotEnabled = false,
            UseGoogleSheetAsDefault = true,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await assignedSite.Create(TimePlanningPnDbContext);

        var updateModel = new AssignedSiteModel
        {
            Id = assignedSite.Id,
            SiteId = 3,
            GpsEnabled = false,
            SnapshotEnabled = true,
            UseGoogleSheetAsDefault = true
        };

        // Act
        var result = await _settingsService.UpdateAssignedSite(updateModel);

        // Assert
        Assert.That(result.Success, Is.True);

        var updatedSite = await TimePlanningPnDbContext.AssignedSites
            .FirstOrDefaultAsync(x => x.Id == assignedSite.Id);
        Assert.That(updatedSite, Is.Not.Null);
        Assert.That(updatedSite.GpsEnabled, Is.False);
        Assert.That(updatedSite.SnapshotEnabled, Is.True);
    }

    [Test]
    public async Task UpdateAssignedSite_UpdatesBothGpsAndSnapshotEnabled_Successfully()
    {
        // Arrange
        var assignedSite = new AssignedSiteEntity
        {
            SiteId = 4,
            GpsEnabled = false,
            SnapshotEnabled = false,
            UseGoogleSheetAsDefault = true,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await assignedSite.Create(TimePlanningPnDbContext);

        var updateModel = new AssignedSiteModel
        {
            Id = assignedSite.Id,
            SiteId = 4,
            GpsEnabled = true,
            SnapshotEnabled = true,
            UseGoogleSheetAsDefault = true
        };

        // Act
        var result = await _settingsService.UpdateAssignedSite(updateModel);

        // Assert
        Assert.That(result.Success, Is.True);

        var updatedSite = await TimePlanningPnDbContext.AssignedSites
            .FirstOrDefaultAsync(x => x.Id == assignedSite.Id);
        Assert.That(updatedSite, Is.Not.Null);
        Assert.That(updatedSite.GpsEnabled, Is.False);
        Assert.That(updatedSite.SnapshotEnabled, Is.True);
    }

    [Test]
    public async Task UpdateAssignedSite_CanDisableBothFlags_Successfully()
    {
        // Arrange
        var assignedSite = new AssignedSiteEntity
        {
            SiteId = 5,
            GpsEnabled = true,
            SnapshotEnabled = true,
            UseGoogleSheetAsDefault = true,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await assignedSite.Create(TimePlanningPnDbContext);

        var updateModel = new AssignedSiteModel
        {
            Id = assignedSite.Id,
            SiteId = 5,
            GpsEnabled = false,
            SnapshotEnabled = false,
            UseGoogleSheetAsDefault = true
        };

        // Act
        var result = await _settingsService.UpdateAssignedSite(updateModel);

        // Assert
        Assert.That(result.Success, Is.True);

        var updatedSite = await TimePlanningPnDbContext.AssignedSites
            .FirstOrDefaultAsync(x => x.Id == assignedSite.Id);
        Assert.That(updatedSite, Is.Not.Null);
        Assert.That(updatedSite.GpsEnabled, Is.False);
        Assert.That(updatedSite.SnapshotEnabled, Is.False);
    }

    [Test]
    public async Task UpdateAssignedSite_ReturnsFalse_WhenSiteNotFound()
    {
        // Arrange
        var updateModel = new AssignedSiteModel
        {
            Id = 999,
            SiteId = 999,
            GpsEnabled = true,
            SnapshotEnabled = true,
            UseGoogleSheetAsDefault = true
        };

        // Act
        var result = await _settingsService.UpdateAssignedSite(updateModel);

        // Assert
        Assert.That(result.Success, Is.False);
    }

    [Test]
    public async Task UpdateAssignedSite_PreservesOtherProperties_WhenUpdatingGpsAndSnapshotFlags()
    {
        // Arrange
        var assignedSite = new AssignedSiteEntity
        {
            SiteId = 6,
            GpsEnabled = false,
            SnapshotEnabled = false,
            UseGoogleSheetAsDefault = true,
            UseOneMinuteIntervals = true,
            AllowAcceptOfPlannedHours = true,
            AllowEditOfRegistrations = true,
            StartMonday = 480,
            EndMonday = 960,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await assignedSite.Create(TimePlanningPnDbContext);

        var updateModel = new AssignedSiteModel
        {
            Id = assignedSite.Id,
            SiteId = 6,
            GpsEnabled = true,
            SnapshotEnabled = true,
            UseGoogleSheetAsDefault = true,
            UseOneMinuteIntervals = true,
            AllowAcceptOfPlannedHours = true,
            AllowEditOfRegistrations = true,
            StartMonday = 480,
            EndMonday = 960
        };

        // Act
        var result = await _settingsService.UpdateAssignedSite(updateModel);

        // Assert
        Assert.That(result.Success, Is.True);

        var updatedSite = await TimePlanningPnDbContext.AssignedSites
            .FirstOrDefaultAsync(x => x.Id == assignedSite.Id);
        Assert.That(updatedSite, Is.Not.Null);
        Assert.That(updatedSite.GpsEnabled, Is.False);
        Assert.That(updatedSite.SnapshotEnabled, Is.True);
        Assert.That(updatedSite.UseOneMinuteIntervals, Is.True);
        Assert.That(updatedSite.AllowAcceptOfPlannedHours, Is.True);
        Assert.That(updatedSite.AllowEditOfRegistrations, Is.True);
        Assert.That(updatedSite.StartMonday, Is.EqualTo(480));
        Assert.That(updatedSite.EndMonday, Is.EqualTo(960));
    }

    [Test]
    public async Task UpdateAssignedSite_PersistsOverMidnight()
    {
        // Arrange
        var assignedSite = new AssignedSiteEntity
        {
            SiteId = 42,
            OverMidnight = false,
            UseGoogleSheetAsDefault = true,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await assignedSite.Create(TimePlanningPnDbContext);

        var updateModel = new AssignedSiteModel
        {
            Id = assignedSite.Id,
            SiteId = 42,
            OverMidnight = true,
            UseGoogleSheetAsDefault = true
        };

        // Act — enable OverMidnight
        var result = await _settingsService.UpdateAssignedSite(updateModel);

        // Assert
        Assert.That(result.Success, Is.True);

        var updatedSite = await TimePlanningPnDbContext.AssignedSites
            .FirstOrDefaultAsync(x => x.Id == assignedSite.Id);
        Assert.That(updatedSite, Is.Not.Null);
        Assert.That(updatedSite.OverMidnight, Is.True);

        // Act — disable OverMidnight again
        updateModel.OverMidnight = false;
        result = await _settingsService.UpdateAssignedSite(updateModel);

        // Assert
        Assert.That(result.Success, Is.True);

        updatedSite = await TimePlanningPnDbContext.AssignedSites
            .FirstOrDefaultAsync(x => x.Id == assignedSite.Id);
        Assert.That(updatedSite, Is.Not.Null);
        Assert.That(updatedSite.OverMidnight, Is.False);
    }

    [Test]
    public async Task GetSettings_ReturnsGpsAndSnapshotEnabledSettings()
    {
        // Arrange
        _options.Value.Returns(new TimePlanningBaseSettings
        {
            AutoBreakCalculationActive = "0",
            DayOfPayment = 20,
            GpsEnabled = "1",
            SnapshotEnabled = "1",
            MondayBreakMinutesDivider = "180",
            MondayBreakMinutesPrDivider = "30",
            TuesdayBreakMinutesDivider = "180",
            TuesdayBreakMinutesPrDivider = "30",
            WednesdayBreakMinutesDivider = "180",
            WednesdayBreakMinutesPrDivider = "30",
            ThursdayBreakMinutesDivider = "180",
            ThursdayBreakMinutesPrDivider = "30",
            FridayBreakMinutesDivider = "180",
            FridayBreakMinutesPrDivider = "30",
            SaturdayBreakMinutesDivider = "120",
            SaturdayBreakMinutesPrDivider = "30",
            SundayBreakMinutesDivider = "120",
            SundayBreakMinutesPrDivider = "30",
            MondayBreakMinutesUpperLimit = "60",
            TuesdayBreakMinutesUpperLimit = "60",
            WednesdayBreakMinutesUpperLimit = "60",
            ThursdayBreakMinutesUpperLimit = "60",
            FridayBreakMinutesUpperLimit = "60",
            SaturdayBreakMinutesUpperLimit = "60",
            SundayBreakMinutesUpperLimit = "60",
            ShowCalculationsAsNumber = "1",
            DaysBackInTimeAllowedEditingEnabled = "0",
            DaysBackInTimeAllowedEditing = 2,
            GoogleSheetId = ""
        });

        // Act
        var result = await _settingsService.GetSettings();

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.Model, Is.Not.Null);
        Assert.That(result.Model.GpsEnabled, Is.True);
        Assert.That(result.Model.SnapshotEnabled, Is.True);
    }

    [Test]
    public async Task UpdateSettings_DoesNotFanGpsAndSnapshotOutToAssignedSites()
    {
        // Business-logic change: GPS/Snapshot now live ONLY in the GLOBAL settings and are
        // served from there on every serve path. UpdateSettings no longer fans these values
        // out to the per-site AssignedSite columns (that loop was removed), so the per-site
        // columns below must stay at their original false even though the settings model
        // sets GpsEnabled=true and SnapshotEnabled=true.
        // Arrange
        var assignedSite1 = new AssignedSiteEntity
        {
            SiteId = 10,
            GpsEnabled = false,
            SnapshotEnabled = false,
            UseGoogleSheetAsDefault = true,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await assignedSite1.Create(TimePlanningPnDbContext);

        var assignedSite2 = new AssignedSiteEntity
        {
            SiteId = 11,
            GpsEnabled = false,
            SnapshotEnabled = false,
            UseGoogleSheetAsDefault = true,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await assignedSite2.Create(TimePlanningPnDbContext);

        var settingsModel = new TimePlanningSettingsModel
        {
            GoogleSheetId = "",
            GpsEnabled = true,
            SnapshotEnabled = true,
            MondayBreakMinutesDivider = 180,
            MondayBreakMinutesPrDivider = 30,
            TuesdayBreakMinutesDivider = 180,
            TuesdayBreakMinutesPrDivider = 30,
            WednesdayBreakMinutesDivider = 180,
            WednesdayBreakMinutesPrDivider = 30,
            ThursdayBreakMinutesDivider = 180,
            ThursdayBreakMinutesPrDivider = 30,
            FridayBreakMinutesDivider = 180,
            FridayBreakMinutesPrDivider = 30,
            SaturdayBreakMinutesDivider = 120,
            SaturdayBreakMinutesPrDivider = 30,
            SundayBreakMinutesDivider = 120,
            SundayBreakMinutesPrDivider = 30,
            MondayBreakMinutesUpperLimit = 60,
            TuesdayBreakMinutesUpperLimit = 60,
            WednesdayBreakMinutesUpperLimit = 60,
            ThursdayBreakMinutesUpperLimit = 60,
            FridayBreakMinutesUpperLimit = 60,
            SaturdayBreakMinutesUpperLimit = 60,
            SundayBreakMinutesUpperLimit = 60,
            AutoBreakCalculationActive = false,
            ShowCalculationsAsNumber = true,
            DayOfPayment = 20,
            DaysBackInTimeAllowedEditingEnabled = false,
            DaysBackInTimeAllowedEditing = 2
        };

        // Act
        var result = await _settingsService.UpdateSettings(settingsModel);

        // Assert
        Assert.That(result.Success, Is.True);

        var updatedSite1 = await TimePlanningPnDbContext.AssignedSites
            .FirstOrDefaultAsync(x => x.Id == assignedSite1.Id);
        var updatedSite2 = await TimePlanningPnDbContext.AssignedSites
            .FirstOrDefaultAsync(x => x.Id == assignedSite2.Id);

        // Per-site columns are intentionally left untouched (loop removed); GPS/Snapshot
        // are served from the GLOBAL settings, not from these columns.
        Assert.That(updatedSite1, Is.Not.Null);
        Assert.That(updatedSite1.GpsEnabled, Is.False);
        Assert.That(updatedSite1.SnapshotEnabled, Is.False);

        Assert.That(updatedSite2, Is.Not.Null);
        Assert.That(updatedSite2.GpsEnabled, Is.False);
        Assert.That(updatedSite2.SnapshotEnabled, Is.False);
    }

    #region Manager and Tags Tests

    [Test]
    public async Task GetAssignedSite_ReturnsIsManager_WhenSet()
    {
        // Arrange
        var assignedSite = new AssignedSiteEntity
        {
            SiteId = 20,
            IsManager = true,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await assignedSite.Create(TimePlanningPnDbContext);

        var core = await _coreService.GetCore();
        var sdkDbContext = core.DbContextHelper.GetDbContext();
        var site = new Microting.eForm.Infrastructure.Data.Entities.Site
        {
            Name = "Manager Test Site",
            MicrotingUid = 20
        };
        await site.Create(sdkDbContext);

        // Act
        var result = await _settingsService.GetAssignedSite(20);

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.Model, Is.Not.Null);
        Assert.That(result.Model.IsManager, Is.True);
        Assert.That(result.Model.ManagingTagIds, Is.Not.Null);
        Assert.That(result.Model.ManagingTagIds.Count, Is.EqualTo(0));
    }

    [Test]
    public async Task GetAssignedSite_LoadsManagingTags_Successfully()
    {
        // Arrange
        var assignedSite = new AssignedSiteEntity
        {
            SiteId = 21,
            IsManager = true,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await assignedSite.Create(TimePlanningPnDbContext);

        // Create managing tags
        var tag1 = new Microting.TimePlanningBase.Infrastructure.Data.Entities.AssignedSiteManagingTag
        {
            AssignedSiteId = assignedSite.Id,
            TagId = 1,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await tag1.Create(TimePlanningPnDbContext);

        var tag2 = new Microting.TimePlanningBase.Infrastructure.Data.Entities.AssignedSiteManagingTag
        {
            AssignedSiteId = assignedSite.Id,
            TagId = 2,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await tag2.Create(TimePlanningPnDbContext);

        var core = await _coreService.GetCore();
        var sdkDbContext = core.DbContextHelper.GetDbContext();
        var site = new Microting.eForm.Infrastructure.Data.Entities.Site
        {
            Name = "Manager Test Site with Tags",
            MicrotingUid = 21
        };
        await site.Create(sdkDbContext);

        // Act
        var result = await _settingsService.GetAssignedSite(21);

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.Model, Is.Not.Null);
        Assert.That(result.Model.IsManager, Is.True);
        Assert.That(result.Model.ManagingTagIds, Is.Not.Null);
        Assert.That(result.Model.ManagingTagIds.Count, Is.EqualTo(2));
        Assert.That(result.Model.ManagingTagIds, Does.Contain(1));
        Assert.That(result.Model.ManagingTagIds, Does.Contain(2));
    }

    [Test]
    public async Task UpdateAssignedSite_UpdatesIsManager_Successfully()
    {
        // Arrange
        var assignedSite = new AssignedSiteEntity
        {
            SiteId = 22,
            IsManager = false,
            UseGoogleSheetAsDefault = true,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await assignedSite.Create(TimePlanningPnDbContext);

        var updateModel = new AssignedSiteModel
        {
            Id = assignedSite.Id,
            SiteId = 22,
            IsManager = true,
            ManagingTagIds = new System.Collections.Generic.List<int>(),
            UseGoogleSheetAsDefault = true
        };

        // Act
        var result = await _settingsService.UpdateAssignedSite(updateModel);

        // Assert
        Assert.That(result.Success, Is.True);

        var updatedSite = await TimePlanningPnDbContext.AssignedSites
            .FirstOrDefaultAsync(x => x.Id == assignedSite.Id);
        Assert.That(updatedSite, Is.Not.Null);
        Assert.That(updatedSite.IsManager, Is.True);
    }

    [Test]
    public async Task UpdateAssignedSite_AddsManagingTags_Successfully()
    {
        // Arrange
        var assignedSite = new AssignedSiteEntity
        {
            SiteId = 23,
            IsManager = true,
            UseGoogleSheetAsDefault = true,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await assignedSite.Create(TimePlanningPnDbContext);

        var updateModel = new AssignedSiteModel
        {
            Id = assignedSite.Id,
            SiteId = 23,
            IsManager = true,
            ManagingTagIds = new System.Collections.Generic.List<int> { 10, 20, 30 },
            UseGoogleSheetAsDefault = true
        };

        // Act
        var result = await _settingsService.UpdateAssignedSite(updateModel);

        // Assert
        Assert.That(result.Success, Is.True);

        var managingTags = await TimePlanningPnDbContext.AssignedSiteManagingTags
            .Where(x => x.AssignedSiteId == assignedSite.Id)
            .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
            .ToListAsync();

        Assert.That(managingTags, Is.Not.Null);
        Assert.That(managingTags.Count, Is.EqualTo(3));
        Assert.That(managingTags.Any(x => x.TagId == 10), Is.True);
        Assert.That(managingTags.Any(x => x.TagId == 20), Is.True);
        Assert.That(managingTags.Any(x => x.TagId == 30), Is.True);
    }

    [Test]
    public async Task UpdateAssignedSite_RemovesManagingTags_Successfully()
    {
        // Arrange
        var assignedSite = new AssignedSiteEntity
        {
            SiteId = 24,
            IsManager = true,
            UseGoogleSheetAsDefault = true,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await assignedSite.Create(TimePlanningPnDbContext);

        // Create initial tags
        var tag1 = new Microting.TimePlanningBase.Infrastructure.Data.Entities.AssignedSiteManagingTag
        {
            AssignedSiteId = assignedSite.Id,
            TagId = 10,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await tag1.Create(TimePlanningPnDbContext);

        var tag2 = new Microting.TimePlanningBase.Infrastructure.Data.Entities.AssignedSiteManagingTag
        {
            AssignedSiteId = assignedSite.Id,
            TagId = 20,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await tag2.Create(TimePlanningPnDbContext);

        // Update model with only one tag (removing tag 20)
        var updateModel = new AssignedSiteModel
        {
            Id = assignedSite.Id,
            SiteId = 24,
            IsManager = true,
            ManagingTagIds = new System.Collections.Generic.List<int> { 10 },
            UseGoogleSheetAsDefault = true
        };

        // Act
        var result = await _settingsService.UpdateAssignedSite(updateModel);

        // Assert
        Assert.That(result.Success, Is.True);

        var managingTags = await TimePlanningPnDbContext.AssignedSiteManagingTags
            .Where(x => x.AssignedSiteId == assignedSite.Id)
            .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
            .ToListAsync();

        Assert.That(managingTags, Is.Not.Null);
        Assert.That(managingTags.Count, Is.EqualTo(1));
        Assert.That(managingTags[0].TagId, Is.EqualTo(10));

        // Verify tag2 was soft-deleted
        var allTags = await TimePlanningPnDbContext.AssignedSiteManagingTags
            .Where(x => x.AssignedSiteId == assignedSite.Id)
            .ToListAsync();
        var removedTag = allTags.FirstOrDefault(x => x.TagId == 20);
        Assert.That(removedTag, Is.Not.Null);
        Assert.That(removedTag.WorkflowState, Is.EqualTo(Constants.WorkflowStates.Removed));
    }

    [Test]
    public async Task UpdateAssignedSite_UpdatesBothIsManagerAndTags_Successfully()
    {
        // Arrange
        var assignedSite = new AssignedSiteEntity
        {
            SiteId = 25,
            IsManager = false,
            UseGoogleSheetAsDefault = true,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await assignedSite.Create(TimePlanningPnDbContext);

        var updateModel = new AssignedSiteModel
        {
            Id = assignedSite.Id,
            SiteId = 25,
            IsManager = true,
            ManagingTagIds = new System.Collections.Generic.List<int> { 100, 200 },
            UseGoogleSheetAsDefault = true
        };

        // Act
        var result = await _settingsService.UpdateAssignedSite(updateModel);

        // Assert
        Assert.That(result.Success, Is.True);

        var updatedSite = await TimePlanningPnDbContext.AssignedSites
            .FirstOrDefaultAsync(x => x.Id == assignedSite.Id);
        Assert.That(updatedSite, Is.Not.Null);
        Assert.That(updatedSite.IsManager, Is.True);

        var managingTags = await TimePlanningPnDbContext.AssignedSiteManagingTags
            .Where(x => x.AssignedSiteId == assignedSite.Id)
            .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
            .ToListAsync();

        Assert.That(managingTags, Is.Not.Null);
        Assert.That(managingTags.Count, Is.EqualTo(2));
        Assert.That(managingTags.Any(x => x.TagId == 100), Is.True);
        Assert.That(managingTags.Any(x => x.TagId == 200), Is.True);
    }

    [Test]
    public async Task UpdateAssignedSite_HandlesNullManagingTagIds_Successfully()
    {
        // Arrange
        var assignedSite = new AssignedSiteEntity
        {
            SiteId = 26,
            IsManager = true,
            UseGoogleSheetAsDefault = true,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await assignedSite.Create(TimePlanningPnDbContext);

        // Create initial tags
        var tag1 = new Microting.TimePlanningBase.Infrastructure.Data.Entities.AssignedSiteManagingTag
        {
            AssignedSiteId = assignedSite.Id,
            TagId = 5,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await tag1.Create(TimePlanningPnDbContext);

        var updateModel = new AssignedSiteModel
        {
            Id = assignedSite.Id,
            SiteId = 26,
            IsManager = true,
            ManagingTagIds = null, // Null should be handled gracefully
            UseGoogleSheetAsDefault = true
        };

        // Act
        var result = await _settingsService.UpdateAssignedSite(updateModel);

        // Assert
        Assert.That(result.Success, Is.True);

        // All tags should be removed when null is passed
        var managingTags = await TimePlanningPnDbContext.AssignedSiteManagingTags
            .Where(x => x.AssignedSiteId == assignedSite.Id)
            .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
            .ToListAsync();

        Assert.That(managingTags.Count, Is.EqualTo(0));
    }

    #endregion

    #region Assigned-site update access

    /// <summary>
    /// Adds a user without the admin role to the seeded base database and makes
    /// it the signed-in caller.
    /// </summary>
    private async Task<EformUser> SignInAsNonAdminAsync(string email)
    {
        var user = new EformUser
        {
            UserName = email,
            Email = email,
            FirstName = "Jane",
            LastName = "Doe"
        };
        SeededBaseDbContext!.Users.Add(user);
        await SeededBaseDbContext.SaveChangesAsync();
        _userService.GetCurrentUserAsync().Returns(new EformUser { Id = user.Id });
        return user;
    }

    /// <summary>
    /// Puts <paramref name="user"/> in a security group named
    /// <paramref name="groupName"/> that grants <paramref name="claimNames"/>.
    /// </summary>
    private async Task AddToSecurityGroupAsync(EformUser user, string groupName, params string[] claimNames)
    {
        var db = SeededBaseDbContext!;
        var group = new SecurityGroup { Name = groupName };
        db.SecurityGroups.Add(group);
        await db.SaveChangesAsync();
        db.SecurityGroupUsers.Add(new SecurityGroupUser { SecurityGroupId = group.Id, EformUserId = user.Id });

        if (claimNames.Length > 0)
        {
            // The base database seeds the core permission types and permissions; reuse them when present.
            var permissionType = await db.PermissionTypes.FirstOrDefaultAsync(x => x.Name == "Device users");
            if (permissionType == null)
            {
                permissionType = new PermissionType { Name = "Device users" };
                db.PermissionTypes.Add(permissionType);
                await db.SaveChangesAsync();
            }

            foreach (var claimName in claimNames)
            {
                var permission = await db.Permissions.FirstOrDefaultAsync(x => x.ClaimName == claimName);
                if (permission == null)
                {
                    permission = new Permission
                    {
                        PermissionName = claimName,
                        ClaimName = claimName,
                        PermissionTypeId = permissionType.Id
                    };
                    db.Permissions.Add(permission);
                    await db.SaveChangesAsync();
                }
                db.GroupPermissions.Add(new GroupPermission
                {
                    PermissionId = permission.Id,
                    SecurityGroupId = group.Id
                });
            }
        }

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// A site with every flag the requests below set still off, managing tag 10.
    /// </summary>
    private async Task<AssignedSiteEntity> CreateSiteAsync(int siteId, bool isManager = false)
    {
        var assignedSite = new AssignedSiteEntity
        {
            SiteId = siteId,
            IsManager = isManager,
            UseOnlyPlanHours = false,
            AllowPersonalTimeRegistration = false,
            AllowEditOfRegistrations = false,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await assignedSite.Create(TimePlanningPnDbContext);

        await new AssignedSiteManagingTag
        {
            AssignedSiteId = assignedSite.Id,
            TagId = 10,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        }.Create(TimePlanningPnDbContext);

        return assignedSite;
    }

    private static AssignedSiteModel RequestFor(
        AssignedSiteEntity assignedSite, bool isManager, List<int> managingTagIds)
    {
        return new AssignedSiteModel
        {
            Id = assignedSite.Id,
            SiteId = assignedSite.SiteId,
            IsManager = isManager,
            ManagingTagIds = managingTagIds,
            UseOnlyPlanHours = true,
            AllowPersonalTimeRegistration = true,
            AllowEditOfRegistrations = true
        };
    }

    private Task<AssignedSiteEntity> LoadStoredSiteAsync(int assignedSiteId)
    {
        return TimePlanningPnDbContext!.AssignedSites
            .AsNoTracking()
            .SingleAsync(x => x.Id == assignedSiteId);
    }

    private Task<List<int>> LoadManagingTagIdsAsync(int assignedSiteId)
    {
        return TimePlanningPnDbContext!.AssignedSiteManagingTags
            .AsNoTracking()
            .Where(x => x.AssignedSiteId == assignedSiteId)
            .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
            .Select(x => x.TagId)
            .ToListAsync();
    }

    private async Task AssertRefusedAndUnchangedAsync(OperationResult result, AssignedSiteEntity assignedSite)
    {
        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Is.EqualTo("NoPermissionToUpdateAssignedSite"));

        var stored = await LoadStoredSiteAsync(assignedSite.Id);
        Assert.That(stored.IsManager, Is.False);
        Assert.That(stored.UseOnlyPlanHours, Is.False);
        Assert.That(stored.AllowPersonalTimeRegistration, Is.False);
        Assert.That(stored.AllowEditOfRegistrations, Is.False);
        Assert.That(await LoadManagingTagIdsAsync(assignedSite.Id), Is.EquivalentTo(new[] { 10 }));
        Assert.That(_pushNotificationService.ReceivedCalls(), Is.Empty,
            "a refused update must not tell the device its settings changed");
    }

    [Test]
    public async Task UpdateAssignedSite_WorkerEditor_SavesSettings_ButNotManagerOrTags()
    {
        var assignedSite = await CreateSiteAsync(siteId: 41);
        var user = await SignInAsNonAdminAsync("worker.editor@example.com");
        await AddToSecurityGroupAsync(user, "Example editors", AuthConsts.EformClaims.DeviceUsersClaims.Update);

        var result = await _settingsService.UpdateAssignedSite(
            RequestFor(assignedSite, isManager: true, managingTagIds: [20, 30]));

        Assert.That(result.Success, Is.True);
        var stored = await LoadStoredSiteAsync(assignedSite.Id);
        Assert.That(stored.UseOnlyPlanHours, Is.True);
        Assert.That(stored.AllowPersonalTimeRegistration, Is.True);
        Assert.That(stored.AllowEditOfRegistrations, Is.True);
        Assert.That(stored.IsManager, Is.False);
        Assert.That(await LoadManagingTagIdsAsync(assignedSite.Id), Is.EquivalentTo(new[] { 10 }));
    }

    [Test]
    public async Task UpdateAssignedSite_WorkerEditor_KeepsStoredManagerAndTags()
    {
        // For a non-admin the server ignores IsManager and the managing tags in
        // the request, so sending false and no tags keeps the stored values.
        var assignedSite = await CreateSiteAsync(siteId: 42, isManager: true);
        var user = await SignInAsNonAdminAsync("worker.editor2@example.com");
        await AddToSecurityGroupAsync(user, "Example editors", AuthConsts.EformClaims.DeviceUsersClaims.Update);

        var result = await _settingsService.UpdateAssignedSite(
            RequestFor(assignedSite, isManager: false, managingTagIds: []));

        Assert.That(result.Success, Is.True);
        var stored = await LoadStoredSiteAsync(assignedSite.Id);
        Assert.That(stored.IsManager, Is.True);
        Assert.That(await LoadManagingTagIdsAsync(assignedSite.Id), Is.EquivalentTo(new[] { 10 }));
    }

    [Test]
    public async Task UpdateAssignedSite_UserWithoutWorkerEditPermission_IsRefused()
    {
        var assignedSite = await CreateSiteAsync(siteId: 43);
        var user = await SignInAsNonAdminAsync("time.user@example.com");
        await AddToSecurityGroupAsync(user, "Example readers", AuthConsts.EformClaims.DeviceUsersClaims.Read);

        var result = await _settingsService.UpdateAssignedSite(
            RequestFor(assignedSite, isManager: true, managingTagIds: [20, 30]));

        await AssertRefusedAndUnchangedAsync(result, assignedSite);
    }

    [Test]
    public async Task UpdateAssignedSite_NoSignedInCaller_IsRefused()
    {
        var assignedSite = await CreateSiteAsync(siteId: 44);
        _userService.GetCurrentUserAsync().Returns((EformUser)null);

        var result = await _settingsService.UpdateAssignedSite(
            RequestFor(assignedSite, isManager: true, managingTagIds: [20, 30]));

        await AssertRefusedAndUnchangedAsync(result, assignedSite);
    }

    [Test]
    public async Task UpdateAssignedSite_Admin_SavesManagerTagsAndSettings()
    {
        // SetUp signed in as the admin.
        var assignedSite = await CreateSiteAsync(siteId: 45);

        var result = await _settingsService.UpdateAssignedSite(
            RequestFor(assignedSite, isManager: true, managingTagIds: [20, 30]));

        Assert.That(result.Success, Is.True);
        var stored = await LoadStoredSiteAsync(assignedSite.Id);
        Assert.That(stored.IsManager, Is.True);
        Assert.That(stored.UseOnlyPlanHours, Is.True);
        Assert.That(stored.AllowPersonalTimeRegistration, Is.True);
        Assert.That(stored.AllowEditOfRegistrations, Is.True);
        Assert.That(await LoadManagingTagIdsAsync(assignedSite.Id), Is.EquivalentTo(new[] { 20, 30 }));
    }

    [Test]
    public async Task UpdateAssignedSite_EformAdminsGroupMember_IsTreatedAsAdmin()
    {
        var assignedSite = await CreateSiteAsync(siteId: 46);
        var user = await SignInAsNonAdminAsync("group.admin@example.com");
        await AddToSecurityGroupAsync(user, "eForm admins");

        var result = await _settingsService.UpdateAssignedSite(
            RequestFor(assignedSite, isManager: true, managingTagIds: [20, 30]));

        Assert.That(result.Success, Is.True);
        var stored = await LoadStoredSiteAsync(assignedSite.Id);
        Assert.That(stored.IsManager, Is.True);
        Assert.That(await LoadManagingTagIdsAsync(assignedSite.Id), Is.EquivalentTo(new[] { 20, 30 }));
    }

    [Test]
    public void UpdateAssignedSite_Endpoint_LeavesTheAccessRuleToTheService()
    {
        // No role gate on the action: the worker edit dialog calls it for
        // non-admins, and the service decides what they may change.
        var attribute = typeof(TimePlanningSettingsController)
            .GetMethod(nameof(TimePlanningSettingsController.UpdateAssignedSite))!
            .GetCustomAttribute<AuthorizeAttribute>();

        Assert.That(attribute, Is.Not.Null);
        Assert.That(attribute!.Policy, Is.EqualTo(TimePlanningClaims.GetWorkingHours));
        Assert.That(attribute.Roles, Is.Null);
    }

    #endregion
}
