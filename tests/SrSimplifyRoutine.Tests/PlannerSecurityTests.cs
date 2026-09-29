using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SrSimplifyRoutine.Web.Data;
using SrSimplifyRoutine.Web.Services;

namespace SrSimplifyRoutine.Tests;

public sealed class PlannerSecurityTests
{
    private static ActivityInput Activity(int categoryId) => new()
    {
        Title = "Study C#", CategoryId = categoryId,
        StartLocal = new DateTime(2026, 10, 5, 18, 0, 0), DurationMinutes = 60
    };

    [Fact]
    public async Task Defaults_are_private_and_created_once()
    {
        await using var scope = await TestScope.Create();
        var first = await scope.Alice.GetCategoriesAsync();
        var again = await scope.Alice.GetCategoriesAsync();
        var other = await scope.Bob.GetCategoriesAsync();
        Assert.Equal(3, first.Count);
        Assert.Equal(first.Select(x => x.Id), again.Select(x => x.Id));
        Assert.Empty(first.Select(x => x.Id).Intersect(other.Select(x => x.Id)));
    }

    [Fact]
    public async Task Queries_and_direct_ids_do_not_reveal_another_users_activity()
    {
        await using var s = await TestScope.Create();
        var category = (await s.Alice.GetCategoriesAsync())[0];
        var id = await s.Alice.SaveActivityAsync(Activity(category.Id));
        Assert.Empty(await s.Bob.GetItemsAsync(new(2026, 10, 1), new(2026, 11, 1)));
        await Assert.ThrowsAsync<PlannerException>(() => s.Bob.GetItemAsync(id));
    }

    [Fact]
    public async Task Other_user_cannot_edit_complete_or_delete_an_activity()
    {
        await using var s = await TestScope.Create();
        var category = (await s.Alice.GetCategoriesAsync())[0];
        var ownCategory = (await s.Bob.GetCategoriesAsync())[0];
        var id = await s.Alice.SaveActivityAsync(Activity(category.Id));
        var attack = Activity(ownCategory.Id); attack.Id = id; attack.Title = "Changed";
        await Assert.ThrowsAsync<PlannerException>(() => s.Bob.SaveActivityAsync(attack));
        await Assert.ThrowsAsync<PlannerException>(() => s.Bob.SetStatusAsync(id, ActivityStatus.Completed));
        await Assert.ThrowsAsync<PlannerException>(() => s.Bob.DeleteActivityAsync(id));
        Assert.Equal("Study C#", (await s.Alice.GetItemAsync(id)).Title);
    }

    [Fact]
    public async Task Other_user_cannot_link_edit_or_archive_a_category()
    {
        await using var s = await TestScope.Create();
        var category = (await s.Alice.GetCategoriesAsync())[0];
        await Assert.ThrowsAsync<PlannerException>(() => s.Bob.SaveActivityAsync(Activity(category.Id)));
        await Assert.ThrowsAsync<PlannerException>(() => s.Bob.SaveCategoryAsync(new() { Id = category.Id, Name = "Stolen" }));
        await Assert.ThrowsAsync<PlannerException>(() => s.Bob.ArchiveCategoryAsync(category.Id, true));
    }

