#nullable enable
namespace TimePlanning.Pn.Controllers;

using System.Linq;
using System.Threading.Tasks;
using Infrastructure.Models.DeviceToken;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microting.eForm.Infrastructure.Constants;
using Microting.EformAngularFrontendBase.Infrastructure.Data;
using Microting.eFormApi.BasePn.Abstractions;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;
using Services.DeviceTokenService;

[Route("api/time-planning-pn/device-tokens")]
public class DeviceTokenController : Controller
{
    private readonly IDeviceTokenService _deviceTokenService;
    private readonly IUserService _userService;
    private readonly IEFormCoreService _coreService;
    private readonly BaseDbContext _baseDbContext;

    public DeviceTokenController(
        IDeviceTokenService deviceTokenService,
        IUserService userService,
        IEFormCoreService coreService,
        BaseDbContext baseDbContext)
    {
        _deviceTokenService = deviceTokenService;
        _userService = userService;
        _coreService = coreService;
        _baseDbContext = baseDbContext;
    }

    [HttpPost]
    public async Task<OperationResult> Register([FromBody] RegisterDeviceTokenModel model)
    {
        var sdkSiteId = await ResolveCallerSdkSiteIdAsync();
        if (sdkSiteId == 0)
        {
            return new OperationResult(false, "Could not resolve caller SdkSiteId");
        }

        return await _deviceTokenService.RegisterAsync(sdkSiteId, model.Token, model.Platform);
    }

    [HttpDelete]
    public async Task<OperationResult> Unregister([FromBody] UnregisterDeviceTokenModel model)
    {
        return await _deviceTokenService.UnregisterAsync(model.Token);
    }

    private async Task<int> ResolveCallerSdkSiteIdAsync()
    {
        var currentUserAsync = await _userService.GetCurrentUserAsync();
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
        return worker.SiteWorkers.First().Site.MicrotingUid ?? 0;
    }
}
