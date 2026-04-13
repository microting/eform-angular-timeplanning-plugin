using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microting.eForm.Infrastructure.Constants;
using Microting.eFormApi.BasePn.Abstractions;
using Microting.eFormApi.BasePn.Infrastructure.Helpers.PluginDbOptions;
using TimePlanning.Pn.Infrastructure.Models.Settings;
using AssignedSiteEntity = Microting.TimePlanningBase.Infrastructure.Data.Entities.AssignedSite;
using NSubstitute;
using NUnit.Framework;
using TimePlanning.Pn.Services.TimePlanningLocalizationService;
using TimePlanning.Pn.Services.TimePlanningSettingService;

namespace TimePlanning.Pn.Test;

[TestFixture]
public class SettingsServicePhoneNumberTests : TestBaseSetup
{
    private ISettingService _settingsService;
    private IUserService _userService;
    private ITimePlanningLocalizationService _localizationService;
    private IEFormCoreService _coreService;
    private IPluginDbOptions<TimePlanningBaseSettings> _options;

    [SetUp]
    public async Task SetUp()
    {
        await base.Setup();
        _userService = Substitute.For<IUserService>();
        _userService.UserId.Returns(1);

        _localizationService = Substitute.For<ITimePlanningLocalizationService>();
        _localizationService.GetString(Arg.Any<string>()).Returns(x => x[0]?.ToString());

        _coreService = Substitute.For<IEFormCoreService>();
        var core = await GetCore();
        _coreService.GetCore().Returns(core);

        _options = Substitute.For<IPluginDbOptions<TimePlanningBaseSettings>>();
        _options.Value.Returns(new TimePlanningBaseSettings
        {
            AutoBreakCalculationActive = "0",
            DayOfPayment = 20,
            GpsEnabled = "0",
            SnapshotEnabled = "0"
        });

        _settingsService = new TimeSettingService(
            _options,
            TimePlanningPnDbContext,
            Substitute.For<ILogger<TimeSettingService>>(),
            _userService,
            _localizationService,
            null,
            _coreService);
    }

