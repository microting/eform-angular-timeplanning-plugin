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
      ""paidBreakSeconds"": 900,
      ""unpaidBreakSeconds"": 1800
    },
    {
      ""id"": null,
      ""dayOfWeek"": 2,
      ""paidBreakSeconds"": 1200,
      ""unpaidBreakSeconds"": 1800
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
        Assert.That(model.BreakPolicyRules[0].PaidBreakSeconds, Is.EqualTo(900));

        // Act - Call service directly (controller just wraps service)
        var breakPolicyService = ServiceProvider.GetRequiredService<Services.BreakPolicyService.IBreakPolicyService>();
        var result = await breakPolicyService.Create(model);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Success, Is.True);
    }

    [Test]
    public async Task Update_WithNestedRules_DeserializesCorrectly()
    {
        // Arrange - Create initial policy
        var createModel = new BreakPolicyCreateModel
        {
            Name = "Original Policy",
            BreakPolicyRules = new List<BreakPolicyRuleModel>
            {
                new BreakPolicyRuleModel
                {
                    Id = null,
                    DayOfWeek = 1,
                    PaidBreakSeconds = 600,
                    UnpaidBreakSeconds = 1200
                }
            }
        };

        var breakPolicyService = ServiceProvider.GetRequiredService<Services.BreakPolicyService.IBreakPolicyService>();
        var createResult = await breakPolicyService.Create(createModel);
        Assert.That(createResult.Success, Is.True);

        // Get the created policy
        var dbContext = ServiceProvider.GetRequiredService<TimePlanningDbContext>();
        var createdPolicy = dbContext.BreakPolicies.First(bp => bp.Name == "Original Policy");

        // Arrange - Update with new rules
        var json = $@"{{
  ""name"": ""Updated Policy"",
  ""breakPolicyRules"": [
    {{
      ""id"": null,
      ""dayOfWeek"": 1,
      ""paidBreakSeconds"": 900,
      ""unpaidBreakSeconds"": 1800
    }},
    {{
      ""id"": null,
      ""dayOfWeek"": 3,
      ""paidBreakSeconds"": 1200,
      ""unpaidBreakSeconds"": 1800
    }}
  ]
}}";

        var updateModel = JsonSerializer.Deserialize<BreakPolicyUpdateModel>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.That(updateModel, Is.Not.Null);
        Assert.That(updateModel.Name, Is.EqualTo("Updated Policy"));
        Assert.That(updateModel.BreakPolicyRules, Is.Not.Null);
        Assert.That(updateModel.BreakPolicyRules.Count, Is.EqualTo(2));

        // Act
        var result = await breakPolicyService.Update(createdPolicy.Id, updateModel);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Success, Is.True);
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
      ""paidBreakSeconds"": 900,
      ""unpaidBreakSeconds"": 1800
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
        Assert.That(model.BreakPolicyRules[0].PaidBreakSeconds, Is.EqualTo(900));
        Assert.That(model.BreakPolicyRules[0].UnpaidBreakSeconds, Is.EqualTo(1800));

        Console.WriteLine("✅ Angular JSON format is VALID and deserializes correctly!");
        Console.WriteLine($"   Name: {model.Name}");
        Console.WriteLine($"   BreakPolicyRules: {model.BreakPolicyRules.Count}");
        Console.WriteLine($"   DayOfWeek: {model.BreakPolicyRules[0].DayOfWeek}");
        Console.WriteLine($"   PaidBreakSeconds: {model.BreakPolicyRules[0].PaidBreakSeconds}");
    }

    [Test]
    public void PropertyCasing_Variations_AllDeserializeCorrectly()
    {
        // Test different casing variations to ensure case-insensitivity works
        var testCases = new[]
        {
            // Standard camelCase (Angular sends this)
            @"{""name"": ""Test"", ""breakPolicyRules"": [{""id"": null, ""dayOfWeek"": 1, ""paidBreakSeconds"": 900, ""unpaidBreakSeconds"": 1800}]}",
            
            // PascalCase (C# uses this)
            @"{""Name"": ""Test"", ""BreakPolicyRules"": [{""Id"": null, ""DayOfWeek"": 1, ""PaidBreakSeconds"": 900, ""UnpaidBreakSeconds"": 1800}]}",
            
            // Mixed case
            @"{""NAME"": ""Test"", ""breakPolicyRules"": [{""ID"": null, ""dayofweek"": 1, ""paidbreakseconds"": 900, ""unpaidbreakseconds"": 1800}]}"
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
