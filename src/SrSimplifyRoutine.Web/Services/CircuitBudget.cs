using Microsoft.AspNetCore.Components.Server.Circuits;

namespace SrSimplifyRoutine.Web.Services;

public sealed class CircuitBudget
{
    private int active;
    public bool TryAcquire()
    {
        if (Interlocked.Increment(ref active) <= 200) return true;
        Interlocked.Decrement(ref active);
        return false;
    }
    public void Release() => Interlocked.Decrement(ref active);
}

public sealed class BudgetCircuitHandler(CircuitBudget budget) : CircuitHandler, IDisposable
{
    private int acquired;
    public override Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        if (!budget.TryAcquire()) throw new InvalidOperationException("The planner is busy. Please try again shortly.");
        Interlocked.Exchange(ref acquired, 1);
        return Task.CompletedTask;
    }
    public override Task OnCircuitClosedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        Dispose();
        return Task.CompletedTask;
    }
    public void Dispose() { if (Interlocked.Exchange(ref acquired, 0) == 1) budget.Release(); }
}
