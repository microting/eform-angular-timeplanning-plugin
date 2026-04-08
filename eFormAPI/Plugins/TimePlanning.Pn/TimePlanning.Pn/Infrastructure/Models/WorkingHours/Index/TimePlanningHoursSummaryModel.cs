namespace TimePlanning.Pn.Infrastructure.Models.WorkingHours.Index;

public class TimePlanningHoursSummaryModel
{
    public double TotalPlanHours { get; set; }
    public double TotalNettoHours { get; set; }
    public double Difference { get; set; }
    public int VacationDays { get; set; }
    public int SickDays { get; set; }
    public int OtherAbsenceDays { get; set; }
    public int AbsenceWithoutPermissionDays { get; set; }
    public double SundayHolidayHours { get; set; }
}