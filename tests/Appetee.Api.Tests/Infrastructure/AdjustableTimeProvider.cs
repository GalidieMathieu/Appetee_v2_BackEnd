/*
 * Purpose: Supplies deterministic UTC time so session lifetimes can be tested without real-time delays.
 * Created: 2026-08-21T11:13:45-06:00
 * Last updated: 2026-08-21T11:39:25-06:00
 */

namespace Appetee.Api.Tests.Infrastructure;

internal sealed class AdjustableTimeProvider(DateTimeOffset initialUtc) : TimeProvider
{
    private DateTimeOffset _utcNow = initialUtc;

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public void Advance(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration));
        }

        _utcNow = _utcNow.Add(duration);
    }
}
