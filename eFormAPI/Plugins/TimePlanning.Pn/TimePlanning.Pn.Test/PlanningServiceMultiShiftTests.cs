using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microting.eForm.Infrastructure.Constants;
using Microting.eFormApi.BasePn.Abstractions;
using Microting.eFormApi.BasePn.Infrastructure.Helpers.PluginDbOptions;
using Microting.eFormApi.BasePn.Infrastructure.Database.Entities;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using AssignedSiteEntity = Microting.TimePlanningBase.Infrastructure.Data.Entities.AssignedSite;
using NSubstitute;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Helpers;
using TimePlanning.Pn.Infrastructure.Models.Planning;
using TimePlanning.Pn.Infrastructure.Models.Settings;
using TimePlanning.Pn.Services.TimePlanningLocalizationService;
using TimePlanning.Pn.Services.TimePlanningPlanningService;

namespace TimePlanning.Pn.Test;

[TestFixture]
public class PlanningServiceMultiShiftTests : TestBaseSetup
{
    private ITimePlanningPlanningService _service;
    private IUserService _userService;
    private ITimePlanningLocalizationService _localizationService;
    private IEFormCoreService _coreService;
    private ITimePlanningDbContextHelper _dbContextHelper;
    private IPluginDbOptions<TimePlanningBaseSettings> _options;

    [SetUp]
    public async Task SetUpTest()
    {
        await base.Setup();

        _userService = Substitute.For<IUserService>();
        _userService.UserId.Returns(1);
        _userService.GetCurrentUserAsync().Returns(new EformUser { Id = 1 });

        _localizationService = Substitute.For<ITimePlanningLocalizationService>();
        _localizationService.GetString(Arg.Any<string>()).Returns(x => x[0]?.ToString());

        _coreService = Substitute.For<IEFormCoreService>();
        var core = await GetCore();
        _coreService.GetCore().Returns(core);

        _dbContextHelper = Substitute.For<ITimePlanningDbContextHelper>();
        _dbContextHelper.GetDbContext().Returns(TimePlanningPnDbContext);

        _options = Substitute.For<IPluginDbOptions<TimePlanningBaseSettings>>();
        _options.Value.Returns(new TimePlanningBaseSettings
        {
            AutoBreakCalculationActive = "0",
            DayOfPayment = 20,
            GpsEnabled = "0",
            SnapshotEnabled = "0"
        });

        _service = new TimePlanningPlanningService(
            Substitute.For<ILogger<TimePlanningPlanningService>>(),
            _options,
            TimePlanningPnDbContext,
            _dbContextHelper,
            _userService,
            _localizationService,
            null,
            _coreService);
    }

    [Test]
    public async Task Update_PersistsAllFiveShifts_RoundTrip()
    {
        // Arrange — seed AssignedSite + PlanRegistration
        var assignedSite = new AssignedSiteEntity
        {
            SiteId = 900,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await assignedSite.Create(TimePlanningPnDbContext);

        var planning = new PlanRegistration
        {
            SdkSitId = 900,
            Date = DateTime.UtcNow.Date,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await planning.Create(TimePlanningPnDbContext);

        // Build model with 5 shifts:
        // Shift 1: 00:00-01:00 (0-60)   break 5
        // Shift 2: 02:00-03:00 (120-180) break 10
        // Shift 3: 04:00-05:00 (240-300) break 15
        // Shift 4: 06:00-07:00 (360-420) break 20
        // Shift 5: 07:00-08:00 (420-480) break 25
        var model = new TimePlanningPlanningPrDayModel
        {
            Id = planning.Id,
            Date = planning.Date,
            CommentOffice = "",
            PlannedStartOfShift1 = 0,   PlannedEndOfShift1 = 60,   PlannedBreakOfShift1 = 5,
            PlannedStartOfShift2 = 120, PlannedEndOfShift2 = 180,  PlannedBreakOfShift2 = 10,
            PlannedStartOfShift3 = 240, PlannedEndOfShift3 = 300,  PlannedBreakOfShift3 = 15,
            PlannedStartOfShift4 = 360, PlannedEndOfShift4 = 420,  PlannedBreakOfShift4 = 20,
            PlannedStartOfShift5 = 420, PlannedEndOfShift5 = 480,  PlannedBreakOfShift5 = 25,
        };

        // Act
        var result = await _service.Update(planning.Id, model);

        // Assert
        Assert.That(result.Success, Is.True, result.Message);

        var reloaded = await TimePlanningPnDbContext.PlanRegistrations
            .AsNoTracking()
            .FirstAsync(x => x.Id == planning.Id);

        Assert.Multiple(() =>
        {
            Assert.That(reloaded.PlannedStartOfShift1, Is.EqualTo(0));
            Assert.That(reloaded.PlannedEndOfShift1,   Is.EqualTo(60));
            Assert.That(reloaded.PlannedBreakOfShift1, Is.EqualTo(5));

            Assert.That(reloaded.PlannedStartOfShift2, Is.EqualTo(120));
            Assert.That(reloaded.PlannedEndOfShift2,   Is.EqualTo(180));
            Assert.That(reloaded.PlannedBreakOfShift2, Is.EqualTo(10));

            Assert.That(reloaded.PlannedStartOfShift3, Is.EqualTo(240));
            Assert.That(reloaded.PlannedEndOfShift3,   Is.EqualTo(300));
            Assert.That(reloaded.PlannedBreakOfShift3, Is.EqualTo(15));

            Assert.That(reloaded.PlannedStartOfShift4, Is.EqualTo(360));
            Assert.That(reloaded.PlannedEndOfShift4,   Is.EqualTo(420));
            Assert.That(reloaded.PlannedBreakOfShift4, Is.EqualTo(20));

            Assert.That(reloaded.PlannedStartOfShift5, Is.EqualTo(420));
            Assert.That(reloaded.PlannedEndOfShift5,   Is.EqualTo(480));
            Assert.That(reloaded.PlannedBreakOfShift5, Is.EqualTo(25));
        });
    }
}
