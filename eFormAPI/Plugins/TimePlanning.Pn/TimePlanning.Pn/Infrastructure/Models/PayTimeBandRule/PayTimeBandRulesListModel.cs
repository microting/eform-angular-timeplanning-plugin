/*
The MIT License (MIT)

Copyright (c) 2007 - 2021 Microting A/S
*/

namespace TimePlanning.Pn.Infrastructure.Models.PayTimeBandRule;

using System.Collections.Generic;

public class PayTimeBandRulesListModel
{
    public int Total { get; set; }
    public List<PayTimeBandRuleSimpleModel> PayTimeBandRules { get; set; }
}
