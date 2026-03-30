using System;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using TimePlanning.Pn.Grpc;
using TimePlanning.Pn.Services.TimePlanningSettingService;

namespace TimePlanning.Pn.Services.GrpcServices;

public class TimePlanningSettingsGrpcService : TimePlanningSettingsService.TimePlanningSettingsServiceBase
{
    private readonly ISettingService _settingService;

    public TimePlanningSettingsGrpcService(ISettingService settingService)
    {
        _settingService = settingService;
    }

    public override async Task<GetAssignedSiteResponse> GetAssignedSite(
        GetAssignedSiteRequest request, ServerCallContext context)
    {
        var result = await _settingService.GetAssignedSiteByCurrentUserName();

        var response = new GetAssignedSiteResponse
        {
            Success = result.Success,
            Message = result.Message ?? ""
        };

        if (result.Success && result.Model != null)
        {
            var m = result.Model;
            var grpcModel = new Grpc.AssignedSite
            {
                Id = m.Id,
                SiteName = m.SiteName ?? "",
                SiteId = m.SiteId,
                CaseMicrotingUid = m.CaseMicrotingUid,
                // Shift 1
                StartMonday = m.StartMonday,
                EndMonday = m.EndMonday,
                BreakMonday = m.BreakMonday,
                StartTuesday = m.StartTuesday,
                EndTuesday = m.EndTuesday,
                BreakTuesday = m.BreakTuesday,
                StartWednesday = m.StartWednesday,
                EndWednesday = m.EndWednesday,
                BreakWednesday = m.BreakWednesday,
                StartThursday = m.StartThursday,
                EndThursday = m.EndThursday,
                BreakThursday = m.BreakThursday,
                StartFriday = m.StartFriday,
                EndFriday = m.EndFriday,
                BreakFriday = m.BreakFriday,
                StartSaturday = m.StartSaturday,
                EndSaturday = m.EndSaturday,
                BreakSaturday = m.BreakSaturday,
                StartSunday = m.StartSunday,
                EndSunday = m.EndSunday,
                BreakSunday = m.BreakSunday,
                Resigned = m.Resigned,
                ResignedAtDate = Timestamp.FromDateTime(
                    DateTime.SpecifyKind(m.ResignedAtDate, DateTimeKind.Utc)),
                // Break calculation settings
                MondayBreakMinutesDivider = m.MondayBreakMinutesDivider,
                MondayBreakMinutesPrDivider = m.MondayBreakMinutesPrDivider,
                TuesdayBreakMinutesDivider = m.TuesdayBreakMinutesDivider,
                TuesdayBreakMinutesPrDivider = m.TuesdayBreakMinutesPrDivider,
                WednesdayBreakMinutesDivider = m.WednesdayBreakMinutesDivider,
                WednesdayBreakMinutesPrDivider = m.WednesdayBreakMinutesPrDivider,
                ThursdayBreakMinutesDivider = m.ThursdayBreakMinutesDivider,
                ThursdayBreakMinutesPrDivider = m.ThursdayBreakMinutesPrDivider,
                FridayBreakMinutesDivider = m.FridayBreakMinutesDivider,
                FridayBreakMinutesPrDivider = m.FridayBreakMinutesPrDivider,
                SaturdayBreakMinutesDivider = m.SaturdayBreakMinutesDivider,
                SaturdayBreakMinutesPrDivider = m.SaturdayBreakMinutesPrDivider,
                SundayBreakMinutesDivider = m.SundayBreakMinutesDivider,
                SundayBreakMinutesPrDivider = m.SundayBreakMinutesPrDivider,
                GlobalAutoBreakCalculationActive = m.GlobalAutoBreakCalculationActive,
                AutoBreakCalculationActive = m.AutoBreakCalculationActive,
                MondayBreakMinutesUpperLimit = m.MondayBreakMinutesUpperLimit,
                TuesdayBreakMinutesUpperLimit = m.TuesdayBreakMinutesUpperLimit,
                WednesdayBreakMinutesUpperLimit = m.WednesdayBreakMinutesUpperLimit,
                ThursdayBreakMinutesUpperLimit = m.ThursdayBreakMinutesUpperLimit,
                FridayBreakMinutesUpperLimit = m.FridayBreakMinutesUpperLimit,
                SaturdayBreakMinutesUpperLimit = m.SaturdayBreakMinutesUpperLimit,
                SundayBreakMinutesUpperLimit = m.SundayBreakMinutesUpperLimit,
                UseOneMinuteIntervals = m.UseOneMinuteIntervals,
                AllowAcceptOfPlannedHours = m.AllowAcceptOfPlannedHours,
                AllowEditOfRegistrations = m.AllowEditOfRegistrations,
                AllowPersonalTimeRegistration = m.AllowPersonalTimeRegistration,
                // Shift 2
                StartMonday2NdShift = m.StartMonday2NdShift,
                EndMonday2NdShift = m.EndMonday2NdShift,
                BreakMonday2NdShift = m.BreakMonday2NdShift,
                StartTuesday2NdShift = m.StartTuesday2NdShift,
                EndTuesday2NdShift = m.EndTuesday2NdShift,
                BreakTuesday2NdShift = m.BreakTuesday2NdShift,
                StartWednesday2NdShift = m.StartWednesday2NdShift,
                EndWednesday2NdShift = m.EndWednesday2NdShift,
                BreakWednesday2NdShift = m.BreakWednesday2NdShift,
                StartThursday2NdShift = m.StartThursday2NdShift,
                EndThursday2NdShift = m.EndThursday2NdShift,
                BreakThursday2NdShift = m.BreakThursday2NdShift,
                StartFriday2NdShift = m.StartFriday2NdShift,
                EndFriday2NdShift = m.EndFriday2NdShift,
                BreakFriday2NdShift = m.BreakFriday2NdShift,
                StartSaturday2NdShift = m.StartSaturday2NdShift,
                EndSaturday2NdShift = m.EndSaturday2NdShift,
                BreakSaturday2NdShift = m.BreakSaturday2NdShift,
                StartSunday2NdShift = m.StartSunday2NdShift,
                EndSunday2NdShift = m.EndSunday2NdShift,
                BreakSunday2NdShift = m.BreakSunday2NdShift,
                // Shift 3
                StartMonday3RdShift = m.StartMonday3RdShift,
                EndMonday3RdShift = m.EndMonday3RdShift,
                BreakMonday3RdShift = m.BreakMonday3RdShift,
                StartTuesday3RdShift = m.StartTuesday3RdShift,
                EndTuesday3RdShift = m.EndTuesday3RdShift,
                BreakTuesday3RdShift = m.BreakTuesday3RdShift,
                StartWednesday3RdShift = m.StartWednesday3RdShift,
                EndWednesday3RdShift = m.EndWednesday3RdShift,
                BreakWednesday3RdShift = m.BreakWednesday3RdShift,
                StartThursday3RdShift = m.StartThursday3RdShift,
                EndThursday3RdShift = m.EndThursday3RdShift,
                BreakThursday3RdShift = m.BreakThursday3RdShift,
                StartFriday3RdShift = m.StartFriday3RdShift,
                EndFriday3RdShift = m.EndFriday3RdShift,
                BreakFriday3RdShift = m.BreakFriday3RdShift,
                StartSaturday3RdShift = m.StartSaturday3RdShift,
                EndSaturday3RdShift = m.EndSaturday3RdShift,
                BreakSaturday3RdShift = m.BreakSaturday3RdShift,
                StartSunday3RdShift = m.StartSunday3RdShift,
                EndSunday3RdShift = m.EndSunday3RdShift,
                BreakSunday3RdShift = m.BreakSunday3RdShift,
                // Shift 4
                StartMonday4ThShift = m.StartMonday4ThShift,
                EndMonday4ThShift = m.EndMonday4ThShift,
                BreakMonday4ThShift = m.BreakMonday4ThShift,
                StartTuesday4ThShift = m.StartTuesday4ThShift,
                EndTuesday4ThShift = m.EndTuesday4ThShift,
                BreakTuesday4ThShift = m.BreakTuesday4ThShift,
                StartWednesday4ThShift = m.StartWednesday4ThShift,
                EndWednesday4ThShift = m.EndWednesday4ThShift,
                BreakWednesday4ThShift = m.BreakWednesday4ThShift,
                StartThursday4ThShift = m.StartThursday4ThShift,
                EndThursday4ThShift = m.EndThursday4ThShift,
                BreakThursday4ThShift = m.BreakThursday4ThShift,
                StartFriday4ThShift = m.StartFriday4ThShift,
                EndFriday4ThShift = m.EndFriday4ThShift,
                BreakFriday4ThShift = m.BreakFriday4ThShift,
                StartSaturday4ThShift = m.StartSaturday4ThShift,
                EndSaturday4ThShift = m.EndSaturday4ThShift,
                BreakSaturday4ThShift = m.BreakSaturday4ThShift,
                StartSunday4ThShift = m.StartSunday4ThShift,
                EndSunday4ThShift = m.EndSunday4ThShift,
                BreakSunday4ThShift = m.BreakSunday4ThShift,
                // Shift 5
                StartMonday5ThShift = m.StartMonday5ThShift,
                EndMonday5ThShift = m.EndMonday5ThShift,
                BreakMonday5ThShift = m.BreakMonday5ThShift,
                StartTuesday5ThShift = m.StartTuesday5ThShift,
                EndTuesday5ThShift = m.EndTuesday5ThShift,
                BreakTuesday5ThShift = m.BreakTuesday5ThShift,
                StartWednesday5ThShift = m.StartWednesday5ThShift,
                EndWednesday5ThShift = m.EndWednesday5ThShift,
                BreakWednesday5ThShift = m.BreakWednesday5ThShift,
                StartThursday5ThShift = m.StartThursday5ThShift,
                EndThursday5ThShift = m.EndThursday5ThShift,
                BreakThursday5ThShift = m.BreakThursday5ThShift,
                StartFriday5ThShift = m.StartFriday5ThShift,
                EndFriday5ThShift = m.EndFriday5ThShift,
                BreakFriday5ThShift = m.BreakFriday5ThShift,
                StartSaturday5ThShift = m.StartSaturday5ThShift,
                EndSaturday5ThShift = m.EndSaturday5ThShift,
                BreakSaturday5ThShift = m.BreakSaturday5ThShift,
                StartSunday5ThShift = m.StartSunday5ThShift,
                EndSunday5ThShift = m.EndSunday5ThShift,
                BreakSunday5ThShift = m.BreakSunday5ThShift,
                // Other settings
                UseGoogleSheetAsDefault = m.UseGoogleSheetAsDefault,
                UseOnlyPlanHours = m.UseOnlyPlanHours,
                MondayPlanHours = m.MondayPlanHours,
                TuesdayPlanHours = m.TuesdayPlanHours,
                WednesdayPlanHours = m.WednesdayPlanHours,
                ThursdayPlanHours = m.ThursdayPlanHours,
                FridayPlanHours = m.FridayPlanHours,
                SaturdayPlanHours = m.SaturdayPlanHours,
                SundayPlanHours = m.SundayPlanHours,
                UsePunchClock = m.UsePunchClock,
                UseDetailedPauseEditing = m.UseDetailedPauseEditing,
                UsePunchClockWithAllowRegisteringInHistory = m.UsePunchClockWithAllowRegisteringInHistory,
                DayOfPayment = m.DayOfPayment,
                ThirdShiftActive = m.ThirdShiftActive,
                FourthShiftActive = m.FourthShiftActive,
                FifthShiftActive = m.FifthShiftActive,
                DaysBackInTimeAllowedEditingEnabled = m.DaysBackInTimeAllowedEditingEnabled,
                DaysBackInTimeAllowedEditing = m.DaysBackInTimeAllowedEditing,
                GpsEnabled = m.GpsEnabled,
                SnapshotEnabled = m.SnapshotEnabled,
                IsManager = m.IsManager,
            };

            if (m.ManagingTagIds != null)
            {
                grpcModel.ManagingTagIds.AddRange(m.ManagingTagIds);
            }

            response.Model = grpcModel;
        }

        return response;
    }

