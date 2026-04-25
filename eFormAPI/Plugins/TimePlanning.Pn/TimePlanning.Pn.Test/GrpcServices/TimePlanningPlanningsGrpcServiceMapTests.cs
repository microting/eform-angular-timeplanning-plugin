using System;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
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
/// Contract tests for MapDayFromGrpc — verifies every proto field on
/// PlanningPrDayModel is correctly mapped to TimePlanningPlanningPrDayModel
/// when calling UpdatePlanningByCurrentUser.
/// </summary>
[TestFixture]
public class TimePlanningPlanningsGrpcServiceMapTests
{
    private ITimePlanningPlanningService _planningService;
    private TimePlanningPlanningsGrpcService _grpcService;

    // Captured domain model passed to the service
    private TimePlanningPlanningPrDayModel _captured;

    [SetUp]
    public void SetUp()
    {
        _planningService = Substitute.For<ITimePlanningPlanningService>();
        _grpcService = new TimePlanningPlanningsGrpcService(_planningService);
        _captured = null!;

        _planningService.UpdateByCurrentUserNam(Arg.Any<TimePlanningPlanningPrDayModel>())
            .Returns(ci =>
            {
                _captured = ci.Arg<TimePlanningPlanningPrDayModel>();
                return new OperationResult(true, "OK");
            });
    }

    /// <summary>
    /// Helper: sends a proto model through the gRPC endpoint and captures
    /// the mapped domain model that MapDayFromGrpc produced.
    /// </summary>
    private async Task<TimePlanningPlanningPrDayModel> MapViaGrpc(PlanningPrDayModel proto)
    {
        var request = new UpdatePlanningByCurrentUserRequest { Model = proto };
        await _grpcService.UpdatePlanningByCurrentUser(
            request, TestServerCallContextFactory.Create());
        return _captured;
    }

    // ---------------------------------------------------------------
    // 1. Identity fields
    // ---------------------------------------------------------------

    [Test]
    public async Task MapDayFromGrpc_Identity_Id()
    {
        var result = await MapViaGrpc(new PlanningPrDayModel { Id = 42 });
        Assert.That(result.Id, Is.EqualTo(42));
    }

    [Test]
    public async Task MapDayFromGrpc_Identity_SdkSiteIdMapsToSiteId()
    {
        var result = await MapViaGrpc(new PlanningPrDayModel { SdkSiteId = 99 });
        Assert.That(result.SiteId, Is.EqualTo(99));
    }

    [Test]
    public async Task MapDayFromGrpc_Identity_Date()
    {
        var ts = Timestamp.FromDateTime(
            DateTime.SpecifyKind(new DateTime(2026, 6, 15), DateTimeKind.Utc));
        var result = await MapViaGrpc(new PlanningPrDayModel { Date = ts });
        Assert.That(result.Date, Is.EqualTo(new DateTime(2026, 6, 15)));
    }

    [Test]
    public async Task MapDayFromGrpc_Identity_NullDateDefaultsToMinValue()
    {
        var result = await MapViaGrpc(new PlanningPrDayModel { Id = 1 });
        Assert.That(result.Date, Is.EqualTo(DateTime.MinValue));
    }

    // ---------------------------------------------------------------
    // 2. Text fields
    // ---------------------------------------------------------------

    [Test]
    public async Task MapDayFromGrpc_Text_PlanText()
    {
        var result = await MapViaGrpc(new PlanningPrDayModel { PlanText = "Meeting day" });
        Assert.That(result.PlanText, Is.EqualTo("Meeting day"));
    }

    [Test]
    public async Task MapDayFromGrpc_Text_CommentOffice()
    {
        var result = await MapViaGrpc(new PlanningPrDayModel { CommentOffice = "Approved" });
        Assert.That(result.CommentOffice, Is.EqualTo("Approved"));
    }

    [Test]
    public async Task MapDayFromGrpc_Text_WorkerComment()
    {
        var result = await MapViaGrpc(new PlanningPrDayModel { WorkerComment = "Feeling good" });
        Assert.That(result.WorkerComment, Is.EqualTo("Feeling good"));
    }

    [Test]
    public async Task MapDayFromGrpc_Text_SiteName()
    {
        var result = await MapViaGrpc(new PlanningPrDayModel { SiteName = "Site Alpha" });
        Assert.That(result.SiteName, Is.EqualTo("Site Alpha"));
    }

    // ---------------------------------------------------------------
    // 3. Shift time IDs — non-zero maps to value, zero maps to null
    // ---------------------------------------------------------------

    [Test]
    public async Task MapDayFromGrpc_ShiftIds_Start1Id_NonZero()
    {
        var result = await MapViaGrpc(new PlanningPrDayModel { Start1Id = 101 });
        Assert.That(result.Start1Id, Is.EqualTo(101));
    }

    [Test]
    public async Task MapDayFromGrpc_ShiftIds_Start1Id_ZeroMapsToNull()
    {
        var result = await MapViaGrpc(new PlanningPrDayModel { Start1Id = 0 });
        Assert.That(result.Start1Id, Is.Null);
    }

    [Test]
    public async Task MapDayFromGrpc_ShiftIds_Stop1Id_NonZero()
    {
        var result = await MapViaGrpc(new PlanningPrDayModel { Stop1Id = 102 });
        Assert.That(result.Stop1Id, Is.EqualTo(102));
    }

    [Test]
    public async Task MapDayFromGrpc_ShiftIds_Stop1Id_ZeroMapsToNull()
    {
        var result = await MapViaGrpc(new PlanningPrDayModel { Stop1Id = 0 });
        Assert.That(result.Stop1Id, Is.Null);
    }

    [Test]
    public async Task MapDayFromGrpc_ShiftIds_Start2Id_NonZero()
    {
        var result = await MapViaGrpc(new PlanningPrDayModel { Start2Id = 201 });
        Assert.That(result.Start2Id, Is.EqualTo(201));
    }

    [Test]
    public async Task MapDayFromGrpc_ShiftIds_Start2Id_ZeroMapsToNull()
    {
        var result = await MapViaGrpc(new PlanningPrDayModel { Start2Id = 0 });
        Assert.That(result.Start2Id, Is.Null);
    }

    [Test]
    public async Task MapDayFromGrpc_ShiftIds_Stop2Id_NonZero()
    {
        var result = await MapViaGrpc(new PlanningPrDayModel { Stop2Id = 202 });
        Assert.That(result.Stop2Id, Is.EqualTo(202));
    }

    [Test]
    public async Task MapDayFromGrpc_ShiftIds_Stop2Id_ZeroMapsToNull()
    {
        var result = await MapViaGrpc(new PlanningPrDayModel { Stop2Id = 0 });
        Assert.That(result.Stop2Id, Is.Null);
    }

    [Test]
    public async Task MapDayFromGrpc_ShiftIds_Start3Id_NonZero()
    {
        var result = await MapViaGrpc(new PlanningPrDayModel { Start3Id = 301 });
        Assert.That(result.Start3Id, Is.EqualTo(301));
    }

    [Test]
    public async Task MapDayFromGrpc_ShiftIds_Start3Id_ZeroMapsToNull()
    {
        var result = await MapViaGrpc(new PlanningPrDayModel { Start3Id = 0 });
        Assert.That(result.Start3Id, Is.Null);
    }

