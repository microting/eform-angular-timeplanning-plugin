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
using TimePlanning.Pn.Infrastructure.Models.PayTierRule;
using TimePlanning.Pn.Services.PayRuleSetService;
using TimePlanning.Pn.Services.TimePlanningLocalizationService;

namespace TimePlanning.Pn.Test;

[TestFixture]
public class PayRuleSetServiceTests : TestBaseSetup
{
    private IPayRuleSetService _service;

    [SetUp]
    public new async Task Setup()
    {
        await base.Setup();
        var localizationService = Substitute.For<ITimePlanningLocalizationService>();
        localizationService.GetString(Arg.Any<string>())
            .Returns(call => call.Arg<string>());
        _service = new PayRuleSetService(
            TimePlanningPnDbContext,
            Substitute.For<ILogger<PayRuleSetService>>(),
            localizationService);
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
        Assert.That(result.Message, Does.Contain("NotFound"));
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
        Assert.That(result.Message, Does.Contain("NotFound"));
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
        Assert.That(result.Message, Does.Contain("NotFound"));
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

    #region Nested Entity Tests

    [Test]
    public async Task Create_WithNestedPayDayRuleAndPayTierRule_CreatesAllEntities()
    {
        // Arrange
        var model = new PayRuleSetCreateModel
        {
            Name = "Test Pay Rule Set with Nested Entities",
            PayDayRules = new System.Collections.Generic.List<PayDayRuleModel>
            {
                new PayDayRuleModel
                {
                    DayCode = "MONDAY",
                    PayTierRules = new System.Collections.Generic.List<PayTierRuleModel>
                    {
                        new PayTierRuleModel
                        {
                            Order = 1,
                            UpToSeconds = 28800, // 8 hours
                            PayCode = "REG"
                        },
                        new PayTierRuleModel
                        {
                            Order = 2,
                            UpToSeconds = null, // unlimited
                            PayCode = "OT"
                        }
                    }
                }
            }
        };

        // Act
        var result = await _service.Create(model);

        // Assert
        Assert.That(result.Success, Is.True, $"Create failed: {result.Message}");
        
        var created = await TimePlanningPnDbContext.PayRuleSets
            .Where(prs => prs.WorkflowState != Constants.WorkflowStates.Removed)
            .FirstOrDefaultAsync(prs => prs.Name == "Test Pay Rule Set with Nested Entities");
        
        Assert.That(created, Is.Not.Null, "PayRuleSet was not created");
        
        var payDayRules = await TimePlanningPnDbContext.PayDayRules
            .Where(pdr => pdr.PayRuleSetId == created.Id && pdr.WorkflowState != Constants.WorkflowStates.Removed)
            .ToListAsync();
        
        Assert.That(payDayRules, Is.Not.Null, "PayDayRules collection is null");
        Assert.That(payDayRules.Count, Is.EqualTo(1), "Expected 1 PayDayRule");
        
        var dayRule = payDayRules.First();
        Assert.That(dayRule.DayCode, Is.EqualTo("MONDAY"));
        
        var payTierRules = await TimePlanningPnDbContext.PayTierRules
            .Where(ptr => ptr.PayDayRuleId == dayRule.Id && ptr.WorkflowState != Constants.WorkflowStates.Removed)
            .ToListAsync();
        
        Assert.That(payTierRules, Is.Not.Null, "PayTierRules collection is null");
        Assert.That(payTierRules.Count, Is.EqualTo(2), "Expected 2 PayTierRules");
        
        var tierRule1 = payTierRules.First(ptr => ptr.Order == 1);
        Assert.That(tierRule1.UpToSeconds, Is.EqualTo(28800));
        Assert.That(tierRule1.PayCode, Is.EqualTo("REG"));
        
        var tierRule2 = payTierRules.First(ptr => ptr.Order == 2);
        Assert.That(tierRule2.UpToSeconds, Is.Null);
        Assert.That(tierRule2.PayCode, Is.EqualTo("OT"));
    }

    [Test]
    public async Task Create_WithMultiplePayDayRules_CreatesAllEntities()
    {
        // Arrange
        var model = new PayRuleSetCreateModel
        {
            Name = "Multi-Day Pay Rule Set",
            PayDayRules = new System.Collections.Generic.List<PayDayRuleModel>
            {
                new PayDayRuleModel
                {
                    DayCode = "MONDAY",
                    PayTierRules = new System.Collections.Generic.List<PayTierRuleModel>
                    {
                        new PayTierRuleModel { Order = 1, UpToSeconds = 28800, PayCode = "REG" }
                    }
                },
                new PayDayRuleModel
                {
                    DayCode = "SUNDAY",
                    PayTierRules = new System.Collections.Generic.List<PayTierRuleModel>
                    {
                        new PayTierRuleModel { Order = 1, UpToSeconds = null, PayCode = "HOLIDAY" }
                    }
                }
            }
        };

        // Act
        var result = await _service.Create(model);

        // Assert
        Assert.That(result.Success, Is.True);
        
        var created = await TimePlanningPnDbContext.PayRuleSets
            .FirstOrDefaultAsync(prs => prs.Name == "Multi-Day Pay Rule Set");
        
        Assert.That(created, Is.Not.Null);
        
        var payDayRules = await TimePlanningPnDbContext.PayDayRules
            .Where(pdr => pdr.PayRuleSetId == created.Id && pdr.WorkflowState != Constants.WorkflowStates.Removed)
            .ToListAsync();
        
        Assert.That(payDayRules.Count, Is.EqualTo(2));
        Assert.That(payDayRules.Any(pdr => pdr.DayCode == "MONDAY"), Is.True);
        Assert.That(payDayRules.Any(pdr => pdr.DayCode == "SUNDAY"), Is.True);
    }

    [Test]
    public async Task Update_WithNestedEntities_UpdatesAllLevels()
    {
        // Arrange - Create initial PayRuleSet with nested entities
        var payRuleSet = new PayRuleSet
        {
            Name = "Original Name",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WorkflowState = Constants.WorkflowStates.Created
        };
        await payRuleSet.Create(TimePlanningPnDbContext);

        var payDayRule = new PayDayRule
        {
            PayRuleSetId = payRuleSet.Id,
            DayCode = "MONDAY",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WorkflowState = Constants.WorkflowStates.Created
        };
        await payDayRule.Create(TimePlanningPnDbContext);

        var payTierRule = new PayTierRule
        {
            PayDayRuleId = payDayRule.Id,
            Order = 1,
            UpToSeconds = 28800,
            PayCode = "REG",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WorkflowState = Constants.WorkflowStates.Created
        };
        await payTierRule.Create(TimePlanningPnDbContext);

        // Update model with modified nested data
        var updateModel = new PayRuleSetUpdateModel
        {
            Name = "Updated Name",
            PayDayRules = new System.Collections.Generic.List<PayDayRuleModel>
            {
                new PayDayRuleModel
                {
                    Id = payDayRule.Id,
                    DayCode = "MONDAY",
                    PayTierRules = new System.Collections.Generic.List<PayTierRuleModel>
                    {
                        new PayTierRuleModel
                        {
                            Id = payTierRule.Id,
                            Order = 1,
                            UpToSeconds = 36000, // Changed from 28800 to 36000
                            PayCode = "REG"
                        },
                        new PayTierRuleModel // New tier
                        {
                            Order = 2,
                            UpToSeconds = null,
                            PayCode = "OT"
                        }
                    }
                }
            }
        };

        // Act
        var result = await _service.Update(payRuleSet.Id, updateModel);

        // Assert
        Assert.That(result.Success, Is.True, $"Update failed: {result.Message}");
        
        var updated = await TimePlanningPnDbContext.PayRuleSets
            .FirstOrDefaultAsync(prs => prs.Id == payRuleSet.Id);
        
        Assert.That(updated, Is.Not.Null);
        Assert.That(updated.Name, Is.EqualTo("Updated Name"));
        
        var payDayRules = await TimePlanningPnDbContext.PayDayRules
            .Where(pdr => pdr.PayRuleSetId == payRuleSet.Id && pdr.WorkflowState != Constants.WorkflowStates.Removed)
            .ToListAsync();
        
        Assert.That(payDayRules.Count, Is.EqualTo(1));
        
        var dayRule = payDayRules.First();
        var payTierRules = await TimePlanningPnDbContext.PayTierRules
            .Where(ptr => ptr.PayDayRuleId == dayRule.Id && ptr.WorkflowState != Constants.WorkflowStates.Removed)
            .ToListAsync();
        
        Assert.That(payTierRules.Count, Is.EqualTo(2), "Expected 2 PayTierRules after update");
        
        var existingTier = payTierRules.First(ptr => ptr.Id == payTierRule.Id);
        Assert.That(existingTier.UpToSeconds, Is.EqualTo(36000), "Existing tier should be updated");
        
        var newTier = payTierRules.First(ptr => ptr.Order == 2);
        Assert.That(newTier.PayCode, Is.EqualTo("OT"), "New tier should be created");
    }

    [Test]
    public async Task Update_AddingNewPayDayRule_CreatesNewRule()
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

        var updateModel = new PayRuleSetUpdateModel
        {
            Name = "Test Pay Rule Set",
            PayDayRules = new System.Collections.Generic.List<PayDayRuleModel>
            {
                new PayDayRuleModel
                {
                    DayCode = "SUNDAY",
                    PayTierRules = new System.Collections.Generic.List<PayTierRuleModel>
                    {
                        new PayTierRuleModel
                        {
                            Order = 1,
                            UpToSeconds = null,
                            PayCode = "HOLIDAY"
                        }
                    }
                }
            }
        };

        // Act
        var result = await _service.Update(payRuleSet.Id, updateModel);

        // Assert
        Assert.That(result.Success, Is.True);
        
        var updated = await TimePlanningPnDbContext.PayRuleSets
            .FirstOrDefaultAsync(prs => prs.Id == payRuleSet.Id);
        
        var payDayRules = await TimePlanningPnDbContext.PayDayRules
            .Where(pdr => pdr.PayRuleSetId == payRuleSet.Id && pdr.WorkflowState != Constants.WorkflowStates.Removed)
            .ToListAsync();
        
        Assert.That(payDayRules.Count, Is.EqualTo(1));
        Assert.That(payDayRules.First().DayCode, Is.EqualTo("SUNDAY"));
        
        var payTierRules = await TimePlanningPnDbContext.PayTierRules
            .Where(ptr => ptr.PayDayRuleId == payDayRules.First().Id && ptr.WorkflowState != Constants.WorkflowStates.Removed)
            .ToListAsync();
        
        Assert.That(payTierRules.Count, Is.EqualTo(1));
    }

