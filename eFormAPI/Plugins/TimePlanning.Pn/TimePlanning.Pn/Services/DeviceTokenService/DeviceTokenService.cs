namespace TimePlanning.Pn.Services.DeviceTokenService;

using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Infrastructure.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microting.eForm.Infrastructure.Constants;
using Microting.eFormApi.BasePn.Abstractions;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;
using Microting.EformAngularFrontendBase.Infrastructure.Data;
using Microting.TimePlanningBase.Infrastructure.Data;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;

public class DeviceTokenService : IDeviceTokenService
{
    /// <summary>
    /// AppId stamped on every token this service registers. The DeviceToken
    /// entity is shared with the BackendConfiguration plugin, so each app's
    /// rows are told apart by this value.
    /// </summary>
    public const string TimePlanningAppId = "time";

    private readonly TimePlanningPnDbContext _dbContext;
    private readonly ILogger<DeviceTokenService> _logger;
    private readonly IUserService _userService;
    private readonly BaseDbContext _baseDbContext;
    private readonly IEFormCoreService _coreService;

    public DeviceTokenService(
        TimePlanningPnDbContext dbContext,
        ILogger<DeviceTokenService> logger,
        IUserService userService,
        BaseDbContext baseDbContext,
        IEFormCoreService coreService)
    {
        _dbContext = dbContext;
        _logger = logger;
        _userService = userService;
        _baseDbContext = baseDbContext;
        _coreService = coreService;
    }

    public async Task<OperationResult> RegisterForCallerAsync(
        string token, string platform, int buildNumber = 0,
        string appId = TimePlanningAppId, string installationId = null)
    {
        var sdkSiteId = await ResolveCallerSdkSiteIdAsync();
        if (sdkSiteId == 0)
        {
            _logger.LogWarning(
                "Rejecting device-token registration: caller has no active site");
            return new OperationResult(
                false, "Could not resolve an active site for the calling user");
        }

        return await RegisterAsync(sdkSiteId, token, platform, buildNumber, appId, installationId);
    }

    /// <summary>
    /// Resolves the authenticated caller's SDK site id (MicrotingUid) from
    /// the JWT. Returns 0 if the user has no worker/site record. Mirrors
    /// AbsenceRequestService.ResolveCallerSdkSiteIdAsync().
    /// </summary>
    private async Task<int> ResolveCallerSdkSiteIdAsync()
    {
        var currentUserAsync = await _userService.GetCurrentUserAsync();
        if (currentUserAsync == null)
        {
            return 0;
        }
        var currentUser = _baseDbContext.Users
            .Single(x => x.Id == currentUserAsync.Id);

        var sdkCore = await _coreService.GetCore();
        var sdkDbContext = sdkCore.DbContextHelper.GetDbContext();

        var worker = await sdkDbContext.Workers
            .Include(x => x.SiteWorkers)
            .ThenInclude(x => x.Site)
            .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
            .FirstOrDefaultAsync(x => x.Email == currentUser.Email);

        if (worker == null || worker.SiteWorkers.Count == 0)
        {
            return 0;
        }
        return worker.ResolveActiveSdkSiteId() ?? 0;
    }

