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
using Sentry;
using TimePlanning.Pn.Services.DeviceTokenService;
using DeviceToken = Microting.TimePlanningBase.Infrastructure.Data.Entities.DeviceToken;

public class PushNotificationService : IPushNotificationService
{
    /// <summary>
    /// AppId of the tokens this sender owns. It holds the credential for one
    /// Firebase project; a token minted by any other app returns
    /// SENDER_ID_MISMATCH, so those are filtered out at selection time rather
    /// than discovered at send time.
    /// </summary>
    private const string TimePlanningAppId = DeviceTokenService.TimePlanningAppId;

    /// <summary>
    /// Name of the FirebaseApp this sender owns.
    ///
    /// It MUST be named. FirebaseApp.DefaultInstance is process-wide, and
    /// TimePlanning.Pn is only one of several plugins loaded into a single
    /// eFormAPI.Web host process - BackendConfiguration.Pn carries its own
    /// sender, and more are coming. Each holds the credential for a DIFFERENT
    /// Firebase project, so whichever plugin initialises first would own the
    /// default instance and every other sender would silently push through
    /// that first project. Every token then comes back SENDER_ID_MISMATCH,
    /// which <see cref="PruneSenderIdMismatchesAsync"/> correctly reads as a
    /// credential fault and leaves alone - so the send is retried forever and
    /// never surfaces as an error. A named app makes the credentials
    /// per-plugin and the failure impossible.
    /// </summary>
    private const string FirebaseAppName = "microting-time";

    // FirebaseApp.Create THROWS FirebaseAppAlreadyExistsException when the
    // name is taken, and this service is scoped - one instance per request.
    // Without this lock two concurrent first requests both see "no app yet",
    // the loser's Create throws, the constructor's catch turns that into
    // "push disabled", and that request silently sends nothing.
    private static readonly object FirebaseInitLock = new();

