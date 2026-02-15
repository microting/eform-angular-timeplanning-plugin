/*
The MIT License (MIT)

Copyright (c) 2007 - 2021 Microting A/S
*/

namespace TimePlanning.Pn.Infrastructure.Models.PayTimeBandRule;

public class PayTimeBandRuleModel
{
    public int Id { get; set; }
    public int PayDayTypeRuleId { get; set; }
    public int StartSecondOfDay { get; set; }
    public int EndSecondOfDay { get; set; }
    public string PayCode { get; set; }
}
