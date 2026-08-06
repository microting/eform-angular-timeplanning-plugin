using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microting.eForm.Infrastructure.Constants;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using NSubstitute;
using NUnit.Framework;
using TimePlanning.Pn.Services.PayDayTypeRuleService;
using TimePlanning.Pn.Services.TimePlanningLocalizationService;
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
            var localizationService = Substitute.For<ITimePlanningLocalizationService>();
            localizationService.GetString(Arg.Any<string>())
                .Returns(call => call.Arg<string>());
            _payDayTypeRuleService = new PayDayTypeRuleService(
                TimePlanningPnDbContext,
                Substitute.For<ILogger<PayDayTypeRuleService>>(),
                localizationService);
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
                WorkflowState = Constants.WorkflowStates.Created
            };
            await payRuleSet.Create(TimePlanningPnDbContext);

            var model = new PayDayTypeRuleCreateModel
            {
                PayRuleSetId = payRuleSet.Id,
                DayType = "Monday"
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
                WorkflowState = Constants.WorkflowStates.Created
            };
            await payRuleSet.Create(TimePlanningPnDbContext);

            var rule = new PayDayTypeRule
            {
                PayRuleSetId = payRuleSet.Id,
                DayType = (DayType)1, // Weekend
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                WorkflowState = Constants.WorkflowStates.Created
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
                WorkflowState = Constants.WorkflowStates.Created
            };
            await payRuleSet.Create(TimePlanningPnDbContext);

            var rule = new PayDayTypeRule
            {
                PayRuleSetId = payRuleSet.Id,
                DayType = (DayType)0, // Monday
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                WorkflowState = Constants.WorkflowStates.Created
            };
            await rule.Create(TimePlanningPnDbContext);

            var updateModel = new PayDayTypeRuleUpdateModel
            {
                DayType = "Tuesday"
            };

            // Act
            var result = await _payDayTypeRuleService.Update(rule.Id, updateModel);

            // Assert
            Assert.That(result.Success, Is.True, $"Update failed: {result.Message}");

            var updatedRule = await TimePlanningPnDbContext.PayDayTypeRules
                .FirstOrDefaultAsync(r => r.Id == rule.Id);
            Assert.That(updatedRule, Is.Not.Null);
            Assert.That(updatedRule.DayType, Is.EqualTo((DayType)1)); // Tuesday
        }

        [Test]
        public async Task Update_NonExistingId_ReturnsFailure()
        {
            // Arrange
            var updateModel = new PayDayTypeRuleUpdateModel
            {
                DayType = "Monday"
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
                WorkflowState = Constants.WorkflowStates.Created
            };
            await payRuleSet.Create(TimePlanningPnDbContext);

            var rule = new PayDayTypeRule
            {
                PayRuleSetId = payRuleSet.Id,
                DayType = (DayType)2, // Holiday
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                WorkflowState = Constants.WorkflowStates.Created
            };
            await rule.Create(TimePlanningPnDbContext);

            // Act
            var result = await _payDayTypeRuleService.Delete(rule.Id);

            // Assert
            Assert.That(result.Success, Is.True);

            var deletedRule = await TimePlanningPnDbContext.PayDayTypeRules
                .FirstOrDefaultAsync(r => r.Id == rule.Id);
            Assert.That(deletedRule, Is.Not.Null);
            Assert.That(deletedRule.WorkflowState, Is.EqualTo(Constants.WorkflowStates.Removed));
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
                WorkflowState = Constants.WorkflowStates.Created
            };
            await payRuleSet.Create(TimePlanningPnDbContext);

            var rule1 = new PayDayTypeRule
            {
                PayRuleSetId = payRuleSet.Id,
                DayType = (DayType)0, // Weekday
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                WorkflowState = Constants.WorkflowStates.Created
            };
            await rule1.Create(TimePlanningPnDbContext);

            var rule2 = new PayDayTypeRule
            {
                PayRuleSetId = payRuleSet.Id,
                DayType = (DayType)1, // Weekend
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                WorkflowState = Constants.WorkflowStates.Created
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
                WorkflowState = Constants.WorkflowStates.Created
            };
            await payRuleSet1.Create(TimePlanningPnDbContext);

            var payRuleSet2 = new PayRuleSet
            {
                Name = "Pay Rule Set 2",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                WorkflowState = Constants.WorkflowStates.Created
            };
            await payRuleSet2.Create(TimePlanningPnDbContext);

            var rule1 = new PayDayTypeRule
            {
                PayRuleSetId = payRuleSet1.Id,
                DayType = (DayType)0, // Weekday
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                WorkflowState = Constants.WorkflowStates.Created
            };
            await rule1.Create(TimePlanningPnDbContext);

            var rule2 = new PayDayTypeRule
            {
                PayRuleSetId = payRuleSet2.Id,
                DayType = (DayType)1, // Saturday
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                WorkflowState = Constants.WorkflowStates.Created
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
                WorkflowState = Constants.WorkflowStates.Created
            };
            await payRuleSet.Create(TimePlanningPnDbContext);

            var activeRule = new PayDayTypeRule
            {
                PayRuleSetId = payRuleSet.Id,
                DayType = (DayType)0, // Weekday
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                WorkflowState = Constants.WorkflowStates.Created
            };
            await activeRule.Create(TimePlanningPnDbContext);

            var deletedRule = new PayDayTypeRule
            {
                PayRuleSetId = payRuleSet.Id,
                DayType = (DayType)1, // Weekend
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                WorkflowState = Constants.WorkflowStates.Created
            };
            await deletedRule.Create(TimePlanningPnDbContext);
            await deletedRule.Delete(TimePlanningPnDbContext);

            var requestModel = new PayDayTypeRulesRequestModel
            {
                PayRuleSetId = payRuleSet.Id,
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

        #region Locked Preset Guard Tests

        /// <summary>
        /// Creates a PayRuleSet with the supplied name and returns it, so the
        /// locked-preset tests only differ by that name.
        /// </summary>
        private async Task<PayRuleSet> CreatePayRuleSetNamed(string payRuleSetName)
        {
            var payRuleSet = new PayRuleSet
            {
                Name = payRuleSetName,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                WorkflowState = Constants.WorkflowStates.Created
            };
            await payRuleSet.Create(TimePlanningPnDbContext);

            return payRuleSet;
        }

        [Test]
        public async Task Create_OwnedByLockedPresetWithLegacyValidityPeriod_ReturnsFailure()
        {
            // Arrange - stored before the catalogue was renamed to "... 2026-2029"
            var payRuleSet = await CreatePayRuleSetNamed("GLS-A / 3F - Jordbrug Dyrehold 2024-2026");

            var model = new PayDayTypeRuleCreateModel
            {
                PayRuleSetId = payRuleSet.Id,
                DayType = "Monday",
                DefaultPayCode = "SNEAKED_IN",
                Priority = 1
            };

            // Act
            var result = await _payDayTypeRuleService.Create(model);

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("CannotEditLockedPreset"));
            var created = await TimePlanningPnDbContext.PayDayTypeRules
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(r => r.PayRuleSetId == payRuleSet.Id);
            Assert.That(created, Is.Null);
        }

        [Test]
        public async Task Update_OwnedByLockedPresetWithLegacyValidityPeriod_ReturnsFailure()
        {
            // Arrange
            var payRuleSet = await CreatePayRuleSetNamed("GLS-A / 3F - Jordbrug Dyrehold 2024-2026");

            var rule = new PayDayTypeRule
            {
                PayRuleSetId = payRuleSet.Id,
                DayType = DayType.Monday,
                DefaultPayCode = "NORMAL",
                Priority = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                WorkflowState = Constants.WorkflowStates.Created
            };
            await rule.Create(TimePlanningPnDbContext);

            var updateModel = new PayDayTypeRuleUpdateModel
            {
                DayType = "Tuesday",
                DefaultPayCode = "HACKED",
                Priority = 9
            };

            // Act
            var result = await _payDayTypeRuleService.Update(rule.Id, updateModel);

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("CannotEditLockedPreset"));
            var unchanged = await TimePlanningPnDbContext.PayDayTypeRules
                .FirstOrDefaultAsync(r => r.Id == rule.Id);
            Assert.That(unchanged, Is.Not.Null);
            Assert.That(unchanged.DayType, Is.EqualTo(DayType.Monday));
            Assert.That(unchanged.DefaultPayCode, Is.EqualTo("NORMAL"));
            Assert.That(unchanged.Priority, Is.EqualTo(1));
        }

        [Test]
        public async Task Update_OwnedByLockedPresetWithCurrentValidityPeriod_ReturnsFailure()
        {
            // Arrange
            var payRuleSet = await CreatePayRuleSetNamed("GLS-A / 3F - Jordbrug Dyrehold 2026-2029");

            var rule = new PayDayTypeRule
            {
                PayRuleSetId = payRuleSet.Id,
                DayType = DayType.Monday,
                DefaultPayCode = "NORMAL",
                Priority = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                WorkflowState = Constants.WorkflowStates.Created
            };
            await rule.Create(TimePlanningPnDbContext);

            var updateModel = new PayDayTypeRuleUpdateModel
            {
                DayType = "Tuesday",
                DefaultPayCode = "HACKED",
                Priority = 9
            };

            // Act
            var result = await _payDayTypeRuleService.Update(rule.Id, updateModel);

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("CannotEditLockedPreset"));
        }

        [Test]
        public async Task Delete_OwnedByLockedPresetWithLegacyValidityPeriod_ReturnsFailure()
        {
            // Arrange
            var payRuleSet = await CreatePayRuleSetNamed("GLS-A / 3F - Jordbrug Dyrehold 2024-2026");

            var rule = new PayDayTypeRule
            {
                PayRuleSetId = payRuleSet.Id,
                DayType = DayType.Monday,
                DefaultPayCode = "NORMAL",
                Priority = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                WorkflowState = Constants.WorkflowStates.Created
            };
            await rule.Create(TimePlanningPnDbContext);

            // Act
            var result = await _payDayTypeRuleService.Delete(rule.Id);

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("CannotEditLockedPreset"));
            var stillThere = await TimePlanningPnDbContext.PayDayTypeRules
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(r => r.Id == rule.Id);
            Assert.That(stillThere, Is.Not.Null);
            Assert.That(stillThere.WorkflowState, Is.EqualTo(Constants.WorkflowStates.Created));
        }

        [Test]
        public async Task CreateUpdateDelete_OwnedByCustomNameWithValidityPeriod_Succeed()
        {
            // Arrange - stripping the year range must not make this collide with a preset
            var payRuleSet = await CreatePayRuleSetNamed("Min egen aftale 2024-2026");

            var createModel = new PayDayTypeRuleCreateModel
            {
                PayRuleSetId = payRuleSet.Id,
                DayType = "Monday",
                DefaultPayCode = "CUSTOM",
                Priority = 1
            };

            // Act - Create
            var createResult = await _payDayTypeRuleService.Create(createModel);

            // Assert - Create
            Assert.That(createResult.Success, Is.True);
            var created = await TimePlanningPnDbContext.PayDayTypeRules
                .Where(r => r.WorkflowState != Constants.WorkflowStates.Removed)
                .FirstOrDefaultAsync(r => r.PayRuleSetId == payRuleSet.Id);
            Assert.That(created, Is.Not.Null);

            // Act - Update
            var updateResult = await _payDayTypeRuleService.Update(created.Id, new PayDayTypeRuleUpdateModel
            {
                DayType = "Tuesday",
                DefaultPayCode = "CUSTOM_2",
                Priority = 2
            });

            // Assert - Update
            Assert.That(updateResult.Success, Is.True);

            // Act - Delete
            var deleteResult = await _payDayTypeRuleService.Delete(created.Id);

            // Assert - Delete
            Assert.That(deleteResult.Success, Is.True);
            var deleted = await TimePlanningPnDbContext.PayDayTypeRules
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(r => r.Id == created.Id);
            Assert.That(deleted, Is.Not.Null);
            Assert.That(deleted.WorkflowState, Is.EqualTo(Constants.WorkflowStates.Removed));
        }

        #endregion
    }
}
