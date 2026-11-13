using System.Globalization;
using Faranou.Modules.Trade.Application.Authorization;
using Faranou.Modules.Trade.Application.ReferenceData;
using Faranou.Modules.Trade.Domain.Parties;

namespace Faranou.Modules.Trade.Application.Parties;

/// <summary>
/// Party use cases: validation, role diff and mapping. The tenant and user come only from the
/// server-validated <see cref="TradeAccessContext" /> passed by the caller, never from a request body.
/// Validation messages never echo the submitted value.
/// </summary>
public sealed class PartyService(IPartyStore store, TimeProvider timeProvider)
{
    public const int MaxSearchLength = 100;
    public const int MaxPage = 100_000;
    public const int MaxPageSize = 100;
    public const int DefaultPageSize = 20;
    public const int RowVersionLength = 8;

    private const int CodeMax = 64;
    private const int NameMax = 200;
    private const int IdentifierMax = 64;

    public async Task<PartyResult<PartyListResponse>> ListAsync(
        TradeAccessContext context,
        PartyListQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(query);

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var search = query.Search?.Trim();
        if (string.IsNullOrEmpty(search))
        {
            search = null;
        }
        else if (search.Length > MaxSearchLength)
        {
            errors["search"] = ["عبارت جست‌وجو بیش از حد طولانی است."];
        }

        PartyRole? role = null;
        if (!string.IsNullOrWhiteSpace(query.Role))
        {
            if (TryParseRole(query.Role, out var parsed))
            {
                role = parsed;
            }
            else
            {
                errors["role"] = ["نقش نامعتبر است."];
            }
        }

        if (errors.Count > 0)
        {
            return new PartyResult<PartyListResponse>(PartyOutcome.ValidationFailed, Errors: errors);
        }

        var page = Math.Clamp(query.Page, 1, MaxPage);
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);
        var result = await store.ListAsync(
            context.TenantId,
            new PartyListFilter(search, role, query.IsActive, (page - 1) * pageSize, pageSize),
            cancellationToken);

