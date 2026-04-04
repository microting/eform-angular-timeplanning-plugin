using System;
using System.Threading.Tasks;
using Grpc.Core;
using TimePlanning.Pn.Grpc;
using TimePlanning.Pn.Infrastructure.Models.ContentHandover;
using TimePlanning.Pn.Services.ContentHandoverService;
using CsContentHandoverRequestModel = TimePlanning.Pn.Infrastructure.Models.ContentHandover.ContentHandoverRequestModel;

namespace TimePlanning.Pn.Services.GrpcServices;

public class TimePlanningContentHandoverGrpcService
    : TimePlanningContentHandoverService.TimePlanningContentHandoverServiceBase
{
    private readonly IContentHandoverService _contentHandoverService;

    public TimePlanningContentHandoverGrpcService(IContentHandoverService contentHandoverService)
    {
        _contentHandoverService = contentHandoverService;
    }

    public override async Task<ContentHandoverResponse> CreateContentHandover(
        CreateContentHandoverRequest request, ServerCallContext context)
    {
        try
        {
            var model = new ContentHandoverRequestCreateModel
            {
                ToSdkSitId = request.ToSdkSiteId,
                RequestComment = request.RequestComment
            };

            var result = await _contentHandoverService.CreateAsync(request.FromPlanRegistrationId, model);

            var response = new ContentHandoverResponse
            {
                Success = result.Success,
                Message = result.Message ?? ""
            };

            if (result.Success && result.Model != null)
            {
                response.Model = MapToGrpc(result.Model);
            }

            return response;
        }
        catch (Exception ex)
        {
            return new ContentHandoverResponse
            {
                Success = false,
                Message = ex.Message
            };
        }
    }

    public override async Task<OperationResponse> AcceptContentHandover(
        ContentHandoverDecisionRequest request, ServerCallContext context)
    {
        try
        {
            var model = new ContentHandoverDecisionModel
            {
                DecisionComment = request.DecisionComment
            };

            var result = await _contentHandoverService.AcceptAsync(
                request.RequestId, request.CurrentSdkSiteId, model);

            return new OperationResponse
            {
                Success = result.Success,
                Message = result.Message ?? ""
            };
        }
        catch (Exception ex)
        {
            return new OperationResponse
            {
                Success = false,
                Message = ex.Message
            };
        }
    }

    public override async Task<OperationResponse> RejectContentHandover(
        ContentHandoverDecisionRequest request, ServerCallContext context)
    {
        try
        {
            var model = new ContentHandoverDecisionModel
            {
                DecisionComment = request.DecisionComment
            };

            var result = await _contentHandoverService.RejectAsync(
                request.RequestId, request.CurrentSdkSiteId, model);

            return new OperationResponse
            {
                Success = result.Success,
                Message = result.Message ?? ""
            };
        }
        catch (Exception ex)
        {
            return new OperationResponse
            {
                Success = false,
                Message = ex.Message
            };
        }
    }

    public override async Task<OperationResponse> CancelContentHandover(
        CancelContentHandoverRequest request, ServerCallContext context)
    {
        try
        {
            var result = await _contentHandoverService.CancelAsync(
                request.RequestId, request.CurrentSdkSiteId);

            return new OperationResponse
            {
                Success = result.Success,
                Message = result.Message ?? ""
            };
        }
        catch (Exception ex)
        {
            return new OperationResponse
            {
                Success = false,
                Message = ex.Message
            };
        }
    }

    public override async Task<ContentHandoverListResponse> GetContentHandoverInbox(
        GetContentHandoverRequestsRequest request, ServerCallContext context)
    {
        try
        {
            var result = await _contentHandoverService.GetInboxAsync(request.SdkSiteId);

            var response = new ContentHandoverListResponse
            {
                Success = result.Success,
                Message = result.Message ?? ""
            };

            if (result.Success && result.Model != null)
            {
                foreach (var item in result.Model)
                {
                    response.Models.Add(MapToGrpc(item));
                }
            }

            return response;
        }
        catch (Exception ex)
        {
            return new ContentHandoverListResponse
            {
                Success = false,
                Message = ex.Message
            };
        }
    }

    public override async Task<ContentHandoverListResponse> GetMyContentHandovers(
        GetContentHandoverRequestsRequest request, ServerCallContext context)
    {
        try
        {
            var result = await _contentHandoverService.GetMineAsync(request.SdkSiteId);

            var response = new ContentHandoverListResponse
            {
                Success = result.Success,
                Message = result.Message ?? ""
            };

            if (result.Success && result.Model != null)
            {
                foreach (var item in result.Model)
                {
                    response.Models.Add(MapToGrpc(item));
                }
            }

            return response;
        }
        catch (Exception ex)
        {
            return new ContentHandoverListResponse
            {
                Success = false,
                Message = ex.Message
            };
        }
    }

    private static Grpc.ContentHandoverRequestModel MapToGrpc(CsContentHandoverRequestModel m)
    {
        return new Grpc.ContentHandoverRequestModel
        {
            Id = m.Id,
            FromSdkSiteId = m.FromSdkSitId,
            ToSdkSiteId = m.ToSdkSitId,
            Date = m.Date.ToString("yyyy-MM-ddTHH:mm:ss"),
            FromPlanRegistrationId = m.FromPlanRegistrationId,
            ToPlanRegistrationId = m.ToPlanRegistrationId,
            Status = m.Status ?? "",
            RequestedAtUtc = m.RequestedAtUtc.ToString("yyyy-MM-ddTHH:mm:ss"),
            RespondedAtUtc = m.RespondedAtUtc?.ToString("yyyy-MM-ddTHH:mm:ss") ?? "",
            RequestComment = m.RequestComment ?? "",
            DecisionComment = m.DecisionComment ?? ""
        };
    }
}
