using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Faranou.Licensing.Contracts;
using Faranou.Vendor.Host.Licensing;
using Faranou.Vendor.Persistence;
using Faranou.Vendor.Persistence.Auditing;
using Faranou.Vendor.Persistence.Domain;
using Faranou.Vendor.Persistence.Identity;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Faranou.Vendor.Host.Authorization;

/// <summary>
/// Maker-checker workflow for signed license and signed revocation artifacts.
/// Signing is performed outside the SQL transaction; the approval, artifact, license state and
/// audit records are committed atomically only after the signature has been verified.
/// </summary>
public static class VendorLicenseOperationEndpoints
{
    private static readonly JsonSerializerOptions EnvelopeJsonOptions = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapVendorLicenseOperationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var operations = endpoints.MapGroup("/api/vendor/license-operations");

        operations.MapGet("/", async (
            int? page, int? pageSize, string? status, VendorDbContext db, CancellationToken ct) =>
        {
            if (status is not null && status is not ("Pending" or "Approved" or "Rejected" or "Stale"))
                return Results.BadRequest(new { error = "invalid_license_operation_status" });

            var paging = VendorPageRequest.Normalize(page, pageSize);
            IQueryable<VendorLicenseOperationRequest> query = db.LicenseOperationRequests
                .AsNoTracking().Include(item => item.License);
            if (status is not null) query = query.Where(item => item.Status == status);
            query = query.OrderByDescending(item => item.RequestedAtUtc).ThenBy(item => item.Id);
            var totalCount = await query.CountAsync(ct);
            var rows = await query.Skip(paging.Skip).Take(paging.PageSize).ToListAsync(ct);
            var items = rows.Select(item => ToResponse(item, item.License, null)).ToArray();
            return Results.Ok(new VendorPageResponse<VendorLicenseOperationResponse>(
                items, paging.Page, paging.PageSize, totalCount));
        })
        .RequireAuthorization(VendorPermissionCatalog.PolicyFor(VendorPermissionCatalog.ApprovalsRead))
        .RequireAuthorization(VendorPermissionCatalog.PolicyFor(VendorPermissionCatalog.LicensesRead));

        operations.MapPost("/{licenseId:guid}", async (
            Guid licenseId, CreateVendorLicenseOperationRequest request, HttpContext context,
            IAntiforgery antiforgery, IAuthorizationService authorization, VendorDbContext db,
            UserManager<VendorAdminUser> users, IVendorAuditWriter audit, CancellationToken ct) =>
        {
            if (!await antiforgery.IsRequestValidAsync(context))
                return Results.BadRequest(new { error = "invalid_antiforgery_token" });
            if (licenseId == Guid.Empty ||
                request.Action is not ("Issue" or "Revoke") ||
                !VendorBusinessMutationRules.TryDecodeRowVersion(request.RowVersion, out var expectedLicenseVersion))
                return Results.BadRequest(new { error = "invalid_license_operation_request" });

            var operationPermission = PermissionFor(request.Action);
            var access = await authorization.AuthorizeAsync(
                context.User,
                new VendorResourceDescriptor("license", licenseId),
                VendorResourceCatalog.PolicyFor("license", operationPermission));
            if (!access.Succeeded) return Results.Forbid();

            var license = await db.Licenses.AsNoTracking().SingleOrDefaultAsync(item => item.Id == licenseId, ct);
            if (license is null) return Results.NotFound();
            if (!license.RowVersion.AsSpan().SequenceEqual(expectedLicenseVersion))
                return Results.Conflict(new { error = "stale_row_version" });

            var related = await LoadRelatedResourcesAsync(db, license, ct);
            if (related is null) return Results.Conflict(new { error = "license_relationship_invalid" });
            var validation = ValidateOperationTarget(request.Action, license, related);
            if (validation is not null) return Results.Conflict(new { error = validation });
            if (request.Action == "Revoke" && !await db.LicenseArtifacts.AnyAsync(item =>
                    item.LicenseId == license.Id && item.ArtifactType == "License" &&
                    item.Sequence == license.LastIssuedSequence, ct))
                return Results.Conflict(new { error = "license_issue_missing" });

            var actor = await users.GetUserAsync(context.User);
            if (actor is null) return Results.Forbid();

            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var item = new VendorLicenseOperationRequest
            {
                Id = Guid.NewGuid(),
                LicenseId = license.Id,
                Action = request.Action,
                ExpectedLicenseRowVersion = expectedLicenseVersion,
                ExpectedCustomerRowVersion = related.Customer.RowVersion.ToArray(),
                ExpectedInstallationRowVersion = related.Installation.RowVersion.ToArray(),
                ExpectedContractRowVersion = related.Contract?.RowVersion.ToArray(),
                RequestedByUserId = actor.Id,
                RequestedAtUtc = DateTimeOffset.UtcNow,
                Status = "Pending"
            };
            db.LicenseOperationRequests.Add(item);
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
            {
                return Results.Conflict(new { error = "license_operation_already_pending" });
            }

            await audit.AppendAsync(new VendorAuditWriteRequest(
                actor.Id,
                request.Action == "Issue" ? "vendor.license.issue.requested" : "vendor.license.revoke.requested",
                nameof(VendorLicenseOperationRequest), item.Id.ToString("D"), CorrelationId()), ct);
            await tx.CommitAsync(ct);
            return Results.Accepted($"/api/vendor/license-operations/{item.Id:D}",
                ToResponse(item, license, null));
        }).RequireAuthorization(VendorPermissionCatalog.PolicyFor(VendorPermissionCatalog.ApprovalsRequest));

