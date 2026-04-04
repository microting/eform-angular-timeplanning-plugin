using System;
using System.Threading.Tasks;
using Grpc.Core;
using TimePlanning.Pn.Grpc;
using TimePlanning.Pn.Infrastructure.Models.AbsenceRequest;
using TimePlanning.Pn.Services.AbsenceRequestService;
using CsAbsenceRequestModel = TimePlanning.Pn.Infrastructure.Models.AbsenceRequest.AbsenceRequestModel;
using CsAbsenceRequestDayModel = TimePlanning.Pn.Infrastructure.Models.AbsenceRequest.AbsenceRequestDayModel;

namespace TimePlanning.Pn.Services.GrpcServices;

public class TimePlanningAbsenceRequestGrpcService
    : TimePlanningAbsenceRequestService.TimePlanningAbsenceRequestServiceBase
{
    private readonly IAbsenceRequestService _absenceRequestService;

    public TimePlanningAbsenceRequestGrpcService(IAbsenceRequestService absenceRequestService)
    {
        _absenceRequestService = absenceRequestService;
    }

    public override async Task<AbsenceRequestResponse> CreateAbsenceRequest(
        CreateAbsenceRequestRequest request, ServerCallContext context)
    {
        try
        {
            var model = new AbsenceRequestCreateModel
            {
                RequestedBySdkSitId = request.RequestedBySdkSiteId,
                DateFrom = DateTime.Parse(request.DateFrom, System.Globalization.CultureInfo.InvariantCulture),
                DateTo = DateTime.Parse(request.DateTo, System.Globalization.CultureInfo.InvariantCulture),
                MessageId = request.MessageId,
                RequestComment = request.RequestComment,
            };

            var result = await _absenceRequestService.CreateAsync(model);

            var response = new AbsenceRequestResponse
            {
                Success = result.Success,
                Message = result.Message ?? "",
            };

            if (result.Success && result.Model != null)
            {
                response.Model = MapToGrpc(result.Model);
            }

            return response;
        }
        catch (Exception ex)
        {
            return new AbsenceRequestResponse
            {
                Success = false,
                Message = ex.Message,
            };
        }
    }

    public override async Task<OperationResponse> ApproveAbsenceRequest(
        AbsenceDecisionRequest request, ServerCallContext context)
    {
        try
        {
            var model = new AbsenceRequestDecisionModel
            {
                ManagerSdkSitId = request.ManagerSdkSitId,
                DecisionComment = request.DecisionComment,
            };

            var result = await _absenceRequestService.ApproveAsync(request.AbsenceRequestId, model);

            return new OperationResponse
            {
                Success = result.Success,
                Message = result.Message ?? "",
            };
        }
        catch (Exception ex)
        {
            return new OperationResponse
            {
                Success = false,
                Message = ex.Message,
            };
        }
    }

    public override async Task<OperationResponse> RejectAbsenceRequest(
        AbsenceDecisionRequest request, ServerCallContext context)
    {
        try
        {
            var model = new AbsenceRequestDecisionModel
            {
                ManagerSdkSitId = request.ManagerSdkSitId,
                DecisionComment = request.DecisionComment,
            };

            var result = await _absenceRequestService.RejectAsync(request.AbsenceRequestId, model);

            return new OperationResponse
            {
                Success = result.Success,
                Message = result.Message ?? "",
            };
        }
        catch (Exception ex)
        {
            return new OperationResponse
            {
                Success = false,
                Message = ex.Message,
            };
        }
    }

    public override async Task<OperationResponse> CancelAbsenceRequest(
        CancelAbsenceRequestRequest request, ServerCallContext context)
    {
        try
        {
            var result = await _absenceRequestService.CancelAsync(
                request.AbsenceRequestId, request.RequestedBySdkSiteId);

            return new OperationResponse
            {
                Success = result.Success,
                Message = result.Message ?? "",
            };
        }
        catch (Exception ex)
        {
            return new OperationResponse
            {
                Success = false,
                Message = ex.Message,
            };
        }
    }

    public override async Task<AbsenceRequestListResponse> GetAbsenceRequestInbox(
        GetAbsenceRequestsRequest request, ServerCallContext context)
    {
        try
        {
            var result = await _absenceRequestService.GetInboxAsync(request.SdkSiteId);

            var response = new AbsenceRequestListResponse
            {
                Success = result.Success,
                Message = result.Message ?? "",
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
            return new AbsenceRequestListResponse
            {
                Success = false,
                Message = ex.Message,
            };
        }
    }

    public override async Task<AbsenceRequestListResponse> GetMyAbsenceRequests(
        GetAbsenceRequestsRequest request, ServerCallContext context)
    {
        try
        {
            var result = await _absenceRequestService.GetMineAsync(request.SdkSiteId);

            var response = new AbsenceRequestListResponse
            {
                Success = result.Success,
                Message = result.Message ?? "",
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
            return new AbsenceRequestListResponse
            {
                Success = false,
                Message = ex.Message,
            };
        }
    }

    private static Grpc.AbsenceRequestModel MapToGrpc(CsAbsenceRequestModel m)
    {
        var grpc = new Grpc.AbsenceRequestModel
        {
            Id = m.Id,
            RequestedBySdkSiteId = m.RequestedBySdkSitId,
            DateFrom = m.DateFrom.ToString("yyyy-MM-ddTHH:mm:ss"),
            DateTo = m.DateTo.ToString("yyyy-MM-ddTHH:mm:ss"),
            Status = m.Status ?? "",
            RequestedAtUtc = m.RequestedAtUtc.ToString("yyyy-MM-ddTHH:mm:ss"),
            DecidedAtUtc = m.DecidedAtUtc?.ToString("yyyy-MM-ddTHH:mm:ss") ?? "",
            DecidedBySdkSiteId = m.DecidedBySdkSitId ?? 0,
            RequestComment = m.RequestComment ?? "",
            DecisionComment = m.DecisionComment ?? "",
        };

        if (m.Days != null)
        {
            foreach (var day in m.Days)
            {
                grpc.Days.Add(new Grpc.AbsenceRequestDayModel
                {
                    Date = day.Date.ToString("yyyy-MM-ddTHH:mm:ss"),
                    MessageId = day.MessageId,
                });
            }
        }

        return grpc;
    }
}
