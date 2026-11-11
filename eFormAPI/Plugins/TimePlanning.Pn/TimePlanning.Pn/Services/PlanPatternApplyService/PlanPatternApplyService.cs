/*
The MIT License (MIT)

Copyright (c) 2007 - 2026 Microting A/S

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

#nullable enable
namespace TimePlanning.Pn.Services.PlanPatternApplyService;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Infrastructure.Helpers;
using Infrastructure.Models.PlanPattern;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microting.eForm.Infrastructure.Constants;
using Microting.eFormApi.BasePn.Abstractions;
using Microting.eFormApi.BasePn.Infrastructure.Helpers.PluginDbOptions;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;
using Microting.TimePlanningBase.Infrastructure.Data;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using Microting.TimePlanningBase.Infrastructure.Helpers;
using Sentry;
using TimePlanning.Pn.Infrastructure.Models.Planning;
using TimePlanning.Pn.Infrastructure.Models.Settings;
using AssignedSite = Microting.TimePlanningBase.Infrastructure.Data.Entities.AssignedSite;
using TimePlanningPlanningService;
using SdkSite = Microting.eForm.Infrastructure.Data.Entities.Site;

/// <inheritdoc />
public class PlanPatternApplyService(
    TimePlanningPnDbContext dbContext,
    IUserService userService,
    ILogger<PlanPatternApplyService> logger,
    IPluginDbOptions<TimePlanningBaseSettings> options,
    ILogger<TimePlanningPlanningService> planningLogger) : IPlanPatternApplyService
{
    /// <summary>
    /// How far ahead rows are written for a worker with a pattern: the same
    /// horizon the Google Sheet import uses.
    /// </summary>
    internal const int HorizonDays = 180;

    /// <summary>
    /// The flex-chain walk run once per save. A seam so a test can count the
    /// calls; production always uses <see cref="FlexChainRecompute.RunForwardAsync"/>.
    /// </summary>
    internal Func<TimePlanningPnDbContext, AssignedSite?, int, DateTime, Task<int>> FlexRecompute { get; set; }
        = FlexChainRecompute.RunForwardAsync;

    // UTC, like TimeSettingService.UpdateAssignedSite: a lock boundary is
    // always before UtcNow.Date, so rows from here on are normally open.
    private static DateTime Today => DateTime.UtcNow.Date;

    public async Task<OperationDataResult<PlanPatternApplyResult>> SavePatternAsync(
        int assignedSiteId,
        int rotationWeeks,
        DateTime anchorDate,
        DateTime validFrom,
        IReadOnlyCollection<PlanPatternDayModel> days,
        bool overwriteManualDays)
    {
        var validationError = Validate(rotationWeeks, anchorDate, days);
        if (validationError != null)
        {
            return new OperationDataResult<PlanPatternApplyResult>(false, validationError);
        }

        var assignedSiteExists = await dbContext.AssignedSites
            .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
            .AnyAsync(x => x.Id == assignedSiteId);
        if (!assignedSiteExists)
        {
            return new OperationDataResult<PlanPatternApplyResult>(false, "AssignedSite not found");
        }

        PlanPattern pattern;
        AssignedSite assignedSite;
        try
        {
            (pattern, assignedSite) =
                await ReplacePatternAsync(assignedSiteId, rotationWeeks, anchorDate, validFrom, days);
        }
        catch (Exception e)
        {
            SentrySdk.CaptureException(e);
            Console.WriteLine(e);
            logger.LogError(e, "Saving the PlanPattern for AssignedSite {AssignedSiteId} failed", assignedSiteId);
            return new OperationDataResult<PlanPatternApplyResult>(false, "ErrorWhileSavingPlanPattern");
        }

        // The pattern is committed from here on. A failure while writing rows
        // must not report the save as failed: the manager is told to re-save,
        // which re-applies the same pattern idempotently, and the flex walk
        // still runs over whatever was written.
        var (from, to) = ApplyRange(validFrom);
        var result = new PlanPatternApplyResult { PlanPatternId = pattern.Id };
        try
        {
            await WriteRowsAsync(assignedSite, pattern, from, to, overwriteManualDays, result);
        }
        catch (Exception e)
        {
            dbContext.ChangeTracker.Clear();
            SentrySdk.CaptureException(e);
            Console.WriteLine(e);
            logger.LogError(e,
                "PlanPattern {PlanPatternId} saved, but writing its rows for AssignedSite {AssignedSiteId} failed",
                pattern.Id, assignedSiteId);
            result.WarningKey = PlanPatternApplyResult.RowsIncompleteKey;
        }

        await CarryFlexForwardAsync(assignedSite, from);

        return new OperationDataResult<PlanPatternApplyResult>(true, result);
    }

    public async Task<OperationResult> DeletePatternAsync(int assignedSiteId)
    {
        // Checked before the transaction: a retry after a lost commit ack
        // removes nothing and must still count as the success it was.
        var hasActivePattern = await dbContext.PlanPatterns
            .AnyAsync(x => x.AssignedSiteId == assignedSiteId
                           && x.WorkflowState != Constants.WorkflowStates.Removed);
        if (!hasActivePattern)
        {
            return new OperationResult(false, "PlanPatternNotFound");
        }

        try
        {
            var strategy = dbContext.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                // Idempotent on a retry: forget whatever the failed attempt tracked.
                dbContext.ChangeTracker.Clear();
                await using var transaction = await dbContext.Database.BeginTransactionAsync();
                await RemoveActivePatternsAsync(assignedSiteId);
                await transaction.CommitAsync();
            });
        }
        catch (Exception e)
        {
            SentrySdk.CaptureException(e);
            Console.WriteLine(e);
            logger.LogError(e, "Deleting the PlanPattern for AssignedSite {AssignedSiteId} failed", assignedSiteId);
            return new OperationResult(false, "ErrorWhileDeletingPlanPattern");
        }

        // The pattern is gone; the worker's rows are handed back to the
        // weekday template. As for a save, a failure here does not undo the
        // delete: the next planning load rewrites open rows from the template.
        try
        {
            await RegenerateFromTemplateAsync(assignedSiteId);
        }
        catch (Exception e)
        {
            dbContext.ChangeTracker.Clear();
            SentrySdk.CaptureException(e);
            Console.WriteLine(e);
            logger.LogError(e,
                "PlanPattern for AssignedSite {AssignedSiteId} deleted, but rewriting its rows from the template failed",
                assignedSiteId);
        }

        return new OperationResult(true);
    }

    /// <summary>
    /// Rewrites the worker's rows from today to today + <see cref="HorizonDays"/>
    /// through the existing template path,
    /// <see cref="PlanRegistrationHelper.UpdatePlanRegistrationsInPeriod"/>,
    /// with its own skip rules (resigned worker, payment cut-off,
    /// PlanChangedByAdmin, the day lock). Days of an accepted content handover
    /// are not handed to it. That path never writes PlanText, so PlanText is
    /// first cleared on every row handed to it (each open, unmanual,
    /// non-handover row from today to today + <see cref="HorizonDays"/>,
    /// whoever wrote its text). Then the flex balance is carried forward once.
    /// </summary>
    private async Task RegenerateFromTemplateAsync(int assignedSiteId)
    {
        var assignedSite = await dbContext.AssignedSites.SingleAsync(x => x.Id == assignedSiteId);
        if (assignedSite.UseGoogleSheetAsDefault)
        {
            // The sheet owns this worker's rows; the template does not apply.
            return;
        }

        var from = Today;
        var to = Today.AddDays(HorizonDays);
        var lockedThrough = await DayLockHelper.LockedThroughAsync(dbContext, assignedSite.SiteId);
        var rows = await dbContext.PlanRegistrations
            .Where(x => x.SdkSitId == assignedSite.SiteId)
            .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
            .Where(x => x.Date >= from && x.Date <= to)
            .Where(x => !x.PlanChangedByAdmin)
            .Where(x => x.ContentHandoverRequestId == null)
            .WhereOpen(lockedThrough)
            .OrderBy(x => x.Date)
            .ToListAsync();
        if (rows.Count == 0)
        {
            return;
        }

        if (!assignedSite.Resigned)
        {
            foreach (var row in rows.Where(x => !string.IsNullOrEmpty(x.PlanText)))
            {
                row.PlanText = "";
                row.UpdatedByUserId = userService.UserId;
                await row.Update(dbContext);
            }
        }

        await PlanRegistrationHelper.UpdatePlanRegistrationsInPeriod(
            rows.Select(x => new PlanRegistration { Id = x.Id, Date = x.Date }).ToList(),
            new TimePlanningPlanningModel
            {
                SiteId = assignedSite.SiteId,
                PlanningPrDayModels = new List<TimePlanningPlanningPrDayModel>()
            },
            dbContext,
            assignedSite,
            planningLogger,
            new SdkSite { MicrotingUid = assignedSite.SiteId },
            from,
            to,
            options);

        await CarryFlexForwardAsync(assignedSite, from);
    }

    public async Task<List<DateTime>> GetManualDaysInRangeAsync(int assignedSiteId, DateTime validFrom)
    {
        var sdkSitId = await dbContext.AssignedSites
            .Where(x => x.Id == assignedSiteId)
            .Select(x => (int?)x.SiteId)
            .FirstOrDefaultAsync();
        if (sdkSitId == null)
        {
            return [];
        }

        var (from, to) = ApplyRange(validFrom);
        return await dbContext.PlanRegistrations
            .AsNoTracking()
            .Where(x => x.SdkSitId == sdkSitId)
            .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
            .Where(x => x.Date >= from && x.Date <= to)
            .Where(x => x.PlanChangedByAdmin)
            .Select(x => x.Date)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync();
    }

    private static (DateTime From, DateTime To) ApplyRange(DateTime validFrom) =>
        (validFrom.Date > Today ? validFrom.Date : Today, Today.AddDays(HorizonDays));

    private static string? Validate(int rotationWeeks, DateTime anchorDate, IReadOnlyCollection<PlanPatternDayModel>? days)
    {
        if (rotationWeeks is < PlanPatternResolver.MinRotationWeeks or > PlanPatternResolver.MaxRotationWeeks)
        {
            return "PlanPatternRotationWeeksOutOfRange";
        }

        if (anchorDate.DayOfWeek != DayOfWeek.Monday)
        {
            return "PlanPatternAnchorMustBeMonday";
        }

        if (days == null)
        {
            return "PlanPatternDaysMissing";
        }

        if (days.Any(d => d.WeekIndex < 0 || d.WeekIndex >= rotationWeeks || d.DayOfWeek is < 0 or > 6))
        {
            return "PlanPatternDayOutOfRange";
        }

        if (days.GroupBy(d => (d.WeekIndex, d.DayOfWeek)).Any(g => g.Count() > 1))
        {
            return "PlanPatternDuplicateDay";
        }

        return null;
    }

    /// <summary>
    /// In one transaction: soft-deletes every active pattern of the worker
    /// (and its days), creates the new one, and takes the worker off the
    /// Google Sheet so the sheet no longer writes their rows.
    ///
    /// Runs inside the execution strategy because the context is configured
    /// with retry-on-failure, which only allows a user transaction there. A
    /// retried attempt must not trust entities the failed one left tracked
    /// (they already read "removed" / "off the sheet", so nothing would be
    /// written), so every attempt clears the tracker and reloads.
    /// </summary>
    private async Task<(PlanPattern Pattern, AssignedSite AssignedSite)> ReplacePatternAsync(int assignedSiteId,
        int rotationWeeks, DateTime anchorDate, DateTime validFrom, IReadOnlyCollection<PlanPatternDayModel> days)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync();

            await RemoveActivePatternsAsync(assignedSiteId);

            var pattern = new PlanPattern
            {
                AssignedSiteId = assignedSiteId,
                RotationWeeks = rotationWeeks,
                AnchorDate = anchorDate.Date,
                ValidFrom = validFrom.Date,
                CreatedByUserId = userService.UserId,
                UpdatedByUserId = userService.UserId
            };
            await pattern.Create(dbContext);

            foreach (var day in days.OrderBy(d => d.WeekIndex).ThenBy(d => d.DayOfWeek))
            {
                await new PlanPatternDay
                {
                    PlanPatternId = pattern.Id,
                    WeekIndex = day.WeekIndex,
                    DayOfWeek = day.DayOfWeek,
                    PlanText = day.PlanText ?? "",
                    CreatedByUserId = userService.UserId,
                    UpdatedByUserId = userService.UserId
                }.Create(dbContext);
            }

            var assignedSite = await dbContext.AssignedSites.SingleAsync(x => x.Id == assignedSiteId);
            assignedSite.UseGoogleSheetAsDefault = false;
            assignedSite.UpdatedByUserId = userService.UserId;
            // Update writes only when something changed.
            await assignedSite.Update(dbContext);

            await transaction.CommitAsync();

            // Read back with only its live days, as every row writer sees it.
            return ((await PlanPatternPlanner.LoadActivePatternAsync(dbContext, assignedSiteId))!, assignedSite);
        });
    }

    /// <summary>
    /// Soft-deletes the worker's active patterns and their days, each with a
    /// version row. Removes them all, so a duplicate left by any other writer
    /// is cleaned up too.
    /// </summary>
    private async Task RemoveActivePatternsAsync(int assignedSiteId)
    {
        var activePatterns = await dbContext.PlanPatterns
            .Where(x => x.AssignedSiteId == assignedSiteId)
            .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
            .ToListAsync();

        foreach (var pattern in activePatterns)
        {
            var activeDays = await dbContext.PlanPatternDays
                .Where(x => x.PlanPatternId == pattern.Id)
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .ToListAsync();
            foreach (var day in activeDays)
            {
                day.UpdatedByUserId = userService.UserId;
                await day.Delete(dbContext);
            }

            pattern.UpdatedByUserId = userService.UserId;
            await pattern.Delete(dbContext);
        }
    }

    /// <summary>
    /// Creates or regenerates one row per day from <paramref name="from"/> to
    /// today + <see cref="HorizonDays"/> through the shared pattern branch.
    /// Locked days are never touched and are reported; days an admin changed
    /// by hand are kept unless <paramref name="overwriteManualDays"/>, in which
    /// case they are regenerated and lose their PlanChangedByAdmin flag.
    /// Absence days take the pattern's plan and keep their absence.
    /// </summary>
    private async Task WriteRowsAsync(AssignedSite assignedSite, PlanPattern pattern,
        DateTime from, DateTime to, bool overwriteManualDays, PlanPatternApplyResult result)
    {
        // A resigned worker gets no plan after the day they leave; one with no
        // date gets none at all.
        if (assignedSite.Resigned)
        {
            var lastDay = assignedSite.ResignedAtDate?.Date;
            if (lastDay == null || lastDay < from)
            {
                return;
            }

            if (lastDay < to)
            {
                to = lastDay.Value;
            }
        }

        var sdkSitId = assignedSite.SiteId;
        var lockedThrough = await DayLockHelper.LockedThroughAsync(dbContext, sdkSitId);

        var existing = await dbContext.PlanRegistrations
            .Where(x => x.SdkSitId == sdkSitId)
            .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
            .Where(x => x.Date >= from && x.Date <= to)
            .OrderBy(x => x.Id)
            .ToListAsync();
        // The first row of a date wins, as everywhere else rows are looked up by date.
        var rowsByDate = existing
            .GroupBy(x => x.Date.Date)
            .ToDictionary(g => g.Key, g => g.First());

        for (var date = from; date <= to; date = date.AddDays(1))
        {
            if (DayLockHelper.IsLocked(lockedThrough, date))
            {
                result.LockedDays.Add(date);
                continue;
            }

            var planText = PlanPatternPlanner.ResolvePlanText(pattern, date, logger);
            if (planText == null)
            {
                continue;
            }

            if (!rowsByDate.TryGetValue(date, out var row))
            {
                row = new PlanRegistration
                {
                    Date = date,
                    SdkSitId = sdkSitId,
                    CreatedByUserId = userService.UserId,
                    UpdatedByUserId = userService.UserId
                };
                PlanPatternPlanner.ApplyPlanText(row, planText);
                await row.Create(dbContext);
                result.RowsWritten++;
                continue;
            }

            // An accepted content handover moved real work onto or off this
            // day; neither a save nor an overwrite takes it back.
            if (PlanPatternPlanner.IsAcceptedHandoverDay(row))
            {
                continue;
            }

            if (row.PlanChangedByAdmin)
            {
                if (!overwriteManualDays)
                {
                    continue;
                }

                row.PlanChangedByAdmin = false;
            }

            PlanPatternPlanner.ApplyPlanText(row, planText);
            row.UpdatedByUserId = userService.UserId;
            await row.Update(dbContext);
            result.RowsWritten++;
        }
    }

    /// <summary>
    /// Carries the worker's flex balance through the rows just written, once.
    /// The rows are already saved, so a failed walk is reported, not returned
    /// as a failed save — the same rule the absence approval follows.
    /// </summary>
    private async Task CarryFlexForwardAsync(AssignedSite assignedSite, DateTime from)
    {
        try
        {
            var changed = await FlexRecompute(dbContext, assignedSite, assignedSite.SiteId, from);
            logger.LogInformation(
                "[PlanPattern] flex carried for sdkSitId={SdkSitId} from {From:yyyy-MM-dd}, {Changed} row(s) changed",
                assignedSite.SiteId, from, changed);
        }
        catch (Exception e)
        {
            dbContext.ChangeTracker.Clear();
            SentrySdk.CaptureException(e);
            logger.LogError(e,
                "PlanPattern saved, but carrying the flex balance forward FAILED for worker {SdkSitId} from {From:yyyy-MM-dd} — MANUAL RECONCILIATION required",
                assignedSite.SiteId, from);
        }
    }
}
