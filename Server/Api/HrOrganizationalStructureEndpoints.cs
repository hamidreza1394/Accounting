using System.Data;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Domain;

namespace Server.Api;

/// <summary>
/// The company-scoped organizational structure of S2 (D-072): employees, organizational
/// units, positions and assignments. Everything here is human-resources data; nothing
/// grants a permission, a data scope or a company role (D-064). Every endpoint accepts
/// the two actor kinds of D-071 — the installation administrator (re-read from the
/// database on every request) and a company manager with an active membership plus the
/// matching <c>hr.*</c> permission code — and every write runs inside a ReadCommitted
/// transaction with fresh rows and rowversion guards, so a concurrent change fails
/// closed instead of being silently overwritten.
/// </summary>
/// <remarks>
/// <para>
/// The delegation ceiling of D-071 stays intact: these endpoints never grant permissions,
/// so the only path that hands <c>hr.*</c> codes to a role is the ceiling-guarded role
/// administration. A manager reference (<c>ManagerEmployeeId</c>) is reporting-line
/// bookkeeping and never an authorization input.
/// </para>
/// </remarks>
public static class HrOrganizationalStructureEndpoints
{
    public const string ListEmployeesPermission = "hr.employees.list";
    public const string ViewEmployeesPermission = "hr.employees.view";
    public const string CreateEmployeesPermission = "hr.employees.create";
    public const string UpdateEmployeesPermission = "hr.employees.update";
    public const string ManageEmployeeUserLinkPermission = "hr.employees.user-link.manage";
    public const string ListUnitsPermission = "hr.organizational-units.list";
    public const string CreateUnitsPermission = "hr.organizational-units.create";
    public const string UpdateUnitsPermission = "hr.organizational-units.update";
    public const string ListPositionsPermission = "hr.positions.list";
    public const string CreatePositionsPermission = "hr.positions.create";
    public const string UpdatePositionsPermission = "hr.positions.update";
    public const string ManageAssignmentsPermission = "hr.assignments.manage";

    /// <summary>Largest accepted page number; keeps (page - 1) * pageSize inside int range.</summary>
    private const int MaxPage = 100_000;
    private const int MaxPageSize = 100;
    private const int MaxSearchLength = 120;

    private static readonly System.Text.RegularExpressions.Regex HrCodePattern =
        new("^[A-Za-z0-9][A-Za-z0-9._-]{0,49}$", System.Text.RegularExpressions.RegexOptions.CultureInvariant);
    private static readonly System.Text.RegularExpressions.Regex EmployeeNumberPattern =
        new("^[A-Za-z0-9][A-Za-z0-9._-]{0,29}$", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    public static RouteGroupBuilder MapHrOrganizationalStructureEndpoints(this RouteGroupBuilder api)
    {
        var employees = api.MapGroup("/companies/{companyId:guid}/org/employees")
            .RequireAuthorization(policy => policy.RequireClaim(
                Infrastructure.Tenancy.CompanyAccessService.AccountKindClaimType,
                Infrastructure.Tenancy.CompanyAccessService.UserAccountKind));
        employees.MapGet("", ListEmployeesAsync)
            .RequireRateLimiting(Infrastructure.Security.RateLimitingPolicies.ApiSensitivePolicy);
        employees.MapGet("/{employeeId:guid}", GetEmployeeAsync)
            .RequireRateLimiting(Infrastructure.Security.RateLimitingPolicies.ApiSensitivePolicy);
        employees.MapPost("", CreateEmployeeAsync)
            .RequireRateLimiting(Infrastructure.Security.RateLimitingPolicies.ApiSensitivePolicy);
        employees.MapPut("/{employeeId:guid}", UpdateEmployeeAsync)
            .RequireRateLimiting(Infrastructure.Security.RateLimitingPolicies.ApiSensitivePolicy);
        employees.MapPost("/{employeeId:guid}/deactivate", DeactivateEmployeeAsync)
            .RequireRateLimiting(Infrastructure.Security.RateLimitingPolicies.ApiSensitivePolicy);
        employees.MapPut("/{employeeId:guid}/user", LinkEmployeeUserAsync)
            .RequireRateLimiting(Infrastructure.Security.RateLimitingPolicies.ApiSensitivePolicy);
        employees.MapDelete("/{employeeId:guid}/user", UnlinkEmployeeUserAsync)
            .RequireRateLimiting(Infrastructure.Security.RateLimitingPolicies.ApiSensitivePolicy);
        employees.MapPost("/{employeeId:guid}/assignment-change", ChangeAssignmentAsync)
            .RequireRateLimiting(Infrastructure.Security.RateLimitingPolicies.ApiSensitivePolicy);
        employees.MapGet("/{employeeId:guid}/history", GetEmployeeHistoryAsync)
            .RequireRateLimiting(Infrastructure.Security.RateLimitingPolicies.ApiSensitivePolicy);

        var units = api.MapGroup("/companies/{companyId:guid}/org/units")
            .RequireAuthorization(policy => policy.RequireClaim(
                Infrastructure.Tenancy.CompanyAccessService.AccountKindClaimType,
                Infrastructure.Tenancy.CompanyAccessService.UserAccountKind));
        units.MapGet("", ListUnitsAsync)
            .RequireRateLimiting(Infrastructure.Security.RateLimitingPolicies.ApiSensitivePolicy);
        units.MapGet("/{unitId:guid}", GetUnitAsync)
            .RequireRateLimiting(Infrastructure.Security.RateLimitingPolicies.ApiSensitivePolicy);
        units.MapPost("", CreateUnitAsync)
            .RequireRateLimiting(Infrastructure.Security.RateLimitingPolicies.ApiSensitivePolicy);
        units.MapPut("/{unitId:guid}", UpdateUnitAsync)
            .RequireRateLimiting(Infrastructure.Security.RateLimitingPolicies.ApiSensitivePolicy);
        units.MapPost("/{unitId:guid}/deactivate", DeactivateUnitAsync)
            .RequireRateLimiting(Infrastructure.Security.RateLimitingPolicies.ApiSensitivePolicy);
        units.MapGet("/{unitId:guid}/subtree", GetUnitSubtreeAsync)
            .RequireRateLimiting(Infrastructure.Security.RateLimitingPolicies.ApiSensitivePolicy);
        units.MapGet("/{unitId:guid}/employees", ListUnitEmployeesAsync)
            .RequireRateLimiting(Infrastructure.Security.RateLimitingPolicies.ApiSensitivePolicy);

        var positions = api.MapGroup("/companies/{companyId:guid}/org/positions")
            .RequireAuthorization(policy => policy.RequireClaim(
                Infrastructure.Tenancy.CompanyAccessService.AccountKindClaimType,
                Infrastructure.Tenancy.CompanyAccessService.UserAccountKind));
        positions.MapGet("", ListPositionsAsync)
            .RequireRateLimiting(Infrastructure.Security.RateLimitingPolicies.ApiSensitivePolicy);
        positions.MapGet("/{positionId:guid}", GetPositionAsync)
            .RequireRateLimiting(Infrastructure.Security.RateLimitingPolicies.ApiSensitivePolicy);
        positions.MapPost("", CreatePositionAsync)
            .RequireRateLimiting(Infrastructure.Security.RateLimitingPolicies.ApiSensitivePolicy);
        positions.MapPut("/{positionId:guid}", UpdatePositionAsync)
            .RequireRateLimiting(Infrastructure.Security.RateLimitingPolicies.ApiSensitivePolicy);
        positions.MapPost("/{positionId:guid}/deactivate", DeactivatePositionAsync)
            .RequireRateLimiting(Infrastructure.Security.RateLimitingPolicies.ApiSensitivePolicy);
        positions.MapGet("/{positionId:guid}/employees", ListPositionEmployeesAsync)
            .RequireRateLimiting(Infrastructure.Security.RateLimitingPolicies.ApiSensitivePolicy);

        var assignments = api.MapGroup("/companies/{companyId:guid}/org/assignments")
            .RequireAuthorization(policy => policy.RequireClaim(
                Infrastructure.Tenancy.CompanyAccessService.AccountKindClaimType,
                Infrastructure.Tenancy.CompanyAccessService.UserAccountKind));
        assignments.MapGet("", ListAssignmentsAsync)
            .RequireRateLimiting(Infrastructure.Security.RateLimitingPolicies.ApiSensitivePolicy);
        assignments.MapPost("", StartAssignmentAsync)
            .RequireRateLimiting(Infrastructure.Security.RateLimitingPolicies.ApiSensitivePolicy);
        assignments.MapPost("/{assignmentId:guid}/end", EndAssignmentAsync)
            .RequireRateLimiting(Infrastructure.Security.RateLimitingPolicies.ApiSensitivePolicy);

        return api;
    }

    // ------------------------------------------------------------------ employees

    /// <summary>Lists the employees of the routed company with an allow-listed projection.</summary>
    public static async Task<IResult> ListEmployeesAsync(
        Guid companyId,
        HttpContext httpContext,
        ClaimsPrincipal principal,
        Data.DatabaseContext database,
        [FromServices] Infrastructure.Tenancy.CompanyAccessService companyAccess,
        string? search = null,
        string? status = null,
        Guid? unitId = null,
        Guid? userId = null,
        int? page = null,
        int? pageSize = null,
        CancellationToken cancellationToken = default)
    {
        var (failure, _) = await AuthorizeHrScopeAsync(
            database, companyAccess, principal, companyId, ListEmployeesPermission, cancellationToken);
        if (failure is not null) return failure;
        if (!await IsActiveCompanyAsync(database, companyId, cancellationToken)) return NotFoundForCompany();

        search = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        if (search?.Length > MaxSearchLength)
            return BadRequest("عبارت جستجو بیش از حد مجاز است.");

        CompanyEmployeeEmploymentStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!System.Enum.TryParse(status, ignoreCase: true, out CompanyEmployeeEmploymentStatus parsedStatus) ||
                !System.Enum.IsDefined(parsedStatus))
                return BadRequest("وضعیت اشتغال معتبر نیست.");
            statusFilter = parsedStatus;
        }

        var normalizedPage = Math.Clamp(page ?? 1, 1, MaxPage);
        var normalizedPageSize = Math.Clamp(pageSize ?? 25, 1, MaxPageSize);

        var query = database.CompanyEmployees.AsNoTracking()
            .Where(employee => employee.TenantId == companyId);
        if (search is not null)
            query = query.Where(employee =>
                employee.FirstName.Contains(search) ||
                employee.LastName.Contains(search) ||
                employee.EmployeeNumber.Contains(search));
        if (statusFilter is not null)
            query = query.Where(employee => employee.EmploymentStatus == statusFilter.Value);
        if (unitId is not null)
            query = query.Where(employee => database.CompanyEmployeeAssignments.Any(assignment =>
                assignment.TenantId == companyId &&
                assignment.EmployeeId == employee.Id &&
                assignment.Status == Domain.CompanyEmployeeAssignmentStatus.Active &&
                assignment.EndOnUtc == null &&
                assignment.OrganizationalUnitId == unitId));
        // Optional link lookup (stage 3 user profile): only the employee whose bookkeeping
        // link points at a membership of this exact user. An unknown user stays an empty list
        // (fail-closed, no existence leak); Guid.Empty is rejected as an invalid identifier.
        if (userId is not null)
        {
            if (userId == Guid.Empty)
                return BadRequest("شناسه کاربر معتبر نیست.");
            query = query.Where(employee =>
                employee.UserCompanyMembershipId != null &&
                employee.Membership != null &&
                employee.Membership.UserId == userId);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(employee => employee.LastName)
            .ThenBy(employee => employee.FirstName)
            .ThenBy(employee => employee.Id)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .Select(employee => new HrEmployeeListItem(
                employee.Id,
                employee.EmployeeNumber,
                employee.FirstName,
                employee.LastName,
                employee.EmploymentStatus,
                employee.HiredOnUtc,
                employee.TerminatedOnUtc,
                employee.UserCompanyMembershipId != null))
            .ToListAsync(cancellationToken);

        var employeeIds = items.Select(item => item.Id).ToArray();
        var activeAssignments = await database.CompanyEmployeeAssignments.AsNoTracking()
            .Where(assignment => assignment.TenantId == companyId &&
                                 assignment.Status == Domain.CompanyEmployeeAssignmentStatus.Active &&
                                 assignment.EndOnUtc == null &&
                                 employeeIds.Contains(assignment.EmployeeId))
            .Select(assignment => new
            {
                assignment.EmployeeId,
                assignment.OrganizationalUnitId,
                UnitName = assignment.OrganizationalUnit!.Name,
                assignment.PositionId,
                PositionName = assignment.Position!.Name,
            })
            .ToListAsync(cancellationToken);
        var byEmployee = activeAssignments.ToDictionary(row => row.EmployeeId);

        SetNoStore(httpContext);
        var utcItems = items.Select(item =>
        {
            byEmployee.TryGetValue(item.Id, out var assignment);
            return item with
            {
                HiredOnUtc = ToUtc(item.HiredOnUtc),
                TerminatedOnUtc = ToUtc(item.TerminatedOnUtc),
                UnitId = assignment?.OrganizationalUnitId,
                UnitName = assignment?.UnitName,
                PositionId = assignment?.PositionId,
                PositionName = assignment?.PositionName,
            };
        }).ToArray();
        return TypedResults.Ok(new HrEmployeeListResponse(utcItems, totalCount, normalizedPage, normalizedPageSize));
    }

