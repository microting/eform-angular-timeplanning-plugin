#nullable enable
namespace TimePlanning.Pn.Infrastructure.Models.Planning;

using System;
using System.Collections.Generic;

/// <summary>
/// One date, many workers. The cascade supplies the range, so this never
/// carries a range of its own.
/// </summary>
public class ReconcileThroughRequestModel
{
    public DateTime Date { get; set; }
    public List<int> SiteIds { get; set; } = new();
}
