using System.ComponentModel.DataAnnotations;
using SrSimplifyRoutine.Web.Data;

namespace SrSimplifyRoutine.Web.Services;

public sealed class CategoryInput
{
    public int? Id { get; set; }
    [Required, StringLength(40)] public string Name { get; set; } = "";
    [Required, RegularExpression("^#[0-9A-Fa-f]{6}$")] public string Colour { get; set; } = "#7660D4";
    [EnumDataType(typeof(CategoryKind))] public CategoryKind Kind { get; set; }
    [Range(0, 10080)] public int WeeklyTargetMinutes { get; set; }
}

public sealed class ActivityInput
{
    public int? Id { get; set; }
    [Required, StringLength(120)] public string Title { get; set; } = "";
    [Range(1, int.MaxValue, ErrorMessage = "Choose a category.")] public int CategoryId { get; set; }
    [StringLength(2000)] public string Notes { get; set; } = "";
    public DateTime StartLocal { get; set; } = DateTime.Today.AddHours(9);
    [Range(5, 1440)] public int DurationMinutes { get; set; } = 60;
    [EnumDataType(typeof(ActivityStatus))] public ActivityStatus Status { get; set; }
    [EnumDataType(typeof(RepeatPattern))] public RepeatPattern Repeat { get; set; }
    [Range(1, 12)] public int Occurrences { get; set; } = 1;
}

public sealed class ProfileInput
{
    [Required, StringLength(60)] public string DisplayName { get; set; } = "";
    [Required, StringLength(100)] public string TimeZoneId { get; set; } = "Europe/Dublin";
}

public sealed class PlannerException(string message) : Exception(message);
