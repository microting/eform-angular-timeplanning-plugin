#nullable enable
using System;
using System.Collections.Generic;
using Microting.eFormApi.BasePn.Infrastructure.Models.Common;

namespace TimePlanning.Pn.Infrastructure.Models.Planner;

/// <summary>GET planner/grid query (#5489).</summary>
public class PlannerGridRequestModel
{
    /// <summary>First day shown (date only).</summary>
    public DateTime From { get; set; }

    /// <summary>7 or 14.</summary>
    public int Days { get; set; } = 14;

    /// <summary>Department tags; empty = every worker in the caller's scope.</summary>
    public List<int> TagIds { get; set; } = [];
}

/// <summary>Workers x days, as the planner grid shows them.</summary>
public class PlannerGridModel
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public List<DateTime> Dates { get; set; } = [];
    public List<PlannerDayTotalModel> DayTotals { get; set; } = [];
    public List<PlannerWorkerModel> Workers { get; set; } = [];

    /// <summary>The import wizard is offered only while this is false.</summary>
    public bool GoogleSheetRetired { get; set; }
}

public class PlannerDayTotalModel
{
    public DateTime Date { get; set; }

    /// <summary>Workers with plan hours that day and no absence.</summary>
    public int PlannedHeadcount { get; set; }

    /// <summary>Workers with a pending absence request covering the day.</summary>
    public int PendingAbsenceRequests { get; set; }
}

public class PlannerWorkerModel
{
    public int AssignedSiteId { get; set; }

    /// <summary>The SDK site MicrotingUid (PlanRegistration.SdkSitId, AbsenceRequest.RequestedBySdkSitId).</summary>
    public int SiteId { get; set; }

    public string SiteName { get; set; } = "";
    public string? AvatarUrl { get; set; }
    public List<CommonDictionaryModel> Tags { get; set; } = [];

    public bool HasPattern { get; set; }

    /// <summary>The active pattern's rotation, or null without a pattern.</summary>
    public int? RotationWeeks { get; set; }

    /// <summary>True when the worker is still read from the Google Sheet.</summary>
    public bool UseGoogleSheetAsDefault { get; set; }

    /// <summary>
    /// The caller is a current manager of this worker, so the existing
    /// absence-requests approve/reject endpoints accept the caller's decision.
    /// </summary>
    public bool CanApproveAbsence { get; set; }

    /// <summary>WorkingTimeRuleSet.WeeklyNormalSeconds in hours; null when the worker has no rule set.</summary>
    public double? WeeklyNormHours { get; set; }

    public List<PlannerWeekSumModel> Weeks { get; set; } = [];
    public List<PlannerCellModel> Cells { get; set; } = [];
}

/// <summary>Sum of PlanHours over one ISO week that the grid touches (the whole week, not only the shown days).</summary>
public class PlannerWeekSumModel
{
    public int IsoYear { get; set; }
    public int IsoWeek { get; set; }

    /// <summary>The Monday of the week.</summary>
    public DateTime WeekStart { get; set; }

    public double PlannedHours { get; set; }

    /// <summary>Same as the worker's WeeklyNormHours; null when there is no norm.</summary>
    public double? NormHours { get; set; }

    /// <summary>PlannedHours - NormHours; null when there is no norm.</summary>
    public double? DifferenceHours { get; set; }
}

public class PlannerCellModel
{
    public DateTime Date { get; set; }

    /// <summary>Null only for a locked day that has no row (rows are created for every open day shown).</summary>
    public int? PlanRegistrationId { get; set; }

    public string PlanText { get; set; } = "";
    public double PlanHours { get; set; }

    /// <summary>The day's absence type (Message id), or null.</summary>
    public int? MessageId { get; set; }

    /// <summary>Set when the absence came from an approved absence request.</summary>
    public int? AbsenceRequestId { get; set; }

    /// <summary>A deliberate manual plan change; the pattern and template leave the day alone.</summary>
    public bool PlanChangedByAdmin { get; set; }

    /// <summary>Reconciled (day lock): nothing can change it.</summary>
    public bool Locked { get; set; }

    /// <summary>On or before the payment cut-off the template no longer rewrites the day.</summary>
    public bool IsPaidDay { get; set; }

    public bool HasPendingAbsenceRequest { get; set; }
}
