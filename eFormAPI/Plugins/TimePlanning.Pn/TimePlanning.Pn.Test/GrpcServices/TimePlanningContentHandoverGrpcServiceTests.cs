using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;
using NSubstitute;
using NUnit.Framework;
using TimePlanning.Pn.Grpc;
using TimePlanning.Pn.Infrastructure.Models.ContentHandover;
using TimePlanning.Pn.Services.ContentHandoverService;
using TimePlanning.Pn.Services.GrpcServices;
using TimePlanning.Pn.Test.Helpers;
using OperationResult = Microting.eFormApi.BasePn.Infrastructure.Models.API.OperationResult;
using CsContentHandoverRequestModel = TimePlanning.Pn.Infrastructure.Models.ContentHandover.ContentHandoverRequestModel;

namespace TimePlanning.Pn.Test.GrpcServices;

[TestFixture]
public class TimePlanningContentHandoverGrpcServiceTests
{
    private IContentHandoverService _service;
    private TimePlanningContentHandoverGrpcService _grpcService;

    [SetUp]
    public void SetUp()
    {
        _service = Substitute.For<IContentHandoverService>();
        _grpcService = new TimePlanningContentHandoverGrpcService(_service);
    }

    [Test]
    public async Task CreateContentHandover_Success_ReturnsModel()
    {
        var csModel = new CsContentHandoverRequestModel
        {
            Id = 42,
            FromSdkSitId = 10,
            ToSdkSitId = 20,
            Date = new DateTime(2026, 4, 3),
            FromPlanRegistrationId = 100,
            ToPlanRegistrationId = 200,
            Status = "Pending",
            RequestedAtUtc = new DateTime(2026, 4, 3, 12, 0, 0),
            RespondedAtUtc = new DateTime(2026, 4, 3, 13, 0, 0),
            RequestComment = "Please take over",
            DecisionComment = "OK"
        };

        _service.CreateAsync(
                Arg.Any<int>(),
                Arg.Any<ContentHandoverRequestCreateModel>())
            .Returns(new OperationDataResult<CsContentHandoverRequestModel>(true, csModel));

        var request = new CreateContentHandoverRequest
        {
            FromPlanRegistrationId = 100,
            ToSdkSiteId = 20,
            RequestComment = "Please take over",
            Device = new DeviceMetadata { SoftwareVersion = "1.0" }
        };

        var response = await _grpcService.CreateContentHandover(
            request, TestServerCallContextFactory.Create());

        Assert.That(response.Success, Is.True);
        Assert.That(response.Model, Is.Not.Null);
        Assert.That(response.Model.Id, Is.EqualTo(42));
        Assert.That(response.Model.FromSdkSiteId, Is.EqualTo(10));
        Assert.That(response.Model.ToSdkSiteId, Is.EqualTo(20));
        Assert.That(response.Model.Date, Is.EqualTo("2026-04-03T00:00:00"));
        Assert.That(response.Model.FromPlanRegistrationId, Is.EqualTo(100));
        Assert.That(response.Model.ToPlanRegistrationId, Is.EqualTo(200));
        Assert.That(response.Model.Status, Is.EqualTo("Pending"));
        Assert.That(response.Model.RequestedAtUtc, Is.EqualTo("2026-04-03T12:00:00"));
        Assert.That(response.Model.RespondedAtUtc, Is.EqualTo("2026-04-03T13:00:00"));
        Assert.That(response.Model.RequestComment, Is.EqualTo("Please take over"));
        Assert.That(response.Model.DecisionComment, Is.EqualTo("OK"));
    }

    [Test]
    public async Task CreateContentHandover_Failure_ReturnsError()
    {
        _service.CreateAsync(
                Arg.Any<int>(),
                Arg.Any<ContentHandoverRequestCreateModel>())
            .Returns(new OperationDataResult<CsContentHandoverRequestModel>(false, "Something went wrong"));

        var request = new CreateContentHandoverRequest
        {
            FromPlanRegistrationId = 100,
            ToSdkSiteId = 20,
            RequestComment = "Please take over"
        };

        var response = await _grpcService.CreateContentHandover(
            request, TestServerCallContextFactory.Create());

        Assert.That(response.Success, Is.False);
        Assert.That(response.Message, Is.EqualTo("Something went wrong"));
        Assert.That(response.Model, Is.Null);
    }

