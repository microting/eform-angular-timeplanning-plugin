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

namespace TimePlanning.Pn.Test;

[TestFixture]
public class PayTimeBandRuleServiceTests : TestBaseSetup
{
    private IPayTimeBandRuleService _service;

    [SetUp]
    public new async Task Setup()
    {
        await base.Setup();
        _service = new PayTimeBandRuleService(
            TimePlanningPnDbContext,
            Substitute.For<ILogger<PayTimeBandRuleService>>());
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
            DayType = DayType.Weekday,
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
            DayType = DayType.Weekday,
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
            DayType = DayType.Weekday,
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
            DayType = DayType.Weekday,
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
            DayType = DayType.Weekday,
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
            DayType = DayType.Weekday,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WorkflowState = Constants.WorkflowStates.Created
        };
        await payDayTypeRule1.Create(TimePlanningPnDbContext);

        var payDayTypeRule2 = new PayDayTypeRule
        {
            PayRuleSetId = payRuleSet.Id,
            DayType = DayType.Weekday,
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
            DayType = DayType.Weekday,
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
            DayType = DayType.Weekday,
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
}
