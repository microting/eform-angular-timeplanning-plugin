using System;
using System.Threading.Tasks;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;
using NSubstitute;
using NUnit.Framework;
using TimePlanning.Pn.Grpc;
using TimePlanning.Pn.Infrastructure.Models.WorkingHours.Index;
using TimePlanning.Pn.Infrastructure.Models.WorkingHours.UpdateCreate;
using TimePlanning.Pn.Services.GrpcServices;
using TimePlanning.Pn.Services.TimePlanningWorkingHoursService;
using TimePlanning.Pn.Test.Helpers;
using OperationResult = Microting.eFormApi.BasePn.Infrastructure.Models.API.OperationResult;

namespace TimePlanning.Pn.Test.GrpcServices;

[TestFixture]
public class TimePlanningWorkingHoursGrpcServiceTests
{
    private ITimePlanningWorkingHoursService _whService;
    private TimePlanningWorkingHoursGrpcService _grpcService;

    [SetUp]
    public void SetUp()
    {
        _whService = Substitute.For<ITimePlanningWorkingHoursService>();
        _grpcService = new TimePlanningWorkingHoursGrpcService(_whService);
    }

    [Test]
    public async Task ReadWorkingHours_Success_MapsAllFields()
    {
        var model = new TimePlanningWorkingHoursModel
        {
            Id = 42,
            SdkSiteId = 7,
            Date = new DateTime(2026, 4, 3),
            PlanText = "Normal",
            PlanHours = 8.0,
            Shift1Start = 101,
            Shift1Stop = 102,
            Shift1Pause = 103,
            Start1StartedAt = new DateTime(2026, 4, 3, 8, 0, 0),
            Stop1StoppedAt = new DateTime(2026, 4, 3, 16, 0, 0),
            Pause1StartedAt = new DateTime(2026, 4, 3, 12, 0, 0),
            Pause1StoppedAt = new DateTime(2026, 4, 3, 12, 30, 0),
            NettoHours = 7.5,
            FlexHours = -0.5,
            SumFlexStart = 10.0,
            SumFlexEnd = 9.5,
            PaidOutFlex = "5",
            CommentWorker = "All good",
            Message = 1,
            Shift1PauseNumber = 1,
        };

        _whService.Read(7, Arg.Any<DateTime>(), "device-token")
            .Returns(new OperationDataResult<TimePlanningWorkingHoursModel>(true, model));

        var request = new ReadWorkingHoursRequest
        {
            Token = "device-token",
            SdkSiteId = 7,
            Date = "2026-04-03"
        };

        var response = await _grpcService.ReadWorkingHours(
            request, TestServerCallContextFactory.Create());

        Assert.That(response.Success, Is.True);
        Assert.That(response.Model, Is.Not.Null);
        Assert.That(response.Model.Id, Is.EqualTo(42));
        Assert.That(response.Model.SdkSiteId, Is.EqualTo(7));
        Assert.That(response.Model.Start1Id, Is.EqualTo(101));
        Assert.That(response.Model.Stop1Id, Is.EqualTo(102));
        Assert.That(response.Model.Pause1Id, Is.EqualTo(103));
        Assert.That(response.Model.Start1StartedAt, Is.EqualTo("2026-04-03T08:00:00"));
        Assert.That(response.Model.Stop1StoppedAt, Is.EqualTo("2026-04-03T16:00:00"));
        Assert.That(response.Model.NetWorkingHours, Is.EqualTo(7.5));
        Assert.That(response.Model.FlexHours, Is.EqualTo(-0.5));
        Assert.That(response.Model.PaidOutFlex, Is.EqualTo(5.0));
        Assert.That(response.Model.Comment, Is.EqualTo("All good"));
    }

    [Test]
    public async Task ReadWorkingHours_Failure_ReturnsError()
    {
        _whService.Read(Arg.Any<int>(), Arg.Any<DateTime>(), Arg.Any<string>())
            .Returns(new OperationDataResult<TimePlanningWorkingHoursModel>(false, "Not found"));

        var request = new ReadWorkingHoursRequest
        {
            Token = "tok",
            SdkSiteId = 1,
            Date = "2026-04-03"
        };

        var response = await _grpcService.ReadWorkingHours(
            request, TestServerCallContextFactory.Create());

        Assert.That(response.Success, Is.False);
        Assert.That(response.Model, Is.Null);
    }

