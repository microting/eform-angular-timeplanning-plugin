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

    public bool AutoBreakCalculationActive { get; set; }

    public int MondayBreakMinutesUpperLimit { get; set; }

    public int TuesdayBreakMinutesUpperLimit { get; set; }

    public int WednesdayBreakMinutesUpperLimit { get; set; }

    public int ThursdayBreakMinutesUpperLimit { get; set; }

    public int FridayBreakMinutesUpperLimit { get; set; }

    public int SaturdayBreakMinutesUpperLimit { get; set; }

    public int SundayBreakMinutesUpperLimit { get; set; }

    // implicit conversion from Microting.TimePlanningBase.Infrastructure.Data.Entities.AssignedSite to AssignedSite
    public static implicit operator AssignedSite(Microting.TimePlanningBase.Infrastructure.Data.Entities.AssignedSite model)
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
            SundayBreakMinutesUpperLimit = model.SundayBreakMinutesUpperLimit
        };
    }
}