    [Test]
    public async Task MapDayFromGrpc_ShiftIds_Stop3Id_NonZero()
    {
        var result = await MapViaGrpc(new PlanningPrDayModel { Stop3Id = 302 });
        Assert.That(result.Stop3Id, Is.EqualTo(302));
    }

    [Test]
    public async Task MapDayFromGrpc_ShiftIds_Stop3Id_ZeroMapsToNull()
    {
        var result = await MapViaGrpc(new PlanningPrDayModel { Stop3Id = 0 });
        Assert.That(result.Stop3Id, Is.Null);
    }

    [Test]
    public async Task MapDayFromGrpc_ShiftIds_Start4Id_NonZero()
    {
        var result = await MapViaGrpc(new PlanningPrDayModel { Start4Id = 401 });
        Assert.That(result.Start4Id, Is.EqualTo(401));
    }

    [Test]
    public async Task MapDayFromGrpc_ShiftIds_Start4Id_ZeroMapsToNull()
    {
        var result = await MapViaGrpc(new PlanningPrDayModel { Start4Id = 0 });
        Assert.That(result.Start4Id, Is.Null);
    }

    [Test]
    public async Task MapDayFromGrpc_ShiftIds_Stop4Id_NonZero()
    {
        var result = await MapViaGrpc(new PlanningPrDayModel { Stop4Id = 402 });
        Assert.That(result.Stop4Id, Is.EqualTo(402));
    }

    [Test]
    public async Task MapDayFromGrpc_ShiftIds_Stop4Id_ZeroMapsToNull()
    {
        var result = await MapViaGrpc(new PlanningPrDayModel { Stop4Id = 0 });
        Assert.That(result.Stop4Id, Is.Null);
    }

    [Test]
    public async Task MapDayFromGrpc_ShiftIds_Start5Id_NonZero()
    {
        var result = await MapViaGrpc(new PlanningPrDayModel { Start5Id = 501 });
        Assert.That(result.Start5Id, Is.EqualTo(501));
    }

    [Test]
    public async Task MapDayFromGrpc_ShiftIds_Start5Id_ZeroMapsToNull()
    {
        var result = await MapViaGrpc(new PlanningPrDayModel { Start5Id = 0 });
        Assert.That(result.Start5Id, Is.Null);
    }

    [Test]
    public async Task MapDayFromGrpc_ShiftIds_Stop5Id_NonZero()
    {
        var result = await MapViaGrpc(new PlanningPrDayModel { Stop5Id = 502 });
        Assert.That(result.Stop5Id, Is.EqualTo(502));
    }

    [Test]
    public async Task MapDayFromGrpc_ShiftIds_Stop5Id_ZeroMapsToNull()
    {
        var result = await MapViaGrpc(new PlanningPrDayModel { Stop5Id = 0 });
        Assert.That(result.Stop5Id, Is.Null);
    }

    // ---------------------------------------------------------------
    // 4. Pause IDs — zero maps to null
    // ---------------------------------------------------------------

    [Test]
    public async Task MapDayFromGrpc_PauseIds_Pause1Id_NonZero()
    {
        var result = await MapViaGrpc(new PlanningPrDayModel { Pause1Id = 111 });
        Assert.That(result.Pause1Id, Is.EqualTo(111));
    }

    [Test]
    public async Task MapDayFromGrpc_PauseIds_Pause1Id_ZeroMapsToNull()
    {
        var result = await MapViaGrpc(new PlanningPrDayModel { Pause1Id = 0 });
        Assert.That(result.Pause1Id, Is.Null);
    }

    [Test]
    public async Task MapDayFromGrpc_PauseIds_Pause2Id_NonZero()
    {
        var result = await MapViaGrpc(new PlanningPrDayModel { Pause2Id = 222 });
        Assert.That(result.Pause2Id, Is.EqualTo(222));
    }

    [Test]
    public async Task MapDayFromGrpc_PauseIds_Pause2Id_ZeroMapsToNull()
    {
        var result = await MapViaGrpc(new PlanningPrDayModel { Pause2Id = 0 });
        Assert.That(result.Pause2Id, Is.Null);
    }

    [Test]
    public async Task MapDayFromGrpc_PauseIds_Pause3Id_NonZero()
    {
        var result = await MapViaGrpc(new PlanningPrDayModel { Pause3Id = 333 });
        Assert.That(result.Pause3Id, Is.EqualTo(333));
    }

    [Test]
    public async Task MapDayFromGrpc_PauseIds_Pause3Id_ZeroMapsToNull()
    {
        var result = await MapViaGrpc(new PlanningPrDayModel { Pause3Id = 0 });
        Assert.That(result.Pause3Id, Is.Null);
    }

    [Test]
    public async Task MapDayFromGrpc_PauseIds_Pause4Id_NonZero()
    {
        var result = await MapViaGrpc(new PlanningPrDayModel { Pause4Id = 444 });
        Assert.That(result.Pause4Id, Is.EqualTo(444));
    }

    [Test]
    public async Task MapDayFromGrpc_PauseIds_Pause4Id_ZeroMapsToNull()
    {
        var result = await MapViaGrpc(new PlanningPrDayModel { Pause4Id = 0 });
        Assert.That(result.Pause4Id, Is.Null);
    }

    [Test]
    public async Task MapDayFromGrpc_PauseIds_Pause5Id_NonZero()
    {
        var result = await MapViaGrpc(new PlanningPrDayModel { Pause5Id = 555 });
        Assert.That(result.Pause5Id, Is.EqualTo(555));
    }

    [Test]
    public async Task MapDayFromGrpc_PauseIds_Pause5Id_ZeroMapsToNull()
    {
        var result = await MapViaGrpc(new PlanningPrDayModel { Pause5Id = 0 });
        Assert.That(result.Pause5Id, Is.Null);
    }

    // ---------------------------------------------------------------
    // 5. Break durations
    // ---------------------------------------------------------------

    [Test]
    public async Task MapDayFromGrpc_BreakDurations_AllShifts()
    {
        var proto = new PlanningPrDayModel
        {
            Break1Shift = 15,
            Break2Shift = 30,
            Break3Shift = 45,
            Break4Shift = 10,
            Break5Shift = 20,
        };
        var result = await MapViaGrpc(proto);

        Assert.That(result.Break1Shift, Is.EqualTo(15));
        Assert.That(result.Break2Shift, Is.EqualTo(30));
        Assert.That(result.Break3Shift, Is.EqualTo(45));
        Assert.That(result.Break4Shift, Is.EqualTo(10));
        Assert.That(result.Break5Shift, Is.EqualTo(20));
    }

    // ---------------------------------------------------------------
    // 6. Planned shift fields
    // ---------------------------------------------------------------

    [Test]
    public async Task MapDayFromGrpc_PlannedShifts_Shift1()
    {
        var proto = new PlanningPrDayModel
        {
            PlannedStartOfShift1 = 800,
            PlannedEndOfShift1 = 1600,
            PlannedBreakOfShift1 = 30,
        };
        var result = await MapViaGrpc(proto);

        Assert.That(result.PlannedStartOfShift1, Is.EqualTo(800));
        Assert.That(result.PlannedEndOfShift1, Is.EqualTo(1600));
        Assert.That(result.PlannedBreakOfShift1, Is.EqualTo(30));
    }

