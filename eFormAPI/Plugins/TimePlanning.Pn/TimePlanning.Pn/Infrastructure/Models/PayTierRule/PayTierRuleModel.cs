/*
The MIT License (MIT)

Copyright (c) 2007 - 2021 Microting A/S
*/

namespace TimePlanning.Pn.Infrastructure.Models.PayTierRule;

public class PayTierRuleModel
{
    public int Id { get; set; }
    public int PayDayRuleId { get; set; }
    public int Order { get; set; }
    public int? UpToSeconds { get; set; }
    public string PayCode { get; set; }
}
