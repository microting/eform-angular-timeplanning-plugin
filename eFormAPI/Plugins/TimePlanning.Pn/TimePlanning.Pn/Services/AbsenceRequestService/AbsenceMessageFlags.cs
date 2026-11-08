#nullable enable
namespace TimePlanning.Pn.Services.AbsenceRequestService;

using System.Collections.Generic;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using Microting.TimePlanningBase.Infrastructure.Extensions.Seed.CodeFirstLists;

/// <summary>
/// The absence message id → PlanRegistration flag mapping, shared by approval
/// (<see cref="AbsenceRequestService"/>) and the start-up repair
/// (<see cref="Infrastructure.Helpers.AbsenceFlagRepair"/>) so the two can't drift.
/// </summary>
internal static class AbsenceMessageFlags
{
    // Message ids not covered by the base MessagesCodeFirst list (plugin seed only).
    private const int MaternityMessageId = 10;
    private const int VacationDayOffMessageId = 11;
    private const int HolidayMessageId = 12;
    private const int PregnancyLeaveMessageId = 13;

    // Absence type is keyed on Message.Id, which is identical in the base and plugin
    // seeds. Message.Name differs between them ("TimeOff" vs "Time off", etc.).
    internal static readonly HashSet<int> VacationMessageIds =
    [
        MessagesCodeFirst.VacationMessage.Id,
        MessagesCodeFirst.TimeOffMessage.Id,
        VacationDayOffMessageId
    ];

    internal static readonly HashSet<int> SickMessageIds =
    [
        MessagesCodeFirst.SickMessage.Id,
        MessagesCodeFirst.ChildrenFirstSickMessage.Id,
        MessagesCodeFirst.ChildrenSecondSickMessage.Id
    ];

    internal static readonly HashSet<int> OtherAllowedAbsenceMessageIds =
    [
        MessagesCodeFirst.DayOffMessage.Id,
        MessagesCodeFirst.CourseMessage.Id,
        MessagesCodeFirst.LeaveOfAbsenceMessage.Id,
        MessagesCodeFirst.CareMessage.Id,
        MaternityMessageId,
        HolidayMessageId,
        PregnancyLeaveMessageId
    ];

    /// <summary>
    /// Sets the one absence flag <paramref name="messageId"/> maps to and clears
    /// the others. An unknown id gets OtherAllowedAbsence; returns false for it
    /// so the caller can log.
    /// </summary>
    internal static bool Apply(PlanRegistration pr, int messageId)
    {
        pr.OnVacation = false;
        pr.Sick = false;
        pr.OtherAllowedAbsence = false;
        pr.AbsenceWithoutPermission = false;

        if (VacationMessageIds.Contains(messageId))
        {
            pr.OnVacation = true;
            return true;
        }

        if (SickMessageIds.Contains(messageId))
        {
            pr.Sick = true;
            return true;
        }

        pr.OtherAllowedAbsence = true;
        return OtherAllowedAbsenceMessageIds.Contains(messageId);
    }
}