    [Test]
    public async Task Update_RemovingPayDayRule_DeletesRule()
    {
        // Arrange - Create PayRuleSet with two PayDayRules
        var payRuleSet = new PayRuleSet
        {
            Name = "Test Pay Rule Set",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WorkflowState = Constants.WorkflowStates.Created
        };
        await payRuleSet.Create(TimePlanningPnDbContext);

        var payDayRule1 = new PayDayRule
        {
            PayRuleSetId = payRuleSet.Id,
            DayCode = "MONDAY",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WorkflowState = Constants.WorkflowStates.Created
        };
        await payDayRule1.Create(TimePlanningPnDbContext);

        var payDayRule2 = new PayDayRule
        {
            PayRuleSetId = payRuleSet.Id,
            DayCode = "SUNDAY",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WorkflowState = Constants.WorkflowStates.Created
        };
        await payDayRule2.Create(TimePlanningPnDbContext);

        // Update model with only one PayDayRule (removing SUNDAY)
        var updateModel = new PayRuleSetUpdateModel
        {
            Name = "Test Pay Rule Set",
            PayDayRules = new System.Collections.Generic.List<PayDayRuleModel>
            {
                new PayDayRuleModel
                {
                    Id = payDayRule1.Id,
                    DayCode = "MONDAY",
                    PayTierRules = new System.Collections.Generic.List<PayTierRuleModel>()
                }
            }
        };

        // Act
        var result = await _service.Update(payRuleSet.Id, updateModel);

        // Assert
        Assert.That(result.Success, Is.True);
        
        var updated = await TimePlanningPnDbContext.PayRuleSets
            .FirstOrDefaultAsync(prs => prs.Id == payRuleSet.Id);
        
        var payDayRules = await TimePlanningPnDbContext.PayDayRules
            .Where(pdr => pdr.PayRuleSetId == payRuleSet.Id && pdr.WorkflowState != Constants.WorkflowStates.Removed)
            .ToListAsync();
        
        Assert.That(payDayRules.Count, Is.EqualTo(1), "Should have only 1 active PayDayRule");
        Assert.That(payDayRules.Any(pdr => pdr.DayCode == "MONDAY"), Is.True, "MONDAY should still exist");
    }

