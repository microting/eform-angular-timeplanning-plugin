using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;
using TimePlanning.Pn.Infrastructure.Models.RegistrationDevice;
using TimePlanning.Pn.Services.TimePlanningRegistrationDeviceService;

namespace TimePlanning.Pn.Controllers;

[Route("api/time-planning-pn/registration-device")]
public class TimePlanningRegistrationDeviceController : Controller
{
    private readonly ITimePlanningRegistrationDeviceService _registrationDeviceService;

    public TimePlanningRegistrationDeviceController(ITimePlanningRegistrationDeviceService registrationDeviceService)
    {
        _registrationDeviceService = registrationDeviceService;
    }

    [HttpPost]
    [Route("index")]
    public async Task<OperationDataResult<List<TimePlanningRegistrationDeviceModel>>> Index(
        [FromBody] TimePlanningRegistrationDeviceRequestModel model)
    {
        return await _registrationDeviceService.Index(model);
    }

    [HttpPost]
    public async Task<OperationResult> Create([FromBody] TimePlanningRegistrationDeviceCreateModel model)
    {
        return await _registrationDeviceService.Create(model);
    }

    [HttpPut]
    public async Task<OperationResult> Update([FromBody] TimePlanningRegistrationDeviceUpdateModel model)
    {
        return await _registrationDeviceService.Update(model);
    }

    [HttpDelete]
    public async Task<OperationResult> Delete(int id)
    {
        return await _registrationDeviceService.Delete(id);
    }

    [HttpPost]
    [Route("activate")]
    public async Task<OperationResult> Activate([FromForm] TimePlanningRegistrationDeviceActivateModel model)
    {
        return await _registrationDeviceService.Activate(model);
    }

    [HttpGet]
    [Route("request-otp/{id}")]
    public async Task<OperationDataResult<TimePlanningRegistrationDeviceModel>> RequestOtp(int id)
    {
        return await _registrationDeviceService.RequestOtp(id);
    }

}