    /// <summary>Shows one employee with the active assignment and the optional user link.</summary>
    public static async Task<IResult> GetEmployeeAsync(
        Guid companyId,
        Guid employeeId,
        HttpContext httpContext,
        ClaimsPrincipal principal,
        Data.DatabaseContext database,
        [FromServices] Infrastructure.Tenancy.CompanyAccessService companyAccess,
        CancellationToken cancellationToken = default)
    {
        var (failure, _) = await AuthorizeHrScopeAsync(
            database, companyAccess, principal, companyId, ViewEmployeesPermission, cancellationToken);
        if (failure is not null) return failure;
        if (!await IsActiveCompanyAsync(database, companyId, cancellationToken)) return NotFoundForCompany();

        var employee = await database.CompanyEmployees.AsNoTracking()
            .Where(current => current.Id == employeeId && current.TenantId == companyId)
            .Select(current => new
            {
                current.Id,
                current.EmployeeNumber,
                current.FirstName,
                current.LastName,
                current.EmploymentStatus,
                current.HiredOnUtc,
                current.TerminatedOnUtc,
                current.UserCompanyMembershipId,
                // The membership is optional: the conditional keeps the LEFT JOIN nullable
                // instead of forcing a Guid materialization that throws on SQL Server.
                LinkedUserId = current.Membership != null ? (Guid?)current.Membership.UserId : null,
                current.CreatedAtUtc,
                current.RowVersion,
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (employee is null) return NotFoundForCompany();

        var assignment = await database.CompanyEmployeeAssignments.AsNoTracking()
            .Where(current => current.TenantId == companyId &&
                              current.EmployeeId == employeeId &&
                              current.Status == Domain.CompanyEmployeeAssignmentStatus.Active &&
                              current.EndOnUtc == null)
            .Select(current => new HrAssignmentItem(
                current.Id,
                current.EmployeeId,
                current.OrganizationalUnitId,
                current.OrganizationalUnit != null ? current.OrganizationalUnit.Code : null,
                current.OrganizationalUnit != null ? current.OrganizationalUnit.Name : null,
                current.PositionId,
                current.Position != null ? current.Position.Code : null,
                current.Position != null ? current.Position.Name : null,
                current.ManagerEmployeeId,
                current.StartOnUtc,
                current.EndOnUtc,
                current.Status))
            .FirstOrDefaultAsync(cancellationToken);

        SetNoStore(httpContext);
        return TypedResults.Ok(new HrEmployeeDetail(
            employee.Id,
            employee.EmployeeNumber,
            employee.FirstName,
            employee.LastName,
            employee.EmploymentStatus,
            ToUtc(employee.HiredOnUtc),
            ToUtc(employee.TerminatedOnUtc),
            employee.UserCompanyMembershipId,
            employee.LinkedUserId,
            ToUtc(employee.CreatedAtUtc),
            ToBase64(employee.RowVersion),
            assignment is null ? null : assignment with
            {
                StartOnUtc = ToUtc(assignment.StartOnUtc),
                EndOnUtc = ToUtc(assignment.EndOnUtc),
            }));
    }

    /// <summary>
    /// Creates a personnel record of the routed company. The employee may exist without a
    /// login account; nothing here touches memberships, roles or permissions (D-064/D-072).
    /// </summary>
    public static async Task<IResult> CreateEmployeeAsync(
        Guid companyId,
        HttpContext httpContext,
        ClaimsPrincipal principal,
        [FromBody] HrCreateEmployeeRequest request,
        [FromServices] IAntiforgery antiforgery,
        Data.DatabaseContext database,
        [FromServices] Infrastructure.Tenancy.CompanyAccessService companyAccess,
        [FromServices] TimeProvider timeProvider,
        CancellationToken cancellationToken = default)
    {
        var (failure, scope) = await AuthorizeHrScopeAsync(
            database, companyAccess, principal, companyId, CreateEmployeesPermission, cancellationToken);
        if (failure is not null) return failure;
        if (!await antiforgery.IsRequestValidAsync(httpContext))
            return TypedResults.Problem(title: "درخواست نامعتبر است.", statusCode: StatusCodes.Status400BadRequest);
        if (!await IsActiveCompanyAsync(database, companyId, cancellationToken)) return NotFoundForCompany();

        var employeeNumber = request.EmployeeNumber?.Trim();
        var firstName = Dtat.Utility.FixText(request.FirstName);
        var lastName = Dtat.Utility.FixText(request.LastName);
        if (string.IsNullOrWhiteSpace(employeeNumber) || !EmployeeNumberPattern.IsMatch(employeeNumber) ||
            string.IsNullOrWhiteSpace(firstName) || firstName.Length > 100 ||
            string.IsNullOrWhiteSpace(lastName) || lastName.Length > 100)
            return BadRequest("شماره پرسنلی و نام و نام خانوادگی معتبر نیست.");

        if (await database.CompanyEmployees.AsNoTracking()
                .AnyAsync(employee => employee.TenantId == companyId && employee.EmployeeNumber == employeeNumber,
                    cancellationToken))
            return TypedResults.Conflict(new { error = "شماره پرسنلی در این شرکت تکراری است." });

        var violation = HrRules.ValidateEmployment(
            Domain.CompanyEmployeeEmploymentStatus.Active, request.HiredOnUtc, null);
        if (violation != HrRuleViolation.None)
            return BadRequest("دوره همکاری معتبر نیست.");

        var employee = new Domain.CompanyEmployee
        {
            TenantId = companyId,
            EmployeeNumber = employeeNumber,
            FirstName = firstName,
            LastName = lastName,
            EmploymentStatus = Domain.CompanyEmployeeEmploymentStatus.Active,
            HiredOnUtc = request.HiredOnUtc,
            CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime,
            CreatedByUserId = scope!.UserId,
        };
        database.CompanyEmployees.Add(employee);

        database.SecurityAuditEvents.Add(new Domain.SecurityAuditEvent
        {
            UserId = scope.UserId,
            TenantId = companyId,
            EventType = "HrEmployeeCreated",
            EntityType = "CompanyEmployee",
            EntityId = employee.Id.ToString(),
            OccurredAtUtc = timeProvider.GetUtcNow().UtcDateTime,
            CorrelationId = System.Diagnostics.Activity.Current?.Id,
            IpAddress = httpContext.Connection.RemoteIpAddress?.ToString(),
            DetailsJson = JsonSerializer.Serialize(new { employee.EmployeeNumber }),
        });

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return TypedResults.Conflict(new { error = "شماره پرسنلی در این شرکت تکراری است." });
        }

        SetNoStore(httpContext);
        return TypedResults.Created(
            $"/api/v1/companies/{companyId}/org/employees/{employee.Id}",
            new HrEmployeeCreatedResponse(employee.Id));
    }

    /// <summary>Edits the base fields of an employee; termination runs through deactivate.</summary>
    public static async Task<IResult> UpdateEmployeeAsync(
        Guid companyId,
        Guid employeeId,
        HttpContext httpContext,
        ClaimsPrincipal principal,
        [FromBody] HrUpdateEmployeeRequest request,
        [FromServices] IAntiforgery antiforgery,
        Data.DatabaseContext database,
        [FromServices] Infrastructure.Tenancy.CompanyAccessService companyAccess,
        [FromServices] TimeProvider timeProvider,
        CancellationToken cancellationToken = default)
    {
        var (failure, scope) = await AuthorizeHrScopeAsync(
            database, companyAccess, principal, companyId, UpdateEmployeesPermission, cancellationToken);
        if (failure is not null) return failure;
        if (!await antiforgery.IsRequestValidAsync(httpContext))
            return TypedResults.Problem(title: "درخواست نامعتبر است.", statusCode: StatusCodes.Status400BadRequest);
        if (!await IsActiveCompanyAsync(database, companyId, cancellationToken)) return NotFoundForCompany();

        if (request.EmploymentStatus == Domain.CompanyEmployeeEmploymentStatus.Terminated)
            return BadRequest("خاتمه همکاری فقط از مسیر deactivate انجام می‌شود.");

        var employee = await database.CompanyEmployees
            .FirstOrDefaultAsync(current => current.Id == employeeId && current.TenantId == companyId,
                cancellationToken);
        if (employee is null) return NotFoundForCompany();
        if (!TryApplyRowVersion(database, employee, request.RowVersion, out var rowVersionError))
            return BadRequest(rowVersionError);

        var firstName = Dtat.Utility.FixText(request.FirstName);
        var lastName = Dtat.Utility.FixText(request.LastName);
        if (string.IsNullOrWhiteSpace(firstName) || firstName.Length > 100 ||
            string.IsNullOrWhiteSpace(lastName) || lastName.Length > 100 ||
            !System.Enum.IsDefined(request.EmploymentStatus))
            return BadRequest("نام و نام خانوادگی یا وضعیت اشتغال معتبر نیست.");
        if (employee.EmploymentStatus == Domain.CompanyEmployeeEmploymentStatus.Terminated &&
            request.EmploymentStatus != Domain.CompanyEmployeeEmploymentStatus.Terminated)
            return TypedResults.Conflict(new { error = "کارمند خاتمه‌همکاری قابل فعال‌سازی مجدد نیست؛ رکورد پرسنلی جدید ثبت کنید." });

        var terminatedOnUtc = employee.EmploymentStatus == Domain.CompanyEmployeeEmploymentStatus.Terminated
            ? employee.TerminatedOnUtc
            : null;
        var violation = HrRules.ValidateEmployment(
            request.EmploymentStatus, request.HiredOnUtc ?? employee.HiredOnUtc, terminatedOnUtc);
        if (violation != HrRuleViolation.None)
            return BadRequest("دوره همکاری معتبر نیست.");

        employee.FirstName = firstName;
        employee.LastName = lastName;
        employee.EmploymentStatus = request.EmploymentStatus;
        employee.HiredOnUtc = request.HiredOnUtc ?? employee.HiredOnUtc;

        database.SecurityAuditEvents.Add(new Domain.SecurityAuditEvent
        {
            UserId = scope!.UserId,
            TenantId = companyId,
            EventType = "HrEmployeeUpdated",
            EntityType = "CompanyEmployee",
            EntityId = employee.Id.ToString(),
            OccurredAtUtc = timeProvider.GetUtcNow().UtcDateTime,
            CorrelationId = System.Diagnostics.Activity.Current?.Id,
            IpAddress = httpContext.Connection.RemoteIpAddress?.ToString(),
            DetailsJson = JsonSerializer.Serialize(new
            {
                employee.FirstName,
                employee.LastName,
                employmentStatus = employee.EmploymentStatus.ToString(),
            }),
        });

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            SetNoStore(httpContext);
            return TypedResults.Conflict(new { error = "این کارمند هم‌زمان توسط کاربر دیگری تغییر کرده است؛ اطلاعات را تازه‌سازی کنید." });
        }

        SetNoStore(httpContext);
        return TypedResults.NoContent();
    }

    /// <summary>
    /// Terminates the employment and ends the active assignment in one transaction. The
    /// linked membership, if any, is intentionally NOT touched: revoking access runs only
    /// through the authorized access-management workflows (D-064/D-065/D-072).
    /// </summary>
    public static async Task<IResult> DeactivateEmployeeAsync(
        Guid companyId,
        Guid employeeId,
        HttpContext httpContext,
        ClaimsPrincipal principal,
        [FromServices] IAntiforgery antiforgery,
        Data.DatabaseContext database,
        [FromServices] Infrastructure.Tenancy.CompanyAccessService companyAccess,
        [FromServices] TimeProvider timeProvider,
        CancellationToken cancellationToken = default)
    {
        var (failure, scope) = await AuthorizeHrScopeAsync(
            database, companyAccess, principal, companyId, UpdateEmployeesPermission, cancellationToken);
        if (failure is not null) return failure;
        if (!await antiforgery.IsRequestValidAsync(httpContext))
            return TypedResults.Problem(title: "درخواست نامعتبر است.", statusCode: StatusCodes.Status400BadRequest);
        if (!await IsActiveCompanyAsync(database, companyId, cancellationToken)) return NotFoundForCompany();

        await using var transaction = database.Database.IsRelational()
            ? await database.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            : null;

        var employee = await database.CompanyEmployees
            .FirstOrDefaultAsync(current => current.Id == employeeId && current.TenantId == companyId,
                cancellationToken);
        if (employee is null) return NotFoundForCompany();
        if (employee.EmploymentStatus == Domain.CompanyEmployeeEmploymentStatus.Terminated)
            return TypedResults.Conflict(new { error = "همکاری این کارمند قبلاً خاتمه یافته است." });

        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        // The termination instant is the stored hire date's counterpart, so the pure
        // employment rule must pass before the state turn: a termination before the
        // recorded (possibly future-dated) hire date would corrupt the employment period.
        var employmentViolation = HrRules.ValidateEmployment(
            Domain.CompanyEmployeeEmploymentStatus.Terminated, employee.HiredOnUtc, nowUtc);
        if (employmentViolation != HrRuleViolation.None)
            return BadRequest("دوره همکاری معتبر نیست؛ تاریخ خاتمه نمی‌تواند قبل از تاریخ شروع همکاری باشد.");

        employee.EmploymentStatus = Domain.CompanyEmployeeEmploymentStatus.Terminated;
        employee.TerminatedOnUtc = nowUtc;

        var activeAssignment = await database.CompanyEmployeeAssignments
            .Where(assignment => assignment.TenantId == companyId &&
                                 assignment.EmployeeId == employeeId &&
                                 assignment.Status == Domain.CompanyEmployeeAssignmentStatus.Active &&
                                 assignment.EndOnUtc == null)
            .FirstOrDefaultAsync(cancellationToken);
        if (activeAssignment is not null)
        {
            activeAssignment.Status = Domain.CompanyEmployeeAssignmentStatus.Ended;
            activeAssignment.EndOnUtc = nowUtc;
            activeAssignment.EndedByUserId = scope!.UserId;
            database.Entry(activeAssignment).Property(current => current.EmployeeId).IsModified = true;
        }

        var actorId = scope!.UserId;
        database.SecurityAuditEvents.Add(new Domain.SecurityAuditEvent
        {
            UserId = actorId,
            TenantId = companyId,
            EventType = "HrEmployeeTerminated",
            EntityType = "CompanyEmployee",
            EntityId = employee.Id.ToString(),
            OccurredAtUtc = nowUtc,
            CorrelationId = System.Diagnostics.Activity.Current?.Id,
            IpAddress = httpContext.Connection.RemoteIpAddress?.ToString(),
            DetailsJson = JsonSerializer.Serialize(new { endedActiveAssignment = activeAssignment is not null }),
        });

        try
        {
            await database.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            SetNoStore(httpContext);
            return TypedResults.Conflict(new { error = "این کارمند هم‌زمان توسط کاربر دیگری تغییر کرده است؛ اطلاعات را تازه‌سازی کنید." });
        }

        SetNoStore(httpContext);
        return TypedResults.NoContent();
    }

