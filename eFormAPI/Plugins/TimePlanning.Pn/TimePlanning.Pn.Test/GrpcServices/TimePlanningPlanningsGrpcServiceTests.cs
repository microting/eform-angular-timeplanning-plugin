using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;
using NSubstitute;
using NUnit.Framework;
using TimePlanning.Pn.Grpc;
using TimePlanning.Pn.Infrastructure.Models.Planning;
using TimePlanning.Pn.Services.GrpcServices;
using TimePlanning.Pn.Services.TimePlanningPlanningService;
using TimePlanning.Pn.Test.Helpers;
using OperationResult = Microting.eFormApi.BasePn.Infrastructure.Models.API.OperationResult;

namespace TimePlanning.Pn.Test.GrpcServices;

[TestFixture]
public class TimePlanningPlanningsGrpcServiceTests
{
    private ITimePlanningPlanningService _planningService;
    private TimePlanningPlanningsGrpcService _grpcService;

    [SetUp]
    public void SetUp()
    {
        _planningService = Substitute.For<ITimePlanningPlanningService>();
        _grpcService = new TimePlanningPlanningsGrpcService(_planningService);
    }

    [Test]
    public async Task GetPlanningsByUser_Success_MapsModelAndDays()
    {
        var planningModel = new TimePlanningPlanningModel
        {
            SiteId = 7,
            AvatarUrl = "https://example.com/avatar.png",
            CurrentWorkedHours = 30,
            CurrentWorkedMinutes = 45,
            PlannedHours = 40,
            PlannedMinutes = 0,
            PercentageCompleted = 77,
            SoftwareVersionIsValid = true,
            PlanningPrDayModels = new List<TimePlanningPlanningPrDayModel>
            {
                new()
                {
                    Id = 1,
                    Date = new DateTime(2026, 4, 3),
                    PlanText = "Normal",
                    PlanHours = 8,
                    ActualHours = 7.5,
                    Difference = -0.5,
                    SiteId = 7,
                    Start1Id = 101,
                    Stop1Id = 102,
                    Pause1Id = 103,
                    Start1StartedAt = new DateTime(2026, 4, 3, 8, 0, 0),
                    Stop1StoppedAt = new DateTime(2026, 4, 3, 16, 0, 0),
                    Break1Shift = 1,
                    WorkerComment = "Good day",
                }
            }
        };

        _planningService.IndexByCurrentUserName(
                Arg.Any<TimePlanningPlanningRequestModel>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>())
            .Returns(new OperationDataResult<TimePlanningPlanningModel>(true, planningModel));

        var request = new GetPlanningsByUserRequest
        {
            DateFrom = "2026-04-01",
            DateTo = "2026-04-07",
            Sort = "Date",
            IsSortDsc = false,
            Device = new Grpc.DeviceMetadata { SoftwareVersion = "1.0" }
        };

        var response = await _grpcService.GetPlanningsByUser(
            request, TestServerCallContextFactory.Create());

        Assert.That(response.Success, Is.True);
        Assert.That(response.Model, Is.Not.Null);
        Assert.That(response.Model.SiteId, Is.EqualTo(7));
        Assert.That(response.Model.CurrentWorkedHours, Is.EqualTo(30));
        Assert.That(response.Model.PlannedHours, Is.EqualTo(40));
        Assert.That(response.Model.PlanningPrDayModels, Has.Count.EqualTo(1));

        var day = response.Model.PlanningPrDayModels[0];
        Assert.That(day.Id, Is.EqualTo(1));
        Assert.That(day.PlanText, Is.EqualTo("Normal"));
        Assert.That(day.Start1Id, Is.EqualTo(101));
        Assert.That(day.Start1StartedAt, Is.EqualTo("2026-04-03T08:00:00"));
        Assert.That(day.Comment, Is.EqualTo("Good day"));
        Assert.That(day.Break1Shift, Is.EqualTo(1));
    }

    [Test]
    public async Task GetPlanningsByUser_Failure_ReturnsError()
    {
        _planningService.IndexByCurrentUserName(
                Arg.Any<TimePlanningPlanningRequestModel>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>())
            .Returns(new OperationDataResult<TimePlanningPlanningModel>(false, "Unauthorized"));

        var response = await _grpcService.GetPlanningsByUser(
            new GetPlanningsByUserRequest { DateFrom = "2026-04-01", DateTo = "2026-04-07" },
            TestServerCallContextFactory.Create());

        Assert.That(response.Success, Is.False);
        Assert.That(response.Message, Is.EqualTo("Unauthorized"));
    }

    [Test]
    public async Task UpdatePlanningByCurrentUser_Success_DelegatesToService()
    {
        _planningService.UpdateByCurrentUserNam(Arg.Any<TimePlanningPlanningPrDayModel>())
            .Returns(new OperationResult(true, "Updated"));

        var request = new UpdatePlanningByCurrentUserRequest
        {
            Model = new Grpc.PlanningPrDayModel
            {
                Id = 42,
                Date = Timestamp.FromDateTime(DateTime.SpecifyKind(new DateTime(2026, 4, 3), DateTimeKind.Utc)),
                PlanText = "Vacation",
                PlanHours = 0,
                SdkSiteId = 7,
                Comment = "On holiday",
                WorkerComment = "On holiday",
            }
        };

        var response = await _grpcService.UpdatePlanningByCurrentUser(
            request, TestServerCallContextFactory.Create());

        Assert.That(response.Success, Is.True);

        await _planningService.Received(1).UpdateByCurrentUserNam(
            Arg.Is<TimePlanningPlanningPrDayModel>(m =>
                m.Id == 42 &&
                m.PlanText == "Vacation" &&
                m.SiteId == 7 &&
                m.WorkerComment == "On holiday"));
    }

