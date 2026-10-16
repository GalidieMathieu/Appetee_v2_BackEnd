namespace Appetee.Application.Abstractions.Catalogue;

public sealed record AccountSecurityState(bool IsActive, long SecurityVersion);

public interface ICatalogueAuthorizationQueries
{
    Task<AccountSecurityState?> GetAccountSecurityAsync(int userId, CancellationToken ct);
    Task<bool> HasPermissionAsync(int userId, long securityVersion, string permission, CancellationToken ct);
    /// <summary>Null for a missing, disabled or stale account; empty for an ordinary active account.</summary>
    Task<IReadOnlyList<string>?> GetPermissionsAsync(int userId, long securityVersion, CancellationToken ct);
}

/// <summary>Server-derived request identity; never bound from catalogue form data.</summary>
public interface ICatalogueActorContext
{
    int UserId { get; }
    long SecurityVersion { get; }
    long? ExpectedResourceVersion { get; }
    string CorrelationId { get; }
}
