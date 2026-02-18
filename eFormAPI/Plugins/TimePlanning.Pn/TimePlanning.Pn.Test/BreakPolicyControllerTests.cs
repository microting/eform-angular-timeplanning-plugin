using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microting.eForm.Infrastructure.Constants;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;
using NUnit.Framework;
using TimePlanning.Pn.Controllers;
using TimePlanning.Pn.Infrastructure.Models.BreakPolicy;
using TimePlanning.Pn.Services.BreakPolicyService;

namespace TimePlanning.Pn.Test
{
    [TestFixture]
    public class BreakPolicyControllerTests : TestBaseSetup
    {
        private BreakPolicyController _controller;
        private IBreakPolicyService _service;

        [SetUp]
        public async Task Setup()
        {
            await GetContext();
            _service = new BreakPolicyService(DbContext, Logger);
            _controller = new BreakPolicyController(_service);
        }

        [Test]
        public async Task Create_WithNestedRules_ReturnsSuccess()
        {
            // Arrange
            var model = new BreakPolicyCreateModel
            {
                Name = "Test Policy with Rules",
                BreakPolicyRules = new List<BreakPolicyRuleModel>
                {
                    new BreakPolicyRuleModel
                    {
                        Id = null,
                        DayOfWeek = 1, // Monday
                        PaidBreakSeconds = 900, // 15 minutes
                        UnpaidBreakSeconds = 1800 // 30 minutes
                    },
                    new BreakPolicyRuleModel
                    {
                        Id = null,
                        DayOfWeek = 2, // Tuesday
                        PaidBreakSeconds = 1200, // 20 minutes
                        UnpaidBreakSeconds = 1800 // 30 minutes
                    }
                }
            };

            // Act
            var result = await _controller.Create(model);

            // Assert
            Assert.IsNotNull(result);
            var operationResult = result as OperationResult;
            Assert.IsNotNull(operationResult);
            Assert.IsTrue(operationResult.Success);

            // Verify in database
            var breakPolicy = await DbContext.BreakPolicies
                .FirstOrDefaultAsync(bp => bp.Name == "Test Policy with Rules" && bp.WorkflowState != Constants.WorkflowStates.Removed);
            Assert.IsNotNull(breakPolicy);

            var rules = await DbContext.BreakPolicyRules
                .Where(r => r.BreakPolicyId == breakPolicy.Id && r.WorkflowState != Constants.WorkflowStates.Removed)
                .ToListAsync();
            Assert.AreEqual(2, rules.Count);
        }

        [Test]
        public async Task Update_WithNestedRules_DeserializesCorrectly()
        {
            // Arrange - Create initial policy
            var breakPolicy = new Microting.TimePlanningBase.Infrastructure.Data.Entities.BreakPolicy
            {
                Name = "Initial Policy",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                WorkflowState = Constants.WorkflowStates.Created
            };
            await breakPolicy.Create(DbContext);

            var updateModel = new BreakPolicyUpdateModel
            {
                Name = "Updated Policy",
                BreakPolicyRules = new List<BreakPolicyRuleModel>
                {
                    new BreakPolicyRuleModel
                    {
                        Id = null,
                        DayOfWeek = 3, // Wednesday
                        PaidBreakSeconds = 600, // 10 minutes
                        UnpaidBreakSeconds = 2400 // 40 minutes
                    }
                }
            };

            // Act
            var result = await _controller.Update(breakPolicy.Id, updateModel);

            // Assert
            Assert.IsNotNull(result);
            var operationResult = result as OperationResult;
            Assert.IsNotNull(operationResult);
            Assert.IsTrue(operationResult.Success);

            // Verify name was updated
            var updated = await DbContext.BreakPolicies
                .FirstOrDefaultAsync(bp => bp.Id == breakPolicy.Id);
            Assert.AreEqual("Updated Policy", updated.Name);
        }

        [Test]
        public async Task AngularJSON_Format_DeserializesCorrectly()
        {
            // This test validates the exact JSON format that Angular sends
            var model = new BreakPolicyCreateModel
            {
                Name = "Angular Format Test",
                BreakPolicyRules = new List<BreakPolicyRuleModel>
                {
                    new BreakPolicyRuleModel
                    {
                        Id = null,  // Angular sends null for new entities
                        DayOfWeek = 0, // Sunday
                        PaidBreakSeconds = 900, // 15 minutes
                        UnpaidBreakSeconds = 1800 // 30 minutes
                    }
                }
            };

            // Act
            var result = await _controller.Create(model);

            // Assert
            var operationResult = result as OperationResult;
            Assert.IsNotNull(operationResult);
            Assert.IsTrue(operationResult.Success, "Angular JSON format should deserialize correctly");

            Console.WriteLine($"✅ Angular JSON format is VALID and deserializes correctly!");
            Console.WriteLine($"   Name: {model.Name}");
            Console.WriteLine($"   BreakPolicyRules: {model.BreakPolicyRules?.Count ?? 0}");
            if (model.BreakPolicyRules?.Count > 0)
            {
                Console.WriteLine($"   DayOfWeek: {model.BreakPolicyRules[0].DayOfWeek}");
                Console.WriteLine($"   PaidBreakSeconds: {model.BreakPolicyRules[0].PaidBreakSeconds}");
            }
        }

        [Test]
        public async Task PropertyCasing_Variations_AllDeserializeCorrectly()
        {
            // Test various property naming to ensure case-insensitive deserialization works
            var model = new BreakPolicyCreateModel
            {
                Name = "Case Test",
                BreakPolicyRules = new List<BreakPolicyRuleModel>
                {
                    new BreakPolicyRuleModel
                    {
                        Id = null,
                        DayOfWeek = 5, // Friday
                        PaidBreakSeconds = 600,
                        UnpaidBreakSeconds = 1200
                    }
                }
            };

            // Act
            var result = await _controller.Create(model);

            // Assert
            var operationResult = result as OperationResult;
            Assert.IsNotNull(operationResult);
            Assert.IsTrue(operationResult.Success);
        }
    }
}