    [Test]
    public async Task GetPlanningsByUser_EmptyDaysList_ReturnsEmptyList()
    {
        var planningModel = new TimePlanningPlanningModel
        {
            SiteId = 7,
            PlanningPrDayModels = new List<TimePlanningPlanningPrDayModel>()
        };

        _planningService.IndexByCurrentUserName(
                Arg.Any<TimePlanningPlanningRequestModel>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>())
            .Returns(new OperationDataResult<TimePlanningPlanningModel>(true, planningModel));

        var response = await _grpcService.GetPlanningsByUser(
            new GetPlanningsByUserRequest { DateFrom = "2026-04-01", DateTo = "2026-04-07" },
            TestServerCallContextFactory.Create());

        Assert.That(response.Success, Is.True);
        Assert.That(response.Model.PlanningPrDayModels, Has.Count.EqualTo(0));
    }

    [Test]
    public async Task IndexPlannings_Success_ReturnsListWithNewFields()
    {
        var models = new List<TimePlanningPlanningModel>
        {
            new()
            {
                SiteId = 5,
                SiteName = "John",
                SoftwareVersion = "1.0",
                DeviceModel = "Pixel",
                DeviceManufacturer = "Google",
                AvatarUrl = "",
                PlannedHours = 8,
                PlannedMinutes = 0,
                CurrentWorkedHours = 4,
                CurrentWorkedMinutes = 30,
                PercentageCompleted = 56,
                SoftwareVersionIsValid = true,
                PlanningPrDayModels = new List<TimePlanningPlanningPrDayModel>
                {
                    new()
                    {
                        Id = 10,
                        Date = new DateTime(2026, 4, 3),
                        PlanText = "Work",
                        PlanHours = 8,
                        ActualHours = 4.5,
                        Difference = -3.5,
                        SiteId = 5,
                    }
                }
            }
        };

        _planningService.Index(Arg.Any<TimePlanningPlanningRequestModel>())
            .Returns(new OperationDataResult<List<TimePlanningPlanningModel>>(true, models));

        var request = new IndexPlanningsRequest
        {
            DateFrom = "2026-04-01",
            DateTo = "2026-04-07",
            Sort = "Date",
            IsSortDsc = false,
        };

        var response = await _grpcService.IndexPlannings(
            request, TestServerCallContextFactory.Create());

        Assert.That(response.Success, Is.True);
        Assert.That(response.Models, Has.Count.EqualTo(1));
        var m = response.Models[0];
        Assert.That(m.SiteId, Is.EqualTo(5));
        Assert.That(m.SiteName, Is.EqualTo("John"));
        Assert.That(m.SoftwareVersion, Is.EqualTo("1.0"));
        Assert.That(m.DeviceModel, Is.EqualTo("Pixel"));
        Assert.That(m.DeviceManufacturer, Is.EqualTo("Google"));
        Assert.That(m.PlanningPrDayModels, Has.Count.EqualTo(1));
        Assert.That(m.PlanningPrDayModels[0].PlanText, Is.EqualTo("Work"));
    }

    [Test]
    public async Task IndexPlannings_Failure_ReturnsError()
    {
        _planningService.Index(Arg.Any<TimePlanningPlanningRequestModel>())
            .Returns(new OperationDataResult<List<TimePlanningPlanningModel>>(false, "Not found"));

        var response = await _grpcService.IndexPlannings(
            new IndexPlanningsRequest { DateFrom = "2026-04-01", DateTo = "2026-04-07" },
            TestServerCallContextFactory.Create());

        Assert.That(response.Success, Is.False);
        Assert.That(response.Message, Is.EqualTo("Not found"));
    }

    [Test]
    public async Task UpdatePlanning_Success_DelegatesToService()
    {
        _planningService.Update(Arg.Any<int>(), Arg.Any<TimePlanningPlanningPrDayModel>())
            .Returns(new OperationResult(true, "Updated"));

        var request = new UpdatePlanningRequest
        {
            PlanningId = 99,
            Model = new Grpc.PlanningPrDayModel
            {
                Id = 42,
                Date = Timestamp.FromDateTime(DateTime.SpecifyKind(new DateTime(2026, 4, 3), DateTimeKind.Utc)),
                PlanText = "Meeting",
                PlanHours = 4,
                SdkSiteId = 5,
            }
        };

        var response = await _grpcService.UpdatePlanning(
            request, TestServerCallContextFactory.Create());

        Assert.That(response.Success, Is.True);

        await _planningService.Received(1).Update(
            99,
            Arg.Is<TimePlanningPlanningPrDayModel>(m =>
                m.Id == 42 &&
                m.PlanText == "Meeting" &&
                m.SiteId == 5));
    }

    [Test]
    public async Task UpdatePlanning_Failure_ReturnsError()
    {
        _planningService.Update(Arg.Any<int>(), Arg.Any<TimePlanningPlanningPrDayModel>())
            .Returns(new OperationResult(false, "Forbidden"));

        var request = new UpdatePlanningRequest
        {
            PlanningId = 1,
            Model = new Grpc.PlanningPrDayModel
            {
                Id = 1,
                Date = Timestamp.FromDateTime(DateTime.SpecifyKind(new DateTime(2026, 4, 3), DateTimeKind.Utc)),
            }
        };

        var response = await _grpcService.UpdatePlanning(
            request, TestServerCallContextFactory.Create());

        Assert.That(response.Success, Is.False);
        Assert.That(response.Message, Is.EqualTo("Forbidden"));
    }
}
