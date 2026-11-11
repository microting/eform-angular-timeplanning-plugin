#nullable enable
using System;
using System.Collections.Generic;

namespace TimePlanning.Pn.Infrastructure.Models.PlanPattern;

/// <summary>What saving a pattern did to the worker's rows (#5489).</summary>
public class PlanPatternApplyResult
{
    public int PlanPatternId { get; set; }

    /// <summary>Rows created or regenerated from the pattern.</summary>
    public int RowsWritten { get; set; }

    /// <summary>Days in the apply range that are locked (reconciled) and were left untouched.</summary>
    public List<DateTime> LockedDays { get; set; } = [];

    /// <summary>
    /// Set when the pattern was saved but not every row could be written; the
    /// manager should save again, which re-applies the pattern.
    /// </summary>
    public const string RowsIncompleteKey = "PlanPatternSavedRowsIncomplete";

    /// <summary>A localization key for a saved-with-a-problem outcome, or null.</summary>
    public string? WarningKey { get; set; }
}
