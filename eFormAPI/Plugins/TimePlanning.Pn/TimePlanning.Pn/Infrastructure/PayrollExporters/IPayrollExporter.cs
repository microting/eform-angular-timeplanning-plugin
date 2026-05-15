using System.Threading.Tasks;
using TimePlanning.Pn.Models.PayrollExport;

namespace TimePlanning.Pn.Infrastructure.PayrollExporters;

public interface IPayrollExporter
{
    PayrollSystemType SystemType { get; }
    Task<PayrollExportResult> Export(PayrollExportModel model);
}

public class PayrollExportResult
{
    public bool Success { get; set; }
    public string FileName { get; set; }
    public byte[] FileContent { get; set; }
    public string ErrorMessage { get; set; }
}
