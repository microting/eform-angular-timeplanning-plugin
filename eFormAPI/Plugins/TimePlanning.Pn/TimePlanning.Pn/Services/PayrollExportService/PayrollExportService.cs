using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microting.eForm.Infrastructure.Constants;
using Microting.eFormApi.BasePn.Abstractions;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;
using Microting.TimePlanningBase.Infrastructure.Data;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using TimePlanning.Pn.Infrastructure.PayrollExporters;
using TimePlanning.Pn.Models.PayrollExport;
using TimePlanning.Pn.Services.TimePlanningLocalizationService;

namespace TimePlanning.Pn.Services.PayrollExportService;

public class PayrollExportService : IPayrollExportService
{
    private readonly TimePlanningPnDbContext _dbContext;
    private readonly ILogger<PayrollExportService> _logger;
    private readonly ITimePlanningLocalizationService _localizationService;
    private readonly IEFormCoreService _coreHelper;

    public PayrollExportService(
        TimePlanningPnDbContext dbContext,
        ILogger<PayrollExportService> logger,
        ITimePlanningLocalizationService localizationService,
        IEFormCoreService coreHelper)
    {
        _dbContext = dbContext;
        _logger = logger;
        _localizationService = localizationService;
        _coreHelper = coreHelper;
    }

    public async Task<PayrollExportResult> ExportPayroll(DateTime periodStart, DateTime periodEnd)
    {
        try
        {
            var settings = await _dbContext.PayrollIntegrationSettings
                .FirstOrDefaultAsync(x => x.WorkflowState != Constants.WorkflowStates.Removed);

            if (settings == null || settings.PayrollSystem == 0)
            {
                return new PayrollExportResult
                {
                    Success = false,
                    ErrorMessage = _localizationService.GetString("PayrollSystemNotConfigured")
                };
            }

            var payLines = await _dbContext.PlanRegistrationPayLines
                .Include(x => x.PlanRegistration)
                .Where(x => x.PlanRegistration.Date >= periodStart
                            && x.PlanRegistration.Date <= periodEnd
                            && x.PayrollCode != null
                            && x.WorkflowState != Constants.WorkflowStates.Removed)
                .ToListAsync();

            if (!payLines.Any())
            {
                return new PayrollExportResult
                {
                    Success = false,
                    ErrorMessage = _localizationService.GetString("NoPayrollDataForPeriod")
                };
            }

            // Resolve Worker.EmployeeNo from the eForm SDK context
            var sdkCore = await _coreHelper.GetCore();
            var sdkDbContext = sdkCore.DbContextHelper.GetDbContext();
            var siteIds = payLines.Select(x => x.PlanRegistration.SdkSitId).Distinct().ToList();
            var sites = await sdkDbContext.Sites
                .Where(s => siteIds.Contains(s.MicrotingUid!.Value))
                .Select(s => new { s.MicrotingUid, s.Name, EmployeeNo = s.EmployeeNo ?? string.Empty })
                .ToListAsync();
            var siteLookup = sites.ToDictionary(s => s.MicrotingUid!.Value, s => s);

            var workerGroups = payLines
                .GroupBy(x => x.PlanRegistration.SdkSitId)
                .Select(siteGroup =>
                {
                    var siteInfo = siteLookup.GetValueOrDefault(siteGroup.Key);
                    var empNo = siteInfo?.EmployeeNo;
                    // Skip workers without an EmployeeNo — they can't be exported to payroll
                    if (string.IsNullOrWhiteSpace(empNo))
                        return null;
                    return new PayrollExportWorkerModel
                    {
                        EmployeeNo = empNo,
                        FullName = siteInfo?.Name ?? string.Empty,
                        Lines = siteGroup
                            .Select(pl => new PayrollExportLineModel
                            {
                                PayrollCode = pl.PayrollCode,
                                PayCode = pl.PayCode,
                                Hours = (decimal)pl.Hours,
                                Date = pl.PlanRegistration.Date
                            })
                            .OrderBy(l => l.Date)
                            .ThenBy(l => l.PayrollCode)
                            .ToList()
                    };
                })
                .Where(w => w != null)
                .OrderBy(w => w.EmployeeNo)
                .ToList();

            var exportModel = new PayrollExportModel
            {
                PeriodStart = periodStart,
                PeriodEnd = periodEnd,
                Workers = workerGroups
            };

            var payrollSystem = (PayrollSystemType)settings.PayrollSystem;
            IPayrollExporter exporter = payrollSystem switch
            {
                PayrollSystemType.DanLon => new DanLonFileExporter(),
                PayrollSystemType.DataLon => new DataLonFileExporter(),
                _ => null
            };

            if (exporter == null)
            {
                return new PayrollExportResult
                {
                    Success = false,
                    ErrorMessage = _localizationService.GetString("UnsupportedPayrollSystem")
                };
            }

            var result = await exporter.Export(exportModel);

            if (result.Success)
            {
                var affectedRegistrationIds = payLines
                    .Select(x => x.PlanRegistrationId)
                    .Distinct()
                    .ToList();

                var registrations = await _dbContext.PlanRegistrations
                    .Where(x => affectedRegistrationIds.Contains(x.Id))
                    .ToListAsync();

                foreach (var registration in registrations)
                {
                    registration.TransferredToPayroll = true;
                    registration.TransferredToPayrollAt = DateTime.UtcNow;
                    await registration.Update(_dbContext);
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PayrollExportService.ExportPayroll: catch");
            return new PayrollExportResult
            {
                Success = false,
                ErrorMessage = _localizationService.GetString("ErrorWhileExportingPayroll")
            };
        }
    }

    public async Task<OperationDataResult<PayrollExportPreviewModel>> PreviewPayroll(
        DateTime periodStart, DateTime periodEnd)
    {
        try
        {
            var payLines = await _dbContext.PlanRegistrationPayLines
                .Include(x => x.PlanRegistration)
                .Where(x => x.PlanRegistration.Date >= periodStart
                            && x.PlanRegistration.Date <= periodEnd
                            && x.PayrollCode != null
                            && x.WorkflowState != Constants.WorkflowStates.Removed)
                .ToListAsync();

            var workerCount = payLines
                .Select(x => x.PlanRegistration.SdkSitId)
                .Distinct()
                .Count();

            var totalHours = payLines.Sum(x => (decimal)x.Hours);

            var hasAlreadyExported = payLines
                .Any(x => x.PlanRegistration.TransferredToPayroll);

            var earliestExportDate = payLines
                .Where(x => x.PlanRegistration.TransferredToPayrollAt.HasValue)
                .Select(x => x.PlanRegistration.TransferredToPayrollAt)
                .OrderBy(x => x)
                .FirstOrDefault();

            var model = new PayrollExportPreviewModel
            {
                WorkerCount = workerCount,
                TotalHours = totalHours,
                LineCount = payLines.Count,
                HasAlreadyExported = hasAlreadyExported,
                EarliestExportDate = earliestExportDate?.ToString("yyyy-MM-dd HH:mm")
            };

            return new OperationDataResult<PayrollExportPreviewModel>(true, model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PayrollExportService.PreviewPayroll: catch");
            return new OperationDataResult<PayrollExportPreviewModel>(false,
                _localizationService.GetString("ErrorWhilePreviewingPayroll"));
        }
    }

    public async Task<OperationDataResult<PayrollIntegrationSettingsModel>> GetPayrollSettings()
    {
        try
        {
            var settings = await _dbContext.PayrollIntegrationSettings
                .FirstOrDefaultAsync(x => x.WorkflowState != Constants.WorkflowStates.Removed);

            var model = new PayrollIntegrationSettingsModel
            {
                PayrollSystem = settings?.PayrollSystem ?? 0,
                CutoffDay = settings?.CutoffDay ?? 19,
                ApiBaseUrl = settings?.ApiBaseUrl,
                ApiKey = settings?.ApiKey,
                ApiSecret = settings?.ApiSecret
            };

            return new OperationDataResult<PayrollIntegrationSettingsModel>(true, model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PayrollExportService.GetPayrollSettings: catch");
            return new OperationDataResult<PayrollIntegrationSettingsModel>(false,
                _localizationService.GetString("ErrorWhileGettingPayrollSettings"));
        }
    }

    public async Task<OperationResult> UpdatePayrollSettings(PayrollIntegrationSettingsModel model)
    {
        try
        {
            if (model.CutoffDay < 1 || model.CutoffDay > 28)
            {
                return new OperationResult(false,
                    _localizationService.GetString("CutoffDayMustBeBetween1And28"));
            }

            var settings = await _dbContext.PayrollIntegrationSettings
                .FirstOrDefaultAsync(x => x.WorkflowState != Constants.WorkflowStates.Removed);

            if (settings == null)
            {
                settings = new PayrollIntegrationSettings
                {
                    PayrollSystem = model.PayrollSystem,
                    CutoffDay = model.CutoffDay,
                    ApiBaseUrl = model.ApiBaseUrl,
                    ApiKey = model.ApiKey,
                    ApiSecret = model.ApiSecret
                };
                await settings.Create(_dbContext);
            }
            else
            {
                settings.PayrollSystem = model.PayrollSystem;
                settings.CutoffDay = model.CutoffDay;
                settings.ApiBaseUrl = model.ApiBaseUrl;
                settings.ApiKey = model.ApiKey;
                settings.ApiSecret = model.ApiSecret;
                await settings.Update(_dbContext);
            }

            return new OperationResult(true,
                _localizationService.GetString("PayrollSettingsUpdatedSuccessfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PayrollExportService.UpdatePayrollSettings: catch");
            return new OperationResult(false,
                _localizationService.GetString("ErrorWhileUpdatingPayrollSettings"));
        }
    }
}
