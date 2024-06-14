using System;

namespace TimePlanning.Pn.Infrastructure.Models.WorkingHours.UpdateCreate;

public class TimePlanningWorkingHoursUpdateModel
{
    public DateTime Date { get; set; }
    public int? Shift1Start { get; set; }
    public int? Shift1Pause { get; set; }
    public int? Shift1Stop { get; set; }
    public int? Shift2Start { get; set; }
    public int? Shift2Pause { get; set; }
    public int? Shift2Stop { get; set; }
    public string CommentWorker { get; set; }

    public string Start1StartedAt { get; set; }

    public string Stop1StoppedAt { get; set; }

    public string Pause1StartedAt { get; set; }

    public string Pause1StoppedAt { get; set; }

    public string Start2StartedAt { get; set; }

    public string Stop2StoppedAt { get; set; }

    public string Pause2StartedAt { get; set; }

    public string Pause2StoppedAt { get; set; }
}