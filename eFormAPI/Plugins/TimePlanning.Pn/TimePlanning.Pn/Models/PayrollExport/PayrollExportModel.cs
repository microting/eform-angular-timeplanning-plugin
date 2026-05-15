using System;
using System.Collections.Generic;

namespace TimePlanning.Pn.Models.PayrollExport;

public class PayrollExportModel
{
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public List<PayrollExportWorkerModel> Workers { get; set; } = new();
}

public class PayrollExportWorkerModel
{
    public string EmployeeNo { get; set; }
    public string FullName { get; set; }
    public List<PayrollExportLineModel> Lines { get; set; } = new();
}

public class PayrollExportLineModel
{
    public string PayrollCode { get; set; }
    public string PayCode { get; set; }
    public decimal Hours { get; set; }
    public DateTime Date { get; set; }
}
