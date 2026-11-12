#nullable enable
using System;
using System.Collections.Generic;
using TimePlanning.Pn.Infrastructure.Models.PlanPattern;

namespace TimePlanning.Pn.Infrastructure.Models.Planner;

/// <summary>GET planner/import/preview (#5489).</summary>
public class PlannerImportPreviewModel
{
    /// <summary>The first Monday read; also every recognised pattern's AnchorDate.</summary>
    public DateTime WindowStart { get; set; }

    public List<PlannerImportWorkerModel> Workers { get; set; } = [];
}

public class PlannerImportWorkerModel
{
    public int AssignedSiteId { get; set; }
    public int SiteId { get; set; }
    public string SiteName { get; set; } = "";

    /// <summary>False = "kunne ikke genkendes": no pattern is proposed.</summary>
    public bool Recognised { get; set; }

    /// <summary>When not recognised: "unparsable", "inconsistent" or "insufficientHistory".</summary>
    public string? Reason { get; set; }

    public int? RotationWeeks { get; set; }
    public DateTime? AnchorDate { get; set; }
    public List<PlanPatternDayModel> Days { get; set; } = [];
}

/// <summary>POST planner/import/apply: the reviewed patterns.</summary>
public class PlannerImportApplyModel
{
    public List<PlannerImportPatternModel> Patterns { get; set; } = [];
}

public class PlannerImportPatternModel
{
    public int AssignedSiteId { get; set; }
    public int RotationWeeks { get; set; }
    public DateTime AnchorDate { get; set; }

    /// <summary>Defaults to today.</summary>
    public DateTime? ValidFrom { get; set; }

    public List<PlanPatternDayModel> Days { get; set; } = [];
}

public class PlannerImportApplyResultModel
{
    public List<PlannerImportAppliedModel> Workers { get; set; } = [];
}

public class PlannerImportAppliedModel
{
    public int AssignedSiteId { get; set; }
    public int PlanPatternId { get; set; }
    public int RowsWritten { get; set; }
    public List<DateTime> LockedDays { get; set; } = [];

    /// <summary>Translated warning when not every row could be written, else null.</summary>
    public string? Warning { get; set; }
}

/// <summary>GET/PUT planner/settings.</summary>
public class PlannerSettingsModel
{
    public bool GoogleSheetRetired { get; set; }
}
