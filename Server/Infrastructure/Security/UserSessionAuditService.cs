using System.Security.Claims;

namespace Infrastructure.Security;

/// <summary>
/// Writes explicit session lifecycle events to the security audit stream.
/// This intentionally does not infer a logout when a browser disappears or a cookie expires.
/// </summary>
public sealed class UserSessionAuditService
{
    private readonly Data.DatabaseContext _databaseContext;
    private readonly TimeProvider _timeProvider;

    public UserSessionAuditService(Data.DatabaseContext databaseContext, TimeProvider timeProvider)
    {
        _databaseContext = databaseContext;
        _timeProvider = timeProvider;
    }

    public async Task RecordLogoutAsync(
        ClaimsPrincipal principal,
        string? ipAddress,
        string? correlationId,
        CancellationToken cancellationToken = default)
    {
        var userId = Infrastructure.Tenancy.CompanyAccessService.GetUserId(principal);
        if (userId is null)
        {
            return;
        }

        _databaseContext.SecurityAuditEvents.Add(new Domain.SecurityAuditEvent
        {
            UserId = userId.Value,
            EventType = "LogoutSucceeded",
            EntityType = "User",
            EntityId = userId.Value.ToString(),
            OccurredAtUtc = _timeProvider.GetUtcNow().UtcDateTime,
            CorrelationId = correlationId,
            IpAddress = ipAddress,
            DetailsJson = "{\"source\":\"ApiLogout\"}",
        });

        await _databaseContext.SaveChangesAsync(cancellationToken);
    }
}