    [Test]
    public async Task UpdateWorkingHours_Success_DelegatesToService()
    {
        _whService.UpdateWorkingHour(Arg.Any<int?>(), Arg.Any<TimePlanningWorkingHoursUpdateModel>(), Arg.Any<string>())
            .Returns(new OperationResult(true, "Updated"));

        var request = new UpdateWorkingHoursRequest
        {
            Token = "device-token",
            SdkSiteId = 7,
            Date = "2026-04-03",
            Model = new Grpc.WorkingHoursModel
            {
                Start1Id = 101,
                Start1StartedAt = "2026-04-03T08:00:00",
                Comment = "Updated comment",
            },
            Device = new Grpc.DeviceMetadata
            {
                SoftwareVersion = "1.0.0",
                DeviceModel = "Pixel",
                Manufacturer = "Google",
                OsVersion = "Android 14",
            }
        };

        var response = await _grpcService.UpdateWorkingHours(
            request, TestServerCallContextFactory.Create());

        Assert.That(response.Success, Is.True);

        await _whService.Received(1).UpdateWorkingHour(
            7,
            Arg.Is<TimePlanningWorkingHoursUpdateModel>(m =>
                m.Shift1Start == 101 &&
                m.Start1StartedAt == "2026-04-03T08:00:00" &&
                m.CommentWorker == "Updated comment" &&
                m.SoftwareVersion == "1.0.0"),
            "device-token");
    }

    [Test]
    public async Task CalculateHoursSummary_Success_MapsFields()
    {
        var summaryModel = new TimePlanningHoursSummaryModel
        {
            TotalPlanHours = 40.0,
            TotalNettoHours = 38.5,
            Difference = -1.5,
        };

        _whService.CalculateHoursSummary(
                Arg.Any<DateTime>(), Arg.Any<DateTime>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>())
            .Returns(new OperationDataResult<TimePlanningHoursSummaryModel>(true, summaryModel));

        var request = new CalculateHoursSummaryRequest
        {
            StartDate = "2026-04-01",
            EndDate = "2026-04-07",
            Device = new Grpc.DeviceMetadata { SoftwareVersion = "1.0" }
        };

        var response = await _grpcService.CalculateHoursSummary(
            request, TestServerCallContextFactory.Create());

        Assert.That(response.Success, Is.True);
        Assert.That(response.Model, Is.Not.Null);
        Assert.That(response.Model.TotalPlannedHours, Is.EqualTo(40.0));
        Assert.That(response.Model.TotalWorkedHours, Is.EqualTo(38.5));
        Assert.That(response.Model.TotalFlexHours, Is.EqualTo(-1.5));
    }

    [Test]
    public async Task ReadWorkingHours_NullDateTimeFields_MappedAsEmptyStrings()
    {
        var model = new TimePlanningWorkingHoursModel
        {
            Id = 1,
            SdkSiteId = 1,
            Date = new DateTime(2026, 4, 3),
            // All DateTime? fields are null by default
        };

        _whService.Read(Arg.Any<int>(), Arg.Any<DateTime>(), Arg.Any<string>())
            .Returns(new OperationDataResult<TimePlanningWorkingHoursModel>(true, model));

        var response = await _grpcService.ReadWorkingHours(
            new ReadWorkingHoursRequest { Token = "t", SdkSiteId = 1, Date = "2026-04-03" },
            TestServerCallContextFactory.Create());

        Assert.That(response.Model.Start1StartedAt, Is.EqualTo(""));
        Assert.That(response.Model.Stop1StoppedAt, Is.EqualTo(""));
        Assert.That(response.Model.Pause10StartedAt, Is.EqualTo(""));
    }

    [Test]
    public async Task UpdateWorkingHours_PersonalMode_PassesNullSdkSiteIdAndNullToken()
    {
        // Personal mode (empty token) routes to the 1-param UpdateWorkingHour
        // overload (JWT-based user lookup) per 0ad1af89.
        _whService.UpdateWorkingHour(Arg.Any<TimePlanningWorkingHoursUpdateModel>())
            .Returns(new OperationResult(true, "Updated"));

        var request = new UpdateWorkingHoursRequest
        {
            SdkSiteId = 0,
            Token = "",
            Date = "2026-04-26",
            Model = new Grpc.WorkingHoursModel
            {
                Start1Id = 101,
                Comment = "Personal update",
            }
        };

        var response = await _grpcService.UpdateWorkingHours(
            request, TestServerCallContextFactory.Create());

        Assert.That(response.Success, Is.True);

        await _whService.Received(1).UpdateWorkingHour(
            Arg.Any<TimePlanningWorkingHoursUpdateModel>());
        await _whService.DidNotReceive().UpdateWorkingHour(
            Arg.Any<int?>(),
            Arg.Any<TimePlanningWorkingHoursUpdateModel>(),
            Arg.Any<string>());
    }

