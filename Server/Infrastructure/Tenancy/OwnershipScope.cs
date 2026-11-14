namespace Infrastructure.Tenancy;

/// <summary>
/// The Tenant boundary of a request (D-087): the owner and isolation boundary of a customer's data.
/// It is a type of its own so that it can not be confused with a <see cref="CompanyScope" />, a
/// <c>CustomerBoundaryId</c> (D-038) or a plain <see cref="Guid" />: there is no conversion in either
/// direction. Only the server creates it, through <see cref="OwnershipScope" />.
/// </summary>
public readonly record struct TenantScope
{
	internal TenantScope(Guid value)
	{
		Value = value == Guid.Empty ? throw new ArgumentException("A tenant scope needs an identifier.", nameof(value)) : value;
	}

	public Guid Value { get; }
}

/// <summary>
/// The Company of a request (D-087): the legal or operating company inside one tenant. Independent
/// of <see cref="TenantScope" />; a company filter never replaces the tenant filter.
/// </summary>
public readonly record struct CompanyScope
{
	internal CompanyScope(Guid value)
	{
		Value = value == Guid.Empty ? throw new ArgumentException("A company scope needs an identifier.", nameof(value)) : value;
	}

	public Guid Value { get; }
}

/// <summary>
/// The server-resolved ownership of a request: exactly one tenant and one company of that tenant.
/// </summary>
/// <remarks>
/// <para>
/// <b>Where it comes from.</b> Only from a <see cref="CompanyAccess" /> (an active membership that
/// <see cref="CompanyAccessService" /> read from the database) or from the <see cref="WorkContext" />
/// that wraps one. There is deliberately no factory that takes a <see cref="Guid" />, a string or a
/// request: an identifier from a client is only a request and never an ownership (D-013, D-087).
/// </para>
/// <para>
/// <b>Legacy state (D-087, <c>NotImplemented</c>).</b> The model has no tenant entity yet and the
/// persisted <c>TenantId</c> columns hold the identifier of the company. <see cref="FromLegacyCompany" />
/// is the ONE place that states this equation, so that the transition (S3-0h) replaces a single method
/// instead of every query. Until then <see cref="Tenant" /> and <see cref="Company" /> carry the same
/// value, which says nothing about the target model (one tenant, several companies).
/// </para>
/// <para>
/// Not a query filter: nothing here is applied to queries automatically (no global query filter
/// before the decision of D-087, section on query filters). A query uses <see cref="Tenant" /> and
/// <see cref="Company" /> explicitly and independently.
/// </para>
/// </remarks>
public sealed record OwnershipScope
{
	private OwnershipScope(TenantScope tenant, CompanyScope company)
	{
		Tenant = tenant;
		Company = company;
	}

	public TenantScope Tenant { get; }

	public CompanyScope Company { get; }

	/// <summary>The ownership of a verified company access under the legacy rule "tenant id = company id".</summary>
	public static OwnershipScope FromLegacyCompany(CompanyAccess access)
	{
		ArgumentNullException.ThrowIfNull(access);

		// the single place of the legacy equation (see the remarks of this type)
		return new OwnershipScope(new TenantScope(access.CompanyId), new CompanyScope(access.CompanyId));
	}

	/// <summary>The ownership of a verified working context (company and fiscal period).</summary>
	public static OwnershipScope FromLegacyCompany(WorkContext context)
	{
		ArgumentNullException.ThrowIfNull(context);
		return FromLegacyCompany(context.Company);
	}
}
