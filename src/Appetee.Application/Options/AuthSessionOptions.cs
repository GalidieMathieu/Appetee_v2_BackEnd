namespace Appetee.Application.Options;

public sealed class AuthSessionOptions
{
    public int SessionLifetimeDays { get; init; } = 30;
}
