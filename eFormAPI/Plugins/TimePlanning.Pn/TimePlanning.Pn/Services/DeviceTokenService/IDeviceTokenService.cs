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
    /// <paramref name="appId"/> and <paramref name="installationId"/> are the
    /// stored row's identity; see <see cref="RegisterAsync"/>.
    /// </summary>
    Task<OperationResult> RegisterForCallerAsync(
        string token, string platform, int buildNumber = 0,
        string appId = DeviceTokenService.TimePlanningAppId, string installationId = null);

    /// <summary>
    /// Upserts one device-token row keyed on
    /// (<paramref name="appId"/>, <paramref name="installationId"/>) - the app
    /// install, not the FCM token. A rotated token updates that install's row
    /// in place, and a different user on the same install reassigns
    /// <paramref name="sdkSiteId"/>. Both arguments are required; an empty one
    /// is rejected without storing anything.
    /// </summary>
    Task<OperationResult> RegisterAsync(
        int sdkSiteId, string token, string platform, int buildNumber = 0,
        string appId = DeviceTokenService.TimePlanningAppId, string installationId = null);

    Task<OperationResult> UnregisterAsync(string token);
}
