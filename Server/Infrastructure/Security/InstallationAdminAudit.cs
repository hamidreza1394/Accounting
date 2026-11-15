using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Security;

/// <summary>One changed field: previous and new value (never a secret such as a password hash).</summary>
public sealed record AuditFieldChange(string Field, string? Previous, string? Current);

/// <summary>
/// D-088: access and changes made by the installation administrator are written to the existing
/// <c>SecurityAuditEvents</c> table (no new table). Reads write one event per request and fail closed:
/// if the event cannot be saved, the data is not returned. Changes add their event to the same
/// <c>SaveChanges</c> as the change itself, so a change without its audit record cannot be committed.
/// </summary>
public static class InstallationAdminAudit
{
	public const string DataViewed = "InstallationAdminDataViewed";
	public const string DataSearched = "InstallationAdminDataSearched";
	public const string UserDeleted = "UserDeleted";
	public const string ReportViewed = "InstallationAdminActivityReportViewed";

	public const int MaxReasonLength = 500;
	public const int MaxDetailsLength = 4000;
	private const int MaxValueLength = 200;

	/// <summary>Event types listed by the independent installation-administrator activity report.</summary>
	public static readonly string[] ReportEventTypes =
	[
		DataViewed, DataSearched, ReportViewed,
		"UserCreated", "UserUpdated", UserDeleted,
		"AdminUserSessionRevoked", "AdminUserSessionsRevoked",
		"SecurityAuditExported",
	];

	/// <summary>The acting user id; a request without one cannot reach an administrator page.</summary>
	public static Guid GetActorId(ClaimsPrincipal principal) =>
		Infrastructure.Tenancy.CompanyAccessService.GetUserId(principal)
		?? throw new InvalidOperationException("The administrator identity is missing.");

	public static string? ClipReason(string? reason)
	{
		var text = reason?.Trim();
		if (string.IsNullOrEmpty(text))
		{
			return null;
		}

		return text.Length <= MaxReasonLength ? text : text[..MaxReasonLength];
	}

	/// <summary>Returns only fields whose value actually changed.</summary>
	public static List<AuditFieldChange> Diff(params (string Field, string? Previous, string? Current)[] fields) =>
		fields
			.Where(field => !string.Equals(field.Previous, field.Current, StringComparison.Ordinal))
			.Select(field => new AuditFieldChange(field.Field, Clip(field.Previous), Clip(field.Current)))
			.ToList();

	public static Domain.SecurityAuditEvent CreateEvent(
		Guid actorId,
		string eventType,
		string? entityType,
		string? entityId,
		object details,
		string? ipAddress,
		Guid? companyId = null) =>
		new()
		{
			UserId = actorId,
			TenantId = companyId,
			EventType = eventType,
			EntityType = entityType,
			EntityId = entityId,
			OccurredAtUtc = TimeProvider.System.GetUtcNow().UtcDateTime,
			CorrelationId = System.Diagnostics.Activity.Current?.Id,
			IpAddress = ipAddress,
			DetailsJson = Serialize(details),
		};

	/// <summary>Serializes details; an oversized payload keeps its summary keys and drops the field list.</summary>
	public static string Serialize(object details)
	{
		var json = JsonSerializer.Serialize(details);
		if (json.Length <= MaxDetailsLength)
		{
			return json;
		}

		return JsonSerializer.Serialize(new { truncated = true, originalLength = json.Length });
	}

	/// <summary>Company ids of the user's active memberships (the customer's companies), capped.</summary>
	public static async Task<Guid[]> GetCompanyIdsAsync(
		Data.DatabaseContext database, Guid userId, CancellationToken cancellationToken) =>
		await database.UserCompanyMemberships.AsNoTracking()
			.Where(membership => membership.UserId == userId && membership.IsActive)
			.OrderBy(membership => membership.TenantId)
			.Select(membership => membership.TenantId)
			.Take(20)
			.ToArrayAsync(cancellationToken);

	/// <summary>
	/// Records a view or search of customer data by the installation administrator and saves it now.
	/// Called after authorization and validation, before the data is returned (fail closed).
	/// </summary>
	public static async Task RecordAccessAsync(
		Data.DatabaseContext database,
		ClaimsPrincipal principal,
		string? ipAddress,
		string resource,
		bool isSearch,
		object? filters,
		int? resultCount,
		string? entityId,
		CancellationToken cancellationToken,
		Guid? companyId = null)
	{
		var actorId = Infrastructure.Tenancy.CompanyAccessService.GetUserId(principal);
		if (actorId is null)
		{
			return;
		}

		database.SecurityAuditEvents.Add(CreateEvent(
			actorId.Value,
			isSearch ? DataSearched : DataViewed,
			resource,
			entityId,
			new { resource, filters, resultCount },
			ipAddress,
			companyId));
		await database.SaveChangesAsync(cancellationToken);
	}

	private static string? Clip(string? value) =>
		value is null || value.Length <= MaxValueLength ? value : value[..MaxValueLength];
}
