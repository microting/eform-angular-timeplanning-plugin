using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microting.eFormApi.BasePn.Abstractions;
using Microting.eFormApi.BasePn.Infrastructure.Helpers.PluginDbOptions;
using Microting.TimePlanningBase.Infrastructure.Data;
using NSubstitute;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Helpers;
using TimePlanning.Pn.Infrastructure.Models.Settings;
using TimePlanning.Pn.Services.PushNotificationService;
using TimePlanning.Pn.Services.TimePlanningLocalizationService;
using TimePlanning.Pn.Services.TimePlanningSettingService;
using static TimePlanning.Pn.Test.PlanPatternTestSeed;

#nullable enable
namespace TimePlanning.Pn.Test;

/// <summary>
/// #5489 guarded addition 2: TimePlanningBaseSettings:GoogleSheetRetired.
/// Seeded false; while false the sheet behaves exactly as before, except that
/// the startup seed leaves workers that have a pattern off the sheet.
/// </summary>
[TestFixture]
// Mutates process-wide environment variables (PRIVATE_KEY_ID).
[NonParallelizable]
public class GoogleSheetRetirementTests : TestBaseSetup
{
    private static DateTime Today => DateTime.UtcNow.Date;

    // ------------------------------------------------------------------ setting

    [Test]
    public async Task Setting_IsSeededFalse()
    {
        var row = await TimePlanningPnDbContext!.PluginConfigurationValues.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Name == GoogleSheetRetirement.SettingName);