        var items = result.Items.Select(ToListItem).ToArray();
        return new PartyResult<PartyListResponse>(
            PartyOutcome.Success,
            new PartyListResponse(items, page, pageSize, result.TotalCount));
    }

    public async Task<PartyResult<PartyDetail>> GetAsync(
        TradeAccessContext context,
        Guid partyId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var party = await store.FindAsync(context.TenantId, partyId, cancellationToken);
        return party is null
            ? new PartyResult<PartyDetail>(PartyOutcome.NotFound)
            : new PartyResult<PartyDetail>(PartyOutcome.Success, ToDetail(party));
    }

    public async Task<PartyResult<PartyDetail>> CreateAsync(
        TradeAccessContext context,
        CreatePartyRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);

        var validated = Validate(request.Code, request.LegalName, request.DisplayName, request.LegalIdentifier,
            request.CountryCode, request.Roles, rowVersion: null, requireRowVersion: false, out var errors);
        if (validated is null)
        {
            return new PartyResult<PartyDetail>(PartyOutcome.ValidationFailed, Errors: errors);
        }

        if (await store.ActiveCodeExistsAsync(context.TenantId, validated.Code, null, cancellationToken))
        {
            return new PartyResult<PartyDetail>(PartyOutcome.DuplicateCode);
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var party = new Party(
            context.TenantId,
            validated.Code,
            validated.LegalName,
            context.UserId,
            now,
            validated.LegalIdentifier,
            validated.CountryCode,
            validated.DisplayName);
        foreach (var role in validated.Roles)
        {
            party.AddRole(role, context.UserId, now);
        }

        return ToResult(party, await store.AddAsync(party, cancellationToken));
    }

    public async Task<PartyResult<PartyDetail>> UpdateAsync(
        TradeAccessContext context,
        Guid partyId,
        UpdatePartyRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);

        var validated = Validate(request.Code, request.LegalName, request.DisplayName, request.LegalIdentifier,
            request.CountryCode, request.Roles, request.RowVersion, requireRowVersion: true, out var errors);
        if (validated is null)
        {
            return new PartyResult<PartyDetail>(PartyOutcome.ValidationFailed, Errors: errors);
        }

        var party = await store.FindForUpdateAsync(context.TenantId, partyId, cancellationToken);
        if (party is null)
        {
            return new PartyResult<PartyDetail>(PartyOutcome.NotFound);
        }

        if (!string.Equals(party.Code, validated.Code, StringComparison.OrdinalIgnoreCase) &&
            await store.ActiveCodeExistsAsync(context.TenantId, validated.Code, party.Id, cancellationToken))
        {
            return new PartyResult<PartyDetail>(PartyOutcome.DuplicateCode);
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;

        // Relies on UpdatedAtUtc changing (it is stamped from the clock on every call), so even a no-op
        // update modifies the row and therefore compares the row version.
        party.UpdateIdentity(
            validated.Code,
            validated.LegalName,
            validated.LegalIdentifier,
            validated.CountryCode,
            validated.DisplayName,
            context.UserId,
            now);

        // The roles are a full replacement applied as a diff: unchanged roles keep their row,
        // never delete-all and re-insert (unique index on the active role per party).
        var current = party.Roles.Where(role => !role.IsDeleted).Select(role => role.Role).ToHashSet();
        foreach (var removed in current.Except(validated.Roles))
        {
            party.RemoveRole(removed, context.UserId, now);
        }

        foreach (var added in validated.Roles.Except(current))
        {
            party.AddRole(added, context.UserId, now);
        }

        return ToResult(party, await store.SaveChangesAsync(party, validated.RowVersion!, cancellationToken));
    }

    private static PartyResult<PartyDetail> ToResult(Party party, PartySaveOutcome outcome) => outcome switch
    {
        PartySaveOutcome.Saved => new PartyResult<PartyDetail>(PartyOutcome.Success, ToDetail(party)),
        PartySaveOutcome.DuplicateCode => new PartyResult<PartyDetail>(PartyOutcome.DuplicateCode),
        _ => new PartyResult<PartyDetail>(PartyOutcome.ConcurrencyConflict),
    };

    private sealed record ValidatedParty(
        string Code,
        string LegalName,
        string? DisplayName,
        string? LegalIdentifier,
        string? CountryCode,
        IReadOnlyList<PartyRole> Roles,
        byte[]? RowVersion);

    private static ValidatedParty? Validate(
        string? code,
        string? legalName,
        string? displayName,
        string? legalIdentifier,
        string? countryCode,
        IReadOnlyList<string?>? roles,
        string? rowVersion,
        bool requireRowVersion,
        out Dictionary<string, string[]> errors)
    {
        errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        var normalizedCode = code?.Trim();
        if (string.IsNullOrEmpty(normalizedCode))
        {
            errors["code"] = ["کد الزامی است."];
        }
        else if (HasForbiddenCharacters(normalizedCode))
        {
            errors["code"] = [ForbiddenCharactersMessage];
        }
        else if (normalizedCode.Length > CodeMax)
        {
            errors["code"] = [$"کد نباید بیش از {CodeMax} نویسه باشد."];
        }

        var normalizedLegalName = legalName?.Trim();
        if (string.IsNullOrEmpty(normalizedLegalName))
        {
            errors["legalName"] = ["نام حقوقی الزامی است."];
        }
        else if (HasForbiddenCharacters(normalizedLegalName))
        {
            errors["legalName"] = [ForbiddenCharactersMessage];
        }
        else if (normalizedLegalName.Length > NameMax)
        {
            errors["legalName"] = [$"نام حقوقی نباید بیش از {NameMax} نویسه باشد."];
        }

        var normalizedDisplayName = Optional(displayName);
        if (normalizedDisplayName is not null && HasForbiddenCharacters(normalizedDisplayName))
        {
            errors["displayName"] = [ForbiddenCharactersMessage];
        }
        else if (normalizedDisplayName?.Length > NameMax)
        {
            errors["displayName"] = [$"نام نمایشی نباید بیش از {NameMax} نویسه باشد."];
        }

        var normalizedIdentifier = Optional(legalIdentifier);
        if (normalizedIdentifier is not null && HasForbiddenCharacters(normalizedIdentifier))
        {
            errors["legalIdentifier"] = [ForbiddenCharactersMessage];
        }
        else if (normalizedIdentifier?.Length > IdentifierMax)
        {
            errors["legalIdentifier"] = [$"شناسه حقوقی نباید بیش از {IdentifierMax} نویسه باشد."];
        }

        string? normalizedCountry = null;
        var country = Optional(countryCode);
        if (country is not null)
        {
            if (country.Length != 2 || !country.All(char.IsAsciiLetter) ||
                !TradeReferenceData.TryFindCountry(country.ToUpperInvariant(), out _))
            {
                errors["countryCode"] = ["کد کشور نامعتبر است."];
            }
            else
            {
                normalizedCountry = country.ToUpperInvariant();
            }
        }

        var parsedRoles = new List<PartyRole>();
        var maxRoles = Enum.GetValues<PartyRole>().Length;
        if (roles is null || roles.Count < 1 || roles.Count > maxRoles)
        {
            errors["roles"] = [string.Create(CultureInfo.InvariantCulture, $"تعداد نقش‌ها باید بین ۱ تا {maxRoles} باشد.")];
        }
        else
        {
            foreach (var name in roles)
            {
                if (name is null || !TryParseRole(name, out var role))
                {
                    errors["roles"] = ["نقش نامعتبر است."];
                    break;
                }

                if (parsedRoles.Contains(role))
                {
                    errors["roles"] = ["نقش‌ها نباید تکراری باشند."];
                    break;
                }

                parsedRoles.Add(role);
            }
        }

        byte[]? token = null;
        if (requireRowVersion && !TryParseRowVersion(rowVersion, out token))
        {
            errors["rowVersion"] = ["نسخه ردیف الزامی و باید معتبر باشد."];
        }

        return errors.Count > 0
            ? null
            : new ValidatedParty(normalizedCode!, normalizedLegalName!, normalizedDisplayName, normalizedIdentifier,
                normalizedCountry, parsedRoles, token);
    }

    private const string ForbiddenCharactersMessage = "مقدار شامل نویسه‌های کنترلی یا جهت‌دهی متن غیرمجاز است.";

    /// <summary>
    /// Control characters and Unicode bidi overrides/isolates/marks are rejected: they can disguise text in
    /// lists and exports. ZWNJ (U+200C), needed by Persian text, stays allowed. No code allowlist by design.
    /// </summary>
    private static bool HasForbiddenCharacters(string value) => value.Any(character =>
        char.IsControl(character) ||
        character is '\u200E' or '\u200F' or (>= '\u202A' and <= '\u202E') or (>= '\u2066' and <= '\u2069'));

    private static string? Optional(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    /// <summary>Exact member names only: numeric strings, different casing and combinations are rejected.</summary>
    private static bool TryParseRole(string value, out PartyRole role) =>
        Enum.TryParse(value, ignoreCase: false, out role) &&
        Enum.IsDefined(role) &&
        string.Equals(Enum.GetName(role), value, StringComparison.Ordinal);

    private static bool TryParseRowVersion(string? value, out byte[]? token)
    {
        token = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var buffer = new byte[RowVersionLength + 4];
        if (!Convert.TryFromBase64String(value, buffer, out var written) || written != RowVersionLength)
        {
            return false;
        }

        token = buffer[..written];
        return true;
    }

    private static string[] ActiveRoleNames(Party party) => party.Roles
        .Where(role => !role.IsDeleted)
        .Select(role => role.Role.ToString())
        .Order(StringComparer.Ordinal)
        .ToArray();

    private static PartyListItem ToListItem(Party party) => new(
        party.Id,
        party.Code,
        party.LegalName,
        party.DisplayName,
        party.CountryCode,
        party.IsActive,
        ActiveRoleNames(party),
        Convert.ToBase64String(party.RowVersion));

    private static PartyDetail ToDetail(Party party) => new(
        party.Id,
        party.Code,
        party.LegalName,
        party.DisplayName,
        party.LegalIdentifier,
        party.CountryCode,
        party.IsActive,
        ActiveRoleNames(party),
        Convert.ToBase64String(party.RowVersion),
        DateTime.SpecifyKind(party.CreatedAtUtc, DateTimeKind.Utc),
        party.UpdatedAtUtc is null ? null : DateTime.SpecifyKind(party.UpdatedAtUtc.Value, DateTimeKind.Utc));
}
