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
            // Fields added for full REST parity
            SiteName = m.SiteName ?? "",
            WeekDay = m.WeekDay,
            ActualHours = m.ActualHours,
            Difference = m.Difference,
            PauseMinutes = m.PauseMinutes,
            WorkDayStarted = m.WorkDayStarted,
            WorkDayEnded = m.WorkDayEnded,
            PlanHoursMatched = m.PlanHoursMatched,
            PlannedStartOfShift1 = m.PlannedStartOfShift1,
            PlannedEndOfShift1 = m.PlannedEndOfShift1,
            PlannedBreakOfShift1 = m.PlannedBreakOfShift1,
            PlannedStartOfShift2 = m.PlannedStartOfShift2,
            PlannedEndOfShift2 = m.PlannedEndOfShift2,
            PlannedBreakOfShift2 = m.PlannedBreakOfShift2,
            PlannedStartOfShift3 = m.PlannedStartOfShift3,
            PlannedEndOfShift3 = m.PlannedEndOfShift3,
            PlannedBreakOfShift3 = m.PlannedBreakOfShift3,
            PlannedStartOfShift4 = m.PlannedStartOfShift4,
            PlannedEndOfShift4 = m.PlannedEndOfShift4,
            PlannedBreakOfShift4 = m.PlannedBreakOfShift4,
            PlannedStartOfShift5 = m.PlannedStartOfShift5,
            PlannedEndOfShift5 = m.PlannedEndOfShift5,
            PlannedBreakOfShift5 = m.PlannedBreakOfShift5,
            IsDoubleShift = m.IsDoubleShift,
            OnVacation = m.OnVacation,
            Sick = m.Sick,
            OtherAllowedAbsence = m.OtherAllowedAbsence,
            AbsenceWithoutPermission = m.AbsenceWithoutPermission,
            CommentOffice = m.CommentOffice ?? "",
            WorkerComment = m.WorkerComment ?? "",
            // Detailed pause timestamps for shift 1
            Pause10StartedAt = FormatDateTime(m.Pause10StartedAt),
            Pause10StoppedAt = FormatDateTime(m.Pause10StoppedAt),
            Pause11StartedAt = FormatDateTime(m.Pause11StartedAt),
            Pause11StoppedAt = FormatDateTime(m.Pause11StoppedAt),
            Pause12StartedAt = FormatDateTime(m.Pause12StartedAt),
            Pause12StoppedAt = FormatDateTime(m.Pause12StoppedAt),
            Pause13StartedAt = FormatDateTime(m.Pause13StartedAt),
            Pause13StoppedAt = FormatDateTime(m.Pause13StoppedAt),
            Pause14StartedAt = FormatDateTime(m.Pause14StartedAt),
            Pause14StoppedAt = FormatDateTime(m.Pause14StoppedAt),
            Pause15StartedAt = FormatDateTime(m.Pause15StartedAt),
            Pause15StoppedAt = FormatDateTime(m.Pause15StoppedAt),
            Pause16StartedAt = FormatDateTime(m.Pause16StartedAt),
            Pause16StoppedAt = FormatDateTime(m.Pause16StoppedAt),
            Pause17StartedAt = FormatDateTime(m.Pause17StartedAt),
            Pause17StoppedAt = FormatDateTime(m.Pause17StoppedAt),
            Pause18StartedAt = FormatDateTime(m.Pause18StartedAt),
            Pause18StoppedAt = FormatDateTime(m.Pause18StoppedAt),
            Pause19StartedAt = FormatDateTime(m.Pause19StartedAt),
            Pause19StoppedAt = FormatDateTime(m.Pause19StoppedAt),
            Pause100StartedAt = FormatDateTime(m.Pause100StartedAt),
            Pause100StoppedAt = FormatDateTime(m.Pause100StoppedAt),
            Pause101StartedAt = FormatDateTime(m.Pause101StartedAt),
            Pause101StoppedAt = FormatDateTime(m.Pause101StoppedAt),
            Pause102StartedAt = FormatDateTime(m.Pause102StartedAt),
            Pause102StoppedAt = FormatDateTime(m.Pause102StoppedAt),
            // Detailed pause timestamps for shift 2
            Pause20StartedAt = FormatDateTime(m.Pause20StartedAt),
            Pause20StoppedAt = FormatDateTime(m.Pause20StoppedAt),
            Pause21StartedAt = FormatDateTime(m.Pause21StartedAt),
            Pause21StoppedAt = FormatDateTime(m.Pause21StoppedAt),
            Pause22StartedAt = FormatDateTime(m.Pause22StartedAt),
            Pause22StoppedAt = FormatDateTime(m.Pause22StoppedAt),
            Pause23StartedAt = FormatDateTime(m.Pause23StartedAt),
            Pause23StoppedAt = FormatDateTime(m.Pause23StoppedAt),
            Pause24StartedAt = FormatDateTime(m.Pause24StartedAt),
            Pause24StoppedAt = FormatDateTime(m.Pause24StoppedAt),
            Pause25StartedAt = FormatDateTime(m.Pause25StartedAt),
            Pause25StoppedAt = FormatDateTime(m.Pause25StoppedAt),
            Pause26StartedAt = FormatDateTime(m.Pause26StartedAt),
            Pause26StoppedAt = FormatDateTime(m.Pause26StoppedAt),
            Pause27StartedAt = FormatDateTime(m.Pause27StartedAt),
            Pause27StoppedAt = FormatDateTime(m.Pause27StoppedAt),
            Pause28StartedAt = FormatDateTime(m.Pause28StartedAt),
            Pause28StoppedAt = FormatDateTime(m.Pause28StoppedAt),
            Pause29StartedAt = FormatDateTime(m.Pause29StartedAt),
            Pause29StoppedAt = FormatDateTime(m.Pause29StoppedAt),
            Pause200StartedAt = FormatDateTime(m.Pause200StartedAt),
            Pause200StoppedAt = FormatDateTime(m.Pause200StoppedAt),
            Pause201StartedAt = FormatDateTime(m.Pause201StartedAt),
            Pause201StoppedAt = FormatDateTime(m.Pause201StoppedAt),
            Pause202StartedAt = FormatDateTime(m.Pause202StartedAt),
            Pause202StoppedAt = FormatDateTime(m.Pause202StoppedAt),
            // Netto hours override
            NettoHoursOverride = m.NettoHoursOverride,
            NettoHoursOverrideActive = m.NettoHoursOverrideActive,
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
            PaidOutFlex = m.PaidOutFlex,
            SumFlexStart = m.SumFlexStart,
            SumFlexEnd = m.SumFlexEnd,
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
            // Fields added for full REST parity
            SiteName = m.SiteName,
            WeekDay = m.WeekDay,
            ActualHours = m.ActualHours,
            Difference = m.Difference,
            PauseMinutes = m.PauseMinutes,
            WorkDayStarted = m.WorkDayStarted,
            WorkDayEnded = m.WorkDayEnded,
            PlanHoursMatched = m.PlanHoursMatched,
            PlannedStartOfShift1 = m.PlannedStartOfShift1,
            PlannedEndOfShift1 = m.PlannedEndOfShift1,
            PlannedBreakOfShift1 = m.PlannedBreakOfShift1,
            PlannedStartOfShift2 = m.PlannedStartOfShift2,
            PlannedEndOfShift2 = m.PlannedEndOfShift2,
            PlannedBreakOfShift2 = m.PlannedBreakOfShift2,
            PlannedStartOfShift3 = m.PlannedStartOfShift3,
            PlannedEndOfShift3 = m.PlannedEndOfShift3,
            PlannedBreakOfShift3 = m.PlannedBreakOfShift3,
            PlannedStartOfShift4 = m.PlannedStartOfShift4,
            PlannedEndOfShift4 = m.PlannedEndOfShift4,
            PlannedBreakOfShift4 = m.PlannedBreakOfShift4,
            PlannedStartOfShift5 = m.PlannedStartOfShift5,
            PlannedEndOfShift5 = m.PlannedEndOfShift5,
            PlannedBreakOfShift5 = m.PlannedBreakOfShift5,
            IsDoubleShift = m.IsDoubleShift,
            OnVacation = m.OnVacation,
            Sick = m.Sick,
            OtherAllowedAbsence = m.OtherAllowedAbsence,
            AbsenceWithoutPermission = m.AbsenceWithoutPermission,
            CommentOffice = m.CommentOffice,
            WorkerComment = m.WorkerComment,
            // Primary shift timestamps — shifts 1-5
            Start1StartedAt = ParseDateTime(m.Start1StartedAt),
            Stop1StoppedAt = ParseDateTime(m.Stop1StoppedAt),
            Pause1StartedAt = ParseDateTime(m.Pause1StartedAt),
            Pause1StoppedAt = ParseDateTime(m.Pause1StoppedAt),
            Start2StartedAt = ParseDateTime(m.Start2StartedAt),
            Stop2StoppedAt = ParseDateTime(m.Stop2StoppedAt),
            Pause2StartedAt = ParseDateTime(m.Pause2StartedAt),
            Pause2StoppedAt = ParseDateTime(m.Pause2StoppedAt),
            Start3StartedAt = ParseDateTime(m.Start3StartedAt),
            Stop3StoppedAt = ParseDateTime(m.Stop3StoppedAt),
            Pause3StartedAt = ParseDateTime(m.Pause3StartedAt),
            Pause3StoppedAt = ParseDateTime(m.Pause3StoppedAt),
            Start4StartedAt = ParseDateTime(m.Start4StartedAt),
            Stop4StoppedAt = ParseDateTime(m.Stop4StoppedAt),
            Pause4StartedAt = ParseDateTime(m.Pause4StartedAt),
            Pause4StoppedAt = ParseDateTime(m.Pause4StoppedAt),
            Start5StartedAt = ParseDateTime(m.Start5StartedAt),
            Stop5StoppedAt = ParseDateTime(m.Stop5StoppedAt),
            Pause5StartedAt = ParseDateTime(m.Pause5StartedAt),
            Pause5StoppedAt = ParseDateTime(m.Pause5StoppedAt),
            // Detailed pause timestamps for shift 1
            Pause10StartedAt = ParseDateTime(m.Pause10StartedAt),
            Pause10StoppedAt = ParseDateTime(m.Pause10StoppedAt),
            Pause11StartedAt = ParseDateTime(m.Pause11StartedAt),
            Pause11StoppedAt = ParseDateTime(m.Pause11StoppedAt),
            Pause12StartedAt = ParseDateTime(m.Pause12StartedAt),
            Pause12StoppedAt = ParseDateTime(m.Pause12StoppedAt),
            Pause13StartedAt = ParseDateTime(m.Pause13StartedAt),
            Pause13StoppedAt = ParseDateTime(m.Pause13StoppedAt),
            Pause14StartedAt = ParseDateTime(m.Pause14StartedAt),
            Pause14StoppedAt = ParseDateTime(m.Pause14StoppedAt),
            Pause15StartedAt = ParseDateTime(m.Pause15StartedAt),
            Pause15StoppedAt = ParseDateTime(m.Pause15StoppedAt),
            Pause16StartedAt = ParseDateTime(m.Pause16StartedAt),
            Pause16StoppedAt = ParseDateTime(m.Pause16StoppedAt),
            Pause17StartedAt = ParseDateTime(m.Pause17StartedAt),
            Pause17StoppedAt = ParseDateTime(m.Pause17StoppedAt),
            Pause18StartedAt = ParseDateTime(m.Pause18StartedAt),
            Pause18StoppedAt = ParseDateTime(m.Pause18StoppedAt),
            Pause19StartedAt = ParseDateTime(m.Pause19StartedAt),
            Pause19StoppedAt = ParseDateTime(m.Pause19StoppedAt),
            Pause100StartedAt = ParseDateTime(m.Pause100StartedAt),
            Pause100StoppedAt = ParseDateTime(m.Pause100StoppedAt),
            Pause101StartedAt = ParseDateTime(m.Pause101StartedAt),
            Pause101StoppedAt = ParseDateTime(m.Pause101StoppedAt),
            Pause102StartedAt = ParseDateTime(m.Pause102StartedAt),
            Pause102StoppedAt = ParseDateTime(m.Pause102StoppedAt),
            // Detailed pause timestamps for shift 2
            Pause20StartedAt = ParseDateTime(m.Pause20StartedAt),
            Pause20StoppedAt = ParseDateTime(m.Pause20StoppedAt),
            Pause21StartedAt = ParseDateTime(m.Pause21StartedAt),
            Pause21StoppedAt = ParseDateTime(m.Pause21StoppedAt),
            Pause22StartedAt = ParseDateTime(m.Pause22StartedAt),
            Pause22StoppedAt = ParseDateTime(m.Pause22StoppedAt),
            Pause23StartedAt = ParseDateTime(m.Pause23StartedAt),
            Pause23StoppedAt = ParseDateTime(m.Pause23StoppedAt),
            Pause24StartedAt = ParseDateTime(m.Pause24StartedAt),
            Pause24StoppedAt = ParseDateTime(m.Pause24StoppedAt),
            Pause25StartedAt = ParseDateTime(m.Pause25StartedAt),
            Pause25StoppedAt = ParseDateTime(m.Pause25StoppedAt),
            Pause26StartedAt = ParseDateTime(m.Pause26StartedAt),
            Pause26StoppedAt = ParseDateTime(m.Pause26StoppedAt),
            Pause27StartedAt = ParseDateTime(m.Pause27StartedAt),
            Pause27StoppedAt = ParseDateTime(m.Pause27StoppedAt),
            Pause28StartedAt = ParseDateTime(m.Pause28StartedAt),
            Pause28StoppedAt = ParseDateTime(m.Pause28StoppedAt),
            Pause29StartedAt = ParseDateTime(m.Pause29StartedAt),
            Pause29StoppedAt = ParseDateTime(m.Pause29StoppedAt),
            Pause200StartedAt = ParseDateTime(m.Pause200StartedAt),
            Pause200StoppedAt = ParseDateTime(m.Pause200StoppedAt),
            Pause201StartedAt = ParseDateTime(m.Pause201StartedAt),
            Pause201StoppedAt = ParseDateTime(m.Pause201StoppedAt),
            Pause202StartedAt = ParseDateTime(m.Pause202StartedAt),
            Pause202StoppedAt = ParseDateTime(m.Pause202StoppedAt),
            // Netto hours override
            NettoHoursOverride = m.NettoHoursOverride,
            NettoHoursOverrideActive = m.NettoHoursOverrideActive,
        };
    }

    private static DateTime? ParseDateTime(string s) =>
        string.IsNullOrEmpty(s) ? null : DateTime.Parse(s);
}