    /// <summary>
    /// Anchors the employee to one approved, active membership of the SAME company. The link
    /// is bookkeeping: it creates no membership, approves nothing and grants no permission
    /// (D-064/D-072). One membership anchors at most one employee (filtered unique index).
    /// </summary>
    public static async Task<IResult> LinkEmployeeUserAsync(
        Guid companyId,
        Guid employeeId,
        HttpContext httpContext,
        ClaimsPrincipal principal,
        [FromBody] HrLinkEmployeeUserRequest request,
        [FromServices] IAntiforgery antiforgery,
        Data.DatabaseContext database,
        [FromServices] Infrastructure.Tenancy.CompanyAccessService companyAccess,
        [FromServices] TimeProvider timeProvider,
        CancellationToken cancellationToken = default)
    {
        var (failure, scope) = await AuthorizeHrScopeAsync(
            database, companyAccess, principal, companyId, ManageEmployeeUserLinkPermission, cancellationToken);
        if (failure is not null) return failure;
        if (!await antiforgery.IsRequestValidAsync(httpContext))
            return TypedResults.Problem(title: "درخواست نامعتبر است.", statusCode: StatusCodes.Status400BadRequest);
        if (!await IsActiveCompanyAsync(database, companyId, cancellationToken)) return NotFoundForCompany();
        if (request.UserId == Guid.Empty)
            return BadRequest("شناسه کاربر معتبر نیست.");

        var employee = await database.CompanyEmployees
            .FirstOrDefaultAsync(current => current.Id == employeeId && current.TenantId == companyId,
                cancellationToken);
        if (employee is null) return NotFoundForCompany();
        if (employee.EmploymentStatus == Domain.CompanyEmployeeEmploymentStatus.Terminated)
            return TypedResults.Conflict(new { error = "کارمند خاتمه‌همکاری قابل اتصال به حساب کاربری نیست." });

        var membership = await database.UserCompanyMemberships.AsNoTracking()
            .Where(current => current.UserId == request.UserId && current.TenantId == companyId && current.IsActive)
            .Select(current => new { current.Id })
            .FirstOrDefaultAsync(cancellationToken);
        if (membership is null)
            return TypedResults.Conflict(new { error = "کاربر مورد نظر عضویت فعال در این شرکت ندارد؛ اتصال فقط به عضویت تأییدشده همان شرکت ممکن است." });

        if (employee.UserCompanyMembershipId == membership.Id)
            return TypedResults.NoContent();

        if (await database.CompanyEmployees.AsNoTracking()
                .AnyAsync(current => current.TenantId == companyId &&
                                     current.Id != employee.Id &&
                                     current.UserCompanyMembershipId == membership.Id, cancellationToken))
            return TypedResults.Conflict(new { error = "این عضویت قبلاً به کارمند دیگری متصل شده است." });

        employee.UserCompanyMembershipId = membership.Id;

        database.SecurityAuditEvents.Add(new Domain.SecurityAuditEvent
        {
            UserId = scope!.UserId,
            TenantId = companyId,
            EventType = "HrEmployeeUserLinked",
            EntityType = "CompanyEmployee",
            EntityId = employee.Id.ToString(),
            OccurredAtUtc = timeProvider.GetUtcNow().UtcDateTime,
            CorrelationId = System.Diagnostics.Activity.Current?.Id,
            IpAddress = httpContext.Connection.RemoteIpAddress?.ToString(),
            DetailsJson = JsonSerializer.Serialize(new { userId = request.UserId, membershipId = membership.Id }),
        });

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            SetNoStore(httpContext);
            return TypedResults.Conflict(new { error = "این کارمند هم‌زمان توسط کاربر دیگری تغییر کرده است؛ اطلاعات را تازه‌سازی کنید." });
        }
        catch (DbUpdateException)
        {
            return TypedResults.Conflict(new { error = "این عضویت قبلاً به کارمند دیگری متصل شده است." });
        }

