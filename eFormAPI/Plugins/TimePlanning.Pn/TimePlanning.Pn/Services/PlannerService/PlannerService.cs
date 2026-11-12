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
namespace TimePlanning.Pn.Services.PlannerService;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using AbsenceRequestService;
using Infrastructure.Helpers;
using Infrastructure.Models.Planning;
using Infrastructure.Models.PlanPattern;
using Infrastructure.Models.Planner;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microting.eForm.Infrastructure.Constants;
using Microting.eFormApi.BasePn.Abstractions;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;
using Microting.EformAngularFrontendBase.Infrastructure.Data;
using Microting.TimePlanningBase.Infrastructure.Data;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using Microting.TimePlanningBase.Infrastructure.Helpers;
using PlanPatternApplyService;
using Sentry;
using TimePlanningLocalizationService;
using TimePlanningPlanningService;
using AssignedSite = Microting.TimePlanningBase.Infrastructure.Data.Entities.AssignedSite;

/// <inheritdoc />
public class PlannerService(
    TimePlanningPnDbContext dbContext,
    IUserService userService,
    ITimePlanningLocalizationService localizationService,
    IEFormCoreService coreService,
    BaseDbContext baseDbContext,
    ILogger<PlannerService> logger,
    ITimePlanningPlanningService planningService,
    IPlanPatternApplyService planPatternApplyService,
    IServiceScopeFactory serviceScopeFactory) : IPlannerService
{
    /// <summary>
    /// Keys <see cref="IPlanPatternApplyService"/> returns, which this service
    /// translates as they come. Listed so LocalizationKeyCoverageTests can
    /// check they are translated: it only finds literal GetString keys.
    /// </summary>
    internal static readonly string[] PassedThroughKeys =
    [
        "PlanPatternRotationWeeksOutOfRange", "PlanPatternAnchorMustBeMonday", "PlanPatternDaysMissing",
        "PlanPatternDayOutOfRange", "PlanPatternDuplicateDay", "PlanPatternDuplicateWorker",
        "PlanPatternNotFound", "ErrorWhileSavingPlanPattern", "ErrorWhileDeletingPlanPattern",
        "ErrorWhileImportingPlanPatterns", PlanPatternApplyResult.RowsIncompleteKey
    ];

    /// <summary>
    /// Runs inside the manager-absence transaction right before it commits. A
    /// seam so a test can fail the request after every write; null in production.
    /// </summary>
    internal Func<Task>? BeforeAbsenceCommit { get; set; }

    /// <summary>Longest manager absence in one request.</summary>
    private const int MaxAbsenceDays = 366;

    // UTC, like PlanPatternApplyService: the pattern's own notion of today.
    private static DateTime Today => DateTime.UtcNow.Date;

    // ------------------------------------------------------------------ grid

    public async Task<OperationDataResult<PlannerGridModel>> GetGridAsync(PlannerGridRequestModel model)
    {
        if (model.Days is not (7 or 14))
        {
            return Refused<PlannerGridModel>("PlannerGridDaysMustBe7Or14");
        }

        try
        {
            var from = model.From.Date;
            var to = from.AddDays(model.Days - 1);

            // The planning board's own read: the same worker scope
            // (SiteScopeResolver), the same tag filter, and the same lazy
            // creation of missing rows, pattern branch included.
            var index = await planningService.Index(new TimePlanningPlanningRequestModel
            {
                DateFrom = from,
                DateTo = to,
                TagIds = model.TagIds ?? [],
                ShowResignedSites = false,
                Sort = ""
            });
            if (!index.Success)
            {
                return new OperationDataResult<PlannerGridModel>(false, index.Message);
            }

            var boardRows = index.Model ?? [];
            var siteIds = boardRows.Select(x => x.SiteId).Distinct().ToList();
            var assignedBySite = (await dbContext.AssignedSites.AsNoTracking()
                    .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                    .Where(x => siteIds.Contains(x.SiteId))
                    .ToListAsync())
                .GroupBy(x => x.SiteId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Id).First());
            var assignedIds = assignedBySite.Values.Select(x => x.Id).ToList();

            var rotationByAssignedSite = (await dbContext.PlanPatterns.AsNoTracking()
                    .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                    .Where(x => assignedIds.Contains(x.AssignedSiteId))
                    .Select(x => new { x.Id, x.AssignedSiteId, x.RotationWeeks })
                    .ToListAsync())
                .GroupBy(x => x.AssignedSiteId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Id).First().RotationWeeks);

            var ruleSetIds = assignedBySite.Values
                .Where(x => x.WorkingTimeRuleSetId != null)
                .Select(x => x.WorkingTimeRuleSetId!.Value)
                .Distinct()
                .ToList();
            var normSecondsByRuleSet = await dbContext.WorkingTimeRuleSets.AsNoTracking()
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .Where(x => ruleSetIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.WeeklyNormalSeconds);

            // Whole ISO weeks, so a week sum covers the week even when the
            // grid starts mid-week.
            var firstMonday = MondayOf(from);
            var afterLastSunday = MondayOf(to).AddDays(7);
            var rows = await dbContext.PlanRegistrations.AsNoTracking()
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .Where(x => siteIds.Contains(x.SdkSitId))
                .Where(x => x.Date >= firstMonday && x.Date < afterLastSunday)
                .OrderBy(x => x.Id)
                .ToListAsync();
            // The first row of a date wins, as everywhere rows are looked up by date.
            var rowByDay = rows
                .GroupBy(x => (x.SdkSitId, x.Date.Date))
                .ToDictionary(g => g.Key, g => g.First());

            var lockedThroughBySite = await DayLockHelper.LockedThroughForSitesAsync(dbContext, siteIds);

            var pending = await dbContext.AbsenceRequests.AsNoTracking()
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .Where(x => x.Status == AbsenceRequestStatus.Pending)
                .Where(x => siteIds.Contains(x.RequestedBySdkSitId))
                .Where(x => x.DateFrom <= to && x.DateTo >= from)
                .Select(x => new { x.RequestedBySdkSitId, x.DateFrom, x.DateTo })
                .ToListAsync();

            var approvable = await ManagedByCallerAsync(siteIds);

            // The template's payment cut-off as PlanRegistrationHelper uses it:
            // on or before it the weekday template no longer rewrites a day.
            var paidThrough = DateTime.Now.Date.AddMonths(-1);

            var dates = Enumerable.Range(0, model.Days).Select(i => from.AddDays(i)).ToList();
            var grid = new PlannerGridModel
            {
                From = from,
                To = to,
                Dates = dates,
                GoogleSheetRetired = await GoogleSheetRetirement.IsRetiredAsync(dbContext)
            };

            var seenSiteIds = new HashSet<int>();
            foreach (var boardRow in boardRows)
            {
                if (!assignedBySite.TryGetValue(boardRow.SiteId, out var assignedSite)
                    || !seenSiteIds.Add(boardRow.SiteId))
                {
                    continue;
                }

                var lockedThrough = lockedThroughBySite.GetValueOrDefault(boardRow.SiteId);
                double? norm = assignedSite.WorkingTimeRuleSetId != null
                               && normSecondsByRuleSet.TryGetValue(assignedSite.WorkingTimeRuleSetId.Value, out var seconds)
                    ? seconds / 3600.0
                    : null;
                int? rotationWeeks = rotationByAssignedSite.TryGetValue(assignedSite.Id, out var weeks) ? weeks : null;
                var worker = new PlannerWorkerModel
                {
                    AssignedSiteId = assignedSite.Id,
                    SiteId = boardRow.SiteId,
                    SiteName = boardRow.SiteName,
                    AvatarUrl = boardRow.AvatarUrl,
                    Tags = boardRow.Tags ?? [],
                    HasPattern = rotationWeeks != null,
                    RotationWeeks = rotationWeeks,
                    UseGoogleSheetAsDefault = assignedSite.UseGoogleSheetAsDefault,
                    CanApproveAbsence = approvable.Contains(boardRow.SiteId),
                    WeeklyNormHours = norm
                };

                foreach (var date in dates)
                {
                    rowByDay.TryGetValue((boardRow.SiteId, date), out var row);
                    worker.Cells.Add(new PlannerCellModel
                    {
                        Date = date,
                        PlanRegistrationId = row?.Id,
                        PlanText = row?.PlanText ?? "",
                        PlanHours = row?.PlanHours ?? 0,
                        MessageId = row?.MessageId,
                        AbsenceRequestId = row?.AbsenceRequestId,
                        PlanChangedByAdmin = row?.PlanChangedByAdmin ?? false,
                        Locked = DayLockHelper.IsLocked(lockedThrough, date),
                        IsPaidDay = date <= paidThrough,
                        HasPendingAbsenceRequest = pending.Any(p =>
                            p.RequestedBySdkSitId == boardRow.SiteId && p.DateFrom <= date && p.DateTo >= date)
                    });
                }

                worker.Weeks = WeekSums(boardRow.SiteId, firstMonday, afterLastSunday, norm, rowByDay);
                grid.Workers.Add(worker);
            }

            grid.DayTotals = dates.Select(date => new PlannerDayTotalModel
            {
                Date = date,
                PlannedHeadcount = grid.Workers.Count(w =>
                    w.Cells.Any(c => c.Date == date && c.PlanHours > 0 && c.MessageId == null)),
                PendingAbsenceRequests = grid.Workers.Count(w =>
                    w.Cells.Any(c => c.Date == date && c.HasPendingAbsenceRequest))
            }).ToList();

            return new OperationDataResult<PlannerGridModel>(true, grid);
        }
        catch (Exception e)
        {
            return Failed<PlannerGridModel>(e, "Reading the planner grid failed");
        }
    }

    /// <summary>
    /// PlanHours per ISO week from <paramref name="firstMonday"/> up to
    /// <paramref name="afterLastSunday"/>, against the weekly norm.
    /// </summary>
    private static List<PlannerWeekSumModel> WeekSums(int siteId, DateTime firstMonday, DateTime afterLastSunday,
        double? norm, Dictionary<(int, DateTime), PlanRegistration> rowByDay)
    {
        var weeks = new List<PlannerWeekSumModel>();
        for (var monday = firstMonday; monday < afterLastSunday; monday = monday.AddDays(7))
        {
            var planned = Enumerable.Range(0, 7)
                .Sum(i => rowByDay.TryGetValue((siteId, monday.AddDays(i)), out var r) ? r.PlanHours : 0);
            weeks.Add(new PlannerWeekSumModel
            {
                IsoYear = ISOWeek.GetYear(monday),
                IsoWeek = ISOWeek.GetWeekOfYear(monday),
                WeekStart = monday,
                PlannedHours = planned,
                NormHours = norm,
                DifferenceHours = planned - norm
            });
        }

        return weeks;
    }

    /// <summary>
    /// Of <paramref name="siteIds"/>, the workers whose absence requests the
    /// caller may decide: the rule ApproveAsync applies, for many at once.
    /// </summary>
    private async Task<HashSet<int>> ManagedByCallerAsync(List<int> siteIds)
    {
        var callerSdkSitId = await AbsenceDecisionRules.ResolveCallerSdkSiteIdAsync(
            userService, baseDbContext, coreService);
        if (callerSdkSitId == 0)
        {
            return [];
        }

        var managingTagIds = await AbsenceDecisionRules.GetActiveManagingTagIdsAsync(dbContext, callerSdkSitId);
        if (managingTagIds == null)
        {
            return [];
        }

        var sdkDbContext = (await coreService.GetCore()).DbContextHelper.GetDbContext();
        return await AbsenceDecisionRules.ManagedAmongAsync(sdkDbContext, managingTagIds, siteIds);
    }

    // ------------------------------------------------------------- bulk edit

    public async Task<OperationDataResult<PlannerCellsUpdateResultModel>> UpdateCellsAsync(PlannerCellsUpdateModel model)
    {
        if (model.Cells == null || model.Cells.Count == 0)
        {
            return Refused<PlannerCellsUpdateResultModel>("PlannerNoCellsSelected");
        }

        var everyWeekday = model.Mode == PlannerEditModes.EveryWeekdayFrom;
        if (!everyWeekday && model.Mode != PlannerEditModes.OnlyTheseDays)
        {
            return Refused<PlannerCellsUpdateResultModel>("PlannerEditModeUnknown");
        }

        if (everyWeekday && model.FromDate == null)
        {
            return Refused<PlannerCellsUpdateResultModel>("PlannerFromDateRequired");
        }

        // Everything from the message lookup and the short-form parse on is
        // inside the try: no input may turn into a 500.
        try
        {
            PlannerShortFormResult shortForm = default;
            if (model.MessageId != null)
            {
                if (everyWeekday)
                {
                    return Refused<PlannerCellsUpdateResultModel>("PlannerAbsenceOnlyForTheseDays");
                }

                if (!await dbContext.Messages.AnyAsync(x => x.Id == model.MessageId))
                {
                    return Refused<PlannerCellsUpdateResultModel>("PlannerAbsenceTypeNotFound");
                }
            }
            else
            {
                shortForm = PlannerShortForm.Parse(model.ShortForm);
                if (shortForm.Kind == PlannerShortFormKind.Invalid)
                {
                    return Refused<PlannerCellsUpdateResultModel>("PlannerShortFormInvalid");
                }
            }

            var (scoped, errorKey) = await ScopedSitesAsync();
            if (errorKey != null)
            {
                return Refused<PlannerCellsUpdateResultModel>(errorKey);
            }

            var result = new PlannerCellsUpdateResultModel();
            foreach (var group in model.Cells.GroupBy(c => c.AssignedSiteId))
            {
                var assignedSite = scoped.FirstOrDefault(x => x.Id == group.Key);
                if (assignedSite == null)
                {
                    AddError(result, group.Key, "PlannerWorkerNotFound");
                    continue;
                }

                var dates = group.Select(c => c.Date.Date).Distinct().OrderBy(d => d).ToList();
                if (everyWeekday)
                {
                    await EditPatternDaysAsync(assignedSite, dates, shortForm.PlanText, model.FromDate!.Value.Date, result);
                }
                else if (assignedSite.Resigned)
                {
                    AddError(result, group.Key, "PlanningCannotBeEditedForResignedSite");
                }
                else
                {
                    await EditDaysAsync(assignedSite, dates, shortForm, model.MessageId, result);
                }
            }

            if (result.LockedCells.Count > 0)
            {
                result.LockedMessage = localizationService.GetString("PlannerDaysLockedNotChanged", result.LockedCells.Count);
            }

            return new OperationDataResult<PlannerCellsUpdateResultModel>(true, result);
        }
        catch (Exception e)
        {
            return Failed<PlannerCellsUpdateResultModel>(e, "Editing planner cells failed");
        }
    }

    /// <summary>
    /// "kun disse dage": each open day gets the edit and, for a plan change,
    /// PlanChangedByAdmin, so neither the template nor a pattern rewrites it.
    /// An absence sets MessageId exactly like the planning board's manual
    /// edit does and leaves the plan (and the flag) alone. Locked days are
    /// skipped and reported. Flex is carried once from the earliest change.
    /// </summary>
    private async Task EditDaysAsync(AssignedSite assignedSite, List<DateTime> dates,
        PlannerShortFormResult shortForm, int? messageId, PlannerCellsUpdateResultModel result)
    {
        var sdkSitId = assignedSite.SiteId;
        var lockedThrough = await DayLockHelper.LockedThroughAsync(dbContext, sdkSitId);
        var rowsByDate = (await dbContext.PlanRegistrations
                .Where(x => x.SdkSitId == sdkSitId)
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .Where(x => dates.Contains(x.Date))
                .OrderBy(x => x.Id)
                .ToListAsync())
            .GroupBy(x => x.Date.Date)
            .ToDictionary(g => g.Key, g => g.First());

        DateTime? earliest = null;
        foreach (var date in dates)
        {
            if (DayLockHelper.IsLocked(lockedThrough, date))
            {
                result.LockedCells.Add(new PlannerCellRefModel { AssignedSiteId = assignedSite.Id, Date = date });
                continue;
            }

            var isNew = !rowsByDate.TryGetValue(date, out var row);
            row ??= new PlanRegistration
            {
                Date = date,
                SdkSitId = sdkSitId,
                PlanText = "",
                CreatedByUserId = userService.UserId
            };

            if (messageId != null)
            {
                row.MessageId = messageId;
            }
            else
            {
                PlanPatternPlanner.ApplyPlanText(row, shortForm.PlanText);
                row.PlanChangedByAdmin = true;
                // "Ryd" also removes a manually set absence; an approved
                // absence request's day keeps it (reject/cancel own that).
                if (shortForm.Kind == PlannerShortFormKind.Clear && row.AbsenceRequestId == null)
                {
                    row.MessageId = null;
                }
            }

            row.UpdatedByUserId = userService.UserId;
            if (isNew)
            {
                await row.Create(dbContext);
            }
            else
            {
                await row.Update(dbContext);
            }

            result.UpdatedCount++;
            earliest ??= date;
        }

        if (earliest != null)
        {
            await CarryFlexForwardAsync(assignedSite, earliest.Value);
        }
    }

    /// <summary>
    /// "alle [ugedag] fra [dato]": sets the pattern day of every selected date,
    /// i.e. that date's rotation week and weekday only, to
    /// <paramref name="planText"/>, and re-applies the pattern from
    /// <paramref name="fromDate"/>. In a 3-week rotation a Wednesday in week 2
    /// changes week 2's Wednesday; weeks 1 and 3 keep theirs. The pattern
    /// keeps its anchor and ValidFrom; rows before the date stay as they are.
    /// </summary>
    private async Task EditPatternDaysAsync(AssignedSite assignedSite, List<DateTime> dates, string planText,
        DateTime fromDate, PlannerCellsUpdateResultModel result)
    {
        var pattern = await PlanPatternPlanner.LoadActivePatternAsync(dbContext, assignedSite.Id);
        if (pattern == null)
        {
            AddError(result, assignedSite.Id, "PlannerWorkerHasNoPattern");
            return;
        }

        var days = FullGrid(pattern.RotationWeeks, pattern.Days.Select(ToModel));
        var patternDays = dates
            .Select(d => (RotationWeekIndex(pattern.AnchorDate, pattern.RotationWeeks, d), IsoDayOfWeek(d)))
            .ToHashSet();
        foreach (var day in days.Where(d => patternDays.Contains((d.WeekIndex, d.DayOfWeek))))
        {
            day.PlanText = planText;
        }

        var saved = await planPatternApplyService.SavePatternAsync(assignedSite.Id, pattern.RotationWeeks,
            pattern.AnchorDate, pattern.ValidFrom, days, false, fromDate);
        if (!saved.Success)
        {
            AddError(result, assignedSite.Id, PlannerKeyFor(saved.Message));
            return;
        }

        result.UpdatedCount += saved.Model!.RowsWritten;
        result.LockedCells.AddRange(saved.Model.LockedDays.Select(d =>
            new PlannerCellRefModel { AssignedSiteId = assignedSite.Id, Date = d }));
        if (saved.Model.WarningKey != null)
        {
            AddError(result, assignedSite.Id, saved.Model.WarningKey);
        }
    }

    // --------------------------------------------------------------- pattern

    public async Task<OperationDataResult<PlannerPatternModel>> GetPatternAsync(int assignedSiteId)
    {
        try
        {
            var (assignedSite, errorKey) = await ScopedSiteAsync(assignedSiteId);
            if (assignedSite == null)
            {
                return Refused<PlannerPatternModel>(errorKey!);
            }

            var pattern = await PlanPatternPlanner.LoadActivePatternAsync(dbContext, assignedSiteId);
            return new OperationDataResult<PlannerPatternModel>(true, pattern == null
                ? new PlannerPatternModel
                {
                    AssignedSiteId = assignedSiteId,
                    IsPrefill = true,
                    RotationWeeks = 1,
                    AnchorDate = MondayOf(Today),
                    ValidFrom = Today,
                    Days = Enumerable.Range(0, 7).Select(dow => new PlanPatternDayModel
                    {
                        WeekIndex = 0,
                        DayOfWeek = dow,
                        PlanText = TemplatePlanText(assignedSite, dow)
                    }).ToList()
                }
                : ToModel(pattern));
        }
        catch (Exception e)
        {
            return Failed<PlannerPatternModel>(e, "Reading a plan pattern failed");
        }
    }

    public async Task<OperationDataResult<PlanPatternApplyResult>> SavePatternAsync(int assignedSiteId,
        PlannerPatternSaveModel model)
    {
        try
        {
            var (assignedSite, errorKey) = await ScopedSiteAsync(assignedSiteId);
            if (assignedSite == null)
            {
                return Refused<PlanPatternApplyResult>(errorKey!);
            }

            var days = NormaliseDays(model.Days);
            if (days == null)
            {
                return Refused<PlanPatternApplyResult>("PlannerShortFormInvalid");
            }

            var saved = await planPatternApplyService.SavePatternAsync(assignedSiteId, model.RotationWeeks,
                model.AnchorDate, model.ValidFrom, days, model.OverwriteManualDays);
            if (!saved.Success)
            {
                return Refused<PlanPatternApplyResult>(PlannerKeyFor(saved.Message));
            }

            return saved.Model!.WarningKey == null
                ? saved
                : new OperationDataResult<PlanPatternApplyResult>(true,
                    localizationService.GetString(saved.Model.WarningKey), saved.Model);
        }
        catch (Exception e)
        {
            return Failed<PlanPatternApplyResult>(e, "Saving a plan pattern failed");
        }
    }

    public async Task<OperationResult> DeletePatternAsync(int assignedSiteId)
    {
        try
        {
            var (assignedSite, errorKey) = await ScopedSiteAsync(assignedSiteId);
            if (assignedSite == null)
            {
                return new OperationResult(false, localizationService.GetString(errorKey!));
            }

            var deleted = await planPatternApplyService.DeletePatternAsync(assignedSiteId);
            return deleted.Success
                ? deleted
                : new OperationResult(false, localizationService.GetString(deleted.Message));
        }
        catch (Exception e)
        {
            return Failed(e, "Deleting a plan pattern failed");
        }
    }

    public async Task<OperationDataResult<List<DateTime>>> GetManualDaysAsync(int assignedSiteId, DateTime validFrom)
    {
        try
        {
            var (assignedSite, errorKey) = await ScopedSiteAsync(assignedSiteId);
            if (assignedSite == null)
            {
                return Refused<List<DateTime>>(errorKey!);
            }

            return new OperationDataResult<List<DateTime>>(true,
                await planPatternApplyService.GetManualDaysInRangeAsync(assignedSiteId, validFrom));
        }
        catch (Exception e)
        {
            return Failed<List<DateTime>>(e, "Reading manual days failed");
        }
    }

    public async Task<OperationDataResult<PlannerPatternModel>> CopyPatternAsync(int assignedSiteId,
        int sourceAssignedSiteId)
    {
        try
        {
            var (target, errorKey) = await ScopedSiteAsync(assignedSiteId);
            if (target == null)
            {
                return Refused<PlannerPatternModel>(errorKey!);
            }

            var (source, sourceErrorKey) = await ScopedSiteAsync(sourceAssignedSiteId);
            if (source == null)
            {
                return Refused<PlannerPatternModel>(sourceErrorKey!);
            }

            var pattern = await PlanPatternPlanner.LoadActivePatternAsync(dbContext, sourceAssignedSiteId);
            if (pattern == null)
            {
                return Refused<PlannerPatternModel>("PlanPatternNotFound");
            }

            // A copy only (decision 7): rotation, anchor and days, no link to
            // the source and nothing saved; the editor saves it with PUT.
            var copy = ToModel(pattern);
            copy.PlanPatternId = null;
            copy.AssignedSiteId = assignedSiteId;
            copy.ValidFrom = Today;
            return new OperationDataResult<PlannerPatternModel>(true, copy);
        }
        catch (Exception e)
        {
            return Failed<PlannerPatternModel>(e, "Copying a plan pattern failed");
        }
    }

    // --------------------------------------------------------------- absence

    public async Task<OperationDataResult<PlannerAbsenceResultModel>> CreateAbsenceAsync(PlannerAbsenceCreateModel model)
    {
        var from = model.From.Date;
        var to = model.To.Date;
        if (to < from)
        {
            return Refused<PlannerAbsenceResultModel>("DateToMustBeGreaterThanOrEqualToDateFrom");
        }

        if ((to - from).Days >= MaxAbsenceDays)
        {
            return Refused<PlannerAbsenceResultModel>("PlannerAbsenceRangeTooLong");
        }

        try
        {
            if (!await dbContext.Messages.AnyAsync(x => x.Id == model.MessageId))
            {
                return Refused<PlannerAbsenceResultModel>("PlannerAbsenceTypeNotFound");
            }

            var (assignedSite, errorKey) = await ScopedSiteAsync(model.AssignedSiteId);
            if (assignedSite == null)
            {
                return Refused<PlannerAbsenceResultModel>(errorKey!);
            }

            var workerSdkSitId = assignedSite.SiteId;
            // The decider is the caller, and only a current manager of the
            // worker may decide, as for ApproveAsync.
            var managerSdkSitId = await AbsenceDecisionRules.ResolveDecidingManagerAsync(
                workerSdkSitId, dbContext, userService, baseDbContext, coreService);
            if (managerSdkSitId == null)
            {
                return Refused<PlannerAbsenceResultModel>("UnauthorizedToApprove");
            }

            var dates = Enumerable.Range(0, (to - from).Days + 1).Select(i => from.AddDays(i)).ToList();

            // Checked before anything is written: a locked day refuses the
            // whole request, as approve does, and the days are listed.
            var lockedThrough = await DayLockHelper.LockedThroughAsync(dbContext, workerSdkSitId);
            var lockedDates = dates.Where(d => DayLockHelper.IsLocked(lockedThrough, d)).ToList();
            if (lockedDates.Count > 0)
            {
                return new OperationDataResult<PlannerAbsenceResultModel>(false,
                    localizationService.GetString("PlannerAbsenceDaysLocked",
                        string.Join(", ", lockedDates.Select(d => d.ToString("yyyy-MM-dd")))),
                    new PlannerAbsenceResultModel { LockedDates = lockedDates });
            }

            // As for a worker's own request: approving over days another
            // pending or approved request holds would re-tag them.
            var overlaps = await dbContext.AbsenceRequests
                .AnyAsync(ar => ar.RequestedBySdkSitId == workerSdkSitId
                                && (ar.Status == AbsenceRequestStatus.Pending
                                    || ar.Status == AbsenceRequestStatus.Approved)
                                && ar.DateFrom <= to
                                && ar.DateTo >= from);
            if (overlaps)
            {
                return Refused<PlannerAbsenceResultModel>("OverlappingAbsenceRequestExists");
            }

            var plannedDates = await PlannedDatesAsync(workerSdkSitId, from, to);
            var result = new PlannerAbsenceResultModel
            {
                AppliedDates = dates.Where(plannedDates.Contains).ToList(),
                SkippedDates = dates.Where(d => !plannedDates.Contains(d)).ToList()
            };
            if (result.AppliedDates.Count == 0)
            {
                return new OperationDataResult<PlannerAbsenceResultModel>(false,
                    localizationService.GetString("PlannerAbsenceNoPlannedDays"), result);
            }

            try
            {
                result.AbsenceRequestId = await CreateApprovedRequestAsync(workerSdkSitId, managerSdkSitId.Value,
                    from, to, model, result.AppliedDates);
            }
            catch (Exception e)
            {
                dbContext.ChangeTracker.Clear();
                LogFailure(e, "Registering a manager absence failed; nothing was saved");
                return Refused<PlannerAbsenceResultModel>("ErrorApprovingAbsenceRequest");
            }

            await CarryFlexForwardAsync(assignedSite, result.AppliedDates.Min());

            // The worker hears of it exactly as of an approved request of their own.
            AbsenceDecisionRules.StartApprovedPush(serviceScopeFactory, logger, workerSdkSitId,
                result.AbsenceRequestId.Value);
            return new OperationDataResult<PlannerAbsenceResultModel>(true, result);
        }
        catch (Exception e)
        {
            return Failed<PlannerAbsenceResultModel>(e, "Registering a manager absence failed");
        }
    }

    /// <summary>
    /// The worker's days in the range that carry plan hours: a day with no
    /// plan has nothing to be absent from. The first row of a date counts.
    /// </summary>
    private async Task<HashSet<DateTime>> PlannedDatesAsync(int sdkSitId, DateTime from, DateTime to) =>
        (await dbContext.PlanRegistrations.AsNoTracking()
            .Where(x => x.SdkSitId == sdkSitId)
            .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
            .Where(x => x.Date >= from && x.Date <= to)
            .OrderBy(x => x.Id)
            .ToListAsync())
        .GroupBy(x => x.Date.Date)
        .Where(g => g.First().PlanHours > 0)
        .Select(g => g.Key)
        .ToHashSet();

    /// <summary>
    /// In one transaction: an Approved request decided by the manager, one day
    /// per planned date, each applied to the worker's row by the same code
    /// ApproveAsync uses. It then reads in Request History like any decided
    /// request.
    /// </summary>
    private async Task<int> CreateApprovedRequestAsync(int workerSdkSitId, int managerSdkSitId,
        DateTime from, DateTime to, PlannerAbsenceCreateModel model, List<DateTime> dates)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync();

            var now = DateTime.UtcNow;
            var decidedAt = new DateTime(now.Ticks - now.Ticks % TimeSpan.TicksPerMicrosecond, DateTimeKind.Utc);
            var request = new AbsenceRequest
            {
                RequestedBySdkSitId = workerSdkSitId,
                DateFrom = from,
                DateTo = to,
                Status = AbsenceRequestStatus.Approved,
                RequestedAtUtc = decidedAt,
                DecidedAtUtc = decidedAt,
                DecidedBySdkSitId = managerSdkSitId,
                RequestComment = model.Comment,
                CreatedByUserId = userService.UserId,
                UpdatedByUserId = userService.UserId
            };
            await request.Create(dbContext);

            foreach (var date in dates)
            {
                var day = new AbsenceRequestDay
                {
                    AbsenceRequestId = request.Id,
                    Date = date,
                    MessageId = model.MessageId,
                    CreatedByUserId = userService.UserId,
                    UpdatedByUserId = userService.UserId
                };
                await day.Create(dbContext);
                await AbsenceDecisionRules.ApplyAbsenceToPlanRegistrationAsync(
                    dbContext, userService, logger, request, day, managerSdkSitId);
            }

            if (BeforeAbsenceCommit != null)
            {
                await BeforeAbsenceCommit();
            }

            await transaction.CommitAsync();
            return request.Id;
        });
    }

    // ---------------------------------------------------------------- import

    public async Task<OperationDataResult<PlannerImportPreviewModel>> GetImportPreviewAsync()
    {
        try
        {
            if (await GoogleSheetRetirement.IsRetiredAsync(dbContext))
            {
                return Refused<PlannerImportPreviewModel>("PlannerGoogleSheetAlreadyRetired");
            }

            var workers = await dbContext.AssignedSites.AsNoTracking()
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .Where(x => x.UseGoogleSheetAsDefault && !x.Resigned)
                .OrderBy(x => x.Id)
                .ToListAsync();
            var siteIds = workers.Select(x => x.SiteId).Distinct().ToList();

            var windowStart = MondayOf(Today).AddDays(-7 * RotationInference.HistoryWeeks);
            var rowsBySite = (await dbContext.PlanRegistrations.AsNoTracking()
                    .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                    .Where(x => siteIds.Contains(x.SdkSitId))
                    .Where(x => x.Date >= windowStart)
                    .OrderBy(x => x.Id)
                    .Select(x => new
                    {
                        x.SdkSitId, x.Date, x.PlanText, x.MessageId,
                        IsAbsence = x.MessageId != null || x.OnVacation || x.Sick || x.OtherAllowedAbsence
                                    || x.AbsenceWithoutPermission
                    })
                    .ToListAsync())
                .GroupBy(x => x.SdkSitId)
                .ToDictionary(g => g.Key, g => g
                    .GroupBy(x => x.Date.Date)
                    .Select(d => d.First())
                    .Select(x => new RotationInference.Day(x.Date, x.PlanText, x.IsAbsence))
                    .ToList());

            var sdkDbContext = (await coreService.GetCore()).DbContextHelper.GetDbContext();
            var names = await sdkDbContext.Sites.AsNoTracking()
                .Where(x => x.MicrotingUid != null && siteIds.Contains(x.MicrotingUid.Value))
                .Select(x => new { Uid = x.MicrotingUid!.Value, x.Name })
                .ToListAsync();

            var preview = new PlannerImportPreviewModel { WindowStart = windowStart };
            foreach (var worker in workers)
            {
                var inferred = RotationInference.Infer(rowsBySite.GetValueOrDefault(worker.SiteId) ?? [], windowStart);
                preview.Workers.Add(new PlannerImportWorkerModel
                {
                    AssignedSiteId = worker.Id,
                    SiteId = worker.SiteId,
                    SiteName = names.FirstOrDefault(x => x.Uid == worker.SiteId)?.Name ?? "",
                    Recognised = inferred.Recognised,
                    Reason = inferred.Reason,
                    RotationWeeks = inferred.Recognised ? inferred.RotationWeeks : null,
                    AnchorDate = inferred.Recognised ? inferred.AnchorDate : null,
                    Days = inferred.Days
                });
            }

            return new OperationDataResult<PlannerImportPreviewModel>(true, preview);
        }
        catch (Exception e)
        {
            return Failed<PlannerImportPreviewModel>(e, "Previewing the Google Sheet import failed");
        }
    }

    public async Task<OperationDataResult<PlannerImportApplyResultModel>> ApplyImportAsync(PlannerImportApplyModel model)
    {
        try
        {
            if (await GoogleSheetRetirement.IsRetiredAsync(dbContext))
            {
                return Refused<PlannerImportApplyResultModel>("PlannerGoogleSheetAlreadyRetired");
            }

            var imports = new List<PlanPatternImport>();
            foreach (var pattern in model.Patterns ?? [])
            {
                var days = NormaliseDays(pattern.Days);
                if (days == null)
                {
                    return Refused<PlannerImportApplyResultModel>("PlannerShortFormInvalid");
                }

                imports.Add(new PlanPatternImport
                {
                    AssignedSiteId = pattern.AssignedSiteId,
                    RotationWeeks = pattern.RotationWeeks,
                    AnchorDate = pattern.AnchorDate,
                    ValidFrom = pattern.ValidFrom?.Date ?? Today,
                    Days = days
                });
            }

            var imported = await planPatternApplyService.ImportPatternsAndRetireSheetAsync(imports);
            if (!imported.Success)
            {
                return Refused<PlannerImportApplyResultModel>(PlannerKeyFor(imported.Message));
            }

            return new OperationDataResult<PlannerImportApplyResultModel>(true, new PlannerImportApplyResultModel
            {
                Workers = imports.Zip(imported.Model!, (import, result) => new PlannerImportAppliedModel
                {
                    AssignedSiteId = import.AssignedSiteId,
                    PlanPatternId = result.PlanPatternId,
                    RowsWritten = result.RowsWritten,
                    LockedDays = result.LockedDays,
                    Warning = result.WarningKey == null ? null : localizationService.GetString(result.WarningKey)
                }).ToList()
            });
        }
        catch (Exception e)
        {
            return Failed<PlannerImportApplyResultModel>(e, "Applying the Google Sheet import failed");
        }
    }

    // -------------------------------------------------------------- settings

    public async Task<OperationDataResult<PlannerSettingsModel>> GetSettingsAsync()
    {
        try
        {
            return new OperationDataResult<PlannerSettingsModel>(true, new PlannerSettingsModel
            {
                GoogleSheetRetired = await GoogleSheetRetirement.IsRetiredAsync(dbContext)
            });
        }
        catch (Exception e)
        {
            return Failed<PlannerSettingsModel>(e, "Reading the planner settings failed");
        }
    }

    /// <summary>
    /// The escape hatch ("Brug Google Sheet igen"). Only the flag changes:
    /// patterns stay, and workers return to the sheet only where their
    /// UseGoogleSheetAsDefault is set again.
    /// </summary>
    public async Task<OperationResult> UpdateSettingsAsync(PlannerSettingsModel model)
    {
        try
        {
            await GoogleSheetRetirement.SetRetiredAsync(dbContext, model.GoogleSheetRetired, userService.UserId);
            return new OperationResult(true, localizationService.GetString("PlannerSettingsSaved"));
        }
        catch (Exception e)
        {
            return Failed(e, "Saving the planner settings failed");
        }
    }

    // --------------------------------------------------------------- helpers

    /// <summary>The caller's AssignedSites, as the planning board scopes them.</summary>
    private async Task<(List<AssignedSite> Sites, string? ErrorKey)> ScopedSitesAsync()
    {
        var sdkDbContext = (await coreService.GetCore()).DbContextHelper.GetDbContext();
        var assignedSites = await dbContext.AssignedSites.AsNoTracking()
            .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
            .ToListAsync();
        var scope = await SiteScopeResolver.ResolveForCurrentUserAsync(
            assignedSites, dbContext, sdkDbContext, baseDbContext, userService);
        return scope.ErrorKey != null ? ([], scope.ErrorKey) : (scope.ScopedSites, null);
    }

    /// <summary>The worker when the caller may plan them; else the key to refuse with.</summary>
    private async Task<(AssignedSite? Site, string? ErrorKey)> ScopedSiteAsync(int assignedSiteId)
    {
        var (sites, errorKey) = await ScopedSitesAsync();
        if (errorKey != null)
        {
            return (null, errorKey);
        }

        var site = sites.FirstOrDefault(x => x.Id == assignedSiteId);
        return site == null ? (null, "PlannerWorkerNotFound") : (site, null);
    }

    /// <summary>Short forms to canonical text ("" for no plan); null when one is not a plan.</summary>
    private static List<PlanPatternDayModel>? NormaliseDays(IEnumerable<PlanPatternDayModel>? days)
    {
        var normalised = new List<PlanPatternDayModel>();
        foreach (var day in days ?? [])
        {
            var parsed = PlannerShortForm.Parse(day.PlanText);
            if (parsed.Kind == PlannerShortFormKind.Invalid)
            {
                return null;
            }

            normalised.Add(new PlanPatternDayModel
            {
                WeekIndex = day.WeekIndex,
                DayOfWeek = day.DayOfWeek,
                PlanText = parsed.PlanText
            });
        }

        return normalised;
    }

    private static PlannerPatternModel ToModel(PlanPattern pattern) => new()
    {
        PlanPatternId = pattern.Id,
        AssignedSiteId = pattern.AssignedSiteId,
        IsPrefill = false,
        RotationWeeks = pattern.RotationWeeks,
        AnchorDate = pattern.AnchorDate.Date,
        ValidFrom = pattern.ValidFrom.Date,
        Days = FullGrid(pattern.RotationWeeks, pattern.Days.Select(ToModel))
    };

    private static PlanPatternDayModel ToModel(PlanPatternDay day) => new()
    {
        WeekIndex = day.WeekIndex,
        DayOfWeek = day.DayOfWeek,
        PlanText = day.PlanText ?? ""
    };

    /// <summary>Every (week, weekday) of the rotation in order; a day not stored is "" (no plan).</summary>
    private static List<PlanPatternDayModel> FullGrid(int rotationWeeks, IEnumerable<PlanPatternDayModel> days)
    {
        var byCell = days
            .GroupBy(d => (d.WeekIndex, d.DayOfWeek))
            .ToDictionary(g => g.Key, g => g.First().PlanText ?? "");
        return Enumerable.Range(0, rotationWeeks)
            .SelectMany(week => Enumerable.Range(0, 7).Select(dow => new PlanPatternDayModel
            {
                WeekIndex = week,
                DayOfWeek = dow,
                PlanText = byCell.GetValueOrDefault((week, dow), "")
            }))
            .ToList();
    }

    private static readonly string[] TemplateDayNames =
        ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"];

    private static readonly string[] TemplateShiftSuffixes = ["", "2NdShift", "3RdShift", "4ThShift", "5ThShift"];

    /// <summary>
    /// The weekday template's shifts for ISO weekday <paramref name="dow"/>
    /// as canonical plan text (decision 6: a new pattern starts from it). A
    /// site that plans hours only has no shifts and so prefills as "".
    /// </summary>
    private static string TemplatePlanText(AssignedSite assignedSite, int dow)
    {
        var type = typeof(AssignedSite);
        int? Read(string name) => (int?)type.GetProperty(name)?.GetValue(assignedSite);
        var day = TemplateDayNames[dow];
        var shifts = TemplateShiftSuffixes
            .Select(suffix => (PlanTextParser.Shift?)new PlanTextParser.Shift(
                Read($"Start{day}{suffix}") ?? 0,
                Read($"End{day}{suffix}") ?? 0,
                Read($"Break{day}{suffix}") ?? 0));
        return PlanTextParser.Generate(shifts);
    }

    private static int IsoDayOfWeek(DateTime date) => ((int)date.DayOfWeek + 6) % 7;

    /// <summary>
    /// The rotation week <paramref name="date"/> falls in, by the rule
    /// PlanPatternResolver uses: whole weeks from the anchor, modulo the
    /// rotation, positive for dates before the anchor.
    /// </summary>
    private static int RotationWeekIndex(DateTime anchorDate, int rotationWeeks, DateTime date)
    {
        var rotationDays = rotationWeeks * 7;
        var dayOffset = (date.Date - anchorDate.Date).Days;
        return ((dayOffset % rotationDays) + rotationDays) % rotationDays / 7;
    }

    private static DateTime MondayOf(DateTime date) => date.Date.AddDays(-IsoDayOfWeek(date));

    private void AddError(PlannerCellsUpdateResultModel result, int assignedSiteId, string key) =>
        result.Errors.Add(new PlannerWorkerErrorModel
        {
            AssignedSiteId = assignedSiteId,
            Key = key,
            Message = localizationService.GetString(key)
        });

    /// <summary>
    /// Carries the worker's flex balance from <paramref name="from"/> through
    /// their later rows, once. The writes are saved, so a failed walk is
    /// reported, not returned as a failure (as the absence approval does).
    /// </summary>
    private async Task CarryFlexForwardAsync(AssignedSite assignedSite, DateTime from)
    {
        try
        {
            await FlexChainRecompute.RunForwardAsync(dbContext, assignedSite, assignedSite.SiteId, from);
        }
        catch (Exception e)
        {
            dbContext.ChangeTracker.Clear();
            SentrySdk.CaptureException(e);
            logger.LogError(e,
                "Planner change saved, but carrying the flex balance forward FAILED for worker {SdkSitId} from {From:yyyy-MM-dd} — MANUAL RECONCILIATION required",
                assignedSite.SiteId, from);
        }
    }

    private OperationDataResult<T> Refused<T>(string key) =>
        new(false, localizationService.GetString(key));

    private OperationDataResult<T> Failed<T>(Exception e, string what)
    {
        LogFailure(e, what);
        return new OperationDataResult<T>(false, localizationService.GetString("PlannerErrorOccurred"));
    }

    private OperationResult Failed(Exception e, string what)
    {
        LogFailure(e, what);
        return new OperationResult(false, localizationService.GetString("PlannerErrorOccurred"));
    }

    /// <summary>
    /// PlanPatternApplyService reports a missing worker as the literal
    /// "AssignedSite not found", which is not a localization key; the planner
    /// says it with its own key. Every other message it returns is a key.
    /// </summary>
    private static string PlannerKeyFor(string applyServiceMessage) =>
        applyServiceMessage == "AssignedSite not found" ? "PlannerWorkerNotFound" : applyServiceMessage;

    private void LogFailure(Exception e, string what)
    {
        SentrySdk.CaptureException(e);
        logger.LogError(e, "[Planner] {What}", what);
    }
}
