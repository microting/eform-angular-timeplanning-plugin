namespace TimePlanning.Pn.Infrastructure.Models.Settings;

public class Site
{
    public int SiteId { get; set; }
    public string SiteName { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public int? CustomerNo { get; set; }
    public int? OtpCode { get; set; }
    public int? UnitId { get; set; }
    public int? WorkerUid { get; set; }
    public string Email { get; set; }
    public string PinCode { get; set; }
    public string DefaultLanguage { get; set; }
    public bool HoursStarted { get; set; }
    public bool PauseStarted { get; set; }
    public bool AutoBreakCalculationActive { get; set; }
    public string AvatarUrl { get; set; }
    public bool ThirdShiftActive { get; set; }
    public bool FourthShiftActive { get; set; }
    public bool FifthShiftActive { get; set; }
}