        operations.MapPost("/{id:guid}/decision", async (
            Guid id, DecideVendorLicenseOperationRequest request, HttpContext context,
            IAntiforgery antiforgery, IAuthorizationService authorization, VendorDbContext db,
            UserManager<VendorAdminUser> users, IVendorAuditWriter audit, IVendorLicenseSigner signer,
            ITrustedKeyResolver keyResolver, ILicenseSignatureVerifier signatureVerifier,
            ILogger<VendorLicenseOperationEndpoints> logger, CancellationToken ct) =>
        {
            if (!await antiforgery.IsRequestValidAsync(context))
                return Results.BadRequest(new { error = "invalid_antiforgery_token" });
            if (id == Guid.Empty ||
                !VendorBusinessMutationRules.TryDecodeRowVersion(request.RowVersion, out var expectedApprovalVersion) ||
                request.Decision is not ("Approved" or "Rejected") ||
                request.Comment?.Length > 500)
                return Results.BadRequest(new { error = "invalid_license_operation_decision" });

            var actor = await users.GetUserAsync(context.User);
            if (actor is null) return Results.Forbid();

            var initial = await db.LicenseOperationRequests.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == id, ct);
            if (initial is null) return Results.NotFound();
            if (!initial.RowVersion.AsSpan().SequenceEqual(expectedApprovalVersion))
                return Results.Conflict(new { error = "stale_approval_version" });
            if (initial.Status != "Pending")
                return Results.Conflict(new { error = "approval_already_decided" });
            if (initial.RequestedByUserId == actor.Id)
                return Results.Forbid();
            if (initial.Action is not ("Issue" or "Revoke"))
                return Results.Conflict(new { error = "unsupported_license_operation" });

            var operationPermission = ApprovalPermissionFor(initial.Action);
            var resourceAccess = await authorization.AuthorizeAsync(
                context.User,
                new VendorResourceDescriptor("license", initial.LicenseId),
                VendorResourceCatalog.PolicyFor("license", operationPermission));
            if (!resourceAccess.Succeeded) return Results.Forbid();

            var initialLicense = await db.Licenses.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == initial.LicenseId, ct);
            if (initialLicense is null)
                return await MarkStaleAsync(db, audit, initial.Id, expectedApprovalVersion, actor.Id, "license_missing", ct);
            var initialRelated = await LoadRelatedResourcesAsync(db, initialLicense, ct);
            if (!VersionsMatch(initial, initialLicense, initialRelated))
                return await MarkStaleAsync(db, audit, initial.Id, expectedApprovalVersion, actor.Id, "related_resource_changed", ct);

            if (request.Decision == "Rejected")
                return await DecideWithoutArtifactAsync(db, audit, initial, expectedApprovalVersion,
                    actor.Id, "Rejected", NormalizeComment(request.Comment), ct);

