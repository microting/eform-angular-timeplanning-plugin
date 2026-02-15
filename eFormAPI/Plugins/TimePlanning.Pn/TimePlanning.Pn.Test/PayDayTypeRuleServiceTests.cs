using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using NSubstitute;
using NUnit.Framework;
using TimePlanning.Pn.Services.PayDayTypeRuleService;
using TimePlanning.Pn.Infrastructure.Models.PayDayTypeRule;

namespace TimePlanning.Pn.Test
{
    [TestFixture]
    public class PayDayTypeRuleServiceTests : TestBaseSetup
    {
        private IPayDayTypeRuleService _payDayTypeRuleService;

        [SetUp]
        public new async Task Setup()
        {
            await base.Setup();
            _payDayTypeRuleService = new PayDayTypeRuleService(
                TimePlanningPnDbContext,
                Substitute.For<ILogger<PayDayTypeRuleService>>());
        }

        [Test]
        public async Task Create_ValidModel_CreatesPayDayTypeRule()
        {
            // Arrange
            var payRuleSet = new PayRuleSet
            {
                Name = "Test Pay Rule Set",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                WorkflowState = "created"
            };
            await payRuleSet.Create(TimePlanningPnDbContext);

            var model = new PayDayTypeRuleCreateModel
            {
                PayRuleSetId = payRuleSet.Id,
                DayType = "Weekday"
            };

            // Act
            var result = await _payDayTypeRuleService.Create(model);

            // Assert
            Assert.That(result.Success, Is.True);

            var createdRule = await TimePlanningPnDbContext.PayDayTypeRules
                .Where(r => r.PayRuleSetId == payRuleSet.Id)
                .FirstOrDefaultAsync();
            Assert.That(createdRule, Is.Not.Null);
        }

        [Test]
        public async Task Read_ExistingId_ReturnsPayDayTypeRule()
        {
            // Arrange
            var payRuleSet = new PayRuleSet
            {
                Name = "Test Pay Rule Set",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                WorkflowState = "created"
            };
            await payRuleSet.Create(TimePlanningPnDbContext);

            var rule = new PayDayTypeRule
            {
                PayRuleSetId = payRuleSet.Id,
                DayType = (DayType)1, // Saturday
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                WorkflowState = "created"
            };
            await rule.Create(TimePlanningPnDbContext);

            // Act
            var result = await _payDayTypeRuleService.Read(rule.Id);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Model, Is.Not.Null);
            Assert.That(result.Model.Id, Is.EqualTo(rule.Id));
        }

        [Test]
        public async Task Read_NonExistingId_ReturnsFailure()
        {
            // Act
            var result = await _payDayTypeRuleService.Read(999999);

            // Assert
            Assert.That(result.Success, Is.False);
        }

        [Test]
        public async Task Update_ExistingId_UpdatesPayDayTypeRule()
        {
            // Arrange
            var payRuleSet = new PayRuleSet
            {
                Name = "Pay Rule Set",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                WorkflowState = "created"
            };
            await payRuleSet.Create(TimePlanningPnDbContext);

            var rule = new PayDayTypeRule
            {
                PayRuleSetId = payRuleSet.Id,
                DayType = (DayType)0, // Weekday
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                WorkflowState = "created"
            };
            await rule.Create(TimePlanningPnDbContext);

            var updateModel = new PayDayTypeRuleUpdateModel
            {
                DayType = "Sunday"
            };

            // Act
            var result = await _payDayTypeRuleService.Update(rule.Id, updateModel);

            // Assert
            Assert.That(result.Success, Is.True);
            
            var updatedRule = await TimePlanningPnDbContext.PayDayTypeRules
                .FirstOrDefaultAsync(r => r.Id == rule.Id);
            Assert.That(updatedRule, Is.Not.Null);
            Assert.That(updatedRule.DayType, Is.EqualTo((DayType)2)); // Sunday
        }

        [Test]
        public async Task Update_NonExistingId_ReturnsFailure()
        {
            // Arrange
            var updateModel = new PayDayTypeRuleUpdateModel
            {
                DayType = "Weekday"
            };

            // Act
            var result = await _payDayTypeRuleService.Update(999999, updateModel);

            // Assert
            Assert.That(result.Success, Is.False);
        }