    public async Task<OperationResult> RegisterAsync(
        int sdkSiteId, string token, string platform, int buildNumber = 0,
        string appId = TimePlanningAppId, string installationId = null)
    {
        try
        {
            // The token is the one field with no sensible fallback, and it is
            // not what old clients omit - keep it hard-required. Without this
            // guard the legacy path below would derive an installation id from
            // the empty string and store a junk row.
            if (string.IsNullOrWhiteSpace(token))
            {
                _logger.LogWarning(
                    "Rejecting device-token registration: fcm_token is required");
                return new OperationResult(false, "fcm_token is required");
            }

            // Clients shipped before the identity model send neither app_id nor
            // installation_id, and proto3 decodes an absent string as "". They
            // must keep registering until the app reaches stores, so both get a
            // backward-compatible reading here rather than a rejection.
            //
            // app_id: this service backs exactly one app, so "" is unambiguous.
            // (The same relaxation would NOT be sound in BackendConfiguration,
            // whose table is shared by two apps.)
            if (string.IsNullOrWhiteSpace(appId))
            {
                appId = TimePlanningAppId;
            }

            var isLegacyRegister = string.IsNullOrWhiteSpace(installationId);

            // Upsert on the install identity, including soft-deleted rows: the
            // unique index has no WorkflowState filter, so Create() over a
            // Removed row throws instead of inserting.
            DeviceToken existing;
            if (isLegacyRegister)
            {
                // installation_id: a legacy register has none, so fall back to
                // the pre-change identity - the token alone. The row found this
                // way is updated in place and KEEPS its InstallationId, so a
                // legacy register can never downgrade a real install id to a
                // synthetic one.
                existing = await FindRowWithSameTokenAsync(appId, token);
            }
            else
            {
                existing = await _dbContext.DeviceTokens
                    .FirstOrDefaultAsync(dt => dt.AppId == appId
                                               && dt.InstallationId == installationId);
                existing ??= await AdoptRowWithSameTokenAsync(appId, token, installationId);
            }

            if (existing != null)
            {
                ApplyRegistration(existing, token, sdkSiteId, platform, buildNumber);
                await existing.Update(_dbContext);
                return new OperationResult(true);
            }

            var newInstallationId = isLegacyRegister
                ? SyntheticInstallationIdFor(token)
                : installationId;

            var deviceToken = new DeviceToken
            {
                AppId = appId,
                InstallationId = newInstallationId,
                FcmToken = token,
                SdkSiteId = sdkSiteId,
                Platform = platform,
                AppBuildNumber = buildNumber,
            };

            try
            {
                await deviceToken.Create(_dbContext);
            }
            catch (DbUpdateException)
            {
                // Lost an insert race on IX_DeviceTokens_AppId_InstallationId.
                // Reachable on a legacy client's FIRST launch, which is the
                // "new installs never register" case this change exists to fix:
                // flutter-time's personal-view init and its onTokenRefresh
                // handler both register the same token concurrently, so both
                // miss the lookup above and both derive the same synthetic id.
                // Letting that reach the catch below would return a failure the
                // client turns into a Sentry warning - the exact noise being
                // removed here. The winner stored the row we wanted, so take it.
                //
                // Also covers the rarer case of a row already holding this
                // installation id under a stale token; adopting it converges on
                // one row per install either way.
                _dbContext.Entry(deviceToken).State = EntityState.Detached;

                var winner = await _dbContext.DeviceTokens.FirstOrDefaultAsync(
                    dt => dt.AppId == appId && dt.InstallationId == newInstallationId);
                if (winner == null)
                {
                    throw;
                }

                ApplyRegistration(winner, token, sdkSiteId, platform, buildNumber);
                await winner.Update(_dbContext);
            }
            return new OperationResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering device token for SdkSiteId {SdkSiteId}", sdkSiteId);
            return new OperationResult(false, "Error registering device token");
        }
    }

    /// <summary>
    /// Writes the mutable half of a register onto an existing row: the token
    /// rotates, and a different user on the same install reassigns the owner.
    ///
    /// The WorkflowState revive is explicit because PnBase.Update() leaves it
    /// alone - a row pruned after an FCM permanent failure would otherwise stay
    /// invisible to the send path and the device would go dark.
    /// </summary>
    private static void ApplyRegistration(
        DeviceToken row, string token, int sdkSiteId, string platform, int buildNumber)
    {
        row.FcmToken = token;
        row.SdkSiteId = sdkSiteId;
        row.Platform = platform;
        row.AppBuildNumber = buildNumber;
        row.WorkflowState = Constants.WorkflowStates.Created;
    }

    /// <summary>Reserved prefix for synthesised installation ids.</summary>
    private const string LegacyInstallationIdPrefix = "legacy-token:";