            if (!CanExecute(initial.Action, initialLicense, initialRelated, out var targetError))
                return await MarkStaleAsync(db, audit, initial.Id, expectedApprovalVersion, actor.Id, targetError, ct);
            if (initial.Action == "Revoke" && !await db.LicenseArtifacts.AnyAsync(item =>
                    item.LicenseId == initialLicense.Id && item.ArtifactType == "License" &&
                    item.Sequence == initialLicense.LastIssuedSequence, ct))
                return await MarkStaleAsync(db, audit, initial.Id, expectedApprovalVersion, actor.Id, "license_issue_missing", ct);

            var now = DateTimeOffset.UtcNow;
            string payloadJson;
            int artifactSequence;
            LicenseEnvelope? licenseEnvelope = null;
            LicenseRevocationEnvelope? revocationEnvelope = null;
            if (initial.Action == "Issue")
            {
                if (initialLicense.LastIssuedSequence == int.MaxValue)
                    return Results.Conflict(new { error = "license_sequence_exhausted" });
                artifactSequence = initialLicense.LastIssuedSequence + 1;
                if (!TryBuildLicensePayload(initialLicense, artifactSequence, now, out var payload, out var payloadError))
                    return Results.Conflict(new { error = payloadError });
                payloadJson = CanonicalJson.SerializePayload(payload!);
            }
            else
            {
                artifactSequence = initialLicense.LastIssuedSequence;
                var payload = new LicenseRevocationPayload(
                    LicenseAlgorithms.CurrentFormatVersion,
                    initialLicense.Id.ToString("N"),
                    initialLicense.InstallationId!.Value.ToString("N"),
                    initialLicense.ProductCode,
                    artifactSequence,
                    now);
                if (StructuralValidation.ValidateRevocationPayload(payload).Count > 0)
                    return Results.Conflict(new { error = "license_revocation_payload_invalid" });
                payloadJson = CanonicalJson.SerializeRevocationPayload(payload);
            }