    [Test]
    public async Task MapDayFromGrpc_PlannedShifts_Shift2()
    {
        var proto = new PlanningPrDayModel
        {
            PlannedStartOfShift2 = 900,
            PlannedEndOfShift2 = 1700,
            PlannedBreakOfShift2 = 45,
        };
        var result = await MapViaGrpc(proto);

        Assert.That(result.PlannedStartOfShift2, Is.EqualTo(900));
        Assert.That(result.PlannedEndOfShift2, Is.EqualTo(1700));
        Assert.That(result.PlannedBreakOfShift2, Is.EqualTo(45));
    }

    [Test]
    public async Task MapDayFromGrpc_PlannedShifts_Shift3()
    {
        var proto = new PlanningPrDayModel
        {
            PlannedStartOfShift3 = 1000,
            PlannedEndOfShift3 = 1800,
            PlannedBreakOfShift3 = 60,
        };
        var result = await MapViaGrpc(proto);

        Assert.That(result.PlannedStartOfShift3, Is.EqualTo(1000));
        Assert.That(result.PlannedEndOfShift3, Is.EqualTo(1800));
        Assert.That(result.PlannedBreakOfShift3, Is.EqualTo(60));
    }

    [Test]
    public async Task MapDayFromGrpc_PlannedShifts_Shift4()
    {
        var proto = new PlanningPrDayModel
        {
            PlannedStartOfShift4 = 1100,
            PlannedEndOfShift4 = 1900,
            PlannedBreakOfShift4 = 15,
        };
        var result = await MapViaGrpc(proto);

        Assert.That(result.PlannedStartOfShift4, Is.EqualTo(1100));
        Assert.That(result.PlannedEndOfShift4, Is.EqualTo(1900));
        Assert.That(result.PlannedBreakOfShift4, Is.EqualTo(15));
    }

    [Test]
    public async Task MapDayFromGrpc_PlannedShifts_Shift5()
    {
        var proto = new PlanningPrDayModel
        {
            PlannedStartOfShift5 = 1200,
            PlannedEndOfShift5 = 2000,
            PlannedBreakOfShift5 = 20,
        };
        var result = await MapViaGrpc(proto);

        Assert.That(result.PlannedStartOfShift5, Is.EqualTo(1200));
        Assert.That(result.PlannedEndOfShift5, Is.EqualTo(2000));
        Assert.That(result.PlannedBreakOfShift5, Is.EqualTo(20));
    }

    // ---------------------------------------------------------------
    // 6b. Primary shift timestamps (ParseDateTime: string -> DateTime?)
    //     Start/Stop/Pause for shifts 1-5
    // ---------------------------------------------------------------

    [Test]
    public async Task MapDayFromGrpc_ShiftTimestamps_Shift1()
    {
        var proto = new PlanningPrDayModel
        {
            Start1StartedAt = "2026-06-15T07:00:00",
            Stop1StoppedAt = "2026-06-15T15:30:00",
            Pause1StartedAt = "2026-06-15T12:00:00",
            Pause1StoppedAt = "2026-06-15T12:30:00",
        };
        var result = await MapViaGrpc(proto);

        Assert.That(result.Start1StartedAt, Is.EqualTo(new DateTime(2026, 6, 15, 7, 0, 0)));
        Assert.That(result.Stop1StoppedAt, Is.EqualTo(new DateTime(2026, 6, 15, 15, 30, 0)));
        Assert.That(result.Pause1StartedAt, Is.EqualTo(new DateTime(2026, 6, 15, 12, 0, 0)));
        Assert.That(result.Pause1StoppedAt, Is.EqualTo(new DateTime(2026, 6, 15, 12, 30, 0)));
    }

    [Test]
    public async Task MapDayFromGrpc_ShiftTimestamps_Shift2()
    {
        var proto = new PlanningPrDayModel
        {
            Start2StartedAt = "2026-06-15T16:00:00",
            Stop2StoppedAt = "2026-06-16T00:00:00",
            Pause2StartedAt = "2026-06-15T20:00:00",
            Pause2StoppedAt = "2026-06-15T20:30:00",
        };
        var result = await MapViaGrpc(proto);

        Assert.That(result.Start2StartedAt, Is.EqualTo(new DateTime(2026, 6, 15, 16, 0, 0)));
        Assert.That(result.Stop2StoppedAt, Is.EqualTo(new DateTime(2026, 6, 16, 0, 0, 0)));
        Assert.That(result.Pause2StartedAt, Is.EqualTo(new DateTime(2026, 6, 15, 20, 0, 0)));
        Assert.That(result.Pause2StoppedAt, Is.EqualTo(new DateTime(2026, 6, 15, 20, 30, 0)));
    }

    [Test]
    public async Task MapDayFromGrpc_ShiftTimestamps_Shift3()
    {
        var proto = new PlanningPrDayModel
        {
            Start3StartedAt = "2026-06-15T08:00:00",
            Stop3StoppedAt = "2026-06-15T16:00:00",
            Pause3StartedAt = "2026-06-15T12:00:00",
            Pause3StoppedAt = "2026-06-15T12:45:00",
        };
        var result = await MapViaGrpc(proto);

        Assert.That(result.Start3StartedAt, Is.EqualTo(new DateTime(2026, 6, 15, 8, 0, 0)));
        Assert.That(result.Stop3StoppedAt, Is.EqualTo(new DateTime(2026, 6, 15, 16, 0, 0)));
        Assert.That(result.Pause3StartedAt, Is.EqualTo(new DateTime(2026, 6, 15, 12, 0, 0)));
        Assert.That(result.Pause3StoppedAt, Is.EqualTo(new DateTime(2026, 6, 15, 12, 45, 0)));
    }

    [Test]
    public async Task MapDayFromGrpc_ShiftTimestamps_Shift4()
    {
        var proto = new PlanningPrDayModel
        {
            Start4StartedAt = "2026-06-15T09:00:00",
            Stop4StoppedAt = "2026-06-15T17:00:00",
            Pause4StartedAt = "2026-06-15T13:00:00",
            Pause4StoppedAt = "2026-06-15T13:30:00",
        };
        var result = await MapViaGrpc(proto);

        Assert.That(result.Start4StartedAt, Is.EqualTo(new DateTime(2026, 6, 15, 9, 0, 0)));
        Assert.That(result.Stop4StoppedAt, Is.EqualTo(new DateTime(2026, 6, 15, 17, 0, 0)));
        Assert.That(result.Pause4StartedAt, Is.EqualTo(new DateTime(2026, 6, 15, 13, 0, 0)));
        Assert.That(result.Pause4StoppedAt, Is.EqualTo(new DateTime(2026, 6, 15, 13, 30, 0)));
    }

    [Test]
    public async Task MapDayFromGrpc_ShiftTimestamps_Shift5()
    {
        var proto = new PlanningPrDayModel
        {
            Start5StartedAt = "2026-06-15T10:00:00",
            Stop5StoppedAt = "2026-06-15T18:00:00",
            Pause5StartedAt = "2026-06-15T14:00:00",
            Pause5StoppedAt = "2026-06-15T14:15:00",
        };
        var result = await MapViaGrpc(proto);

        Assert.That(result.Start5StartedAt, Is.EqualTo(new DateTime(2026, 6, 15, 10, 0, 0)));
        Assert.That(result.Stop5StoppedAt, Is.EqualTo(new DateTime(2026, 6, 15, 18, 0, 0)));
        Assert.That(result.Pause5StartedAt, Is.EqualTo(new DateTime(2026, 6, 15, 14, 0, 0)));
        Assert.That(result.Pause5StoppedAt, Is.EqualTo(new DateTime(2026, 6, 15, 14, 15, 0)));
    }