        SetNoStore(httpContext);
        return TypedResults.NoContent();
    }

    /// <summary>Removes the optional bookkeeping link between the employee and a user.</summary>
    public static async Task<IResult> UnlinkEmployeeUserAsync(
        Guid companyId,
        Guid employeeId,
        HttpContext httpContext,
        ClaimsPrincipal principal,
        [FromServices] IAntiforgery antiforgery,
        Data.DatabaseContext database,
        [FromServices] Infrastructure.Tenancy.CompanyAccessService companyAccess,
        [FromServices] TimeProvider timeProvider,
        CancellationToken cancellationToken = default)
    {
        var (failure, scope) = await AuthorizeHrScopeAsync(
            database, companyAccess, principal, companyId, ManageEmployeeUserLinkPermission, cancellationToken);
        if (failure is not null) return failure;
        if (!await antiforgery.IsRequestValidAsync(httpContext))
            return TypedResults.Problem(title: "درخواست نامعتبر است.", statusCode: StatusCodes.Status400BadRequest);
        if (!await IsActiveCompanyAsync(database, companyId, cancellationToken)) return NotFoundForCompany();

        var employee = await database.CompanyEmployees
            .FirstOrDefaultAsync(current => current.Id == employeeId && current.TenantId == companyId,
                cancellationToken);
        if (employee is null) return NotFoundForCompany();
        if (employee.UserCompanyMembershipId is null)
            return TypedResults.Conflict(new { error = "این کارمند به هیچ حساب کاربری متصل نیست." });

        var previousMembershipId = employee.UserCompanyMembershipId;
        employee.UserCompanyMembershipId = null;

        database.SecurityAuditEvents.Add(new Domain.SecurityAuditEvent
        {
            UserId = scope!.UserId,
            TenantId = companyId,
            EventType = "HrEmployeeUserUnlinked",
            EntityType = "CompanyEmployee",
            EntityId = employee.Id.ToString(),
            OccurredAtUtc = timeProvider.GetUtcNow().UtcDateTime,
            CorrelationId = System.Diagnostics.Activity.Current?.Id,
            IpAddress = httpContext.Connection.RemoteIpAddress?.ToString(),
            DetailsJson = JsonSerializer.Serialize(new { membershipId = previousMembershipId }),
        });

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            SetNoStore(httpContext);
            return TypedResults.Conflict(new { error = "این کارمند هم‌زمان توسط کاربر دیگری تغییر کرده است؛ اطلاعات را تازه‌سازی کنید." });
        }

        SetNoStore(httpContext);
        return TypedResults.NoContent();
    }

    // ------------------------------------------------------------------ units

    /// <summary>Lists the organizational units, flat or as a tree of the active units.</summary>
    public static async Task<IResult> ListUnitsAsync(
        Guid companyId,
        HttpContext httpContext,
        ClaimsPrincipal principal,
        Data.DatabaseContext database,
        [FromServices] Infrastructure.Tenancy.CompanyAccessService companyAccess,
        string? view = null,
        string? search = null,
        int? page = null,
        int? pageSize = null,
        CancellationToken cancellationToken = default)
    {
        var (failure, _) = await AuthorizeHrScopeAsync(
            database, companyAccess, principal, companyId, ListUnitsPermission, cancellationToken);
        if (failure is not null) return failure;
        if (!await IsActiveCompanyAsync(database, companyId, cancellationToken)) return NotFoundForCompany();

        search = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        if (search?.Length > MaxSearchLength)
            return BadRequest("عبارت جستجو بیش از حد مجاز است.");

        var wantsTree = string.Equals(view, "tree", StringComparison.OrdinalIgnoreCase);
        if (!wantsTree && !string.IsNullOrWhiteSpace(view) &&
            !string.Equals(view, "flat", StringComparison.OrdinalIgnoreCase))
            return BadRequest("نمای درخواست معتبر نیست.");

        var query = database.CompanyOrganizationalUnits.AsNoTracking()
            .Where(unit => unit.TenantId == companyId);
        if (!wantsTree)
            query = query.Where(unit => unit.IsActive);
        if (search is not null)
            query = query.Where(unit => unit.Name.Contains(search) || unit.Code.Contains(search));

        var units = await query
            .OrderBy(unit => unit.Code)
            .ThenBy(unit => unit.Id)
            .Select(unit => new HrUnitItem(
                unit.Id,
                unit.Code,
                unit.Name,
                unit.Description,
                unit.ParentUnitId,
                unit.IsActive,
                unit.ValidFromUtc,
                unit.ValidToUtc))
            .ToListAsync(cancellationToken);
        units = units.Select(unit => unit with
        {
            ValidFromUtc = ToUtc(unit.ValidFromUtc),
            ValidToUtc = ToUtc(unit.ValidToUtc),
        }).ToList();

        SetNoStore(httpContext);
        if (!wantsTree)
        {
            var normalizedPage = Math.Clamp(page ?? 1, 1, MaxPage);
            var normalizedPageSize = Math.Clamp(pageSize ?? 25, 1, MaxPageSize);
            var pageItems = units
                .Skip((normalizedPage - 1) * normalizedPageSize)
                .Take(normalizedPageSize)
                .ToArray();
            return TypedResults.Ok(new HrUnitListResponse(pageItems, units.Count, normalizedPage, normalizedPageSize));
        }

        var roots = BuildUnitTree(units);
        return TypedResults.Ok(new HrUnitTreeResponse(roots));
    }

    /// <summary>Shows one unit with its active child and assignment counts.</summary>
    public static async Task<IResult> GetUnitAsync(
        Guid companyId,
        Guid unitId,
        HttpContext httpContext,
        ClaimsPrincipal principal,
        Data.DatabaseContext database,
        [FromServices] Infrastructure.Tenancy.CompanyAccessService companyAccess,
        CancellationToken cancellationToken = default)
    {
        var (failure, _) = await AuthorizeHrScopeAsync(
            database, companyAccess, principal, companyId, ListUnitsPermission, cancellationToken);
        if (failure is not null) return failure;
        if (!await IsActiveCompanyAsync(database, companyId, cancellationToken)) return NotFoundForCompany();

        var unit = await database.CompanyOrganizationalUnits.AsNoTracking()
            .Where(current => current.Id == unitId && current.TenantId == companyId)
            .Select(current => new
            {
                current.Id,
                current.Code,
                current.Name,
                current.Description,
                current.ParentUnitId,
                current.IsActive,
                current.ValidFromUtc,
                current.ValidToUtc,
                ActiveChildCount = current.ChildUnits.Count(child => child.IsActive),
                ActiveAssignmentCount = current.Assignments.Count(assignment =>
                    assignment.Status == Domain.CompanyEmployeeAssignmentStatus.Active &&
                    assignment.EndOnUtc == null),
                current.RowVersion,
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (unit is null) return NotFoundForCompany();

        SetNoStore(httpContext);
        return TypedResults.Ok(new HrUnitDetail(
            unit.Id,
            unit.Code,
            unit.Name,
            unit.Description,
            unit.ParentUnitId,
            unit.IsActive,
            ToUtc(unit.ValidFromUtc),
            ToUtc(unit.ValidToUtc),
            unit.ActiveChildCount,
            unit.ActiveAssignmentCount,
            ToBase64(unit.RowVersion)));
    }

    /// <summary>Creates a unit of the routed company, optionally under an active parent.</summary>
    public static async Task<IResult> CreateUnitAsync(
        Guid companyId,
        HttpContext httpContext,
        ClaimsPrincipal principal,
        [FromBody] HrCreateUnitRequest request,
        [FromServices] IAntiforgery antiforgery,
        Data.DatabaseContext database,
        [FromServices] Infrastructure.Tenancy.CompanyAccessService companyAccess,
        [FromServices] TimeProvider timeProvider,
        CancellationToken cancellationToken = default)
    {
        var (failure, scope) = await AuthorizeHrScopeAsync(
            database, companyAccess, principal, companyId, CreateUnitsPermission, cancellationToken);
        if (failure is not null) return failure;
        if (!await antiforgery.IsRequestValidAsync(httpContext))
            return TypedResults.Problem(title: "درخواست نامعتبر است.", statusCode: StatusCodes.Status400BadRequest);
        if (!await IsActiveCompanyAsync(database, companyId, cancellationToken)) return NotFoundForCompany();

        var code = request.Code?.Trim();
        var name = Dtat.Utility.FixText(request.Name);
        if (string.IsNullOrWhiteSpace(code) || !HrCodePattern.IsMatch(code) ||
            string.IsNullOrWhiteSpace(name) || name.Length > 200 ||
            (request.Description?.Length ?? 0) > 1000)
            return BadRequest("کد و عنوان واحد سازمانی معتبر نیست.");
        if (request.ValidFromUtc is not null && request.ValidToUtc is not null &&
            request.ValidToUtc < request.ValidFromUtc)
            return BadRequest("بازه اعتبار واحد معتبر نیست.");

        if (request.ParentUnitId is not null)
        {
            var parent = await database.CompanyOrganizationalUnits.AsNoTracking()
                .Where(unit => unit.Id == request.ParentUnitId && unit.TenantId == companyId && unit.IsActive)
                .Select(unit => new { unit.Id })
                .FirstOrDefaultAsync(cancellationToken);
            if (parent is null)
                return TypedResults.Conflict(new { error = "واحد والد باید فعال و متعلق به همین شرکت باشد." });
        }

        if (await database.CompanyOrganizationalUnits.AsNoTracking()
                .AnyAsync(unit => unit.TenantId == companyId && unit.Code == code, cancellationToken))
            return TypedResults.Conflict(new { error = "کد واحد سازمانی در این شرکت تکراری است." });

        var unit = new Domain.CompanyOrganizationalUnit
        {
            TenantId = companyId,
            Code = code,
            Name = name,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            ParentUnitId = request.ParentUnitId,
            IsActive = true,
            ValidFromUtc = request.ValidFromUtc,
            ValidToUtc = request.ValidToUtc,
            CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime,
            CreatedByUserId = scope!.UserId,
        };
        database.CompanyOrganizationalUnits.Add(unit);

        database.SecurityAuditEvents.Add(new Domain.SecurityAuditEvent
        {
            UserId = scope.UserId,
            TenantId = companyId,
            EventType = "HrUnitCreated",
            EntityType = "CompanyOrganizationalUnit",
            EntityId = unit.Id.ToString(),
            OccurredAtUtc = timeProvider.GetUtcNow().UtcDateTime,
            CorrelationId = System.Diagnostics.Activity.Current?.Id,
            IpAddress = httpContext.Connection.RemoteIpAddress?.ToString(),
            DetailsJson = JsonSerializer.Serialize(new { unit.Code, unit.ParentUnitId }),
        });

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return TypedResults.Conflict(new { error = "کد واحد سازمانی در این شرکت تکراری است." });
        }

        SetNoStore(httpContext);
        return TypedResults.Created(
            $"/api/v1/companies/{companyId}/org/units/{unit.Id}",
            new HrUnitCreatedResponse(unit.Id));
    }

    /// <summary>
    /// Edits a unit, including moving it under a new parent of the same company. The parent
    /// chain is cycle-checked inside the transaction over fresh rows; the database FK keeps
    /// the parent inside the company in any case.
    /// </summary>
    public static async Task<IResult> UpdateUnitAsync(
        Guid companyId,
        Guid unitId,
        HttpContext httpContext,
        ClaimsPrincipal principal,
        [FromBody] HrUpdateUnitRequest request,
        [FromServices] IAntiforgery antiforgery,
        Data.DatabaseContext database,
        [FromServices] Infrastructure.Tenancy.CompanyAccessService companyAccess,
        [FromServices] TimeProvider timeProvider,
        CancellationToken cancellationToken = default)
    {
        var (failure, scope) = await AuthorizeHrScopeAsync(
            database, companyAccess, principal, companyId, UpdateUnitsPermission, cancellationToken);
        if (failure is not null) return failure;
        if (!await antiforgery.IsRequestValidAsync(httpContext))
            return TypedResults.Problem(title: "درخواست نامعتبر است.", statusCode: StatusCodes.Status400BadRequest);
        if (!await IsActiveCompanyAsync(database, companyId, cancellationToken)) return NotFoundForCompany();

        await using var transaction = database.Database.IsRelational()
            ? await database.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            : null;

        var unit = await database.CompanyOrganizationalUnits
            .FirstOrDefaultAsync(current => current.Id == unitId && current.TenantId == companyId,
                cancellationToken);
        if (unit is null) return NotFoundForCompany();
        if (!TryApplyRowVersion(database, unit, request.RowVersion, out var rowVersionError))
            return BadRequest(rowVersionError);

        var name = Dtat.Utility.FixText(request.Name);
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200 ||
            (request.Description?.Length ?? 0) > 1000)
            return BadRequest("عنوان واحد سازمانی معتبر نیست.");
        if (request.ValidFromUtc is not null && request.ValidToUtc is not null &&
            request.ValidToUtc < request.ValidFromUtc)
            return BadRequest("بازه اعتبار واحد معتبر نیست.");

        if (request.ParentUnitId is not null)
        {
            var parent = await database.CompanyOrganizationalUnits.AsNoTracking()
                .Where(current => current.Id == request.ParentUnitId && current.TenantId == companyId && current.IsActive)
                .Select(current => new { current.Id, current.ParentUnitId })
                .FirstOrDefaultAsync(cancellationToken);
            if (parent is null)
                return TypedResults.Conflict(new { error = "واحد والد باید فعال و متعلق به همین شرکت باشد." });

            // The whole ancestor walk runs on the rows of this transaction, so a concurrent
            // move of an ancestor is either visible here or breaks this save (rowversion).
            var allUnits = await database.CompanyOrganizationalUnits.AsNoTracking()
                .Where(current => current.TenantId == companyId)
                .Select(current => new { current.Id, current.ParentUnitId })
                .ToListAsync(cancellationToken);
            var chain = BuildAncestorFacts(allUnits.Select(current => new HrUnitFact(current.Id, current.ParentUnitId, true)).ToList());
            var violation = HrRules.ValidateUnitParentChain(unit.Id, request.ParentUnitId, chain);
            if (violation == HrRuleViolation.UnitCycleTooDeep)
                return TypedResults.Conflict(new { error = "عمق ساختار سازمانی از حد مجاز گذشته است." });
            if (violation != HrRuleViolation.None)
                return TypedResults.Conflict(new { error = "انتقال واحد باعث چرخه در ساختار سازمانی می‌شود." });
        }

        var previousParentUnitId = unit.ParentUnitId;

        if (unit.IsActive && !request.IsActive)
        {
            // The edit endpoint turns the unit inactive with the same deactivation guard
            // as the dedicated deactivate endpoint (D-072): an active child unit or an
            // active employee assignment blocks the transition with 409.
            var updateHasActiveChild = await database.CompanyOrganizationalUnits.AsNoTracking()
                .AnyAsync(current => current.TenantId == companyId && current.ParentUnitId == unitId && current.IsActive,
                    cancellationToken);
            var updateHasActiveAssignment = await database.CompanyEmployeeAssignments.AsNoTracking()
                .AnyAsync(assignment => assignment.TenantId == companyId &&
                                        assignment.OrganizationalUnitId == unitId &&
                                        assignment.Status == Domain.CompanyEmployeeAssignmentStatus.Active &&
                                        assignment.EndOnUtc == null, cancellationToken);
            var block = HrRules.ValidateUnitDeactivation(updateHasActiveChild, updateHasActiveAssignment);
            if (block == CompanyUnitDeactivationBlock.HasActiveChildUnit)
                return TypedResults.Conflict(new { error = "ابتدا واحدهای زیرمجموعه فعال را منتقل یا غیرفعال کنید." });
            if (block == CompanyUnitDeactivationBlock.HasActiveAssignment)
                return TypedResults.Conflict(new { error = "ابتدا انتساب‌های فعال این واحد را پایان دهید." });
        }

        unit.Name = name;
        unit.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        unit.ParentUnitId = request.ParentUnitId;
        unit.IsActive = request.IsActive;
        unit.ValidFromUtc = request.ValidFromUtc ?? unit.ValidFromUtc;
        unit.ValidToUtc = request.ValidToUtc;

        database.SecurityAuditEvents.Add(new Domain.SecurityAuditEvent
        {
            UserId = scope!.UserId,
            TenantId = companyId,
            EventType = "HrUnitUpdated",
            EntityType = "CompanyOrganizationalUnit",
            EntityId = unit.Id.ToString(),
            OccurredAtUtc = timeProvider.GetUtcNow().UtcDateTime,
            CorrelationId = System.Diagnostics.Activity.Current?.Id,
            IpAddress = httpContext.Connection.RemoteIpAddress?.ToString(),
            DetailsJson = JsonSerializer.Serialize(new
            {
                unit.Name,
                unit.ParentUnitId,
                unit.IsActive,
                parentChanged = previousParentUnitId != request.ParentUnitId,
            }),
        });

        try
        {
            await database.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            SetNoStore(httpContext);
            return TypedResults.Conflict(new { error = "این واحد هم‌زمان توسط کاربر دیگری تغییر کرده است؛ اطلاعات را تازه‌سازی کنید." });
        }

        SetNoStore(httpContext);
        return TypedResults.NoContent();
    }

    /// <summary>Deactivates a unit; active children or active assignments block it (409).</summary>
    public static async Task<IResult> DeactivateUnitAsync(
        Guid companyId,
        Guid unitId,
        HttpContext httpContext,
        ClaimsPrincipal principal,
        [FromServices] IAntiforgery antiforgery,
        Data.DatabaseContext database,
        [FromServices] Infrastructure.Tenancy.CompanyAccessService companyAccess,
        [FromServices] TimeProvider timeProvider,
        CancellationToken cancellationToken = default)
    {
        var (failure, scope) = await AuthorizeHrScopeAsync(
            database, companyAccess, principal, companyId, UpdateUnitsPermission, cancellationToken);
        if (failure is not null) return failure;
        if (!await antiforgery.IsRequestValidAsync(httpContext))
            return TypedResults.Problem(title: "درخواست نامعتبر است.", statusCode: StatusCodes.Status400BadRequest);
        if (!await IsActiveCompanyAsync(database, companyId, cancellationToken)) return NotFoundForCompany();

        await using var transaction = database.Database.IsRelational()
            ? await database.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            : null;

        var unit = await database.CompanyOrganizationalUnits
            .FirstOrDefaultAsync(current => current.Id == unitId && current.TenantId == companyId,
                cancellationToken);
        if (unit is null) return NotFoundForCompany();
        if (!unit.IsActive)
            return TypedResults.Conflict(new { error = "این واحد قبلاً غیرفعال شده است." });

        var hasActiveChild = await database.CompanyOrganizationalUnits.AsNoTracking()
            .AnyAsync(current => current.TenantId == companyId && current.ParentUnitId == unitId && current.IsActive,
                cancellationToken);
        var hasActiveAssignment = await database.CompanyEmployeeAssignments.AsNoTracking()
            .AnyAsync(assignment => assignment.TenantId == companyId &&
                                    assignment.OrganizationalUnitId == unitId &&
                                    assignment.Status == Domain.CompanyEmployeeAssignmentStatus.Active &&
                                    assignment.EndOnUtc == null, cancellationToken);
        var deactivationBlock = HrRules.ValidateUnitDeactivation(hasActiveChild, hasActiveAssignment);
        if (deactivationBlock == CompanyUnitDeactivationBlock.HasActiveChildUnit)
            return TypedResults.Conflict(new { error = "ابتدا واحدهای زیرمجموعه فعال را منتقل یا غیرفعال کنید." });
        if (deactivationBlock == CompanyUnitDeactivationBlock.HasActiveAssignment)
            return TypedResults.Conflict(new { error = "ابتدا انتساب‌های فعال این واحد را پایان دهید." });

        unit.IsActive = false;
        database.Entry(unit).Property(current => current.Name).IsModified = true;

        database.SecurityAuditEvents.Add(new Domain.SecurityAuditEvent
        {
            UserId = scope!.UserId,
            TenantId = companyId,
            EventType = "HrUnitDeactivated",
            EntityType = "CompanyOrganizationalUnit",
            EntityId = unit.Id.ToString(),
            OccurredAtUtc = timeProvider.GetUtcNow().UtcDateTime,
            CorrelationId = System.Diagnostics.Activity.Current?.Id,
            IpAddress = httpContext.Connection.RemoteIpAddress?.ToString(),
            DetailsJson = JsonSerializer.Serialize(new { unit.Code }),
        });

        try
        {
            await database.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            SetNoStore(httpContext);
            return TypedResults.Conflict(new { error = "این واحد هم‌زمان توسط کاربر دیگری تغییر کرده است؛ اطلاعات را تازه‌سازی کنید." });
        }

        SetNoStore(httpContext);
        return TypedResults.NoContent();
    }

    // ------------------------------------------------------------------ positions

    /// <summary>Lists the positions of the routed company.</summary>
    public static async Task<IResult> ListPositionsAsync(
        Guid companyId,
        HttpContext httpContext,
        ClaimsPrincipal principal,
        Data.DatabaseContext database,
        [FromServices] Infrastructure.Tenancy.CompanyAccessService companyAccess,
        string? search = null,
        int? page = null,
        int? pageSize = null,
        CancellationToken cancellationToken = default)
    {
        var (failure, _) = await AuthorizeHrScopeAsync(
            database, companyAccess, principal, companyId, ListPositionsPermission, cancellationToken);
        if (failure is not null) return failure;
        if (!await IsActiveCompanyAsync(database, companyId, cancellationToken)) return NotFoundForCompany();

        search = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        if (search?.Length > MaxSearchLength)
            return BadRequest("عبارت جستجو بیش از حد مجاز است.");

        var normalizedPage = Math.Clamp(page ?? 1, 1, MaxPage);
        var normalizedPageSize = Math.Clamp(pageSize ?? 25, 1, MaxPageSize);

        var query = database.CompanyPositions.AsNoTracking()
            .Where(position => position.TenantId == companyId);
        if (search is not null)
            query = query.Where(position => position.Name.Contains(search) || position.Code.Contains(search));

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(position => position.Code)
            .ThenBy(position => position.Id)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .Select(position => new HrPositionItem(
                position.Id,
                position.Code,
                position.Name,
                position.Description,
                position.Rank,
                position.IsActive))
            .ToListAsync(cancellationToken);

        SetNoStore(httpContext);
        return TypedResults.Ok(new HrPositionListResponse(items, totalCount, normalizedPage, normalizedPageSize));
    }

    /// <summary>Shows one position with its active assignment count.</summary>
    public static async Task<IResult> GetPositionAsync(
        Guid companyId,
        Guid positionId,
        HttpContext httpContext,
        ClaimsPrincipal principal,
        Data.DatabaseContext database,
        [FromServices] Infrastructure.Tenancy.CompanyAccessService companyAccess,
        CancellationToken cancellationToken = default)
    {
        var (failure, _) = await AuthorizeHrScopeAsync(
            database, companyAccess, principal, companyId, ListPositionsPermission, cancellationToken);
        if (failure is not null) return failure;
        if (!await IsActiveCompanyAsync(database, companyId, cancellationToken)) return NotFoundForCompany();

        var position = await database.CompanyPositions.AsNoTracking()
            .Where(current => current.Id == positionId && current.TenantId == companyId)
            .Select(current => new
            {
                current.Id,
                current.Code,
                current.Name,
                current.Description,
                current.Rank,
                current.IsActive,
                ActiveAssignmentCount = current.Assignments.Count(assignment =>
                    assignment.Status == Domain.CompanyEmployeeAssignmentStatus.Active &&
                    assignment.EndOnUtc == null),
                current.RowVersion,
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (position is null) return NotFoundForCompany();

        SetNoStore(httpContext);
        return TypedResults.Ok(new HrPositionDetail(
            position.Id,
            position.Code,
            position.Name,
            position.Description,
            position.Rank,
            position.IsActive,
            position.ActiveAssignmentCount,
            ToBase64(position.RowVersion)));
    }

    /// <summary>Creates a position of the routed company.</summary>
    public static async Task<IResult> CreatePositionAsync(
        Guid companyId,
        HttpContext httpContext,
        ClaimsPrincipal principal,
        [FromBody] HrCreatePositionRequest request,
        [FromServices] IAntiforgery antiforgery,
        Data.DatabaseContext database,
        [FromServices] Infrastructure.Tenancy.CompanyAccessService companyAccess,
        [FromServices] TimeProvider timeProvider,
        CancellationToken cancellationToken = default)
    {
        var (failure, scope) = await AuthorizeHrScopeAsync(
            database, companyAccess, principal, companyId, CreatePositionsPermission, cancellationToken);
        if (failure is not null) return failure;
        if (!await antiforgery.IsRequestValidAsync(httpContext))
            return TypedResults.Problem(title: "درخواست نامعتبر است.", statusCode: StatusCodes.Status400BadRequest);
        if (!await IsActiveCompanyAsync(database, companyId, cancellationToken)) return NotFoundForCompany();

        var code = request.Code?.Trim();
        var name = Dtat.Utility.FixText(request.Name);
        if (string.IsNullOrWhiteSpace(code) || !HrCodePattern.IsMatch(code) ||
            string.IsNullOrWhiteSpace(name) || name.Length > 200 ||
            (request.Description?.Length ?? 0) > 1000)
            return BadRequest("کد و عنوان سمت سازمانی معتبر نیست.");

        if (await database.CompanyPositions.AsNoTracking()
                .AnyAsync(position => position.TenantId == companyId && position.Code == code, cancellationToken))
            return TypedResults.Conflict(new { error = "کد سمت سازمانی در این شرکت تکراری است." });

        var position = new Domain.CompanyPosition
        {
            TenantId = companyId,
            Code = code,
            Name = name,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Rank = request.Rank,
            IsActive = true,
            CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime,
            CreatedByUserId = scope!.UserId,
        };
        database.CompanyPositions.Add(position);

        database.SecurityAuditEvents.Add(new Domain.SecurityAuditEvent
        {
            UserId = scope.UserId,
            TenantId = companyId,
            EventType = "HrPositionCreated",
            EntityType = "CompanyPosition",
            EntityId = position.Id.ToString(),
            OccurredAtUtc = timeProvider.GetUtcNow().UtcDateTime,
            CorrelationId = System.Diagnostics.Activity.Current?.Id,
            IpAddress = httpContext.Connection.RemoteIpAddress?.ToString(),
            DetailsJson = JsonSerializer.Serialize(new { position.Code }),
        });

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return TypedResults.Conflict(new { error = "کد سمت سازمانی در این شرکت تکراری است." });
        }

        SetNoStore(httpContext);
        return TypedResults.Created(
            $"/api/v1/companies/{companyId}/org/positions/{position.Id}",
            new HrPositionCreatedResponse(position.Id));
    }

    /// <summary>Edits a position; positions with active assignments can be deactivated only after they end.</summary>
    public static async Task<IResult> UpdatePositionAsync(
        Guid companyId,
        Guid positionId,
        HttpContext httpContext,
        ClaimsPrincipal principal,
        [FromBody] HrUpdatePositionRequest request,
        [FromServices] IAntiforgery antiforgery,
        Data.DatabaseContext database,
        [FromServices] Infrastructure.Tenancy.CompanyAccessService companyAccess,
        [FromServices] TimeProvider timeProvider,
        CancellationToken cancellationToken = default)
    {
        var (failure, scope) = await AuthorizeHrScopeAsync(
            database, companyAccess, principal, companyId, UpdatePositionsPermission, cancellationToken);
        if (failure is not null) return failure;
        if (!await antiforgery.IsRequestValidAsync(httpContext))
            return TypedResults.Problem(title: "درخواست نامعتبر است.", statusCode: StatusCodes.Status400BadRequest);
        if (!await IsActiveCompanyAsync(database, companyId, cancellationToken)) return NotFoundForCompany();

        var position = await database.CompanyPositions
            .FirstOrDefaultAsync(current => current.Id == positionId && current.TenantId == companyId,
                cancellationToken);
        if (position is null) return NotFoundForCompany();
        if (!TryApplyRowVersion(database, position, request.RowVersion, out var rowVersionError))
            return BadRequest(rowVersionError);

        var name = Dtat.Utility.FixText(request.Name);
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200 ||
            (request.Description?.Length ?? 0) > 1000)
            return BadRequest("عنوان سمت سازمانی معتبر نیست.");

        position.Name = name;
        position.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        position.Rank = request.Rank;

        if (position.IsActive && !request.IsActive)
        {
            // The edit endpoint turns the position inactive with the same deactivation
            // guard as the dedicated deactivate endpoint (D-072): an active assignment
            // blocks the transition with 409.
            var updateHasActiveAssignment = await database.CompanyEmployeeAssignments.AsNoTracking()
                .AnyAsync(assignment => assignment.TenantId == companyId &&
                                        assignment.PositionId == positionId &&
                                        assignment.Status == Domain.CompanyEmployeeAssignmentStatus.Active &&
                                        assignment.EndOnUtc == null, cancellationToken);
            var updateViolation = HrRules.ValidatePositionDeactivation(updateHasActiveAssignment);
            if (updateViolation != HrRuleViolation.None)
                return TypedResults.Conflict(new { error = "ابتدا انتساب‌های فعال این سمت را پایان دهید." });
        }

        position.IsActive = request.IsActive;

        database.SecurityAuditEvents.Add(new Domain.SecurityAuditEvent
        {
            UserId = scope!.UserId,
            TenantId = companyId,
            EventType = "HrPositionUpdated",
            EntityType = "CompanyPosition",
            EntityId = position.Id.ToString(),
            OccurredAtUtc = timeProvider.GetUtcNow().UtcDateTime,
            CorrelationId = System.Diagnostics.Activity.Current?.Id,
            IpAddress = httpContext.Connection.RemoteIpAddress?.ToString(),
            DetailsJson = JsonSerializer.Serialize(new { position.Name, position.IsActive }),
        });

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            SetNoStore(httpContext);
            return TypedResults.Conflict(new { error = "این سمت هم‌زمان توسط کاربر دیگری تغییر کرده است؛ اطلاعات را تازه‌سازی کنید." });
        }

        SetNoStore(httpContext);
        return TypedResults.NoContent();
    }

    /// <summary>Deactivates a position; an active assignment blocks it (409).</summary>
    public static async Task<IResult> DeactivatePositionAsync(
        Guid companyId,
        Guid positionId,
        HttpContext httpContext,
        ClaimsPrincipal principal,
        [FromServices] IAntiforgery antiforgery,
        Data.DatabaseContext database,
        [FromServices] Infrastructure.Tenancy.CompanyAccessService companyAccess,
        [FromServices] TimeProvider timeProvider,
        CancellationToken cancellationToken = default)
    {
        var (failure, scope) = await AuthorizeHrScopeAsync(
            database, companyAccess, principal, companyId, UpdatePositionsPermission, cancellationToken);
        if (failure is not null) return failure;
        if (!await antiforgery.IsRequestValidAsync(httpContext))
            return TypedResults.Problem(title: "درخواست نامعتبر است.", statusCode: StatusCodes.Status400BadRequest);
        if (!await IsActiveCompanyAsync(database, companyId, cancellationToken)) return NotFoundForCompany();

        await using var transaction = database.Database.IsRelational()
            ? await database.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            : null;

        var position = await database.CompanyPositions
            .FirstOrDefaultAsync(current => current.Id == positionId && current.TenantId == companyId,
                cancellationToken);
        if (position is null) return NotFoundForCompany();
        if (!position.IsActive)
            return TypedResults.Conflict(new { error = "این سمت قبلاً غیرفعال شده است." });

        var hasActiveAssignment = await database.CompanyEmployeeAssignments.AsNoTracking()
            .AnyAsync(assignment => assignment.TenantId == companyId &&
                                    assignment.PositionId == positionId &&
                                    assignment.Status == Domain.CompanyEmployeeAssignmentStatus.Active &&
                                    assignment.EndOnUtc == null, cancellationToken);
        var deactivationViolation = HrRules.ValidatePositionDeactivation(hasActiveAssignment);
        if (deactivationViolation != HrRuleViolation.None)
            return TypedResults.Conflict(new { error = "ابتدا انتساب‌های فعال این سمت را پایان دهید." });

        position.IsActive = false;
        database.Entry(position).Property(current => current.Name).IsModified = true;

        database.SecurityAuditEvents.Add(new Domain.SecurityAuditEvent
        {
            UserId = scope!.UserId,
            TenantId = companyId,
            EventType = "HrPositionDeactivated",
            EntityType = "CompanyPosition",
            EntityId = position.Id.ToString(),
            OccurredAtUtc = timeProvider.GetUtcNow().UtcDateTime,
            CorrelationId = System.Diagnostics.Activity.Current?.Id,
            IpAddress = httpContext.Connection.RemoteIpAddress?.ToString(),
            DetailsJson = JsonSerializer.Serialize(new { position.Code }),
        });

        try
        {
            await database.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            SetNoStore(httpContext);
            return TypedResults.Conflict(new { error = "این سمت هم‌زمان توسط کاربر دیگری تغییر کرده است؛ اطلاعات را تازه‌سازی کنید." });
        }

        SetNoStore(httpContext);
        return TypedResults.NoContent();
    }

    // ------------------------------------------------------------------ assignments

    /// <summary>
    /// Lists the assignment history of the routed company. At least one filter
    /// (employeeId, unitId or positionId) is required, so the endpoint can never be used
    /// to dump the whole table.
    /// </summary>
    public static async Task<IResult> ListAssignmentsAsync(
        Guid companyId,
        HttpContext httpContext,
        ClaimsPrincipal principal,
        Data.DatabaseContext database,
        [FromServices] Infrastructure.Tenancy.CompanyAccessService companyAccess,
        Guid? employeeId = null,
        Guid? unitId = null,
        Guid? positionId = null,
        int? page = null,
        int? pageSize = null,
        CancellationToken cancellationToken = default)
    {
        var (failure, _) = await AuthorizeHrScopeAsync(
            database, companyAccess, principal, companyId, ViewEmployeesPermission, cancellationToken);
        if (failure is not null) return failure;
        if (!await IsActiveCompanyAsync(database, companyId, cancellationToken)) return NotFoundForCompany();
        if (employeeId is null && unitId is null && positionId is null)
            return BadRequest("حداقل یکی از فیلترهای employeeId، unitId یا positionId لازم است.");

        var normalizedPage = Math.Clamp(page ?? 1, 1, MaxPage);
        var normalizedPageSize = Math.Clamp(pageSize ?? 25, 1, MaxPageSize);

        var query = database.CompanyEmployeeAssignments.AsNoTracking()
            .Where(assignment => assignment.TenantId == companyId);
        if (employeeId is not null)
            query = query.Where(assignment => assignment.EmployeeId == employeeId);
        if (unitId is not null)
            query = query.Where(assignment => assignment.OrganizationalUnitId == unitId);
        if (positionId is not null)
            query = query.Where(assignment => assignment.PositionId == positionId);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(assignment => assignment.StartOnUtc)
            .ThenBy(assignment => assignment.Id)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .Select(assignment => new HrAssignmentItem(
                assignment.Id,
                assignment.EmployeeId,
                assignment.OrganizationalUnitId,
                assignment.OrganizationalUnit != null ? assignment.OrganizationalUnit.Code : null,
                assignment.OrganizationalUnit != null ? assignment.OrganizationalUnit.Name : null,
                assignment.PositionId,
                assignment.Position != null ? assignment.Position.Code : null,
                assignment.Position != null ? assignment.Position.Name : null,
                assignment.ManagerEmployeeId,
                assignment.StartOnUtc,
                assignment.EndOnUtc,
                assignment.Status))
            .ToListAsync(cancellationToken);

        SetNoStore(httpContext);
        var utcItems = items.Select(item => item with
        {
            StartOnUtc = ToUtc(item.StartOnUtc),
            EndOnUtc = ToUtc(item.EndOnUtc),
        }).ToArray();
        return TypedResults.Ok(new HrAssignmentListResponse(utcItems, totalCount, normalizedPage, normalizedPageSize));
    }

    /// <summary>
    /// Starts an assignment. The employee must be active; the unit, position and manager
    /// must be active rows of the same company. An existing active assignment blocks the
    /// start unless <c>endActive</c> ends it in the same transaction (the transfer flow).
    /// </summary>
    public static async Task<IResult> StartAssignmentAsync(
        Guid companyId,
        HttpContext httpContext,
        ClaimsPrincipal principal,
        [FromBody] HrStartAssignmentRequest request,
        [FromServices] IAntiforgery antiforgery,
        Data.DatabaseContext database,
        [FromServices] Infrastructure.Tenancy.CompanyAccessService companyAccess,
        [FromServices] TimeProvider timeProvider,
        CancellationToken cancellationToken = default)
    {
        var (failure, scope) = await AuthorizeHrScopeAsync(
            database, companyAccess, principal, companyId, ManageAssignmentsPermission, cancellationToken);
        if (failure is not null) return failure;
        if (!await antiforgery.IsRequestValidAsync(httpContext))
            return TypedResults.Problem(title: "درخواست نامعتبر است.", statusCode: StatusCodes.Status400BadRequest);
        if (!await IsActiveCompanyAsync(database, companyId, cancellationToken)) return NotFoundForCompany();

        await using var transaction = database.Database.IsRelational()
            ? await database.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            : null;

        var startResult = await StartAssignmentCoreAsync(
            database, scope!, companyId, request, endActive: request.EndActive,
            httpContext, timeProvider, cancellationToken);
        if (startResult.Error is not null)
            return startResult.Error;

        try
        {
            await database.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            SetNoStore(httpContext);
            return TypedResults.Conflict(new { error = "ساختار سازمانی هم‌زمان توسط کاربر دیگری تغییر کرده است؛ اطلاعات را تازه‌سازی کنید." });
        }
        catch (DbUpdateException)
        {
            return TypedResults.Conflict(new { error = "این کارمند در همین لحظه انتساب فعال دیگری گرفته است؛ اطلاعات را تازه‌سازی کنید." });
        }

        SetNoStore(httpContext);
        return TypedResults.Created(
            $"/api/v1/companies/{companyId}/org/assignments/{startResult.Assignment!.Id}",
            new HrAssignmentCreatedResponse(startResult.Assignment.Id));
    }

    /// <summary>Ends the active assignment; the history row is kept and never rewritten.</summary>
    public static async Task<IResult> EndAssignmentAsync(
        Guid companyId,
        Guid assignmentId,
        HttpContext httpContext,
        ClaimsPrincipal principal,
        [FromBody] HrEndAssignmentRequest? request,
        [FromServices] IAntiforgery antiforgery,
        Data.DatabaseContext database,
        [FromServices] Infrastructure.Tenancy.CompanyAccessService companyAccess,
        [FromServices] TimeProvider timeProvider,
        CancellationToken cancellationToken = default)
    {
        var (failure, scope) = await AuthorizeHrScopeAsync(
            database, companyAccess, principal, companyId, ManageAssignmentsPermission, cancellationToken);
        if (failure is not null) return failure;
        if (!await antiforgery.IsRequestValidAsync(httpContext))
            return TypedResults.Problem(title: "درخواست نامعتبر است.", statusCode: StatusCodes.Status400BadRequest);
        if (!await IsActiveCompanyAsync(database, companyId, cancellationToken)) return NotFoundForCompany();

        await using var transaction = database.Database.IsRelational()
            ? await database.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            : null;

        var assignment = await database.CompanyEmployeeAssignments
            .FirstOrDefaultAsync(current => current.Id == assignmentId && current.TenantId == companyId,
                cancellationToken);
        if (assignment is null) return NotFoundForCompany();
        if (assignment.Status == Domain.CompanyEmployeeAssignmentStatus.Ended || assignment.EndOnUtc is not null)
            return TypedResults.Conflict(new { error = "این انتساب قبلاً پایان یافته است." });

        var endOnUtc = request?.EndOnUtc ?? timeProvider.GetUtcNow().UtcDateTime;
        var violation = HrRules.ValidateAssignmentWindow(assignment.StartOnUtc, endOnUtc);
        if (violation != HrRuleViolation.None)
            return BadRequest("تاریخ پایان انتساب نمی‌تواند قبل از شروع آن باشد.");

        assignment.Status = Domain.CompanyEmployeeAssignmentStatus.Ended;
        assignment.EndOnUtc = endOnUtc;
        assignment.EndedByUserId = scope!.UserId;
        database.Entry(assignment).Property(current => current.EmployeeId).IsModified = true;

        database.SecurityAuditEvents.Add(new Domain.SecurityAuditEvent
        {
            UserId = scope.UserId,
            TenantId = companyId,
            EventType = "HrAssignmentEnded",
            EntityType = "CompanyEmployeeAssignment",
            EntityId = assignment.Id.ToString(),
            OccurredAtUtc = timeProvider.GetUtcNow().UtcDateTime,
            CorrelationId = System.Diagnostics.Activity.Current?.Id,
            IpAddress = httpContext.Connection.RemoteIpAddress?.ToString(),
            DetailsJson = JsonSerializer.Serialize(new { assignment.EmployeeId, endOnUtc }),
        });

        try
        {
            await database.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            SetNoStore(httpContext);
            return TypedResults.Conflict(new { error = "این انتساب هم‌زمان توسط کاربر دیگری تغییر کرده است؛ اطلاعات را تازه‌سازی کنید." });
        }

        SetNoStore(httpContext);
        return TypedResults.NoContent();
    }

    /// <summary>
    /// Transfers the employee in one atomic step: the active assignment ends (now) and a
    /// new one starts with the submitted unit/position/manager; a <c>null</c> field keeps
    /// the previous value. History rows are never rewritten.
    /// </summary>
    public static async Task<IResult> ChangeAssignmentAsync(
        Guid companyId,
        Guid employeeId,
        HttpContext httpContext,
        ClaimsPrincipal principal,
        [FromBody] HrChangeAssignmentRequest request,
        [FromServices] IAntiforgery antiforgery,
        Data.DatabaseContext database,
        [FromServices] Infrastructure.Tenancy.CompanyAccessService companyAccess,
        [FromServices] TimeProvider timeProvider,
        CancellationToken cancellationToken = default)
    {
        var (failure, scope) = await AuthorizeHrScopeAsync(
            database, companyAccess, principal, companyId, ManageAssignmentsPermission, cancellationToken);
        if (failure is not null) return failure;
        if (!await antiforgery.IsRequestValidAsync(httpContext))
            return TypedResults.Problem(title: "درخواست نامعتبر است.", statusCode: StatusCodes.Status400BadRequest);
        if (!await IsActiveCompanyAsync(database, companyId, cancellationToken)) return NotFoundForCompany();

        await using var transaction = database.Database.IsRelational()
            ? await database.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            : null;

        var employee = await database.CompanyEmployees
            .FirstOrDefaultAsync(current => current.Id == employeeId && current.TenantId == companyId,
                cancellationToken);
        if (employee is null) return NotFoundForCompany();
        if (employee.EmploymentStatus == Domain.CompanyEmployeeEmploymentStatus.Terminated)
            return TypedResults.Conflict(new { error = "کارمند خاتمه‌همکاری قابل انتقال نیست." });

        var active = await database.CompanyEmployeeAssignments
            .FirstOrDefaultAsync(assignment => assignment.TenantId == companyId &&
                                               assignment.EmployeeId == employeeId &&
                                               assignment.Status == Domain.CompanyEmployeeAssignmentStatus.Active &&
                                               assignment.EndOnUtc == null, cancellationToken);
        if (active is null)
            return TypedResults.Conflict(new { error = "این کارمند انتساب فعالی ندارد؛ ابتدا انتساب جدید ثبت کنید." });

        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var startRequest = new HrStartAssignmentRequest(
            employeeId,
            request.OrganizationalUnitId ?? active.OrganizationalUnitId,
            request.PositionId ?? active.PositionId,
            request.ManagerEmployeeId ?? active.ManagerEmployeeId,
            nowUtc,
            EndActive: false);
        var startResult = await StartAssignmentCoreAsync(
            database, scope!, companyId, startRequest, endActive: true,
            httpContext, timeProvider, cancellationToken, knownActiveAssignment: active);
        if (startResult.Error is not null)
            return startResult.Error;

        try
        {
            await database.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            SetNoStore(httpContext);
            return TypedResults.Conflict(new { error = "ساختار سازمانی هم‌زمان توسط کاربر دیگری تغییر کرده است؛ اطلاعات را تازه‌سازی کنید." });
        }
        catch (DbUpdateException)
        {
            return TypedResults.Conflict(new { error = "این کارمند در همین لحظه انتساب فعال دیگری گرفته است؛ اطلاعات را تازه‌سازی کنید." });
        }

        SetNoStore(httpContext);
        return TypedResults.Ok(new HrAssignmentCreatedResponse(startResult.Assignment!.Id));
    }

    /// <summary>
    /// The shared core of starting an assignment: validates the employee, the unit, the
    /// position and the manager inside the caller's transaction, optionally ends the
    /// current active assignment (the transfer flow) and stages the new history row.
    /// </summary>
    private static async Task<(Domain.CompanyEmployeeAssignment? Assignment, IResult? Error)> StartAssignmentCoreAsync(
        Data.DatabaseContext database,
        HrScope scope,
        Guid companyId,
        HrStartAssignmentRequest request,
        bool endActive,
        HttpContext httpContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken,
        Domain.CompanyEmployeeAssignment? knownActiveAssignment = null)
    {
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var employee = await database.CompanyEmployees
            .FirstOrDefaultAsync(current => current.Id == request.EmployeeId && current.TenantId == companyId,
                cancellationToken);
        if (employee is null)
            return (null, NotFoundForCompany());
        if (employee.EmploymentStatus == Domain.CompanyEmployeeEmploymentStatus.Terminated)
            return (null, TypedResults.Conflict(new { error = "کارمند خاتمه‌همکاری قابل انتساب نیست." }));

        // Effective-dating controls (phase 4): the model stages at most one active
        // assignment, so future-dated starts are refused and a start before the recorded
        // hire date would contradict the employment history.
        var startViolation = HrRules.ValidateAssignmentStart(request.StartOnUtc, nowUtc, employee.HiredOnUtc);
        if (startViolation == HrRuleViolation.AssignmentStartInFuture)
            return (null, BadRequest("انتساب آینده‌نگر پشتیبانی نمی‌شود؛ انتساب را در زمان اجرا ثبت کنید."));
        if (startViolation == HrRuleViolation.AssignmentStartBeforeHire)
            return (null, BadRequest("شروع انتساب نمی‌تواند قبل از تاریخ شروع همکاری باشد."));

        var violation = HrRules.ValidateAssignmentWindow(request.StartOnUtc, null);
        if (violation != HrRuleViolation.None)
            return (null, BadRequest("تاریخ شروع انتساب معتبر نیست."));

        if (request.OrganizationalUnitId is not null)
        {
            var unit = await database.CompanyOrganizationalUnits.AsNoTracking()
                .Where(current => current.Id == request.OrganizationalUnitId && current.TenantId == companyId)
                .Select(current => new { current.IsActive, current.ValidFromUtc, current.ValidToUtc })
                .FirstOrDefaultAsync(cancellationToken);
            if (unit is null || !unit.IsActive || !IsWithinValidity(unit.ValidFromUtc, unit.ValidToUtc, nowUtc))
                return (null, TypedResults.Conflict(new { error = "واحد سازمانی باید فعال، هم‌شرکت و در بازه اعتبار باشد." }));
        }

        if (request.PositionId is not null)
        {
            var position = await database.CompanyPositions.AsNoTracking()
                .Where(current => current.Id == request.PositionId && current.TenantId == companyId && current.IsActive)
                .Select(current => new { current.Id })
                .FirstOrDefaultAsync(cancellationToken);
            if (position is null)
                return (null, TypedResults.Conflict(new { error = "سمت سازمانی باید فعال و متعلق به همین شرکت باشد." }));
        }

        if (request.ManagerEmployeeId is not null)
        {
            var manager = await database.CompanyEmployees.AsNoTracking()
                .Where(current => current.Id == request.ManagerEmployeeId && current.TenantId == companyId &&
                                  current.EmploymentStatus == Domain.CompanyEmployeeEmploymentStatus.Active)
                .Select(current => new { current.Id })
                .FirstOrDefaultAsync(cancellationToken);
            if (manager is null)
                return (null, TypedResults.Conflict(new { error = "مدیر مستقیم باید کارمند فعال همان شرکت باشد." }));
        }

        var activeAssignment = knownActiveAssignment ?? await database.CompanyEmployeeAssignments
            .FirstOrDefaultAsync(assignment => assignment.TenantId == companyId &&
                                               assignment.EmployeeId == request.EmployeeId &&
                                               assignment.Status == Domain.CompanyEmployeeAssignmentStatus.Active &&
                                               assignment.EndOnUtc == null, cancellationToken);
        if (activeAssignment is not null)
        {
            if (!endActive)
                return (null, TypedResults.Conflict(new { error = "این کارمند انتساب فعالی دارد؛ انتساب جدید فقط با پایان آن ثبت می‌شود." }));
            if (request.StartOnUtc <= activeAssignment.StartOnUtc)
                return (null, TypedResults.Conflict(new { error = "شروع انتساب جدید باید بعد از شروع انتساب جاری باشد." }));

            activeAssignment.Status = Domain.CompanyEmployeeAssignmentStatus.Ended;
            activeAssignment.EndOnUtc = request.StartOnUtc;
            activeAssignment.EndedByUserId = scope.UserId;
            database.Entry(activeAssignment).Property(current => current.EmployeeId).IsModified = true;
        }

        if (request.ManagerEmployeeId is not null)
        {
            // The manager chain is walked on the fresh rows of this transaction; a
            // concurrent change of an active assignment breaks this save via rowversion.
            var activeAssignments = await database.CompanyEmployeeAssignments.AsNoTracking()
                .Where(assignment => assignment.TenantId == companyId &&
                                     assignment.Status == Domain.CompanyEmployeeAssignmentStatus.Active &&
                                     assignment.EndOnUtc == null &&
                                     assignment.ManagerEmployeeId != null)
                .Select(assignment => new { assignment.EmployeeId, assignment.ManagerEmployeeId })
                .ToListAsync(cancellationToken);
            var managerRows = activeAssignments
                .Select(row => (row.EmployeeId, row.ManagerEmployeeId))
                .ToList();
            var chain = BuildManagerChain(managerRows, request.ManagerEmployeeId.Value);
            var managerViolation = HrRules.ValidateManagerChain(request.EmployeeId, request.ManagerEmployeeId, chain);
            if (managerViolation == HrRuleViolation.ManagerCycleTooDeep)
                return (null, TypedResults.Conflict(new { error = "زنجیره مدیریتی از حد مجاز گذشته است." }));
            if (managerViolation == HrRuleViolation.SelfManagement)
                return (null, TypedResults.Conflict(new { error = "یک کارمند نمی‌تواند مدیر مستقیم خودش باشد." }));
            if (managerViolation != HrRuleViolation.None)
                return (null, TypedResults.Conflict(new { error = "انتخاب این مدیر، چرخه مدیریتی می‌سازد." }));
        }

        var assignment = new Domain.CompanyEmployeeAssignment
        {
            TenantId = companyId,
            EmployeeId = request.EmployeeId,
            OrganizationalUnitId = request.OrganizationalUnitId,
            PositionId = request.PositionId,
            ManagerEmployeeId = request.ManagerEmployeeId,
            StartOnUtc = request.StartOnUtc,
            Status = Domain.CompanyEmployeeAssignmentStatus.Active,
            CreatedAtUtc = nowUtc,
            CreatedByUserId = scope.UserId,
        };
        database.CompanyEmployeeAssignments.Add(assignment);

        database.SecurityAuditEvents.Add(new Domain.SecurityAuditEvent
        {
            UserId = scope.UserId,
            TenantId = companyId,
            EventType = "HrAssignmentStarted",
            EntityType = "CompanyEmployeeAssignment",
            EntityId = assignment.Id.ToString(),
            OccurredAtUtc = nowUtc,
            CorrelationId = System.Diagnostics.Activity.Current?.Id,
            IpAddress = httpContext.Connection.RemoteIpAddress?.ToString(),
            DetailsJson = JsonSerializer.Serialize(new
            {
                assignment.EmployeeId,
                assignment.OrganizationalUnitId,
                assignment.PositionId,
                assignment.ManagerEmployeeId,
                endedPreviousAssignment = activeAssignment is not null,
            }),
        });

        return (assignment, null);
    }

    // ------------------------------------------------- operational workflows (phase 4)

    /// <summary>
    /// The subtree of one unit (phase 4 workflow): every descendant of the routed unit with
    /// its active assignment count. The walk is bounded and cycle-safe, so even a manually
    /// corrupted parent chain can never hang or loop the response.
    /// </summary>
    public static async Task<IResult> GetUnitSubtreeAsync(
        Guid companyId,
        Guid unitId,
        HttpContext httpContext,
        ClaimsPrincipal principal,
        Data.DatabaseContext database,
        [FromServices] Infrastructure.Tenancy.CompanyAccessService companyAccess,
        CancellationToken cancellationToken = default)
    {
        var (failure, _) = await AuthorizeHrScopeAsync(
            database, companyAccess, principal, companyId, ListUnitsPermission, cancellationToken);
        if (failure is not null) return failure;
        if (!await IsActiveCompanyAsync(database, companyId, cancellationToken)) return NotFoundForCompany();

        // The same stable Code/Id order as the units list (D-072), so the rendered tree is
        // deterministic regardless of the storage engine's row order.
        var units = await database.CompanyOrganizationalUnits.AsNoTracking()
            .Where(current => current.TenantId == companyId)
            .OrderBy(current => current.Code)
            .ThenBy(current => current.Id)
            .Select(current => new HrUnitItem(
                current.Id, current.Code, current.Name, current.Description,
                current.ParentUnitId, current.IsActive, current.ValidFromUtc, current.ValidToUtc))
            .ToListAsync(cancellationToken);
        if (units.All(current => current.Id != unitId)) return NotFoundForCompany();

        var childrenByParent = units
            .Where(current => current.ParentUnitId is not null)
            .GroupBy(current => current.ParentUnitId!.Value)
            .ToDictionary(group => group.Key, group => group.ToList());

        var subtreeIds = new List<Guid>();
        var queue = new System.Collections.Generic.Queue<(Guid Id, int Depth)>();
        queue.Enqueue((unitId, 0));
        var visited = new HashSet<Guid> { unitId };
        while (queue.Count > 0)
        {
            var (currentId, depth) = queue.Dequeue();
            subtreeIds.Add(currentId);
            if (depth >= HrRules.MaxChainDepth)
                continue;
            if (!childrenByParent.TryGetValue(currentId, out var children))
                continue;
            foreach (var child in children)
            {
                if (visited.Add(child.Id))
                    queue.Enqueue((child.Id, depth + 1));
            }
        }

        var counts = await database.CompanyEmployeeAssignments.AsNoTracking()
            .Where(assignment => assignment.TenantId == companyId &&
                                 assignment.Status == Domain.CompanyEmployeeAssignmentStatus.Active &&
                                 assignment.EndOnUtc == null &&
                                 assignment.OrganizationalUnitId != null &&
                                 subtreeIds.Contains(assignment.OrganizationalUnitId.Value))
            .GroupBy(assignment => assignment.OrganizationalUnitId!.Value)
            .Select(group => new { UnitId = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);
        var countByUnit = counts.ToDictionary(row => row.UnitId, row => row.Count);

        HrUnitSubtreeNode BuildNode(HrUnitItem unit, int depth) => new(
            unit.Id,
            unit.Code,
            unit.Name,
            unit.IsActive,
            unit.ParentUnitId,
            countByUnit.TryGetValue(unit.Id, out var count) ? count : 0,
            depth >= HrRules.MaxChainDepth
                ? []
                : childrenByParent.TryGetValue(unit.Id, out var children)
                    ? children.Select(child => BuildNode(child, depth + 1)).ToArray()
                    : []);

        var root = units.Single(current => current.Id == unitId);
        SetNoStore(httpContext);
        return TypedResults.Ok(BuildNode(root, 0) with
        {
            ValidFromUtc = ToUtc(root.ValidFromUtc),
            ValidToUtc = ToUtc(root.ValidToUtc),
        });
    }

    /// <summary>
    /// The employees actively assigned to one unit (phase 4 workflow), with their position
    /// and the effective start of the assignment.
    /// </summary>
    public static async Task<IResult> ListUnitEmployeesAsync(
        Guid companyId,
        Guid unitId,
        HttpContext httpContext,
        ClaimsPrincipal principal,
        Data.DatabaseContext database,
        [FromServices] Infrastructure.Tenancy.CompanyAccessService companyAccess,
        int? page = null,
        int? pageSize = null,
        CancellationToken cancellationToken = default)
    {
        var (failure, _) = await AuthorizeHrScopeAsync(
            database, companyAccess, principal, companyId, ListUnitsPermission, cancellationToken);
        if (failure is not null) return failure;
        if (!await IsActiveCompanyAsync(database, companyId, cancellationToken)) return NotFoundForCompany();
        if (!await database.CompanyOrganizationalUnits.AsNoTracking()
                .AnyAsync(current => current.Id == unitId && current.TenantId == companyId, cancellationToken))
            return NotFoundForCompany();

        var normalizedPage = Math.Clamp(page ?? 1, 1, MaxPage);
        var normalizedPageSize = Math.Clamp(pageSize ?? 25, 1, MaxPageSize);

        var query = database.CompanyEmployeeAssignments.AsNoTracking()
            .Where(assignment => assignment.TenantId == companyId &&
                                 assignment.OrganizationalUnitId == unitId &&
                                 assignment.Status == Domain.CompanyEmployeeAssignmentStatus.Active &&
                                 assignment.EndOnUtc == null);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(assignment => assignment.Employee.LastName)
            .ThenBy(assignment => assignment.Employee.FirstName)
            .ThenBy(assignment => assignment.EmployeeId)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .Select(assignment => new HrUnitEmployeeItem(
                assignment.EmployeeId,
                assignment.Employee.EmployeeNumber,
                assignment.Employee.FirstName,
                assignment.Employee.LastName,
                assignment.PositionId,
                assignment.Position != null ? assignment.Position.Name : null,
                assignment.StartOnUtc))
            .ToListAsync(cancellationToken);

        SetNoStore(httpContext);
        var utcItems = items.Select(item => item with { StartOnUtc = ToUtc(item.StartOnUtc) }).ToArray();
        return TypedResults.Ok(new HrUnitEmployeeListResponse(utcItems, totalCount, normalizedPage, normalizedPageSize));
    }

    /// <summary>
    /// The employees actively holding one position across the company (phase 4 workflow);
    /// a position can be reused by several units, so the unit comes with each row.
    /// </summary>
    public static async Task<IResult> ListPositionEmployeesAsync(
        Guid companyId,
        Guid positionId,
        HttpContext httpContext,
        ClaimsPrincipal principal,
        Data.DatabaseContext database,
        [FromServices] Infrastructure.Tenancy.CompanyAccessService companyAccess,
        int? page = null,
        int? pageSize = null,
        CancellationToken cancellationToken = default)
    {
        var (failure, _) = await AuthorizeHrScopeAsync(
            database, companyAccess, principal, companyId, ListPositionsPermission, cancellationToken);
        if (failure is not null) return failure;
        if (!await IsActiveCompanyAsync(database, companyId, cancellationToken)) return NotFoundForCompany();
        if (!await database.CompanyPositions.AsNoTracking()
                .AnyAsync(current => current.Id == positionId && current.TenantId == companyId, cancellationToken))
            return NotFoundForCompany();

        var normalizedPage = Math.Clamp(page ?? 1, 1, MaxPage);
        var normalizedPageSize = Math.Clamp(pageSize ?? 25, 1, MaxPageSize);

        var query = database.CompanyEmployeeAssignments.AsNoTracking()
            .Where(assignment => assignment.TenantId == companyId &&
                                 assignment.PositionId == positionId &&
                                 assignment.Status == Domain.CompanyEmployeeAssignmentStatus.Active &&
                                 assignment.EndOnUtc == null);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(assignment => assignment.Employee.LastName)
            .ThenBy(assignment => assignment.Employee.FirstName)
            .ThenBy(assignment => assignment.EmployeeId)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .Select(assignment => new HrPositionEmployeeItem(
                assignment.EmployeeId,
                assignment.Employee.EmployeeNumber,
                assignment.Employee.FirstName,
                assignment.Employee.LastName,
                assignment.OrganizationalUnitId,
                assignment.OrganizationalUnit != null ? assignment.OrganizationalUnit.Name : null,
                assignment.StartOnUtc))
            .ToListAsync(cancellationToken);

        SetNoStore(httpContext);
        var utcItems = items.Select(item => item with { StartOnUtc = ToUtc(item.StartOnUtc) }).ToArray();
        return TypedResults.Ok(new HrPositionEmployeeListResponse(utcItems, totalCount, normalizedPage, normalizedPageSize));
    }

    /// <summary>
    /// The change history of one employee (phase 4 workflow): the allow-listed audit trail
    /// of the employee rows and of every assignment row of that employee. Only employees of
    /// the routed company are visible; a foreign id is the same 404 as an unknown one.
    /// </summary>
    public static async Task<IResult> GetEmployeeHistoryAsync(
        Guid companyId,
        Guid employeeId,
        HttpContext httpContext,
        ClaimsPrincipal principal,
        Data.DatabaseContext database,
        [FromServices] Infrastructure.Tenancy.CompanyAccessService companyAccess,
        int? page = null,
        int? pageSize = null,
        CancellationToken cancellationToken = default)
    {
        var (failure, _) = await AuthorizeHrScopeAsync(
            database, companyAccess, principal, companyId, ViewEmployeesPermission, cancellationToken);
        if (failure is not null) return failure;
        if (!await IsActiveCompanyAsync(database, companyId, cancellationToken)) return NotFoundForCompany();
        if (!await database.CompanyEmployees.AsNoTracking()
                .AnyAsync(current => current.Id == employeeId && current.TenantId == companyId, cancellationToken))
            return NotFoundForCompany();

        var normalizedPage = Math.Clamp(page ?? 1, 1, MaxPage);
        var normalizedPageSize = Math.Clamp(pageSize ?? 25, 1, MaxPageSize);

        var assignmentIdStrings = await database.CompanyEmployeeAssignments.AsNoTracking()
            .Where(assignment => assignment.TenantId == companyId && assignment.EmployeeId == employeeId)
            .Select(assignment => assignment.Id.ToString())
            .ToListAsync(cancellationToken);

        var employeeEvents = database.SecurityAuditEvents.AsNoTracking()
            .Where(securityEvent => securityEvent.TenantId == companyId &&
                                    securityEvent.EntityType == "CompanyEmployee" &&
                                    securityEvent.EntityId == employeeId.ToString())
            .Select(securityEvent => new
            {
                securityEvent.EventType,
                securityEvent.EntityType,
                securityEvent.EntityId,
                securityEvent.OccurredAtUtc,
                securityEvent.UserId,
                securityEvent.CorrelationId,
            });
        var assignmentEvents = database.SecurityAuditEvents.AsNoTracking()
            .Where(securityEvent => securityEvent.TenantId == companyId &&
                                    securityEvent.EntityType == "CompanyEmployeeAssignment" &&
                                    assignmentIdStrings.Contains(securityEvent.EntityId))
            .Select(securityEvent => new
            {
                securityEvent.EventType,
                securityEvent.EntityType,
                securityEvent.EntityId,
                securityEvent.OccurredAtUtc,
                securityEvent.UserId,
                securityEvent.CorrelationId,
            });

        var totalCount = await employeeEvents.CountAsync(cancellationToken)
                         + await assignmentEvents.CountAsync(cancellationToken);
        // OccurredAtUtc alone is not a stable page boundary when several rows share one
        // transaction timestamp; the entity id is the deterministic tie-breaker.
        var merged = await employeeEvents
            .Concat(assignmentEvents)
            .OrderByDescending(securityEvent => securityEvent.OccurredAtUtc)
            .ThenByDescending(securityEvent => securityEvent.EntityId)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync(cancellationToken);

        SetNoStore(httpContext);
        var items = merged.Select(securityEvent => new HrEmployeeHistoryItem(
            securityEvent.EventType,
            securityEvent.EntityType,
            securityEvent.EntityId,
            ToUtc(securityEvent.OccurredAtUtc),
            securityEvent.UserId,
            securityEvent.CorrelationId)).ToArray();
        return TypedResults.Ok(new HrEmployeeHistoryResponse(items, totalCount, normalizedPage, normalizedPageSize));
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>
    /// Dual-mode authorization for one routed company (D-071): the installation administrator
    /// passes without a membership; a company actor must prove an active membership plus the
    /// routed <c>hr.*</c> permission. Anonymous callers get 401, callers without any
    /// relationship to the company get the same 404 as an unknown company (no disclosure),
    /// and members without the permission get an explicit 403.
    /// </summary>
    private static async Task<(IResult? Failure, HrScope? Scope)> AuthorizeHrScopeAsync(
        Data.DatabaseContext database,
        Infrastructure.Tenancy.CompanyAccessService companyAccess,
        ClaimsPrincipal principal,
        Guid companyId,
        string permissionCode,
        CancellationToken cancellationToken)
    {
        var userId = Infrastructure.Tenancy.CompanyAccessService.GetUserId(principal);
        if (userId is null)
            return (TypedResults.Unauthorized(), null);

        if (await UserAdministrationEndpoints.IsInstallationAdministratorAsync(userId.Value, database, cancellationToken))
            return (null, new HrScope(userId.Value, true));

        var access = await companyAccess.FindAccessibleCompanyAsync(userId.Value, companyId, cancellationToken);
        if (access is null)
            return (TypedResults.NotFound(), null);

        if (!await companyAccess.HasPermissionAsync(userId.Value, companyId, permissionCode, cancellationToken))
            return (TypedResults.Json(
                new { error = "شما مجاز به این عملیات در این شرکت نیستید." },
                statusCode: StatusCodes.Status403Forbidden), null);

        return (null, new HrScope(userId.Value, false));
    }

    private static async Task<bool> IsActiveCompanyAsync(
        Data.DatabaseContext database,
        Guid companyId,
        CancellationToken cancellationToken) =>
        companyId != Guid.Empty && await database.Companies.AsNoTracking()
            .AnyAsync(company => company.Id == companyId && company.IsActive, cancellationToken);

    private static IResult NotFoundForCompany() => TypedResults.NotFound();

    private static IResult BadRequest(string message) =>
        TypedResults.BadRequest(new { error = message });

    private static bool IsWithinValidity(DateTime? validFromUtc, DateTime? validToUtc, DateTime nowUtc) =>
        (validFromUtc is null || validFromUtc <= nowUtc) &&
        (validToUtc is null || validToUtc >= nowUtc);

    /// <summary>
    /// Applies the submitted concurrency token of a PUT body. An invalid or malformed
    /// token is a bad request; a stale token fails the save with 409.
    /// </summary>
    private static bool TryApplyRowVersion(
        Data.DatabaseContext database, object tracked, string? rowVersion, out string? error)
    {
        error = null;
        if (rowVersion is null)
        {
            error = "نشانه هم‌زمانی (rowVersion) الزامی است.";
            return false;
        }

        byte[] token;
        try
        {
            token = Convert.FromBase64String(rowVersion);
        }
        catch (FormatException)
        {
            error = "نشانه هم‌زمانی (rowVersion) معتبر نیست.";
            return false;
        }

        // An empty token never matches a real SQL Server rowversion, so a client that
        // skips the token still fails closed with 409 instead of overwriting blindly.
        database.Entry(tracked).Property("RowVersion").OriginalValue = token;
        return true;
    }

    private static List<HrUnitFact> BuildAncestorFacts(List<HrUnitFact> units)
    {
        // The rule walks the chain through the parent links of the facts themselves, so the
        // fact list is the whole company map and the walk is bounded by MaxChainDepth.
        return units;
    }

    private static List<Guid> BuildManagerChain(
        List<(Guid EmployeeId, Guid? ManagerEmployeeId)> activeAssignments,
        Guid startManagerId)
    {
        var managerOf = activeAssignments
            .Where(row => row.ManagerEmployeeId is not null)
            .GroupBy(row => row.EmployeeId)
            .ToDictionary(group => group.Key, group => group.First().ManagerEmployeeId!.Value);

        var chain = new List<Guid>();
        var current = startManagerId;
        while (current != Guid.Empty && chain.Count <= HrRules.MaxChainDepth + 1)
        {
            chain.Add(current);
            if (!managerOf.TryGetValue(current, out var next))
                break;
            current = next;
        }

        return chain;
    }

    private static IReadOnlyList<HrUnitTreeNode> BuildUnitTree(List<HrUnitItem> units)
    {
        var byParent = units
            .Where(unit => unit.ParentUnitId is not null)
            .GroupBy(unit => unit.ParentUnitId!.Value)
            .ToDictionary(group => group.Key, group => group.ToList());
        var knownIds = units.Select(unit => unit.Id).ToHashSet();

        IReadOnlyList<HrUnitTreeNode> BuildChildren(Guid? parentId, int depth)
        {
            if (depth > HrRules.MaxChainDepth)
                return [];
            IEnumerable<HrUnitItem> children = parentId is null
                ? units.Where(unit => unit.ParentUnitId is null || !knownIds.Contains(unit.ParentUnitId.Value))
                : byParent.TryGetValue(parentId.Value, out var bucket) ? bucket : [];
            return children
                .Select(unit => new HrUnitTreeNode(
                    unit.Id,
                    unit.Code,
                    unit.Name,
                    unit.Description,
                    unit.ParentUnitId,
                    unit.IsActive,
                    unit.ValidFromUtc,
                    unit.ValidToUtc,
                    BuildChildren(unit.Id, depth + 1)))
                .ToArray();
        }

        return BuildChildren(null, 0);
    }

    private static DateTime? ToUtc(DateTime? value) =>
        value is null ? null : DateTime.SpecifyKind(value.Value, DateTimeKind.Utc);

    private static string ToBase64(byte[] value) => Convert.ToBase64String(value);

    private static DateTime ToUtc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static void SetNoStore(HttpContext context)
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.Pragma = "no-cache";
    }

    private sealed record HrScope(Guid UserId, bool IsInstallationAdministrator);
}

