/*
The MIT License (MIT)

Copyright (c) 2007 - 2021 Microting A/S
*/

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microting.eForm.Infrastructure.Constants;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using Microting.TimePlanningBase.Infrastructure.Data.Factories;
using NSubstitute;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Models.PayTimeBandRule;
using TimePlanning.Pn.Services.PayTimeBandRuleService;
using TimePlanning.Pn.Services.TimePlanningLocalizationService;

namespace TimePlanning.Pn.Test;

[TestFixture]
public class PayTimeBandRuleServiceTests : TestBaseSetup
{
    private IPayTimeBandRuleService _service;

    [SetUp]
    public new async Task Setup()
    {
        await base.Setup();
        var localizationService = Substitute.For<ITimePlanningLocalizationService>();
        localizationService.GetString(Arg.Any<string>())
            .Returns(call => call.Arg<string>());
        _service = new PayTimeBandRuleService(
            TimePlanningPnDbContext,
            Substitute.For<ILogger<PayTimeBandRuleService>>(),
            localizationService);
    }

    [Test]
    public async Task Create_ValidModel_CreatesPayTimeBandRule()
    {
        // Arrange
        var payRuleSet = new PayRuleSet
        {
            Name = "Test Rule Set",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WorkflowState = Constants.WorkflowStates.Created
        };
        await payRuleSet.Create(TimePlanningPnDbContext);

        var payDayTypeRule = new PayDayTypeRule
        {
            PayRuleSetId = payRuleSet.Id,
            DayType = DayType.Monday,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WorkflowState = Constants.WorkflowStates.Created
        };
        await payDayTypeRule.Create(TimePlanningPnDbContext);

        var model = new PayTimeBandRuleCreateModel
        {
            PayDayTypeRuleId = payDayTypeRule.Id,
            StartSecondOfDay = 0,
            EndSecondOfDay = 64800,
            PayCode = "SUN_DAY",
        };

        // Act
        var result = await _service.Create(model);

        // Assert
        Assert.That(result.Success, Is.True);
        var created = await TimePlanningPnDbContext.PayTimeBandRules
            .Where(ptr => ptr.WorkflowState != Constants.WorkflowStates.Removed)
            .FirstOrDefaultAsync(ptr => ptr.PayCode == "SUN_DAY");
        Assert.That(created, Is.Not.Null);
        Assert.That(created.StartSecondOfDay, Is.EqualTo(0));
        Assert.That(created.EndSecondOfDay, Is.EqualTo(64800));
    }