        Assert.That(row, Is.Not.Null);
        Assert.That(row!.Value, Is.EqualTo("false"));
        Assert.That(await GoogleSheetRetirement.IsRetiredAsync(TimePlanningPnDbContext), Is.False);
    }

    // One test rather than a TestCase per value: every test in this assembly
    // re-migrates its database, which costs far more than the assertions.
    [Test]
    public async Task IsRetired_ParsesTheValue()
    {
        var cases = new (string Value, bool Expected)[]
        {
            ("true", true), ("True", true), (" 1 ", true),
            ("false", false), ("0", false), ("", false), ("yes please", false)
        };

        foreach (var (value, expected) in cases)
        {
            await SetRetired(value);

            Assert.That(await GoogleSheetRetirement.IsRetiredAsync(TimePlanningPnDbContext!), Is.EqualTo(expected),
                $"async, value '{value}'");
            Assert.That(GoogleSheetRetirement.IsRetired(TimePlanningPnDbContext!), Is.EqualTo(expected),
                $"sync, value '{value}'");
        }
    }

    [Test]
    public async Task IsRetired_MissingRow_IsFalse()
    {
        var row = await TimePlanningPnDbContext!.PluginConfigurationValues
            .SingleAsync(x => x.Name == GoogleSheetRetirement.SettingName);
        TimePlanningPnDbContext.PluginConfigurationValues.Remove(row);
        await TimePlanningPnDbContext.SaveChangesAsync();

        Assert.That(await GoogleSheetRetirement.IsRetiredAsync(TimePlanningPnDbContext), Is.False);
    }

    // --------------------------------------------------------------------- seed

    [Test]
    public async Task Seed_NotRetired_UnpatternedWorkerIsStillPutOnTheSheet()
    {
        var worker = await CreateTemplateWorkerAsync(TimePlanningPnDbContext!, 7001, useGoogleSheet: false);

        new EformTimePlanningPlugin().SeedDatabase(PluginConnectionString);

        Assert.That(await UseGoogleSheet(worker.Id), Is.True);
    }

    [Test]
    public async Task Seed_NotRetired_PatternedWorkerStaysOffTheSheet()
    {
        var patterned = await CreateTemplateWorkerAsync(TimePlanningPnDbContext!, 7002, useGoogleSheet: false);
        await CreatePatternAsync(TimePlanningPnDbContext!, patterned, Today);
        var plain = await CreateTemplateWorkerAsync(TimePlanningPnDbContext!, 7003, useGoogleSheet: false);

        new EformTimePlanningPlugin().SeedDatabase(PluginConnectionString);
        new EformTimePlanningPlugin().SeedDatabase(PluginConnectionString);

        Assert.That(await UseGoogleSheet(patterned.Id), Is.False);
        Assert.That(await UseGoogleSheet(plain.Id), Is.True);
    }

    // Deleting a pattern hands the worker to the weekday template, not back
    // to the sheet, so a removed pattern still keeps them off it at start-up.
    [Test]
    public async Task Seed_NotRetired_WorkerWhosePatternWasDeletedStaysOffTheSheet()
    {
        var worker = await CreateTemplateWorkerAsync(TimePlanningPnDbContext!, 7004, useGoogleSheet: false);
        var pattern = await CreatePatternAsync(TimePlanningPnDbContext!, worker, Today);
        await pattern.Delete(TimePlanningPnDbContext!);
        var neverPatterned = await CreateTemplateWorkerAsync(TimePlanningPnDbContext!, 7007, useGoogleSheet: false);

        new EformTimePlanningPlugin().SeedDatabase(PluginConnectionString);
        new EformTimePlanningPlugin().SeedDatabase(PluginConnectionString);

        Assert.That(await UseGoogleSheet(worker.Id), Is.False);
        Assert.That(await UseGoogleSheet(neverPatterned.Id), Is.True);
    }

    [Test]
    public async Task Seed_Retired_TouchesNoWorkersSheetFlag_AndStaysRetiredAcrossAStart()
    {
        await SetRetired("true");
        var off = await CreateTemplateWorkerAsync(TimePlanningPnDbContext!, 7005, useGoogleSheet: false);
        var on = await CreateTemplateWorkerAsync(TimePlanningPnDbContext!, 7006, useGoogleSheet: true);

        new EformTimePlanningPlugin().SeedDatabase(PluginConnectionString);

        Assert.That(await UseGoogleSheet(off.Id), Is.False);
        Assert.That(await UseGoogleSheet(on.Id), Is.True);

        // Both start-up paths that touch configuration: SeedDatabase (above)
        // writes missing values through TimePlanningPluginSeed.SeedData, and
        // AddPluginConfig loads them into IConfiguration.
        var configuration = new ConfigurationBuilder();
        new EformTimePlanningPlugin().AddPluginConfig(configuration, PluginConnectionString);
        Assert.That(configuration.Build()[GoogleSheetRetirement.SettingName], Is.EqualTo("true"),
            "plugin start must not reset the retired flag");
        await using var fresh = CreateTimePlanningPnDbContext();
        Assert.That(await GoogleSheetRetirement.IsRetiredAsync(fresh), Is.True);
    }

    // ----------------------------------------------------------- UpdateSettings

    // UpdateSettings relies on the guards inside GoogleSheetHelper. With
    // PRIVATE_KEY_ID set and no SDK core, a call that gets past the guard
    // fails and UpdateSettings reports it; a retired sheet is never reached.
    [Test]
    public async Task UpdateSettings_ReachesTheSheetOnlyWhileNotRetired()
    {
        var service = CreateSettingsService();
        var previous = Environment.GetEnvironmentVariable("PRIVATE_KEY_ID");
        Environment.SetEnvironmentVariable("PRIVATE_KEY_ID", "configured");
        try
        {
            var notRetired = await service.UpdateSettings(SettingsModel(forcePull: true));
            await SetRetired("true");
            var retired = await service.UpdateSettings(SettingsModel(forcePull: true));

            Assert.That(notRetired.Success, Is.False, "control: a live sheet is reached");
            Assert.That(retired.Success, Is.True, retired.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PRIVATE_KEY_ID", previous);
        }
    }

    // -------------------------------------------------------- GoogleSheetHelper

    [Test]
    public async Task GoogleSheetHelper_Retired_ReturnsBeforeTouchingTheSheet()
    {
        await SetRetired("true");
        var previous = Environment.GetEnvironmentVariable("PRIVATE_KEY_ID");
        Environment.SetEnvironmentVariable("PRIVATE_KEY_ID", "configured");
        try
        {
            // A null Core would throw the moment either method went past its guard.
            await Assert.DoesNotThrowAsync(() =>
                GoogleSheetHelper.PushToGoogleSheet(null!, TimePlanningPnDbContext!, Substitute.For<ILogger>()));
            await Assert.DoesNotThrowAsync(() =>
                GoogleSheetHelper.PullEverythingFromGoogleSheet(null!, TimePlanningPnDbContext!, Substitute.For<ILogger>()));
        }
        finally
        {
            Environment.SetEnvironmentVariable("PRIVATE_KEY_ID", previous);
        }
    }

    // ------------------------------------------------------------------ helpers

    private async Task SetRetired(string value)
    {
        var row = await TimePlanningPnDbContext!.PluginConfigurationValues
            .SingleAsync(x => x.Name == GoogleSheetRetirement.SettingName);
        row.Value = value;
        await TimePlanningPnDbContext.SaveChangesAsync();
    }

    private async Task<bool> UseGoogleSheet(int assignedSiteId)
    {
        await using var fresh = CreateTimePlanningPnDbContext();
        return (await fresh.AssignedSites.AsNoTracking().SingleAsync(x => x.Id == assignedSiteId))
            .UseGoogleSheetAsDefault;
    }

    private static TimePlanningSettingsModel SettingsModel(bool forcePull) => new()
    {
        GoogleSheetId = "sheet-id",
        DayOfPayment = 20,
        ForceLoadAllPlanningsFromGoogleSheet = forcePull
    };

    private TimeSettingService CreateSettingsService()
    {
        var userService = Substitute.For<IUserService>();
        userService.UserId.Returns(1);
        var localization = Substitute.For<ITimePlanningLocalizationService>();
        localization.GetString(Arg.Any<string>()).Returns(x => x[0]?.ToString());
        var options = Substitute.For<IPluginDbOptions<TimePlanningBaseSettings>>();
        options.Value.Returns(new TimePlanningBaseSettings());

        return new TimeSettingService(
            options,
            TimePlanningPnDbContext!,
            Substitute.For<ILogger<TimeSettingService>>(),
            userService,
            localization,
            GetBaseDbContext(),
            Substitute.For<IEFormCoreService>(),
            Substitute.For<IPushNotificationService>());
    }
}
