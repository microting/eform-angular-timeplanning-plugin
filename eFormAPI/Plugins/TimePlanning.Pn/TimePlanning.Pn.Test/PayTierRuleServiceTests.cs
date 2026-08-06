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
using NSubstitute;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Models.PayTierRule;
using TimePlanning.Pn.Services.PayTierRuleService;
using TimePlanning.Pn.Services.TimePlanningLocalizationService;

namespace TimePlanning.Pn.Test;

[TestFixture]
public class PayTierRuleServiceTests : TestBaseSetup
{
    private IPayTierRuleService _service;

    [SetUp]
    public new async Task Setup()
    {
        await base.Setup();
        var localizationService = Substitute.For<ITimePlanningLocalizationService>();
        localizationService.GetString(Arg.Any<string>())
            .Returns(call => call.Arg<string>());
        _service = new PayTierRuleService(
            TimePlanningPnDbContext,
            Substitute.For<ILogger<PayTierRuleService>>(),
            localizationService);
    }

    [Test]
    public async Task Create_ValidModel_CreatesPayTierRule()
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

        var payDayRule = new PayDayRule
        {
            PayRuleSetId = payRuleSet.Id,
            DayCode = "SUNDAY",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WorkflowState = Constants.WorkflowStates.Created
        };
        await payDayRule.Create(TimePlanningPnDbContext);

        var model = new PayTierRuleCreateModel
        {
            PayDayRuleId = payDayRule.Id,
            Order = 1,
            UpToSeconds = 39600,
            PayCode = "SUN_80"
        };

        // Act
        var result = await _service.Create(model);

