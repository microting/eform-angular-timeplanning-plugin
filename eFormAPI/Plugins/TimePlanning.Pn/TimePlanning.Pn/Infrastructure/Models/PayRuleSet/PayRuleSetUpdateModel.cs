/*
The MIT License (MIT)

Copyright (c) 2007 - 2021 Microting A/S
*/

namespace TimePlanning.Pn.Infrastructure.Models.PayRuleSet;

using System.Collections.Generic;
using PayDayTypeRule;

public class PayRuleSetUpdateModel
{
    // Id is passed in the URL route, not in the model body
    public string Name { get; set; }
    public List<PayDayRuleModel> PayDayRules { get; set; } = new List<PayDayRuleModel>();
    public List<PayDayTypeRuleModel> PayDayTypeRules { get; set; } = new List<PayDayTypeRuleModel>();
}