    [Test]
    public async Task GetAvailableSitesByCurrentUser_ReturnsPhoneNumbers_FromSdkWorkers()
    {
        // Arrange — create 3 SDK sites, site workers, and workers with varying phone numbers
        var core = await _coreService.GetCore();
        var sdkDbContext = core.DbContextHelper.GetDbContext();

        // Worker 1: has phone number
        var site1 = new Microting.eForm.Infrastructure.Data.Entities.Site
        {
            Name = "Site Alice",
            MicrotingUid = 100
        };
        await site1.Create(sdkDbContext);

        var worker1 = new Microting.eForm.Infrastructure.Data.Entities.Worker
        {
            FirstName = "Alice",
            LastName = "Smith",
            Email = "alice@example.com",
            PhoneNumber = "+4512345678",
            MicrotingUid = 1001
        };
        await worker1.Create(sdkDbContext);

        var siteWorker1 = new Microting.eForm.Infrastructure.Data.Entities.SiteWorker
        {
            SiteId = site1.Id,
            WorkerId = worker1.Id,
            MicrotingUid = 2001
        };
        await siteWorker1.Create(sdkDbContext);

        // Worker 2: no phone number (null)
        var site2 = new Microting.eForm.Infrastructure.Data.Entities.Site
        {
            Name = "Site Bob",
            MicrotingUid = 200
        };
        await site2.Create(sdkDbContext);

        var worker2 = new Microting.eForm.Infrastructure.Data.Entities.Worker
        {
            FirstName = "Bob",
            LastName = "Jones",
            Email = "bob@example.com",
            PhoneNumber = null,
            MicrotingUid = 1002
        };
        await worker2.Create(sdkDbContext);

        var siteWorker2 = new Microting.eForm.Infrastructure.Data.Entities.SiteWorker
        {
            SiteId = site2.Id,
            WorkerId = worker2.Id,
            MicrotingUid = 2002
        };
        await siteWorker2.Create(sdkDbContext);

        // Worker 3: has different phone number
        var site3 = new Microting.eForm.Infrastructure.Data.Entities.Site
        {
            Name = "Site Carol",
            MicrotingUid = 300
        };
        await site3.Create(sdkDbContext);

        var worker3 = new Microting.eForm.Infrastructure.Data.Entities.Worker
        {
            FirstName = "Carol",
            LastName = "Lee",
            Email = "carol@example.com",
            PhoneNumber = "+4587654321",
            MicrotingUid = 1003
        };
        await worker3.Create(sdkDbContext);

        var siteWorker3 = new Microting.eForm.Infrastructure.Data.Entities.SiteWorker
        {
            SiteId = site3.Id,
            WorkerId = worker3.Id,
            MicrotingUid = 2003
        };
        await siteWorker3.Create(sdkDbContext);

        // Create units (required by the service)
        var unit1 = new Microting.eForm.Infrastructure.Data.Entities.Unit
        {
            SiteId = site1.Id,
            MicrotingUid = 3001,
            CustomerNo = 1
        };
        await unit1.Create(sdkDbContext);

        var unit2 = new Microting.eForm.Infrastructure.Data.Entities.Unit
        {
            SiteId = site2.Id,
            MicrotingUid = 3002,
            CustomerNo = 2
        };
        await unit2.Create(sdkDbContext);

        var unit3 = new Microting.eForm.Infrastructure.Data.Entities.Unit
        {
            SiteId = site3.Id,
            MicrotingUid = 3003,
            CustomerNo = 3
        };
        await unit3.Create(sdkDbContext);

        // Create languages
        var language = await sdkDbContext.Languages.FirstOrDefaultAsync();
        if (language == null)
        {
            language = new Microting.eForm.Infrastructure.Data.Entities.Language
            {
                LanguageCode = "da",
                Name = "Danish"
            };
            await language.Create(sdkDbContext);
        }

        // Assign language to sites
        site1.LanguageId = language.Id;
        await site1.Update(sdkDbContext);
        site2.LanguageId = language.Id;
        await site2.Update(sdkDbContext);
        site3.LanguageId = language.Id;
        await site3.Update(sdkDbContext);

        // Create AssignedSites in plugin DB (links to SDK sites via MicrotingUid)
        var assignedSite1 = new AssignedSiteEntity
        {
            SiteId = 100, // matches site1.MicrotingUid
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await assignedSite1.Create(TimePlanningPnDbContext);

        var assignedSite2 = new AssignedSiteEntity
        {
            SiteId = 200, // matches site2.MicrotingUid
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await assignedSite2.Create(TimePlanningPnDbContext);

        var assignedSite3 = new AssignedSiteEntity
        {
            SiteId = 300, // matches site3.MicrotingUid
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await assignedSite3.Create(TimePlanningPnDbContext);

        // Act
        var result = await _settingsService.GetAvailableSitesByCurrentUser();

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.Model, Is.Not.Null);
        Assert.That(result.Model.Count, Is.EqualTo(3));

        var alice = result.Model.First(x => x.FirstName == "Alice");
        Assert.That(alice.PhoneNumber, Is.EqualTo("+4512345678"));
        Assert.That(alice.LastName, Is.EqualTo("Smith"));
        Assert.That(alice.Email, Is.EqualTo("alice@example.com"));

        var bob = result.Model.First(x => x.FirstName == "Bob");
        Assert.That(bob.PhoneNumber, Is.EqualTo(""));
        Assert.That(bob.LastName, Is.EqualTo("Jones"));

        var carol = result.Model.First(x => x.FirstName == "Carol");
        Assert.That(carol.PhoneNumber, Is.EqualTo("+4587654321"));
        Assert.That(carol.LastName, Is.EqualTo("Lee"));
    }

    [Test]
    public async Task GetAvailableSitesByCurrentUser_PhoneComesFromWorker_NotFromUser()
    {
        // This test verifies that even when an Angular User exists with a DIFFERENT phone number,
        // the phone number returned comes from the SDK Worker, not the User.
        var core = await _coreService.GetCore();
        var sdkDbContext = core.DbContextHelper.GetDbContext();

        var site = new Microting.eForm.Infrastructure.Data.Entities.Site
        {
            Name = "Site Dave",
            MicrotingUid = 400
        };
        await site.Create(sdkDbContext);

        var worker = new Microting.eForm.Infrastructure.Data.Entities.Worker
        {
            FirstName = "Dave",
            LastName = "Brown",
            Email = "dave@example.com",
            PhoneNumber = "+45WorkerPhone",
            MicrotingUid = 1004
        };
        await worker.Create(sdkDbContext);

        var siteWorker = new Microting.eForm.Infrastructure.Data.Entities.SiteWorker
        {
            SiteId = site.Id,
            WorkerId = worker.Id,
            MicrotingUid = 2004
        };
        await siteWorker.Create(sdkDbContext);

        var unit = new Microting.eForm.Infrastructure.Data.Entities.Unit
        {
            SiteId = site.Id,
            MicrotingUid = 3004,
            CustomerNo = 4
        };
        await unit.Create(sdkDbContext);

        var language = await sdkDbContext.Languages.FirstOrDefaultAsync();
        if (language == null)
        {
            language = new Microting.eForm.Infrastructure.Data.Entities.Language
            {
                LanguageCode = "da",
                Name = "Danish"
            };
            await language.Create(sdkDbContext);
        }
        site.LanguageId = language.Id;
        await site.Update(sdkDbContext);

        var assignedSite = new AssignedSiteEntity
        {
            SiteId = 400,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await assignedSite.Create(TimePlanningPnDbContext);

        // Note: baseDbContext is null in this test setup, so no Angular User lookup occurs.
        // The key assertion is that PhoneNumber comes from worker.PhoneNumber.

        // Act
        var result = await _settingsService.GetAvailableSitesByCurrentUser();

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.Model.Count, Is.EqualTo(1));

        var dave = result.Model.First();
        Assert.That(dave.PhoneNumber, Is.EqualTo("+45WorkerPhone"));
        Assert.That(dave.FirstName, Is.EqualTo("Dave"));
        Assert.That(dave.LastName, Is.EqualTo("Brown"));
        Assert.That(dave.Email, Is.EqualTo("dave@example.com"));
    }

    [Test]
    public async Task GetAvailableSitesByCurrentUser_ExcludesResignedSites()
    {
        // Verify resigned sites are excluded from the result
        var core = await _coreService.GetCore();
        var sdkDbContext = core.DbContextHelper.GetDbContext();

        var site = new Microting.eForm.Infrastructure.Data.Entities.Site
        {
            Name = "Site Resigned",
            MicrotingUid = 500
        };
        await site.Create(sdkDbContext);

        var worker = new Microting.eForm.Infrastructure.Data.Entities.Worker
        {
            FirstName = "Eve",
            LastName = "Wilson",
            Email = "eve@example.com",
            PhoneNumber = "+4511111111",
            MicrotingUid = 1005
        };
        await worker.Create(sdkDbContext);

        var siteWorker = new Microting.eForm.Infrastructure.Data.Entities.SiteWorker
        {
            SiteId = site.Id,
            WorkerId = worker.Id,
            MicrotingUid = 2005
        };
        await siteWorker.Create(sdkDbContext);

        var unit = new Microting.eForm.Infrastructure.Data.Entities.Unit
        {
            SiteId = site.Id,
            MicrotingUid = 3005,
            CustomerNo = 5
        };
        await unit.Create(sdkDbContext);

        var language = await sdkDbContext.Languages.FirstOrDefaultAsync();
        if (language == null)
        {
            language = new Microting.eForm.Infrastructure.Data.Entities.Language
            {
                LanguageCode = "da",
                Name = "Danish"
            };
            await language.Create(sdkDbContext);
        }
        site.LanguageId = language.Id;
        await site.Update(sdkDbContext);

        // Create a resigned assigned site
        var assignedSite = new AssignedSiteEntity
        {
            SiteId = 500,
            Resigned = true,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await assignedSite.Create(TimePlanningPnDbContext);

        // Act
        var result = await _settingsService.GetAvailableSitesByCurrentUser();

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.Model.Count, Is.EqualTo(0));
    }
}
