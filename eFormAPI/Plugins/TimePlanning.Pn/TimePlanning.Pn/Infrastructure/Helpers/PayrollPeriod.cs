#nullable enable
namespace TimePlanning.Pn.Infrastructure.Helpers;

using System;

/// <summary>
/// A monthly payroll period, <see cref="Start"/>..<see cref="End"/> inclusive.
/// Both are calendar-day labels (time zeroed, Kind Unspecified), the same
/// shape as PlanRegistration.Date, so they compare directly against it.
/// </summary>
public readonly record struct PayrollPeriod(DateTime Start, DateTime End)
{
    /// <summary>
    /// The most recent CLOSED period: End is the latest day strictly before
    /// <paramref name="todayUtc"/> whose day-of-month is
    /// min(cutoffDay, days in that month), and Start is the day after the
    /// previous period's End. The cutoff day itself still belongs to the open
    /// period until it is over, which matches DayLockHelper.CanReconcile
    /// (only days before UtcNow.Date can be reconciled).
    ///
    /// Callers pass DateTime.UtcNow.Date -- the same clock as CanReconcile.
    /// <paramref name="cutoffDay"/> outside 1..31 is clamped into that range.
    /// </summary>
    public static PayrollPeriod LastClosed(DateTime todayUtc, int cutoffDay)
    {
        var cutoff = Math.Clamp(cutoffDay, 1, 31);
        var today = todayUtc.Date;

        var end = CutoffIn(today, cutoff);
        if (end >= today)
        {
            end = CutoffIn(FirstOfMonth(today).AddMonths(-1), cutoff);
        }

        var start = CutoffIn(FirstOfMonth(end).AddMonths(-1), cutoff).AddDays(1);
        return new PayrollPeriod(start, end);
    }

    private static DateTime FirstOfMonth(DateTime d) => new(d.Year, d.Month, 1);

    private static DateTime CutoffIn(DateTime anyDayOfMonth, int cutoff) =>
        new(anyDayOfMonth.Year, anyDayOfMonth.Month,
            Math.Min(cutoff, DateTime.DaysInMonth(anyDayOfMonth.Year, anyDayOfMonth.Month)));
}
