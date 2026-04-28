using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;
using NSubstitute;
using NUnit.Framework;
using TimePlanning.Pn.Grpc;
using TimePlanning.Pn.Services.AbsenceRequestService;
using TimePlanning.Pn.Services.GrpcServices;
using TimePlanning.Pn.Test.Helpers;
using OperationResult = Microting.eFormApi.BasePn.Infrastructure.Models.API.OperationResult;
using CsAbsenceRequestModel = TimePlanning.Pn.Infrastructure.Models.AbsenceRequest.AbsenceRequestModel;
using CsAbsenceRequestDayModel = TimePlanning.Pn.Infrastructure.Models.AbsenceRequest.AbsenceRequestDayModel;
using TimePlanning.Pn.Infrastructure.Models.AbsenceRequest;

namespace TimePlanning.Pn.Test.GrpcServices;

[TestFixture]
public class TimePlanningAbsenceRequestGrpcServiceTests
{
    private IAbsenceRequestService _absenceRequestService;
    private TimePlanningAbsenceRequestGrpcService _grpcService;

    [SetUp]
    public void SetUp()
    {
        _absenceRequestService = Substitute.For<IAbsenceRequestService>();
        _grpcService = new TimePlanningAbsenceRequestGrpcService(_absenceRequestService);
    }

    [Test]
    public async Task CreateAbsenceRequest_Success_ReturnsModel()
    {
        var model = new CsAbsenceRequestModel
        {
            Id = 42,
            RequestedBySdkSitId = 7,
            DateFrom = new DateTime(2026, 4, 10),
            DateTo = new DateTime(2026, 4, 12),
            Status = "Pending",
            RequestedAtUtc = new DateTime(2026, 4, 3, 10, 30, 0),
            DecidedAtUtc = null,
            DecidedBySdkSitId = null,
            RequestComment = "Vacation",
            DecisionComment = null,
            Days = new List<CsAbsenceRequestDayModel>
            {
                new() { Date = new DateTime(2026, 4, 10), MessageId = 1 },
                new() { Date = new DateTime(2026, 4, 11), MessageId = 2 },
            }
        };

        _absenceRequestService.CreateAsync(Arg.Any<AbsenceRequestCreateModel>())
            .Returns(new OperationDataResult<CsAbsenceRequestModel>(true, "OK", model));

        var request = new CreateAbsenceRequestRequest
        {
            RequestedBySdkSiteId = 7,
            DateFrom = "2026-04-10",
            DateTo = "2026-04-12",
            MessageId = 1,
            RequestComment = "Vacation",
            Device = new DeviceMetadata { SoftwareVersion = "1.0" }
        };

        var response = await _grpcService.CreateAbsenceRequest(
            request, TestServerCallContextFactory.Create());

        Assert.That(response.Success, Is.True);
        Assert.That(response.Message, Is.EqualTo("OK"));
        Assert.That(response.Model, Is.Not.Null);
        Assert.That(response.Model.Id, Is.EqualTo(42));
        Assert.That(response.Model.RequestedBySdkSiteId, Is.EqualTo(7));
        Assert.That(response.Model.DateFrom, Is.EqualTo("2026-04-10T00:00:00"));
        Assert.That(response.Model.DateTo, Is.EqualTo("2026-04-12T00:00:00"));
        Assert.That(response.Model.Status, Is.EqualTo("Pending"));
        Assert.That(response.Model.RequestedAtUtc, Is.EqualTo("2026-04-03T10:30:00"));
        Assert.That(response.Model.DecidedAtUtc, Is.Null);
        Assert.That(response.Model.DecidedBySdkSiteId, Is.EqualTo(0));
        Assert.That(response.Model.RequestComment, Is.EqualTo("Vacation"));
        Assert.That(response.Model.DecisionComment, Is.EqualTo(""));
        Assert.That(response.Model.Days, Has.Count.EqualTo(2));
        Assert.That(response.Model.Days[0].Date, Is.EqualTo("2026-04-10T00:00:00"));
        Assert.That(response.Model.Days[0].MessageId, Is.EqualTo(1));
        Assert.That(response.Model.Days[1].MessageId, Is.EqualTo(2));
    }

    [Test]
    public async Task CreateAbsenceRequest_Failure_ReturnsError()
    {
        _absenceRequestService.CreateAsync(Arg.Any<AbsenceRequestCreateModel>())
            .Returns(new OperationDataResult<CsAbsenceRequestModel>(false, "Overlap"));

        var request = new CreateAbsenceRequestRequest
        {
            RequestedBySdkSiteId = 7,
            DateFrom = "2026-04-10",
            DateTo = "2026-04-12",
        };

        var response = await _grpcService.CreateAbsenceRequest(
            request, TestServerCallContextFactory.Create());

        Assert.That(response.Success, Is.False);
        Assert.That(response.Message, Is.EqualTo("Overlap"));
        Assert.That(response.Model, Is.Null);
    }

