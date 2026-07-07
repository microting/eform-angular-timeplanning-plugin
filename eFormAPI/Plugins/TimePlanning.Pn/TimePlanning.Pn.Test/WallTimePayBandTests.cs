using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Microting.eFormApi.BasePn.Abstractions;
using Microting.eFormApi.BasePn.Infrastructure.Helpers.PluginDbOptions;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using NSubstitute;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Models.Settings;
using TimePlanning.Pn.Infrastructure.Models.WorkingHours.Index;
using TimePlanning.Pn.Services.TimePlanningLocalizationService;
using TimePlanning.Pn.Services.TimePlanningWorkingHoursService;

namespace TimePlanning.Pn.Test;

/// <summary>
/// WALL-TIME-AT-REST regression lock (green) for pay attribution. Time-band
/// pay rules (evening supplement) and the Grundlovsdag after-noon rule are
/// defined in local wall time — and because PlanRegistration stamps STORE
/// wall time, feeding the stored digits straight into seconds-of-day math is
/// CORRECT by design. These tests document that invariant: had the columns
/// held UTC, every band boundary would shift by the offset (the rejected
/// alternative design).
/// </summary>
[TestFixture]
public class WallTimePayBandTests
{
    private static TimePlanningWorkingHoursService BuildService()
    {
        return new TimePlanningWorkingHoursService(
            Substitute.For<ILogger<TimePlanningWorkingHoursService>>(),
            dbContext: null!,
            Substitute.For<IUserService>(),
            Substitute.For<ITimePlanningLocalizationService>(),
            baseDbContext: null!,
            Substitute.For<IPluginDbOptions<TimePlanningBaseSettings>>(),
            Substitute.For<IEFormCoreService>());
    }

    /// <summary>
    /// Evening shift stored as wall digits 18:30 → 00:00 (next day). The
    /// seconds-of-day pair is (66600, 86400): stop crosses midnight and clamps
    /// to end-of-day per the documented per-day scoping.
    /// </summary>
    [Test]
    public void ResolveShiftSeconds_WallDigits_MapDirectlyToSecondsOfDay()
    {
        var seg = TimePlanningWorkingHoursService.ResolveShiftSeconds(
            new DateTime(2026, 6, 15, 18, 30, 0),  // wall time
            new DateTime(2026, 6, 16, 0, 0, 0));   // wall time, crosses midnight

        Assert.That(seg, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(seg!.Value.Start, Is.EqualTo(66600),
                "18:30 wall = 66600 s — stored digits are already local, no conversion");
            Assert.That(seg.Value.Stop, Is.EqualTo(86400),
                "Midnight stop clamps to end of day");
        });
    }

    /// <summary>
    /// The full evening-band attribution: an 18:30–00:00 wall-time shift lies
    /// entirely inside an 18:00–24:00 evening band, so all 19800 s go to
    /// EVENING and nothing leaks into the default day code. 2026-06-15 is a
    /// Monday (DayType.Monday).
    /// </summary>
    [Test]
    public void CalculatePayLinesForDay_EveningBand_WallDigitsAttributeCorrectly()
    {
        var payRuleSet = new PayRuleSet
        {
            Name = "EveningBand",
            DayRules = new List<PayDayRule>(),
            DayTypeRules = new List<PayDayTypeRule>
            {
                new PayDayTypeRule
                {
                    DayType = DayType.Monday,
                    DefaultPayCode = "DAY",
                    Priority = 1,
                    TimeBandRules = new List<PayTimeBandRule>
                    {
                        new PayTimeBandRule
                        {
                            StartSecondOfDay = 64800, // 18:00
                            EndSecondOfDay = 86400,   // 24:00
                            PayCode = "EVENING",
                            PayrollCode = "200",
                            Priority = 1
                        }
                    }
                }
            }
        };

        var model = new TimePlanningWorkingHoursModel
        {
            Date = new DateTime(2026, 6, 15),
            Start1StartedAt = new DateTime(2026, 6, 15, 18, 30, 0), // wall time
            Stop1StoppedAt = new DateTime(2026, 6, 16, 0, 0, 0),    // wall time
        };

        var lines = TimePlanningWorkingHoursService.CalculatePayLinesForDay(
            planRegistrationId: 1, date: model.Date, dayModel: model,
            totalSeconds: 19800, payRuleSet: payRuleSet);

        var evening = lines.SingleOrDefault(l => l.PayCode == "EVENING");
        Assert.That(evening, Is.Not.Null, "Shift must produce an EVENING pay line");
        Assert.That(evening!.HoursInSeconds, Is.EqualTo(19800),
            "All 5.5 h of the 18:30–00:00 wall shift fall in the 18:00+ band; " +
            "correct precisely because stored digits are wall time");
        Assert.That(lines.Any(l => l.PayCode == "DAY"), Is.False,
            "Nothing may leak into the default day code");
    }

    /// <summary>
    /// Grundlovsdag (June 5): only hours after 12:00 noon count. A shift
    /// stored as wall digits 12:30–14:00 is entirely after noon → 1.5 h.
    /// CalculateHoursAfterNoon compares the stored digits against a naive
    /// 12:00 on the same date — correct under wall-time-at-rest.
    /// (Private helper — driven via reflection like the export would.)
    /// </summary>
    [Test]
    public void CalculateHoursAfterNoon_Grundlovsdag_WallDigits_CountAfterLocalNoon()
    {
        var day = new TimePlanningWorkingHoursModel
        {
            Date = new DateTime(2026, 6, 5),
            Start1StartedAt = new DateTime(2026, 6, 5, 12, 30, 0), // wall time
            Stop1StoppedAt = new DateTime(2026, 6, 5, 14, 0, 0),   // wall time
        };

        var method = typeof(TimePlanningWorkingHoursService).GetMethod(
            "CalculateHoursAfterNoon", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(method, Is.Not.Null);

        var hoursAfterNoon = (double)method!.Invoke(BuildService(), new object[] { day })!;

        Assert.That(hoursAfterNoon, Is.EqualTo(1.5).Within(1e-9),
            "12:30–14:00 wall time is entirely after the wall-clock noon → 1.5 h");
    }
}