    public override async Task<GetRegistrationSitesResponse> GetRegistrationSites(
        GetRegistrationSitesRequest request, ServerCallContext context)
    {
        var result = await _settingService.GetAvailableSites(request.Token);

        var response = new GetRegistrationSitesResponse
        {
            Success = result.Success,
            Message = result.Message ?? ""
        };

        if (result.Success && result.Model != null)
        {
            foreach (var site in result.Model)
            {
                var grpcSite = new Grpc.Site
                {
                    SiteId = site.SiteId,
                    SiteName = site.SiteName ?? "",
                    FirstName = site.FirstName ?? "",
                    LastName = site.LastName ?? "",
                    CustomerNo = site.CustomerNo.GetValueOrDefault(),
                    OtpCode = site.OtpCode.GetValueOrDefault(),
                    UnitId = site.UnitId.GetValueOrDefault(),
                    WorkerUid = site.WorkerUid.GetValueOrDefault(),
                    Email = site.Email ?? "",
                    PinCode = site.PinCode ?? "",
                    DefaultLanguage = site.DefaultLanguage ?? "",
                    HoursStarted = site.HoursStarted,
                    PauseStarted = site.PauseStarted,
                    AutoBreakCalculationActive = site.AutoBreakCalculationActive,
                    AvatarUrl = site.AvatarUrl ?? "",
                    ThirdShiftActive = site.ThirdShiftActive,
                    FourthShiftActive = site.FourthShiftActive,
                    FifthShiftActive = site.FifthShiftActive,
                    SnapshotEnabled = site.SnapshotEnabled,
                    Resigned = site.Resigned,
                    ResignedAtDate = Timestamp.FromDateTime(
                        DateTime.SpecifyKind(site.ResignedAtDate, DateTimeKind.Utc)),
                };
                response.Model.Add(grpcSite);
            }
        }

        return response;
    }
}
