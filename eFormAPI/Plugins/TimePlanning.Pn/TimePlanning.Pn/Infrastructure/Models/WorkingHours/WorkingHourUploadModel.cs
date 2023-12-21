using Microsoft.AspNetCore.Http;

namespace TimePlanning.Pn.Infrastructure.Models.WorkingHours;

public class WorkingHourUploadModel
{
    public IFormFile File { get; set; }
}