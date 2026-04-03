using System.Threading.Tasks;
using Grpc.Core;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;
using TimePlanning.Pn.Grpc;
using TimePlanning.Pn.Infrastructure.Models.RegistrationDevice;
using TimePlanning.Pn.Services.TimePlanningRegistrationDeviceService;

namespace TimePlanning.Pn.Services.GrpcServices;

public class TimePlanningAuthGrpcService : TimePlanningAuthService.TimePlanningAuthServiceBase
{
    private readonly ITimePlanningRegistrationDeviceService _registrationDeviceService;

    public TimePlanningAuthGrpcService(ITimePlanningRegistrationDeviceService registrationDeviceService)
    {
        _registrationDeviceService = registrationDeviceService;
    }

    public override async Task<ActivateDeviceResponse> ActivateDevice(
        ActivateDeviceRequest request, ServerCallContext context)
    {
        var model = new TimePlanningRegistrationDeviceActivateModel
        {
            CustomerNo = int.Parse(request.CustomerNo),
            OtCode = request.OtCode
        };

        var result = await _registrationDeviceService.Activate(model);

        var response = new ActivateDeviceResponse
        {
            Success = result.Success,
            Message = result.Message ?? ""
        };

        if (result.Success && result is OperationDataResult<TimePlanningRegistrationDeviceAuthModel> dataResult
            && dataResult.Model != null)
        {
            response.Model = new ActivateDeviceModel
            {
                Token = dataResult.Model.Token ?? ""
            };
        }

        return response;
    }
}
