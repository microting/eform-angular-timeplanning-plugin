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
namespace TimePlanning.Pn.Infrastructure.Models.WorkingHours.Index
{
    using System;

    /// <summary>
    /// TimePlanningWorkingHoursModel
    /// </summary>
    public class TimePlanningWorkingHoursModel
    {
        public int SdkSiteId { get; set; }
        public string WorkerName { get; set; }
        public int WeekDay { get; set; }
        public DateTime Date { get; set; }
        public string PlanText { get; set; }
        public double PlanHours { get; set; }
        public int? Shift1Start { get; set; }
        public int? Shift1Pause { get; set; }
        public int? Shift1Stop { get; set; }
        public int? Shift2Start { get; set; }
        public int? Shift2Pause { get; set; }
        public int? Shift2Stop { get; set; }
        public double NettoHours { get; set; }
        public double FlexHours { get; set; }
        public double SumFlexStart { get; set; }
        public double SumFlexEnd { get; set; }
        public double PaidOutFlex { get; set; }
        public int? Message { get; set; }
        public string CommentWorker { get; set; }
        public string CommentOffice { get; set; }
        public string CommentOfficeAll { get; set; }
        public bool IsLocked { get; set; }
        public bool IsWeekend { get; set; }
        public DateTime? Start1StartedAt { get; set; }
        public DateTime? Stop1StoppedAt { get; set; }
        public DateTime? Pause1StartedAt { get; set; }
        public DateTime? Pause1StoppedAt { get; set; }
        public DateTime? Start2StartedAt { get; set; }
        public DateTime? Stop2StoppedAt { get; set; }
        public DateTime? Pause2StartedAt { get; set; }
        public DateTime? Pause2StoppedAt { get; set; }
    }
}