using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;
using Microting.TimePlanningBase.Infrastructure.Data;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Models.BreakPolicy;

namespace TimePlanning.Pn.Test;

[TestFixture]
public class BreakPolicyControllerTests : TestBaseSetup
{
    [Test]
    public async Task Create_WithNestedRules_ReturnsSuccess()
    {
        // Arrange
        var json = @"{
  ""name"": ""Test BreakPolicy via Controller"",
  ""breakPolicyRules"": [
    {
      ""id"": null,
      ""dayOfWeek"": 1,
      ""paidBreakMinutes"": 15,
      ""unpaidBreakMinutes"": 30
    },
    {
      ""id"": null,
      ""dayOfWeek"": 2,
      ""paidBreakMinutes"": 20,
      ""unpaidBreakMinutes"": 30
    }
  ]
}";

        // Parse and validate JSON structure
        var model = JsonSerializer.Deserialize<BreakPolicyCreateModel>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.That(model, Is.Not.Null);
        Assert.That(model.Name, Is.EqualTo("Test BreakPolicy via Controller"));
        Assert.That(model.BreakPolicyRules, Is.Not.Null);
        Assert.That(model.BreakPolicyRules.Count, Is.EqualTo(2));
        Assert.That(model.BreakPolicyRules[0].Id, Is.Null);
        Assert.That(model.BreakPolicyRules[0].DayOfWeek, Is.EqualTo(1));
        Assert.That(model.BreakPolicyRules[0].PaidBreakMinutes, Is.EqualTo(900));
        
        Console.WriteLine("✅ JSON deserialization successful - BreakPolicy model structure is correct");
        await Task.CompletedTask; // Keep async signature
    }

    [Test]
    public async Task Update_WithNestedRules_DeserializesCorrectly()
    {
        // Arrange - Update with new rules
        var json = @"{
  ""name"": ""Updated Policy"",
  ""breakPolicyRules"": [
    {
      ""id"": null,
      ""dayOfWeek"": 1,
      ""paidBreakMinutes"": 15,
      ""unpaidBreakMinutes"": 30
    },
    {
      ""id"": null,
      ""dayOfWeek"": 3,
      ""paidBreakMinutes"": 20,
      ""unpaidBreakMinutes"": 30
    }
  ]
}";

        // Parse and validate JSON structure
        var model = JsonSerializer.Deserialize<BreakPolicyUpdateModel>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        // Validate deserialization worked
        Assert.That(model, Is.Not.Null);
        Assert.That(model.Name, Is.EqualTo("Updated Policy"));
        Assert.That(model.BreakPolicyRules, Is.Not.Null);
        Assert.That(model.BreakPolicyRules.Count, Is.EqualTo(2));
        Assert.That(model.BreakPolicyRules[0].Id, Is.Null);
        Assert.That(model.BreakPolicyRules[0].DayOfWeek, Is.EqualTo(1));
        Assert.That(model.BreakPolicyRules[0].PaidBreakMinutes, Is.EqualTo(900));
        
        Console.WriteLine("✅ Update JSON deserialization successful - model structure is correct");
        await Task.CompletedTask; // Keep async signature
    }

    [Test]
    public async Task AngularJSON_Format_DeserializesCorrectly()
    {
        // This tests the EXACT JSON format that Angular sends
        var angularJson = @"{
  ""name"": ""Test from Angular"",
  ""breakPolicyRules"": [
    {
      ""id"": null,
      ""dayOfWeek"": 1,
      ""paidBreakMinutes"": 15,
      ""unpaidBreakMinutes"": 30
    }
  ]
}";

        // Deserialize - this should NOT throw an exception
        var model = JsonSerializer.Deserialize<BreakPolicyCreateModel>(angularJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        // Verify all properties deserialized correctly
        Assert.That(model, Is.Not.Null, "Model should not be null");
        Assert.That(model.Name, Is.EqualTo("Test from Angular"));
        Assert.That(model.BreakPolicyRules, Is.Not.Null);
        Assert.That(model.BreakPolicyRules.Count, Is.EqualTo(1));
        Assert.That(model.BreakPolicyRules[0].Id, Is.Null, "Id should be null for new entities");
        Assert.That(model.BreakPolicyRules[0].DayOfWeek, Is.EqualTo(1));
        Assert.That(model.BreakPolicyRules[0].PaidBreakMinutes, Is.EqualTo(900));
        Assert.That(model.BreakPolicyRules[0].UnpaidBreakMinutes, Is.EqualTo(1800));

        Console.WriteLine("✅ Angular JSON format is VALID and deserializes correctly!");
        Console.WriteLine($"   Name: {model.Name}");
        Console.WriteLine($"   BreakPolicyRules: {model.BreakPolicyRules.Count}");
        Console.WriteLine($"   DayOfWeek: {model.BreakPolicyRules[0].DayOfWeek}");
        Console.WriteLine($"   PaidBreakMinutes: {model.BreakPolicyRules[0].PaidBreakMinutes}");
    }

    [Test]
    public void PropertyCasing_Variations_AllDeserializeCorrectly()
    {
        // Test different casing variations to ensure case-insensitivity works
        var testCases = new[]
        {
            // Standard camelCase (Angular sends this)
            @"{""name"": ""Test"", ""breakPolicyRules"": [{""id"": null, ""dayOfWeek"": 1, ""paidBreakMinutes"": 15, ""unpaidBreakMinutes"": 30}]}",
            
            // PascalCase (C# uses this)
            @"{""Name"": ""Test"", ""BreakPolicyRules"": [{""Id"": null, ""DayOfWeek"": 1, ""PaidBreakMinutes"": 15, ""UnpaidBreakMinutes"": 30}]}",
            
            // Mixed case
            @"{""NAME"": ""Test"", ""breakPolicyRules"": [{""ID"": null, ""dayofweek"": 1, ""paidbreakseconds"": 15, ""unpaidbreakseconds"": 30}]}"
        };

        foreach (var json in testCases)
        {
            var model = JsonSerializer.Deserialize<BreakPolicyCreateModel>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            Assert.That(model, Is.Not.Null);
            Assert.That(model.Name, Is.EqualTo("Test"));
            Assert.That(model.BreakPolicyRules, Is.Not.Null);
            Assert.That(model.BreakPolicyRules.Count, Is.EqualTo(1));
        }
    }
}