    [Test]
    public async Task Read_ExistingId_ReturnsPayTimeBandRule()
    {
        // Arrange
        var payRuleSet = new PayRuleSet
        {
            Name = "Test Rule Set",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WorkflowState = Constants.WorkflowStates.Created
        };
        await payRuleSet.Create(TimePlanningPnDbContext);

        var payDayTypeRule = new PayDayTypeRule
        {
            PayRuleSetId = payRuleSet.Id,
            DayType = DayType.Monday,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WorkflowState = Constants.WorkflowStates.Created
        };
        await payDayTypeRule.Create(TimePlanningPnDbContext);

        var payTimeBandRule = new PayTimeBandRule
        {
            PayDayTypeRuleId = payDayTypeRule.Id,
            StartSecondOfDay = 0,
            EndSecondOfDay = 64800,
            PayCode = "SUN_DAY",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WorkflowState = Constants.WorkflowStates.Created
        };
        await payTimeBandRule.Create(TimePlanningPnDbContext);

        // Act
        var result = await _service.Read(payTimeBandRule.Id);

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.Model, Is.Not.Null);
        Assert.That(result.Model.PayCode, Is.EqualTo("SUN_DAY"));
        Assert.That(result.Model.StartSecondOfDay, Is.EqualTo(0));
        Assert.That(result.Model.EndSecondOfDay, Is.EqualTo(64800));
    }

    [Test]
    public async Task Read_NonExistingId_ReturnsFailure()
    {
        // Act
        var result = await _service.Read(99999);

        // Assert
        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Does.Contain("not found"));
    }

    [Test]
    public async Task Update_ExistingId_UpdatesPayTimeBandRule()
    {
        // Arrange
        var payRuleSet = new PayRuleSet
        {
            Name = "Test Rule Set",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WorkflowState = Constants.WorkflowStates.Created
        };
        await payRuleSet.Create(TimePlanningPnDbContext);

        var payDayTypeRule = new PayDayTypeRule
        {
            PayRuleSetId = payRuleSet.Id,
            DayType = DayType.Monday,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WorkflowState = Constants.WorkflowStates.Created
        };
        await payDayTypeRule.Create(TimePlanningPnDbContext);

        var payTimeBandRule = new PayTimeBandRule
        {
            PayDayTypeRuleId = payDayTypeRule.Id,
            StartSecondOfDay = 0,
            EndSecondOfDay = 64800,
            PayCode = "SUN_DAY",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WorkflowState = Constants.WorkflowStates.Created
        };
        await payTimeBandRule.Create(TimePlanningPnDbContext);

        var updateModel = new PayTimeBandRuleUpdateModel
        {
            StartSecondOfDay = 64800,
            EndSecondOfDay = 86399,
            PayCode = "SUN_EVENING",
        };

        // Act
        var result = await _service.Update(payTimeBandRule.Id, updateModel);

        // Assert
        Assert.That(result.Success, Is.True);
        var updated = await TimePlanningPnDbContext.PayTimeBandRules
            .FirstOrDefaultAsync(ptr => ptr.Id == payTimeBandRule.Id);
        Assert.That(updated, Is.Not.Null);
        Assert.That(updated.StartSecondOfDay, Is.EqualTo(64800));
        Assert.That(updated.EndSecondOfDay, Is.EqualTo(86399));
        Assert.That(updated.PayCode, Is.EqualTo("SUN_EVENING"));
    }

    [Test]
    public async Task Update_NonExistingId_ReturnsFailure()
    {
        // Arrange
        var updateModel = new PayTimeBandRuleUpdateModel
        {
            StartSecondOfDay = 64800,
            EndSecondOfDay = 86399,
            PayCode = "SUN_EVENING",
        };

        // Act
        var result = await _service.Update(99999, updateModel);

        // Assert
        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Does.Contain("not found"));
    }

    [Test]
    public async Task Delete_ExistingId_SoftDeletesPayTimeBandRule()
    {
        // Arrange
        var payRuleSet = new PayRuleSet
        {
            Name = "Test Rule Set",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WorkflowState = Constants.WorkflowStates.Created
        };
        await payRuleSet.Create(TimePlanningPnDbContext);

        var payDayTypeRule = new PayDayTypeRule
        {
            PayRuleSetId = payRuleSet.Id,
            DayType = DayType.Monday,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WorkflowState = Constants.WorkflowStates.Created
        };
        await payDayTypeRule.Create(TimePlanningPnDbContext);

        var payTimeBandRule = new PayTimeBandRule
        {
            PayDayTypeRuleId = payDayTypeRule.Id,
            StartSecondOfDay = 0,
            EndSecondOfDay = 64800,
            PayCode = "SUN_DAY",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WorkflowState = Constants.WorkflowStates.Created
        };
        await payTimeBandRule.Create(TimePlanningPnDbContext);

        // Act
        var result = await _service.Delete(payTimeBandRule.Id);

        // Assert
        Assert.That(result.Success, Is.True);
        var deleted = await TimePlanningPnDbContext.PayTimeBandRules
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(ptr => ptr.Id == payTimeBandRule.Id);
        Assert.That(deleted, Is.Not.Null);
        Assert.That(deleted.WorkflowState, Is.EqualTo(Constants.WorkflowStates.Removed));
    }

    [Test]
    public async Task Delete_NonExistingId_ReturnsFailure()
    {
        // Act
        var result = await _service.Delete(99999);

        // Assert
        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Does.Contain("not found"));
    }

    [Test]
    public async Task Index_ReturnsPayTimeBandRules()
    {
        // Arrange
        var payRuleSet = new PayRuleSet
        {
            Name = "Test Rule Set",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WorkflowState = Constants.WorkflowStates.Created
        };
        await payRuleSet.Create(TimePlanningPnDbContext);

        var payDayTypeRule = new PayDayTypeRule
        {
            PayRuleSetId = payRuleSet.Id,
            DayType = DayType.Monday,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WorkflowState = Constants.WorkflowStates.Created
        };
        await payDayTypeRule.Create(TimePlanningPnDbContext);

        var payTimeBandRule1 = new PayTimeBandRule
        {
            PayDayTypeRuleId = payDayTypeRule.Id,
            StartSecondOfDay = 0,
            EndSecondOfDay = 64800,
            PayCode = "SUN_DAY",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WorkflowState = Constants.WorkflowStates.Created
        };
        await payTimeBandRule1.Create(TimePlanningPnDbContext);

        var payTimeBandRule2 = new PayTimeBandRule
        {
            PayDayTypeRuleId = payDayTypeRule.Id,
            StartSecondOfDay = 64800,
            EndSecondOfDay = 86399,
            PayCode = "SUN_EVENING",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WorkflowState = Constants.WorkflowStates.Created
        };
        await payTimeBandRule2.Create(TimePlanningPnDbContext);

        var requestModel = new PayTimeBandRulesRequestModel
        {
            Offset = 0,
            PageSize = 10
        };

        // Act
        var result = await _service.Index(requestModel);

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.Model, Is.Not.Null);
        Assert.That(result.Model.Total, Is.EqualTo(2));
        Assert.That(result.Model.PayTimeBandRules.Count, Is.EqualTo(2));
    }

    [Test]
    public async Task Index_WithPayDayTypeRuleIdFilter_ReturnsFilteredRules()
    {
        // Arrange
        var payRuleSet = new PayRuleSet
        {
            Name = "Test Rule Set",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WorkflowState = Constants.WorkflowStates.Created
        };
        await payRuleSet.Create(TimePlanningPnDbContext);

        var payDayTypeRule1 = new PayDayTypeRule
        {
            PayRuleSetId = payRuleSet.Id,
            DayType = DayType.Monday,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WorkflowState = Constants.WorkflowStates.Created
        };
        await payDayTypeRule1.Create(TimePlanningPnDbContext);

        var payDayTypeRule2 = new PayDayTypeRule
        {
            PayRuleSetId = payRuleSet.Id,
            DayType = DayType.Monday,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WorkflowState = Constants.WorkflowStates.Created
        };
        await payDayTypeRule2.Create(TimePlanningPnDbContext);

        var payTimeBandRule1 = new PayTimeBandRule
        {
            PayDayTypeRuleId = payDayTypeRule1.Id,
            StartSecondOfDay = 0,
            EndSecondOfDay = 64800,
            PayCode = "SUN_DAY",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WorkflowState = Constants.WorkflowStates.Created
        };
        await payTimeBandRule1.Create(TimePlanningPnDbContext);

        var payTimeBandRule2 = new PayTimeBandRule
        {
            PayDayTypeRuleId = payDayTypeRule2.Id,
            StartSecondOfDay = 0,
            EndSecondOfDay = 64800,
            PayCode = "SAT_DAY",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WorkflowState = Constants.WorkflowStates.Created
        };
        await payTimeBandRule2.Create(TimePlanningPnDbContext);

        var requestModel = new PayTimeBandRulesRequestModel
        {
            Offset = 0,
            PageSize = 10,
            PayDayTypeRuleId = payDayTypeRule1.Id
        };

        // Act
        var result = await _service.Index(requestModel);

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.Model.Total, Is.EqualTo(1));
        Assert.That(result.Model.PayTimeBandRules[0].PayCode, Is.EqualTo("SUN_DAY"));
    }

    [Test]
    public async Task Index_OrdersByOrder_ReturnsOrderedRules()
    {
        // Arrange
        var payRuleSet = new PayRuleSet
        {
            Name = "Test Rule Set",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WorkflowState = Constants.WorkflowStates.Created
        };
        await payRuleSet.Create(TimePlanningPnDbContext);

        var payDayTypeRule = new PayDayTypeRule
        {
            PayRuleSetId = payRuleSet.Id,
            DayType = DayType.Monday,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WorkflowState = Constants.WorkflowStates.Created
        };
        await payDayTypeRule.Create(TimePlanningPnDbContext);

        // Create in reverse order
        var payTimeBandRule3 = new PayTimeBandRule
        {
            PayDayTypeRuleId = payDayTypeRule.Id,
            StartSecondOfDay = 79200,
            EndSecondOfDay = 86399,
            PayCode = "SUN_LATE",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WorkflowState = Constants.WorkflowStates.Created
        };
        await payTimeBandRule3.Create(TimePlanningPnDbContext);

        var payTimeBandRule1 = new PayTimeBandRule
        {
            PayDayTypeRuleId = payDayTypeRule.Id,
            StartSecondOfDay = 0,
            EndSecondOfDay = 64800,
            PayCode = "SUN_DAY",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WorkflowState = Constants.WorkflowStates.Created
        };
        await payTimeBandRule1.Create(TimePlanningPnDbContext);

        var payTimeBandRule2 = new PayTimeBandRule
        {
            PayDayTypeRuleId = payDayTypeRule.Id,
            StartSecondOfDay = 64800,
            EndSecondOfDay = 79200,
            PayCode = "SUN_EVENING",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WorkflowState = Constants.WorkflowStates.Created
        };
        await payTimeBandRule2.Create(TimePlanningPnDbContext);

        var requestModel = new PayTimeBandRulesRequestModel
        {
            Offset = 0,
            PageSize = 10
        };

        // Act
        var result = await _service.Index(requestModel);

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.Model.Total, Is.EqualTo(3));
        Assert.That(result.Model.PayTimeBandRules[0].PayCode, Is.EqualTo("SUN_DAY"));
        Assert.That(result.Model.PayTimeBandRules[1].PayCode, Is.EqualTo("SUN_EVENING"));
        Assert.That(result.Model.PayTimeBandRules[2].PayCode, Is.EqualTo("SUN_LATE"));
    }

    [Test]
    public async Task Index_ExcludesDeletedPayTimeBandRules()
    {
        // Arrange
        var payRuleSet = new PayRuleSet
        {
            Name = "Test Rule Set",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WorkflowState = Constants.WorkflowStates.Created
        };
        await payRuleSet.Create(TimePlanningPnDbContext);

        var payDayTypeRule = new PayDayTypeRule
        {
            PayRuleSetId = payRuleSet.Id,
            DayType = DayType.Monday,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WorkflowState = Constants.WorkflowStates.Created
        };
        await payDayTypeRule.Create(TimePlanningPnDbContext);

        var payTimeBandRule1 = new PayTimeBandRule
        {
            PayDayTypeRuleId = payDayTypeRule.Id,
            StartSecondOfDay = 0,
            EndSecondOfDay = 64800,
            PayCode = "Active",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WorkflowState = Constants.WorkflowStates.Created
        };
        await payTimeBandRule1.Create(TimePlanningPnDbContext);

        var payTimeBandRule2 = new PayTimeBandRule
        {
            PayDayTypeRuleId = payDayTypeRule.Id,
            StartSecondOfDay = 64800,
            EndSecondOfDay = 86399,
            PayCode = "Deleted",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WorkflowState = Constants.WorkflowStates.Created
        };
        await payTimeBandRule2.Create(TimePlanningPnDbContext);
        await payTimeBandRule2.Delete(TimePlanningPnDbContext);

        var requestModel = new PayTimeBandRulesRequestModel
        {
            Offset = 0,
            PageSize = 10
        };

        // Act
        var result = await _service.Index(requestModel);

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.Model.Total, Is.EqualTo(1));
        Assert.That(result.Model.PayTimeBandRules[0].PayCode, Is.EqualTo("Active"));
    }

    #region Locked Preset Guard Tests

    /// <summary>
    /// Creates a PayDayTypeRule under a PayRuleSet with the supplied name and
    /// returns it, so the locked-preset tests only differ by that name.
    /// </summary>
    private async Task<PayDayTypeRule> CreatePayDayTypeRuleUnderRuleSetNamed(string payRuleSetName)
    {
        var payRuleSet = new PayRuleSet
        {
            Name = payRuleSetName,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WorkflowState = Constants.WorkflowStates.Created
        };
        await payRuleSet.Create(TimePlanningPnDbContext);

        var payDayTypeRule = new PayDayTypeRule
        {
            PayRuleSetId = payRuleSet.Id,
            DayType = DayType.Monday,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WorkflowState = Constants.WorkflowStates.Created
        };
        await payDayTypeRule.Create(TimePlanningPnDbContext);

        return payDayTypeRule;
    }

    [Test]
    public async Task Create_OwnedByLockedPresetWithLegacyValidityPeriod_ReturnsFailure()
    {
        // Arrange - stored before the catalogue was renamed to "... 2026-2029"
        var payDayTypeRule = await CreatePayDayTypeRuleUnderRuleSetNamed("GLS-A / 3F - Jordbrug Dyrehold 2024-2026");

        var model = new PayTimeBandRuleCreateModel
        {
            PayDayTypeRuleId = payDayTypeRule.Id,
            StartSecondOfDay = 0,
            EndSecondOfDay = 64800,
            PayCode = "SNEAKED_IN",
            Priority = 1
        };

        // Act
        var result = await _service.Create(model);

        // Assert
        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Does.Contain("CannotEditLockedPreset"));
        var created = await TimePlanningPnDbContext.PayTimeBandRules
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(ptbr => ptbr.PayCode == "SNEAKED_IN");
        Assert.That(created, Is.Null);
    }

    [Test]
    public async Task Update_OwnedByLockedPresetWithLegacyValidityPeriod_ReturnsFailure()
    {
        // Arrange
        var payDayTypeRule = await CreatePayDayTypeRuleUnderRuleSetNamed("GLS-A / 3F - Jordbrug Dyrehold 2024-2026");

        var payTimeBandRule = new PayTimeBandRule
        {
            PayDayTypeRuleId = payDayTypeRule.Id,
            StartSecondOfDay = 0,
            EndSecondOfDay = 64800,
            PayCode = "DAY",
            Priority = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WorkflowState = Constants.WorkflowStates.Created
        };
        await payTimeBandRule.Create(TimePlanningPnDbContext);

        var updateModel = new PayTimeBandRuleUpdateModel
        {
            StartSecondOfDay = 3600,
            EndSecondOfDay = 7200,
            PayCode = "HACKED",
            Priority = 9
        };

        // Act
        var result = await _service.Update(payTimeBandRule.Id, updateModel);

        // Assert
        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Does.Contain("CannotEditLockedPreset"));
        var unchanged = await TimePlanningPnDbContext.PayTimeBandRules
            .FirstOrDefaultAsync(ptbr => ptbr.Id == payTimeBandRule.Id);
        Assert.That(unchanged, Is.Not.Null);
        Assert.That(unchanged.StartSecondOfDay, Is.EqualTo(0));
        Assert.That(unchanged.EndSecondOfDay, Is.EqualTo(64800));
        Assert.That(unchanged.PayCode, Is.EqualTo("DAY"));
    }

    [Test]
    public async Task Update_OwnedByLockedPresetWithCurrentValidityPeriod_ReturnsFailure()
    {
        // Arrange
        var payDayTypeRule = await CreatePayDayTypeRuleUnderRuleSetNamed("GLS-A / 3F - Jordbrug Dyrehold 2026-2029");

        var payTimeBandRule = new PayTimeBandRule
        {
            PayDayTypeRuleId = payDayTypeRule.Id,
            StartSecondOfDay = 0,
            EndSecondOfDay = 64800,
            PayCode = "DAY",
            Priority = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WorkflowState = Constants.WorkflowStates.Created
        };
        await payTimeBandRule.Create(TimePlanningPnDbContext);

        var updateModel = new PayTimeBandRuleUpdateModel
        {
            StartSecondOfDay = 3600,
            EndSecondOfDay = 7200,
            PayCode = "HACKED",
            Priority = 9
        };

        // Act
        var result = await _service.Update(payTimeBandRule.Id, updateModel);

        // Assert
        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Does.Contain("CannotEditLockedPreset"));
    }

    [Test]
    public async Task Delete_OwnedByLockedPresetWithLegacyValidityPeriod_ReturnsFailure()
    {
        // Arrange
        var payDayTypeRule = await CreatePayDayTypeRuleUnderRuleSetNamed("GLS-A / 3F - Jordbrug Dyrehold 2024-2026");

        var payTimeBandRule = new PayTimeBandRule
        {
            PayDayTypeRuleId = payDayTypeRule.Id,
            StartSecondOfDay = 0,
            EndSecondOfDay = 64800,
            PayCode = "DAY",
            Priority = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WorkflowState = Constants.WorkflowStates.Created
        };
        await payTimeBandRule.Create(TimePlanningPnDbContext);

        // Act
        var result = await _service.Delete(payTimeBandRule.Id);

        // Assert
        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Does.Contain("CannotEditLockedPreset"));
        var stillThere = await TimePlanningPnDbContext.PayTimeBandRules
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(ptbr => ptbr.Id == payTimeBandRule.Id);
        Assert.That(stillThere, Is.Not.Null);
        Assert.That(stillThere.WorkflowState, Is.EqualTo(Constants.WorkflowStates.Created));
    }

    [Test]
    public async Task CreateUpdateDelete_OwnedByCustomNameWithValidityPeriod_Succeed()
    {
        // Arrange - stripping the year range must not make this collide with a preset
        var payDayTypeRule = await CreatePayDayTypeRuleUnderRuleSetNamed("Min egen aftale 2024-2026");

        var createModel = new PayTimeBandRuleCreateModel
        {
            PayDayTypeRuleId = payDayTypeRule.Id,
            StartSecondOfDay = 0,
            EndSecondOfDay = 64800,
            PayCode = "CUSTOM_DAY",
            Priority = 1
        };

        // Act - Create
        var createResult = await _service.Create(createModel);

        // Assert - Create
        Assert.That(createResult.Success, Is.True);
        var created = await TimePlanningPnDbContext.PayTimeBandRules
            .Where(ptbr => ptbr.WorkflowState != Constants.WorkflowStates.Removed)
            .FirstOrDefaultAsync(ptbr => ptbr.PayCode == "CUSTOM_DAY");
        Assert.That(created, Is.Not.Null);

        // Act - Update
        var updateResult = await _service.Update(created.Id, new PayTimeBandRuleUpdateModel
        {
            StartSecondOfDay = 3600,
            EndSecondOfDay = 7200,
            PayCode = "CUSTOM_NIGHT",
            Priority = 2
        });

        // Assert - Update
        Assert.That(updateResult.Success, Is.True);

        // Act - Delete
        var deleteResult = await _service.Delete(created.Id);

        // Assert - Delete
        Assert.That(deleteResult.Success, Is.True);
        var deleted = await TimePlanningPnDbContext.PayTimeBandRules
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(ptbr => ptbr.Id == created.Id);
        Assert.That(deleted, Is.Not.Null);
        Assert.That(deleted.WorkflowState, Is.EqualTo(Constants.WorkflowStates.Removed));
    }

    #endregion
}
