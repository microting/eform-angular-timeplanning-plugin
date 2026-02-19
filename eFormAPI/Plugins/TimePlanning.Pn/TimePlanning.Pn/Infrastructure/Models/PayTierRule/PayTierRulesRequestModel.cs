/*
The MIT License (MIT)

Copyright (c) 2007 - 2021 Microting A/S
*/

namespace TimePlanning.Pn.Infrastructure.Models.PayTierRule;

public class PayTierRulesRequestModel
{
    public int Offset { get; set; }
    public int PageSize { get; set; } = 30;
    public int? PayDayRuleId { get; set; } // Optional filter by PayDayRule
}
