using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microting.eForm.Dto;
using Microting.eFormApi.BasePn.Abstractions;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using NSubstitute;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Models.RegistrationDevice;
using TimePlanning.Pn.Services.TimePlanningLocalizationService;
using TimePlanning.Pn.Services.TimePlanningRegistrationDeviceService;

namespace TimePlanning.Pn.Test;

/// <summary>
/// Activate is anonymous, so a request without an OTP must never pair a
/// device. Activation clears OtpCode and OtpEnabled, so an empty OtCode must
/// never be allowed to match (OtpCode IS NULL) an already-paired tablet.
/// </summary>
[TestFixture]
public class RegistrationDeviceActivateTests : TestBaseSetup
{
    private TimePlanningRegistrationDeviceService _service = null!;
    private int _customerNo;

    [SetUp]
    public async Task SetUp()
    {
        await base.Setup();

        var core = await GetCore();
        _customerNo = int.Parse(await core.GetSdkSetting(Settings.customerNo));
        var coreService = Substitute.For<IEFormCoreService>();
        coreService.GetCore().Returns(core);

        _service = new TimePlanningRegistrationDeviceService(
            Substitute.For<ILogger<TimePlanningRegistrationDeviceService>>(),
            TimePlanningPnDbContext!,
            Substitute.For<IUserService>(),
            coreService,
            Substitute.For<ITimePlanningLocalizationService>());
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("  ")]
    public async Task Activate_WithoutOtp_DoesNotPairAnAlreadyPairedDevice(string? otCode)
    {
        var device = new RegistrationDevice { Name = "Tablet", Token = "paired" };
        await device.Create(TimePlanningPnDbContext!);

        var result = await _service.Activate(new TimePlanningRegistrationDeviceActivateModel
        {
            CustomerNo = _customerNo,
            OtCode = otCode
        });

        Assert.That(result.Success, Is.False);
        var stored = await TimePlanningPnDbContext!.RegistrationDevices.AsNoTracking()
            .SingleAsync(x => x.Id == device.Id);
        Assert.That(stored.Token, Is.EqualTo("paired"));
    }

    [Test]
    public async Task Activate_WithValidOtp_ReturnsNewToken()
    {
        var device = new RegistrationDevice { Name = "Tablet" };
        await device.Create(TimePlanningPnDbContext!);
        await device.GenerateOtp(TimePlanningPnDbContext!);

        var result = await _service.Activate(new TimePlanningRegistrationDeviceActivateModel
        {
            CustomerNo = _customerNo,
            OtCode = device.OtpCode
        });

        Assert.That(result.Success, Is.True);
        var issued = ((OperationDataResult<TimePlanningRegistrationDeviceAuthModel>)result).Model.Token;
        var stored = await TimePlanningPnDbContext!.RegistrationDevices.AsNoTracking()
            .SingleAsync(x => x.Id == device.Id);
        Assert.Multiple(() =>
        {
            Assert.That(issued, Is.Not.Null.And.Length.EqualTo(32));
            Assert.That(stored.OtpCode, Is.Null);
            Assert.That(stored.OtpEnabled, Is.False);
        });
    }
}
