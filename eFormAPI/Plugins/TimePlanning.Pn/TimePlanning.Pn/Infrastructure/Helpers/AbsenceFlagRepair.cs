#nullable enable
namespace TimePlanning.Pn.Infrastructure.Helpers;

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microting.eForm.Infrastructure.Constants;
using Microting.TimePlanningBase.Infrastructure.Data;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using Sentry;
using Services.AbsenceRequestService;

/// <summary>
/// Idempotent start-up repair (kanban #5583, follow-up to #5568). Before #5568,
/// approval mapped the absence flag from Message.Name, so on base-seeded DBs
/// message ids 7/8 (Barns 1./2. sygedag) and 9 (Tidsoff) were stored as
/// OtherAllowedAbsence. This re-applies <see cref="AbsenceMessageFlags"/> to the
/// days of approved requests whose flags disagree with it. Only the four absence
/// flags change: plan hours and shifts are untouched. Locked days are skipped and
/// reported for manual reconciliation.
///
/// No flex walk: the flags are not an input to the flex chain (FlexChain /
/// FlexChainRecompute read only netto, plan and the SumFlex columns), so
/// changing them cannot change any flex value.
/// </summary>
public static class AbsenceFlagRepair
{
    /// <summary>Returns the number of days fixed.</summary>
    public static async Task<int> Run(TimePlanningPnDbContext dbContext)
    {
        // Arrays so EF expands each Contains to an IN list.
        var vacation = AbsenceMessageFlags.VacationMessageIds.ToArray();
        var sick = AbsenceMessageFlags.SickMessageIds.ToArray();
        var other = AbsenceMessageFlags.OtherAllowedAbsenceMessageIds.ToArray();
        var known = vacation.Concat(sick).Concat(other).ToArray();

        // One query; the mismatch test runs in the database, so a clean DB
        // returns nothing. Only days still carrying the approved message are
        // considered (a later manual change of the day's message is left alone),
        // and unknown ids are skipped since their flag was never name-dependent.
        var candidates = await (
                from pr in dbContext.PlanRegistrations
                join day in dbContext.AbsenceRequestDays
                    on new { RequestId = pr.AbsenceRequestId, pr.Date }
                    equals new { RequestId = (int?)day.AbsenceRequestId, day.Date }
                where pr.WorkflowState != Constants.WorkflowStates.Removed
                      && day.WorkflowState != Constants.WorkflowStates.Removed
                      && day.AbsenceRequest.WorkflowState != Constants.WorkflowStates.Removed
                      && day.AbsenceRequest.Status == AbsenceRequestStatus.Approved
                      && pr.SdkSitId == day.AbsenceRequest.RequestedBySdkSitId
                      && pr.MessageId == day.MessageId
                      && known.Contains(day.MessageId)
                      && (pr.AbsenceWithoutPermission
                          || pr.OnVacation != vacation.Contains(day.MessageId)
                          || pr.Sick != sick.Contains(day.MessageId)
                          || pr.OtherAllowedAbsence != other.Contains(day.MessageId))
                orderby pr.SdkSitId, pr.Date
                select new { PlanRegistration = pr, day.MessageId })
            .AsTracking()
            .ToListAsync()
            .ConfigureAwait(false);

        if (candidates.Count == 0)
        {
            return 0;
        }

        var lockedThroughBySite = await DayLockHelper
            .LockedThroughForSitesAsync(dbContext,
                candidates.Select(c => c.PlanRegistration.SdkSitId).Distinct().ToList())
            .ConfigureAwait(false);

        var fixedCount = 0;
        var lockedCount = 0;
        foreach (var candidate in candidates)
        {
            var pr = candidate.PlanRegistration;
            if (DayLockHelper.IsLocked(lockedThroughBySite, pr.SdkSitId, pr.Date))
            {
                // Payroll may already have used this day, so it is reported, not changed.
                var message =
                    $"[AbsenceFlagRepair] WARNING: day {pr.Date:yyyy-MM-dd} of worker {pr.SdkSitId} (PlanRegistration {pr.Id}, absence request {pr.AbsenceRequestId}) has the wrong absence flag for message {candidate.MessageId} but is locked; MANUAL RECONCILIATION required.";
                Console.WriteLine(message);
                SentrySdk.CaptureMessage(message, SentryLevel.Warning);
                lockedCount++;
                continue;
            }

            AbsenceMessageFlags.Apply(pr, candidate.MessageId);
            await pr.Update(dbContext).ConfigureAwait(false);
            fixedCount++;
        }

        Console.WriteLine(
            $"[AbsenceFlagRepair] fixed {fixedCount} day(s), skipped {lockedCount} locked day(s).");
        return fixedCount;
    }
}
