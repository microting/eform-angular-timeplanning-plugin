/*
The MIT License (MIT)

Copyright (c) 2007 - 2021 Microting A/S
*/

namespace TimePlanning.Pn.Infrastructure.Models.PayDayTypeRule;

public class PayDayTypeRulesRequestModel
{
    public int Offset { get; set; }
    public int PageSize { get; set; } = 30;
    public int? PayRuleSetId { get; set; } // Optional filter by PayRuleSet
}
