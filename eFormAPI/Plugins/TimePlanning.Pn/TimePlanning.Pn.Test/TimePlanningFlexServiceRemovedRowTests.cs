using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microting.eForm.Infrastructure.Constants;
using Microting.eFormApi.BasePn.Abstractions;
using Microting.eFormApi.BasePn.Infrastructure.Helpers.PluginDbOptions;
using Microting.eFormApi.BasePn.Infrastructure.Models.Common;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using NSubstitute;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Models.Flex.Update;
using TimePlanning.Pn.Infrastructure.Models.Settings;
using TimePlanning.Pn.Services.TimePlanningFlexService;
using TimePlanning.Pn.Services.TimePlanningLocalizationService;

namespace TimePlanning.Pn.Test;

/// <summary>
/// Regression coverage for TimePlanningFlexService.UpdateCreate writing flex
/// values (SumFlexEnd / PaiedOutFlex / CommentOffice) into a soft-removed
/// PlanRegistration. The match query had no WorkflowState filter, so a Removed
/// row for the same date/site was resurrected as the flex target.
///
/// Structured to FAIL pre-fix (the Removed row is updated in place) and PASS
/// post-fix (the Removed row is skipped and a fresh active row is created).
/// </summary>
[TestFixture]
public class TimePlanningFlexServiceRemovedRowTests : TestBaseSetup
{
    private ITimePlanningFlexService _service;

    [SetUp]
    public async Task SetUpTest()
    {
        await base.Setup();

        var userService = Substitute.For<IUserService>();
        userService.UserId.Returns(1);

        var localizationService = Substitute.For<ITimePlanningLocalizationService>();
        localizationService.GetString(Arg.Any<string>()).Returns(x => x[0]?.ToString());

        var coreService = Substitute.For<IEFormCoreService>();
        var core = await GetCore();
        coreService.GetCore().Returns(core);

        var options = Substitute.For<IPluginDbOptions<TimePlanningBaseSettings>>();
        options.Value.Returns(new TimePlanningBaseSettings());

        _service = new TimePlanningFlexService(
            Substitute.For<ILogger<TimePlanningFlexService>>(),
            TimePlanningPnDbContext,
            userService,
            localizationService,
            coreService,
            options);
    }

    [Test]
    public async Task UpdateCreate_DoesNotWriteIntoRemovedRow_CreatesFreshActiveRow()
    {
        const int sdkSitId = 555;
        var date = DateTime.UtcNow.Date;

        // A soft-removed flex row already exists for this date/site.
        var removed = new PlanRegistration
        {
            SdkSitId = sdkSitId,
            Date = date,
            CommentOffice = "ORIGINAL",
            PaiedOutFlex = 5,
            SumFlexEnd = 100,
            StatusCaseId = 0,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await removed.Create(TimePlanningPnDbContext);
        await removed.Delete(TimePlanningPnDbContext); // WorkflowState -> Removed
        var removedId = removed.Id;

        var model = new List<TimePlanningFlexUpdateModel>
        {
            new TimePlanningFlexUpdateModel
            {
                Date = date,
                Worker = new CommonDictionaryModel { Id = sdkSitId },
                CommentOffice = "NEW",
                CommentOfficeAll = "NEW-ALL",
                PaidOutFlex = 9,
                SumFlexStart = 50
            }
        };

        var result = await _service.UpdateCreate(model);
        Assert.That(result.Success, Is.True, result.Message);

        // The removed row must be untouched.
        var reloadedRemoved = await TimePlanningPnDbContext.PlanRegistrations
            .AsNoTracking().FirstAsync(x => x.Id == removedId);
        Assert.That(reloadedRemoved.WorkflowState, Is.EqualTo(Constants.WorkflowStates.Removed));
        Assert.That(reloadedRemoved.CommentOffice, Is.EqualTo("ORIGINAL"),
            "Flex UpdateCreate must not overwrite a removed row's comment");
        Assert.That(reloadedRemoved.PaiedOutFlex, Is.EqualTo(5),
            "Flex UpdateCreate must not overwrite a removed row's paid-out flex");

        // A brand-new active row must carry the update.
        var activeRows = await TimePlanningPnDbContext.PlanRegistrations
            .AsNoTracking()
            .Where(x => x.SdkSitId == sdkSitId && x.Date == date
                        && x.WorkflowState != Constants.WorkflowStates.Removed)
            .ToListAsync();
        Assert.That(activeRows.Count, Is.EqualTo(1),
            "A fresh active flex row must be created instead of reusing the removed one");
        Assert.That(activeRows[0].CommentOffice, Is.EqualTo("NEW"));
        Assert.That(activeRows[0].PaiedOutFlex, Is.EqualTo(9));
    }
}
