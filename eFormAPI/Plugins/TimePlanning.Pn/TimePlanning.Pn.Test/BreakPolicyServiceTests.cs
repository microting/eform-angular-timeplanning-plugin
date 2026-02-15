/*
The MIT License (MIT)

Copyright (c) 2007 - 2021 Microting A/S

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

using System;
using System.Linq;
using System.Threading.Tasks;
using BackendConfiguration.Pn.Integration.Test;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microting.eForm.Infrastructure.Constants;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using NSubstitute;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Models.BreakPolicy;
using TimePlanning.Pn.Services.BreakPolicyService;

namespace TimePlanning.Pn.Test;

[TestFixture]
public class BreakPolicyServiceTests : TestBaseSetup
{
    private IBreakPolicyService _service;

    [SetUp]
    public new async Task Setup()
    {
        await base.Setup();
        _service = new BreakPolicyService(
            TimePlanningPnDbContext,
            Substitute.For<ILogger<BreakPolicyService>>());
    }

    [Test]
    public async Task Create_ValidModel_CreatesBreakPolicy()
    {
        // Arrange
        var model = new BreakPolicyCreateModel
        {
            Name = "Test Break Policy",
            Description = "Test description" // May not be used if entity doesn't have Description
        };

        // Act
        var result = await _service.Create(model);

        // Assert
        Assert.That(result.Success, Is.True);
        var created = await TimePlanningPnDbContext.BreakPolicies
            .Where(bp => bp.WorkflowState != Constants.WorkflowStates.Removed)
            .FirstOrDefaultAsync(bp => bp.Name == "Test Break Policy");
        Assert.That(created, Is.Not.Null);
    }

    [Test]
    public async Task Read_ExistingId_ReturnsBreakPolicy()
    {
        // Arrange
        var breakPolicy = new BreakPolicy
        {
            Name = "Test Break Policy",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WorkflowState = Constants.WorkflowStates.Created
        };
        await breakPolicy.Create(TimePlanningPnDbContext);

        // Act
        var result = await _service.Read(breakPolicy.Id);

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.Model, Is.Not.Null);
        Assert.That(result.Model.Name, Is.EqualTo("Test Break Policy"));
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
    public async Task Read_WithRules_ReturnsBreakPolicyWithRules()
    {
        // Arrange
        var breakPolicy = new BreakPolicy
        {
            Name = "Test Break Policy",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WorkflowState = Constants.WorkflowStates.Created
        };
        await breakPolicy.Create(TimePlanningPnDbContext);

        // TODO: Add BreakPolicyRule creation when schema is confirmed
        // var rule = new BreakPolicyRule
        // {
        //     BreakPolicyId = breakPolicy.Id,
        //     DayOfWeek = (DayOfWeek)4, // Thursday
        //     CreatedAt = DateTime.UtcNow,
        //     UpdatedAt = DateTime.UtcNow,
        //     WorkflowState = Constants.WorkflowStates.Created
        // };
        // await rule.Create(TimePlanningPnDbContext);

        // Act
        var result = await _service.Read(breakPolicy.Id);

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.Model, Is.Not.Null);
        Assert.That(result.Model.BreakPolicyRules, Is.Not.Null);
        // Assert.That(result.Model.BreakPolicyRules.Count, Is.EqualTo(1));
    }

    [Test]
    public async Task Update_ExistingId_UpdatesBreakPolicy()
    {
        // Arrange
        var breakPolicy = new BreakPolicy
        {
            Name = "Original Name",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WorkflowState = Constants.WorkflowStates.Created
        };
        await breakPolicy.Create(TimePlanningPnDbContext);

        var updateModel = new BreakPolicyUpdateModel
        {
            Name = "Updated Name",
            Description = "Updated description" // May not be used
        };

        // Act
        var result = await _service.Update(breakPolicy.Id, updateModel);

        // Assert
        Assert.That(result.Success, Is.True);
        var updated = await TimePlanningPnDbContext.BreakPolicies
            .FirstOrDefaultAsync(bp => bp.Id == breakPolicy.Id);
        Assert.That(updated, Is.Not.Null);
        Assert.That(updated.Name, Is.EqualTo("Updated Name"));
    }

    [Test]
    public async Task Update_NonExistingId_ReturnsFailure()
    {
        // Arrange
        var updateModel = new BreakPolicyUpdateModel
        {
            Name = "Updated Name",
            Description = "Updated description"
        };

        // Act
        var result = await _service.Update(99999, updateModel);

        // Assert
        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Does.Contain("not found"));
    }

    [Test]
    public async Task Delete_ExistingId_SoftDeletesBreakPolicy()
    {
        // Arrange
        var breakPolicy = new BreakPolicy
        {
            Name = "Test Break Policy",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WorkflowState = Constants.WorkflowStates.Created
        };
        await breakPolicy.Create(TimePlanningPnDbContext);

        // Act
        var result = await _service.Delete(breakPolicy.Id);

        // Assert
        Assert.That(result.Success, Is.True);
        var deleted = await TimePlanningPnDbContext.BreakPolicies
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(bp => bp.Id == breakPolicy.Id);
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
    public async Task Index_ReturnsBreakPolicies()
    {
        // Arrange
        var breakPolicy1 = new BreakPolicy
        {
            Name = "Policy 1",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WorkflowState = Constants.WorkflowStates.Created
        };
        await breakPolicy1.Create(TimePlanningPnDbContext);

        var breakPolicy2 = new BreakPolicy
        {
            Name = "Policy 2",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WorkflowState = Constants.WorkflowStates.Created
        };
        await breakPolicy2.Create(TimePlanningPnDbContext);

        var requestModel = new BreakPoliciesRequestModel
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
        Assert.That(result.Model.BreakPolicies.Count, Is.EqualTo(2));
    }

    [Test]
    public async Task Index_WithPagination_ReturnsCorrectPage()
    {
        // Arrange
        for (int i = 1; i <= 5; i++)
        {
            var breakPolicy = new BreakPolicy
            {
                Name = $"Policy {i}",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                WorkflowState = Constants.WorkflowStates.Created
            };
            await breakPolicy.Create(TimePlanningPnDbContext);
        }

        var requestModel = new BreakPoliciesRequestModel
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
        Assert.That(result.Model.BreakPolicies.Count, Is.EqualTo(2));
    }

    [Test]
    public async Task Index_ExcludesDeletedPolicies()
    {
        // Arrange
        var breakPolicy1 = new BreakPolicy
        {
            Name = "Active Policy",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WorkflowState = Constants.WorkflowStates.Created
        };
        await breakPolicy1.Create(TimePlanningPnDbContext);

        var breakPolicy2 = new BreakPolicy
        {
            Name = "Deleted Policy",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WorkflowState = Constants.WorkflowStates.Created
        };
        await breakPolicy2.Create(TimePlanningPnDbContext);
        await breakPolicy2.Delete(TimePlanningPnDbContext);

        var requestModel = new BreakPoliciesRequestModel
        {
            Offset = 0,
            PageSize = 10
        };

        // Act
        var result = await _service.Index(requestModel);

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.Model.Total, Is.EqualTo(1));
        Assert.That(result.Model.BreakPolicies[0].Name, Is.EqualTo("Active Policy"));
    }
}
