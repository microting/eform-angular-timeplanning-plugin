#nullable enable
namespace TimePlanning.Pn.Infrastructure.Models.Planning;

using System;
using System.Collections.Generic;

public class ReconcileThroughResultModel
{
    /// <summary>Where each worker's boundary landed. Per worker, not shared:
    /// the mark falls on that worker's latest day with a registration at or
    /// before the requested date, so a staircase has no single landing date.</summary>
    public Dictionary<int, DateTime> LandedOnBySiteId { get; set; } = new();

    /// <summary>Workers whose boundary actually moved. Excludes no-ops.</summary>
    public int Applied { get; set; }

    /// <summary>Already reconciled at or past the target. Moving them back would
    /// be an unlock, which is deliberately a separate, heavier action.</summary>
    public List<int> SkippedAlreadyFurtherForward { get; set; } = new();

    /// <summary>No registration at or before the target, so there was nothing
    /// to mark. A distinct case from the above — the spec distinguishes them.</summary>
    public List<int> SkippedNoRegistration { get; set; } = new();

    /// <summary>Already marked on exactly the landing day; nothing changed.</summary>
    public List<int> AlreadyReconciledSiteIds { get; set; } = new();
}
