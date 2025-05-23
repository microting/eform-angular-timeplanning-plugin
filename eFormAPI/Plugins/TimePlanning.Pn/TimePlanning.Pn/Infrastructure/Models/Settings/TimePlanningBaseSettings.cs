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

namespace TimePlanning.Pn.Infrastructure.Models.Settings;

public class TimePlanningBaseSettings
{
    public int? EformId{ get; set; }

    public int? FolderId { get; set; }

    public int? InfoeFormId { get; set; }

    public int? MaxHistoryDays { get; set; }

    public int? MaxDaysEditable { get; set; }
    public string GoogleApiKey { get; set; }
    public string GoogleSheetId { get; set; }
    public string GoogleSheetLastModified { get; set; }
    public string MondayBreakMinutesDivider { get; set; }
    public string MondayBreakMinutesPrDivider { get; set; }
    public string TuesdayBreakMinutesDivider { get; set; }
    public string TuesdayBreakMinutesPrDivider { get; set; }
    public string WednesdayBreakMinutesDivider { get; set; }
    public string WednesdayBreakMinutesPrDivider { get; set; }
    public string ThursdayBreakMinutesDivider { get; set; }
    public string ThursdayBreakMinutesPrDivider { get; set; }
    public string FridayBreakMinutesDivider { get; set; }
    public string FridayBreakMinutesPrDivider { get; set; }
    public string SaturdayBreakMinutesDivider { get; set; }
    public string SaturdayBreakMinutesPrDivider { get; set; }
    public string SundayBreakMinutesDivider { get; set; }
    public string SundayBreakMinutesPrDivider { get; set; }
    public string AutoBreakCalculationActive { get; set; }
    public string MondayBreakMinutesUpperLimit { get; set; }
    public string TuesdayBreakMinutesUpperLimit { get; set; }
    public string WednesdayBreakMinutesUpperLimit { get; set; }
    public string ThursdayBreakMinutesUpperLimit { get; set; }
    public string FridayBreakMinutesUpperLimit { get; set; }
    public string SaturdayBreakMinutesUpperLimit { get; set; }
    public string SundayBreakMinutesUpperLimit { get; set; }
    public string DayOfPayment { get; set; }
    public string ShowCalculationsAsNumber { get; set; }
}