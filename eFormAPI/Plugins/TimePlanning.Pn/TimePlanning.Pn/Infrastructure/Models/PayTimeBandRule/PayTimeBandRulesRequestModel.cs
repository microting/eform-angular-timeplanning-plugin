/*
The MIT License (MIT)

Copyright (c) 2007 - 2021 Microting A/S
*/

namespace TimePlanning.Pn.Infrastructure.Models.PayTimeBandRule;

public class PayTimeBandRulesRequestModel
{
    public int Offset { get; set; }
    public int PageSize { get; set; } = 30;
    public int? PayDayTypeRuleId { get; set; } // Optional filter by PayDayTypeRule
}
