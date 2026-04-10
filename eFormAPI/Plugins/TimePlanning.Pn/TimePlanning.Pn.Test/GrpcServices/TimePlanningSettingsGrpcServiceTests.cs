using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;
using NSubstitute;
using NUnit.Framework;
using TimePlanning.Pn.Grpc;
using TimePlanning.Pn.Services.GrpcServices;
using TimePlanning.Pn.Services.TimePlanningSettingService;
using TimePlanning.Pn.Test.Helpers;

namespace TimePlanning.Pn.Test.GrpcServices;

[TestFixture]
public class TimePlanningSettingsGrpcServiceTests
{
    private ISettingService _settingService;
    private TimePlanningSettingsGrpcService _grpcService;

    [SetUp]
    public void SetUp()
    {
        _settingService = Substitute.For<ISettingService>();
        _grpcService = new TimePlanningSettingsGrpcService(_settingService);
    }

    [Test]
    public async Task GetRegistrationSitesByCurrentUser_Success_MapsSitesToGrpcResponse()
    {
        var sites = new List<Infrastructure.Models.Settings.Site>
        {
            new()
            {
                SiteId = 1,
                SiteName = "Site Alpha",
                FirstName = "John",
                LastName = "Doe",
                Email = "john@example.com",
                PinCode = "1234",
                DefaultLanguage = "en",
                CustomerNo = 42,
                OtpCode = 100,
                UnitId = 10,
                WorkerUid = 200,
                HoursStarted = true,
                PauseStarted = false,
                AutoBreakCalculationActive = true,
                ThirdShiftActive = true,
                FourthShiftActive = false,
                FifthShiftActive = true,
                SnapshotEnabled = false,
                Resigned = false,
                ResignedAtDate = DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc),
                AvatarUrl = "https://example.com/avatar1.png",
                PhoneNumber = "+4512345678",
            },
            new()
            {
                SiteId = 2,
                SiteName = "Site Beta",
                FirstName = "Jane",
                LastName = "Smith",
                Email = "jane@example.com",
                PinCode = "5678",
                DefaultLanguage = "da",
                CustomerNo = 99,
                OtpCode = 200,
                UnitId = 20,
                WorkerUid = 300,
                HoursStarted = false,
                PauseStarted = true,
                AutoBreakCalculationActive = false,
                ThirdShiftActive = false,
                FourthShiftActive = true,
                FifthShiftActive = false,
                SnapshotEnabled = true,
                Resigned = true,
                ResignedAtDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                AvatarUrl = "https://example.com/avatar2.png",
                PhoneNumber = "+4587654321",
            },
        };

        _settingService.GetAvailableSitesByCurrentUser()
            .Returns(new OperationDataResult<List<Infrastructure.Models.Settings.Site>>(
                true, "OK", sites));

        var request = new GetRegistrationSitesByCurrentUserRequest();

        var response = await _grpcService.GetRegistrationSitesByCurrentUser(
            request, TestServerCallContextFactory.Create());

        Assert.That(response.Success, Is.True);
        Assert.That(response.Message, Is.EqualTo("OK"));
        Assert.That(response.Model, Has.Count.EqualTo(2));

        var site1 = response.Model[0];
        Assert.That(site1.SiteId, Is.EqualTo(1));
        Assert.That(site1.SiteName, Is.EqualTo("Site Alpha"));
        Assert.That(site1.FirstName, Is.EqualTo("John"));
        Assert.That(site1.LastName, Is.EqualTo("Doe"));
        Assert.That(site1.Email, Is.EqualTo("john@example.com"));
        Assert.That(site1.PinCode, Is.EqualTo("1234"));
        Assert.That(site1.DefaultLanguage, Is.EqualTo("en"));
        Assert.That(site1.CustomerNo, Is.EqualTo(42));
        Assert.That(site1.OtpCode, Is.EqualTo(100));
        Assert.That(site1.UnitId, Is.EqualTo(10));
        Assert.That(site1.WorkerUid, Is.EqualTo(200));
        Assert.That(site1.HoursStarted, Is.True);
        Assert.That(site1.PauseStarted, Is.False);
        Assert.That(site1.AutoBreakCalculationActive, Is.True);
        Assert.That(site1.ThirdShiftActive, Is.True);
        Assert.That(site1.FourthShiftActive, Is.False);
        Assert.That(site1.FifthShiftActive, Is.True);
        Assert.That(site1.SnapshotEnabled, Is.False);
        Assert.That(site1.Resigned, Is.False);
        Assert.That(site1.AvatarUrl, Is.EqualTo("https://example.com/avatar1.png"));
        Assert.That(site1.PhoneNumber, Is.EqualTo("+4512345678"));

        var site2 = response.Model[1];
        Assert.That(site2.SiteId, Is.EqualTo(2));
        Assert.That(site2.SiteName, Is.EqualTo("Site Beta"));
        Assert.That(site2.FirstName, Is.EqualTo("Jane"));
        Assert.That(site2.LastName, Is.EqualTo("Smith"));
        Assert.That(site2.Email, Is.EqualTo("jane@example.com"));
        Assert.That(site2.PinCode, Is.EqualTo("5678"));
        Assert.That(site2.DefaultLanguage, Is.EqualTo("da"));
        Assert.That(site2.CustomerNo, Is.EqualTo(99));
        Assert.That(site2.OtpCode, Is.EqualTo(200));
        Assert.That(site2.UnitId, Is.EqualTo(20));
        Assert.That(site2.WorkerUid, Is.EqualTo(300));
        Assert.That(site2.HoursStarted, Is.False);
        Assert.That(site2.PauseStarted, Is.True);
        Assert.That(site2.AutoBreakCalculationActive, Is.False);
        Assert.That(site2.ThirdShiftActive, Is.False);
        Assert.That(site2.FourthShiftActive, Is.True);
        Assert.That(site2.FifthShiftActive, Is.False);
        Assert.That(site2.SnapshotEnabled, Is.True);
        Assert.That(site2.Resigned, Is.True);
        Assert.That(site2.AvatarUrl, Is.EqualTo("https://example.com/avatar2.png"));
        Assert.That(site2.PhoneNumber, Is.EqualTo("+4587654321"));
    }

    [Test]
    public async Task GetRegistrationSitesByCurrentUser_Failure_ReturnsErrorMessage()
    {
        _settingService.GetAvailableSitesByCurrentUser()
            .Returns(new OperationDataResult<List<Infrastructure.Models.Settings.Site>>(
                false, "Access denied"));

        var request = new GetRegistrationSitesByCurrentUserRequest();

        var response = await _grpcService.GetRegistrationSitesByCurrentUser(
            request, TestServerCallContextFactory.Create());

        Assert.That(response.Success, Is.False);
        Assert.That(response.Message, Is.EqualTo("Access denied"));
        Assert.That(response.Model, Has.Count.EqualTo(0));
    }

    [Test]
    public async Task GetRegistrationSitesByCurrentUser_EmptyList_ReturnsSuccessWithNoSites()
    {
        _settingService.GetAvailableSitesByCurrentUser()
            .Returns(new OperationDataResult<List<Infrastructure.Models.Settings.Site>>(
                true, "OK", new List<Infrastructure.Models.Settings.Site>()));

        var request = new GetRegistrationSitesByCurrentUserRequest();

        var response = await _grpcService.GetRegistrationSitesByCurrentUser(
            request, TestServerCallContextFactory.Create());

        Assert.That(response.Success, Is.True);
        Assert.That(response.Model, Has.Count.EqualTo(0));
    }
}
