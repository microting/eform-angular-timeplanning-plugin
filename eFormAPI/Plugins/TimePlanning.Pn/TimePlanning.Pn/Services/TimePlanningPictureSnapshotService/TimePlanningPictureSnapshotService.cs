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

namespace TimePlanning.Pn.Services.TimePlanningPictureSnapshotService;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Infrastructure.Models.PictureSnapshot;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microting.eForm.Infrastructure.Constants;
using Microting.eFormApi.BasePn.Abstractions;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;
using Microting.TimePlanningBase.Infrastructure.Data;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using TimePlanningLocalizationService;

public class TimePlanningPictureSnapshotService(
    ILogger<TimePlanningPictureSnapshotService> logger,
    TimePlanningPnDbContext dbContext,
    IUserService userService,
    ITimePlanningLocalizationService localizationService,
    IEFormCoreService coreService)
    : ITimePlanningPictureSnapshotService
{
    public async Task<OperationDataResult<List<PictureSnapshotModel>>> Index(int planRegistrationId)
    {
        try
        {
            var pictureSnapshots = await dbContext.PictureSnapshots
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .Where(x => x.PlanRegistrationId == planRegistrationId)
                .Select(x => new PictureSnapshotModel
                {
                    Id = x.Id,
                    PlanRegistrationId = x.PlanRegistrationId,
                    PictureHash = x.PictureHash,
                    RegistrationType = x.RegistrationType,
                    FileUrl = $"/api/time-planning-pn/picture-snapshots/{x.Id}/file"
                })
                .ToListAsync();

            return new OperationDataResult<List<PictureSnapshotModel>>(true, pictureSnapshots);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting picture snapshots for plan registration {PlanRegistrationId}", planRegistrationId);
            return new OperationDataResult<List<PictureSnapshotModel>>(false,
                localizationService.GetString("ErrorWhileObtainingPictureSnapshots"));
        }
    }

    public async Task<OperationDataResult<PictureSnapshotModel>> GetById(int id)
    {
        try
        {
            var pictureSnapshot = await dbContext.PictureSnapshots
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .Where(x => x.Id == id)
                .Select(x => new PictureSnapshotModel
                {
                    Id = x.Id,
                    PlanRegistrationId = x.PlanRegistrationId,
                    PictureHash = x.PictureHash,
                    RegistrationType = x.RegistrationType,
                    FileUrl = $"/api/time-planning-pn/picture-snapshots/{x.Id}/file"
                })
                .FirstOrDefaultAsync();

            if (pictureSnapshot == null)
            {
                return new OperationDataResult<PictureSnapshotModel>(false,
                    localizationService.GetString("PictureSnapshotNotFound"));
            }

            return new OperationDataResult<PictureSnapshotModel>(true, pictureSnapshot);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting picture snapshot with id {Id}", id);
            return new OperationDataResult<PictureSnapshotModel>(false,
                localizationService.GetString("ErrorWhileObtainingPictureSnapshot"));
        }
    }

    public async Task<OperationDataResult<byte[]>> GetFile(int id)
    {
        try
        {
            var pictureSnapshot = await dbContext.PictureSnapshots
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .Where(x => x.Id == id)
                .FirstOrDefaultAsync();

            if (pictureSnapshot == null)
            {
                return new OperationDataResult<byte[]>(false,
                    localizationService.GetString("PictureSnapshotNotFound"));
            }

            if (string.IsNullOrEmpty(pictureSnapshot.PictureHash))
            {
                return new OperationDataResult<byte[]>(false,
                    localizationService.GetString("PictureSnapshotFileNotFound"));
            }

            var core = await coreService.GetCore();
            var response = await core.GetFileFromS3Storage(pictureSnapshot.PictureHash);

            if (response == null)
            {
                return new OperationDataResult<byte[]>(false,
                    localizationService.GetString("PictureSnapshotFileNotFound"));
            }

            await using var memoryStream = new MemoryStream();
            await response.ResponseStream.CopyToAsync(memoryStream);
            var fileBytes = memoryStream.ToArray();

            return new OperationDataResult<byte[]>(true, fileBytes);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting picture snapshot file with id {Id}", id);
            return new OperationDataResult<byte[]>(false,
                localizationService.GetString("ErrorWhileObtainingPictureSnapshotFile"));
        }
    }

    public async Task<OperationResult> Create(PictureSnapshotCreateModel model, IFormFile file)
    {
        try
        {
            var planRegistration = await dbContext.PlanRegistrations
                .Where(x => x.Date == model.Date)
                .Where(x => x.SdkSitId == model.SdkSiteId)
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .FirstOrDefaultAsync();
            string pictureHash = null;

            if (file.Length > 0)
            {
                var tempPath = Path.Combine(Path.GetTempPath(), model.FileName);
                await using (var stream = new FileStream(tempPath, FileMode.Create))
                {
                    await file.OpenReadStream().CopyToAsync(stream);
                }
                var core = await coreService.GetCore();
                await core.PutFileToStorageSystem(tempPath, model.FileName);
                pictureHash = model.FileName;

                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }

            var pictureSnapshot = new PictureSnapshot
            {
                PlanRegistrationId = planRegistration.Id,
                PictureHash = pictureHash ?? model.PictureHash,
                RegistrationType = model.RegistrationType,
                CreatedByUserId = userService.UserId,
                UpdatedByUserId = userService.UserId
            };

            await pictureSnapshot.Create(dbContext);

            return new OperationResult(true, localizationService.GetString("PictureSnapshotCreatedSuccessfully"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating picture snapshot");
            return new OperationResult(false, localizationService.GetString("ErrorWhileCreatingPictureSnapshot"));
        }
    }

    public async Task<OperationResult> Update(PictureSnapshotUpdateModel model)
    {
        try
        {
            var planRegistration = await dbContext.PlanRegistrations
                .Where(x => x.Date == model.Date)
                .Where(x => x.SdkSitId == model.SdkSiteId)
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .FirstOrDefaultAsync();
            var pictureSnapshot = await dbContext.PictureSnapshots
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .Where(x => x.Id == model.Id)
                .FirstOrDefaultAsync();

            if (pictureSnapshot == null)
            {
                return new OperationResult(false, localizationService.GetString("PictureSnapshotNotFound"));
            }

            if (model.FileContent != null && model.FileContent.Length > 0)
            {
                var tempPath = Path.Combine(Path.GetTempPath(), model.FileName);
                await File.WriteAllBytesAsync(tempPath, model.FileContent);

                var core = await coreService.GetCore();
                await core.PutFileToStorageSystem(tempPath, model.FileName);
                pictureSnapshot.PictureHash = model.FileName;

                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            else if (!string.IsNullOrEmpty(model.PictureHash))
            {
                pictureSnapshot.PictureHash = model.PictureHash;
            }

            pictureSnapshot.PlanRegistrationId = planRegistration.Id;
            pictureSnapshot.RegistrationType = model.RegistrationType;
            pictureSnapshot.UpdatedByUserId = userService.UserId;

            await pictureSnapshot.Update(dbContext);

            return new OperationResult(true, localizationService.GetString("PictureSnapshotUpdatedSuccessfully"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating picture snapshot with id {Id}", model.Id);
            return new OperationResult(false, localizationService.GetString("ErrorWhileUpdatingPictureSnapshot"));
        }
    }

    public async Task<OperationResult> Delete(int id)
    {
        try
        {
            var pictureSnapshot = await dbContext.PictureSnapshots
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .Where(x => x.Id == id)
                .FirstOrDefaultAsync();

            if (pictureSnapshot == null)
            {
                return new OperationResult(false, localizationService.GetString("PictureSnapshotNotFound"));
            }

            pictureSnapshot.UpdatedByUserId = userService.UserId;
            await pictureSnapshot.Delete(dbContext);

            return new OperationResult(true, localizationService.GetString("PictureSnapshotDeletedSuccessfully"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting picture snapshot with id {Id}", id);
            return new OperationResult(false, localizationService.GetString("ErrorWhileDeletingPictureSnapshot"));
        }
    }
}
