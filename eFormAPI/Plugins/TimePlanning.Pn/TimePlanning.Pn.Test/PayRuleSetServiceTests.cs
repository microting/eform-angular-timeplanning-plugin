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
using TimePlanning.Pn.Infrastructure.Models.PayRuleSet;
using TimePlanning.Pn.Services.PayRuleSetService;

namespace TimePlanning.Pn.Test;

[TestFixture]
public class PayRuleSetServiceTests : TestBaseSetup
{
    private IPayRuleSetService _service;

    [SetUp]
    public new async Task Setup()
    {
        await base.Setup();
        _service = new PayRuleSetService(
            TimePlanningPnDbContext,
            Substitute.For<ILogger<PayRuleSetService>>());
    }

    [Test]
    public async Task Create_ValidModel_CreatesPayRuleSet()
    {
        // Arrange
        var model = new PayRuleSetCreateModel
        {
            Name = "Test Pay Rule Set"
        };

        // Act
        var result = await _service.Create(model);

        // Assert
        Assert.That(result.Success, Is.True);
        var created = await TimePlanningPnDbContext.PayRuleSets
            .Where(prs => prs.WorkflowState != Constants.WorkflowStates.Removed)
            .FirstOrDefaultAsync(prs => prs.Name == "Test Pay Rule Set");
        Assert.That(created, Is.Not.Null);
    }

    [Test]
    public async Task Read_ExistingId_ReturnsPayRuleSet()
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

        // Act
        var result = await _service.Read(payRuleSet.Id);

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.Model, Is.Not.Null);
        Assert.That(result.Model.Name, Is.EqualTo("Test Pay Rule Set"));
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
    public async Task Update_ExistingId_UpdatesPayRuleSet()
    {
        // Arrange
        var payRuleSet = new PayRuleSet
        {
            Name = "Original Name",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WorkflowState = Constants.WorkflowStates.Created
        };
        await payRuleSet.Create(TimePlanningPnDbContext);

        var updateModel = new PayRuleSetUpdateModel
        {
            Name = "Updated Name"
        };

        // Act
        var result = await _service.Update(payRuleSet.Id, updateModel);

        // Assert
        Assert.That(result.Success, Is.True);
        var updated = await TimePlanningPnDbContext.PayRuleSets
            .FirstOrDefaultAsync(prs => prs.Id == payRuleSet.Id);
        Assert.That(updated, Is.Not.Null);
        Assert.That(updated.Name, Is.EqualTo("Updated Name"));
    }

    [Test]
    public async Task Update_NonExistingId_ReturnsFailure()
    {
        // Arrange
        var updateModel = new PayRuleSetUpdateModel
        {
            Name = "Updated Name"
        };

        // Act
        var result = await _service.Update(99999, updateModel);

        // Assert
        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Does.Contain("not found"));
    }

    [Test]
    public async Task Delete_ExistingId_SoftDeletesPayRuleSet()
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

        // Act
        var result = await _service.Delete(payRuleSet.Id);

        // Assert
        Assert.That(result.Success, Is.True);
        var deleted = await TimePlanningPnDbContext.PayRuleSets
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(prs => prs.Id == payRuleSet.Id);
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
    public async Task Index_ReturnsPayRuleSets()
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

        var requestModel = new PayRuleSetsRequestModel
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
        Assert.That(result.Model.PayRuleSets.Count, Is.EqualTo(2));
    }

    [Test]
    public async Task Index_WithPagination_ReturnsCorrectPage()
    {
        // Arrange
        for (int i = 1; i <= 5; i++)
        {
            var payRuleSet = new PayRuleSet
            {
                Name = $"Pay Rule Set {i}",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                WorkflowState = Constants.WorkflowStates.Created
            };
            await payRuleSet.Create(TimePlanningPnDbContext);
        }

        var requestModel = new PayRuleSetsRequestModel
        {
            Offset = 2,
            PageSize = 2
        };

        // Act
        var result = await _service.Index(requestModel);

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.Model, Is.Not.Null);
        Assert.That(result.Model.Total, Is.EqualTo(5));
        Assert.That(result.Model.PayRuleSets.Count, Is.EqualTo(2));
    }

    [Test]
    public async Task Index_ExcludesDeletedPayRuleSets()
    {
        // Arrange
        var payRuleSet1 = new PayRuleSet
        {
            Name = "Active Pay Rule Set",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WorkflowState = Constants.WorkflowStates.Created
        };
        await payRuleSet1.Create(TimePlanningPnDbContext);

        var payRuleSet2 = new PayRuleSet
        {
            Name = "Deleted Pay Rule Set",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WorkflowState = Constants.WorkflowStates.Created
        };
        await payRuleSet2.Create(TimePlanningPnDbContext);
        await payRuleSet2.Delete(TimePlanningPnDbContext);

        var requestModel = new PayRuleSetsRequestModel
        {
            Offset = 0,
            PageSize = 10
        };

        // Act
        var result = await _service.Index(requestModel);

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.Model.Total, Is.EqualTo(1));
        Assert.That(result.Model.PayRuleSets[0].Name, Is.EqualTo("Active Pay Rule Set"));
    }
}