public sealed record HrCreateEmployeeRequest(
    string? EmployeeNumber, string? FirstName, string? LastName, DateTime? HiredOnUtc);
public sealed record HrUpdateEmployeeRequest(
    string? FirstName, string? LastName, Domain.CompanyEmployeeEmploymentStatus EmploymentStatus,
    DateTime? HiredOnUtc, string? RowVersion);
public sealed record HrLinkEmployeeUserRequest(Guid UserId);
public sealed record HrCreateUnitRequest(
    string? Code, string? Name, string? Description, Guid? ParentUnitId,
    DateTime? ValidFromUtc, DateTime? ValidToUtc);
public sealed record HrUpdateUnitRequest(
    string? Name, string? Description, Guid? ParentUnitId, bool IsActive,
    DateTime? ValidFromUtc, DateTime? ValidToUtc, string? RowVersion);
public sealed record HrCreatePositionRequest(
    string? Code, string? Name, string? Description, int? Rank);
public sealed record HrUpdatePositionRequest(
    string? Name, string? Description, int? Rank, bool IsActive, string? RowVersion);
public sealed record HrStartAssignmentRequest(
    Guid EmployeeId, Guid? OrganizationalUnitId, Guid? PositionId, Guid? ManagerEmployeeId,
    DateTime StartOnUtc, bool EndActive);
