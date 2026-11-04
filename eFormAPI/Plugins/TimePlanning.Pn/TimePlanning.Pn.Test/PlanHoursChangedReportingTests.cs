using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microting.eFormApi.BasePn.Infrastructure.Helpers.PluginDbOptions;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using NSubstitute;
using NUnit.Framework;
using Sentry;
using TimePlanning.Pn.Infrastructure.Helpers;
using TimePlanning.Pn.Infrastructure.Models.Planning;
using TimePlanning.Pn.Infrastructure.Models.Settings;
using TimePlanning.Pn.Services.TimePlanningPlanningService;
using AssignedSiteEntity = Microting.TimePlanningBase.Infrastructure.Data.Entities.AssignedSite;
using SdkSite = Microting.eForm.Infrastructure.Data.Entities.Site;

namespace TimePlanning.Pn.Test;

/// <summary>
/// How <see cref="PlanRegistrationHelper.UpdatePlanRegistrationsInPeriod"/>
/// reports a recomputed plan.
///
/// Recomputing a day's planned hours from its PlanText is expected on every
/// load of a sheet site. It used to send a Sentry warning for it, and a
/// `tainted` flag then repeated that warning for every later day of the loop,
/// changed or not ("from 7.5 to 7.5"). One dashboard load produced dozens of
/// events, split into several Sentry issues by the interpolated values.
///
/// The change is now logged once, as structured information, for the day that
/// actually changed, and Sentry hears nothing about it.
/// </summary>
[TestFixture]
// Installs a process-wide Sentry hub; a fixture running alongside could replace it.
[NonParallelizable]
public class PlanHoursChangedReportingTests : TestBaseSetup
{
    private const string PlanHoursChanged = "Plan hours changed";

    [Test]
    public async Task UpdatePlanRegistrationsInPeriod_OnlyTheChangedDayIsReported_AndNotToSentry()
    {
        var assignedSite = new AssignedSiteEntity
        {
            SiteId = 9406,
            UseGoogleSheetAsDefault = true,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await assignedSite.Create(TimePlanningPnDbContext);

        // Open days: after the payment cut-off and with nothing reconciled.
        var firstDay = DateTime.Today.AddDays(1);
        var changed = await CreateDay(firstDay, planHours: 5);
        var unchanged1 = await CreateDay(firstDay.AddDays(1), planHours: 7.5);
        var unchanged2 = await CreateDay(firstDay.AddDays(2), planHours: 7.5);
        var ids = new[] { changed.Id, unchanged1.Id, unchanged2.Id };

        var logger = new CapturingLogger();
        var sentryMessages = new List<string>();
        using (SiteScopeResolverTests.SentryHubCapturing(sentryMessages))
        {
            await RunUpdate(assignedSite, firstDay, logger);
            SentrySdk.Flush(TimeSpan.FromSeconds(2));
        }

        List<string> sentryAboutTheseDays;
        lock (sentryMessages)
        {
            sentryAboutTheseDays = sentryMessages
                .Where(m => m.Contains(PlanHoursChanged, StringComparison.Ordinal))
                .Where(m => ids.Any(id => m.Contains($"ID {id} ", StringComparison.Ordinal)))
                .ToList();
        }

        var logged = logger.Entries
            .Where(e => e.Message.Contains(PlanHoursChanged, StringComparison.Ordinal))
            .ToList();

        var stored = await TimePlanningPnDbContext.PlanRegistrations.AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.PlanHours);

        Assert.Multiple(() =>
        {
            Assert.That(stored[changed.Id], Is.EqualTo(7.5),
                "Control: the first day's hours are still recomputed from its PlanText and saved");
            Assert.That(stored[unchanged1.Id], Is.EqualTo(7.5));
            Assert.That(stored[unchanged2.Id], Is.EqualTo(7.5));

            Assert.That(sentryAboutTheseDays, Is.Empty,
                "A recomputed plan is expected behaviour, not a Sentry event: " +
                string.Join(" | ", sentryAboutTheseDays));

            Assert.That(logged, Has.Count.EqualTo(1),
                "Exactly the one day whose hours changed is reported: " +
                string.Join(" | ", logged.Select(e => e.Message)));
            Assert.That(logged.Select(e => e.Level), Is.All.EqualTo(LogLevel.Information));
            Assert.That(logged.Select(e => e.Message),
                Is.All.Contains($"PlanRegistration {changed.Id} "),
                "The report names the day that changed");
            Assert.That(logged.Select(e => (e.Values["OldPlanHours"], e.Values["NewPlanHours"])),
                Is.All.Matches<(object Old, object New)>(v => !Equals(v.Old, v.New)),
                "An unchanged day must never be reported as changed");
        });
    }

    private async Task<PlanRegistration> CreateDay(DateTime date, double planHours)
    {
        var registration = new PlanRegistration
        {
            SdkSitId = 9406,
            Date = date,
            PlanText = "8:00-16:00/0.5",
            PlanHours = planHours,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await registration.Create(TimePlanningPnDbContext);
        return registration;
    }

    private async Task RunUpdate(AssignedSiteEntity assignedSite, DateTime firstDay,
        ILogger<TimePlanningPlanningService> logger)
    {
        var options = Substitute.For<IPluginDbOptions<TimePlanningBaseSettings>>();
        options.Value.Returns(new TimePlanningBaseSettings
        {
            AutoBreakCalculationActive = "0",
            DayOfPayment = 20,
            GpsEnabled = "0",
            SnapshotEnabled = "0"
        });

        // Id + Date only, in date order, the way Index() hands them over.
        var planningsInPeriod = await TimePlanningPnDbContext.PlanRegistrations
            .AsNoTracking()
            .Where(x => x.SdkSitId == assignedSite.SiteId)
            .OrderBy(x => x.Date)
            .Select(x => new PlanRegistration { Id = x.Id, Date = x.Date })
            .ToListAsync();

        var siteModel = new TimePlanningPlanningModel
        {
            SiteId = assignedSite.SiteId,
            SiteName = $"Test site {assignedSite.SiteId}",
            PlanningPrDayModels = new List<TimePlanningPlanningPrDayModel>()
        };
        var sdkSite = new SdkSite
        {
            Name = $"Test site {assignedSite.SiteId}",
            MicrotingUid = assignedSite.SiteId
        };

        await PlanRegistrationHelper.UpdatePlanRegistrationsInPeriod(
            planningsInPeriod,
            siteModel,
            TimePlanningPnDbContext,
            assignedSite,
            logger,
            sdkSite,
            firstDay.AddDays(-1),
            firstDay.AddDays(3),
            options);
    }

    private sealed class CapturingLogger : ILogger<TimePlanningPlanningService>
    {
        public List<(LogLevel Level, string Message, Dictionary<string, object> Values)> Entries { get; } = new();

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception,
            Func<TState, Exception, string> formatter)
        {
            var values = state is IEnumerable<KeyValuePair<string, object>> pairs
                ? pairs.ToDictionary(p => p.Key, p => p.Value)
                : new Dictionary<string, object>();
            Entries.Add((logLevel, formatter(state, exception), values));
        }
    }
}
