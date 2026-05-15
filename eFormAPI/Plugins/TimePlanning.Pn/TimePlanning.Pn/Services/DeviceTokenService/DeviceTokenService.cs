namespace TimePlanning.Pn.Services.DeviceTokenService;

using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;
using Microting.TimePlanningBase.Infrastructure.Data;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;

public class DeviceTokenService : IDeviceTokenService
{
    private readonly TimePlanningPnDbContext _dbContext;
    private readonly ILogger<DeviceTokenService> _logger;

    public DeviceTokenService(
        TimePlanningPnDbContext dbContext,
        ILogger<DeviceTokenService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<OperationResult> RegisterAsync(int sdkSiteId, string token, string platform)
    {
        try
        {
            var existing = await _dbContext.DeviceTokens
                .FirstOrDefaultAsync(dt => dt.Token == token);

            if (existing != null)
            {
                existing.SdkSiteId = sdkSiteId;
                existing.Platform = platform;
                await existing.Update(_dbContext);
            }
            else
            {
                var deviceToken = new DeviceToken
                {
                    SdkSiteId = sdkSiteId,
                    Token = token,
                    Platform = platform,
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
