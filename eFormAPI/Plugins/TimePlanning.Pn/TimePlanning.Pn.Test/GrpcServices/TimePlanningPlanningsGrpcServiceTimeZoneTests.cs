using System;
using System.Threading.Tasks;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NUnit.Framework;
using Microting.eFormApi.BasePn.Abstractions;
using TimePlanning.Pn.Grpc;
using TimePlanning.Pn.Infrastructure.Models.Planning;
using TimePlanning.Pn.Services.GrpcServices;
using TimePlanning.Pn.Services.TimePlanningPlanningService;
using TimePlanning.Pn.Test.Helpers;
using OperationResult = Microting.eFormApi.BasePn.Infrastructure.Models.API.OperationResult;

namespace TimePlanning.Pn.Test.GrpcServices;

/// <summary>
/// Regression tests for the timezone handling in the gRPC write path
/// (UpdatePlanningByCurrentUser / UpdatePlanning → MapDayFromGrpc →
/// ParseDateTime). PlanRegistration exact-timestamp columns hold user-local
/// wall time by convention, so:
/// - input WITH an explicit zone designator (Z or ±hh:mm) must be converted
///   to the current user's timezone wall time (Kind=Unspecified);
/// - naive input (no designator) must be stored byte-verbatim.
/// </summary>
[TestFixture]
public class TimePlanningPlanningsGrpcServiceTimeZoneTests
{
    private ITimePlanningPlanningService _planningService;
    private IUserService _userService;
    private TimePlanningPlanningsGrpcService _grpcService;

    // Captured domain model passed to the service
    private TimePlanningPlanningPrDayModel _captured;

