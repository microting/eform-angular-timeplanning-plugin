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
            // The site id is resolved server-side from the JWT (see
            // DeviceTokenService); the client no longer sends one. The client's
            // reported app build number is persisted for push version-gating,
            // and (AppId, InstallationId) is the identity of the stored row.
            // Clients shipped before that identity model send neither field and
            // proto3 decodes both as "", so the service reads them
            // backward-compatibly rather than rejecting; only an empty token
            // comes back as an unsuccessful OperationResponse - this transport
            // reports failures in the response body, not as a gRPC status.
            var result = await _deviceTokenService.RegisterForCallerAsync(
                request.Token, request.Platform, request.BuildNumber,
                request.AppId, request.InstallationId);

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
