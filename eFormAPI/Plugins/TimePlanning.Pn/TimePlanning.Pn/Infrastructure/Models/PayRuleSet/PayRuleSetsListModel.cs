/*
The MIT License (MIT)

Copyright (c) 2007 - 2021 Microting A/S
*/

namespace TimePlanning.Pn.Infrastructure.Models.PayRuleSet;

using System.Collections.Generic;

public class PayRuleSetsListModel
{
    public int Total { get; set; }
    public List<PayRuleSetSimpleModel> PayRuleSets { get; set; }
}