public sealed record HrEndAssignmentRequest(DateTime? EndOnUtc);
public sealed record HrChangeAssignmentRequest(
    Guid? OrganizationalUnitId, Guid? PositionId, Guid? ManagerEmployeeId);

public sealed record HrEmployeeCreatedResponse(Guid Id);
public sealed record HrUnitCreatedResponse(Guid Id);
public sealed record HrPositionCreatedResponse(Guid Id);
public sealed record HrAssignmentCreatedResponse(Guid Id);

public sealed record HrEmployeeListItem(
    Guid Id, string EmployeeNumber, string FirstName, string LastName,
    Domain.CompanyEmployeeEmploymentStatus EmploymentStatus,
    DateTime? HiredOnUtc, DateTime? TerminatedOnUtc, bool HasLinkedUser,
    Guid? UnitId = null, string? UnitName = null, Guid? PositionId = null, string? PositionName = null);
public sealed record HrEmployeeListResponse(
    IReadOnlyList<HrEmployeeListItem> Items, int TotalCount, int Page, int PageSize);
public sealed record HrEmployeeDetail(
    Guid Id, string EmployeeNumber, string FirstName, string LastName,
    Domain.CompanyEmployeeEmploymentStatus EmploymentStatus,
    DateTime? HiredOnUtc, DateTime? TerminatedOnUtc,
    Guid? UserCompanyMembershipId, Guid? LinkedUserId, DateTime CreatedAtUtc,
    string RowVersion,
    HrAssignmentItem? ActiveAssignment);
