using System.Collections.Generic;
using System.Threading.Tasks;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;
using TimePlanning.Pn.Infrastructure.Models.RegistrationDevice;

namespace TimePlanning.Pn.Services.TimePlanningRegistrationDeviceService;

public interface ITimePlanningRegistrationDeviceService
{
    Task<OperationDataResult<List<TimePlanningRegistrationDeviceModel>>> Index(TimePlanningRegistrationDeviceRequestModel model);
    Task<OperationResult> Create(TimePlanningRegistrationDeviceCreateModel model);
    Task<OperationDataResult<TimePlanningRegistrationDeviceModel>> Read(int id);
    Task<OperationResult> Update(TimePlanningRegistrationDeviceUpdateModel model);
    Task<OperationResult> Delete(int id);
    Task<OperationResult> Activate(TimePlanningRegistrationDeviceActivateModel model);
    Task<OperationDataResult<TimePlanningRegistrationDeviceModel>> RequestOtp(int id);
}