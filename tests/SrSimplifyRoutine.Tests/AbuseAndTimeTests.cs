using SrSimplifyRoutine.Web.Services;

namespace SrSimplifyRoutine.Tests;

public class AbuseAndTimeTests
{
    [Fact]
    public void Rate_limit_rejects_excess_requests_and_recovers_next_window()
    {
        var clock = new Clock(); using var guard = new AbuseGuard(clock);
        Assert.True(guard.Allow("signup", 2, TimeSpan.FromMinutes(1)));
        Assert.True(guard.Allow("signup", 2, TimeSpan.FromMinutes(1)));
        Assert.False(guard.Allow("signup", 2, TimeSpan.FromMinutes(1)));
        Assert.True(guard.Allow("another-source", 2, TimeSpan.FromMinutes(1)));
        clock.Now = clock.Now.AddMinutes(1);
        Assert.True(guard.Allow("signup", 2, TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public void Repeated_interactive_writes_are_throttled_without_blocking_another_user()
    {
        using var guard = new AbuseGuard(TimeProvider.System);
        for (var i = 0; i < 40; i++) guard.Require("alice", true);
        Assert.Throws<PlannerException>(() => guard.Require("alice", true));
        guard.Require("bob", true);
    }

    [Theory]
    [InlineData(2026, 3, 29, 1, 30)]
    [InlineData(2026, 10, 25, 1, 30)]
    public void Clock_change_gaps_and_ambiguities_are_not_silently_scheduled(int y, int m, int d, int h, int min)
    { Assert.Throws<PlannerException>(() => PlannerClock.ToUtc(new(y, m, d, h, min, 0), "Europe/Dublin")); }

    [Fact]
    public void Unknown_timezone_is_rejected() => Assert.Throws<PlannerException>(() => PlannerClock.Zone("invented/zone"));

    [Fact]
    public void Active_circuit_budget_is_bounded_and_reusable()
    {
        var budget = new CircuitBudget();
        for (var i = 0; i < 200; i++) Assert.True(budget.TryAcquire());
        Assert.False(budget.TryAcquire());
        budget.Release();
        Assert.True(budget.TryAcquire());
    }

    private sealed class Clock : TimeProvider
    { public DateTimeOffset Now { get; set; } = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero); public override DateTimeOffset GetUtcNow() => Now; }
}
