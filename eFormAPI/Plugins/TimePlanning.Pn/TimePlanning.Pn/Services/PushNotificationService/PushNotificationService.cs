namespace TimePlanning.Pn.Services.PushNotificationService;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Infrastructure.Models.DeviceToken;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

public class PushNotificationService : IPushNotificationService
{
    private readonly DeviceTokenDbContext _dbContext;
    private readonly ILogger<PushNotificationService> _logger;
    private readonly bool _isEnabled;

    public PushNotificationService(
        DeviceTokenDbContext dbContext,
        IConfiguration configuration,
        ILogger<PushNotificationService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;

        var serviceAccountPath = configuration["Firebase:ServiceAccountPath"];
        if (!string.IsNullOrWhiteSpace(serviceAccountPath) && System.IO.File.Exists(serviceAccountPath))
        {
            try
            {
                if (FirebaseApp.DefaultInstance == null)
                {
                    FirebaseApp.Create(new AppOptions
                    {
                        Credential = GoogleCredential.FromFile(serviceAccountPath)
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
                "Firebase:ServiceAccountPath not configured or file not found. " +
                "Push notifications are disabled (SendToSiteAsync will be a no-op)");
            _isEnabled = false;
        }
    }

    public async Task SendToSiteAsync(
        int targetSdkSiteId,
        string title,
        string body,
        Dictionary<string, string>? data = null)
    {
        if (!_isEnabled)
        {
            _logger.LogDebug(
                "Push notification skipped (Firebase not configured): SdkSiteId={SdkSiteId}, Title={Title}",
                targetSdkSiteId, title);
            return;
        }

        try
        {
            var tokens = await _dbContext.DeviceTokens
                .Where(dt => dt.SdkSiteId == targetSdkSiteId && dt.WorkflowState == "created")
                .ToListAsync();

            if (tokens.Count == 0)
            {
                _logger.LogDebug("No device tokens found for SdkSiteId {SdkSiteId}", targetSdkSiteId);
                return;
            }

            foreach (var deviceToken in tokens)
            {
                try
                {
                    var message = new Message
                    {
                        Token = deviceToken.Token,
                        Notification = new Notification
                        {
                            Title = title,
                            Body = body
                        },
                        Data = data
                    };

                    await FirebaseMessaging.DefaultInstance.SendAsync(message);
                }
                catch (FirebaseMessagingException fex)
                    when (fex.MessagingErrorCode == MessagingErrorCode.Unregistered
                          || fex.MessagingErrorCode == MessagingErrorCode.InvalidArgument)
                {
                    _logger.LogInformation(
                        "Removing stale device token {TokenId} for SdkSiteId {SdkSiteId}: {Error}",
                        deviceToken.Id, targetSdkSiteId, fex.MessagingErrorCode);
                    _dbContext.DeviceTokens.Remove(deviceToken);
                    await _dbContext.SaveChangesAsync();
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