            VendorLicenseSignature signed;
            try
            {
                signed = await signer.SignAsync(CanonicalJson.GetUtf8Bytes(payloadJson), ct);
            }
            catch (VendorLicenseSignerUnavailableException)
            {
                await RecordSigningFailureAsync(db, audit, actor.Id, initial, "vendor.license.signer-unavailable", ct);
                return Results.Json(new { error = "license_signer_unavailable" }, statusCode: StatusCodes.Status503ServiceUnavailable);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError("Vendor license signing failed. Exception type: {ExceptionType}", exception.GetType().Name);
                await RecordSigningFailureAsync(db, audit, actor.Id, initial, "vendor.license.signer-failed", ct);
                return Results.Json(new { error = "license_signer_unavailable" }, statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            if (!TryVerifySignature(signed, payloadJson, keyResolver, signatureVerifier, out var signatureError))
            {
                logger.LogWarning("Vendor license signer returned an invalid result. Validation code: {ValidationCode}", signatureError);
                await RecordSigningFailureAsync(db, audit, actor.Id, initial, "vendor.license.signature-rejected", ct);
                return Results.Json(new { error = "license_signature_invalid" }, statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            if (initial.Action == "Issue")
            {
                licenseEnvelope = new LicenseEnvelope(
                    LicenseAlgorithms.CurrentFormatVersion, signed.Kid, signed.Algorithm, payloadJson, signed.SignatureBase64);
            }
            else
            {
                revocationEnvelope = new LicenseRevocationEnvelope(
                    LicenseAlgorithms.CurrentFormatVersion, signed.Kid, signed.Algorithm, payloadJson, signed.SignatureBase64);
            }

            var envelopeJson = initial.Action == "Issue"
                ? JsonSerializer.Serialize(licenseEnvelope, EnvelopeJsonOptions)
                : JsonSerializer.Serialize(revocationEnvelope, EnvelopeJsonOptions);
            if (envelopeJson.Length > 20000)
            {
                await RecordSigningFailureAsync(db, audit, actor.Id, initial, "vendor.license.artifact-too-large", ct);
                return Results.Json(new { error = "license_artifact_invalid" }, statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var operation = await db.LicenseOperationRequests.SingleOrDefaultAsync(item => item.Id == id, ct);
            var license = await db.Licenses.SingleOrDefaultAsync(item => item.Id == initial.LicenseId, ct);
            if (operation is null || license is null) return Results.NotFound();
            if (!operation.RowVersion.AsSpan().SequenceEqual(expectedApprovalVersion))
                return Results.Conflict(new { error = "stale_approval_version" });
            if (operation.Status != "Pending")
                return Results.Conflict(new { error = "approval_already_decided" });
            var related = await LoadRelatedResourcesAsync(db, license, ct);
            if (!VersionsMatch(operation, license, related))
            {
                operation.Status = "Stale";
                operation.ReviewedByUserId = actor.Id;
                operation.ReviewedAtUtc = DateTimeOffset.UtcNow;
                operation.ReviewComment = "resource_changed";
                db.Entry(operation).Property(item => item.RowVersion).OriginalValue = expectedApprovalVersion;
                await db.SaveChangesAsync(ct);
                await audit.AppendAsync(new VendorAuditWriteRequest(
                    actor.Id, "vendor.license.operation.stale", nameof(VendorLicenseOperationRequest),
                    operation.Id.ToString("D"), CorrelationId()), ct);
                await transaction.CommitAsync(ct);
                return Results.Conflict(new { error = "approval_resource_stale" });
            }
            if (!CanExecute(operation.Action, license, related, out var currentTargetError) ||
                operation.Action == "Revoke" && !await db.LicenseArtifacts.AnyAsync(item =>
                    item.LicenseId == license.Id && item.ArtifactType == "License" &&
                    item.Sequence == license.LastIssuedSequence, ct))
            {
                if (string.IsNullOrEmpty(currentTargetError)) currentTargetError = "license_issue_missing";
                operation.Status = "Stale";
                operation.ReviewedByUserId = actor.Id;
                operation.ReviewedAtUtc = DateTimeOffset.UtcNow;
                operation.ReviewComment = currentTargetError;
                db.Entry(operation).Property(item => item.RowVersion).OriginalValue = expectedApprovalVersion;
                await db.SaveChangesAsync(ct);
                await audit.AppendAsync(new VendorAuditWriteRequest(
                    actor.Id, "vendor.license.operation.stale", nameof(VendorLicenseOperationRequest),
                    operation.Id.ToString("D"), CorrelationId()), ct);
                await transaction.CommitAsync(ct);
                return Results.Conflict(new { error = "approval_resource_stale" });
            }

            var artifact = new VendorLicenseArtifact
            {
                Id = Guid.NewGuid(),
                LicenseId = license.Id,
                OperationRequestId = operation.Id,
                ArtifactType = operation.Action == "Issue" ? "License" : "Revocation",
                Sequence = artifactSequence,
                Kid = signed.Kid,
                Algorithm = signed.Algorithm,
                EnvelopeJson = envelopeJson,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedByUserId = actor.Id
            };
            db.LicenseArtifacts.Add(artifact);
            license.Status = operation.Action == "Issue" ? "Issued" : "Revoked";
            if (operation.Action == "Issue") license.LastIssuedSequence = artifactSequence;
            operation.Status = "Approved";
            operation.ReviewedByUserId = actor.Id;
            operation.ReviewedAtUtc = DateTimeOffset.UtcNow;
            operation.ReviewComment = NormalizeComment(request.Comment);
            db.Entry(operation).Property(item => item.RowVersion).OriginalValue = expectedApprovalVersion;
            db.Entry(license).Property(item => item.RowVersion).OriginalValue = operation.ExpectedLicenseRowVersion;

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                db.ChangeTracker.Clear();
                return await MarkStaleAsync(db, audit, id, expectedApprovalVersion, actor.Id, "concurrent_resource_change", ct);
            }
            catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
            {
                return Results.Conflict(new { error = "license_artifact_already_exists" });
            }

            await audit.AppendAsync(new VendorAuditWriteRequest(
                actor.Id, "vendor.license.operation.approved", nameof(VendorLicenseOperationRequest),
                operation.Id.ToString("D"), CorrelationId()), ct);
            await audit.AppendAsync(new VendorAuditWriteRequest(
                actor.Id,
                operation.Action == "Issue" ? "vendor.license.issued" : "vendor.license.revocation-signed",
                nameof(VendorLicenseArtifact), artifact.Id.ToString("D"), CorrelationId()), ct);
            await transaction.CommitAsync(ct);
            return Results.Ok(ToResponse(operation, license, artifact.Id));
        }).RequireAuthorization(VendorPermissionCatalog.PolicyFor(VendorPermissionCatalog.ApprovalsReview));

        var licenseArtifacts = endpoints.MapGroup("/api/vendor/licenses");
        licenseArtifacts.MapGet("/{id:guid}/artifacts/latest", async (
            Guid id, HttpContext context, IAuthorizationService authorization,
            VendorDbContext db, CancellationToken ct) =>
        {
            var access = await authorization.AuthorizeAsync(
                context.User,
                new VendorResourceDescriptor("license", id),
                VendorResourceCatalog.PolicyFor("license", VendorPermissionCatalog.LicensesRead));
            if (!access.Succeeded) return Results.Forbid();
            var artifact = await db.LicenseArtifacts.AsNoTracking()
                .Where(item => item.LicenseId == id)
                .OrderByDescending(item => item.CreatedAtUtc).ThenByDescending(item => item.Id)
                .Select(item => item.EnvelopeJson)
                .FirstOrDefaultAsync(ct);
            if (artifact is null) return Results.NotFound();
            context.Response.Headers.CacheControl = "no-store";
            return Results.Text(artifact, "application/json; charset=utf-8");
        }).RequireAuthorization(VendorPermissionCatalog.PolicyFor(VendorPermissionCatalog.LicensesRead));

        return endpoints;
    }

    private static string PermissionFor(string action) =>
        action == "Issue" ? VendorPermissionCatalog.LicensesIssue : VendorPermissionCatalog.LicensesRevoke;

    private static string ApprovalPermissionFor(string action) =>
        action == "Issue" ? VendorPermissionCatalog.LicensesIssueApprove : VendorPermissionCatalog.LicensesRevokeApprove;

    private static string CorrelationId() =>
        Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");

    private static string? NormalizeComment(string? comment) =>
        string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.GetBaseException() is SqlException { Number: 2601 or 2627 };

    private static string? ValidateOperationTarget(
        string action, VendorLicense license, RelatedLicenseResources related)
    {
        if (action == "Issue")
        {
            if (license.Status is not ("Draft" or "Revoked"))
                return "license_not_issuable";
            if (license.LastIssuedSequence == int.MaxValue)
                return "license_sequence_exhausted";
            if (related.Customer.Status != "Active")
                return "customer_not_active";
            if (related.Installation.Status != "Active")
                return "installation_not_active";
            if (!TryBuildLicensePayload(license, Math.Max(1, license.LastIssuedSequence + 1),
                    DateTimeOffset.UtcNow, out _, out var error))
                return error;
        }
        else
        {
            if (license.Status != "Issued")
                return "license_not_revokeable";
            if (license.LastIssuedSequence < 1)
                return "license_issue_missing";
        }
        return null;
    }

    private static bool CanExecute(
        string action, VendorLicense license, RelatedLicenseResources? related, out string error)
    {
        error = "license_operation_target_invalid";
        if (related is null) return false;
        var targetError = ValidateOperationTarget(action, license, related);
        if (targetError is null)
        {
            error = string.Empty;
            return true;
        }
        error = targetError;
        return false;
    }

    private static async Task<RelatedLicenseResources?> LoadRelatedResourcesAsync(
        VendorDbContext db, VendorLicense license, CancellationToken ct)
    {
        var customer = await db.Customers.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == license.CustomerId, ct);
        if (customer is null || !license.InstallationId.HasValue) return null;
        var installation = await db.Installations.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == license.InstallationId.Value &&
                item.CustomerId == license.CustomerId, ct);
        if (installation is null) return null;
        VendorContract? contract = null;
        if (license.ContractId.HasValue)
        {
            contract = await db.Contracts.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == license.ContractId.Value &&
                    item.CustomerId == license.CustomerId, ct);
            if (contract is null) return null;
        }
        return new RelatedLicenseResources(customer, installation, contract);
    }

    private static bool VersionsMatch(
        VendorLicenseOperationRequest request,
        VendorLicense license,
        RelatedLicenseResources? related) =>
        related is not null &&
        license.RowVersion.AsSpan().SequenceEqual(request.ExpectedLicenseRowVersion) &&
        related.Customer.RowVersion.AsSpan().SequenceEqual(request.ExpectedCustomerRowVersion) &&
        request.ExpectedInstallationRowVersion is not null &&
        related.Installation.RowVersion.AsSpan().SequenceEqual(request.ExpectedInstallationRowVersion) &&
        ((related.Contract is null && request.ExpectedContractRowVersion is null) ||
         (related.Contract is not null && request.ExpectedContractRowVersion is not null &&
          related.Contract.RowVersion.AsSpan().SequenceEqual(request.ExpectedContractRowVersion)));

    private static bool TryBuildLicensePayload(
        VendorLicense license, int sequence, DateTimeOffset issuedAtUtc,
        out LicensePayload? payload, out string error)
    {
        payload = null;
        error = "license_metadata_invalid";
        if (!license.InstallationId.HasValue || sequence < 1) return false;
        IReadOnlyList<string> subsystems;
        try
        {
            subsystems = VendorLicenseEntitlementRules.ParseSubsystems(license.SubsystemsJson);
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        if (!VendorLicenseEntitlementRules.TryNormalize(
                license.LicenseKind, license.MaxCompanies, subsystems, license.GraceDays,
                license.ValidFromUtc, license.ValidUntilUtc, out var entitlements))
            return false;

        var candidate = new LicensePayload(
            LicenseAlgorithms.CurrentFormatVersion,
            license.Id.ToString("N"),
            license.InstallationId.Value.ToString("N"),
            license.ProductCode,
            entitlements!.LicenseKind,
            issuedAtUtc,
            license.ValidFromUtc,
            license.ValidUntilUtc,
            entitlements.GraceDays,
            sequence,
            new LicenseLimits(entitlements.MaxCompanies, entitlements.Subsystems));
        if (StructuralValidation.ValidatePayload(candidate).Count > 0) return false;
        payload = candidate;
        error = string.Empty;
        return true;
    }

    private static bool TryVerifySignature(
        VendorLicenseSignature signature, string payloadJson,
        ITrustedKeyResolver keyResolver, ILicenseSignatureVerifier verifier, out string error)
    {
        error = "signature_result_invalid";
        if (signature is null || !LicenseAlgorithms.IsAllowed(signature.Algorithm)) return false;
        var envelope = new LicenseEnvelope(
            LicenseAlgorithms.CurrentFormatVersion, signature.Kid, signature.Algorithm,
            payloadJson, signature.SignatureBase64);
        if (StructuralValidation.ValidateEnvelopeStructure(envelope).Count > 0) return false;
        if (!keyResolver.TryResolve(signature.Kid, out var trusted) || trusted is null ||
            trusted.Retired || !string.Equals(trusted.Kid, signature.Kid, StringComparison.Ordinal) ||
            !string.Equals(trusted.Algorithm, signature.Algorithm, StringComparison.Ordinal))
        {
            error = "signing_key_untrusted";
            return false;
        }
        if (!verifier.TryVerify(envelope, trusted.PublicKeySpki, out var verificationError))
        {
            error = verificationError ?? "signature_invalid";
            return false;
        }
        error = string.Empty;
        return true;
    }

    private static async Task RecordSigningFailureAsync(
        VendorDbContext db, IVendorAuditWriter audit, Guid actorId,
        VendorLicenseOperationRequest operation, string actionCode, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await audit.AppendAsync(new VendorAuditWriteRequest(
            actorId, actionCode, nameof(VendorLicenseOperationRequest),
            operation.Id.ToString("D"), CorrelationId()), ct);
        await tx.CommitAsync(ct);
    }

    private static async Task<IResult> DecideWithoutArtifactAsync(
        VendorDbContext db, IVendorAuditWriter audit, VendorLicenseOperationRequest initial,
        byte[] expectedApprovalVersion, Guid actorId, string decision, string? comment, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var operation = await db.LicenseOperationRequests.SingleOrDefaultAsync(item => item.Id == initial.Id, ct);
        if (operation is null) return Results.NotFound();
        if (!operation.RowVersion.AsSpan().SequenceEqual(expectedApprovalVersion))
            return Results.Conflict(new { error = "stale_approval_version" });
        if (operation.Status != "Pending")
            return Results.Conflict(new { error = "approval_already_decided" });
        var license = await db.Licenses.SingleOrDefaultAsync(item => item.Id == operation.LicenseId, ct);
        var related = license is null ? null : await LoadRelatedResourcesAsync(db, license, ct);
        if (license is null || !VersionsMatch(operation, license, related))
        {
            operation.Status = "Stale";
            operation.ReviewedByUserId = actorId;
            operation.ReviewedAtUtc = DateTimeOffset.UtcNow;
            operation.ReviewComment = "related_resource_changed";
            db.Entry(operation).Property(item => item.RowVersion).OriginalValue = expectedApprovalVersion;
            await db.SaveChangesAsync(ct);
            await audit.AppendAsync(new VendorAuditWriteRequest(
                actorId, "vendor.license.operation.stale", nameof(VendorLicenseOperationRequest),
                operation.Id.ToString("D"), CorrelationId()), ct);
            await tx.CommitAsync(ct);
            return Results.Conflict(new { error = "approval_resource_stale" });
        }
        operation.Status = decision;
        operation.ReviewedByUserId = actorId;
        operation.ReviewedAtUtc = DateTimeOffset.UtcNow;
        operation.ReviewComment = comment;
        db.Entry(operation).Property(item => item.RowVersion).OriginalValue = expectedApprovalVersion;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Conflict(new { error = "stale_approval_version" });
        }
        await audit.AppendAsync(new VendorAuditWriteRequest(
            actorId, "vendor.license.operation.rejected", nameof(VendorLicenseOperationRequest),
            operation.Id.ToString("D"), CorrelationId()), ct);
        await tx.CommitAsync(ct);
        return Results.Ok(ToResponse(operation, license, null));
    }

    private static async Task<IResult> MarkStaleAsync(
        VendorDbContext db, IVendorAuditWriter audit, Guid id, byte[] expectedApprovalVersion,
        Guid actorId, string reason, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var operation = await db.LicenseOperationRequests.SingleOrDefaultAsync(item => item.Id == id, ct);
        if (operation is null) return Results.NotFound();
        if (!operation.RowVersion.AsSpan().SequenceEqual(expectedApprovalVersion))
            return Results.Conflict(new { error = "stale_approval_version" });
        if (operation.Status != "Pending")
            return Results.Conflict(new { error = "approval_already_decided" });
        operation.Status = "Stale";
        operation.ReviewedByUserId = actorId;
        operation.ReviewedAtUtc = DateTimeOffset.UtcNow;
        operation.ReviewComment = reason.Length > 500 ? reason[..500] : reason;
        db.Entry(operation).Property(item => item.RowVersion).OriginalValue = expectedApprovalVersion;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Conflict(new { error = "stale_approval_version" });
        }
        await audit.AppendAsync(new VendorAuditWriteRequest(
            actorId, "vendor.license.operation.stale", nameof(VendorLicenseOperationRequest),
            operation.Id.ToString("D"), CorrelationId()), ct);
        await tx.CommitAsync(ct);
        return Results.Conflict(new { error = "approval_resource_stale" });
    }

    private static VendorLicenseOperationResponse ToResponse(
        VendorLicenseOperationRequest item, VendorLicense license, Guid? artifactId) =>
        new(item.Id, item.LicenseId, license.LicenseNumber, license.ProductCode, item.Action,
            license.Status, item.Status, item.RequestedByUserId, item.RequestedAtUtc,
            item.ReviewedByUserId, item.ReviewedAtUtc, item.ReviewComment,
            Convert.ToBase64String(item.RowVersion), artifactId);

    private sealed record RelatedLicenseResources(
        VendorCustomer Customer, VendorInstallation Installation, VendorContract? Contract);
}

public sealed record CreateVendorLicenseOperationRequest(string? Action, string? RowVersion);

public sealed record DecideVendorLicenseOperationRequest(string? Decision, string? RowVersion, string? Comment);

public sealed record VendorLicenseOperationResponse(
    Guid Id, Guid LicenseId, string LicenseNumber, string ProductCode, string Action,
    string LicenseStatus, string Status, Guid RequestedByUserId, DateTimeOffset RequestedAtUtc,
    Guid? ReviewedByUserId, DateTimeOffset? ReviewedAtUtc, string? ReviewComment,
    string RowVersion, Guid? ArtifactId);
