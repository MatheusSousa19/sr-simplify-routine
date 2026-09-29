using System.ComponentModel.DataAnnotations;

namespace SrSimplifyRoutine.Web.Data;

public enum CategoryKind { General, Study, Workout }
public enum ActivityStatus { Planned, Completed, Skipped }
public enum RepeatPattern { None, Daily, Weekly }

public class Category
{
    public int Id { get; set; }
    [MaxLength(450)] public string UserId { get; set; } = "";
    [MaxLength(40)] public string Name { get; set; } = "";
    [MaxLength(80)] public string NormalizedName { get; set; } = "";
    [MaxLength(7)] public string Colour { get; set; } = "#7660D4";
    public CategoryKind Kind { get; set; }
    public int WeeklyTargetMinutes { get; set; }
    public bool IsArchived { get; set; }
}

public class PlannerItem
{
    public int Id { get; set; }
    [MaxLength(450)] public string UserId { get; set; } = "";
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    [MaxLength(120)] public string Title { get; set; } = "";
    [MaxLength(2000)] public string Notes { get; set; } = "";
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public ActivityStatus Status { get; set; }
    public Guid? SeriesId { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public int Minutes => (int)(EndUtc - StartUtc).TotalMinutes;
}
