using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace SrSimplifyRoutine.Web.Data;

public class ApplicationUser : IdentityUser
{
    [MaxLength(60)] public string DisplayName { get; set; } = "Planner";
    [MaxLength(100)] public string TimeZoneId { get; set; } = "Europe/Dublin";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