    [Test]
    public async Task MapDayFromGrpc_ShiftTimestamps_EmptyStringMapsToNull()
    {
        var proto = new PlanningPrDayModel
        {
            Start1StartedAt = "",
            Stop1StoppedAt = "",
            Pause1StartedAt = "",
            Pause1StoppedAt = "",
        };
        var result = await MapViaGrpc(proto);

        Assert.That(result.Start1StartedAt, Is.Null);
        Assert.That(result.Stop1StoppedAt, Is.Null);
        Assert.That(result.Pause1StartedAt, Is.Null);
        Assert.That(result.Pause1StoppedAt, Is.Null);
    }

    // ---------------------------------------------------------------
    // 7. Detailed pause timestamps (ParseDateTime: string -> DateTime?)
    //    Shift 1: Pause10-Pause19, Pause100-Pause102
    //    Shift 2: Pause20-Pause29, Pause200-Pause202
    // ---------------------------------------------------------------

    [Test]
    public async Task MapDayFromGrpc_PauseTimestamps_Shift1_Pause10()
    {
        var proto = new PlanningPrDayModel
        {
            Pause10StartedAt = "2026-06-15T10:00:00",
            Pause10StoppedAt = "2026-06-15T10:15:00",
        };
        var result = await MapViaGrpc(proto);

        Assert.That(result.Pause10StartedAt, Is.EqualTo(new DateTime(2026, 6, 15, 10, 0, 0)));
        Assert.That(result.Pause10StoppedAt, Is.EqualTo(new DateTime(2026, 6, 15, 10, 15, 0)));
    }

    [Test]
    public async Task MapDayFromGrpc_PauseTimestamps_Shift1_Pause11()
    {
        var proto = new PlanningPrDayModel
        {
            Pause11StartedAt = "2026-06-15T11:00:00",
            Pause11StoppedAt = "2026-06-15T11:10:00",
        };
        var result = await MapViaGrpc(proto);

        Assert.That(result.Pause11StartedAt, Is.EqualTo(new DateTime(2026, 6, 15, 11, 0, 0)));
        Assert.That(result.Pause11StoppedAt, Is.EqualTo(new DateTime(2026, 6, 15, 11, 10, 0)));
    }

    [Test]
    public async Task MapDayFromGrpc_PauseTimestamps_Shift1_Pause12()
    {
        var proto = new PlanningPrDayModel
        {
            Pause12StartedAt = "2026-06-15T12:00:00",
            Pause12StoppedAt = "2026-06-15T12:30:00",
        };
        var result = await MapViaGrpc(proto);

        Assert.That(result.Pause12StartedAt, Is.EqualTo(new DateTime(2026, 6, 15, 12, 0, 0)));
        Assert.That(result.Pause12StoppedAt, Is.EqualTo(new DateTime(2026, 6, 15, 12, 30, 0)));
    }

    [Test]
    public async Task MapDayFromGrpc_PauseTimestamps_Shift1_Pause13Through19()
    {
        var proto = new PlanningPrDayModel
        {
            Pause13StartedAt = "2026-06-15T13:00:00",
            Pause13StoppedAt = "2026-06-15T13:05:00",
            Pause14StartedAt = "2026-06-15T14:00:00",
            Pause14StoppedAt = "2026-06-15T14:05:00",
            Pause15StartedAt = "2026-06-15T15:00:00",
            Pause15StoppedAt = "2026-06-15T15:05:00",
            Pause16StartedAt = "2026-06-15T16:00:00",
            Pause16StoppedAt = "2026-06-15T16:05:00",
            Pause17StartedAt = "2026-06-15T17:00:00",
            Pause17StoppedAt = "2026-06-15T17:05:00",
            Pause18StartedAt = "2026-06-15T18:00:00",
            Pause18StoppedAt = "2026-06-15T18:05:00",
            Pause19StartedAt = "2026-06-15T19:00:00",
            Pause19StoppedAt = "2026-06-15T19:05:00",
        };
        var result = await MapViaGrpc(proto);

        Assert.That(result.Pause13StartedAt, Is.EqualTo(new DateTime(2026, 6, 15, 13, 0, 0)));
        Assert.That(result.Pause13StoppedAt, Is.EqualTo(new DateTime(2026, 6, 15, 13, 5, 0)));
        Assert.That(result.Pause14StartedAt, Is.EqualTo(new DateTime(2026, 6, 15, 14, 0, 0)));
        Assert.That(result.Pause14StoppedAt, Is.EqualTo(new DateTime(2026, 6, 15, 14, 5, 0)));
        Assert.That(result.Pause15StartedAt, Is.EqualTo(new DateTime(2026, 6, 15, 15, 0, 0)));
        Assert.That(result.Pause15StoppedAt, Is.EqualTo(new DateTime(2026, 6, 15, 15, 5, 0)));
        Assert.That(result.Pause16StartedAt, Is.EqualTo(new DateTime(2026, 6, 15, 16, 0, 0)));
        Assert.That(result.Pause16StoppedAt, Is.EqualTo(new DateTime(2026, 6, 15, 16, 5, 0)));
        Assert.That(result.Pause17StartedAt, Is.EqualTo(new DateTime(2026, 6, 15, 17, 0, 0)));
        Assert.That(result.Pause17StoppedAt, Is.EqualTo(new DateTime(2026, 6, 15, 17, 5, 0)));
        Assert.That(result.Pause18StartedAt, Is.EqualTo(new DateTime(2026, 6, 15, 18, 0, 0)));
        Assert.That(result.Pause18StoppedAt, Is.EqualTo(new DateTime(2026, 6, 15, 18, 5, 0)));
        Assert.That(result.Pause19StartedAt, Is.EqualTo(new DateTime(2026, 6, 15, 19, 0, 0)));
        Assert.That(result.Pause19StoppedAt, Is.EqualTo(new DateTime(2026, 6, 15, 19, 5, 0)));
    }

    [Test]
    public async Task MapDayFromGrpc_PauseTimestamps_Shift1_Pause100Through102()
    {
        var proto = new PlanningPrDayModel
        {
            Pause100StartedAt = "2026-06-15T10:00:00",
            Pause100StoppedAt = "2026-06-15T10:10:00",
            Pause101StartedAt = "2026-06-15T11:00:00",
            Pause101StoppedAt = "2026-06-15T11:10:00",
            Pause102StartedAt = "2026-06-15T12:00:00",
            Pause102StoppedAt = "2026-06-15T12:10:00",
        };
        var result = await MapViaGrpc(proto);

        Assert.That(result.Pause100StartedAt, Is.EqualTo(new DateTime(2026, 6, 15, 10, 0, 0)));
        Assert.That(result.Pause100StoppedAt, Is.EqualTo(new DateTime(2026, 6, 15, 10, 10, 0)));
        Assert.That(result.Pause101StartedAt, Is.EqualTo(new DateTime(2026, 6, 15, 11, 0, 0)));
        Assert.That(result.Pause101StoppedAt, Is.EqualTo(new DateTime(2026, 6, 15, 11, 10, 0)));
        Assert.That(result.Pause102StartedAt, Is.EqualTo(new DateTime(2026, 6, 15, 12, 0, 0)));
        Assert.That(result.Pause102StoppedAt, Is.EqualTo(new DateTime(2026, 6, 15, 12, 10, 0)));
    }

