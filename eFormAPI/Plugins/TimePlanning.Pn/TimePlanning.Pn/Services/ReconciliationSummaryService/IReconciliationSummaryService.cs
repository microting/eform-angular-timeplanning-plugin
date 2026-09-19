#nullable enable
namespace TimePlanning.Pn.Services.ReconciliationSummaryService;

using System.Threading.Tasks;
using Infrastructure.Models.Reconciliation;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;

public interface IReconciliationSummaryService
{
    /// <summary>Summary for the last closed payroll period as of DateTime.UtcNow.Date.</summary>
    Task<OperationDataResult<ReconciliationSummaryModel>> GetSummaryAsync();
}