    [Fact]
    public async Task Database_itself_rejects_a_cross_user_category_link()
    {
        await using var s = await TestScope.Create();
        var category = (await s.Alice.GetCategoriesAsync())[0];
        await using var db = s.Factory.CreateDbContext();
        db.PlannerItems.Add(new() { UserId = "bob", CategoryId = category.Id, Title = "Forbidden", StartUtc = DateTime.UtcNow, EndUtc = DateTime.UtcNow.AddHours(1) });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Owner_can_edit_complete_reopen_and_delete()
    {
        await using var s = await TestScope.Create();
        var category = (await s.Alice.GetCategoriesAsync())[0];
        var input = Activity(category.Id);
        var id = await s.Alice.SaveActivityAsync(input);
        input.Id = id; input.Title = "SQL revision";
        await s.Alice.SaveActivityAsync(input);
        await s.Alice.SetStatusAsync(id, ActivityStatus.Completed);
        Assert.Equal(ActivityStatus.Completed, (await s.Alice.GetItemAsync(id)).Status);
        await s.Alice.SetStatusAsync(id, ActivityStatus.Planned);
        Assert.Equal("SQL revision", (await s.Alice.GetItemAsync(id)).Title);
        await s.Alice.DeleteActivityAsync(id);
        await Assert.ThrowsAsync<PlannerException>(() => s.Alice.GetItemAsync(id));
    }

    [Fact]
    public async Task Archived_category_keeps_history_and_blocks_new_items_until_restored()
    {
        await using var s = await TestScope.Create();
        var category = (await s.Alice.GetCategoriesAsync())[0];
        var id = await s.Alice.SaveActivityAsync(Activity(category.Id));
        await s.Alice.ArchiveCategoryAsync(category.Id, true);
        Assert.DoesNotContain(await s.Alice.GetCategoriesAsync(), x => x.Id == category.Id);
        Assert.Equal(id, (await s.Alice.GetItemAsync(id)).Id);
        await Assert.ThrowsAsync<PlannerException>(() => s.Alice.SaveActivityAsync(Activity(category.Id)));
        await s.Alice.ArchiveCategoryAsync(category.Id, false);
        await s.Alice.SaveActivityAsync(Activity(category.Id));
    }

    [Theory]
    [InlineData(0)] [InlineData(1441)] [InlineData(-10)]
    public async Task Service_enforces_duration_independently_of_UI(int minutes)
    {
        await using var s = await TestScope.Create();
        var input = Activity((await s.Alice.GetCategoriesAsync())[0].Id); input.DurationMinutes = minutes;
        await Assert.ThrowsAsync<PlannerException>(() => s.Alice.SaveActivityAsync(input));
    }

    [Fact]
    public async Task Service_rejects_invalid_colour_and_duplicate_category_names()
    {
        await using var s = await TestScope.Create();
        await s.Alice.GetCategoriesAsync();
        await Assert.ThrowsAsync<PlannerException>(() => s.Alice.SaveCategoryAsync(new() { Name = "Bad", Colour = "red;background:url(x)" }));
        await Assert.ThrowsAsync<PlannerException>(() => s.Alice.SaveCategoryAsync(new() { Name = " studies " }));
    }

    [Fact]
    public async Task Weekly_repetition_keeps_local_time_across_daylight_saving()
    {
        await using var s = await TestScope.Create();
        var input = Activity((await s.Alice.GetCategoriesAsync())[0].Id);
        input.StartLocal = new(2026, 10, 18, 18, 0, 0); input.Repeat = RepeatPattern.Weekly; input.Occurrences = 3;
        await s.Alice.SaveActivityAsync(input);
        var items = await s.Alice.GetItemsAsync(new(2026, 10, 1), new(2026, 11, 10));
        Assert.Equal(3, items.Count);
        Assert.Equal(17, items[0].StartUtc.Hour);
        Assert.Equal(18, items[1].StartUtc.Hour);
        Assert.All(items, x => Assert.Equal(18, PlannerClock.Local(x.StartUtc, "Europe/Dublin").Hour));
        Assert.Single(items.Select(x => x.SeriesId).Distinct());
    }

    [Fact]
    public async Task Invalid_later_occurrence_rolls_back_the_whole_series()
    {
        await using var s = await TestScope.Create();
        var input = Activity((await s.Alice.GetCategoriesAsync())[0].Id);
        input.StartLocal = new(2026, 10, 18, 1, 30, 0); input.Repeat = RepeatPattern.Weekly; input.Occurrences = 3;
        await Assert.ThrowsAsync<PlannerException>(() => s.Alice.SaveActivityAsync(input));
        Assert.Empty(await s.Alice.GetItemsAsync(new(2026, 10, 1), new(2026, 11, 10)));
    }

    [Fact]
    public async Task Repetition_is_bounded_and_one_occurrence_can_be_completed_separately()
    {
        await using var s = await TestScope.Create();
        var input = Activity((await s.Alice.GetCategoriesAsync())[0].Id);
        input.Repeat = RepeatPattern.Daily; input.Occurrences = 13;
        await Assert.ThrowsAsync<PlannerException>(() => s.Alice.SaveActivityAsync(input));
        input.Occurrences = 3;
        var id = await s.Alice.SaveActivityAsync(input);
        await s.Alice.SetStatusAsync(id, ActivityStatus.Completed);
        var items = await s.Alice.GetItemsAsync(new(2026, 10, 1), new(2026, 11, 1));
        Assert.Single(items, x => x.Status == ActivityStatus.Completed);
        Assert.Equal(2, items.Count(x => x.Status == ActivityStatus.Planned));
    }

    [Fact]
    public async Task Account_activity_quota_counts_the_entire_new_series()
    {
        await using var s = await TestScope.Create();
        var category = (await s.Alice.GetCategoriesAsync())[0];
        await using (var db = s.Factory.CreateDbContext())
        {
            db.PlannerItems.AddRange(Enumerable.Range(0, PlannerService.MaximumActivities - 1).Select(_ => new PlannerItem
            { UserId = "alice", CategoryId = category.Id, Title = "Existing", StartUtc = new(2026, 10, 1), EndUtc = new(2026, 10, 1, 1, 0, 0) }));
            await db.SaveChangesAsync();
        }
        var input = Activity(category.Id); input.Repeat = RepeatPattern.Daily; input.Occurrences = 2;
        await Assert.ThrowsAsync<PlannerException>(() => s.Alice.SaveActivityAsync(input));
        await using var check = s.Factory.CreateDbContext();
        Assert.Equal(PlannerService.MaximumActivities - 1, await check.PlannerItems.CountAsync());
    }

    [Fact]
    public async Task Category_quota_cannot_be_bypassed_by_archiving()
    {
        await using var s = await TestScope.Create();
        await using (var db = s.Factory.CreateDbContext())
        {
            db.Categories.AddRange(Enumerable.Range(0, PlannerService.MaximumCategories).Select(i => new Category
            { UserId = "alice", Name = $"Cat {i}", NormalizedName = $"CAT {i}", IsArchived = true }));
            await db.SaveChangesAsync();
        }
        await Assert.ThrowsAsync<PlannerException>(() => s.Alice.SaveCategoryAsync(new() { Name = "One more" }));
    }

    [Fact]
    public async Task Unverified_or_deleted_account_cannot_use_planner_services()
    {
        await using var s = await TestScope.Create();
        await using var db = s.Factory.CreateDbContext();
        var user = await db.Users.SingleAsync(x => x.Id == "alice");
        user.EmailConfirmed = false; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => s.Alice.GetCategoriesAsync());
        db.Users.Remove(user); await db.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => s.Alice.GetProfileAsync());
    }

    [Fact]
    public async Task Oversized_date_queries_are_rejected()
    {
        await using var s = await TestScope.Create();
        await Assert.ThrowsAsync<PlannerException>(() => s.Alice.GetItemsAsync(new(2026, 1, 1), new(2027, 1, 1)));
    }

    [Fact]
    public async Task Normalized_email_uniqueness_is_enforced_by_the_database()
    {
        await using var s = await TestScope.Create();
        await using var db = s.Factory.CreateDbContext();
        db.Users.Add(new() { Id = "duplicate", Email = "ALICE@example.test", NormalizedEmail = "ALICE@EXAMPLE.TEST" });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    private sealed class TestScope(SqliteConnection connection, TestFactory factory, AbuseGuard guard) : IAsyncDisposable
    {
        public TestFactory Factory => factory;
        public PlannerService Alice { get; } = new(factory, new User("alice"), guard);
        public PlannerService Bob { get; } = new(factory, new User("bob"), guard);
        public static async Task<TestScope> Create()
        {
            var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True"); await connection.OpenAsync();
            var factory = new TestFactory(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
            await using var db = factory.CreateDbContext(); await db.Database.EnsureCreatedAsync();
            db.Users.AddRange(new ApplicationUser { Id = "alice", Email = "alice@example.test", NormalizedEmail = "ALICE@EXAMPLE.TEST", EmailConfirmed = true },
                new ApplicationUser { Id = "bob", Email = "bob@example.test", NormalizedEmail = "BOB@EXAMPLE.TEST", EmailConfirmed = true });
            await db.SaveChangesAsync();
            return new(connection, factory, new AbuseGuard(TimeProvider.System));
        }
        public async ValueTask DisposeAsync() { guard.Dispose(); await connection.DisposeAsync(); }
    }
    private sealed class User(string id) : ICurrentUser { public Task<string> GetIdAsync() => Task.FromResult(id); }
    private sealed class TestFactory(DbContextOptions<ApplicationDbContext> options) : IDbContextFactory<ApplicationDbContext>
    { public ApplicationDbContext CreateDbContext() => new(options); }
}
