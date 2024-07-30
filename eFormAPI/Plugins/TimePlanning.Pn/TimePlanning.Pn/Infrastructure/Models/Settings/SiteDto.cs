namespace TimePlanning.Pn.Infrastructure.Models.Settings;

public class SiteDto
{
    #region var

    /// <summary>
    ///...
    /// </summary>
    public int SiteId { get; set; }

    /// <summary>
    ///...
    /// </summary>
    public string SiteName { get; set; }

    /// <summary>
    ///...
    /// </summary>
    public string FirstName { get; set; }

    /// <summary>
    ///...
    /// </summary>
    public string LastName { get; set; }

    /// <summary>
    ///...
    /// </summary>
    public int? CustomerNo { get; set; }

    /// <summary>
    ///...
    /// </summary>
    public int? OtpCode { get; set; }

    /// <summary>
    ///...
    /// </summary>
    public int? UnitId { get; set; }

    /// <summary>
    ///...
    /// </summary>
    public int? WorkerUid { get; set; }

    public string Email { get; set; }

    public string PinCode { get; set; }

    public string DefaultLanguage { get; set; }

    // convert eForm.Core.SiteDto to TimePlanning.Pn.Infrastructure.Models.Settings.SiteDto
    public static implicit operator SiteDto(Microting.eForm.Dto.SiteDto model)
    {
        return new SiteDto
        {
            SiteId = model.SiteId,
            SiteName = model.SiteName,
            FirstName = model.FirstName,
            LastName = model.LastName,
            CustomerNo = model.CustomerNo,
            OtpCode = model.OtpCode,
            UnitId = model.UnitId,
            WorkerUid = model.WorkerUid,
            Email = model.Email
        };
    }

    #endregion

}