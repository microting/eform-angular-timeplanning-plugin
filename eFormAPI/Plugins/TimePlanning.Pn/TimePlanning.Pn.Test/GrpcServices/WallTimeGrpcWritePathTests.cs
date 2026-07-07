using System;
using System.Threading.Tasks;
using Microting.eFormApi.BasePn.Abstractions;
using NSubstitute;
using NUnit.Framework;
using TimePlanning.Pn.Grpc;
using TimePlanning.Pn.Infrastructure.Models.Planning;
using TimePlanning.Pn.Services.GrpcServices;
using TimePlanning.Pn.Services.TimePlanningPlanningService;
using TimePlanning.Pn.Test.Helpers;
using OperationResult = Microting.eFormApi.BasePn.Infrastructure.Models.API.OperationResult;

namespace TimePlanning.Pn.Test.GrpcServices;

/// <summary>
/// WALL-TIME-AT-REST contract for the gRPC plannings write path
/// (<c>UpdatePlanningByCurrentUser</c> / <c>UpdatePlanning</c> → MapDayFromGrpc →
/// ParseDateTime). PlanRegistration exact-time columns store USER-LOCAL WALL
/// TIME digits: punch-clock flows write local wall digits, flag-off sites are
/// id-driven, and EnsureTimestampsFromIds backfills wall digits from interval
/// ids. The ONLY UTC writer is the app edit screen ("register workday"), whose
/// <c>.toUtc().toIso8601String()</c> Z-path shipped in the June 25 Android
/// release — this RPC (<c>UpdatePlanningByCurrentUser</c>) is exactly what that
/// screen calls, so it must normalize Z/offset-carrying stamps into the
/// current user's zone (IUserService.GetCurrentUserTimeZoneInfo(), default
/// Europe/Copenhagen) before they reach storage. Naive digits pass through
/// verbatim.
/// </summary>
[TestFixture]
public class WallTimeGrpcWritePathTests
{
    private ITimePlanningPlanningService _planningService = null!;
    private TimePlanningPlanningsGrpcService _grpcService = null!;
    private TimePlanningPlanningPrDayModel _captured = null!;

