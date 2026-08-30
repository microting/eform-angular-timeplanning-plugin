namespace TimePlanning.Pn.Services.DeviceTokenService;

using System;
using System.Linq;
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
            if (string.IsNullOrWhiteSpace(appId))
            {
                _logger.LogWarning(
                    "Rejecting device-token registration: app_id is required");
                return new OperationResult(false, "app_id is required");
            }

            if (string.IsNullOrWhiteSpace(installationId))
            {
                _logger.LogWarning(
                    "Rejecting device-token registration: installation_id is required");
                return new OperationResult(false, "installation_id is required");
            }

            // Upsert on the install identity, including soft-deleted rows: the
            // unique index has no WorkflowState filter, so Create() over a
            // Removed row throws instead of inserting.
            var existing = await _dbContext.DeviceTokens
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
                    InstallationId = installationId,
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
    /// Ordered by Id so the outcome is deterministic if two rows ever share a
    /// token; the oldest wins. Pre-migration that could not happen at all -
    /// this table's old unique index was on Token alone.
    /// </summary>
    private async Task<DeviceToken> AdoptRowWithSameTokenAsync(
        string appId, string token, string installationId)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var adopted = await _dbContext.DeviceTokens
            .Where(dt => dt.AppId == appId && dt.FcmToken == token)
            .OrderBy(dt => dt.Id)
            .FirstOrDefaultAsync();

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
