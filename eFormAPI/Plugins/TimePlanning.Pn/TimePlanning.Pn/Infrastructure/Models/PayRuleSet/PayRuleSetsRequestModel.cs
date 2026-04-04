/*
The MIT License (MIT)

Copyright (c) 2007 - 2021 Microting A/S
*/

namespace TimePlanning.Pn.Infrastructure.Models.PayRuleSet;

public class PayRuleSetsRequestModel
{
    public int Offset { get; set; }
    public int PageSize { get; set; } = 30;
}
