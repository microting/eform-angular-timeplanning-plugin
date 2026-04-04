/*
The MIT License (MIT)

Copyright (c) 2007 - 2021 Microting A/S

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

namespace TimePlanning.Pn.Controllers;

using System.Collections.Generic;
using System.Threading.Tasks;
using Infrastructure.Models.GpsCoordinate;
using Microsoft.AspNetCore.Mvc;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;
using Services.TimePlanningGpsCoordinateService;

[Route("api/time-planning-pn/gps-coordinates")]
public class TimePlanningGpsCoordinateController
{
    private readonly ITimePlanningGpsCoordinateService _gpsCoordinateService;

    public TimePlanningGpsCoordinateController(ITimePlanningGpsCoordinateService gpsCoordinateService)
    {
        _gpsCoordinateService = gpsCoordinateService;
    }

    [HttpGet]
    [Route("")]
    public async Task<OperationDataResult<List<GpsCoordinateModel>>> Index([FromQuery] int planRegistrationId)
    {
        return await _gpsCoordinateService.Index(planRegistrationId);
    }

    [HttpGet]
    [Route("{id}")]
    public async Task<OperationDataResult<GpsCoordinateModel>> GetById(int id)
    {
        return await _gpsCoordinateService.GetById(id);
    }

    [HttpPost]
    [Route("")]
    public async Task<OperationResult> Create([FromBody] GpsCoordinateCreateModel model)
    {
        return await _gpsCoordinateService.Create(model);
    }

    [HttpPut]
    [Route("")]
    public async Task<OperationResult> Update([FromBody] GpsCoordinateUpdateModel model)
    {
        return await _gpsCoordinateService.Update(model);
    }

    [HttpDelete]
    [Route("{id}")]
    public async Task<OperationResult> Delete(int id)
    {
        return await _gpsCoordinateService.Delete(id);
    }
}