public sealed record HrUnitItem(
    Guid Id, string Code, string Name, string? Description, Guid? ParentUnitId,
    bool IsActive, DateTime? ValidFromUtc, DateTime? ValidToUtc);
public sealed record HrUnitDetail(
    Guid Id, string Code, string Name, string? Description, Guid? ParentUnitId,
    bool IsActive, DateTime? ValidFromUtc, DateTime? ValidToUtc,
    int ActiveChildCount, int ActiveAssignmentCount, string RowVersion);
public sealed record HrUnitListResponse(
    IReadOnlyList<HrUnitItem> Items, int TotalCount, int Page, int PageSize);
public sealed record HrUnitTreeNode(
    Guid Id, string Code, string Name, string? Description, Guid? ParentUnitId,
    bool IsActive, DateTime? ValidFromUtc, DateTime? ValidToUtc,
    IReadOnlyList<HrUnitTreeNode> Children);
public sealed record HrUnitTreeResponse(IReadOnlyList<HrUnitTreeNode> Roots);
public sealed record HrPositionItem(
    Guid Id, string Code, string Name, string? Description, int? Rank, bool IsActive);
public sealed record HrPositionDetail(
    Guid Id, string Code, string Name, string? Description, int? Rank, bool IsActive,
    int ActiveAssignmentCount, string RowVersion);
