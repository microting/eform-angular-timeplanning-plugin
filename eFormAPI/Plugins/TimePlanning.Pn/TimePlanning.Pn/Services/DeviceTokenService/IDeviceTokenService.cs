namespace TimePlanning.Pn.Services.DeviceTokenService;

using System.Threading.Tasks;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;

public interface IDeviceTokenService
{
    /// <summary>
    /// Registers a device token for the authenticated caller. The SDK site id
    /// is resolved server-side from the JWT (client-sent ids are ignored).
    /// Fails without storing anything when no active site resolves.
    /// </summary>
    Task<OperationResult> RegisterForCallerAsync(string token, string platform);

    Task<OperationResult> RegisterAsync(int sdkSiteId, string token, string platform);
    Task<OperationResult> UnregisterAsync(string token);
}
