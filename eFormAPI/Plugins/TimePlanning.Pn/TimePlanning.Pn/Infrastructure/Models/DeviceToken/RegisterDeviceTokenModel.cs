namespace TimePlanning.Pn.Infrastructure.Models.DeviceToken;

public class RegisterDeviceTokenModel
{
    public string Token { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;

    /// <summary>
    /// Which app minted the token. Always "time" here, so a body that omits it
    /// (binding to "") is read as "time" rather than rejected.
    /// </summary>
    public string AppId { get; set; } = string.Empty;

    /// <summary>
    /// Stable per-install UUID; the identity of the stored row. A body that
    /// omits it binds to "" and falls back to matching on the FCM token - see
    /// IDeviceTokenService.RegisterAsync.
    /// </summary>
    public string InstallationId { get; set; } = string.Empty;

    /// <summary>App build number reported by the client (0 = old/unknown).</summary>
    public int BuildNumber { get; set; }
}