    [Test]
    public async Task Read_WithNestedEntities_ReturnsCompleteHierarchy()
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

        var payDayRule = new PayDayRule
        {
            PayRuleSetId = payRuleSet.Id,
            DayCode = "MONDAY",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WorkflowState = Constants.WorkflowStates.Created
        };
        await payDayRule.Create(TimePlanningPnDbContext);

        var payTierRule1 = new PayTierRule
        {
            PayDayRuleId = payDayRule.Id,
            Order = 1,
            UpToSeconds = 28800,
            PayCode = "REG",
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
            PayCode = "OT",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WorkflowState = Constants.WorkflowStates.Created
        };
        await payTierRule2.Create(TimePlanningPnDbContext);

        // Act
        var result = await _service.Read(payRuleSet.Id);

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.Model, Is.Not.Null);
        Assert.That(result.Model.Name, Is.EqualTo("Test Pay Rule Set"));
        Assert.That(result.Model.PayDayRules, Is.Not.Null);
        Assert.That(result.Model.PayDayRules.Count, Is.EqualTo(1));
        
        var dayRuleModel = result.Model.PayDayRules.First();
        Assert.That(dayRuleModel.DayCode, Is.EqualTo("MONDAY"));
        Assert.That(dayRuleModel.PayTierRules, Is.Not.Null);
        Assert.That(dayRuleModel.PayTierRules.Count, Is.EqualTo(2));
        
        var tierRule1Model = dayRuleModel.PayTierRules.First(ptr => ptr.Order == 1);
        Assert.That(tierRule1Model.UpToSeconds, Is.EqualTo(28800));
        Assert.That(tierRule1Model.PayCode, Is.EqualTo("REG"));
        
        var tierRule2Model = dayRuleModel.PayTierRules.First(ptr => ptr.Order == 2);
        Assert.That(tierRule2Model.UpToSeconds, Is.Null);
        Assert.That(tierRule2Model.PayCode, Is.EqualTo("OT"));
    }

    #endregion
}
