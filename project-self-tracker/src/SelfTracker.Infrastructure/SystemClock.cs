using SelfTracker.Application.Abstractions;

namespace SelfTracker.Infrastructure;

public sealed class SystemClock(TimeProvider time) : IClock
{
    public DateTimeOffset UtcNow => time.GetUtcNow();
}