        [Test]
        public async Task Delete_ExistingId_SoftDeletesPayDayTypeRule()
        {
            // Arrange
            var payRuleSet = new PayRuleSet
            {
                Name = "Test Pay Rule Set",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                WorkflowState = "created"
            };
            await payRuleSet.Create(TimePlanningPnDbContext);

            var rule = new PayDayTypeRule
            {
                PayRuleSetId = payRuleSet.Id,
                DayType = (DayType)3, // PublicHoliday
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                WorkflowState = "created"
            };
            await rule.Create(TimePlanningPnDbContext);

            // Act
            var result = await _payDayTypeRuleService.Delete(rule.Id);

            // Assert
            Assert.That(result.Success, Is.True);
            
            var deletedRule = await TimePlanningPnDbContext.PayDayTypeRules
                .FirstOrDefaultAsync(r => r.Id == rule.Id);
            Assert.That(deletedRule, Is.Not.Null);
            Assert.That(deletedRule.WorkflowState, Is.EqualTo("removed"));
        }

        [Test]
        public async Task Delete_NonExistingId_ReturnsFailure()
        {
            // Act
            var result = await _payDayTypeRuleService.Delete(999999);

            // Assert
            Assert.That(result.Success, Is.False);
        }

        [Test]
        public async Task Index_ReturnsPayDayTypeRules()
        {
            // Arrange
            var payRuleSet = new PayRuleSet
            {
                Name = "Test Pay Rule Set",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                WorkflowState = "created"
            };
            await payRuleSet.Create(TimePlanningPnDbContext);

            var rule1 = new PayDayTypeRule
            {
                PayRuleSetId = payRuleSet.Id,
                DayType = (DayType)0, // Weekday
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                WorkflowState = "created"
            };
            await rule1.Create(TimePlanningPnDbContext);

            var rule2 = new PayDayTypeRule
            {
                PayRuleSetId = payRuleSet.Id,
                DayType = (DayType)1, // Saturday
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                WorkflowState = "created"
            };
            await rule2.Create(TimePlanningPnDbContext);

            var requestModel = new PayDayTypeRulesRequestModel
            {
                Offset = 0,
                PageSize = 10
            };

            // Act
            var result = await _payDayTypeRuleService.Index(requestModel);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Model, Is.Not.Null);
            Assert.That(result.Model.Total, Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public async Task Index_WithPayRuleSetIdFilter_ReturnsFilteredRules()
        {
            // Arrange
            var payRuleSet1 = new PayRuleSet
            {
                Name = "Pay Rule Set 1",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                WorkflowState = "created"
            };
            await payRuleSet1.Create(TimePlanningPnDbContext);

            var payRuleSet2 = new PayRuleSet
            {
                Name = "Pay Rule Set 2",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                WorkflowState = "created"
            };
            await payRuleSet2.Create(TimePlanningPnDbContext);

            var rule1 = new PayDayTypeRule
            {
                PayRuleSetId = payRuleSet1.Id,
                DayType = (DayType)0, // Weekday
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                WorkflowState = "created"
            };
            await rule1.Create(TimePlanningPnDbContext);

            var rule2 = new PayDayTypeRule
            {
                PayRuleSetId = payRuleSet2.Id,
                DayType = (DayType)1, // Saturday
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                WorkflowState = "created"
            };
            await rule2.Create(TimePlanningPnDbContext);

            var requestModel = new PayDayTypeRulesRequestModel
            {
                PayRuleSetId = payRuleSet1.Id,
                Offset = 0,
                PageSize = 10
            };

            // Act
            var result = await _payDayTypeRuleService.Index(requestModel);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Model, Is.Not.Null);
            Assert.That(result.Model.Total, Is.GreaterThan(0));
        }

        [Test]
        public async Task Index_ExcludesDeletedPayDayTypeRules()
        {
            // Arrange
            var payRuleSet = new PayRuleSet
            {
                Name = "Test Pay Rule Set",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                WorkflowState = "created"
            };
            await payRuleSet.Create(TimePlanningPnDbContext);

            var activeRule = new PayDayTypeRule
            {
                PayRuleSetId = payRuleSet.Id,
                DayType = (DayType)0, // Weekday
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                WorkflowState = "created"
            };
            await activeRule.Create(TimePlanningPnDbContext);

            var deletedRule = new PayDayTypeRule
            {
                PayRuleSetId = payRuleSet.Id,
                DayType = (DayType)1, // Saturday
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                WorkflowState = "removed"
            };
            await deletedRule.Create(TimePlanningPnDbContext);

            var requestModel = new PayDayTypeRulesRequestModel
            {
                Offset = 0,
                PageSize = 10
            };

            // Act
            var result = await _payDayTypeRuleService.Index(requestModel);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Model, Is.Not.Null);
            Assert.That(result.Model.PayDayTypeRules.Any(r => r.Id == deletedRule.Id), Is.False);
            Assert.That(result.Model.PayDayTypeRules.Any(r => r.Id == activeRule.Id), Is.True);
        }
    }
}
