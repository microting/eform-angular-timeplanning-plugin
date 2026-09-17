using System.Collections.Generic;
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
        // RoleManager is only reached once a login succeeds, which no test here does, so
        // it stays null. UserManager substitutes fine through its virtual members.
        _userService = Substitute.For<IUserService>();
        _userManager = SubstituteUserManager();
        _roleManager = null;
        _tokenOptions = Substitute.For<IOptions<EformTokenOptions>>();
        _grpcService = new TimePlanningAuthGrpcService(
            _deviceService, _userService, _userManager, _roleManager, _tokenOptions);
    }

    [TearDown]
    public void TearDown()
    {
        _userManager?.Dispose();
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

    // This service is a second, parallel login implementation that mints the same JWT as
    // the JSON path, so a disabled account has to be refused here too - otherwise a
    // resigned employee's flutter-time app keeps working. Every credential failure answers
    // with one message, for the same reason the JSON path does.
    // Spelled out rather than referenced from the production constant: asserting against
    // the constant would pass however the message changed.
    private const string ExpectedMessage = "You have entered an invalid username or password";

    private static UserManager<EformUser> SubstituteUserManager() =>
        Substitute.For<UserManager<EformUser>>(
            Substitute.For<IUserStore<EformUser>>(), null, null, null, null, null, null, null, null);

    private TimePlanningAuthGrpcService ServiceWith(UserManager<EformUser> userManager) =>
        new(_deviceService, _userService, userManager, _roleManager, _tokenOptions);

    private static EformUser DisabledUser() => new()
    {
        Id = 42,
        UserName = "someone@example.com",
        Email = "someone@example.com",
        EmailConfirmed = true,
        IsActive = false
    };

    [Test]
    public async Task AuthenticateUser_DisabledAccount_IsRefused()
    {
        var userManager = SubstituteUserManager();
        userManager.FindByNameAsync(Arg.Any<string>()).Returns(DisabledUser());
        userManager.CheckPasswordAsync(Arg.Any<EformUser>(), Arg.Any<string>()).Returns(true);
        // A role, so the call would otherwise get past every other check and mint a token -
        // without this the method returns "Role ... not found" and the refusal proves nothing.
        userManager.GetRolesAsync(Arg.Any<EformUser>()).Returns(new List<string> { "admin" });

        var response = await ServiceWith(userManager).AuthenticateUser(
            new AuthenticateUserRequest { Username = "someone@example.com", Password = "right" }, TestServerCallContextFactory.Create());

        Assert.That(response.Success, Is.False, "a disabled account must not be able to log in");
        Assert.That(response.Message, Is.EqualTo(ExpectedMessage));
        Assert.That(response.Model, Is.Null, "no token may be issued for a disabled account");
    }

    [Test]
    public async Task AuthenticateUser_DisabledAccount_ReturnsSameMessageAsUnknownAccount()
    {
        var disabledManager = SubstituteUserManager();
        disabledManager.FindByNameAsync(Arg.Any<string>()).Returns(DisabledUser());
        disabledManager.CheckPasswordAsync(Arg.Any<EformUser>(), Arg.Any<string>()).Returns(true);
        var disabled = await ServiceWith(disabledManager).AuthenticateUser(
            new AuthenticateUserRequest { Username = "someone@example.com", Password = "right" }, TestServerCallContextFactory.Create());

        var unknownManager = SubstituteUserManager();
        unknownManager.FindByNameAsync(Arg.Any<string>()).Returns((EformUser)null!);
        unknownManager.FindByEmailAsync(Arg.Any<string>()).Returns((EformUser)null!);
        var unknown = await ServiceWith(unknownManager).AuthenticateUser(
            new AuthenticateUserRequest { Username = "someone@example.com", Password = "right" }, TestServerCallContextFactory.Create());

        Assert.That(disabled.Message, Is.EqualTo(unknown.Message),
            "a disabled account must not be distinguishable from one that does not exist");
        Assert.That(unknown.Message, Does.Not.Contain("someone@example.com"),
            "the response must not repeat what was typed");
    }

    [Test]
    public async Task RefreshToken_DisabledAccount_IsRefused()
    {
        _userService.UserId.Returns(42);
        _userService.GetByIdAsync(Arg.Any<int>()).Returns(DisabledUser());

        var response = await _grpcService.RefreshToken(new RefreshTokenRequest(), TestServerCallContextFactory.Create());

        Assert.That(response.Success, Is.False,
            "a disabled account must not be able to roll its session forward");
        Assert.That(response.Message, Is.EqualTo(ExpectedMessage));
        Assert.That(response.Model, Is.Null, "no fresh token may be minted for a disabled account");
    }
}
