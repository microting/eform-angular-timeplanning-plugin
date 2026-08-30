namespace TimePlanning.Pn.Infrastructure.Models.DeviceToken;

public class RegisterDeviceTokenModel
{
    public string Token { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;

    /// <summary>Which app minted the token. Always "time" here.</summary>
    public string AppId { get; set; } = string.Empty;

    /// <summary>
    /// Stable per-install UUID; the identity of the stored row. Required.
    /// </summary>
    public string InstallationId { get; set; } = string.Empty;

    /// <summary>App build number reported by the client (0 = old/unknown).</summary>
    public int BuildNumber { get; set; }
}
