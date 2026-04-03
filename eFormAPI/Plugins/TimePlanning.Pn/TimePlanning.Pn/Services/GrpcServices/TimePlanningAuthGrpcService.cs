using System;
using System.Reflection;
using System.Threading.Tasks;
using Grpc.Core;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;
using Microting.eFormApi.BasePn.Infrastructure.Models.Auth;
using TimePlanning.Pn.Grpc;
using TimePlanning.Pn.Infrastructure.Models.RegistrationDevice;
using TimePlanning.Pn.Services.TimePlanningRegistrationDeviceService;

namespace TimePlanning.Pn.Services.GrpcServices;

public class TimePlanningAuthGrpcService : TimePlanningAuthService.TimePlanningAuthServiceBase
{
    private readonly ITimePlanningRegistrationDeviceService _registrationDeviceService;
    private readonly IServiceProvider _serviceProvider;

    public TimePlanningAuthGrpcService(
        ITimePlanningRegistrationDeviceService registrationDeviceService,
        IServiceProvider serviceProvider)
    {
        _registrationDeviceService = registrationDeviceService;
        _serviceProvider = serviceProvider;
    }

    public override async Task<ActivateDeviceResponse> ActivateDevice(
        ActivateDeviceRequest request, ServerCallContext context)
    {
        var model = new TimePlanningRegistrationDeviceActivateModel
        {
            CustomerNo = int.TryParse(request.CustomerNo, out var custNo) ? custNo : 0,
            OtCode = request.OtCode
        };

        var result = await _registrationDeviceService.Activate(model);

        var response = new ActivateDeviceResponse
        {
            Success = result.Success,
            Message = result.Message ?? ""
        };

        if (result.Success && result is OperationDataResult<TimePlanningRegistrationDeviceAuthModel> dataResult
            && dataResult.Model != null)
        {
            response.Model = new ActivateDeviceModel
            {
                Token = dataResult.Model.Token ?? ""
            };
        }

        return response;
    }

    public override async Task<AuthenticateUserResponse> AuthenticateUser(
        AuthenticateUserRequest request, ServerCallContext context)
    {
        try
        {
            var authService = ResolveAuthService();
            if (authService == null)
            {
                return new AuthenticateUserResponse
                {
                    Success = false,
                    Message = "Auth service not available"
                };
            }

            var loginModel = CreateLoginModel(request.Username, request.Password);
            var result = await InvokeAuthenticateUser(authService, loginModel);

            return BuildAuthenticateUserResponse(result);
        }
        catch (Exception ex)
        {
            return new AuthenticateUserResponse
            {
                Success = false,
                Message = ex.Message
            };
        }
    }

    public override async Task<RefreshTokenResponse> RefreshToken(
        RefreshTokenRequest request, ServerCallContext context)
    {
        try
        {
            var authService = ResolveAuthService();
            if (authService == null)
            {
                return new RefreshTokenResponse
                {
                    Success = false,
                    Message = "Auth service not available"
                };
            }

            var result = await InvokeRefreshToken(authService);

            return BuildRefreshTokenResponse(result);
        }
        catch (Exception ex)
        {
            return new RefreshTokenResponse
            {
                Success = false,
                Message = ex.Message
            };
        }
    }

    private object? ResolveAuthService()
    {
        var authServiceType = Type.GetType(
            "eFormAPI.Web.Abstractions.IAuthService, eFormAPI.Web");
        if (authServiceType == null)
        {
            return null;
        }

        return _serviceProvider.GetService(authServiceType);
    }

    private static object CreateLoginModel(string username, string password)
    {
        var loginModel = new LoginModel
        {
            Username = username,
            Password = password
        };
        return loginModel;
    }

    private static async Task<dynamic> InvokeAuthenticateUser(object authService, object loginModel)
    {
        var method = authService.GetType().GetMethod("AuthenticateUser");
        var task = (Task)method!.Invoke(authService, new[] { loginModel })!;
        await task;
        return ((dynamic)task).Result;
    }

    private static async Task<dynamic> InvokeRefreshToken(object authService)
    {
        var method = authService.GetType().GetMethod("RefreshToken");
        var task = (Task)method!.Invoke(authService, Array.Empty<object>())!;
        await task;
        return ((dynamic)task).Result;
    }

    private static AuthenticateUserResponse BuildAuthenticateUserResponse(dynamic result)
    {
        var response = new AuthenticateUserResponse
        {
            Success = (bool)result.Success,
            Message = (string)(result.Message ?? "")
        };

        if (result.Success && result.Model != null)
        {
            response.Model = new AuthUserModel
            {
                AccessToken = (string)(result.Model.AccessToken ?? ""),
                FirstName = (string)(result.Model.FirstName ?? ""),
                LastName = (string)(result.Model.LastName ?? "")
            };
        }

        return response;
    }

    private static RefreshTokenResponse BuildRefreshTokenResponse(dynamic result)
    {
        var response = new RefreshTokenResponse
        {
            Success = (bool)result.Success,
            Message = (string)(result.Message ?? "")
        };

        if (result.Success && result.Model != null)
        {
            response.Model = new AuthUserModel
            {
                AccessToken = (string)(result.Model.AccessToken ?? ""),
                FirstName = (string)(result.Model.FirstName ?? ""),
                LastName = (string)(result.Model.LastName ?? "")
            };
        }

        return response;
    }
}
