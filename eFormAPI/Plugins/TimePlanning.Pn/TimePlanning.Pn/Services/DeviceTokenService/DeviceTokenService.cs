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
            //
            // installation_id: a legacy register has none, so fall back to the
            // pre-change identity - the token alone. The row found that way is
            // updated in place and KEEPS its InstallationId, so a legacy
            // register can never downgrade a real install id to a synthetic one.
            var existing = isLegacyRegister
                ? await FindRowWithSameTokenAsync(appId, token)
                : await _dbContext.DeviceTokens
                      .FirstOrDefaultAsync(dt => dt.AppId == appId
                                                 && dt.InstallationId == installationId)
                  ?? await AdoptRowWithSameTokenAsync(appId, token, installationId);

            if (existing != null)
            {
                existing.FcmToken = token;
                existing.SdkSiteId = sdkSiteId;
                existing.Platform = platform;
                existing.AppBuildNumber = buildNumber;
                // Explicit revive: PnBase.Update() leaves WorkflowState alone,
                // so a row pruned after an FCM permanent failure would stay
                // invisible to the send path and the device would go dark.
                existing.WorkflowState = Constants.WorkflowStates.Created;
                await existing.Update(_dbContext);
            }
            else
            {
                var deviceToken = new DeviceToken
                {
                    AppId = appId,
                    InstallationId = isLegacyRegister
                        ? SyntheticInstallationIdFor(token)
                        : installationId,
                    FcmToken = token,
                    SdkSiteId = sdkSiteId,
                    Platform = platform,
                    AppBuildNumber = buildNumber,
                };
                await deviceToken.Create(_dbContext);
            }
            return new OperationResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering device token for SdkSiteId {SdkSiteId}", sdkSiteId);
            return new OperationResult(false, "Error registering device token");
        }
    }

    /// <summary>Reserved prefix for synthesised installation ids.</summary>
    private const string LegacyInstallationIdPrefix = "legacy-token:";

    /// <summary>
    /// InstallationId stood in for a client that sent none. Derived from the
    /// FCM token so that repeated legacy registers from one device resolve to
    /// the SAME row instead of inserting a duplicate on every call - the column
    /// is NOT NULL, so an insert has to put something there.
    ///
    /// sha256(token) as lowercase hex, under a reserved prefix. That prefix is
    /// what makes a collision with a real client id impossible: clients send a
    /// canonical v4 UUID - 36 characters over [0-9a-f-] - and ':' is outside
    /// that alphabet, so no client-generated value can ever equal one of these.
    /// It is equally distinct from the migration's 'legacy:&lt;Id&gt;' backfill.
    /// The result is 77 characters, well inside InstallationId's varchar(128).
    ///
    /// Hashing the TOKEN is sound here specifically because this table's old
    /// unique index was on Token alone: tokens are unique in it, so two
    /// distinct devices cannot hash to the same id. The identical formula was
    /// rejected for the BackendConfiguration migration, whose old key was
    /// (WorkerId, FcmToken) and therefore permits duplicate tokens - that
    /// rejection does not carry over to this table.
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
