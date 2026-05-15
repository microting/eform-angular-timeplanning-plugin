using System;
using System.Collections.Generic;
using System.Linq;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Models.WorkingHours.Index;
using TimePlanning.Pn.Services.TimePlanningWorkingHoursService;

namespace TimePlanning.Pn.Test;

/// <summary>
/// Regression coverage for <c>TimePlanningWorkingHoursService.CalculatePayLinesForDay</c>,
/// the dispatcher that routes a day's worked seconds either to tier-based pay lines
/// (<see cref="PayLineGenerator.GeneratePayLines"/>) or to time-band attribution
/// (<see cref="PayLineGenerator.GenerateTimeBandPayLines"/>). The HIGH-risk gap from
/// the 2026-05-15 coverage audit: neither branch was exercised by any unit test, so
/// 5-min rounding of non-5-min stamps could regress silently. These tests lock both
/// branches against the user-reported 08:04→10:10 = 7560 s example.
/// </summary>
[TestFixture]
public class CalculatePayLinesForDayTests
{
    private static DateTime D(int hour, int min) => new DateTime(2026, 5, 15, hour, min, 0);

    [Test]
    public void NullPayRuleSet_ReturnsEmpty()
    {
        var model = new TimePlanningWorkingHoursModel
        {
            Date = new DateTime(2026, 5, 15),
            Start1StartedAt = D(8, 4),
            Stop1StoppedAt = D(10, 10),
        };
        var lines = TimePlanningWorkingHoursService.CalculatePayLinesForDay(
            planRegistrationId: 1, date: model.Date, dayModel: model,
            totalSeconds: 7560, payRuleSet: null!);
        Assert.That(lines, Is.Empty);
    }

    [Test]
    public void TierPath_NoTimeBandRules_UsesTotalSeconds_Exact7560()
    {
        // Friday → WEEKDAY. PayRuleSet has only tier rules (no TimeBandRules) so the
        // time-band branch is skipped and the tier path consumes totalSeconds directly.
        var payRuleSet = new PayRuleSet
        {
            Name = "TierOnly",
            DayRules = new List<PayDayRule>
            {
                new PayDayRule
                {
                    DayCode = "WEEKDAY",
                    Tiers = new List<PayTierRule>
                    {
                        new PayTierRule { UpToSeconds = null, PayCode = "WORK", PayrollCode = "100", Order = 1 }
                    }
                }
            },
            DayTypeRules = new List<PayDayTypeRule>(),
        };

        var model = new TimePlanningWorkingHoursModel
        {
            Date = new DateTime(2026, 5, 15),
            Start1StartedAt = D(8, 4),
            Stop1StoppedAt = D(10, 10),
        };

        var lines = TimePlanningWorkingHoursService.CalculatePayLinesForDay(
            planRegistrationId: 1, date: model.Date, dayModel: model,
            totalSeconds: 7560, payRuleSet: payRuleSet);

        Assert.That(lines, Is.Not.Empty);
        Assert.That(lines.Sum(l => l.HoursInSeconds), Is.EqualTo(7560),
            "Tier path must consume exact totalSeconds without 5-min rounding");
    }

    [Test]
    public void TierPath_ZeroTotalSeconds_ReturnsEmpty()
    {
        var payRuleSet = new PayRuleSet
        {
            Name = "TierOnly",
            DayRules = new List<PayDayRule>
            {
                new PayDayRule
                {
                    DayCode = "WEEKDAY",
                    Tiers = new List<PayTierRule>
                    {
                        new PayTierRule { UpToSeconds = null, PayCode = "WORK", PayrollCode = "100", Order = 1 }
                    }
                }
            },
            DayTypeRules = new List<PayDayTypeRule>(),
        };

        var model = new TimePlanningWorkingHoursModel { Date = new DateTime(2026, 5, 15) };
        var lines = TimePlanningWorkingHoursService.CalculatePayLinesForDay(
            planRegistrationId: 1, date: model.Date, dayModel: model,
            totalSeconds: 0, payRuleSet: payRuleSet);
        Assert.That(lines, Is.Empty);
    }