        // Assert
        Assert.That(result.Success, Is.True);
        var created = await TimePlanningPnDbContext.PayTierRules
            .Where(ptr => ptr.WorkflowState != Constants.WorkflowStates.Removed)
            .FirstOrDefaultAsync(ptr => ptr.PayCode == "SUN_80");
        Assert.That(created, Is.Not.Null);
        Assert.That(created.Order, Is.EqualTo(1));
        Assert.That(created.UpToSeconds, Is.EqualTo(39600));
    }

    [Test]
    public async Task Read_ExistingId_ReturnsPayTierRule()
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

        var payDayRule = new PayDayRule
        {
            PayRuleSetId = payRuleSet.Id,
            DayCode = "SUNDAY",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WorkflowState = Constants.WorkflowStates.Created
        };
        await payDayRule.Create(TimePlanningPnDbContext);

        var payTierRule = new PayTierRule
        {
            PayDayRuleId = payDayRule.Id,
            Order = 1,
            UpToSeconds = 39600,
            PayCode = "SUN_80",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WorkflowState = Constants.WorkflowStates.Created
        };
        await payTierRule.Create(TimePlanningPnDbContext);

        // Act
        var result = await _service.Read(payTierRule.Id);

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.Model, Is.Not.Null);
        Assert.That(result.Model.PayCode, Is.EqualTo("SUN_80"));
        Assert.That(result.Model.Order, Is.EqualTo(1));
        Assert.That(result.Model.UpToSeconds, Is.EqualTo(39600));
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
    public async Task Update_ExistingId_UpdatesPayTierRule()
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

        var payDayRule = new PayDayRule
        {
            PayRuleSetId = payRuleSet.Id,
            DayCode = "SUNDAY",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WorkflowState = Constants.WorkflowStates.Created
        };
        await payDayRule.Create(TimePlanningPnDbContext);

        var payTierRule = new PayTierRule
        {
            PayDayRuleId = payDayRule.Id,
            Order = 1,
            UpToSeconds = 39600,
            PayCode = "SUN_80",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WorkflowState = Constants.WorkflowStates.Created
        };
        await payTierRule.Create(TimePlanningPnDbContext);

        var updateModel = new PayTierRuleUpdateModel
        {
            Order = 2,
            UpToSeconds = 43200,
            PayCode = "SUN_90"
        };

        // Act
        var result = await _service.Update(payTierRule.Id, updateModel);

        // Assert
        Assert.That(result.Success, Is.True);
        var updated = await TimePlanningPnDbContext.PayTierRules
            .FirstOrDefaultAsync(ptr => ptr.Id == payTierRule.Id);
        Assert.That(updated, Is.Not.Null);
        Assert.That(updated.Order, Is.EqualTo(2));
        Assert.That(updated.UpToSeconds, Is.EqualTo(43200));
        Assert.That(updated.PayCode, Is.EqualTo("SUN_90"));
    }

    [Test]
    public async Task Update_NonExistingId_ReturnsFailure()
    {
        // Arrange
        var updateModel = new PayTierRuleUpdateModel
        {
            Order = 2,
            UpToSeconds = 43200,
            PayCode = "SUN_90"
        };

        // Act
        var result = await _service.Update(99999, updateModel);

        // Assert
        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Does.Contain("not found"));
    }

    [Test]
    public async Task Delete_ExistingId_SoftDeletesPayTierRule()
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

        var payDayRule = new PayDayRule
        {
            PayRuleSetId = payRuleSet.Id,
            DayCode = "SUNDAY",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WorkflowState = Constants.WorkflowStates.Created
        };
        await payDayRule.Create(TimePlanningPnDbContext);

        var payTierRule = new PayTierRule
        {
            PayDayRuleId = payDayRule.Id,
            Order = 1,
            UpToSeconds = 39600,
            PayCode = "SUN_80",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WorkflowState = Constants.WorkflowStates.Created
        };
        await payTierRule.Create(TimePlanningPnDbContext);

        // Act
        var result = await _service.Delete(payTierRule.Id);

        // Assert
        Assert.That(result.Success, Is.True);
        var deleted = await TimePlanningPnDbContext.PayTierRules
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(ptr => ptr.Id == payTierRule.Id);
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

    #region Locked Preset Guard Tests

    /// <summary>
    /// Creates a PayDayRule under a PayRuleSet with the supplied name and
    /// returns it, so the locked-preset tests only differ by that name.
    /// </summary>
    private async Task<PayDayRule> CreatePayDayRuleUnderRuleSetNamed(string payRuleSetName)
    {
        var payRuleSet = new PayRuleSet
        {
            Name = payRuleSetName,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WorkflowState = Constants.WorkflowStates.Created
        };
        await payRuleSet.Create(TimePlanningPnDbContext);

        var payDayRule = new PayDayRule
        {
            PayRuleSetId = payRuleSet.Id,
            DayCode = "SUNDAY",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WorkflowState = Constants.WorkflowStates.Created
        };
        await payDayRule.Create(TimePlanningPnDbContext);

        return payDayRule;
    }

    [Test]
    public async Task Create_OwnedByLockedPresetWithLegacyValidityPeriod_ReturnsFailure()
    {
        // Arrange - stored before the catalogue was renamed to "... 2026-2029"
        var payDayRule = await CreatePayDayRuleUnderRuleSetNamed("GLS-A / 3F - Jordbrug Dyrehold 2024-2026");

        var model = new PayTierRuleCreateModel
        {
            PayDayRuleId = payDayRule.Id,
            Order = 1,
            UpToSeconds = 39600,
            PayCode = "SNEAKED_IN"
        };

        // Act
        var result = await _service.Create(model);

        // Assert
        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Does.Contain("CannotEditLockedPreset"));
        var created = await TimePlanningPnDbContext.PayTierRules
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(ptr => ptr.PayCode == "SNEAKED_IN");
        Assert.That(created, Is.Null);
    }

    [Test]
    public async Task Update_OwnedByLockedPresetWithLegacyValidityPeriod_ReturnsFailure()
    {
        // Arrange
        var payDayRule = await CreatePayDayRuleUnderRuleSetNamed("GLS-A / 3F - Jordbrug Dyrehold 2024-2026");

        var payTierRule = new PayTierRule
        {
            PayDayRuleId = payDayRule.Id,
            Order = 1,
            UpToSeconds = 39600,
            PayCode = "SUN_80",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WorkflowState = Constants.WorkflowStates.Created
        };
        await payTierRule.Create(TimePlanningPnDbContext);

        var updateModel = new PayTierRuleUpdateModel
        {
            Order = 2,
            UpToSeconds = 43200,
            PayCode = "SUN_90"
        };

        // Act
        var result = await _service.Update(payTierRule.Id, updateModel);

        // Assert
        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Does.Contain("CannotEditLockedPreset"));
        var unchanged = await TimePlanningPnDbContext.PayTierRules
            .FirstOrDefaultAsync(ptr => ptr.Id == payTierRule.Id);
        Assert.That(unchanged, Is.Not.Null);
        Assert.That(unchanged.Order, Is.EqualTo(1));
        Assert.That(unchanged.UpToSeconds, Is.EqualTo(39600));
        Assert.That(unchanged.PayCode, Is.EqualTo("SUN_80"));
    }

    [Test]
    public async Task Update_OwnedByLockedPresetWithCurrentValidityPeriod_ReturnsFailure()
    {
        // Arrange
        var payDayRule = await CreatePayDayRuleUnderRuleSetNamed("GLS-A / 3F - Jordbrug Dyrehold 2026-2029");

        var payTierRule = new PayTierRule
        {
            PayDayRuleId = payDayRule.Id,
            Order = 1,
            UpToSeconds = 39600,
            PayCode = "SUN_80",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WorkflowState = Constants.WorkflowStates.Created
        };
        await payTierRule.Create(TimePlanningPnDbContext);

        var updateModel = new PayTierRuleUpdateModel
        {
            Order = 2,
            UpToSeconds = 43200,
            PayCode = "SUN_90"
        };

        // Act
        var result = await _service.Update(payTierRule.Id, updateModel);

        // Assert
        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Does.Contain("CannotEditLockedPreset"));
    }

    [Test]
    public async Task Delete_OwnedByLockedPresetWithLegacyValidityPeriod_ReturnsFailure()
    {
        // Arrange
        var payDayRule = await CreatePayDayRuleUnderRuleSetNamed("GLS-A / 3F - Jordbrug Dyrehold 2024-2026");

        var payTierRule = new PayTierRule
        {
            PayDayRuleId = payDayRule.Id,
            Order = 1,
            UpToSeconds = 39600,
            PayCode = "SUN_80",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WorkflowState = Constants.WorkflowStates.Created
        };
        await payTierRule.Create(TimePlanningPnDbContext);

        // Act
        var result = await _service.Delete(payTierRule.Id);

        // Assert
        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Does.Contain("CannotEditLockedPreset"));
        var stillThere = await TimePlanningPnDbContext.PayTierRules
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(ptr => ptr.Id == payTierRule.Id);
        Assert.That(stillThere, Is.Not.Null);
        Assert.That(stillThere.WorkflowState, Is.EqualTo(Constants.WorkflowStates.Created));
    }

    [Test]
    public async Task CreateUpdateDelete_OwnedByCustomNameWithValidityPeriod_Succeed()
    {
        // Arrange - stripping the year range must not make this collide with a preset
        var payDayRule = await CreatePayDayRuleUnderRuleSetNamed("Min egen aftale 2024-2026");

        var createModel = new PayTierRuleCreateModel
        {
            PayDayRuleId = payDayRule.Id,
            Order = 1,
            UpToSeconds = 39600,
            PayCode = "CUSTOM_80"
        };

        // Act - Create
        var createResult = await _service.Create(createModel);

        // Assert - Create
        Assert.That(createResult.Success, Is.True);
        var created = await TimePlanningPnDbContext.PayTierRules
            .Where(ptr => ptr.WorkflowState != Constants.WorkflowStates.Removed)
            .FirstOrDefaultAsync(ptr => ptr.PayCode == "CUSTOM_80");
        Assert.That(created, Is.Not.Null);

        // Act - Update
        var updateResult = await _service.Update(created.Id, new PayTierRuleUpdateModel
        {
            Order = 2,
            UpToSeconds = 43200,
            PayCode = "CUSTOM_90"
        });

        // Assert - Update
        Assert.That(updateResult.Success, Is.True);

        // Act - Delete
        var deleteResult = await _service.Delete(created.Id);

        // Assert - Delete
        Assert.That(deleteResult.Success, Is.True);
        var deleted = await TimePlanningPnDbContext.PayTierRules
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(ptr => ptr.Id == created.Id);
        Assert.That(deleted, Is.Not.Null);
        Assert.That(deleted.WorkflowState, Is.EqualTo(Constants.WorkflowStates.Removed));
    }

    #endregion

    [Test]
    public async Task Index_ReturnsPayTierRules()
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

        var payDayRule = new PayDayRule
        {
            PayRuleSetId = payRuleSet.Id,
            DayCode = "SUNDAY",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WorkflowState = Constants.WorkflowStates.Created
        };
        await payDayRule.Create(TimePlanningPnDbContext);

        var payTierRule1 = new PayTierRule
        {
            PayDayRuleId = payDayRule.Id,
            Order = 1,
            UpToSeconds = 39600,
            PayCode = "SUN_80",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WorkflowState = Constants.WorkflowStates.Created
        };
        await payTierRule1.Create(TimePlanningPnDbContext);

        var payTierRule2 = new PayTierRule
        {
            PayDayRuleId = payDayRule.Id,
            Order = 2,
            UpToSeconds = null,
            PayCode = "SUN_100",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WorkflowState = Constants.WorkflowStates.Created
        };
        await payTierRule2.Create(TimePlanningPnDbContext);

        var requestModel = new PayTierRulesRequestModel
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
        Assert.That(result.Model.PayTierRules.Count, Is.EqualTo(2));
    }

    [Test]
    public async Task Index_WithPayDayRuleIdFilter_ReturnsFilteredRules()
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

        var payDayRule1 = new PayDayRule
        {
            PayRuleSetId = payRuleSet.Id,
            DayCode = "SUNDAY",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WorkflowState = Constants.WorkflowStates.Created
        };
        await payDayRule1.Create(TimePlanningPnDbContext);

        var payDayRule2 = new PayDayRule
        {
            PayRuleSetId = payRuleSet.Id,
            DayCode = "SATURDAY",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WorkflowState = Constants.WorkflowStates.Created
        };
        await payDayRule2.Create(TimePlanningPnDbContext);

        var payTierRule1 = new PayTierRule
        {
            PayDayRuleId = payDayRule1.Id,
            Order = 1,
            UpToSeconds = 39600,
            PayCode = "SUN_80",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WorkflowState = Constants.WorkflowStates.Created
        };
        await payTierRule1.Create(TimePlanningPnDbContext);

        var payTierRule2 = new PayTierRule
        {
            PayDayRuleId = payDayRule2.Id,
            Order = 1,
            UpToSeconds = 39600,
            PayCode = "SAT_80",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WorkflowState = Constants.WorkflowStates.Created
        };
        await payTierRule2.Create(TimePlanningPnDbContext);

        var requestModel = new PayTierRulesRequestModel
        {
            Offset = 0,
            PageSize = 10,
            PayDayRuleId = payDayRule1.Id
        };

        // Act
        var result = await _service.Index(requestModel);

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.Model.Total, Is.EqualTo(1));
        Assert.That(result.Model.PayTierRules[0].PayCode, Is.EqualTo("SUN_80"));
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

        var payDayRule = new PayDayRule
        {
            PayRuleSetId = payRuleSet.Id,
            DayCode = "SUNDAY",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WorkflowState = Constants.WorkflowStates.Created
        };
        await payDayRule.Create(TimePlanningPnDbContext);

        // Create in reverse order
        var payTierRule3 = new PayTierRule
        {
            PayDayRuleId = payDayRule.Id,
            Order = 3,
            UpToSeconds = null,
            PayCode = "SUN_100",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WorkflowState = Constants.WorkflowStates.Created
        };
        await payTierRule3.Create(TimePlanningPnDbContext);

        var payTierRule1 = new PayTierRule
        {
            PayDayRuleId = payDayRule.Id,
            Order = 1,
            UpToSeconds = 39600,
            PayCode = "SUN_80",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WorkflowState = Constants.WorkflowStates.Created
        };
        await payTierRule1.Create(TimePlanningPnDbContext);

        var payTierRule2 = new PayTierRule
        {
            PayDayRuleId = payDayRule.Id,
            Order = 2,
            UpToSeconds = 43200,
            PayCode = "SUN_90",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WorkflowState = Constants.WorkflowStates.Created
        };
        await payTierRule2.Create(TimePlanningPnDbContext);

        var requestModel = new PayTierRulesRequestModel
        {
            Offset = 0,
            PageSize = 10
        };

        // Act
        var result = await _service.Index(requestModel);

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.Model.Total, Is.EqualTo(3));
        Assert.That(result.Model.PayTierRules[0].Order, Is.EqualTo(1));
        Assert.That(result.Model.PayTierRules[0].PayCode, Is.EqualTo("SUN_80"));
        Assert.That(result.Model.PayTierRules[1].Order, Is.EqualTo(2));
        Assert.That(result.Model.PayTierRules[1].PayCode, Is.EqualTo("SUN_90"));
        Assert.That(result.Model.PayTierRules[2].Order, Is.EqualTo(3));
        Assert.That(result.Model.PayTierRules[2].PayCode, Is.EqualTo("SUN_100"));
    }

    [Test]
    public async Task Index_ExcludesDeletedPayTierRules()
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

        var payDayRule = new PayDayRule
        {
            PayRuleSetId = payRuleSet.Id,
            DayCode = "SUNDAY",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WorkflowState = Constants.WorkflowStates.Created
        };
        await payDayRule.Create(TimePlanningPnDbContext);

        var payTierRule1 = new PayTierRule
        {
            PayDayRuleId = payDayRule.Id,
            Order = 1,
            UpToSeconds = 39600,
            PayCode = "Active",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WorkflowState = Constants.WorkflowStates.Created
        };
        await payTierRule1.Create(TimePlanningPnDbContext);

        var payTierRule2 = new PayTierRule
        {
            PayDayRuleId = payDayRule.Id,
            Order = 2,
            UpToSeconds = null,
            PayCode = "Deleted",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WorkflowState = Constants.WorkflowStates.Created
        };
        await payTierRule2.Create(TimePlanningPnDbContext);
        await payTierRule2.Delete(TimePlanningPnDbContext);

        var requestModel = new PayTierRulesRequestModel
        {
            Offset = 0,
            PageSize = 10
        };

        // Act
        var result = await _service.Index(requestModel);

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.Model.Total, Is.EqualTo(1));
        Assert.That(result.Model.PayTierRules[0].PayCode, Is.EqualTo("Active"));
    }
}
