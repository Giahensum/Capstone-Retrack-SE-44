namespace Retrack.API.Tests;
internal sealed class DispatchTestClock(DateTime value) : TimeProvider
{
    public override DateTimeOffset GetUtcNow()=>new(DateTime.SpecifyKind(value,DateTimeKind.Utc));
}
