namespace TimePlanning.Pn.Infrastructure.Models.DeviceToken;

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("DeviceTokens")]
public class DeviceToken
{
    [Key]
    public int Id { get; set; }
    public int SdkSiteId { get; set; }

    [Required]
    [MaxLength(512)]
    public string Token { get; set; } = string.Empty;

    [Required]
    [MaxLength(16)]
    public string Platform { get; set; } = string.Empty; // "android" or "ios"

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    [MaxLength(50)]
    public string WorkflowState { get; set; } = "created";
}
