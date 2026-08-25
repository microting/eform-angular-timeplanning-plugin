namespace TimePlanning.Pn.Services.PushNotificationService;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microting.eForm.Infrastructure.Constants;
using Microting.TimePlanningBase.Infrastructure.Data;
using DeviceToken = Microting.TimePlanningBase.Infrastructure.Data.Entities.DeviceToken;

public class PushNotificationService : IPushNotificationService
{
    private readonly TimePlanningPnDbContext _dbContext;
    private readonly ILogger<PushNotificationService> _logger;
    private readonly bool _isEnabled;

    public PushNotificationService(
        TimePlanningPnDbContext dbContext,
        ILogger<PushNotificationService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;

        var serviceAccountJson = _dbContext.PluginConfigurationValues
            .FirstOrDefault(x => x.Name == "TimePlanningBaseSettings:FirebaseServiceAccountJson")?.Value;

        if (!string.IsNullOrWhiteSpace(serviceAccountJson))
        {
            try
            {
                if (FirebaseApp.DefaultInstance == null)
                {
                    FirebaseApp.Create(new AppOptions
                    {
                        Credential = GoogleCredential.FromJson(serviceAccountJson)
                    });
                }
                _isEnabled = true;
                _logger.LogInformation("Firebase push notifications initialized");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Firebase Admin SDK");
                _isEnabled = false;
            }
        }
        else
        {
            _logger.LogWarning(
                "TimePlanningBaseSettings:FirebaseServiceAccountJson not configured. " +
                "Push notifications are disabled");
            _isEnabled = false;
        }
    }

    /// <summary>
    /// Builds an FCM message. When both <paramref name="title"/> and
    /// <paramref name="body"/> are empty the message is data-only (silent): no
    /// visible <see cref="Notification"/> block is attached and APNs
    /// content-available is set so iOS wakes the app in the background to
    /// process the data payload. Otherwise a normal visible notification is
    /// attached alongside the data.
    /// </summary>
    public static Message BuildMessage(
        string token,
        string title,
        string body,
        Dictionary<string, string>? data)
    {
        var hasNotification = !string.IsNullOrEmpty(title) || !string.IsNullOrEmpty(body);
        var message = new Message
        {
            Token = token,
            Data = data
        };

        if (hasNotification)
        {
            message.Notification = new Notification
            {
                Title = title,
                Body = body
            };
        }
        else
        {
            message.Apns = new ApnsConfig
            {
                Aps = new Aps { ContentAvailable = true }
            };
        }

        return message;
    }

    /// <summary>
    /// Resolves the live device tokens targeted by a push: same site, still in
    /// the Created workflow state, and reporting an app build number at or above
    /// <paramref name="minBuild"/>. A <paramref name="minBuild"/> of 0 includes
    /// every device (old installs report AppBuildNumber 0).
    /// </summary>
    internal Task<List<DeviceToken>> ResolveTargetTokensAsync(int targetSdkSiteId, int minBuild) =>
        _dbContext.DeviceTokens
            .Where(dt => dt.SdkSiteId == targetSdkSiteId
                         && dt.WorkflowState == Constants.WorkflowStates.Created
                         && dt.AppBuildNumber >= minBuild)
            .ToListAsync();

    public async Task SendToSiteAsync(
        int targetSdkSiteId,
        string title,
        string body,
        Dictionary<string, string>? data = null,
        int minBuild = 0)
    {
        if (!_isEnabled)
        {
            _logger.LogInformation(
                "Push notification skipped (Firebase not configured): SdkSiteId={SdkSiteId}, Title={Title}",
                targetSdkSiteId, title);
            return;
        }

        try
        {
            var tokens = await ResolveTargetTokensAsync(targetSdkSiteId, minBuild);

            if (tokens.Count == 0)
            {
                _logger.LogInformation("No device tokens found for SdkSiteId {SdkSiteId}", targetSdkSiteId);
                return;
            }

            foreach (var deviceToken in tokens)
            {
                try
                {
                    var message = BuildMessage(deviceToken.Token, title, body, data);

                    await FirebaseMessaging.DefaultInstance.SendAsync(message);
                }
                catch (FirebaseMessagingException fex)
                    when (fex.MessagingErrorCode == MessagingErrorCode.Unregistered
                          || fex.MessagingErrorCode == MessagingErrorCode.InvalidArgument)
                {
                    _logger.LogInformation(
                        "Removing stale device token {TokenId} for SdkSiteId {SdkSiteId}: {Error}",
                        deviceToken.Id, targetSdkSiteId, fex.MessagingErrorCode);
                    await deviceToken.Delete(_dbContext);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Failed to send push notification to token {TokenId} for SdkSiteId {SdkSiteId}",
                        deviceToken.Id, targetSdkSiteId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error sending push notifications to SdkSiteId {SdkSiteId}", targetSdkSiteId);
        }
    }
}
