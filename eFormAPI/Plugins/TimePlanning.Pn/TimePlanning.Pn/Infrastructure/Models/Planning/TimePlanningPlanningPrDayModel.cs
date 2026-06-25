/*
The MIT License (MIT)

Copyright (c) 2007 - 2021 Microting A/S

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

using System;

namespace TimePlanning.Pn.Infrastructure.Models.Planning;

public class TimePlanningPlanningPrDayModel
{
    public int Id { get; set; }
    public int SiteId { get; set; }
    public string SiteName { get; set; }
    public int WeekDay { get; set; }
    public DateTime Date { get; set; }
    public string PlanText { get; set; }
    public double PlanHours { get; set; }
    public double ActualHours { get; set; }
    public double Difference { get; set; }
    public double PauseMinutes { get; set; }
    public int? Message { get; set; }
    public string? MessageLabel { get; set; }
    public bool WorkDayStarted { get; set; }
    public bool WorkDayEnded { get; set; }
    public bool PlanHoursMatched { get; set; }
    public int PlannedStartOfShift1 { get; set; }
    public int PlannedEndOfShift1 { get; set; }
    public int PlannedBreakOfShift1 { get; set; }
    public int PlannedStartOfShift2 { get; set; }
    public int PlannedEndOfShift2 { get; set; }
    public int PlannedBreakOfShift2 { get; set; }
    public int PlannedStartOfShift3 { get; set; }
    public int PlannedEndOfShift3 { get; set; }
    public int PlannedBreakOfShift3 { get; set; }
    public int PlannedStartOfShift4 { get; set; }
    public int PlannedEndOfShift4 { get; set; }
    public int PlannedBreakOfShift4 { get; set; }
    public int PlannedStartOfShift5 { get; set; }
    public int PlannedEndOfShift5 { get; set; }
    public int PlannedBreakOfShift5 { get; set; }
    public bool IsDoubleShift { get; set; }
    public bool OnVacation { get; set; }
    public bool Sick { get; set; }
    public bool OtherAllowedAbsence { get; set; }
    public bool AbsenceWithoutPermission { get; set; }
    public DateTime? Start1StartedAt { get; set; }
    public DateTime? Stop1StoppedAt { get; set; }
    public DateTime? Start2StartedAt { get; set; }
    public DateTime? Stop2StoppedAt { get; set; }
    public int? Start1Id { get; set; }

    public int? Stop1Id { get; set; }

    public int? Pause1Id { get; set; }

    public int? Start2Id { get; set; }

    public int? Stop2Id { get; set; }

    public int? Pause2Id { get; set; }

    public int? Start3Id { get; set; }

    public int? Stop3Id { get; set; }

    public int? Pause3Id { get; set; }

    public int? Start4Id { get; set; }

    public int? Stop4Id { get; set; }

    public int? Pause4Id { get; set; }

    public int? Start5Id { get; set; }

    public int? Stop5Id { get; set; }

    public int? Pause5Id { get; set; }

    // Request-only: exact-minute pause durations under UseOneMinuteIntervals=true.
    // Null means the client did not send the new field; backend falls back to the
    // legacy Pause*Id (5-minute slot) write path. When set, backend translates the
    // duration into Pause*StartedAt/Pause*StoppedAt timestamp pairs (anchor: existing
    // Pause*StartedAt if present, else shift midpoint).
    public int? Pause1ExactMinutes { get; set; }
    public int? Pause2ExactMinutes { get; set; }
    public int? Pause3ExactMinutes { get; set; }
    public int? Pause4ExactMinutes { get; set; }
    public int? Pause5ExactMinutes { get; set; }

    // Request-only: exact-minute start/stop times under UseOneMinuteIntervals=true.
    // Null means the client did not send the new field; backend falls back to the
    // legacy Start*Id/Stop*Id (5-minute slot) write path. When set, backend
    // translates the minutes-of-day into Start*StartedAt/Stop*StoppedAt timestamps
    // (anchor: planning.Date; cross-midnight on Stop when value <= matching Start).
    public int? Start1ExactMinutes { get; set; }
    public int? Start2ExactMinutes { get; set; }
    public int? Start3ExactMinutes { get; set; }
    public int? Start4ExactMinutes { get; set; }
    public int? Start5ExactMinutes { get; set; }
    public int? Stop1ExactMinutes { get; set; }
    public int? Stop2ExactMinutes { get; set; }
    public int? Stop3ExactMinutes { get; set; }
    public int? Stop4ExactMinutes { get; set; }
    public int? Stop5ExactMinutes { get; set; }

    public int Break1Shift { get; set; }
    public int Break2Shift { get; set; }
    public int Break3Shift { get; set; }
    public int Break4Shift { get; set; }
    public int Break5Shift { get; set; }

    // Admin/manual pause override (Approach C). null = compute pause from the
    // recorded slots (current behavior); non-null = authoritative total pause
    // MINUTES for that shift. The web workday dialog sets these explicitly; on
    // read they are populated from the entity so the dialog can show the value.
    // The worker's recorded Pause*StartedAt/StoppedAt are never destroyed.
    public int? Pause1OverrideMinutes { get; set; }
    public int? Pause2OverrideMinutes { get; set; }
    public int? Pause3OverrideMinutes { get; set; }
    public int? Pause4OverrideMinutes { get; set; }
    public int? Pause5OverrideMinutes { get; set; }

    // Clear-to-null capability (FIX 2 — plumbing for the Phase 3 web affordance).
    // The override fields above are int? so "not sent" and "explicit null/clear"
    // are indistinguishable on the wire. These companion signals let the web path
    // explicitly distinguish CLEAR (revert to compute-from-slots) from NOT-SENT
    // (leave the inference / existing override untouched):
    //   • Pause{N}OverrideMinutesSpecified == true  → honor Pause{N}OverrideMinutes
    //     for that shift exactly (a value sets it; null clears to compute-from-slots)
    //     and SKIP inference for that shift.
    //   • ClearPauseOverrides == true → clear ALL five shifts to null in one shot
    //     (coarse convenience signal; takes precedence over per-shift signals).
    // The app/inference path does NOT need to clear — Break{N}Shift == 0 → override
    // 0 is sufficient there. The Phase 3 web UI wires the clear affordance to these.
    public bool Pause1OverrideMinutesSpecified { get; set; }
    public bool Pause2OverrideMinutesSpecified { get; set; }
    public bool Pause3OverrideMinutesSpecified { get; set; }
    public bool Pause4OverrideMinutesSpecified { get; set; }
    public bool Pause5OverrideMinutesSpecified { get; set; }
    public bool ClearPauseOverrides { get; set; }
    public string CommentOffice { get; set; }
    public string WorkerComment { get; set; }
    public double SumFlexStart { get; set; }
    public double SumFlexEnd { get; set; }
    public double PaidOutFlex { get; set; }
    public DateTime? Pause1StartedAt { get; set; }
    public DateTime? Pause1StoppedAt { get; set; }
    public DateTime? Pause2StartedAt { get; set; }
    public DateTime? Pause2StoppedAt { get; set; }
    public DateTime? Pause10StartedAt { get; set; }
    public DateTime? Pause10StoppedAt { get; set; }
    public DateTime? Pause11StartedAt { get; set; }
    public DateTime? Pause11StoppedAt { get; set; }
    public DateTime? Pause12StartedAt { get; set; }
    public DateTime? Pause12StoppedAt { get; set; }
    public DateTime? Pause13StartedAt { get; set; }
    public DateTime? Pause13StoppedAt { get; set; }
    public DateTime? Pause14StartedAt { get; set; }
    public DateTime? Pause14StoppedAt { get; set; }
    public DateTime? Pause15StartedAt { get; set; }
    public DateTime? Pause15StoppedAt { get; set; }
    public DateTime? Pause16StartedAt { get; set; }
    public DateTime? Pause16StoppedAt { get; set; }
    public DateTime? Pause17StartedAt { get; set; }
    public DateTime? Pause17StoppedAt { get; set; }
    public DateTime? Pause18StartedAt { get; set; }
    public DateTime? Pause18StoppedAt { get; set; }
    public DateTime? Pause19StartedAt { get; set; }
    public DateTime? Pause19StoppedAt { get; set; }
    public DateTime? Pause100StartedAt { get; set; }
    public DateTime? Pause100StoppedAt { get; set; }
    public DateTime? Pause101StartedAt { get; set; }
    public DateTime? Pause101StoppedAt { get; set; }
    public DateTime? Pause102StartedAt { get; set; }
    public DateTime? Pause102StoppedAt { get; set; }
    public DateTime? Pause20StartedAt { get; set; }
    public DateTime? Pause20StoppedAt { get; set; }
    public DateTime? Pause21StartedAt { get; set; }
    public DateTime? Pause21StoppedAt { get; set; }
    public DateTime? Pause22StartedAt { get; set; }
    public DateTime? Pause22StoppedAt { get; set; }
    public DateTime? Pause23StartedAt { get; set; }
    public DateTime? Pause23StoppedAt { get; set; }
    public DateTime? Pause24StartedAt { get; set; }
    public DateTime? Pause24StoppedAt { get; set; }
    public DateTime? Pause25StartedAt { get; set; }
    public DateTime? Pause25StoppedAt { get; set; }
    public DateTime? Pause26StartedAt { get; set; }
    public DateTime? Pause26StoppedAt { get; set; }
    public DateTime? Pause27StartedAt { get; set; }
    public DateTime? Pause27StoppedAt { get; set; }
    public DateTime? Pause28StartedAt { get; set; }
    public DateTime? Pause28StoppedAt { get; set; }
    public DateTime? Pause29StartedAt { get; set; }
    public DateTime? Pause29StoppedAt { get; set; }
    public DateTime? Pause200StartedAt { get; set; }
    public DateTime? Pause200StoppedAt { get; set; }
    public DateTime? Pause201StartedAt { get; set; }
    public DateTime? Pause201StoppedAt { get; set; }
    public DateTime? Pause202StartedAt { get; set; }
    public DateTime? Pause202StoppedAt { get; set; }
    public DateTime? Start3StartedAt { get; set; }
    public DateTime? Stop3StoppedAt { get; set; }
    public DateTime? Pause3StartedAt { get; set; }
    public DateTime? Pause3StoppedAt { get; set; }
    public DateTime? Start4StartedAt { get; set; }
    public DateTime? Stop4StoppedAt { get; set; }
    public DateTime? Pause4StartedAt { get; set; }
    public DateTime? Pause4StoppedAt { get; set; }
    public DateTime? Start5StartedAt { get; set; }
    public DateTime? Stop5StoppedAt { get; set; }
    public DateTime? Pause5StartedAt { get; set; }
    public DateTime? Pause5StoppedAt { get; set; }
    public double NettoHoursOverride { get; set; }
    public bool NettoHoursOverrideActive { get; set; }
}