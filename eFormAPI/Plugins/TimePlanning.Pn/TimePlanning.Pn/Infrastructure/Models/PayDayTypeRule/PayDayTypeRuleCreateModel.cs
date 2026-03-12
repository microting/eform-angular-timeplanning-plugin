/*
The MIT License (MIT)

Copyright (c) 2007 - 2021 Microting A/S
*/

namespace TimePlanning.Pn.Infrastructure.Models.PayDayTypeRule;

using System.Collections.Generic;
using PayTimeBandRule;

public class PayDayTypeRuleCreateModel
{
    public int PayRuleSetId { get; set; }
    public string DayType { get; set; }
    public string DefaultPayCode { get; set; }
    public int Priority { get; set; }
    public List<PayTimeBandRuleModel> TimeBandRules { get; set; } = new List<PayTimeBandRuleModel>();
}