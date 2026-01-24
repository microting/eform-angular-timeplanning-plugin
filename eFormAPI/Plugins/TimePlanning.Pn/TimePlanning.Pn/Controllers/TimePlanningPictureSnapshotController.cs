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

using Microsoft.AspNetCore.Http;

namespace TimePlanning.Pn.Controllers;

using System.Collections.Generic;
using System.Threading.Tasks;
using Infrastructure.Models.PictureSnapshot;
using Microsoft.AspNetCore.Mvc;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;
using Services.TimePlanningPictureSnapshotService;

[Route("api/time-planning-pn/picture-snapshots")]
public class TimePlanningPictureSnapshotController
{
    private readonly ITimePlanningPictureSnapshotService _pictureSnapshotService;

    public TimePlanningPictureSnapshotController(ITimePlanningPictureSnapshotService pictureSnapshotService)
    {
        _pictureSnapshotService = pictureSnapshotService;
    }

    [HttpGet]
    [Route("")]
    public async Task<OperationDataResult<List<PictureSnapshotModel>>> Index([FromQuery] int planRegistrationId)
    {
        return await _pictureSnapshotService.Index(planRegistrationId);
    }

    [HttpGet]
    [Route("{id}")]
    public async Task<OperationDataResult<PictureSnapshotModel>> GetById(int id)
    {
        return await _pictureSnapshotService.GetById(id);
    }

    [HttpGet]
    [Route("{id}/file")]
    public async Task<IActionResult> GetFile(int id)
    {
        var result = await _pictureSnapshotService.GetFile(id);

        if (!result.Success)
        {
            return new BadRequestObjectResult(result);
        }

        return new FileContentResult(result.Model, "image/jpeg");
    }

    [HttpPost]
    [Route("")]
    public async Task<OperationResult> Create([FromBody] PictureSnapshotCreateModel model, [FromForm] IFormFile file)
    {
        return await _pictureSnapshotService.Create(model, file);
    }

    [HttpPut]
    [Route("")]
    public async Task<OperationResult> Update([FromBody] PictureSnapshotUpdateModel model)
    {
        return await _pictureSnapshotService.Update(model);
    }

    [HttpDelete]
    [Route("{id}")]
    public async Task<OperationResult> Delete(int id)
    {
        return await _pictureSnapshotService.Delete(id);
    }
}
