/*
The MIT License (MIT)

Copyright (c) 2007 - 2021 Microting A/S
*/

namespace TimePlanning.Pn.Infrastructure.Models.PayTierRule;

using System.Collections.Generic;

public class PayTierRulesListModel
{
    public int Total { get; set; }
    public List<PayTierRuleSimpleModel> PayTierRules { get; set; }
}
