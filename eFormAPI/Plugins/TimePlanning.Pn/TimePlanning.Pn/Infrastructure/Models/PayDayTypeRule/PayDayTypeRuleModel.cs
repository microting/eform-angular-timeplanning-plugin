/*
The MIT License (MIT)

Copyright (c) 2007 - 2021 Microting A/S
*/

namespace TimePlanning.Pn.Infrastructure.Models.PayDayTypeRule;

public class PayDayTypeRuleModel
{
    public int Id { get; set; }
    public int PayRuleSetId { get; set; }
    public string DayType { get; set; } // Weekday, Saturday, Sunday, PublicHoliday, CompanyHoliday
}
