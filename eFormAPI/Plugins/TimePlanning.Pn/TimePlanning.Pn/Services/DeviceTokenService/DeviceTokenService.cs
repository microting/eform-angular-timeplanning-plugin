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

    public async Task<OperationResult> RegisterForCallerAsync(string token, string platform, int buildNumber = 0)
    {
        var sdkSiteId = await ResolveCallerSdkSiteIdAsync();
        if (sdkSiteId == 0)
        {
            _logger.LogWarning(
                "Rejecting device-token registration: caller has no active site");
            return new OperationResult(
                false, "Could not resolve an active site for the calling user");
        }

        return await RegisterAsync(sdkSiteId, token, platform, buildNumber);
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

    public async Task<OperationResult> RegisterAsync(int sdkSiteId, string token, string platform, int buildNumber = 0)
    {
        try
        {
            var existing = await _dbContext.DeviceTokens
                .FirstOrDefaultAsync(dt => dt.Token == token);

            if (existing != null)
            {
                existing.SdkSiteId = sdkSiteId;
                existing.Platform = platform;
                existing.AppBuildNumber = buildNumber;
                await existing.Update(_dbContext);
            }
            else
            {
                var deviceToken = new DeviceToken
                {
                    SdkSiteId = sdkSiteId,
                    Token = token,
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

    public async Task<OperationResult> UnregisterAsync(string token)
    {
        try
        {
            var existing = await _dbContext.DeviceTokens
                .FirstOrDefaultAsync(dt => dt.Token == token);

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