    [Test]
    public async Task UpdateWorkingHours_KioskMode_PassesSdkSiteIdAndToken()
    {
        _whService.UpdateWorkingHour(Arg.Any<int?>(), Arg.Any<TimePlanningWorkingHoursUpdateModel>(), Arg.Any<string>())
            .Returns(new OperationResult(true, "Updated"));

        var request = new UpdateWorkingHoursRequest
        {
            SdkSiteId = 42,
            Token = "device-abc-123",
            Date = "2026-04-26",
            Model = new Grpc.WorkingHoursModel
            {
                Start1Id = 101,
                Comment = "Kiosk update",
            }
        };

        var response = await _grpcService.UpdateWorkingHours(
            request, TestServerCallContextFactory.Create());

        Assert.That(response.Success, Is.True);

        await _whService.Received(1).UpdateWorkingHour(
            42,
            Arg.Any<TimePlanningWorkingHoursUpdateModel>(),
            "device-abc-123");
    }

    [Test]
    public async Task UpdateWorkingHours_PersonalMode_MapsModelFieldsCorrectly()
    {
        // Personal mode (empty token) routes to the 1-param UpdateWorkingHour overload.
        _whService.UpdateWorkingHour(Arg.Any<TimePlanningWorkingHoursUpdateModel>())
            .Returns(new OperationResult(true, "Updated"));

        var request = new UpdateWorkingHoursRequest
        {
            SdkSiteId = 0,
            Token = "",
            Date = "2026-04-26",
            Model = new Grpc.WorkingHoursModel
            {
                Start1Id = 101,
                Start1StartedAt = "2026-04-26T08:00:00",
                Stop1StoppedAt = "2026-04-26T16:00:00",
                Comment = "Morning shift",
            }
        };

        var response = await _grpcService.UpdateWorkingHours(
            request, TestServerCallContextFactory.Create());

        Assert.That(response.Success, Is.True);

        await _whService.Received(1).UpdateWorkingHour(
            Arg.Is<TimePlanningWorkingHoursUpdateModel>(m =>
                m.Date == DateTime.Parse("2026-04-26") &&
                m.Shift1Start == 101 &&
                m.Start1StartedAt == "2026-04-26T08:00:00" &&
                m.Stop1StoppedAt == "2026-04-26T16:00:00" &&
                m.CommentWorker == "Morning shift"));
    }

    [Test]
    public async Task UpdateWorkingHours_KioskMode_MapsDeviceMetadataToModel()
    {
        _whService.UpdateWorkingHour(Arg.Any<int?>(), Arg.Any<TimePlanningWorkingHoursUpdateModel>(), Arg.Any<string>())
            .Returns(new OperationResult(true, "Updated"));

        var request = new UpdateWorkingHoursRequest
        {
            SdkSiteId = 42,
            Token = "device-abc-123",
            Date = "2026-04-26",
            Model = new Grpc.WorkingHoursModel
            {
                Start1Id = 101,
                Comment = "Kiosk shift",
            },
            Device = new Grpc.DeviceMetadata
            {
                SoftwareVersion = "4.0.7",
                DeviceModel = "Pixel 8",
                Manufacturer = "Google",
                OsVersion = "Android 15",
            }
        };

        var response = await _grpcService.UpdateWorkingHours(
            request, TestServerCallContextFactory.Create());

        Assert.That(response.Success, Is.True);

        await _whService.Received(1).UpdateWorkingHour(
            Arg.Any<int?>(),
            Arg.Is<TimePlanningWorkingHoursUpdateModel>(m =>
                m.SoftwareVersion == "4.0.7" &&
                m.Model == "Pixel 8" &&
                m.Manufacturer == "Google" &&
                m.OsVersion == "Android 15"),
            Arg.Any<string>());
    }

