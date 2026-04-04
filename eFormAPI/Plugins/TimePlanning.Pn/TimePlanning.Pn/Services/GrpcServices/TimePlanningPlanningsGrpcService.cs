using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using TimePlanning.Pn.Grpc;
using TimePlanning.Pn.Infrastructure.Models.Planning;
using TimePlanning.Pn.Services.TimePlanningPlanningService;

namespace TimePlanning.Pn.Services.GrpcServices;

public class TimePlanningPlanningsGrpcService
    : TimePlanningPlanningsService.TimePlanningPlanningsServiceBase
{
    private readonly ITimePlanningPlanningService _planningService;

    public TimePlanningPlanningsGrpcService(ITimePlanningPlanningService planningService)
    {
        _planningService = planningService;
    }

    public override async Task<GetPlanningsByUserResponse> GetPlanningsByUser(
        GetPlanningsByUserRequest request, ServerCallContext context)
    {
        var requestModel = new TimePlanningPlanningRequestModel
        {
            DateFrom = DateTime.Parse(request.DateFrom, System.Globalization.CultureInfo.InvariantCulture),
            DateTo = DateTime.Parse(request.DateTo, System.Globalization.CultureInfo.InvariantCulture),
            Sort = request.Sort,
            IsSortDsc = request.IsSortDsc,
        };

        var device = request.Device;
        var result = await _planningService.IndexByCurrentUserName(
            requestModel,
            device?.SoftwareVersion,
            device?.DeviceModel,
            device?.Manufacturer,
            device?.OsVersion);

        var response = new GetPlanningsByUserResponse
        {
            Success = result.Success,
            Message = result.Message ?? ""
        };

        if (result.Success && result.Model != null)
        {
            var m = result.Model;
            var grpcModel = new PlanningsByUserModel
            {
                SiteId = m.SiteId,
                AvatarUrl = m.AvatarUrl ?? "",
                CurrentWorkedHours = m.CurrentWorkedHours,
                CurrentWorkedMinutes = m.CurrentWorkedMinutes,
                PercentageCompleted = m.PercentageCompleted,
                PlannedHours = m.PlannedHours,
                PlannedMinutes = m.PlannedMinutes,
                SoftwareVersionIsValid = m.SoftwareVersionIsValid,
            };

            if (m.PlanningPrDayModels != null)
            {
                foreach (var day in m.PlanningPrDayModels)
                {
                    grpcModel.PlanningPrDayModels.Add(MapDayToGrpc(day));
                }
            }

            response.Model = grpcModel;
        }

        return response;
    }

    public override async Task<UpdatePlanningByCurrentUserResponse> UpdatePlanningByCurrentUser(
        UpdatePlanningByCurrentUserRequest request, ServerCallContext context)
    {
        var model = MapDayFromGrpc(request.Model);
        var result = await _planningService.UpdateByCurrentUserNam(model);

        return new UpdatePlanningByCurrentUserResponse
        {
            Success = result.Success,
            Message = result.Message ?? ""
        };
    }

    public override async Task<IndexPlanningsResponse> IndexPlannings(
        IndexPlanningsRequest request, ServerCallContext context)
    {
        try
        {
            var requestModel = new TimePlanningPlanningRequestModel
            {
                DateFrom = string.IsNullOrEmpty(request.DateFrom) ? null : DateTime.Parse(request.DateFrom, System.Globalization.CultureInfo.InvariantCulture),
                DateTo = string.IsNullOrEmpty(request.DateTo) ? null : DateTime.Parse(request.DateTo, System.Globalization.CultureInfo.InvariantCulture),
                SiteId = request.SiteId == 0 ? null : request.SiteId,
                Sort = request.Sort,
                IsSortDsc = request.IsSortDsc,
                ShowResignedSites = request.ShowResignedSites,
                TagIds = request.TagIds.ToList(),
            };

            var result = await _planningService.Index(requestModel);

            var response = new IndexPlanningsResponse
            {
                Success = result.Success,
                Message = result.Message ?? ""
            };

            if (result.Success && result.Model != null)
            {
                foreach (var m in result.Model)
                {
                    var grpcModel = new PlanningsByUserModel
                    {
                        SiteId = m.SiteId,
                        AvatarUrl = m.AvatarUrl ?? "",
                        CurrentWorkedHours = m.CurrentWorkedHours,
                        CurrentWorkedMinutes = m.CurrentWorkedMinutes,
                        PercentageCompleted = m.PercentageCompleted,
                        PlannedHours = m.PlannedHours,
                        PlannedMinutes = m.PlannedMinutes,
                        SoftwareVersionIsValid = m.SoftwareVersionIsValid,
                        SiteName = m.SiteName ?? "",
                        SoftwareVersion = m.SoftwareVersion ?? "",
                        DeviceModel = m.DeviceModel ?? "",
                        DeviceManufacturer = m.DeviceManufacturer ?? "",
                    };

                    if (m.PlanningPrDayModels != null)
                    {
                        foreach (var day in m.PlanningPrDayModels)
                        {
                            grpcModel.PlanningPrDayModels.Add(MapDayToGrpc(day));
                        }
                    }

                    response.Models.Add(grpcModel);
                }
            }

            return response;
        }
        catch (Exception ex)
        {
            return new IndexPlanningsResponse
            {
                Success = false,
                Message = ex.Message
            };
        }
    }

    public override async Task<UpdatePlanningResponse> UpdatePlanning(
        UpdatePlanningRequest request, ServerCallContext context)
    {
        try
        {
            var model = MapDayFromGrpc(request.Model);
            var result = await _planningService.Update(request.PlanningId, model);

            return new UpdatePlanningResponse
            {
                Success = result.Success,
                Message = result.Message ?? ""
            };
        }
        catch (Exception ex)
        {
            return new UpdatePlanningResponse
            {
                Success = false,
                Message = ex.Message
            };
        }
    }

    private static string FormatDateTime(DateTime? dt) =>
        dt?.ToString("yyyy-MM-ddTHH:mm:ss") ?? "";

    private static Grpc.PlanningPrDayModel MapDayToGrpc(TimePlanningPlanningPrDayModel m)
    {
        return new Grpc.PlanningPrDayModel
        {
            Id = m.Id,
            Date = Timestamp.FromDateTime(DateTime.SpecifyKind(m.Date, DateTimeKind.Utc)),
            PlanText = m.PlanText ?? "",
            PlanHours = (int)m.PlanHours,
            NetWorkingHours = m.ActualHours,
            FlexHours = m.Difference,
            PaidOutFlex = m.PaidOutFlex,
            Message = m.Message?.ToString() ?? "",
            SumFlexStart = m.SumFlexStart,
            SumFlexEnd = m.SumFlexEnd,
            Comment = m.WorkerComment ?? "",
            SdkSiteId = m.SiteId,
            // Shift timestamps
            Start1StartedAt = FormatDateTime(m.Start1StartedAt),
            Stop1StoppedAt = FormatDateTime(m.Stop1StoppedAt),
            Pause1StartedAt = FormatDateTime(m.Pause1StartedAt),
            Pause1StoppedAt = FormatDateTime(m.Pause1StoppedAt),
            Start2StartedAt = FormatDateTime(m.Start2StartedAt),
            Stop2StoppedAt = FormatDateTime(m.Stop2StoppedAt),
            Pause2StartedAt = FormatDateTime(m.Pause2StartedAt),
            Pause2StoppedAt = FormatDateTime(m.Pause2StoppedAt),
            Start3StartedAt = FormatDateTime(m.Start3StartedAt),
            Stop3StoppedAt = FormatDateTime(m.Stop3StoppedAt),
            Pause3StartedAt = FormatDateTime(m.Pause3StartedAt),
            Pause3StoppedAt = FormatDateTime(m.Pause3StoppedAt),
            Start4StartedAt = FormatDateTime(m.Start4StartedAt),
            Stop4StoppedAt = FormatDateTime(m.Stop4StoppedAt),
            Pause4StartedAt = FormatDateTime(m.Pause4StartedAt),
            Pause4StoppedAt = FormatDateTime(m.Pause4StoppedAt),
            Start5StartedAt = FormatDateTime(m.Start5StartedAt),
            Stop5StoppedAt = FormatDateTime(m.Stop5StoppedAt),
            Pause5StartedAt = FormatDateTime(m.Pause5StartedAt),
            Pause5StoppedAt = FormatDateTime(m.Pause5StoppedAt),
            // Shift IDs
            Start1Id = m.Start1Id ?? 0,
            Stop1Id = m.Stop1Id ?? 0,
            Pause1Id = m.Pause1Id ?? 0,
            Start2Id = m.Start2Id ?? 0,
            Stop2Id = m.Stop2Id ?? 0,
            Pause2Id = m.Pause2Id ?? 0,
            Start3Id = m.Start3Id ?? 0,
            Stop3Id = m.Stop3Id ?? 0,
            Pause3Id = m.Pause3Id ?? 0,
            Start4Id = m.Start4Id ?? 0,
            Stop4Id = m.Stop4Id ?? 0,
            Pause4Id = m.Pause4Id ?? 0,
            Start5Id = m.Start5Id ?? 0,
            Stop5Id = m.Stop5Id ?? 0,
            Pause5Id = m.Pause5Id ?? 0,
            // Break shifts
            Break1Shift = m.Break1Shift,
            Break2Shift = m.Break2Shift,
            Break3Shift = m.Break3Shift,
            Break4Shift = m.Break4Shift,
            Break5Shift = m.Break5Shift,
        };
    }

    private static TimePlanningPlanningPrDayModel MapDayFromGrpc(Grpc.PlanningPrDayModel? m)
    {
        if (m == null) return new TimePlanningPlanningPrDayModel();

        return new TimePlanningPlanningPrDayModel
        {
            Id = m.Id,
            Date = m.Date?.ToDateTime() ?? DateTime.MinValue,
            PlanText = m.PlanText,
            PlanHours = m.PlanHours,
            ActualHours = m.NetWorkingHours,
            Difference = m.FlexHours,
            PaidOutFlex = m.PaidOutFlex,
            SumFlexStart = m.SumFlexStart,
            SumFlexEnd = m.SumFlexEnd,
            WorkerComment = m.Comment,
            SiteId = m.SdkSiteId,
            Start1Id = m.Start1Id != 0 ? m.Start1Id : null,
            Stop1Id = m.Stop1Id != 0 ? m.Stop1Id : null,
            Pause1Id = m.Pause1Id != 0 ? m.Pause1Id : null,
            Start2Id = m.Start2Id != 0 ? m.Start2Id : null,
            Stop2Id = m.Stop2Id != 0 ? m.Stop2Id : null,
            Pause2Id = m.Pause2Id != 0 ? m.Pause2Id : null,
            Start3Id = m.Start3Id != 0 ? m.Start3Id : null,
            Stop3Id = m.Stop3Id != 0 ? m.Stop3Id : null,
            Pause3Id = m.Pause3Id != 0 ? m.Pause3Id : null,
            Start4Id = m.Start4Id != 0 ? m.Start4Id : null,
            Stop4Id = m.Stop4Id != 0 ? m.Stop4Id : null,
            Pause4Id = m.Pause4Id != 0 ? m.Pause4Id : null,
            Start5Id = m.Start5Id != 0 ? m.Start5Id : null,
            Stop5Id = m.Stop5Id != 0 ? m.Stop5Id : null,
            Pause5Id = m.Pause5Id != 0 ? m.Pause5Id : null,
            Break1Shift = m.Break1Shift,
            Break2Shift = m.Break2Shift,
            Break3Shift = m.Break3Shift,
            Break4Shift = m.Break4Shift,
            Break5Shift = m.Break5Shift,
        };
    }
}