public sealed record HrPositionListResponse(
    IReadOnlyList<HrPositionItem> Items, int TotalCount, int Page, int PageSize);
public sealed record HrAssignmentItem(
    Guid Id, Guid EmployeeId,
    Guid? OrganizationalUnitId, string? UnitCode, string? UnitName,
    Guid? PositionId, string? PositionCode, string? PositionName,
    Guid? ManagerEmployeeId, DateTime StartOnUtc, DateTime? EndOnUtc,
    Domain.CompanyEmployeeAssignmentStatus Status);
public sealed record HrAssignmentListResponse(
    IReadOnlyList<HrAssignmentItem> Items, int TotalCount, int Page, int PageSize);

public sealed record HrUnitSubtreeNode(
    Guid Id, string Code, string Name, bool IsActive, Guid? ParentUnitId,
    int ActiveAssignmentCount, IReadOnlyList<HrUnitSubtreeNode> Children,
    DateTime? ValidFromUtc = null, DateTime? ValidToUtc = null);
public sealed record HrUnitEmployeeItem(
    Guid EmployeeId, string EmployeeNumber, string FirstName, string LastName,
    Guid? PositionId, string? PositionName, DateTime StartOnUtc);
public sealed record HrUnitEmployeeListResponse(
    IReadOnlyList<HrUnitEmployeeItem> Items, int TotalCount, int Page, int PageSize);
public sealed record HrPositionEmployeeItem(
    Guid EmployeeId, string EmployeeNumber, string FirstName, string LastName,
    Guid? OrganizationalUnitId, string? OrganizationalUnitName, DateTime StartOnUtc);
public sealed record HrPositionEmployeeListResponse(
    IReadOnlyList<HrPositionEmployeeItem> Items, int TotalCount, int Page, int PageSize);
public sealed record HrEmployeeHistoryItem(
    string EventType, string EntityType, string EntityId,
    DateTime OccurredAtUtc, Guid? ActorUserId, string? CorrelationId);
public sealed record HrEmployeeHistoryResponse(
    IReadOnlyList<HrEmployeeHistoryItem> Items, int TotalCount, int Page, int PageSize);
