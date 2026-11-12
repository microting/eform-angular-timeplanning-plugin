#nullable enable
using System;
using System.Collections.Generic;

namespace TimePlanning.Pn.Infrastructure.Models.PlanPattern;

/// <summary>One worker's pattern in the one-off sheet import (#5489).</summary>
public class PlanPatternImport
{
    public int AssignedSiteId { get; set; }
    public int RotationWeeks { get; set; }
    public DateTime AnchorDate { get; set; }
    public DateTime ValidFrom { get; set; }
    public IReadOnlyCollection<PlanPatternDayModel> Days { get; set; } = [];
}
