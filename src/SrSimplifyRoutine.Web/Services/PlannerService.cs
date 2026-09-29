using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using SrSimplifyRoutine.Web.Data;

namespace SrSimplifyRoutine.Web.Services;

public sealed class PlannerService(IDbContextFactory<ApplicationDbContext> factory, ICurrentUser currentUser, AbuseGuard guard)
{
    public const int MaximumCategories = 30;
    public const int MaximumActivities = 2000;

    private async Task<string> AuthorizeAsync(ApplicationDbContext db, bool write = false)
    {
        var id = await currentUser.GetIdAsync();
        guard.Require(id, write);
        if (!await db.Users.AnyAsync(x => x.Id == id && x.EmailConfirmed))
            throw new UnauthorizedAccessException("Please sign in with a verified account.");
        return id;
    }

    public async Task<ApplicationUser> GetProfileAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        var id = await AuthorizeAsync(db);
        return await db.Users.AsNoTracking().SingleAsync(x => x.Id == id);
    }

    public async Task SaveProfileAsync(ProfileInput input)
    {
        Validate(input);
        PlannerClock.Zone(input.TimeZoneId);
        await using var db = await factory.CreateDbContextAsync();
        var id = await AuthorizeAsync(db, true);
        var user = await db.Users.SingleAsync(x => x.Id == id);
        user.DisplayName = input.DisplayName.Trim();
        user.TimeZoneId = input.TimeZoneId;
        await db.SaveChangesAsync();
    }

    public async Task<List<Category>> GetCategoriesAsync(bool includeArchived = false)
    {
        await using var db = await factory.CreateDbContextAsync();
        var id = await AuthorizeAsync(db);
        // First-use defaults are private to each account. Transaction and unique index prevent duplicates.
        await using var transaction = await db.Database.BeginTransactionAsync();
        if (!await db.Categories.AnyAsync(x => x.UserId == id))
        {
            db.Categories.AddRange(
                new Category { UserId = id, Name = "Studies", NormalizedName = "STUDIES", Colour = "#7660D4", Kind = CategoryKind.Study, WeeklyTargetMinutes = 480 },
                new Category { UserId = id, Name = "Workouts", NormalizedName = "WORKOUTS", Colour = "#258D76", Kind = CategoryKind.Workout, WeeklyTargetMinutes = 180 },
                new Category { UserId = id, Name = "Personal", NormalizedName = "PERSONAL", Colour = "#C67C38", WeeklyTargetMinutes = 120 });
            await db.SaveChangesAsync();
        }
        await transaction.CommitAsync();
        return await db.Categories.AsNoTracking().Where(x => x.UserId == id && (includeArchived || !x.IsArchived)).OrderBy(x => x.Id).ToListAsync();
    }

    public async Task SaveCategoryAsync(CategoryInput input)
    {
        Validate(input);
        await using var db = await factory.CreateDbContextAsync();
        var id = await AuthorizeAsync(db, true);
        await using var transaction = await db.Database.BeginTransactionAsync();
        var normalized = input.Name.Trim().ToUpperInvariant();
        if (await db.Categories.AnyAsync(x => x.UserId == id && x.NormalizedName == normalized && x.Id != input.Id))
            throw new PlannerException("You already have a category with that name, including archived categories.");
        Category category;
        if (input.Id is int categoryId)
            category = await db.Categories.SingleOrDefaultAsync(x => x.Id == categoryId && x.UserId == id)
                ?? throw new PlannerException("Category not found.");
        else
        {
            if (await db.Categories.CountAsync(x => x.UserId == id) >= MaximumCategories)
                throw new PlannerException($"You can keep up to {MaximumCategories} categories.");
            category = new Category { UserId = id };
            db.Categories.Add(category);
        }
        category.Name = input.Name.Trim();
        category.NormalizedName = normalized;
        category.Colour = input.Colour;
        category.Kind = input.Kind;
        category.WeeklyTargetMinutes = input.WeeklyTargetMinutes;
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    public async Task ArchiveCategoryAsync(int categoryId, bool archived)
    {
        await using var db = await factory.CreateDbContextAsync();
        var id = await AuthorizeAsync(db, true);
        var category = await db.Categories.SingleOrDefaultAsync(x => x.Id == categoryId && x.UserId == id)
            ?? throw new PlannerException("Category not found.");
        category.IsArchived = archived;
        await db.SaveChangesAsync();
    }

    public async Task<List<PlannerItem>> GetItemsAsync(DateTime fromUtc, DateTime toUtc)
    {
        if (toUtc <= fromUtc || (toUtc - fromUtc).TotalDays > 93)
            throw new PlannerException("Choose a date range of no more than three months.");
        await using var db = await factory.CreateDbContextAsync();
        var id = await AuthorizeAsync(db);
        return await db.PlannerItems.AsNoTracking().Include(x => x.Category)
            .Where(x => x.UserId == id && x.StartUtc < toUtc && x.EndUtc > fromUtc)
            .OrderBy(x => x.StartUtc).Take(MaximumActivities).ToListAsync();
    }

    public async Task<PlannerItem> GetItemAsync(int itemId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var id = await AuthorizeAsync(db);
        return await db.PlannerItems.AsNoTracking().Include(x => x.Category)
            .SingleOrDefaultAsync(x => x.Id == itemId && x.UserId == id)
            ?? throw new PlannerException("Activity not found.");
    }

    public async Task<int> SaveActivityAsync(ActivityInput input)
    {
        Validate(input);
        if (input.StartLocal.Year < 2000 || input.StartLocal.Year > 2100)
            throw new PlannerException("Choose a date between 2000 and 2100.");
        await using var db = await factory.CreateDbContextAsync();
        var id = await AuthorizeAsync(db, true);
        await using var transaction = await db.Database.BeginTransactionAsync();
        var category = await db.Categories.SingleOrDefaultAsync(x => x.Id == input.CategoryId && x.UserId == id)
            ?? throw new PlannerException("Category not found.");
        var zone = await db.Users.Where(x => x.Id == id).Select(x => x.TimeZoneId).SingleAsync();
        if (category.IsArchived) throw new PlannerException("Restore this category before adding or editing activities.");
        var count = input.Id.HasValue || input.Repeat == RepeatPattern.None ? 1 : input.Occurrences;
        if (!input.Id.HasValue && await db.PlannerItems.CountAsync(x => x.UserId == id) + count > MaximumActivities)
            throw new PlannerException($"Your planner has a {MaximumActivities}-activity limit. Delete older activities before adding more.");
        var series = count > 1 ? Guid.NewGuid() : (Guid?)null;
        var firstId = 0;
        for (var index = 0; index < count; index++)
        {
            var local = input.StartLocal.AddDays(index * (input.Repeat == RepeatPattern.Weekly ? 7 : 1));
            var start = PlannerClock.ToUtc(local, zone);
            PlannerItem item;
            if (input.Id is int itemId)
                item = await db.PlannerItems.SingleOrDefaultAsync(x => x.Id == itemId && x.UserId == id)
                    ?? throw new PlannerException("Activity not found.");
            else
            {
                item = new PlannerItem { UserId = id, SeriesId = series };
                db.PlannerItems.Add(item);
            }
            item.CategoryId = category.Id;
            item.Title = input.Title.Trim();
            item.Notes = input.Notes?.Trim() ?? "";
            item.StartUtc = start;
            item.EndUtc = start.AddMinutes(input.DurationMinutes);
            item.Status = input.Status;
            await db.SaveChangesAsync();
            if (index == 0) firstId = item.Id;
        }
        await transaction.CommitAsync();
        return firstId;
    }

    public async Task SetStatusAsync(int itemId, ActivityStatus status)
    {
        if (!Enum.IsDefined(status)) throw new PlannerException("Choose a valid status.");
        await using var db = await factory.CreateDbContextAsync();
        var id = await AuthorizeAsync(db, true);
        var item = await db.PlannerItems.SingleOrDefaultAsync(x => x.Id == itemId && x.UserId == id)
            ?? throw new PlannerException("Activity not found.");
        item.Status = status;
        await db.SaveChangesAsync();
    }

    public async Task DeleteActivityAsync(int itemId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var id = await AuthorizeAsync(db, true);
        var item = await db.PlannerItems.SingleOrDefaultAsync(x => x.Id == itemId && x.UserId == id)
            ?? throw new PlannerException("Activity not found.");
        db.PlannerItems.Remove(item);
        await db.SaveChangesAsync();
    }

    private static void Validate(object input)
    {
        var errors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(input, new ValidationContext(input), errors, true))
            throw new PlannerException(errors[0].ErrorMessage ?? "Check your entries.");
    }
}
