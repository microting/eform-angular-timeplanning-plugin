using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json.Serialization;

namespace TimePlanning.Pn.Infrastructure.Models.Holiday;

/// <summary>
/// Root model for the Danish holidays JSON configuration.
/// </summary>
public class DanishHolidayConfiguration
{
    [JsonPropertyName("jurisdiction")]
    public string Jurisdiction { get; set; }
    
    [JsonPropertyName("agreement")]
    public string Agreement { get; set; }
    
    [JsonPropertyName("range_inclusive")]
    public DateRange RangeInclusive { get; set; }
    
    [JsonPropertyName("premium_rule_definitions")]
    public Dictionary<string, List<PremiumRuleDefinition>> PremiumRuleDefinitions { get; set; }
    
    [JsonPropertyName("notes")]
    public List<string> Notes { get; set; }
    
    [JsonPropertyName("holidays")]
    public List<HolidayDefinition> Holidays { get; set; }
    
    [JsonPropertyName("excluded")]
    public List<ExcludedHoliday> Excluded { get; set; }
}

public class DateRange
{
    [JsonPropertyName("from")]
    public string From { get; set; }
    
    [JsonPropertyName("to")]
    public string To { get; set; }
}

public class PremiumRuleDefinition
{
    [JsonPropertyName("from")]
    public string From { get; set; }
    
    [JsonPropertyName("to")]
    public string To { get; set; }
    
    [JsonPropertyName("premium_percent")]
    public int PremiumPercent { get; set; }
    
    [JsonPropertyName("basis")]
    public string Basis { get; set; }
    
    [JsonPropertyName("note")]
    public string Note { get; set; }
}

public class HolidayDefinition
{
    [JsonPropertyName("date")]
    public string Date { get; set; }
    
    [JsonPropertyName("name_da")]
    public string NameDa { get; set; }
    
    [JsonPropertyName("category")]
    public string Category { get; set; }
    
    [JsonPropertyName("premium_rule")]
    public string PremiumRule { get; set; }
    
    private readonly object _parseLock = new object();
    private DateTime? _parsedDate;
    
    /// <summary>
    /// Gets the parsed DateTime for this holiday (date only, no time component).
    /// Thread-safe lazy initialization.
    /// </summary>
    public DateTime ParsedDate
    {
        get
        {
            if (!_parsedDate.HasValue)
            {
                lock (_parseLock)
                {
                    if (!_parsedDate.HasValue)
                    {
                        if (DateTime.TryParse(Date, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                        {
                            _parsedDate = new DateTime(parsed.Year, parsed.Month, parsed.Day, 0, 0, 0);
                        }
                        else
                        {
                            throw new FormatException($"Failed to parse holiday date: {Date}");
                        }
                    }
                }
            }
            return _parsedDate.Value;
        }
    }
}

public class ExcludedHoliday
{
    [JsonPropertyName("name_da")]
    public string NameDa { get; set; }
    
    [JsonPropertyName("reason")]
    public string Reason { get; set; }
}
