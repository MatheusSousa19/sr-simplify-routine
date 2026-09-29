using Microsoft.Extensions.Caching.Memory;

namespace SrSimplifyRoutine.Web.Services;

// Bounded, process-local counters. Multi-instance deployments need shared limits at the gateway.
public sealed class AbuseGuard(TimeProvider time) : IDisposable
{
    private readonly MemoryCache cache = new(new MemoryCacheOptions { SizeLimit = 10000 });
    private readonly object sync = new();
    private sealed class Counter { public int Value; }

    public bool Allow(string key, int limit, TimeSpan window)
    {
        lock (sync)
        {
            // Use TimeProvider in the key so expiry is deterministic in tests as well.
            var period = time.GetUtcNow().Ticks / window.Ticks;
            var cacheKey = $"{key}:{period}";
            if (!cache.TryGetValue(cacheKey, out Counter? counter))
            {
                counter = new Counter();
                cache.Set(cacheKey, counter, new MemoryCacheEntryOptions().SetSize(1).SetAbsoluteExpiration(window));
            }
            return ++counter!.Value <= limit;
        }
    }

    public void Require(string userId, bool write)
    {
        if (!Allow("planner:global", 3000, TimeSpan.FromMinutes(1)) ||
            !Allow($"planner:{(write ? "write" : "read")}:{userId}", write ? 40 : 180, TimeSpan.FromMinutes(1)))
            throw new PlannerException("You're making changes too quickly. Please wait a minute and try again.");
    }

    public void Dispose() => cache.Dispose();
}
