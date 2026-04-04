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

namespace TimePlanning.Pn.Infrastructure.Models.Settings;

using System.Collections.Generic;

public class TimePlanningSettingsModel
{
    public string GoogleSheetId { get; set; }
    public int MondayBreakMinutesDivider { get; set; }
    public int MondayBreakMinutesPrDivider { get; set; }
    public int TuesdayBreakMinutesDivider { get; set; }
    public int TuesdayBreakMinutesPrDivider { get; set; }
    public int WednesdayBreakMinutesDivider { get; set; }
    public int WednesdayBreakMinutesPrDivider { get; set; }
    public int ThursdayBreakMinutesDivider { get; set; }
    public int ThursdayBreakMinutesPrDivider { get; set; }
    public int FridayBreakMinutesDivider { get; set; }
    public int FridayBreakMinutesPrDivider { get; set; }
    public int SaturdayBreakMinutesDivider { get; set; }
    public int SaturdayBreakMinutesPrDivider { get; set; }
    public int SundayBreakMinutesDivider { get; set; }
    public int SundayBreakMinutesPrDivider { get; set; }
    public bool AutoBreakCalculationActive { get; set; }
    public bool ForceLoadAllPlanningsFromGoogleSheet { get; set; }
    public int MondayBreakMinutesUpperLimit { get; set; }
    public int TuesdayBreakMinutesUpperLimit { get; set; }
    public int WednesdayBreakMinutesUpperLimit { get; set; }
    public int ThursdayBreakMinutesUpperLimit { get; set; }
    public int FridayBreakMinutesUpperLimit { get; set; }
    public int SaturdayBreakMinutesUpperLimit { get; set; }
    public int SundayBreakMinutesUpperLimit { get; set; }
    public int DayOfPayment { get; set; }
    public bool ShowCalculationsAsNumber { get; set; }
    public bool DaysBackInTimeAllowedEditingEnabled { get; set; } = false;
    public int DaysBackInTimeAllowedEditing { get; set; } = 2;
    public bool GpsEnabled { get; set; }
    public bool SnapshotEnabled { get; set; }
}