namespace TimePlanning.Pn.Services.PushNotificationService;

using System.Collections.Generic;
using System.Threading.Tasks;

public interface IPushNotificationService
{
    /// <summary>
    /// Sends a push to every registered device of <paramref name="targetSdkSiteId"/>.
    /// <paramref name="minBuild"/> gates delivery to devices whose reported
    /// AppBuildNumber is &gt;= the value; the default of 0 includes every device
    /// (including old installs that report 0), so existing callers are unaffected.
    /// </summary>
    Task SendToSiteAsync(int targetSdkSiteId, string title, string body, Dictionary<string, string>? data = null, int minBuild = 0);
}