    [Test]
    public async Task MapDayFromGrpc_PauseTimestamps_Shift2_Pause20Through29()
    {
        var proto = new PlanningPrDayModel
        {
            Pause20StartedAt = "2026-06-15T20:00:00",
            Pause20StoppedAt = "2026-06-15T20:05:00",
            Pause21StartedAt = "2026-06-15T21:00:00",
            Pause21StoppedAt = "2026-06-15T21:05:00",
            Pause22StartedAt = "2026-06-15T22:00:00",
            Pause22StoppedAt = "2026-06-15T22:05:00",
            Pause23StartedAt = "2026-06-15T23:00:00",
            Pause23StoppedAt = "2026-06-15T23:05:00",
            Pause24StartedAt = "2026-06-16T00:00:00",
            Pause24StoppedAt = "2026-06-16T00:05:00",
            Pause25StartedAt = "2026-06-16T01:00:00",
            Pause25StoppedAt = "2026-06-16T01:05:00",
            Pause26StartedAt = "2026-06-16T02:00:00",
            Pause26StoppedAt = "2026-06-16T02:05:00",
            Pause27StartedAt = "2026-06-16T03:00:00",
            Pause27StoppedAt = "2026-06-16T03:05:00",
            Pause28StartedAt = "2026-06-16T04:00:00",
            Pause28StoppedAt = "2026-06-16T04:05:00",
            Pause29StartedAt = "2026-06-16T05:00:00",
            Pause29StoppedAt = "2026-06-16T05:05:00",
        };
        var result = await MapViaGrpc(proto);

        Assert.That(result.Pause20StartedAt, Is.EqualTo(new DateTime(2026, 6, 15, 20, 0, 0)));
        Assert.That(result.Pause20StoppedAt, Is.EqualTo(new DateTime(2026, 6, 15, 20, 5, 0)));
        Assert.That(result.Pause21StartedAt, Is.EqualTo(new DateTime(2026, 6, 15, 21, 0, 0)));
        Assert.That(result.Pause21StoppedAt, Is.EqualTo(new DateTime(2026, 6, 15, 21, 5, 0)));
        Assert.That(result.Pause22StartedAt, Is.EqualTo(new DateTime(2026, 6, 15, 22, 0, 0)));
        Assert.That(result.Pause22StoppedAt, Is.EqualTo(new DateTime(2026, 6, 15, 22, 5, 0)));
        Assert.That(result.Pause23StartedAt, Is.EqualTo(new DateTime(2026, 6, 15, 23, 0, 0)));
        Assert.That(result.Pause23StoppedAt, Is.EqualTo(new DateTime(2026, 6, 15, 23, 5, 0)));
        Assert.That(result.Pause24StartedAt, Is.EqualTo(new DateTime(2026, 6, 16, 0, 0, 0)));
        Assert.That(result.Pause24StoppedAt, Is.EqualTo(new DateTime(2026, 6, 16, 0, 5, 0)));
        Assert.That(result.Pause25StartedAt, Is.EqualTo(new DateTime(2026, 6, 16, 1, 0, 0)));
        Assert.That(result.Pause25StoppedAt, Is.EqualTo(new DateTime(2026, 6, 16, 1, 5, 0)));
        Assert.That(result.Pause26StartedAt, Is.EqualTo(new DateTime(2026, 6, 16, 2, 0, 0)));
        Assert.That(result.Pause26StoppedAt, Is.EqualTo(new DateTime(2026, 6, 16, 2, 5, 0)));
        Assert.That(result.Pause27StartedAt, Is.EqualTo(new DateTime(2026, 6, 16, 3, 0, 0)));
        Assert.That(result.Pause27StoppedAt, Is.EqualTo(new DateTime(2026, 6, 16, 3, 5, 0)));
        Assert.That(result.Pause28StartedAt, Is.EqualTo(new DateTime(2026, 6, 16, 4, 0, 0)));
        Assert.That(result.Pause28StoppedAt, Is.EqualTo(new DateTime(2026, 6, 16, 4, 5, 0)));
        Assert.That(result.Pause29StartedAt, Is.EqualTo(new DateTime(2026, 6, 16, 5, 0, 0)));
        Assert.That(result.Pause29StoppedAt, Is.EqualTo(new DateTime(2026, 6, 16, 5, 5, 0)));
    }

    [Test]
    public async Task MapDayFromGrpc_PauseTimestamps_Shift2_Pause200Through202()
    {
        var proto = new PlanningPrDayModel
        {
            Pause200StartedAt = "2026-06-16T06:00:00",
            Pause200StoppedAt = "2026-06-16T06:10:00",
            Pause201StartedAt = "2026-06-16T07:00:00",
            Pause201StoppedAt = "2026-06-16T07:10:00",
            Pause202StartedAt = "2026-06-16T08:00:00",
            Pause202StoppedAt = "2026-06-16T08:10:00",
        };
        var result = await MapViaGrpc(proto);

        Assert.That(result.Pause200StartedAt, Is.EqualTo(new DateTime(2026, 6, 16, 6, 0, 0)));
        Assert.That(result.Pause200StoppedAt, Is.EqualTo(new DateTime(2026, 6, 16, 6, 10, 0)));
        Assert.That(result.Pause201StartedAt, Is.EqualTo(new DateTime(2026, 6, 16, 7, 0, 0)));
        Assert.That(result.Pause201StoppedAt, Is.EqualTo(new DateTime(2026, 6, 16, 7, 10, 0)));
        Assert.That(result.Pause202StartedAt, Is.EqualTo(new DateTime(2026, 6, 16, 8, 0, 0)));
        Assert.That(result.Pause202StoppedAt, Is.EqualTo(new DateTime(2026, 6, 16, 8, 10, 0)));
    }

    [Test]
    public async Task MapDayFromGrpc_PauseTimestamps_EmptyStringMapsToNull()
    {
        var proto = new PlanningPrDayModel
        {
            Pause10StartedAt = "",
            Pause10StoppedAt = "",
        };
        var result = await MapViaGrpc(proto);

        Assert.That(result.Pause10StartedAt, Is.Null);
        Assert.That(result.Pause10StoppedAt, Is.Null);
    }

    // ---------------------------------------------------------------
    // 8. Boolean flags
    // ---------------------------------------------------------------

    [Test]
    public async Task MapDayFromGrpc_Booleans_OnVacation_True()
    {
        var result = await MapViaGrpc(new PlanningPrDayModel { OnVacation = true });
        Assert.That(result.OnVacation, Is.True);
    }

    [Test]
    public async Task MapDayFromGrpc_Booleans_OnVacation_False()
    {
        var result = await MapViaGrpc(new PlanningPrDayModel { OnVacation = false });
        Assert.That(result.OnVacation, Is.False);
    }

    [Test]
    public async Task MapDayFromGrpc_Booleans_Sick_True()
    {
        var result = await MapViaGrpc(new PlanningPrDayModel { Sick = true });
        Assert.That(result.Sick, Is.True);
    }

    [Test]
    public async Task MapDayFromGrpc_Booleans_Sick_False()
    {
        var result = await MapViaGrpc(new PlanningPrDayModel { Sick = false });
        Assert.That(result.Sick, Is.False);
    }

