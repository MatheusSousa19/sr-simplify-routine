using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace SrSimplifyRoutine.Web.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<PlannerItem> PlannerItems => Set<PlannerItem>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<ApplicationUser>().HasIndex(x => x.NormalizedEmail).IsUnique();
        builder.Entity<Category>().HasIndex(x => new { x.UserId, x.NormalizedName }).IsUnique();
        builder.Entity<Category>().HasAlternateKey(x => new { x.Id, x.UserId });
        builder.Entity<Category>().HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        // The composite foreign key enforces category ownership at the database boundary too.
        builder.Entity<PlannerItem>().HasOne(x => x.Category).WithMany()
            .HasForeignKey(x => new { x.CategoryId, x.UserId })
            .HasPrincipalKey(x => new { x.Id, x.UserId }).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<PlannerItem>().HasIndex(x => new { x.UserId, x.StartUtc });
        builder.Entity<PlannerItem>().Ignore(x => x.Minutes);
    }
}
