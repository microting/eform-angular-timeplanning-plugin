using System;
using System.Threading.Tasks;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;
using TimePlanning.Pn.Infrastructure.PayrollExporters;
using TimePlanning.Pn.Models.PayrollExport;

namespace TimePlanning.Pn.Services.PayrollExportService;

public interface IPayrollExportService
{
    Task<PayrollExportResult> ExportPayroll(DateTime periodStart, DateTime periodEnd);
    Task<OperationDataResult<PayrollExportPreviewModel>> PreviewPayroll(DateTime periodStart, DateTime periodEnd);
    Task<OperationDataResult<PayrollIntegrationSettingsModel>> GetPayrollSettings();
    Task<OperationResult> UpdatePayrollSettings(PayrollIntegrationSettingsModel model);
}