    [Test]
    public async Task MapDayFromGrpc_Booleans_OtherAllowedAbsence_True()
    {
        var result = await MapViaGrpc(new PlanningPrDayModel { OtherAllowedAbsence = true });
        Assert.That(result.OtherAllowedAbsence, Is.True);
    }

    [Test]
    public async Task MapDayFromGrpc_Booleans_AbsenceWithoutPermission_True()
    {
        var result = await MapViaGrpc(new PlanningPrDayModel { AbsenceWithoutPermission = true });
        Assert.That(result.AbsenceWithoutPermission, Is.True);
    }

    [Test]
    public async Task MapDayFromGrpc_Booleans_IsDoubleShift_True()
    {
        var result = await MapViaGrpc(new PlanningPrDayModel { IsDoubleShift = true });
        Assert.That(result.IsDoubleShift, Is.True);
    }

    [Test]
    public async Task MapDayFromGrpc_Booleans_WorkDayStarted_True()
    {
        var result = await MapViaGrpc(new PlanningPrDayModel { WorkDayStarted = true });
        Assert.That(result.WorkDayStarted, Is.True);
    }

    [Test]
    public async Task MapDayFromGrpc_Booleans_WorkDayEnded_True()
    {
        var result = await MapViaGrpc(new PlanningPrDayModel { WorkDayEnded = true });
        Assert.That(result.WorkDayEnded, Is.True);
    }

    [Test]
    public async Task MapDayFromGrpc_Booleans_PlanHoursMatched_True()
    {
        var result = await MapViaGrpc(new PlanningPrDayModel { PlanHoursMatched = true });
        Assert.That(result.PlanHoursMatched, Is.True);
    }

    [Test]
    public async Task MapDayFromGrpc_Booleans_NettoHoursOverrideActive_True()
    {
        var result = await MapViaGrpc(new PlanningPrDayModel { NettoHoursOverrideActive = true });
        Assert.That(result.NettoHoursOverrideActive, Is.True);
    }

    // ---------------------------------------------------------------
    // 9. Numeric fields
    // ---------------------------------------------------------------

    [Test]
    public async Task MapDayFromGrpc_Numeric_PlanHours()
    {
        var result = await MapViaGrpc(new PlanningPrDayModel { PlanHours = 8 });
        Assert.That(result.PlanHours, Is.EqualTo(8));
    }

    [Test]
    public async Task MapDayFromGrpc_Numeric_NettoHoursOverride()
    {
        var result = await MapViaGrpc(new PlanningPrDayModel { NettoHoursOverride = 7.5 });
        Assert.That(result.NettoHoursOverride, Is.EqualTo(7.5));
    }

    [Test]
    public async Task MapDayFromGrpc_Numeric_PaidOutFlex()
    {
        var result = await MapViaGrpc(new PlanningPrDayModel { PaidOutFlex = 2.5 });
        Assert.That(result.PaidOutFlex, Is.EqualTo(2.5));
    }

    [Test]
    public async Task MapDayFromGrpc_Numeric_SumFlexStart()
    {
        var result = await MapViaGrpc(new PlanningPrDayModel { SumFlexStart = 10.0 });
        Assert.That(result.SumFlexStart, Is.EqualTo(10.0));
    }

    [Test]
    public async Task MapDayFromGrpc_Numeric_SumFlexEnd()
    {
        var result = await MapViaGrpc(new PlanningPrDayModel { SumFlexEnd = 12.5 });
        Assert.That(result.SumFlexEnd, Is.EqualTo(12.5));
    }

    [Test]
    public async Task MapDayFromGrpc_Numeric_ActualHours()
    {
        var result = await MapViaGrpc(new PlanningPrDayModel { ActualHours = 7.75 });
        Assert.That(result.ActualHours, Is.EqualTo(7.75));
    }

    [Test]
    public async Task MapDayFromGrpc_Numeric_Difference()
    {
        var result = await MapViaGrpc(new PlanningPrDayModel { Difference = -0.25 });
        Assert.That(result.Difference, Is.EqualTo(-0.25));
    }

    [Test]
    public async Task MapDayFromGrpc_Numeric_PauseMinutes()
    {
        var result = await MapViaGrpc(new PlanningPrDayModel { PauseMinutes = 30.0 });
        Assert.That(result.PauseMinutes, Is.EqualTo(30.0));
    }

    [Test]
    public async Task MapDayFromGrpc_Numeric_WeekDay()
    {
        var result = await MapViaGrpc(new PlanningPrDayModel { WeekDay = 3 });
        Assert.That(result.WeekDay, Is.EqualTo(3));
    }

    // ---------------------------------------------------------------
    // 10. Zero / default handling
    // ---------------------------------------------------------------

    [Test]
    public async Task MapDayFromGrpc_Defaults_EmptyProtoMapsToDefaults()
    {
        var result = await MapViaGrpc(new PlanningPrDayModel());

        // Identity defaults
        Assert.That(result.Id, Is.EqualTo(0));
        Assert.That(result.SiteId, Is.EqualTo(0));
        Assert.That(result.Date, Is.EqualTo(DateTime.MinValue));

        // Text defaults (proto default "" maps straight through)
        Assert.That(result.PlanText, Is.EqualTo(""));
        Assert.That(result.CommentOffice, Is.EqualTo(""));
        Assert.That(result.WorkerComment, Is.EqualTo(""));
        Assert.That(result.SiteName, Is.EqualTo(""));

        // Numeric defaults
        Assert.That(result.PlanHours, Is.EqualTo(0));
        Assert.That(result.NettoHoursOverride, Is.EqualTo(0));
        Assert.That(result.PaidOutFlex, Is.EqualTo(0));
        Assert.That(result.SumFlexStart, Is.EqualTo(0));
        Assert.That(result.SumFlexEnd, Is.EqualTo(0));
        Assert.That(result.ActualHours, Is.EqualTo(0));
        Assert.That(result.Difference, Is.EqualTo(0));
        Assert.That(result.PauseMinutes, Is.EqualTo(0));
        Assert.That(result.WeekDay, Is.EqualTo(0));

        // Boolean defaults
        Assert.That(result.OnVacation, Is.False);
        Assert.That(result.Sick, Is.False);
        Assert.That(result.OtherAllowedAbsence, Is.False);
        Assert.That(result.AbsenceWithoutPermission, Is.False);
        Assert.That(result.IsDoubleShift, Is.False);
        Assert.That(result.WorkDayStarted, Is.False);
        Assert.That(result.WorkDayEnded, Is.False);
        Assert.That(result.PlanHoursMatched, Is.False);
        Assert.That(result.NettoHoursOverrideActive, Is.False);

        // All shift/pause IDs should be null when proto sends 0
        Assert.That(result.Start1Id, Is.Null);
        Assert.That(result.Stop1Id, Is.Null);
        Assert.That(result.Pause1Id, Is.Null);
        Assert.That(result.Start2Id, Is.Null);
        Assert.That(result.Stop2Id, Is.Null);
        Assert.That(result.Pause2Id, Is.Null);
        Assert.That(result.Start3Id, Is.Null);
        Assert.That(result.Stop3Id, Is.Null);
        Assert.That(result.Pause3Id, Is.Null);
        Assert.That(result.Start4Id, Is.Null);
        Assert.That(result.Stop4Id, Is.Null);
        Assert.That(result.Pause4Id, Is.Null);
        Assert.That(result.Start5Id, Is.Null);
        Assert.That(result.Stop5Id, Is.Null);
        Assert.That(result.Pause5Id, Is.Null);

        // Break durations default to 0
        Assert.That(result.Break1Shift, Is.EqualTo(0));
        Assert.That(result.Break2Shift, Is.EqualTo(0));
        Assert.That(result.Break3Shift, Is.EqualTo(0));
        Assert.That(result.Break4Shift, Is.EqualTo(0));
        Assert.That(result.Break5Shift, Is.EqualTo(0));

        // Planned shifts default to 0
        Assert.That(result.PlannedStartOfShift1, Is.EqualTo(0));
        Assert.That(result.PlannedEndOfShift1, Is.EqualTo(0));
        Assert.That(result.PlannedBreakOfShift1, Is.EqualTo(0));

        // All primary shift timestamps should be null when proto sends ""
        Assert.That(result.Start1StartedAt, Is.Null);
        Assert.That(result.Stop1StoppedAt, Is.Null);
        Assert.That(result.Pause1StartedAt, Is.Null);
        Assert.That(result.Pause1StoppedAt, Is.Null);
        Assert.That(result.Start2StartedAt, Is.Null);
        Assert.That(result.Stop2StoppedAt, Is.Null);
        Assert.That(result.Start3StartedAt, Is.Null);
        Assert.That(result.Start4StartedAt, Is.Null);
        Assert.That(result.Start5StartedAt, Is.Null);

        // All detailed pause timestamps should be null when proto sends ""
        Assert.That(result.Pause10StartedAt, Is.Null);
        Assert.That(result.Pause10StoppedAt, Is.Null);
        Assert.That(result.Pause20StartedAt, Is.Null);
        Assert.That(result.Pause20StoppedAt, Is.Null);
        Assert.That(result.Pause100StartedAt, Is.Null);
        Assert.That(result.Pause200StartedAt, Is.Null);
    }