    [Test]
    public void TimeBandPath_SingleBand_NonRoundStamps_PreserveExactSeconds()
    {
        // Friday with one all-day band → WORK. The time-band branch fires (DayTypeRules has
        // TimeBandRules for DayType.Friday) and EnumerateShiftSegments yields the 08:04→10:10
        // segment. Total attributed seconds must equal 7560.
        var payRuleSet = new PayRuleSet
        {
            Name = "AllDayWork",
            DayRules = new List<PayDayRule>(),
            DayTypeRules = new List<PayDayTypeRule>
            {
                new PayDayTypeRule
                {
                    DayType = DayType.Friday,
                    DefaultPayCode = "WORK",
                    Priority = 1,
                    TimeBandRules = new List<PayTimeBandRule>
                    {
                        new PayTimeBandRule
                        {
                            StartSecondOfDay = 0, EndSecondOfDay = 86400,
                            PayCode = "WORK", PayrollCode = "100", Priority = 1,
                        }
                    }
                }
            }
        };

        var model = new TimePlanningWorkingHoursModel
        {
            Date = new DateTime(2026, 5, 15),
            Start1StartedAt = D(8, 4),
            Stop1StoppedAt = D(10, 10),
        };

        var lines = TimePlanningWorkingHoursService.CalculatePayLinesForDay(
            planRegistrationId: 1, date: model.Date, dayModel: model,
            totalSeconds: 7560, payRuleSet: payRuleSet);

        Assert.That(lines, Is.Not.Empty, "Time-band path should emit at least one pay line for a populated shift");
        Assert.That(lines.Sum(l => l.HoursInSeconds), Is.EqualTo(7560),
            "Time-band attribution must preserve sub-5-min precision (08:04→10:10 = 7560 s)");
    }

    [Test]
    public void TimeBandPath_NullStamps_YieldsZeroLines()
    {
        // Documents the design intent at TimePlanningWorkingHoursService.cs:3795-3801:
        // "If a shift has no real timestamps populated, it has no recorded clock time
        // and contributes no time-band pay lines."
        var payRuleSet = new PayRuleSet
        {
            Name = "AllDayWork",
            DayRules = new List<PayDayRule>(),
            DayTypeRules = new List<PayDayTypeRule>
            {
                new PayDayTypeRule
                {
                    DayType = DayType.Friday,
                    DefaultPayCode = "WORK",
                    Priority = 1,
                    TimeBandRules = new List<PayTimeBandRule>
                    {
                        new PayTimeBandRule
                        {
                            StartSecondOfDay = 0, EndSecondOfDay = 86400,
                            PayCode = "WORK", PayrollCode = "100", Priority = 1,
                        }
                    }
                }
            }
        };

        var model = new TimePlanningWorkingHoursModel { Date = new DateTime(2026, 5, 15) };
        var lines = TimePlanningWorkingHoursService.CalculatePayLinesForDay(
            planRegistrationId: 1, date: model.Date, dayModel: model,
            totalSeconds: 0, payRuleSet: payRuleSet);
        Assert.That(lines, Is.Empty);
    }

    [Test]
    public void TierPath_RoundMinutes_UsesTotalSeconds_Exact7500()
    {
        // 5-min-aligned counterpart: 08:00→10:05 = 7500 s.  Legacy slot path
        // must produce the same exact total without rounding drift.
        var payRuleSet = new PayRuleSet
        {
            Name = "TierOnly",
            DayRules = new List<PayDayRule>
            {
                new PayDayRule
                {
                    DayCode = "WEEKDAY",
                    Tiers = new List<PayTierRule>
                    {
                        new PayTierRule { UpToSeconds = null, PayCode = "WORK", PayrollCode = "100", Order = 1 }
                    }
                }
            },
            DayTypeRules = new List<PayDayTypeRule>(),
        };
        var model = new TimePlanningWorkingHoursModel
        {
            Date = new DateTime(2026, 5, 15),
            Start1StartedAt = D(8, 0),
            Stop1StoppedAt = D(10, 5),
        };
        var lines = TimePlanningWorkingHoursService.CalculatePayLinesForDay(
            planRegistrationId: 1, date: model.Date, dayModel: model,
            totalSeconds: 7500, payRuleSet: payRuleSet);
        Assert.That(lines.Sum(l => l.HoursInSeconds), Is.EqualTo(7500),
            "5-min-aligned tier path must preserve exact totalSeconds");
    }

