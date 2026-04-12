namespace TimePlanning.Pn.Services.DeviceTokenService;

using System.Threading.Tasks;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;

public interface IDeviceTokenService
{
    Task<OperationResult> RegisterAsync(int sdkSiteId, string token, string platform);
    Task<OperationResult> UnregisterAsync(string token);
}