    // ---------------------------------------------------------------
    // 11. Full round-trip: all fields populated at once
    // ---------------------------------------------------------------

    [Test]
    public async Task MapDayFromGrpc_FullModel_AllFieldsMappedCorrectly()
    {
        var proto = new PlanningPrDayModel
        {
            Id = 77,
            Date = Timestamp.FromDateTime(
                DateTime.SpecifyKind(new DateTime(2026, 7, 4), DateTimeKind.Utc)),
            PlanText = "Full day",
            PlanHours = 8,
            PaidOutFlex = 1.5,
            SumFlexStart = 5.0,
            SumFlexEnd = 6.5,
            SdkSiteId = 42,
            Start1Id = 101,
            Stop1Id = 102,
            Pause1Id = 103,
            Start2Id = 201,
            Stop2Id = 202,
            Pause2Id = 203,
            Start3Id = 301,
            Stop3Id = 302,
            Pause3Id = 303,
            Start4Id = 401,
            Stop4Id = 402,
            Pause4Id = 403,
            Start5Id = 501,
            Stop5Id = 502,
            Pause5Id = 503,
            Break1Shift = 15,
            Break2Shift = 30,
            Break3Shift = 45,
            Break4Shift = 10,
            Break5Shift = 20,
            SiteName = "HQ",
            WeekDay = 5,
            ActualHours = 7.5,
            Difference = -0.5,
            PauseMinutes = 30,
            WorkDayStarted = true,
            WorkDayEnded = true,
            PlanHoursMatched = true,
            PlannedStartOfShift1 = 800,
            PlannedEndOfShift1 = 1600,
            PlannedBreakOfShift1 = 30,
            PlannedStartOfShift2 = 900,
            PlannedEndOfShift2 = 1700,
            PlannedBreakOfShift2 = 45,
            PlannedStartOfShift3 = 1000,
            PlannedEndOfShift3 = 1800,
            PlannedBreakOfShift3 = 60,
            PlannedStartOfShift4 = 1100,
            PlannedEndOfShift4 = 1900,
            PlannedBreakOfShift4 = 15,
            PlannedStartOfShift5 = 1200,
            PlannedEndOfShift5 = 2000,
            PlannedBreakOfShift5 = 20,
            IsDoubleShift = true,
            OnVacation = true,
            Sick = false,
            OtherAllowedAbsence = true,
            AbsenceWithoutPermission = false,
            CommentOffice = "Office note",
            WorkerComment = "Worker note",
            // Primary shift timestamps
            Start1StartedAt = "2026-07-04T07:00:00",
            Stop1StoppedAt = "2026-07-04T15:30:00",
            Pause1StartedAt = "2026-07-04T12:00:00",
            Pause1StoppedAt = "2026-07-04T12:30:00",
            Start2StartedAt = "2026-07-04T16:00:00",
            Stop2StoppedAt = "2026-07-04T23:00:00",
            Pause2StartedAt = "2026-07-04T20:00:00",
            Pause2StoppedAt = "2026-07-04T20:30:00",
            Start3StartedAt = "2026-07-04T08:00:00",
            Stop3StoppedAt = "2026-07-04T16:00:00",
            Pause3StartedAt = "2026-07-04T12:00:00",
            Pause3StoppedAt = "2026-07-04T12:45:00",
            Start4StartedAt = "2026-07-04T09:00:00",
            Stop4StoppedAt = "2026-07-04T17:00:00",
            Pause4StartedAt = "2026-07-04T13:00:00",
            Pause4StoppedAt = "2026-07-04T13:30:00",
            Start5StartedAt = "2026-07-04T10:00:00",
            Stop5StoppedAt = "2026-07-04T18:00:00",
            Pause5StartedAt = "2026-07-04T14:00:00",
            Pause5StoppedAt = "2026-07-04T14:15:00",
            // Detailed pause timestamps
            Pause10StartedAt = "2026-07-04T10:00:00",
            Pause10StoppedAt = "2026-07-04T10:15:00",
            Pause20StartedAt = "2026-07-04T14:00:00",
            Pause20StoppedAt = "2026-07-04T14:10:00",
            NettoHoursOverride = 6.5,
            NettoHoursOverrideActive = true,
        };

        var result = await MapViaGrpc(proto);

        // Identity
        Assert.That(result.Id, Is.EqualTo(77));
        Assert.That(result.SiteId, Is.EqualTo(42));
        Assert.That(result.Date, Is.EqualTo(new DateTime(2026, 7, 4)));

        // Text
        Assert.That(result.PlanText, Is.EqualTo("Full day"));
        Assert.That(result.CommentOffice, Is.EqualTo("Office note"));
        Assert.That(result.WorkerComment, Is.EqualTo("Worker note"));
        Assert.That(result.SiteName, Is.EqualTo("HQ"));

        // Shift IDs (non-zero)
        Assert.That(result.Start1Id, Is.EqualTo(101));
        Assert.That(result.Stop1Id, Is.EqualTo(102));
        Assert.That(result.Pause1Id, Is.EqualTo(103));
        Assert.That(result.Start2Id, Is.EqualTo(201));
        Assert.That(result.Stop2Id, Is.EqualTo(202));
        Assert.That(result.Pause2Id, Is.EqualTo(203));
        Assert.That(result.Start3Id, Is.EqualTo(301));
        Assert.That(result.Stop3Id, Is.EqualTo(302));
        Assert.That(result.Pause3Id, Is.EqualTo(303));
        Assert.That(result.Start4Id, Is.EqualTo(401));
        Assert.That(result.Stop4Id, Is.EqualTo(402));
        Assert.That(result.Pause4Id, Is.EqualTo(403));
        Assert.That(result.Start5Id, Is.EqualTo(501));
        Assert.That(result.Stop5Id, Is.EqualTo(502));
        Assert.That(result.Pause5Id, Is.EqualTo(503));

        // Breaks
        Assert.That(result.Break1Shift, Is.EqualTo(15));
        Assert.That(result.Break2Shift, Is.EqualTo(30));
        Assert.That(result.Break3Shift, Is.EqualTo(45));
        Assert.That(result.Break4Shift, Is.EqualTo(10));
        Assert.That(result.Break5Shift, Is.EqualTo(20));

        // Planned shifts
        Assert.That(result.PlannedStartOfShift1, Is.EqualTo(800));
        Assert.That(result.PlannedEndOfShift1, Is.EqualTo(1600));
        Assert.That(result.PlannedBreakOfShift1, Is.EqualTo(30));
        Assert.That(result.PlannedStartOfShift2, Is.EqualTo(900));
        Assert.That(result.PlannedEndOfShift2, Is.EqualTo(1700));
        Assert.That(result.PlannedBreakOfShift2, Is.EqualTo(45));
        Assert.That(result.PlannedStartOfShift3, Is.EqualTo(1000));
        Assert.That(result.PlannedEndOfShift3, Is.EqualTo(1800));
        Assert.That(result.PlannedBreakOfShift3, Is.EqualTo(60));
        Assert.That(result.PlannedStartOfShift4, Is.EqualTo(1100));
        Assert.That(result.PlannedEndOfShift4, Is.EqualTo(1900));
        Assert.That(result.PlannedBreakOfShift4, Is.EqualTo(15));
        Assert.That(result.PlannedStartOfShift5, Is.EqualTo(1200));
        Assert.That(result.PlannedEndOfShift5, Is.EqualTo(2000));
        Assert.That(result.PlannedBreakOfShift5, Is.EqualTo(20));

        // Booleans
        Assert.That(result.IsDoubleShift, Is.True);
        Assert.That(result.OnVacation, Is.True);
        Assert.That(result.Sick, Is.False);
        Assert.That(result.OtherAllowedAbsence, Is.True);
        Assert.That(result.AbsenceWithoutPermission, Is.False);
        Assert.That(result.WorkDayStarted, Is.True);
        Assert.That(result.WorkDayEnded, Is.True);
        Assert.That(result.PlanHoursMatched, Is.True);
        Assert.That(result.NettoHoursOverrideActive, Is.True);

        // Numerics
        Assert.That(result.PlanHours, Is.EqualTo(8));
        Assert.That(result.PaidOutFlex, Is.EqualTo(1.5));
        Assert.That(result.SumFlexStart, Is.EqualTo(5.0));
        Assert.That(result.SumFlexEnd, Is.EqualTo(6.5));
        Assert.That(result.ActualHours, Is.EqualTo(7.5));
        Assert.That(result.Difference, Is.EqualTo(-0.5));
        Assert.That(result.PauseMinutes, Is.EqualTo(30.0));
        Assert.That(result.WeekDay, Is.EqualTo(5));
        Assert.That(result.NettoHoursOverride, Is.EqualTo(6.5));

        // Primary shift timestamps
        Assert.That(result.Start1StartedAt, Is.EqualTo(new DateTime(2026, 7, 4, 7, 0, 0)));
        Assert.That(result.Stop1StoppedAt, Is.EqualTo(new DateTime(2026, 7, 4, 15, 30, 0)));
        Assert.That(result.Pause1StartedAt, Is.EqualTo(new DateTime(2026, 7, 4, 12, 0, 0)));
        Assert.That(result.Pause1StoppedAt, Is.EqualTo(new DateTime(2026, 7, 4, 12, 30, 0)));
        Assert.That(result.Start2StartedAt, Is.EqualTo(new DateTime(2026, 7, 4, 16, 0, 0)));
        Assert.That(result.Stop2StoppedAt, Is.EqualTo(new DateTime(2026, 7, 4, 23, 0, 0)));
        Assert.That(result.Pause2StartedAt, Is.EqualTo(new DateTime(2026, 7, 4, 20, 0, 0)));
        Assert.That(result.Pause2StoppedAt, Is.EqualTo(new DateTime(2026, 7, 4, 20, 30, 0)));
        Assert.That(result.Start3StartedAt, Is.EqualTo(new DateTime(2026, 7, 4, 8, 0, 0)));
        Assert.That(result.Stop3StoppedAt, Is.EqualTo(new DateTime(2026, 7, 4, 16, 0, 0)));
        Assert.That(result.Pause3StartedAt, Is.EqualTo(new DateTime(2026, 7, 4, 12, 0, 0)));
        Assert.That(result.Pause3StoppedAt, Is.EqualTo(new DateTime(2026, 7, 4, 12, 45, 0)));
        Assert.That(result.Start4StartedAt, Is.EqualTo(new DateTime(2026, 7, 4, 9, 0, 0)));
        Assert.That(result.Stop4StoppedAt, Is.EqualTo(new DateTime(2026, 7, 4, 17, 0, 0)));
        Assert.That(result.Pause4StartedAt, Is.EqualTo(new DateTime(2026, 7, 4, 13, 0, 0)));
        Assert.That(result.Pause4StoppedAt, Is.EqualTo(new DateTime(2026, 7, 4, 13, 30, 0)));
        Assert.That(result.Start5StartedAt, Is.EqualTo(new DateTime(2026, 7, 4, 10, 0, 0)));
        Assert.That(result.Stop5StoppedAt, Is.EqualTo(new DateTime(2026, 7, 4, 18, 0, 0)));
        Assert.That(result.Pause5StartedAt, Is.EqualTo(new DateTime(2026, 7, 4, 14, 0, 0)));
        Assert.That(result.Pause5StoppedAt, Is.EqualTo(new DateTime(2026, 7, 4, 14, 15, 0)));

        // Detailed pause timestamps (spot check)
        Assert.That(result.Pause10StartedAt, Is.EqualTo(new DateTime(2026, 7, 4, 10, 0, 0)));
        Assert.That(result.Pause10StoppedAt, Is.EqualTo(new DateTime(2026, 7, 4, 10, 15, 0)));
        Assert.That(result.Pause20StartedAt, Is.EqualTo(new DateTime(2026, 7, 4, 14, 0, 0)));
        Assert.That(result.Pause20StoppedAt, Is.EqualTo(new DateTime(2026, 7, 4, 14, 10, 0)));
    }

    // ---------------------------------------------------------------
    // 12. Null proto model -> empty domain model
    // ---------------------------------------------------------------

    [Test]
    public async Task MapDayFromGrpc_NullModel_ReturnsDefaultDomainModel()
    {
        // When request.Model is null, MapDayFromGrpc returns new()
        var request = new UpdatePlanningByCurrentUserRequest();
        // request.Model is null by default

        await _grpcService.UpdatePlanningByCurrentUser(
            request, TestServerCallContextFactory.Create());

        Assert.That(_captured, Is.Not.Null);
        Assert.That(_captured.Id, Is.EqualTo(0));
        Assert.That(_captured.Date, Is.EqualTo(DateTime.MinValue));
        Assert.That(_captured.SiteId, Is.EqualTo(0));
    }
}
