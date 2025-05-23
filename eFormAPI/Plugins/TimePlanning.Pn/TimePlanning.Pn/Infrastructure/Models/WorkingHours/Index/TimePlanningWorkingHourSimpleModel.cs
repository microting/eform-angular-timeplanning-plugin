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

#nullable enable
namespace TimePlanning.Pn.Infrastructure.Models.WorkingHours.Index;

using System;

/// <summary>
/// TimePlanningWorkingHoursModel
/// </summary>
public class TimePlanningWorkingHourSimpleModel
{
    public string Date { get; set; }
    public string YesterDay { get; set; }
    public string Worker { get; set; }
    public string PlanText { get; set; }
    public double PlanHours { get; set; }
    public double NettoHours { get; set; }
    public double FlexHours { get; set; }
    public string SumFlexStart { get; set; }
    public string SumFlexEnd { get; set; }
    public double PaidOutFlex { get; set; }
    public string Message { get; set; }
    public string CommentWorker { get; set; }
    public string CommentOffice { get; set; }
    public string? Start1StartedAt { get; set; }
    public string? Stop1StoppedAt { get; set; }
    public string? Pause1StartedAt { get; set; }
    public string? Pause1StoppedAt { get; set; }
    public string? Pause1TotalTime { get; set; }
    public string? Start2StartedAt { get; set; }
    public string? Stop2StoppedAt { get; set; }
    public string? Pause2StartedAt { get; set; }
    public string? Pause2StoppedAt { get; set; }
    public string? Pause2TotalTime { get; set; }
}