    private readonly TimePlanningPnDbContext _dbContext;
    private readonly ILogger<PushNotificationService> _logger;
    private readonly FirebaseApp? _firebaseApp;

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
                _firebaseApp = EnsureFirebaseApp(serviceAccountJson);
                _logger.LogInformation(
                    "Firebase push notifications initialized on app {FirebaseAppName}",
                    FirebaseAppName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Firebase Admin SDK");
                _firebaseApp = null;
            }
        }
        else
        {
            _logger.LogWarning(
                "TimePlanningBaseSettings:FirebaseServiceAccountJson not configured. " +
                "Push notifications are disabled");
            _firebaseApp = null;
        }
    }

    /// <summary>
    /// Returns this plugin's named FirebaseApp, creating it on first use.
    ///
    /// Double-checked over <see cref="FirebaseApp.GetInstance(string)"/>,
    /// which returns null when the app is absent - unlike
    /// <see cref="FirebaseApp.Create(AppOptions, string)"/>, which throws when
    /// the name is already taken. Re-checking INSIDE the lock is what makes
    /// concurrent first requests idempotent instead of turning the loser into
    /// a swallowed exception. Mirrors AdhocReminderJob.EnsureFirebaseApp in
    /// eform-service-backendconfiguration-plugin.
    /// </summary>
    private static FirebaseApp EnsureFirebaseApp(string serviceAccountJson)
    {
        var app = FirebaseApp.GetInstance(FirebaseAppName);
        if (app != null)
        {
            return app;
        }

        lock (FirebaseInitLock)
        {
            return FirebaseApp.GetInstance(FirebaseAppName)
                   ?? FirebaseApp.Create(
                       new AppOptions
                       {
                           Credential = GoogleCredential.FromJson(serviceAccountJson)
                       },
                       FirebaseAppName);
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
    /// Resolves the live device tokens targeted by a push: this app's tokens,
    /// same site, still in the Created workflow state, and reporting an app
    /// build number at or above <paramref name="minBuild"/>. A
    /// <paramref name="minBuild"/> of 0 includes every device (old installs
    /// report AppBuildNumber 0).
    ///
    /// INVARIANT: this query must always carry an equality predicate on AppId.
    /// AppId is the leading column of
    /// IX_DeviceTokens_AppId_SdkSiteId_WorkflowState and the old site-only
    /// index was dropped with it, so a query without an AppId predicate has no
    /// usable index and table-scans. Presence is what matters, not where the
    /// clause sits - MariaDB normalises the conjunction. That index is defined
    /// in eform-timeplanning-base (TimePlanningPnDbContext.OnModelCreating);
    /// re-check it there whenever the base package is bumped.
    /// </summary>
    internal Task<List<DeviceToken>> ResolveTargetTokensAsync(int targetSdkSiteId, int minBuild) =>
        _dbContext.DeviceTokens
            .Where(dt => dt.AppId == TimePlanningAppId
                         && dt.SdkSiteId == targetSdkSiteId
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
        if (_firebaseApp == null)
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

            var senderIdMismatches = new List<DeviceToken>();

            foreach (var deviceToken in tokens)
            {
                try
                {
                    var message = BuildMessage(deviceToken.FcmToken, title, body, data);

                    // GetMessaging(app), never DefaultInstance: see FirebaseAppName.
                    await FirebaseMessaging.GetMessaging(_firebaseApp).SendAsync(message);
                }
                catch (FirebaseMessagingException fex)
                    when (fex.MessagingErrorCode == MessagingErrorCode.SenderIdMismatch)
                {
                    // Collected, not pruned here: the decision needs the whole
                    // send's outcome. See PruneSenderIdMismatchesAsync.
                    _logger.LogWarning(
                        "Device token {TokenId} for SdkSiteId {SdkSiteId} was minted by a "
                        + "different Firebase project (SenderIdMismatch)",
                        deviceToken.Id, targetSdkSiteId);
                    SentrySdk.CaptureMessage(
                        $"SenderIdMismatch for DeviceToken {deviceToken.Id} "
                        + $"(SdkSiteId {targetSdkSiteId})",
                        SentryLevel.Warning);
                    senderIdMismatches.Add(deviceToken);
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

            await PruneSenderIdMismatchesAsync(
                senderIdMismatches, tokens.Count, targetSdkSiteId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error sending push notifications to SdkSiteId {SdkSiteId}", targetSdkSiteId);
        }
    }

    /// <summary>
    /// Applies the prune decision for the tokens of one send that failed with
    /// SENDER_ID_MISMATCH.
    ///
    /// That error has two causes. Either the token was minted by a different
    /// app's Firebase project - a token fault, and pruning it is right - or
    /// this sender is holding the wrong credential
    /// (TimePlanningBaseSettings:FirebaseServiceAccountJson pointing at the
    /// wrong project), in which case EVERY token mismatches and a naive prune
    /// silently soft-deletes the tenant's entire token set. The two are
    /// indistinguishable per token, but not per send: a mismatch alongside
    /// tokens that went through is a token fault, while a wholesale mismatch
    /// is a credential fault and is left alone for an operator to fix.
    /// </summary>
    internal async Task PruneSenderIdMismatchesAsync(
        IReadOnlyList<DeviceToken> senderIdMismatches, int targetedCount, int targetSdkSiteId)
    {
        if (senderIdMismatches.Count == 0)
        {
            return;
        }

        if (senderIdMismatches.Count == targetedCount)
        {
            _logger.LogWarning(
                "All {Count} device tokens for SdkSiteId {SdkSiteId} returned SenderIdMismatch. "
                + "This is a Firebase credential fault, not a token fault - keeping the tokens. "
                + "Check TimePlanningBaseSettings:FirebaseServiceAccountJson",
                targetedCount, targetSdkSiteId);
            SentrySdk.CaptureMessage(
                $"All {targetedCount} device tokens for SdkSiteId {targetSdkSiteId} returned "
                + "SenderIdMismatch - check TimePlanningBaseSettings:FirebaseServiceAccountJson",
                SentryLevel.Warning);
            return;
        }

        foreach (var deviceToken in senderIdMismatches)
        {
            _logger.LogInformation(
                "Removing foreign device token {TokenId} for SdkSiteId {SdkSiteId}: SenderIdMismatch",
                deviceToken.Id, targetSdkSiteId);
            await deviceToken.Delete(_dbContext);
        }
    }
}
