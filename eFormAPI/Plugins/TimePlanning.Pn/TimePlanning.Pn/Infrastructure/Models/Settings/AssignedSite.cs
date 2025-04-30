namespace TimePlanning.Pn.Infrastructure.Models.Settings;

public class AssignedSite
{
    public int Id { get; set; }
    public string SiteName { get; set; }
    public int SiteId { get; set; }
    public int? CaseMicrotingUid { get; set; }
    public int? StartMonday { get; set; }
    public int? EndMonday { get; set; }
    public int? BreakMonday { get; set; }
    public int? StartTuesday { get; set; }
    public int? EndTuesday { get; set; }
    public int? BreakTuesday { get; set; }
    public int? StartWednesday { get; set; }
    public int? EndWednesday { get; set; }
    public int? BreakWednesday { get; set; }
    public int? StartThursday { get; set; }
    public int? EndThursday { get; set; }
    public int? BreakThursday { get; set; }
    public int? StartFriday { get; set; }
    public int? EndFriday { get; set; }
    public int? BreakFriday { get; set; }
    public int? StartSaturday { get; set; }
    public int? EndSaturday { get; set; }
    public int? BreakSaturday { get; set; }
    public int? StartSunday { get; set; }
    public int? EndSunday { get; set; }
    public int? BreakSunday { get; set; }
    public bool Resigned { get; set; }
    public int MondayBreakMinutesDivider { get; set; }
    public int MondayBreakMinutesPrDivider { get; set; }
    public int TuesdayBreakMinutesDivider { get; set; }
    public int TuesdayBreakMinutesPrDivider { get; set; }
    public int WednesdayBreakMinutesDivider { get; set; }
    public int WednesdayBreakMinutesPrDivider { get; set; }
    public int ThursdayBreakMinutesDivider { get; set; }
    public int ThursdayBreakMinutesPrDivider { get; set; }
    public int FridayBreakMinutesDivider { get; set; }
    public int FridayBreakMinutesPrDivider { get; set; }
    public int SaturdayBreakMinutesDivider { get; set; }
    public int SaturdayBreakMinutesPrDivider { get; set; }
    public int SundayBreakMinutesDivider { get; set; }
    public int SundayBreakMinutesPrDivider { get; set; }
    public bool GlobalAutoBreakCalculationActive { get; set; }
    public bool AutoBreakCalculationActive { get; set; }
    public int MondayBreakMinutesUpperLimit { get; set; }
    public int TuesdayBreakMinutesUpperLimit { get; set; }
    public int WednesdayBreakMinutesUpperLimit { get; set; }
    public int ThursdayBreakMinutesUpperLimit { get; set; }
    public int FridayBreakMinutesUpperLimit { get; set; }
    public int SaturdayBreakMinutesUpperLimit { get; set; }
    public int SundayBreakMinutesUpperLimit { get; set; }
    public bool UseOneMinuteIntervals { get; set; }
    public bool AllowAcceptOfPlannedHours { get; set; }
    public bool AllowEditOfRegistrations { get; set; }
    public bool AllowPersonalTimeRegistration { get; set; }
    public int? StartMonday2NdShift { get; set; }
    public int? EndMonday2NdShift { get; set; }
    public int? BreakMonday2NdShift { get; set; }
    public int? StartTuesday2NdShift { get; set; }
    public int? EndTuesday2NdShift { get; set; }
    public int? BreakTuesday2NdShift { get; set; }
    public int? StartWednesday2NdShift { get; set; }
    public int? EndWednesday2NdShift { get; set; }
    public int? BreakWednesday2NdShift { get; set; }
    public int? StartThursday2NdShift { get; set; }
    public int? EndThursday2NdShift { get; set; }
    public int? BreakThursday2NdShift { get; set; }
    public int? StartFriday2NdShift { get; set; }
    public int? EndFriday2NdShift { get; set; }
    public int? BreakFriday2NdShift { get; set; }
    public int? StartSaturday2NdShift { get; set; }
    public int? EndSaturday2NdShift { get; set; }
    public int? BreakSaturday2NdShift { get; set; }
    public int? StartSunday2NdShift { get; set; }
    public int? EndSunday2NdShift { get; set; }
    public int? BreakSunday2NdShift { get; set; }
    public int? StartMonday3RdShift { get; set; }
    public int? EndMonday3RdShift { get; set; }
    public int? BreakMonday3RdShift { get; set; }
    public int? StartTuesday3RdShift { get; set; }
    public int? EndTuesday3RdShift { get; set; }
    public int? BreakTuesday3RdShift { get; set; }
    public int? StartWednesday3RdShift { get; set; }
    public int? EndWednesday3RdShift { get; set; }
    public int? BreakWednesday3RdShift { get; set; }
    public int? StartThursday3RdShift { get; set; }
    public int? EndThursday3RdShift { get; set; }
    public int? BreakThursday3RdShift { get; set; }
    public int? StartFriday3RdShift { get; set; }
    public int? EndFriday3RdShift { get; set; }
    public int? BreakFriday3RdShift { get; set; }
    public int? StartSaturday3RdShift { get; set; }
    public int? EndSaturday3RdShift { get; set; }
    public int? BreakSaturday3RdShift { get; set; }
    public int? StartSunday3RdShift { get; set; }
    public int? EndSunday3RdShift { get; set; }
    public int? BreakSunday3RdShift { get; set; }
    public int? StartMonday4ThShift { get; set; }
    public int? EndMonday4ThShift { get; set; }
    public int? BreakMonday4ThShift { get; set; }
    public int? StartTuesday4ThShift { get; set; }
    public int? EndTuesday4ThShift { get; set; }
    public int? BreakTuesday4ThShift { get; set; }
    public int? StartWednesday4ThShift { get; set; }
    public int? EndWednesday4ThShift { get; set; }
    public int? BreakWednesday4ThShift { get; set; }
    public int? StartThursday4ThShift { get; set; }
    public int? EndThursday4ThShift { get; set; }
    public int? BreakThursday4ThShift { get; set; }
    public int? StartFriday4ThShift { get; set; }
    public int? EndFriday4ThShift { get; set; }
    public int? BreakFriday4ThShift { get; set; }
    public int? StartSaturday4ThShift { get; set; }
    public int? EndSaturday4ThShift { get; set; }
    public int? BreakSaturday4ThShift { get; set; }
    public int? StartSunday4ThShift { get; set; }
    public int? EndSunday4ThShift { get; set; }
    public int? BreakSunday4ThShift { get; set; }
    public int? StartMonday5ThShift { get; set; }
    public int? EndMonday5ThShift { get; set; }
    public int? BreakMonday5ThShift { get; set; }
    public int? StartTuesday5ThShift { get; set; }
    public int? EndTuesday5ThShift { get; set; }
    public int? BreakTuesday5ThShift { get; set; }
    public int? StartWednesday5ThShift { get; set; }
    public int? EndWednesday5ThShift { get; set; }
    public int? BreakWednesday5ThShift { get; set; }
    public int? StartThursday5ThShift { get; set; }
    public int? EndThursday5ThShift { get; set; }
    public int? BreakThursday5ThShift { get; set; }
    public int? StartFriday5ThShift { get; set; }
    public int? EndFriday5ThShift { get; set; }
    public int? BreakFriday5ThShift { get; set; }
    public int? StartSaturday5ThShift { get; set; }
    public int? EndSaturday5ThShift { get; set; }
    public int? BreakSaturday5ThShift { get; set; }
    public int? StartSunday5ThShift { get; set; }
    public int? EndSunday5ThShift { get; set; }
    public int? BreakSunday5ThShift { get; set; }

