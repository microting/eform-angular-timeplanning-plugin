/*
The MIT License (MIT)

Copyright (c) 2007 - 2021 Microting A/S
*/

namespace TimePlanning.Pn.Infrastructure.Models.PayDayTypeRule;

using System.Collections.Generic;

public class PayDayTypeRulesListModel
{
    public int Total { get; set; }
    public List<PayDayTypeRuleSimpleModel> PayDayTypeRules { get; set; }
}
