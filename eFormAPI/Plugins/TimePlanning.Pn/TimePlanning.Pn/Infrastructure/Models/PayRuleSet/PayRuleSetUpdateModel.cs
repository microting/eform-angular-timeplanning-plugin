/*
The MIT License (MIT)

Copyright (c) 2007 - 2021 Microting A/S
*/

namespace TimePlanning.Pn.Infrastructure.Models.PayRuleSet;

using System.Collections.Generic;

public class PayRuleSetUpdateModel
{
    public int Id { get; set; }
    public string Name { get; set; }
    public List<PayDayRuleModel> PayDayRules { get; set; } = new List<PayDayRuleModel>();
}
