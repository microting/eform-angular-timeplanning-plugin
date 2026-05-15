/*
The MIT License (MIT)

Copyright (c) 2007 - 2021 Microting A/S
*/

#nullable enable
namespace TimePlanning.Pn.Infrastructure.Models.ContentHandover;

public class HandoverCoworkerModel
{
    public int SdkSiteId { get; set; }
    public string SiteName { get; set; } = string.Empty;
    public int PlanRegistrationId { get; set; }
}
