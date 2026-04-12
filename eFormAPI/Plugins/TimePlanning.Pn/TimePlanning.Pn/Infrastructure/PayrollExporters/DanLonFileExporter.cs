using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using TimePlanning.Pn.Models.PayrollExport;

namespace TimePlanning.Pn.Infrastructure.PayrollExporters;

public class DanLonFileExporter : IPayrollExporter
{
    public PayrollSystemType SystemType => PayrollSystemType.DanLon;

    public Task<PayrollExportResult> Export(PayrollExportModel model)
    {
        var sb = new StringBuilder();
        sb.AppendLine("MedarbejderNr;Lønart;Timer;FraDato;TilDato");

        foreach (var worker in model.Workers)
        {
            foreach (var line in worker.Lines)
            {
                sb.AppendLine(string.Join(";",
                    worker.EmployeeNo,
                    line.PayrollCode,
                    line.Hours.ToString("F2", CultureInfo.InvariantCulture),
                    line.Date.ToString("yyyy-MM-dd"),
                    line.Date.ToString("yyyy-MM-dd")));
            }
        }

        var fileName = $"DanLon_Export_{model.PeriodStart:yyyyMMdd}_{model.PeriodEnd:yyyyMMdd}.csv";
        var preamble = Encoding.UTF8.GetPreamble();
        var content = Encoding.UTF8.GetBytes(sb.ToString());
        var fileContent = new byte[preamble.Length + content.Length];
        preamble.CopyTo(fileContent, 0);
        content.CopyTo(fileContent, preamble.Length);

        return Task.FromResult(new PayrollExportResult
        {
            Success = true,
            FileName = fileName,
            FileContent = fileContent
        });
    }
}