    [Test]
    public async Task UpdateWorkingHours_PersonalMode_ServiceFailure_ReturnsErrorMessage()
    {
        // Personal mode (empty token) routes to the 1-param UpdateWorkingHour overload.
        _whService.UpdateWorkingHour(Arg.Any<TimePlanningWorkingHoursUpdateModel>())
            .Returns(new OperationResult(false, "User not found"));

        var request = new UpdateWorkingHoursRequest
        {
            SdkSiteId = 0,
            Token = "",
            Date = "2026-04-26",
            Model = new Grpc.WorkingHoursModel
            {
                Start1Id = 101,
                Comment = "Failing request",
            }
        };

        var response = await _grpcService.UpdateWorkingHours(
            request, TestServerCallContextFactory.Create());

        Assert.That(response.Success, Is.False);
        Assert.That(response.Message, Is.EqualTo("User not found"));
    }

    [Test]
    public async Task UpdateWorkingHours_KioskMode_InvalidToken_ReturnsTokenNotFound()
    {
        _whService.UpdateWorkingHour(Arg.Any<int?>(), Arg.Any<TimePlanningWorkingHoursUpdateModel>(), Arg.Any<string>())
            .Returns(new OperationResult(false, "Token not found"));

        var request = new UpdateWorkingHoursRequest
        {
            SdkSiteId = 42,
            Token = "invalid-token",
            Date = "2026-04-26",
            Model = new Grpc.WorkingHoursModel
            {
                Start1Id = 101,
            }
        };

        var response = await _grpcService.UpdateWorkingHours(
            request, TestServerCallContextFactory.Create());

        Assert.That(response.Success, Is.False);
        Assert.That(response.Message, Is.EqualTo("Token not found"));
    }

    [Test]
    public async Task UpdateWorkingHours_EmptyTokenWithNonZeroSdkSiteId_RoutesToPersonalMode()
    {
        // After 0ad1af89, empty/whitespace token is the sole personal-mode trigger.
        // SdkSiteId is ignored when token is empty (user is resolved via JWT).
        _whService.UpdateWorkingHour(Arg.Any<TimePlanningWorkingHoursUpdateModel>())
            .Returns(new OperationResult(true, "Updated"));

        var request = new UpdateWorkingHoursRequest
        {
            SdkSiteId = 7,
            Token = "",
            Date = "2026-04-26",
            Model = new Grpc.WorkingHoursModel
            {
                Start1Id = 101,
            }
        };

        var response = await _grpcService.UpdateWorkingHours(
            request, TestServerCallContextFactory.Create());

        Assert.That(response.Success, Is.True);

        // Personal-mode 1-param overload is called; 3-param kiosk overload is not.
        await _whService.Received(1).UpdateWorkingHour(
            Arg.Any<TimePlanningWorkingHoursUpdateModel>());
        await _whService.DidNotReceive().UpdateWorkingHour(
            Arg.Any<int?>(),
            Arg.Any<TimePlanningWorkingHoursUpdateModel>(),
            Arg.Any<string>());
    }

    [Test]
    public async Task UpdateWorkingHours_NonEmptyTokenWithZeroSdkSiteId_TokenPassesThrough()
    {
        _whService.UpdateWorkingHour(Arg.Any<int?>(), Arg.Any<TimePlanningWorkingHoursUpdateModel>(), Arg.Any<string>())
            .Returns(new OperationResult(true, "Updated"));

        var request = new UpdateWorkingHoursRequest
        {
            SdkSiteId = 0,
            Token = "abc",
            Date = "2026-04-26",
            Model = new Grpc.WorkingHoursModel
            {
                Start1Id = 101,
            }
        };

        var response = await _grpcService.UpdateWorkingHours(
            request, TestServerCallContextFactory.Create());

        Assert.That(response.Success, Is.True);

        await _whService.Received(1).UpdateWorkingHour(
            null,
            Arg.Any<TimePlanningWorkingHoursUpdateModel>(),
            "abc");
    }

    // ── Routing tests: personal vs kiosk mode dispatch ──