    [Test]
    public void TimeBandPath_RoundMinutes_SingleBand_PreservesExactSeconds()
    {
        var payRuleSet = new PayRuleSet
        {
            Name = "AllDayWork",
            DayRules = new List<PayDayRule>(),
            DayTypeRules = new List<PayDayTypeRule>
            {
                new PayDayTypeRule
                {
                    DayType = DayType.Friday,
                    DefaultPayCode = "WORK",
                    Priority = 1,
                    TimeBandRules = new List<PayTimeBandRule>
                    {
                        new PayTimeBandRule
                        {
                            StartSecondOfDay = 0, EndSecondOfDay = 86400,
                            PayCode = "WORK", PayrollCode = "100", Priority = 1,
                        }
                    }
                }
            }
        };
        var model = new TimePlanningWorkingHoursModel
        {
            Date = new DateTime(2026, 5, 15),
            Start1StartedAt = D(8, 0),
            Stop1StoppedAt = D(10, 5),
        };
        var lines = TimePlanningWorkingHoursService.CalculatePayLinesForDay(
            planRegistrationId: 1, date: model.Date, dayModel: model,
            totalSeconds: 7500, payRuleSet: payRuleSet);
        Assert.That(lines.Sum(l => l.HoursInSeconds), Is.EqualTo(7500),
            "5-min-aligned time-band attribution must equal worked seconds");
    }

    [Test]
    public void TimeBandPath_ShiftCrossesBandBoundary_AttributedToBothBands()
    {
        // Friday with two bands: 00:00-09:00 = NIGHT, 09:00-24:00 = WORK.
        // Shift 08:04 → 10:10 splits across the 09:00 boundary:
        //   NIGHT: 08:04 → 09:00 = 56 min = 3360 s
        //   WORK:  09:00 → 10:10 = 70 min = 4200 s
        var payRuleSet = new PayRuleSet
        {
            Name = "NightThenWork",
            DayRules = new List<PayDayRule>(),
            DayTypeRules = new List<PayDayTypeRule>
            {
                new PayDayTypeRule
                {
                    DayType = DayType.Friday,
                    DefaultPayCode = "WORK",
                    Priority = 1,
                    TimeBandRules = new List<PayTimeBandRule>
                    {
                        new PayTimeBandRule
                        {
                            StartSecondOfDay = 0, EndSecondOfDay = 9 * 3600,
                            PayCode = "NIGHT", PayrollCode = "200", Priority = 1,
                        },
                        new PayTimeBandRule
                        {
                            StartSecondOfDay = 9 * 3600, EndSecondOfDay = 86400,
                            PayCode = "WORK", PayrollCode = "100", Priority = 2,
                        },
                    }
                }
            }
        };

        var model = new TimePlanningWorkingHoursModel
        {
            Date = new DateTime(2026, 5, 15),
            Start1StartedAt = D(8, 4),
            Stop1StoppedAt = D(10, 10),
        };

        var lines = TimePlanningWorkingHoursService.CalculatePayLinesForDay(
            planRegistrationId: 1, date: model.Date, dayModel: model,
            totalSeconds: 7560, payRuleSet: payRuleSet);

        Assert.That(lines.Sum(l => l.HoursInSeconds), Is.EqualTo(7560),
            "Sum across all bands must equal worked seconds");

        var night = lines.SingleOrDefault(l => l.PayCode == "NIGHT");
        var work  = lines.SingleOrDefault(l => l.PayCode == "WORK");
        Assert.That(night, Is.Not.Null, "Expected a NIGHT pay line");
        Assert.That(work,  Is.Not.Null, "Expected a WORK pay line");
        Assert.That(night!.HoursInSeconds, Is.EqualTo(3360), "08:04→09:00 = 56 min = 3360 s");
        Assert.That(work!.HoursInSeconds,  Is.EqualTo(4200), "09:00→10:10 = 70 min = 4200 s");
    }
}
