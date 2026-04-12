namespace TimePlanning.Pn.Services.PushNotificationService;

using System.Collections.Generic;
using System.Threading.Tasks;

public interface IPushNotificationService
{
    Task SendToSiteAsync(int targetSdkSiteId, string title, string body, Dictionary<string, string>? data = null);
}
