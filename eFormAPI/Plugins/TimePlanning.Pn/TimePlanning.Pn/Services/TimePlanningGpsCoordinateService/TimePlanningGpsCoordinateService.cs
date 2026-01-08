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

namespace TimePlanning.Pn.Services.TimePlanningGpsCoordinateService;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Infrastructure.Models.GpsCoordinate;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microting.eForm.Infrastructure.Constants;
using Microting.eFormApi.BasePn.Abstractions;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;
using Microting.TimePlanningBase.Infrastructure.Data;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using TimePlanningLocalizationService;

public class TimePlanningGpsCoordinateService(
    ILogger<TimePlanningGpsCoordinateService> logger,
    TimePlanningPnDbContext dbContext,
    IUserService userService,
    ITimePlanningLocalizationService localizationService)
    : ITimePlanningGpsCoordinateService
{
    public async Task<OperationDataResult<List<GpsCoordinateModel>>> Index(int planRegistrationId)
    {
        try
        {
            var gpsCoordinates = await dbContext.GpsCoordinates
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .Where(x => x.PlanRegistrationId == planRegistrationId)
                .Select(x => new GpsCoordinateModel
                {
                    Id = x.Id,
                    PlanRegistrationId = x.PlanRegistrationId,
                    Latitude = x.Latitude,
                    Longitude = x.Longitude,
                    RegistrationType = x.RegistrationType
                })
                .ToListAsync();

            return new OperationDataResult<List<GpsCoordinateModel>>(true, gpsCoordinates);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting GPS coordinates for plan registration {PlanRegistrationId}", planRegistrationId);
            return new OperationDataResult<List<GpsCoordinateModel>>(false,
                localizationService.GetString("ErrorWhileObtainingGpsCoordinates"));
        }
    }

    public async Task<OperationDataResult<GpsCoordinateModel>> GetById(int id)
    {
        try
        {
            var gpsCoordinate = await dbContext.GpsCoordinates
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .Where(x => x.Id == id)
                .Select(x => new GpsCoordinateModel
                {
                    Id = x.Id,
                    PlanRegistrationId = x.PlanRegistrationId,
                    Latitude = x.Latitude,
                    Longitude = x.Longitude,
                    RegistrationType = x.RegistrationType
                })
                .FirstOrDefaultAsync();

            if (gpsCoordinate == null)
            {
                return new OperationDataResult<GpsCoordinateModel>(false,
                    localizationService.GetString("GpsCoordinateNotFound"));
            }

            return new OperationDataResult<GpsCoordinateModel>(true, gpsCoordinate);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting GPS coordinate with id {Id}", id);
            return new OperationDataResult<GpsCoordinateModel>(false,
                localizationService.GetString("ErrorWhileObtainingGpsCoordinate"));
        }
    }

    public async Task<OperationResult> Create(GpsCoordinateCreateModel model)
    {
        try
        {
            var gpsCoordinate = new GpsCoordinate
            {
                PlanRegistrationId = model.PlanRegistrationId,
                Latitude = model.Latitude,
                Longitude = model.Longitude,
                RegistrationType = model.RegistrationType,
                CreatedByUserId = userService.UserId,
                UpdatedByUserId = userService.UserId
            };

            await gpsCoordinate.Create(dbContext);

            return new OperationResult(true, localizationService.GetString("GpsCoordinateCreatedSuccessfully"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating GPS coordinate");
            return new OperationResult(false, localizationService.GetString("ErrorWhileCreatingGpsCoordinate"));
        }
    }

    public async Task<OperationResult> Update(GpsCoordinateUpdateModel model)
    {
        try
        {
            var gpsCoordinate = await dbContext.GpsCoordinates
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .Where(x => x.Id == model.Id)
                .FirstOrDefaultAsync();

            if (gpsCoordinate == null)
            {
                return new OperationResult(false, localizationService.GetString("GpsCoordinateNotFound"));
            }

            gpsCoordinate.PlanRegistrationId = model.PlanRegistrationId;
            gpsCoordinate.Latitude = model.Latitude;
            gpsCoordinate.Longitude = model.Longitude;
            gpsCoordinate.RegistrationType = model.RegistrationType;
            gpsCoordinate.UpdatedByUserId = userService.UserId;

            await gpsCoordinate.Update(dbContext);

            return new OperationResult(true, localizationService.GetString("GpsCoordinateUpdatedSuccessfully"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating GPS coordinate with id {Id}", model.Id);
            return new OperationResult(false, localizationService.GetString("ErrorWhileUpdatingGpsCoordinate"));
        }
    }

    public async Task<OperationResult> Delete(int id)
    {
        try
        {
            var gpsCoordinate = await dbContext.GpsCoordinates
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .Where(x => x.Id == id)
                .FirstOrDefaultAsync();

            if (gpsCoordinate == null)
            {
                return new OperationResult(false, localizationService.GetString("GpsCoordinateNotFound"));
            }

            gpsCoordinate.UpdatedByUserId = userService.UserId;
            await gpsCoordinate.Delete(dbContext);

            return new OperationResult(true, localizationService.GetString("GpsCoordinateDeletedSuccessfully"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting GPS coordinate with id {Id}", id);
            return new OperationResult(false, localizationService.GetString("ErrorWhileDeletingGpsCoordinate"));
        }
    }
}
