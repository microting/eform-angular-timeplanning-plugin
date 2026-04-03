using System.Threading.Tasks;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;
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
    private TimePlanningAuthGrpcService _grpcService;

    [SetUp]
    public void SetUp()
    {
        _deviceService = Substitute.For<ITimePlanningRegistrationDeviceService>();
        _grpcService = new TimePlanningAuthGrpcService(_deviceService);
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
}
