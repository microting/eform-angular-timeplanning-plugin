namespace TimePlanning.Pn.Models.PayrollExport;

public class PayrollIntegrationSettingsModel
{
    public int PayrollSystem { get; set; }
    public int CutoffDay { get; set; } = 19;
    public string ApiBaseUrl { get; set; }
    public string ApiKey { get; set; }
    public string ApiSecret { get; set; }
}