    [Test]
    public async Task AcceptContentHandover_DelegatesToService()
    {
        _service.AcceptAsync(
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<ContentHandoverDecisionModel>())
            .Returns(new OperationResult(true, "Accepted"));

        var request = new ContentHandoverDecisionRequest
        {
            RequestId = 42,
            CurrentSdkSiteId = 20,
            DecisionComment = "Looks good"
        };

        var response = await _grpcService.AcceptContentHandover(
            request, TestServerCallContextFactory.Create());

        Assert.That(response.Success, Is.True);
        Assert.That(response.Message, Is.EqualTo("Accepted"));

        await _service.Received(1).AcceptAsync(
            42,
            20,
            Arg.Is<ContentHandoverDecisionModel>(m => m.DecisionComment == "Looks good"));
    }

    [Test]
    public async Task RejectContentHandover_DelegatesToService()
    {
        _service.RejectAsync(
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<ContentHandoverDecisionModel>())
            .Returns(new OperationResult(true, "Rejected"));

        var request = new ContentHandoverDecisionRequest
        {
            RequestId = 42,
            CurrentSdkSiteId = 20,
            DecisionComment = "Not available"
        };

        var response = await _grpcService.RejectContentHandover(
            request, TestServerCallContextFactory.Create());

        Assert.That(response.Success, Is.True);
        Assert.That(response.Message, Is.EqualTo("Rejected"));

        await _service.Received(1).RejectAsync(
            42,
            20,
            Arg.Is<ContentHandoverDecisionModel>(m => m.DecisionComment == "Not available"));
    }

    [Test]
    public async Task CancelContentHandover_DelegatesToService()
    {
        _service.CancelAsync(Arg.Any<int>(), Arg.Any<int>())
            .Returns(new OperationResult(true, "Cancelled"));

        var request = new CancelContentHandoverRequest
        {
            RequestId = 42,
            CurrentSdkSiteId = 10,
        };

        var response = await _grpcService.CancelContentHandover(
            request, TestServerCallContextFactory.Create());

        Assert.That(response.Success, Is.True);
        Assert.That(response.Message, Is.EqualTo("Cancelled"));

        await _service.Received(1).CancelAsync(42, 10);
    }

    [Test]
    public async Task GetContentHandoverInbox_ReturnsList()
    {
        var items = new List<CsContentHandoverRequestModel>
        {
            new()
            {
                Id = 1, FromSdkSitId = 10, ToSdkSitId = 20, Date = new DateTime(2026, 4, 1),
                Status = "Pending", RequestedAtUtc = new DateTime(2026, 4, 1, 8, 0, 0)
            },
            new()
            {
                Id = 2, FromSdkSitId = 30, ToSdkSitId = 20, Date = new DateTime(2026, 4, 2),
                Status = "Pending", RequestedAtUtc = new DateTime(2026, 4, 2, 9, 0, 0)
            }
        };

        _service.GetInboxAsync(20)
            .Returns(new OperationDataResult<List<CsContentHandoverRequestModel>>(true, items));

        var request = new GetContentHandoverRequestsRequest { SdkSiteId = 20 };

        var response = await _grpcService.GetContentHandoverInbox(
            request, TestServerCallContextFactory.Create());

        Assert.That(response.Success, Is.True);
        Assert.That(response.Models, Has.Count.EqualTo(2));
        Assert.That(response.Models[0].Id, Is.EqualTo(1));
        Assert.That(response.Models[1].Id, Is.EqualTo(2));
    }

    [Test]
    public async Task GetMyContentHandovers_ReturnsList()
    {
        var items = new List<CsContentHandoverRequestModel>
        {
            new()
            {
                Id = 5, FromSdkSitId = 10, ToSdkSitId = 20, Date = new DateTime(2026, 4, 3),
                Status = "Accepted", RequestedAtUtc = new DateTime(2026, 4, 3, 10, 0, 0),
                RespondedAtUtc = new DateTime(2026, 4, 3, 11, 0, 0)
            }
        };

        _service.GetMineAsync(10)
            .Returns(new OperationDataResult<List<CsContentHandoverRequestModel>>(true, items));

        var request = new GetContentHandoverRequestsRequest { SdkSiteId = 10 };

        var response = await _grpcService.GetMyContentHandovers(
            request, TestServerCallContextFactory.Create());

        Assert.That(response.Success, Is.True);
        Assert.That(response.Models, Has.Count.EqualTo(1));
        Assert.That(response.Models[0].Id, Is.EqualTo(5));
        Assert.That(response.Models[0].Status, Is.EqualTo("Accepted"));
    }
}
