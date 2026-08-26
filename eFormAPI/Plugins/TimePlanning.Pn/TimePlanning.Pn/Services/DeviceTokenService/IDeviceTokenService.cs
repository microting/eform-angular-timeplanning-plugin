namespace TimePlanning.Pn.Services.DeviceTokenService;

using System.Threading.Tasks;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;

public interface IDeviceTokenService
{
    /// <summary>
    /// Registers a device token for the authenticated caller. The SDK site id
    /// is resolved server-side from the JWT (client-sent ids are ignored).
    /// Fails without storing anything when no active site resolves.
    /// <paramref name="buildNumber"/> is the client's app build number
    /// (0 = old/unknown), stored for push version-gating.
    /// </summary>
    Task<OperationResult> RegisterForCallerAsync(string token, string platform, int buildNumber = 0);

    Task<OperationResult> RegisterAsync(int sdkSiteId, string token, string platform, int buildNumber = 0);
    Task<OperationResult> UnregisterAsync(string token);
}