    [SetUp]
    public void SetUp()
    {
        _planningService = Substitute.For<ITimePlanningPlanningService>();

        // The current user's zone — the fix resolves it once per write request
        // via IUserService.GetCurrentUserTimeZoneInfo() (default when absent:
        // Europe/Copenhagen, covered by WallTimeNormalizerTests).
        var userService = Substitute.For<IUserService>();
        userService.GetCurrentUserTimeZoneInfo()
            .Returns(TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen"));

        _grpcService = new TimePlanningPlanningsGrpcService(_planningService, userService);
        _captured = null!;

        _planningService.UpdateByCurrentUserNam(Arg.Any<TimePlanningPlanningPrDayModel>())
            .Returns(ci =>
            {
                _captured = ci.Arg<TimePlanningPlanningPrDayModel>();
                return new OperationResult(true, "OK");
            });
        _planningService.Update(Arg.Any<int>(), Arg.Any<TimePlanningPlanningPrDayModel>())
            .Returns(ci =>
            {
                _captured = ci.ArgAt<TimePlanningPlanningPrDayModel>(1);
                return new OperationResult(true, "OK");
            });
    }

    private async Task<TimePlanningPlanningPrDayModel> MapViaUpdateByCurrentUser(PlanningPrDayModel proto)
    {
        await _grpcService.UpdatePlanningByCurrentUser(
            new UpdatePlanningByCurrentUserRequest { Model = proto },
            TestServerCallContextFactory.Create());
        return _captured;
    }

    /// <summary>
    /// The June-25 app edit screen path: Z-suffixed UTC stamps. 2026-07-07 is
    /// CEST (+02:00), so 04:30Z/14:00Z is the 06:30–16:00 wall-time shift
    /// (prod oracle row 16703 shape). The mapped model must carry the WALL
    /// digits — today ParseDateTime's AdjustToUniversal lets the UTC digits
    /// (04:30/14:00) through to storage.
    /// </summary>
    [Test]
    public async Task UpdateByCurrentUser_ZSuffixedUtcStamps_NormalizeToWallDigits()
    {
        var model = await MapViaUpdateByCurrentUser(new PlanningPrDayModel
        {
            Id = 1,
            Start1StartedAt = "2026-07-07T04:30:00Z",
            Stop1StoppedAt = "2026-07-07T14:00:00Z",
        });

        Assert.Multiple(() =>
        {
            Assert.That(model.Start1StartedAt, Is.EqualTo(new DateTime(2026, 7, 7, 6, 30, 0)),
                "Z-suffixed 04:30Z must be normalized to the user's wall time 06:30 " +
                "(Europe/Copenhagen); letting UTC digits through violates wall-time-at-rest");
            Assert.That(model.Stop1StoppedAt, Is.EqualTo(new DateTime(2026, 7, 7, 16, 0, 0)),
                "Z-suffixed 14:00Z must be normalized to wall time 16:00");
        });
    }

    /// <summary>
    /// Explicit +00:00 offset — same instant as the Z form, same rule.
    /// </summary>
    [Test]
    public async Task UpdateByCurrentUser_PlusZeroOffsetStamp_NormalizesToWallDigits()
    {
        var model = await MapViaUpdateByCurrentUser(new PlanningPrDayModel
        {
            Id = 1,
            Start1StartedAt = "2026-07-07T04:30:00+00:00",
        });

        Assert.That(model.Start1StartedAt, Is.EqualTo(new DateTime(2026, 7, 7, 6, 30, 0)),
            "A +00:00 offset stamp is the same instant as 04:30Z and must land as wall 06:30");
    }

    /// <summary>
    /// Non-UTC offset: 10:00+05:30 is the instant 04:30Z — must convert into
    /// the USER's zone (06:30 Copenhagen wall), not be kept at the sender's
    /// digits and not be adjusted to UTC.
    /// </summary>
    [Test]
    public async Task UpdateByCurrentUser_NonUtcOffsetStamp_ConvertsIntoUserZone()
    {
        var model = await MapViaUpdateByCurrentUser(new PlanningPrDayModel
        {
            Id = 1,
            Start1StartedAt = "2026-07-07T10:00:00+05:30",
        });

        Assert.That(model.Start1StartedAt, Is.EqualTo(new DateTime(2026, 7, 7, 6, 30, 0)),
            "10:00+05:30 == 04:30Z == 06:30 Europe/Copenhagen wall time");
    }

    /// <summary>
    /// Admin path shares MapDayFromGrpc — same normalization rule.
    /// </summary>
    [Test]
    public async Task UpdatePlanning_AdminPath_ZSuffixedUtcStamp_NormalizesToWallDigits()
    {
        await _grpcService.UpdatePlanning(new UpdatePlanningRequest
        {
            PlanningId = 5,
            Model = new PlanningPrDayModel
            {
                Id = 5,
                Start1StartedAt = "2026-07-07T04:30:00Z",
            }
        }, TestServerCallContextFactory.Create());

        Assert.That(_captured.Start1StartedAt, Is.EqualTo(new DateTime(2026, 7, 7, 6, 30, 0)),
            "Admin gRPC update must apply the same UTC→wall normalization");
    }

    /// <summary>
    /// REGRESSION LOCK (green today): naive wall digits — the shape all
    /// punch-clock flows and pre-June-25 app builds send — pass through
    /// verbatim, byte-identical.
    /// </summary>
    [Test]
    public async Task UpdateByCurrentUser_NaiveWallDigits_PassThroughVerbatim()
    {
        var model = await MapViaUpdateByCurrentUser(new PlanningPrDayModel
        {
            Id = 1,
            Start1StartedAt = "2026-07-07T06:30:00",
            Stop1StoppedAt = "2026-07-07T16:00:00",
        });

        Assert.Multiple(() =>
        {
            Assert.That(model.Start1StartedAt, Is.EqualTo(new DateTime(2026, 7, 7, 6, 30, 0)),
                "Naive wall digits are already the storage convention — must not be shifted");
            Assert.That(model.Stop1StoppedAt, Is.EqualTo(new DateTime(2026, 7, 7, 16, 0, 0)));
        });
    }
}
