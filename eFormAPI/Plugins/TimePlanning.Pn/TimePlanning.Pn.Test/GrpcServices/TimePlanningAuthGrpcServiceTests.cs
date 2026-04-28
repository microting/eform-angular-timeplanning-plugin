using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microting.eFormApi.BasePn.Abstractions;
using Microting.eFormApi.BasePn.Infrastructure.Database.Entities;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;
using Microting.eFormApi.BasePn.Infrastructure.Models.Application;
using NSubstitute;
using NUnit.Framework;
using TimePlanning.Pn.Grpc;
using TimePlanning.Pn.Infrastructure.Models.RegistrationDevice;
using TimePlanning.Pn.Services.GrpcServices;
using TimePlanning.Pn.Services.TimePlanningRegistrationDeviceService;
using TimePlanning.Pn.Test.Helpers;
using OperationResult = Microting.eFormApi.BasePn.Infrastructure.Models.API.OperationResult;

namespace TimePlanning.Pn.Test.GrpcServices;

[TestFixture]
public class TimePlanningAuthGrpcServiceTests
{
    private ITimePlanningRegistrationDeviceService _deviceService;
    private IUserService _userService;
    private UserManager<EformUser> _userManager;
    private RoleManager<EformRole> _roleManager;
    private IOptions<EformTokenOptions> _tokenOptions;
    private TimePlanningAuthGrpcService _grpcService;

    [SetUp]
    public void SetUp()
    {
        _deviceService = Substitute.For<ITimePlanningRegistrationDeviceService>();
        // RefreshToken-related deps are not exercised by ActivateDevice tests;
        // null-pass these. UserManager/RoleManager have no usable interface to
        // substitute against, so we pass null — any test that exercises
        // RefreshToken would need to construct real instances.
        _userService = Substitute.For<IUserService>();
        _userManager = null;
        _roleManager = null;
        _tokenOptions = Substitute.For<IOptions<EformTokenOptions>>();
        _grpcService = new TimePlanningAuthGrpcService(
            _deviceService, _userService, _userManager, _roleManager, _tokenOptions);
    }

    [Test]
    public async Task ActivateDevice_Success_ReturnsToken()
    {
        var authModel = new TimePlanningRegistrationDeviceAuthModel { Token = "test-token-123" };
        _deviceService.Activate(Arg.Any<TimePlanningRegistrationDeviceActivateModel>())
            .Returns(new OperationDataResult<TimePlanningRegistrationDeviceAuthModel>(
                true, "Activated", authModel));

        var request = new ActivateDeviceRequest
        {
            CustomerNo = "42",
            OtCode = "123456"
        };

        var response = await _grpcService.ActivateDevice(
            request, TestServerCallContextFactory.Create());

        Assert.That(response.Success, Is.True);
        Assert.That(response.Model, Is.Not.Null);
        Assert.That(response.Model.Token, Is.EqualTo("test-token-123"));

        await _deviceService.Received(1).Activate(
            Arg.Is<TimePlanningRegistrationDeviceActivateModel>(m =>
                m.CustomerNo == 42 && m.OtCode == "123456"));
    }

    [Test]
    public async Task ActivateDevice_Failure_ReturnsError()
    {
        _deviceService.Activate(Arg.Any<TimePlanningRegistrationDeviceActivateModel>())
            .Returns(new OperationResult(false, "CustomerNoMismatch"));

        var request = new ActivateDeviceRequest
        {
            CustomerNo = "99",
            OtCode = "000000"
        };

        var response = await _grpcService.ActivateDevice(
            request, TestServerCallContextFactory.Create());

        Assert.That(response.Success, Is.False);
        Assert.That(response.Message, Is.EqualTo("CustomerNoMismatch"));
        Assert.That(response.Model, Is.Null);
    }

    [Test]
    public async Task ActivateDevice_DeviceNotFound_ReturnsError()
    {
        _deviceService.Activate(Arg.Any<TimePlanningRegistrationDeviceActivateModel>())
            .Returns(new OperationResult(false, "RegistrationDeviceNotFound"));

        var request = new ActivateDeviceRequest
        {
            CustomerNo = "42",
            OtCode = "999999"
        };

        var response = await _grpcService.ActivateDevice(
            request, TestServerCallContextFactory.Create());

        Assert.That(response.Success, Is.False);
        Assert.That(response.Message, Is.EqualTo("RegistrationDeviceNotFound"));
    }

    [Test]
    public async Task ActivateDevice_InvalidCustomerNo_DefaultsToZero()
    {
        _deviceService.Activate(Arg.Any<TimePlanningRegistrationDeviceActivateModel>())
            .Returns(new OperationResult(false, "NotFound"));

        var request = new ActivateDeviceRequest
        {
            CustomerNo = "not-a-number",
            OtCode = "123456"
        };

        await _grpcService.ActivateDevice(request, TestServerCallContextFactory.Create());

        await _deviceService.Received(1).Activate(
            Arg.Is<TimePlanningRegistrationDeviceActivateModel>(m => m.CustomerNo == 0));
    }
}
