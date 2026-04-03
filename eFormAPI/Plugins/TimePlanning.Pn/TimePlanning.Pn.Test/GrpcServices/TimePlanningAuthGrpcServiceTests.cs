using System;
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

public class FakeAuthResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public FakeAuthResultModel Model { get; set; }
}

public class FakeAuthResultModel
{
    public string AccessToken { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
}

[TestFixture]
public class TimePlanningAuthGrpcServiceTests
{
    private ITimePlanningRegistrationDeviceService _deviceService;
    private IServiceProvider _serviceProvider;

    [SetUp]
    public void SetUp()
    {
        _deviceService = Substitute.For<ITimePlanningRegistrationDeviceService>();
        _serviceProvider = Substitute.For<IServiceProvider>();
    }

    private TimePlanningAuthGrpcService CreateService(object authService = null)
    {
        if (authService != null)
            return new TestableAuthGrpcService(_deviceService, _serviceProvider, authService);
        return new TimePlanningAuthGrpcService(_deviceService, _serviceProvider);
    }

    [Test]
    public async Task ActivateDevice_Success_ReturnsToken()
    {
        var service = CreateService();
        var authModel = new TimePlanningRegistrationDeviceAuthModel { Token = "test-token-123" };
        _deviceService.Activate(Arg.Any<TimePlanningRegistrationDeviceActivateModel>())
            .Returns(new OperationDataResult<TimePlanningRegistrationDeviceAuthModel>(
                true, "Activated", authModel));

        var request = new ActivateDeviceRequest
        {
            CustomerNo = "42",
            OtCode = "123456"
        };

        var response = await service.ActivateDevice(
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
        var service = CreateService();
        _deviceService.Activate(Arg.Any<TimePlanningRegistrationDeviceActivateModel>())
            .Returns(new OperationResult(false, "CustomerNoMismatch"));

        var request = new ActivateDeviceRequest
        {
            CustomerNo = "99",
            OtCode = "000000"
        };

        var response = await service.ActivateDevice(
            request, TestServerCallContextFactory.Create());

        Assert.That(response.Success, Is.False);
        Assert.That(response.Message, Is.EqualTo("CustomerNoMismatch"));
        Assert.That(response.Model, Is.Null);
    }

    [Test]
    public async Task ActivateDevice_DeviceNotFound_ReturnsError()
    {
        var service = CreateService();
        _deviceService.Activate(Arg.Any<TimePlanningRegistrationDeviceActivateModel>())
            .Returns(new OperationResult(false, "RegistrationDeviceNotFound"));

        var request = new ActivateDeviceRequest
        {
            CustomerNo = "42",
            OtCode = "999999"
        };

        var response = await service.ActivateDevice(
            request, TestServerCallContextFactory.Create());

        Assert.That(response.Success, Is.False);
        Assert.That(response.Message, Is.EqualTo("RegistrationDeviceNotFound"));
    }

    [Test]
    public async Task ActivateDevice_InvalidCustomerNo_DefaultsToZero()
    {
        var service = CreateService();
        _deviceService.Activate(Arg.Any<TimePlanningRegistrationDeviceActivateModel>())
            .Returns(new OperationResult(false, "NotFound"));

        var request = new ActivateDeviceRequest
        {
            CustomerNo = "not-a-number",
            OtCode = "123456"
        };

        await service.ActivateDevice(request, TestServerCallContextFactory.Create());

        await _deviceService.Received(1).Activate(
            Arg.Is<TimePlanningRegistrationDeviceActivateModel>(m => m.CustomerNo == 0));
    }

    [Test]
    public async Task AuthenticateUser_AuthServiceNotAvailable_ReturnsError()
    {
        // No auth service injected — ResolveAuthService returns null via Type.GetType failing
        var service = CreateService();

        var request = new AuthenticateUserRequest
        {
            Username = "user@test.com",
            Password = "password123"
        };

        var response = await service.AuthenticateUser(
            request, TestServerCallContextFactory.Create());

        Assert.That(response.Success, Is.False);
        Assert.That(response.Message, Is.EqualTo("Auth service not available"));
    }

    [Test]
    public async Task AuthenticateUser_Success_ReturnsTokenAndUser()
    {
        var fakeAuthService = new FakeAuthService(new FakeAuthResult
        {
            Success = true, Message = "OK", Model = new FakeAuthResultModel
            {
                AccessToken = "jwt-token-abc",
                FirstName = "John",
                LastName = "Doe"
            }
        });

        var service = CreateService(fakeAuthService);

        var request = new AuthenticateUserRequest
        {
            Username = "user@test.com",
            Password = "password123"
        };

        var response = await service.AuthenticateUser(
            request, TestServerCallContextFactory.Create());

        Assert.That(response.Success, Is.True);
        Assert.That(response.Model, Is.Not.Null);
        Assert.That(response.Model.AccessToken, Is.EqualTo("jwt-token-abc"));
        Assert.That(response.Model.FirstName, Is.EqualTo("John"));
        Assert.That(response.Model.LastName, Is.EqualTo("Doe"));
    }

    [Test]
    public async Task AuthenticateUser_Failure_ReturnsError()
    {
        var fakeAuthService = new FakeAuthService(
            new OperationResult(false, "Invalid credentials"));

        var service = CreateService(fakeAuthService);

        var request = new AuthenticateUserRequest
        {
            Username = "user@test.com",
            Password = "wrong"
        };

        var response = await service.AuthenticateUser(
            request, TestServerCallContextFactory.Create());

        Assert.That(response.Success, Is.False);
        Assert.That(response.Message, Is.EqualTo("Invalid credentials"));
        Assert.That(response.Model, Is.Null);
    }

    [Test]
    public async Task RefreshToken_AuthServiceNotAvailable_ReturnsError()
    {
        var service = CreateService();

        var response = await service.RefreshToken(
            new RefreshTokenRequest(), TestServerCallContextFactory.Create());

        Assert.That(response.Success, Is.False);
        Assert.That(response.Message, Is.EqualTo("Auth service not available"));
    }

    [Test]
    public async Task RefreshToken_Success_ReturnsNewToken()
    {
        var fakeAuthService = new FakeAuthService(
            refreshResult: new FakeAuthResult
            {
                Success = true, Message = "OK", Model = new FakeAuthResultModel
                {
                    AccessToken = "refreshed-token-xyz",
                    FirstName = "Jane",
                    LastName = "Smith"
                }
            });

        var service = CreateService(fakeAuthService);

        var response = await service.RefreshToken(
            new RefreshTokenRequest(), TestServerCallContextFactory.Create());

        Assert.That(response.Success, Is.True);
        Assert.That(response.Model, Is.Not.Null);
        Assert.That(response.Model.AccessToken, Is.EqualTo("refreshed-token-xyz"));
        Assert.That(response.Model.FirstName, Is.EqualTo("Jane"));
        Assert.That(response.Model.LastName, Is.EqualTo("Smith"));
    }

    [Test]
    public async Task RefreshToken_Failure_ReturnsError()
    {
        var fakeAuthService = new FakeAuthService(
            refreshResult: new OperationResult(false, "Token expired"));

        var service = CreateService(fakeAuthService);

        var response = await service.RefreshToken(
            new RefreshTokenRequest(), TestServerCallContextFactory.Create());

        Assert.That(response.Success, Is.False);
        Assert.That(response.Message, Is.EqualTo("Token expired"));
        Assert.That(response.Model, Is.Null);
    }

    private class FakeAuthService
    {
        private readonly object _authenticateResult;
        private readonly object _refreshResult;

        public FakeAuthService(
            object authenticateResult = null,
            object refreshResult = null)
        {
            _authenticateResult = authenticateResult ?? new OperationResult(false, "Not configured");
            _refreshResult = refreshResult ?? new OperationResult(false, "Not configured");
        }

        public Task<object> AuthenticateUser(object model)
        {
            return Task.FromResult(_authenticateResult);
        }

        public Task<object> RefreshToken()
        {
            return Task.FromResult(_refreshResult);
        }
    }

    private class TestableAuthGrpcService : TimePlanningAuthGrpcService
    {
        private readonly object _authService;

        public TestableAuthGrpcService(
            ITimePlanningRegistrationDeviceService deviceService,
            IServiceProvider serviceProvider,
            object authService)
            : base(deviceService, serviceProvider)
        {
            _authService = authService;
        }

        protected override object ResolveAuthService()
        {
            return _authService;
        }
    }
}
