/*
The MIT License (MIT)

Copyright (c) 2007 - 2021 Microting A/S
*/

namespace TimePlanning.Pn.Infrastructure.Models.PayDayTypeRule;

using System.Collections.Generic;
using PayTimeBandRule;

public class PayDayTypeRuleModel
{
    public int? Id { get; set; }
    public int PayRuleSetId { get; set; }
    public string DayType { get; set; } // Monday, Tuesday, Wednesday, Thursday, Friday, Saturday, Sunday, Holiday
    public string DefaultPayCode { get; set; }
    public int Priority { get; set; }
    public List<PayTimeBandRuleModel> TimeBandRules { get; set; } = new List<PayTimeBandRuleModel>();
}