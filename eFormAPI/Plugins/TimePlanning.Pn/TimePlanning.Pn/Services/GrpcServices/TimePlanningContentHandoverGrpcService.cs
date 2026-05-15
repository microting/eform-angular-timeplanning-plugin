using System;
using System.Globalization;
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
                RequestComment = request.RequestComment,
                ShiftIndices = new System.Collections.Generic.List<int>(request.ShiftIndices)
            };

            var result = await _contentHandoverService.CreateAsync(request.FromPlanRegistrationId, model);

            var response = new ContentHandoverResponse
            {
                Success = result.Success,
                Message = result.Message ?? ""
            };

            if (result.Success && result.Model != null)
            {
                // Populate BOTH `model` (first row) for legacy/ancient clients AND
                // `models` (full list) for new clients. For the single-row legacy
                // full-day path this is a list of one; for partial multi-shift it
                // is the N created rows and `model` is the first as a defensive
                // fallback.
                foreach (var item in result.Model)
                {
                    response.Models.Add(MapToGrpc(item));
                }
                if (result.Model.Count > 0)
                {
                    response.Model = MapToGrpc(result.Model[0]);
                }
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
            // sdk site id is derived from the JWT inside the service; the
            // client-supplied request.SdkSiteId is intentionally ignored so
            // a malicious client cannot peek at another worker's inbox.
            var result = await _contentHandoverService.GetInboxAsync();

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

    public override async Task<GetHandoverEligibleCoworkersResponse> GetHandoverEligibleCoworkers(
        GetHandoverEligibleCoworkersRequest request, ServerCallContext context)
    {
        try
        {
            if (!DateTime.TryParse(request.Date, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var parsedDate))
            {
                return new GetHandoverEligibleCoworkersResponse
                {
                    Success = false,
                    Message = "Invalid date"
                };
            }

            var shiftIndices = new System.Collections.Generic.List<int>(request.ShiftIndices);
            var result = await _contentHandoverService.GetHandoverEligibleCoworkersAsync(parsedDate, shiftIndices);

            var response = new GetHandoverEligibleCoworkersResponse
            {
                Success = result.Success,
                Message = result.Message ?? ""
            };

            if (result.Success && result.Model != null)
            {
                foreach (var item in result.Model)
                {
                    response.Coworkers.Add(new HandoverCoworker
                    {
                        SdkSiteId = item.SdkSiteId,
                        SiteName = item.SiteName ?? "",
                        PlanRegistrationId = item.PlanRegistrationId
                    });
                }
            }

            return response;
        }
        catch (Exception ex)
        {
            return new GetHandoverEligibleCoworkersResponse
            {
                Success = false,
                Message = ex.Message
            };
        }
    }

    // ContentHandover-specific date format: matches Newtonsoft.Json's
    // serialization behavior. Newtonsoft only emits the trailing 'Z' when
    // DateTime.Kind == Utc; the EF Pomelo MySQL provider materializes
    // DateTime values with Kind == Unspecified, so the JSON read-path drops
    // the Z. To keep the gRPC envelope diff-equal to JSON for both
    // write-path values (Kind=Utc, e.g. freshly-set DateTime.UtcNow) and
    // read-path values (Kind=Unspecified, loaded from MySQL), the Z suffix
    // is conditional on Kind. Microsecond precision (FFFFFF) is preserved
    // unconditionally. Mirror of FormatAbsenceDateUtc in the AbsenceRequest
    // gRPC service.
    private static string FormatContentHandoverDateUtc(DateTime dt) =>
        dt.Kind == DateTimeKind.Utc
            ? dt.ToString("yyyy-MM-ddTHH:mm:ss.FFFFFFZ")
            : dt.ToString("yyyy-MM-ddTHH:mm:ss.FFFFFF");

    private static string FormatContentHandoverDateUtcOrNull(DateTime? dt) =>
        dt == null ? null : FormatContentHandoverDateUtc(dt.Value);

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
            RequestedAtUtc = FormatContentHandoverDateUtc(m.RequestedAtUtc),
            // Wrapped fields — leave null when source is null so proto3
            // hasField semantics propagate to the dart converter.
            RespondedAtUtc = FormatContentHandoverDateUtcOrNull(m.RespondedAtUtc),
            RequestComment = m.RequestComment,
            DecisionComment = m.DecisionComment,
            ShiftIndex = m.ShiftIndex,
            ShiftStartTime = m.ShiftStartTime,
            ShiftEndTime = m.ShiftEndTime,
            FromWorkerName = m.FromWorkerName,
            ToWorkerName = m.ToWorkerName,
        };
    }
}
