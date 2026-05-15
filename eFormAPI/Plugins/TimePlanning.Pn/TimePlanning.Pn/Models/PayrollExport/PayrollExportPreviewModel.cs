namespace TimePlanning.Pn.Models.PayrollExport;

public class PayrollExportPreviewModel
{
    public int WorkerCount { get; set; }
    public decimal TotalHours { get; set; }
    public int LineCount { get; set; }
    public bool HasAlreadyExported { get; set; }
    public string EarliestExportDate { get; set; }
}