    [SetUp]
    public void SetUp()
    {
        _planningService = Substitute.For<ITimePlanningPlanningService>();
        _userService = Substitute.For<IUserService>();
        _userService.GetCurrentUserTimeZoneInfo()
            .Returns(TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen"));

        _grpcService = new TimePlanningPlanningsGrpcService(_planningService, _userService);
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
                _captured = ci.Arg<TimePlanningPlanningPrDayModel>();
                return new OperationResult(true, "OK");
            });
    }

    private async Task<TimePlanningPlanningPrDayModel> MapViaUpdateByCurrentUser(string start1StartedAt)
    {
        var request = new UpdatePlanningByCurrentUserRequest
        {
            Model = new PlanningPrDayModel { Start1StartedAt = start1StartedAt }
        };
        await _grpcService.UpdatePlanningByCurrentUser(
            request, TestServerCallContextFactory.Create());
        return _captured;
    }

    private async Task<TimePlanningPlanningPrDayModel> MapViaUpdatePlanning(string start1StartedAt)
    {
        var request = new UpdatePlanningRequest
        {
            PlanningId = 1,
            Model = new PlanningPrDayModel { Start1StartedAt = start1StartedAt }
        };
        await _grpcService.UpdatePlanning(
            request, TestServerCallContextFactory.Create());
        return _captured;
    }

    // ---------------------------------------------------------------
    // Explicit zone designator → converted to user wall time
    // ---------------------------------------------------------------

    [Test]
    public async Task ZSuffixedUtc_Summer_IsConvertedToUserWallTime()
    {
        // 04:30 UTC on a CEST date (+02:00) is 06:30 Copenhagen wall time.
        var result = await MapViaUpdateByCurrentUser("2026-07-07T04:30:00Z");

        Assert.That(result.Start1StartedAt, Is.EqualTo(new DateTime(2026, 7, 7, 6, 30, 0)));
        Assert.That(result.Start1StartedAt!.Value.Kind, Is.EqualTo(DateTimeKind.Unspecified));
    }

    [Test]
    public async Task ZSuffixedUtc_Winter_IsConvertedToUserWallTime()
    {
        // 04:30 UTC on a CET date (+01:00) is 05:30 Copenhagen wall time.
        var result = await MapViaUpdateByCurrentUser("2026-01-15T04:30:00Z");

        Assert.That(result.Start1StartedAt, Is.EqualTo(new DateTime(2026, 1, 15, 5, 30, 0)));
        Assert.That(result.Start1StartedAt!.Value.Kind, Is.EqualTo(DateTimeKind.Unspecified));
    }

    [Test]
    public async Task ExplicitZeroOffset_IsConvertedToUserWallTime()
    {
        // "+00:00" is the same instant as "Z" and must behave identically.
        var result = await MapViaUpdateByCurrentUser("2026-07-07T04:30:00+00:00");

        Assert.That(result.Start1StartedAt, Is.EqualTo(new DateTime(2026, 7, 7, 6, 30, 0)));
        Assert.That(result.Start1StartedAt!.Value.Kind, Is.EqualTo(DateTimeKind.Unspecified));
    }

    [Test]
    public async Task ExplicitNonUtcOffset_IsConvertedToUserWallTime()
    {
        // 10:00 +05:30 is 04:30 UTC, i.e. 06:30 Copenhagen wall time (CEST).
        var result = await MapViaUpdateByCurrentUser("2026-07-07T10:00:00+05:30");

        Assert.That(result.Start1StartedAt, Is.EqualTo(new DateTime(2026, 7, 7, 6, 30, 0)));
        Assert.That(result.Start1StartedAt!.Value.Kind, Is.EqualTo(DateTimeKind.Unspecified));
    }

    [Test]
    public async Task ZSuffixedUtc_ViaAdminUpdatePlanning_IsConvertedToUserWallTime()
    {
        // The admin UpdatePlanning RPC shares MapDayFromGrpc and must normalize too.
        var result = await MapViaUpdatePlanning("2026-07-07T04:30:00Z");

        Assert.That(result.Start1StartedAt, Is.EqualTo(new DateTime(2026, 7, 7, 6, 30, 0)));
        Assert.That(result.Start1StartedAt!.Value.Kind, Is.EqualTo(DateTimeKind.Unspecified));
    }

    // ---------------------------------------------------------------
    // Naive input (no zone designator) → byte-verbatim
    // ---------------------------------------------------------------

    [Test]
    public async Task NaiveTSeparated_IsStoredVerbatim()
    {
        var result = await MapViaUpdateByCurrentUser("2026-07-07T06:30:00");

        Assert.That(result.Start1StartedAt, Is.EqualTo(new DateTime(2026, 7, 7, 6, 30, 0)));
        Assert.That(result.Start1StartedAt!.Value.Kind, Is.EqualTo(DateTimeKind.Unspecified));
    }

    [Test]
    public async Task NaiveSpaceSeparatedDartToString_IsStoredVerbatim()
    {
        // Dart's DateTime.toString() for a naive local value.
        var result = await MapViaUpdateByCurrentUser("2026-07-07 06:30:00.000");

        Assert.That(result.Start1StartedAt, Is.EqualTo(new DateTime(2026, 7, 7, 6, 30, 0)));
        Assert.That(result.Start1StartedAt!.Value.Kind, Is.EqualTo(DateTimeKind.Unspecified));
    }

    // ---------------------------------------------------------------
    // Timezone resolution fallback
    // ---------------------------------------------------------------

    [Test]
    public async Task UserTimeZoneResolutionThrows_FallsBackToEuropeCopenhagen()
    {
        _userService.GetCurrentUserTimeZoneInfo()
            .Throws(new Exception("User not authorized!"));

        var result = await MapViaUpdateByCurrentUser("2026-07-07T04:30:00Z");

        Assert.That(result.Start1StartedAt, Is.EqualTo(new DateTime(2026, 7, 7, 6, 30, 0)));
    }

    [Test]
    public async Task NoUserService_FallsBackToEuropeCopenhagen()
    {
        // Existing fixtures construct the service without IUserService;
        // conversion must still normalize against the fallback zone.
        var grpcService = new TimePlanningPlanningsGrpcService(_planningService);
        var request = new UpdatePlanningByCurrentUserRequest
        {
            Model = new PlanningPrDayModel { Start1StartedAt = "2026-07-07T04:30:00Z" }
        };
        await grpcService.UpdatePlanningByCurrentUser(
            request, TestServerCallContextFactory.Create());

        Assert.That(_captured.Start1StartedAt, Is.EqualTo(new DateTime(2026, 7, 7, 6, 30, 0)));
    }
}
