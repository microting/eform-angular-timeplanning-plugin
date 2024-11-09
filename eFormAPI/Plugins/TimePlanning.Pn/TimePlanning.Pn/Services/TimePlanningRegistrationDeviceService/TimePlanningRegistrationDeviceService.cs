using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microting.eForm.Dto;
using Microting.eForm.Infrastructure.Constants;
using Microting.eFormApi.BasePn.Abstractions;
using Microting.eFormApi.BasePn.Infrastructure.Helpers.PluginDbOptions;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;
using Microting.TimePlanningBase.Infrastructure.Data;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using Microting.TimePlanningBase.Infrastructure.Data.Models;
using Sentry;
using TimePlanning.Pn.Infrastructure.Models.RegistrationDevice;
using TimePlanning.Pn.Services.TimePlanningLocalizationService;

namespace TimePlanning.Pn.Services.TimePlanningRegistrationDeviceService;

public class TimePlanningRegistrationDeviceService(
    ILogger<TimePlanningRegistrationDeviceService> logger,
    TimePlanningPnDbContext dbContext,
    IUserService userService, IEFormCoreService _core,
    ITimePlanningLocalizationService localizationService)
    : ITimePlanningRegistrationDeviceService
{
    public async Task<OperationDataResult<List<TimePlanningRegistrationDeviceModel>>> Index(
        TimePlanningRegistrationDeviceRequestModel model)
    {
        try
        {
            var core = await _core.GetCore();
            var customerNo = await core.GetSdkSetting(Settings.customerNo);
            var registrationDevicesQuery = dbContext.RegistrationDevices.Where(x => x.WorkflowState != Constants.WorkflowStates.Removed);

            return new OperationDataResult<List<TimePlanningRegistrationDeviceModel>>(true, await registrationDevicesQuery.Select(x => new TimePlanningRegistrationDeviceModel
            {
                Id = x.Id,
                SoftwareVersion = x.SoftwareVersion,
                Model = x.Model,
                Manufacturer = x.Manufacturer,
                OsVersion = x.OsVersion,
                LastIp = x.LastIp,
                LastKnownLocation = x.LastKnownLocation,
                OtpEnabled = x.OtpEnabled,
                CustomerNo = customerNo,
                OtpCode = x.OtpCode,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt,
                Name = x.Name,
                Description = x.Description
            }).ToListAsync());
        }
        catch (Exception e)
        {
            SentrySdk.CaptureException(e);
            logger.LogError(e, e.Message);
            return new OperationDataResult<List<TimePlanningRegistrationDeviceModel>>(false,
                localizationService.GetString("ErrorObtainingRegistrationDevices"));
        }
    }

    public async Task<OperationDataResult<TimePlanningRegistrationDeviceModel>> Read(int id)
    {
        var registrationDevice = await dbContext.RegistrationDevices
            .Where(x => x.Id == id && x.WorkflowState != Constants.WorkflowStates.Removed)
            .Select(x => new TimePlanningRegistrationDeviceModel
            {
                Id = x.Id,
                SoftwareVersion = x.SoftwareVersion,
                Model = x.Model,
                Manufacturer = x.Manufacturer,
                OsVersion = x.OsVersion,
                LastIp = x.LastIp,
                LastKnownLocation = x.LastKnownLocation,
            }).FirstOrDefaultAsync();

        if (registrationDevice == null)
        {
            return new OperationDataResult<TimePlanningRegistrationDeviceModel>(false,
                localizationService.GetString("RegistrationDeviceNotFound"));
        }

        return new OperationDataResult<TimePlanningRegistrationDeviceModel>(true, registrationDevice);
    }

    public async Task<OperationResult> Activate(TimePlanningRegistrationDeviceActivateModel model)
    {
        var core = await _core.GetCore();
        var customerNo = await core.GetSdkSetting(Settings.customerNo);

        if (model.CustomerNo.ToString() != customerNo)
        {
            return new OperationResult(false,
                localizationService.GetString("CustomerNoMismatch"));
        }

        var registrationDevice = await dbContext.RegistrationDevices
            .Where(x => x.OtpCode == model.OtCode && x.WorkflowState != Constants.WorkflowStates.Removed)
            .FirstOrDefaultAsync();

        if (registrationDevice == null)
        {
            return new OperationResult(false,
                localizationService.GetString("RegistrationDeviceNotFound"));
        }

        registrationDevice.OtpEnabled = false;
        registrationDevice.OtpCode = null;
        await registrationDevice.GenerateToken(dbContext);

        var registrationDeviceAuth = new TimePlanningRegistrationDeviceAuthModel
        {
            Token = registrationDevice.Token
        };

        return new OperationDataResult<TimePlanningRegistrationDeviceAuthModel>(true,
            localizationService.GetString("RegistrationDeviceActivatedSuccessfully"), registrationDeviceAuth);
    }

    public async Task<OperationResult> Create(TimePlanningRegistrationDeviceCreateModel model)
    {
        var registrationDevice = new RegistrationDevice
        {
            CreatedByUserId = userService.UserId,
            UpdatedByUserId = userService.UserId,
            Name = model.Name,
            Description = model.Description
        };

        await registrationDevice.Create(dbContext);

        await registrationDevice.GenerateOtp(dbContext);

        return new OperationResult(true,
            localizationService.GetString("RegistrationDeviceCreatedSuccessfully"));
    }

    public async Task<OperationResult> Update(TimePlanningRegistrationDeviceUpdateModel model)
    {
        var registrationDevice = await dbContext.RegistrationDevices
            .Where(x => x.Id == model.Id && x.WorkflowState != Constants.WorkflowStates.Removed)
            .FirstOrDefaultAsync();

        if (registrationDevice == null)
        {
            return new OperationResult(false,
                localizationService.GetString("RegistrationDeviceNotFound"));
        }

        registrationDevice.UpdatedByUserId = userService.UserId;
        registrationDevice.Name = model.Name;
        registrationDevice.Description = model.Description;

        await registrationDevice.Update(dbContext);

        return new OperationResult(true,
            localizationService.GetString("RegistrationDeviceUpdatedSuccessfully"));
    }

    public async Task<OperationDataResult<TimePlanningRegistrationDeviceModel>> RequestOtp(int id)
    {
        var registrationDevice = await dbContext.RegistrationDevices
            .Where(x => x.Id == id && x.WorkflowState != Constants.WorkflowStates.Removed)
            .FirstOrDefaultAsync();

        if (registrationDevice == null)
        {
            return new OperationDataResult<TimePlanningRegistrationDeviceModel>(false,
                localizationService.GetString("RegistrationDeviceNotFound"));
        }

        await registrationDevice.GenerateOtp(dbContext);

        var core = await _core.GetCore();
        var customerNo = await core.GetSdkSetting(Settings.customerNo);
        var registrationDeviceModel = new TimePlanningRegistrationDeviceModel
        {
            Id = registrationDevice.Id,
            SoftwareVersion = registrationDevice.SoftwareVersion,
            Model = registrationDevice.Model,
            Manufacturer = registrationDevice.Manufacturer,
            OsVersion = registrationDevice.OsVersion,
            LastIp = registrationDevice.LastIp,
            LastKnownLocation = registrationDevice.LastKnownLocation,
            OtpEnabled = registrationDevice.OtpEnabled,
            CustomerNo = customerNo,
            OtpCode = registrationDevice.OtpCode,
            CreatedAt = registrationDevice.CreatedAt,
            UpdatedAt = registrationDevice.UpdatedAt,
            Name = registrationDevice.Name,
            Description = registrationDevice.Description
        };

        return new OperationDataResult<TimePlanningRegistrationDeviceModel>(true,
            localizationService.GetString("RegistrationDeviceOtpRequestedSuccessfully"), registrationDeviceModel);
    }

    public async Task<OperationResult> Delete(int id)
    {
        var registrationDevice = await dbContext.RegistrationDevices
            .Where(x => x.Id == id && x.WorkflowState != Constants.WorkflowStates.Removed)
            .FirstOrDefaultAsync();

        if (registrationDevice == null)
        {
            return new OperationResult(false,
                localizationService.GetString("RegistrationDeviceNotFound"));
        }

        registrationDevice.UpdatedByUserId = userService.UserId;
        await registrationDevice.Delete(dbContext);

        return new OperationResult(true,
            localizationService.GetString("RegistrationDeviceDeletedSuccessfully"));
    }
}