    [Test]
    public async Task ReadWorkingHours_PersonalMode_EmptyToken_CallsReadFullByCurrentUser()
    {
        var model = new TimePlanningWorkingHoursModel
        {
            Id = 1,
            SdkSiteId = 7,
            Date = new DateTime(2026, 4, 3),
        };

        _whService.ReadFullByCurrentUser(
                Arg.Any<DateTime>(),
                Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<string?>())
            .Returns(new OperationDataResult<TimePlanningWorkingHoursModel>(true, model));

        var request = new ReadWorkingHoursRequest
        {
            Token = "",
            SdkSiteId = 7,
            Date = "2026-04-03"
        };

        var response = await _grpcService.ReadWorkingHours(
            request, TestServerCallContextFactory.Create());

        Assert.That(response.Success, Is.True);

        await _whService.Received(1).ReadFullByCurrentUser(
            Arg.Any<DateTime>(),
            Arg.Any<string?>(), Arg.Any<string?>(),
            Arg.Any<string?>(), Arg.Any<string?>());

        await _whService.DidNotReceive().Read(
            Arg.Any<int>(), Arg.Any<DateTime>(), Arg.Any<string>());
    }

    [Test]
    public async Task ReadWorkingHours_KioskMode_NonEmptyToken_CallsRead()
    {
        var model = new TimePlanningWorkingHoursModel
        {
            Id = 1,
            SdkSiteId = 7,
            Date = new DateTime(2026, 4, 3),
        };

        _whService.Read(7, Arg.Any<DateTime>(), "abc123device")
            .Returns(new OperationDataResult<TimePlanningWorkingHoursModel>(true, model));

        var request = new ReadWorkingHoursRequest
        {
            Token = "abc123device",
            SdkSiteId = 7,
            Date = "2026-04-03"
        };

        var response = await _grpcService.ReadWorkingHours(
            request, TestServerCallContextFactory.Create());

        Assert.That(response.Success, Is.True);

        await _whService.Received(1).Read(7, Arg.Any<DateTime>(), "abc123device");

        await _whService.DidNotReceive().ReadFullByCurrentUser(
            Arg.Any<DateTime>(),
            Arg.Any<string?>(), Arg.Any<string?>(),
            Arg.Any<string?>(), Arg.Any<string?>());
    }

    [Test]
    public async Task UpdateWorkingHours_PersonalMode_EmptyToken_Calls1ParamUpdateWorkingHour()
    {
        _whService.UpdateWorkingHour(Arg.Any<TimePlanningWorkingHoursUpdateModel>())
            .Returns(new OperationResult(true, "Updated"));

        var request = new UpdateWorkingHoursRequest
        {
            Token = "",
            SdkSiteId = 0,
            Date = "2026-04-03",
            Model = new Grpc.WorkingHoursModel
            {
                Start1Id = 101,
                Comment = "Personal mode update",
            }
        };

        var response = await _grpcService.UpdateWorkingHours(
            request, TestServerCallContextFactory.Create());

        Assert.That(response.Success, Is.True);

        await _whService.Received(1).UpdateWorkingHour(
            Arg.Any<TimePlanningWorkingHoursUpdateModel>());

        await _whService.DidNotReceive().UpdateWorkingHour(
            Arg.Any<int?>(),
            Arg.Any<TimePlanningWorkingHoursUpdateModel>(),
            Arg.Any<string>());
    }

    [Test]
    public async Task UpdateWorkingHours_KioskMode_NonEmptyToken_Calls3ParamUpdateWorkingHour()
    {
        _whService.UpdateWorkingHour(
                Arg.Any<int?>(),
                Arg.Any<TimePlanningWorkingHoursUpdateModel>(),
                Arg.Any<string>())
            .Returns(new OperationResult(true, "Updated"));

        var request = new UpdateWorkingHoursRequest
        {
            Token = "abc123device",
            SdkSiteId = 42,
            Date = "2026-04-03",
            Model = new Grpc.WorkingHoursModel
            {
                Start1Id = 101,
                Comment = "Kiosk mode update",
            }
        };

        var response = await _grpcService.UpdateWorkingHours(
            request, TestServerCallContextFactory.Create());

        Assert.That(response.Success, Is.True);

        await _whService.Received(1).UpdateWorkingHour(
            Arg.Any<int?>(),
            Arg.Any<TimePlanningWorkingHoursUpdateModel>(),
            "abc123device");

        await _whService.DidNotReceive().UpdateWorkingHour(
            Arg.Any<TimePlanningWorkingHoursUpdateModel>());
    }
}
