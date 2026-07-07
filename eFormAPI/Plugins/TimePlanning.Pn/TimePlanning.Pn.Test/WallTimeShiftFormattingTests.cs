using System;
using Microsoft.Extensions.Logging;
using Microting.eFormApi.BasePn.Abstractions;
using Microting.eFormApi.BasePn.Infrastructure.Helpers.PluginDbOptions;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using NSubstitute;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Models.Settings;
using TimePlanning.Pn.Services.TimePlanningLocalizationService;
using TimePlanning.Pn.Services.TimePlanningWorkingHoursService;

namespace TimePlanning.Pn.Test;

/// <summary>
/// WALL-TIME-AT-REST regression lock (green) for the read-side formatters.
/// PlanRegistration exact-time columns store user-local WALL digits, so
/// <c>GetShiftTime</c>/<c>GetShiftTimeFraction</c> must format the stored
/// digits VERBATIM — no timezone conversion exists on the read path, by
/// design. Also locks the id↔wall equivalence (Start1Id=79 ↔ 06:30) that the
/// wall-time convention rests on: the id path and the stamp path must render
/// the same wall time for the same shift.
/// </summary>
[TestFixture]
public class WallTimeShiftFormattingTests
{
    private TimePlanningWorkingHoursService _service = null!;

    [SetUp]
    public void SetUpTest()
    {
        var userService = Substitute.For<IUserService>();
        userService.UserId.Returns(1);

        // Pure formatting helpers — no database access, contexts stay null
        // (same fixture shape as DagsoversigtWorksheetExportTests' helper test).
        _service = new TimePlanningWorkingHoursService(
            Substitute.For<ILogger<TimePlanningWorkingHoursService>>(),
            dbContext: null!,
            userService,
            Substitute.For<ITimePlanningLocalizationService>(),
            baseDbContext: null!,
            Substitute.For<IPluginDbOptions<TimePlanningBaseSettings>>(),
            Substitute.For<IEFormCoreService>());
    }

    [Test]
    public void GetShiftTime_OneMinuteFlag_WallDigits_FormattedVerbatim()
    {
        // Stored stamp is wall time (prod oracle: Start1Id=79 ↔ 06:30 wall).
        var wallStamp = new DateTime(2026, 7, 7, 6, 30, 0);

        var formatted = _service.GetShiftTime(new PlanRegistration(), 79, wallStamp,
            useOneMinuteIntervals: true);

        Assert.That(formatted, Is.EqualTo("06:30"),
            "Stored wall digits must be rendered verbatim — no conversion on read");
    }

    [Test]
    public void GetShiftTime_IdPathAndStampPath_AgreeOnWallTime()
    {
        var plr = new PlanRegistration();
        var wallStamp = new DateTime(2026, 7, 7, 6, 30, 0);

        var fromId = _service.GetShiftTime(plr, 79);
        var fromStamp = _service.GetShiftTime(plr, 79, wallStamp, useOneMinuteIntervals: true);

        Assert.That(fromStamp, Is.EqualTo(fromId),
            "Interval id 79 and its wall stamp 06:30 must render identically — " +
            "the invariant that makes wall-time-at-rest coherent");
        Assert.That(fromId, Is.EqualTo("06:30"));
    }

    [Test]
    public void GetShiftTimeFraction_OneMinuteFlag_WallDigits_UsedVerbatim()
    {
        var wallStamp = new DateTime(2026, 7, 7, 6, 30, 0);

        var fraction = _service.GetShiftTimeFraction(79, wallStamp, useOneMinuteIntervals: true);

        Assert.That(fraction, Is.Not.Null);
        Assert.That(fraction!.Value, Is.EqualTo(390.0 / 1440.0).Within(1e-9),
            "Day-fraction comes straight from the stored wall digits (06:30 = 390 min)");
    }

    [Test]
    public void GetShiftTimeFraction_IdPathAndStampPath_AgreeOnWallTime()
    {
        var wallStamp = new DateTime(2026, 7, 7, 6, 30, 0);

        var fromId = _service.GetShiftTimeFraction(79, null, useOneMinuteIntervals: false);
        var fromStamp = _service.GetShiftTimeFraction(79, wallStamp, useOneMinuteIntervals: true);

        Assert.That(fromStamp!.Value, Is.EqualTo(fromId!.Value).Within(1e-9),
            "Id-derived and stamp-derived fractions must agree for the same wall time");
    }
}
