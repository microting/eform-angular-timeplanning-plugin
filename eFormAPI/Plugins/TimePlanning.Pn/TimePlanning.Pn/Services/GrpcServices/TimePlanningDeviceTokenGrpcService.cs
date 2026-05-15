using System;
using System.Threading.Tasks;
using Grpc.Core;
using TimePlanning.Pn.Grpc;
using TimePlanning.Pn.Services.DeviceTokenService;

namespace TimePlanning.Pn.Services.GrpcServices;

public class TimePlanningDeviceTokenGrpcService
    : TimePlanningDeviceTokenService.TimePlanningDeviceTokenServiceBase
{
    private readonly IDeviceTokenService _deviceTokenService;

    public TimePlanningDeviceTokenGrpcService(IDeviceTokenService deviceTokenService)
    {
        _deviceTokenService = deviceTokenService;
    }

    public override async Task<OperationResponse> RegisterDeviceToken(
        RegisterDeviceTokenRequest request, ServerCallContext context)
    {
        try
        {
            var result = await _deviceTokenService.RegisterAsync(
                request.SdkSiteId, request.Token, request.Platform);

            return new OperationResponse
            {
                Success = result.Success,
                Message = result.Message ?? ""
            };
        }
        catch (Exception ex)
        {
            return new OperationResponse
            {
                Success = false,
                Message = ex.Message
            };
        }
    }

    public override async Task<OperationResponse> UnregisterDeviceToken(
        UnregisterDeviceTokenRequest request, ServerCallContext context)
    {
        try
        {
            var result = await _deviceTokenService.UnregisterAsync(request.Token);

            return new OperationResponse
            {
                Success = result.Success,
                Message = result.Message ?? ""
            };
        }
        catch (Exception ex)
        {
            return new OperationResponse
            {
                Success = false,
                Message = ex.Message
            };
        }
    }
}