    [Test]
    public async Task ApproveAbsenceRequest_DelegatesToService()
    {
        _absenceRequestService.ApproveAsync(Arg.Any<int>(), Arg.Any<AbsenceRequestDecisionModel>())
            .Returns(new OperationResult(true, "Approved"));

        var request = new AbsenceDecisionRequest
        {
            AbsenceRequestId = 42,
            ManagerSdkSitId = 5,
            DecisionComment = "Looks good",
        };

        var response = await _grpcService.ApproveAbsenceRequest(
            request, TestServerCallContextFactory.Create());

        Assert.That(response.Success, Is.True);
        Assert.That(response.Message, Is.EqualTo("Approved"));

        await _absenceRequestService.Received(1).ApproveAsync(
            42,
            Arg.Is<AbsenceRequestDecisionModel>(m =>
                m.ManagerSdkSitId == 5 &&
                m.DecisionComment == "Looks good"));
    }

    [Test]
    public async Task RejectAbsenceRequest_DelegatesToService()
    {
        _absenceRequestService.RejectAsync(Arg.Any<int>(), Arg.Any<AbsenceRequestDecisionModel>())
            .Returns(new OperationResult(true, "Rejected"));

        var request = new AbsenceDecisionRequest
        {
            AbsenceRequestId = 42,
            ManagerSdkSitId = 5,
            DecisionComment = "Not enough notice",
        };

        var response = await _grpcService.RejectAbsenceRequest(
            request, TestServerCallContextFactory.Create());

        Assert.That(response.Success, Is.True);
        Assert.That(response.Message, Is.EqualTo("Rejected"));

        await _absenceRequestService.Received(1).RejectAsync(
            42,
            Arg.Is<AbsenceRequestDecisionModel>(m =>
                m.ManagerSdkSitId == 5 &&
                m.DecisionComment == "Not enough notice"));
    }

    [Test]
    public async Task CancelAbsenceRequest_DelegatesToService()
    {
        _absenceRequestService.CancelAsync(Arg.Any<int>(), Arg.Any<int>())
            .Returns(new OperationResult(true, "Cancelled"));

        var request = new CancelAbsenceRequestRequest
        {
            AbsenceRequestId = 42,
            RequestedBySdkSiteId = 7,
        };

        var response = await _grpcService.CancelAbsenceRequest(
            request, TestServerCallContextFactory.Create());

        Assert.That(response.Success, Is.True);
        Assert.That(response.Message, Is.EqualTo("Cancelled"));

        await _absenceRequestService.Received(1).CancelAsync(42, 7);
    }

    [Test]
    public async Task GetAbsenceRequestInbox_ReturnsList()
    {
        var list = new List<CsAbsenceRequestModel>
        {
            new()
            {
                Id = 1, RequestedBySdkSitId = 7, DateFrom = new DateTime(2026, 4, 10),
                DateTo = new DateTime(2026, 4, 11), Status = "Pending",
                RequestedAtUtc = new DateTime(2026, 4, 3, 10, 0, 0),
                RequestComment = "Sick",
                Days = new List<CsAbsenceRequestDayModel>()
            },
            new()
            {
                Id = 2, RequestedBySdkSitId = 8, DateFrom = new DateTime(2026, 4, 15),
                DateTo = new DateTime(2026, 4, 16), Status = "Pending",
                RequestedAtUtc = new DateTime(2026, 4, 4, 9, 0, 0),
                RequestComment = "Personal",
                Days = new List<CsAbsenceRequestDayModel>()
            },
        };

        _absenceRequestService.GetInboxAsync()
            .Returns(new OperationDataResult<List<CsAbsenceRequestModel>>(true, list));

        // SdkSiteId is ignored by the handler — the service now resolves
        // the caller's site from the JWT.
        var request = new GetAbsenceRequestsRequest { SdkSiteId = 5 };

        var response = await _grpcService.GetAbsenceRequestInbox(
            request, TestServerCallContextFactory.Create());

        Assert.That(response.Success, Is.True);
        Assert.That(response.Models, Has.Count.EqualTo(2));
        Assert.That(response.Models[0].Id, Is.EqualTo(1));
        Assert.That(response.Models[0].RequestedBySdkSiteId, Is.EqualTo(7));
        Assert.That(response.Models[1].Id, Is.EqualTo(2));
        Assert.That(response.Models[1].RequestedBySdkSiteId, Is.EqualTo(8));
    }

    [Test]
    public async Task GetMyAbsenceRequests_ReturnsList()
    {
        var list = new List<CsAbsenceRequestModel>
        {
            new()
            {
                Id = 10, RequestedBySdkSitId = 7, DateFrom = new DateTime(2026, 5, 1),
                DateTo = new DateTime(2026, 5, 3), Status = "Approved",
                RequestedAtUtc = new DateTime(2026, 4, 20, 8, 0, 0),
                DecidedAtUtc = new DateTime(2026, 4, 21, 14, 0, 0),
                DecidedBySdkSitId = 5,
                Days = new List<CsAbsenceRequestDayModel>()
            },
        };

        _absenceRequestService.GetMineAsync(7)
            .Returns(new OperationDataResult<List<CsAbsenceRequestModel>>(true, list));

        var request = new GetAbsenceRequestsRequest { SdkSiteId = 7 };

        var response = await _grpcService.GetMyAbsenceRequests(
            request, TestServerCallContextFactory.Create());

        Assert.That(response.Success, Is.True);
        Assert.That(response.Models, Has.Count.EqualTo(1));
        Assert.That(response.Models[0].Id, Is.EqualTo(10));
        Assert.That(response.Models[0].Status, Is.EqualTo("Approved"));
    }
}
