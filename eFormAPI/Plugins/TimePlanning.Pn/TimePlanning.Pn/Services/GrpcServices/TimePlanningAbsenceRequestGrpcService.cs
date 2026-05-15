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
                DateFrom = DateTime.Parse(request.DateFrom, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal),
                DateTo = DateTime.Parse(request.DateTo, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal),
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
            // sdk site id is derived from the JWT inside the service; the
            // client-supplied request.SdkSiteId is intentionally ignored so
            // a malicious client cannot peek at another manager's inbox.
            var result = await _absenceRequestService.GetInboxAsync();

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

    // AbsenceRequest-specific date format: matches Newtonsoft.Json's serialization
    // behavior. Newtonsoft only emits the trailing 'Z' when DateTime.Kind == Utc;
    // the EF Pomelo MySQL provider materializes DateTime values with
    // Kind == Unspecified, so the JSON read-path drops the Z. To keep the gRPC
    // envelope diff-equal to JSON for both write-path values (Kind=Utc, e.g.
    // freshly-set DateTime.UtcNow) and read-path values (Kind=Unspecified,
    // loaded from MySQL), the Z suffix is conditional on Kind. Microsecond
    // precision (FFFFFF) is preserved unconditionally.
    private static string FormatAbsenceDateUtc(DateTime dt) =>
        dt.Kind == DateTimeKind.Utc
            ? dt.ToString("yyyy-MM-ddTHH:mm:ss.FFFFFFZ")
            : dt.ToString("yyyy-MM-ddTHH:mm:ss.FFFFFF");

    private static string FormatAbsenceDateUtcOrNull(DateTime? dt) =>
        dt == null ? null : FormatAbsenceDateUtc(dt.Value);

    private static Grpc.AbsenceRequestModel MapToGrpc(CsAbsenceRequestModel m)
    {
        var grpc = new Grpc.AbsenceRequestModel
        {
            Id = m.Id,
            RequestedBySdkSiteId = m.RequestedBySdkSitId,
            DateFrom = FormatAbsenceDateUtc(m.DateFrom),
            DateTo = FormatAbsenceDateUtc(m.DateTo),
            Status = m.Status ?? "",
            RequestedAtUtc = FormatAbsenceDateUtc(m.RequestedAtUtc),
            // Wrapped fields — leave null when source is null so proto3 hasField
            // semantics propagate to the dart converter.
            DecidedAtUtc = FormatAbsenceDateUtcOrNull(m.DecidedAtUtc),
            DecidedBySdkSiteId = m.DecidedBySdkSitId,
            RequestComment = m.RequestComment ?? "",
            DecisionComment = m.DecisionComment,
            RequestedByWorkerName = m.RequestedByWorkerName,
            DecidedByWorkerName = m.DecidedByWorkerName,
        };

        if (m.Days != null)
        {
            foreach (var day in m.Days)
            {
                grpc.Days.Add(new Grpc.AbsenceRequestDayModel
                {
                    Date = FormatAbsenceDateUtc(day.Date),
                    MessageId = day.MessageId,
                });
            }
        }

        return grpc;
    }
}
