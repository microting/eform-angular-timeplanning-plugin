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

    [HttpGet]
    [Route("{id}")]
    public async Task<OperationDataResult<TimePlanningRegistrationDeviceModel>> Read(int id)
    {
        return await _registrationDeviceService.Read(id);
    }

    [HttpPut]
    public async Task<OperationResult> Update([FromBody] TimePlanningRegistrationDeviceUpdateModel model)
    {
        return await _registrationDeviceService.Update(model);
    }

    [HttpPost]
    [Route("activate")]
    public async Task<OperationResult> Activate([FromBody] TimePlanningRegistrationDeviceActivateModel model)
    {
        return await _registrationDeviceService.Activate(model);
    }

    [HttpDelete]
    public async Task<OperationResult> Delete(int id)
    {
        return await _registrationDeviceService.Delete(id);
    }

}