using GameBug.Application.Abstractions.Time;

namespace GameBug.Infrastructure.Time;

public class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
