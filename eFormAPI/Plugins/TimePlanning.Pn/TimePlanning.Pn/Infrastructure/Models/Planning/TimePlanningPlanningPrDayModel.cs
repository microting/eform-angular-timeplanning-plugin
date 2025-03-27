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
    public bool WorkDayStarted { get; set; }
    public bool WorkDayEnded { get; set; }
    public bool PlanHoursMatched { get; set; }
    public int PlannedStartOfShift1 { get; set; }
    public int PlannedEndOfShift1 { get; set; }
    public int PlannedBreakOfShift1 { get; set; }
    public int PlannedStartOfShift2 { get; set; }
    public int PlannedEndOfShift2 { get; set; }
    public int PlannedBreakOfShift2 { get; set; }
    public bool IsDoubleShift { get; set; }
    public bool OnVacation { get; set; }
    public bool Sick { get; set; }
    public bool OtherAllowedAbsence { get; set; }
    public bool AbsenceWithoutPermission { get; set; }
    public DateTime? Start1StartedAt { get; set; }
    public DateTime? Stop1StoppedAt { get; set; }
    public DateTime? Start2StartedAt { get; set; }
    public DateTime? Stop2StoppedAt { get; set; }
    public int Start1Id { get; set; }
    public int Stop1Id { get; set; }
    public int Start2Id { get; set; }
    public int Stop2Id { get; set; }
    public int Break1Shift { get; set; }
    public int Break2Shift { get; set; }
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
}