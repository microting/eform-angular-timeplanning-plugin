using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;
using Microting.TimePlanningBase.Infrastructure.Data;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Models.PayRuleSet;
using TimePlanning.Pn.Infrastructure.Models.PayTierRule;

namespace TimePlanning.Pn.Test;

[TestFixture]
public class PayRuleSetControllerTests : TestBaseSetup
{
    [Test]
    public async Task Create_WithNestedEntities_ReturnsSuccess()
    {
        // Arrange
        var json = @"{
  ""name"": ""Test PayRuleSet via Controller"",
  ""payDayRules"": [
    {
      ""dayCode"": ""MONDAY"",
      ""payTierRules"": [
        {
          ""order"": 1,
          ""upToSeconds"": 28800,
          ""payCode"": ""REG""
        },
        {
          ""order"": 2,
          ""upToSeconds"": null,
          ""payCode"": ""OT""
        }
      ]
    }
  ]
}";

        // Parse and validate JSON structure
        var model = JsonSerializer.Deserialize<PayRuleSetCreateModel>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.That(model, Is.Not.Null);
        Assert.That(model.Name, Is.EqualTo("Test PayRuleSet via Controller"));
        Assert.That(model.PayDayRules, Is.Not.Null);
        Assert.That(model.PayDayRules.Count, Is.EqualTo(1));
        Assert.That(model.PayDayRules[0].DayCode, Is.EqualTo("MONDAY"));
        Assert.That(model.PayDayRules[0].PayTierRules, Is.Not.Null);
        Assert.That(model.PayDayRules[0].PayTierRules.Count, Is.EqualTo(2));
        Assert.That(model.PayDayRules[0].PayTierRules[0].Order, Is.EqualTo(1));
        Assert.That(model.PayDayRules[0].PayTierRules[0].UpToSeconds, Is.EqualTo(28800));
        Assert.That(model.PayDayRules[0].PayTierRules[0].PayCode, Is.EqualTo("REG"));
        
        Console.WriteLine("✅ JSON deserialization successful - model structure is correct");
    }

    [Test]
    public async Task Update_WithNestedEntities_DeserializesCorrectly()
    {
        // Arrange
        var json = @"{
  ""name"": ""Updated via Controller"",
  ""payDayRules"": [
    {
      ""id"": null,
      ""dayCode"": ""SUNDAY"",
      ""payTierRules"": [
        {
          ""id"": null,
          ""order"": 1,
          ""upToSeconds"": null,
          ""payCode"": ""HOLIDAY""
        }
      ]
    }
  ]
}";

        // Parse and validate JSON structure
        var model = JsonSerializer.Deserialize<PayRuleSetUpdateModel>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.That(model, Is.Not.Null);
        Assert.That(model.Name, Is.EqualTo("Updated via Controller"));
        Assert.That(model.PayDayRules, Is.Not.Null);
        Assert.That(model.PayDayRules.Count, Is.EqualTo(1));
        Assert.That(model.PayDayRules[0].DayCode, Is.EqualTo("SUNDAY"));
        Assert.That(model.PayDayRules[0].PayTierRules, Is.Not.Null);
        Assert.That(model.PayDayRules[0].PayTierRules.Count, Is.EqualTo(1));
        Assert.That(model.PayDayRules[0].PayTierRules[0].PayCode, Is.EqualTo("HOLIDAY"));
        
        Console.WriteLine("✅ JSON deserialization successful - update model structure is correct");
    }

    [Test]
    public async Task AngularJSON_Format_DeserializesCorrectly()
    {
        // Arrange - This is the exact JSON Angular is sending
        var angularJson = @"{
  ""name"": ""jk"",
  ""payDayRules"": [
    {
      ""id"": null,
      ""dayCode"": ""SUNDAY"",
      ""payTierRules"": [
        {
          ""id"": null,
          ""order"": 1,
          ""upToSeconds"": null,
          ""payCode"": ""norm""
        }
      ]
    }
  ]
}";

        // Act - Test deserialization with case-insensitive matching (ASP.NET Core default)
        var model = JsonSerializer.Deserialize<PayRuleSetUpdateModel>(angularJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        // Assert
        Assert.That(model, Is.Not.Null, "Model should not be null");
        Assert.That(model.Name, Is.EqualTo("jk"), "Name should match");
        Assert.That(model.PayDayRules, Is.Not.Null, "PayDayRules should not be null");
        Assert.That(model.PayDayRules.Count, Is.EqualTo(1), "Should have 1 PayDayRule");
        
        var dayRule = model.PayDayRules[0];
        Assert.That(dayRule.Id, Is.EqualTo(null), "PayDayRule Id should be null for new rule");
        Assert.That(dayRule.DayCode, Is.EqualTo("SUNDAY"), "DayCode should match");
        Assert.That(dayRule.PayTierRules, Is.Not.Null, "PayTierRules should not be null");
        Assert.That(dayRule.PayTierRules.Count, Is.EqualTo(1), "Should have 1 PayTierRule");
        
        var tierRule = dayRule.PayTierRules[0];
        Assert.That(tierRule.Id, Is.EqualTo(null), "PayTierRule Id should be null for new rule");
        Assert.That(tierRule.Order, Is.EqualTo(1), "Order should be 1");
        Assert.That(tierRule.UpToSeconds, Is.EqualTo(null), "UpToSeconds should be null");
        Assert.That(tierRule.PayCode, Is.EqualTo("norm"), "PayCode should match");
        
        Console.WriteLine("✅ Angular JSON format is VALID and deserializes correctly!");
        Console.WriteLine($"   Name: {model.Name}");
        Console.WriteLine($"   PayDayRules: {model.PayDayRules.Count}");
        Console.WriteLine($"   DayCode: {dayRule.DayCode}");
        Console.WriteLine($"   PayTierRules: {dayRule.PayTierRules.Count}");
        Console.WriteLine($"   PayCode: {tierRule.PayCode}");
    }

    [Test]
    public async Task PropertyCasing_Variations_AllDeserializeCorrectly()
    {
        // Test that ASP.NET Core's default case-insensitive deserialization works
        var testCases = new[]
        {
            // lowercase (Angular default)
            @"{""name"":""test"",""payDayRules"":[{""dayCode"":""MONDAY"",""payTierRules"":[{""order"":1,""payCode"":""REG""}]}]}",
            
            // PascalCase (C# default)
            @"{""Name"":""test"",""PayDayRules"":[{""DayCode"":""MONDAY"",""PayTierRules"":[{""Order"":1,""PayCode"":""REG""}]}]}",
            
            // Mixed case
            @"{""Name"":""test"",""payDayRules"":[{""dayCode"":""MONDAY"",""PayTierRules"":[{""order"":1,""PayCode"":""REG""}]}]}"
        };

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        foreach (var json in testCases)
        {
            var model = JsonSerializer.Deserialize<PayRuleSetCreateModel>(json, options);
            
            Assert.That(model, Is.Not.Null);
            Assert.That(model.Name, Is.EqualTo("test"));
            Assert.That(model.PayDayRules, Is.Not.Null);
            Assert.That(model.PayDayRules.Count, Is.EqualTo(1));
            Assert.That(model.PayDayRules[0].DayCode, Is.EqualTo("MONDAY"));
            Assert.That(model.PayDayRules[0].PayTierRules, Is.Not.Null);
            Assert.That(model.PayDayRules[0].PayTierRules.Count, Is.EqualTo(1));
        }
        
        Console.WriteLine("✅ All property casing variations deserialize correctly");
    }
}