    /// <summary>
    /// InstallationId stood in for a client that sent none. Derived from the
    /// FCM token so that repeated legacy registers carrying the SAME token
    /// resolve to the same row instead of inserting a duplicate on every call -
    /// the column is NOT NULL, so an insert has to put something there.
    ///
    /// It cannot make a legacy client's token ROTATION land on the old row:
    /// nothing ties the new token to the old one without a real install id, so
    /// that inserts a second row and the device is pushed to twice until FCM
    /// reports the dead token. That is exactly the pre-identity-model
    /// behaviour, not a regression, and it ends the moment the client upgrades.
    ///
    /// The reserved prefix is what rules out a collision with a real client id:
    /// clients send a canonical v4 UUID - 36 characters over [0-9a-f-] - and
    /// ':' is outside that alphabet, so no client we ship can produce one.
    /// (Nothing server-side VALIDATES the shape; both transports accept an
    /// arbitrary string, so this is a claim about our clients, not an
    /// invariant.) It is equally distinct from the migration's
    /// 'legacy:&lt;Id&gt;' backfill. The result is 77 characters, well inside
    /// InstallationId's varchar(128).
    ///
    /// Hashing the TOKEN is sound in THIS table because its old unique index
    /// was on Token alone: tokens are unique here, so two distinct devices
    /// cannot hash to the same id. The identical formula was rejected for the
    /// BackendConfiguration migration, whose old key (WorkerId, FcmToken)
    /// permits duplicate tokens - that rejection does not carry over.
    /// </summary>
    private static string SyntheticInstallationIdFor(string token)
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return LegacyInstallationIdPrefix + Convert.ToHexString(digest).ToLowerInvariant();
    }

    /// <summary>
    /// The pre-identity lookup: one row per FCM token within this app. Ordered
    /// by Id so the outcome is deterministic if two rows ever share a token;
    /// the oldest wins.
    /// </summary>
    private Task<DeviceToken> FindRowWithSameTokenAsync(string appId, string token) =>
        _dbContext.DeviceTokens
            .Where(dt => dt.AppId == appId && dt.FcmToken == token)
            .OrderBy(dt => dt.Id)
            .FirstOrDefaultAsync();

    /// <summary>
    /// Claims a pre-existing row that already carries this FCM token under a
    /// different InstallationId, rewriting its InstallationId to the real one.
    /// Callers must have rejected a blank token first (RegisterAsync does);
    /// this does not re-check.
    ///
    /// This exists for the DeviceTokenIdentityModel migration in
    /// eform-timeplanning-base, which backfills every pre-existing row with
    /// InstallationId 'legacy:&lt;Id&gt;'. Without adoption the first register
    /// after that migration finds no (AppId, InstallationId) match and inserts
    /// a SECOND row: the legacy row and the new one are both live, both carry
    /// the same token and site, and the sender selects both - every
    /// pre-existing user gets doubled pushes indefinitely. FCM never clears it
    /// either, because the legacy row's token is perfectly valid.
    ///
    /// It also claims a row a LEGACY register created under a synthetic
    /// installation id: when the fleet upgrades and starts sending real ids,
    /// that row must be rewritten rather than joined by a second live row
    /// carrying the same token and site - the sender would select both and the
    /// user would get every push twice, forever.
    /// </summary>
    private async Task<DeviceToken> AdoptRowWithSameTokenAsync(
        string appId, string token, string installationId)
    {
        var adopted = await FindRowWithSameTokenAsync(appId, token);

        if (adopted == null)
        {
            return null;
        }

        _logger.LogInformation(
            "Adopting device-token row {DeviceTokenId} for install {InstallationId} "
            + "(was {PreviousInstallationId})",
            adopted.Id, installationId, adopted.InstallationId);
        adopted.InstallationId = installationId;
        return adopted;
    }

    public async Task<OperationResult> UnregisterAsync(string token)
    {
        try
        {
            // Scoped to this app: the old unique index on the token column is
            // gone, so an unscoped lookup could soft-delete another app's row
            // if the two ever shared a token value.
            var existing = await _dbContext.DeviceTokens
                .FirstOrDefaultAsync(dt => dt.AppId == TimePlanningAppId
                                           && dt.FcmToken == token);

            if (existing != null)
            {
                await existing.Delete(_dbContext);
            }

            return new OperationResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unregistering device token");
            return new OperationResult(false, "Error unregistering device token");
        }
    }
}
