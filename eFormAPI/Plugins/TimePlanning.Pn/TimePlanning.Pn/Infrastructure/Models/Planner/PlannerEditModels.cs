#nullable enable
using System;
using System.Collections.Generic;
using TimePlanning.Pn.Infrastructure.Models.PlanPattern;

namespace TimePlanning.Pn.Infrastructure.Models.Planner;

public class PlannerCellRefModel
{
    public int AssignedSiteId { get; set; }
    public DateTime Date { get; set; }
}

/// <summary>POST planner/cells: one edit applied to every selected cell (#5489).</summary>
public class PlannerCellsUpdateModel
{
    public List<PlannerCellRefModel> Cells { get; set; } = [];

    /// <summary>
    /// <c>7-15/0,5</c>, <c>6-10;14-18</c>, <c>Fri</c> (no plan), or empty
    /// ("Ryd"). Ignored when <see cref="MessageId"/> is set.
    /// </summary>
    public string? ShortForm { get; set; }

    /// <summary>An absence type (Message id). Only with mode onlyTheseDays.</summary>
    public int? MessageId { get; set; }

    /// <summary><see cref="PlannerEditModes.OnlyTheseDays"/> or <see cref="PlannerEditModes.EveryWeekdayFrom"/>.</summary>
    public string Mode { get; set; } = PlannerEditModes.OnlyTheseDays;

    /// <summary>Required for everyWeekdayFrom: the first date the changed pattern day applies.</summary>
    public DateTime? FromDate { get; set; }
}

public static class PlannerEditModes
{
    public const string OnlyTheseDays = "onlyTheseDays";
    public const string EveryWeekdayFrom = "everyWeekdayFrom";
}

public class PlannerCellsUpdateResultModel
{
    /// <summary>Rows written (onlyTheseDays) or rows the re-applied patterns wrote (everyWeekdayFrom).</summary>
    public int UpdatedCount { get; set; }

    /// <summary>Selected cells (onlyTheseDays) or apply-range days (everyWeekdayFrom) that are locked and were not changed.</summary>
    public List<PlannerCellRefModel> LockedCells { get; set; } = [];

    /// <summary>Workers the edit was refused for, e.g. no pattern for everyWeekdayFrom.</summary>
    public List<PlannerWorkerErrorModel> Errors { get; set; } = [];

    /// <summary>Translated "n days locked – not changed", or null when nothing was locked.</summary>
    public string? LockedMessage { get; set; }
}

public class PlannerWorkerErrorModel
{
    public int AssignedSiteId { get; set; }

    /// <summary>Untranslated key, stable for the client.</summary>
    public string Key { get; set; } = "";

    /// <summary>Translated text.</summary>
    public string Message { get; set; } = "";
}

/// <summary>A worker's pattern as the editor shows it.</summary>
public class PlannerPatternModel
{
    /// <summary>Null when the pattern is not saved (a prefill or a copy).</summary>
    public int? PlanPatternId { get; set; }

    public int AssignedSiteId { get; set; }

    /// <summary>
    /// True when the worker has no pattern and this one was prefilled from
    /// their weekday template (1 week); nothing is saved until PUT.
    /// </summary>
    public bool IsPrefill { get; set; }

    public int RotationWeeks { get; set; }

    /// <summary>The Monday that is week 1 of the rotation.</summary>
    public DateTime AnchorDate { get; set; }

    public DateTime ValidFrom { get; set; }

    /// <summary>Every (weekIndex 0..RotationWeeks-1, dayOfWeek 0=Mon..6) with its canonical text ("" = no plan).</summary>
    public List<PlanPatternDayModel> Days { get; set; } = [];
}

/// <summary>PUT planner/patterns/{assignedSiteId}.</summary>
public class PlannerPatternSaveModel
{
    public int RotationWeeks { get; set; }
    public DateTime AnchorDate { get; set; }
    public DateTime ValidFrom { get; set; }

    /// <summary>Day texts may be short forms; they are stored canonical.</summary>
    public List<PlanPatternDayModel> Days { get; set; } = [];

    /// <summary>"Overskriv også mine rettede dage".</summary>
    public bool OverwriteManualDays { get; set; }
}

/// <summary>POST planner/absence: a manager registers an absence over a period.</summary>
public class PlannerAbsenceCreateModel
{
    public int AssignedSiteId { get; set; }
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public int MessageId { get; set; }
    public string? Comment { get; set; }
}

public class PlannerAbsenceResultModel
{
    /// <summary>The Approved request, or null when refused.</summary>
    public int? AbsenceRequestId { get; set; }

    public List<DateTime> AppliedDates { get; set; } = [];

    /// <summary>Days in the range with no plan; not part of the request.</summary>
    public List<DateTime> SkippedDates { get; set; } = [];

    /// <summary>On refusal: the locked days that blocked the whole request.</summary>
    public List<DateTime> LockedDates { get; set; } = [];
}
