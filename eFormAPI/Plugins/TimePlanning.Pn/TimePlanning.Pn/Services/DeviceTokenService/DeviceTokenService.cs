namespace TimePlanning.Pn.Services.DeviceTokenService;

using System;
using System.Threading.Tasks;
using Infrastructure.Models.DeviceToken;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;

public class DeviceTokenService : IDeviceTokenService
{
    private readonly DeviceTokenDbContext _dbContext;
    private readonly ILogger<DeviceTokenService> _logger;

    public DeviceTokenService(
        DeviceTokenDbContext dbContext,
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
                existing.UpdatedAt = DateTime.UtcNow;
                existing.WorkflowState = "created";
                _dbContext.DeviceTokens.Update(existing);
            }
            else
            {
                var deviceToken = new DeviceToken
                {
                    SdkSiteId = sdkSiteId,
                    Token = token,
                    Platform = platform,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    WorkflowState = "created"
                };
                await _dbContext.DeviceTokens.AddAsync(deviceToken);
            }

            await _dbContext.SaveChangesAsync();
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
                _dbContext.DeviceTokens.Remove(existing);
                await _dbContext.SaveChangesAsync();
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
