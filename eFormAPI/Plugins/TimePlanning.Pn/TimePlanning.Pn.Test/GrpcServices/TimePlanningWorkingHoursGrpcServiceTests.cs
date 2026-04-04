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
}