    public bool UseGoogleSheetAsDefault { get; set; } = true;

    public bool UseOnlyPlanHours { get; set; }
    public int MondayPlanHours { get; set; }
    public int TuesdayPlanHours { get; set; }
    public int WednesdayPlanHours { get; set; }
    public int ThursdayPlanHours { get; set; }
    public int FridayPlanHours { get; set; }
    public int SaturdayPlanHours { get; set; }
    public int SundayPlanHours { get; set; }

    public bool UsePunchClock { get; set; }

    public bool UseDetailedPauseEditing { get; set; }
    public bool UsePunchClockWithAllowRegisteringInHistory { get; set; }

    // implicit conversion from Microting.TimePlanningBase.Infrastructure.Data.Entities.AssignedSite to AssignedSite
    public static implicit operator AssignedSite(
        Microting.TimePlanningBase.Infrastructure.Data.Entities.AssignedSite model)
    {
        return new AssignedSite
        {
            Id = model.Id,
            SiteName = null,
            SiteId = model.SiteId,
            CaseMicrotingUid = model.CaseMicrotingUid,
            StartMonday = model.StartMonday,
            EndMonday = model.EndMonday,
            BreakMonday = model.BreakMonday,
            StartTuesday = model.StartTuesday,
            EndTuesday = model.EndTuesday,
            BreakTuesday = model.BreakTuesday,
            StartWednesday = model.StartWednesday,
            EndWednesday = model.EndWednesday,
            BreakWednesday = model.BreakWednesday,
            StartThursday = model.StartThursday,
            EndThursday = model.EndThursday,
            BreakThursday = model.BreakThursday,
            StartFriday = model.StartFriday,
            EndFriday = model.EndFriday,
            BreakFriday = model.BreakFriday,
            StartSaturday = model.StartSaturday,
            EndSaturday = model.EndSaturday,
            BreakSaturday = model.BreakSaturday,
            StartSunday = model.StartSunday,
            EndSunday = model.EndSunday,
            BreakSunday = model.BreakSunday,
            Resigned = model.Resigned,
            MondayBreakMinutesDivider = model.MondayBreakMinutesDivider,
            MondayBreakMinutesPrDivider = model.MondayBreakMinutesPrDivider,
            TuesdayBreakMinutesDivider = model.TuesdayBreakMinutesDivider,
            TuesdayBreakMinutesPrDivider = model.TuesdayBreakMinutesPrDivider,
            WednesdayBreakMinutesDivider = model.WednesdayBreakMinutesDivider,
            WednesdayBreakMinutesPrDivider = model.WednesdayBreakMinutesPrDivider,
            ThursdayBreakMinutesDivider = model.ThursdayBreakMinutesDivider,
            ThursdayBreakMinutesPrDivider = model.ThursdayBreakMinutesPrDivider,
            FridayBreakMinutesDivider = model.FridayBreakMinutesDivider,
            FridayBreakMinutesPrDivider = model.FridayBreakMinutesPrDivider,
            SaturdayBreakMinutesDivider = model.SaturdayBreakMinutesDivider,
            SaturdayBreakMinutesPrDivider = model.SaturdayBreakMinutesPrDivider,
            SundayBreakMinutesDivider = model.SundayBreakMinutesDivider,
            SundayBreakMinutesPrDivider = model.SundayBreakMinutesPrDivider,
            AutoBreakCalculationActive = model.AutoBreakCalculationActive,
            MondayBreakMinutesUpperLimit = model.MondayBreakMinutesUpperLimit,
            TuesdayBreakMinutesUpperLimit = model.TuesdayBreakMinutesUpperLimit,
            WednesdayBreakMinutesUpperLimit = model.WednesdayBreakMinutesUpperLimit,
            ThursdayBreakMinutesUpperLimit = model.ThursdayBreakMinutesUpperLimit,
            FridayBreakMinutesUpperLimit = model.FridayBreakMinutesUpperLimit,
            SaturdayBreakMinutesUpperLimit = model.SaturdayBreakMinutesUpperLimit,
            SundayBreakMinutesUpperLimit = model.SundayBreakMinutesUpperLimit,
            UseOneMinuteIntervals = model.UseOneMinuteIntervals,
            AllowAcceptOfPlannedHours = model.AllowAcceptOfPlannedHours,
            AllowEditOfRegistrations = model.AllowEditOfRegistrations,
            AllowPersonalTimeRegistration = model.AllowPersonalTimeRegistration,
            StartMonday2NdShift = model.StartMonday2NdShift,
            EndMonday2NdShift = model.EndMonday2NdShift,
            BreakMonday2NdShift = model.BreakMonday2NdShift,
            StartTuesday2NdShift = model.StartTuesday2NdShift,
            EndTuesday2NdShift = model.EndTuesday2NdShift,
            BreakTuesday2NdShift = model.BreakTuesday2NdShift,
            StartWednesday2NdShift = model.StartWednesday2NdShift,
            EndWednesday2NdShift = model.EndWednesday2NdShift,
            BreakWednesday2NdShift = model.BreakWednesday2NdShift,
            StartThursday2NdShift = model.StartThursday2NdShift,
            EndThursday2NdShift = model.EndThursday2NdShift,
            BreakThursday2NdShift = model.BreakThursday2NdShift,
            StartFriday2NdShift = model.StartFriday2NdShift,
            EndFriday2NdShift = model.EndFriday2NdShift,
            BreakFriday2NdShift = model.BreakFriday2NdShift,
            StartSaturday2NdShift = model.StartSaturday2NdShift,
            EndSaturday2NdShift = model.EndSaturday2NdShift,
            BreakSaturday2NdShift = model.BreakSaturday2NdShift,
            StartSunday2NdShift = model.StartSunday2NdShift,
            EndSunday2NdShift = model.EndSunday2NdShift,
            BreakSunday2NdShift = model.BreakSunday2NdShift,
            StartMonday3RdShift = model.StartMonday3RdShift,
            EndMonday3RdShift = model.EndMonday3RdShift,
            BreakMonday3RdShift = model.BreakMonday3RdShift,
            StartTuesday3RdShift = model.StartTuesday3RdShift,
            EndTuesday3RdShift = model.EndTuesday3RdShift,
            BreakTuesday3RdShift = model.BreakTuesday3RdShift,
            StartWednesday3RdShift = model.StartWednesday3RdShift,
            EndWednesday3RdShift = model.EndWednesday3RdShift,
            BreakWednesday3RdShift = model.BreakWednesday3RdShift,
            StartThursday3RdShift = model.StartThursday3RdShift,
            EndThursday3RdShift = model.EndThursday3RdShift,
            BreakThursday3RdShift = model.BreakThursday3RdShift,
            StartFriday3RdShift = model.StartFriday3RdShift,
            EndFriday3RdShift = model.EndFriday3RdShift,
            BreakFriday3RdShift = model.BreakFriday3RdShift,
            StartSaturday3RdShift = model.StartSaturday3RdShift,
            EndSaturday3RdShift = model.EndSaturday3RdShift,
            BreakSaturday3RdShift = model.BreakSaturday3RdShift,
            StartSunday3RdShift = model.StartSunday3RdShift,
            EndSunday3RdShift = model.EndSunday3RdShift,
            BreakSunday3RdShift = model.BreakSunday3RdShift,
            StartMonday4ThShift = model.StartMonday4ThShift,
            EndMonday4ThShift = model.EndMonday4ThShift,
            BreakMonday4ThShift = model.BreakMonday4ThShift,
            StartTuesday4ThShift = model.StartTuesday4ThShift,
            EndTuesday4ThShift = model.EndTuesday4ThShift,
            BreakTuesday4ThShift = model.BreakTuesday4ThShift,
            StartWednesday4ThShift = model.StartWednesday4ThShift,
            EndWednesday4ThShift = model.EndWednesday4ThShift,
            BreakWednesday4ThShift = model.BreakWednesday4ThShift,
            StartThursday4ThShift = model.StartThursday4ThShift,
            EndThursday4ThShift = model.EndThursday4ThShift,
            BreakThursday4ThShift = model.BreakThursday4ThShift,
            StartFriday4ThShift = model.StartFriday4ThShift,
            EndFriday4ThShift = model.EndFriday4ThShift,
            BreakFriday4ThShift = model.BreakFriday4ThShift,
            StartSaturday4ThShift = model.StartSaturday4ThShift,
            EndSaturday4ThShift = model.EndSaturday4ThShift,
            BreakSaturday4ThShift = model.BreakSaturday4ThShift,
            StartSunday4ThShift = model.StartSunday4ThShift,
            EndSunday4ThShift = model.EndSunday4ThShift,
            BreakSunday4ThShift = model.BreakSunday4ThShift,
            StartMonday5ThShift = model.StartMonday5ThShift,
            EndMonday5ThShift = model.EndMonday5ThShift,
            BreakMonday5ThShift = model.BreakMonday5ThShift,
            StartTuesday5ThShift = model.StartTuesday5ThShift,
            EndTuesday5ThShift = model.EndTuesday5ThShift,
            BreakTuesday5ThShift = model.BreakTuesday5ThShift,
            StartWednesday5ThShift = model.StartWednesday5ThShift,
            EndWednesday5ThShift = model.EndWednesday5ThShift,
            BreakWednesday5ThShift = model.BreakWednesday5ThShift,
            StartThursday5ThShift = model.StartThursday5ThShift,
            EndThursday5ThShift = model.EndThursday5ThShift,
            BreakThursday5ThShift = model.BreakThursday5ThShift,
            StartFriday5ThShift = model.StartFriday5ThShift,
            EndFriday5ThShift = model.EndFriday5ThShift,
            BreakFriday5ThShift = model.BreakFriday5ThShift,
            StartSaturday5ThShift = model.StartSaturday5ThShift,
            EndSaturday5ThShift = model.EndSaturday5ThShift,
            BreakSaturday5ThShift = model.BreakSaturday5ThShift,
            StartSunday5ThShift = model.StartSunday5ThShift,
            EndSunday5ThShift = model.EndSunday5ThShift,
            BreakSunday5ThShift = model.BreakSunday5ThShift,
            UseGoogleSheetAsDefault = model.UseGoogleSheetAsDefault,
            UseOnlyPlanHours = model.UseOnlyPlanHours,
            UsePunchClock = model.UsePunchClock,
            UseDetailedPauseEditing = model.UseDetailedPauseEditing,
            UsePunchClockWithAllowRegisteringInHistory = model.UsePunchClockWithAllowRegisteringInHistory,
        };
    }
}



