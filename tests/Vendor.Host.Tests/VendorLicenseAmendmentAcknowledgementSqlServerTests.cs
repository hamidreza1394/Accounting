using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Faranou.Licensing.Contracts;
using Faranou.Vendor.Host.Authorization;
using Faranou.Vendor.Host.Signing;
using Faranou.Vendor.Persistence;
using Faranou.Vendor.Persistence.Auditing;
using Faranou.Vendor.Persistence.Domain;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

/// <summary>
/// SQL Server cycle for license amendment delivery and signed acknowledgement intake (#393,
/// S-B owner decision): delivery stamps a per-delivery nonce and the exact issued payload hash,
/// re-export is idempotent, only the first valid signed acknowledgement wins, exact signed
/// retries are idempotent and any invalid/untrusted/mismatched acknowledgement advances nothing.
/// </summary>
public sealed class VendorLicenseAmendmentAcknowledgementSqlServerTests
{
    private const string LicenseKeyId = "test-license-kid";
    private const string CustomerAckKeyId = "customer-ack-key-v1";
    private const string CustomerReference = "CUSTOMER-ACK-TEST";
    private const string InstallationCode = "INSTALL-ACK-TEST";
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task DeliveryStampsMetadataThenReExportReturnsIdenticalNonceWithoutRestamping()
    {
        await using var fixture = await AckFixture.OpenAsync();
        if (fixture is null) return;
        var ct = TestContext.Current.CancellationToken;

        var first = await fixture.DeliverAsync();
        Assert.True(first.Succeeded, first.ErrorCode);
        Assert.NotNull(first.Delivery);
        Assert.False(first.Delivery!.Repeated);
        Assert.Equal(VendorLicenseAmendmentRequest.StatusDelivered, first.Delivery.Status);
        Assert.Equal(fixture.PayloadSha256, first.Delivery.PayloadSha256);
        Assert.Equal(2, first.Delivery.Sequence);
        Assert.True(VendorLicenseAmendmentAcknowledgementRules.IsDeliveryNonceShape(first.Delivery.Nonce));
        Assert.Equal(fixture.PayloadJson, first.Delivery.PayloadJson);

        var second = await fixture.DeliverAsync();
        Assert.True(second.Succeeded, second.ErrorCode);
        Assert.NotNull(second.Delivery);
        Assert.True(second.Delivery!.Repeated);
        // Re-export of the same packet is idempotent: same bytes, same nonce, no restamp.
        Assert.Equal(first.Delivery.Nonce, second.Delivery.Nonce);
        Assert.Equal(first.Delivery.PayloadJson, second.Delivery.PayloadJson);

        fixture.Db.ChangeTracker.Clear();
        var request = await fixture.Db.LicenseAmendmentRequests.AsNoTracking().SingleAsync(ct);
        Assert.Equal(VendorLicenseAmendmentRequest.StatusDelivered, request.Status);
        Assert.Equal(first.Delivery.Nonce, request.DeliveredNonce);
        Assert.Equal(fixture.PayloadSha256, request.DeliveredPayloadSha256);
        Assert.NotNull(request.DeliveredAtUtc);
        Assert.Null(request.AcknowledgementSha256);
        Assert.Single(await fixture.Db.AuditEvents.AsNoTracking()
            .Where(item => item.ActionCode == "vendor.amendment.delivered").ToListAsync(ct));
        Assert.Single(await fixture.Db.AuditEvents.AsNoTracking()
            .Where(item => item.ActionCode == "vendor.amendment.delivery-retried").ToListAsync(ct));
    }

    [Fact]
    public async Task DeliveryIsRefusedForStatesThatNeverHeldAnEnvelope()
    {
        await using var requested = await AckFixture.OpenAsync();
        if (requested is null) return;
        await requested.Db.Database.ExecuteSqlRawAsync(
            "UPDATE VendorLicenseAmendmentRequests SET Status = 'Requested' WHERE IssuedSequence = 2",
            TestContext.Current.CancellationToken);
        var requestedResult = await requested.DeliverAsync();
        Assert.False(requestedResult.Succeeded);
        Assert.Equal("amendment_not_deliverable", requestedResult.ErrorCode);

        await using var rejected = await AckFixture.OpenAsync();
        if (rejected is null) return;
        await rejected.Db.Database.ExecuteSqlRawAsync(
            "UPDATE VendorLicenseAmendmentRequests SET Status = 'Rejected', IssuedSequence = NULL WHERE IssuedSequence = 2",
            TestContext.Current.CancellationToken);
        var rejectedResult = await rejected.DeliverAsync();
        Assert.False(rejectedResult.Succeeded);
        Assert.Equal("amendment_not_deliverable", rejectedResult.ErrorCode);
    }

    [Fact]
    public async Task CorruptedStoredEnvelopeFailsDeliveryClosed()
    {
        await using var fixture = await AckFixture.OpenAsync();
        if (fixture is null) return;
        var ct = TestContext.Current.CancellationToken;
        // A tampered payload hash must fail delivery: the exported bytes can no longer be
        // proven to be the envelope recorded at issuance.
        await fixture.Db.Database.ExecuteSqlRawAsync(
            "UPDATE VendorSignedLicenseEnvelopes SET PayloadSha256 = '" + new string('f', 64) + "' WHERE Sequence = 2", ct);

        var result = await fixture.DeliverAsync();
        Assert.False(result.Succeeded);
        Assert.Equal("amendment_envelope_invalid", result.ErrorCode);
        fixture.Db.ChangeTracker.Clear();
        var request = await fixture.Db.LicenseAmendmentRequests.AsNoTracking().SingleAsync(ct);
        Assert.Equal(VendorLicenseAmendmentRequest.StatusIssued, request.Status);
        Assert.Null(request.DeliveredAtUtc);
    }

    [Fact]
    public async Task SignedAppliedAcknowledgementMarksAppliedAndExactRetryIsIdempotent()
    {
        await using var fixture = await AckFixture.OpenAsync();
        if (fixture is null) return;
        var ct = TestContext.Current.CancellationToken;
        var delivery = await fixture.DeliverAsync();
        Assert.True(delivery.Succeeded, delivery.ErrorCode);
        var nonce = delivery.Delivery!.Nonce;

        var appliedEnvelope = fixture.CreateAcknowledgement(
            nonce, "Applied", fixture.ProcessedAtUtc, null);
        using (var response = await fixture.Client.PostAsJsonAsync(
                   $"/api/vendor/license-amendment-requests/{fixture.AmendmentId:D}/acknowledgement",
                   appliedEnvelope, ct))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<StatusBody>(ct);
            Assert.NotNull(body);
            Assert.Equal(VendorLicenseAmendmentRequest.StatusApplied, body!.Status);
            Assert.False(body.Repeated);
            Assert.True(response.Headers.CacheControl?.NoStore);
        }

        // The exact signed retry is idempotent and never rewrites the customer health stamp.
        using (var retry = await fixture.Client.PostAsJsonAsync(
                   $"/api/vendor/license-amendment-requests/{fixture.AmendmentId:D}/acknowledgement",
                   appliedEnvelope, ct))
        {
            Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
            var body = await retry.Content.ReadFromJsonAsync<StatusBody>(ct);
            Assert.NotNull(body);
            Assert.True(body!.Repeated);
        }

        fixture.Db.ChangeTracker.Clear();
        var request = await fixture.Db.LicenseAmendmentRequests.AsNoTracking().SingleAsync(ct);
        Assert.Equal(VendorLicenseAmendmentRequest.StatusApplied, request.Status);
        Assert.Equal("Applied", request.AcknowledgementResult);
        Assert.Equal(CustomerAckKeyId, request.AcknowledgementKeyId);
        Assert.Null(request.AcknowledgementErrorCode);
        Assert.NotNull(request.CustomerProcessedAtUtc);
        Assert.NotNull(request.AcknowledgedAtUtc);
        Assert.Null(request.FailureCode);
        Assert.Single(await fixture.Db.AuditEvents.AsNoTracking()
            .Where(item => item.ActionCode == "vendor.amendment.acknowledgement-confirmed").ToListAsync(ct));
        // The exact signed retry appends the retried audit event by design (same as the
        // provisioning acknowledgement intake) and never rewrites the first result.
        Assert.Single(await fixture.Db.AuditEvents.AsNoTracking()
            .Where(item => item.ActionCode == "vendor.amendment.acknowledgement-retried").ToListAsync(ct));
    }

    [Fact]
    public async Task ConflictingSignedAcknowledgementIsRejectedAndKeepsTheFirstResult()
    {
        await using var fixture = await AckFixture.OpenAsync();
        if (fixture is null) return;
        var ct = TestContext.Current.CancellationToken;
        var delivery = await fixture.DeliverAsync();
        Assert.True(delivery.Succeeded, delivery.ErrorCode);

        using (var first = await fixture.Client.PostAsJsonAsync(
                   $"/api/vendor/license-amendment-requests/{fixture.AmendmentId:D}/acknowledgement",
                   fixture.CreateAcknowledgement(delivery.Delivery!.Nonce, "Applied", fixture.ProcessedAtUtc, null), ct))
        {
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        }

        // A different signed outcome (same trust key, different result) cannot overwrite the
        // first acknowledgement: the amendment stays Applied.
        using (var conflict = await fixture.Client.PostAsJsonAsync(
                   $"/api/vendor/license-amendment-requests/{fixture.AmendmentId:D}/acknowledgement",
                   fixture.CreateAcknowledgement(delivery.Delivery.Nonce, "Rejected", fixture.ProcessedAtUtc,
                       "license_sequence_replay_or_downgrade"), ct))
        {
            Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        }

        fixture.Db.ChangeTracker.Clear();
        var request = await fixture.Db.LicenseAmendmentRequests.AsNoTracking().SingleAsync(ct);
        Assert.Equal(VendorLicenseAmendmentRequest.StatusApplied, request.Status);
        Assert.Equal("Applied", request.AcknowledgementResult);
    }

    [Fact]
    public async Task InvalidOrUntrustedOrMismatchedAcknowledgementNeverAdvancesState()
    {
        await using var fixture = await AckFixture.OpenAsync();
        if (fixture is null) return;
        var ct = TestContext.Current.CancellationToken;
        var delivery = await fixture.DeliverAsync();
        Assert.True(delivery.Succeeded, delivery.ErrorCode);
        var route = $"/api/vendor/license-amendment-requests/{fixture.AmendmentId:D}/acknowledgement";

        // Not delivered yet: the endpoint refuses before any signature validation.
        await using var undelivered = await AckFixture.OpenAsync();
        if (undelivered is null) return;
        using (var notDelivered = await undelivered.Client.PostAsJsonAsync(
                   $"/api/vendor/license-amendment-requests/{undelivered.AmendmentId:D}/acknowledgement",
                   undelivered.CreateAcknowledgement(new string('a', 64), "Applied", undelivered.ProcessedAtUtc, null), ct))
        {
            Assert.Equal(HttpStatusCode.Conflict, notDelivered.StatusCode);
        }
        undelivered.Db.ChangeTracker.Clear();
        var undeliveredRequest = await undelivered.Db.LicenseAmendmentRequests.AsNoTracking().SingleAsync(ct);
        Assert.Equal(VendorLicenseAmendmentRequest.StatusIssued, undeliveredRequest.Status);
        Assert.Null(undeliveredRequest.AcknowledgementSha256);

        // Untrusted key: rejected before any state is read (fail-closed trust store).
        using (var untrusted = await fixture.Client.PostAsJsonAsync(route, new CustomerProvisioningAcknowledgementEnvelope(
                   1, "unconfigured-customer-key", LicenseAlgorithms.EcdsaP256Sha256,
                   "{}", Convert.ToBase64String(new byte[64])), ct))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, untrusted.StatusCode);
        }

        // Signature over the wrong nonce (delivery binding mismatch) — 400, state unchanged.
        using (var mismatch = await fixture.Client.PostAsJsonAsync(route,
                   fixture.CreateAcknowledgement(new string('9', 64), "Applied", fixture.ProcessedAtUtc, null), ct))
        {
            Assert.Equal(HttpStatusCode.BadRequest, mismatch.StatusCode);
        }

        // Customer rejection with a non-allowlisted code: fail closed, nothing recorded.
        using (var unapprovedCode = await fixture.Client.PostAsJsonAsync(route,
                   fixture.CreateAcknowledgement(delivery.Delivery!.Nonce, "Rejected", fixture.ProcessedAtUtc,
                       "SqlException: database unavailable for admin@example.com"), ct))
        {
            Assert.Equal(HttpStatusCode.BadRequest, unapprovedCode.StatusCode);
        }

        fixture.Db.ChangeTracker.Clear();
        var request = await fixture.Db.LicenseAmendmentRequests.AsNoTracking().SingleAsync(ct);
        Assert.Equal(VendorLicenseAmendmentRequest.StatusDelivered, request.Status);
        Assert.Null(request.AcknowledgementSha256);
        Assert.Null(request.AcknowledgementResult);
        Assert.Null(request.FailureCode);
        Assert.Empty(await fixture.Db.AuditEvents.AsNoTracking()
            .Where(item => Microsoft.EntityFrameworkCore.EF.Functions.Like(
                item.ActionCode, "vendor.amendment.acknowledgement-%"))
            .ToListAsync(ct));
    }

    [Fact]
    public async Task CustomerRejectedAcknowledgementIsATerminalRejection()
    {
        await using var fixture = await AckFixture.OpenAsync();
        if (fixture is null) return;
        var ct = TestContext.Current.CancellationToken;
        var delivery = await fixture.DeliverAsync();
        Assert.True(delivery.Succeeded, delivery.ErrorCode);

        using (var response = await fixture.Client.PostAsJsonAsync(
                   $"/api/vendor/license-amendment-requests/{fixture.AmendmentId:D}/acknowledgement",
                   fixture.CreateAcknowledgement(delivery.Delivery!.Nonce, "Rejected", fixture.ProcessedAtUtc,
                       "license_sequence_replay_or_downgrade"), ct))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        fixture.Db.ChangeTracker.Clear();
        var request = await fixture.Db.LicenseAmendmentRequests.AsNoTracking().SingleAsync(ct);
        Assert.Equal(VendorLicenseAmendmentRequest.StatusRejected, request.Status);
        Assert.Equal("Rejected", request.AcknowledgementResult);
        Assert.Equal("license_sequence_replay_or_downgrade", request.AcknowledgementErrorCode);
        Assert.Equal("amendment_ack_rejected", request.FailureCode);
        Assert.Single(await fixture.Db.AuditEvents.AsNoTracking()
            .Where(item => item.ActionCode == "vendor.amendment.acknowledgement-rejected").ToListAsync(ct));
    }

    [Fact]
    public async Task ADeliveredAmendmentStillBlocksASecondActiveRequestForTheSameLine()
    {
        await using var fixture = await AckFixture.OpenAsync();
        if (fixture is null) return;
        var ct = TestContext.Current.CancellationToken;
        var delivery = await fixture.DeliverAsync();
        Assert.True(delivery.Succeeded, delivery.ErrorCode);

        fixture.Db.ChangeTracker.Clear();
        var original = await fixture.Db.LicenseAmendmentRequests.AsNoTracking().SingleAsync(ct);
        // Terminal states no longer block; Delivered does. Try to insert a second active request.
        var second = new VendorLicenseAmendmentRequest
        {
            Id = Guid.NewGuid(),
            OrderId = original.OrderId,
            OrderLineId = original.OrderLineId,
            CustomerId = original.CustomerId,
            LicenseId = original.LicenseId,
            Kind = original.Kind,
            Status = VendorLicenseAmendmentRequest.StatusRequested,
            RequestedByUserId = Guid.NewGuid(),
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
        fixture.Db.LicenseAmendmentRequests.Add(second);
        await Assert.ThrowsAsync<DbUpdateException>(() => fixture.Db.SaveChangesAsync(ct));
    }

    [Fact]
    public async Task DeliveryFailsClosedForLegacyAmendmentWithoutResolvableSequence()
    {
        await using var fixture = await AckFixture.OpenAsync();
        if (fixture is null) return;
        var ct = TestContext.Current.CancellationToken;

        // Simulate a legacy row issued before the migration: no IssuedSequence was ever stamped
        // and the migration backfill could not resolve it (ambiguous or missing envelope).
        await fixture.Db.Database.ExecuteSqlRawAsync(
            "UPDATE VendorLicenseAmendmentRequests SET IssuedSequence = NULL", ct);

        var result = await fixture.DeliverAsync();
        Assert.False(result.Succeeded);
        Assert.Equal("amendment_envelope_missing", result.ErrorCode);

        fixture.Db.ChangeTracker.Clear();
        var request = await fixture.Db.LicenseAmendmentRequests.AsNoTracking().SingleAsync(ct);
        Assert.Equal(VendorLicenseAmendmentRequest.StatusIssued, request.Status);
        Assert.Null(request.IssuedSequence);
        Assert.Null(request.DeliveredAtUtc);
        Assert.Null(request.DeliveredNonce);
        Assert.Empty(await fixture.Db.AuditEvents.AsNoTracking()
            .Where(item => item.ActionCode == "vendor.amendment.delivered").ToListAsync(ct));
    }

    [Fact]
    public async Task AcknowledgementWithWrongPayloadHashIsRejectedWithoutStateChange()
    {
        await using var fixture = await AckFixture.OpenAsync();
        if (fixture is null) return;
        var ct = TestContext.Current.CancellationToken;
        var delivery = await fixture.DeliverAsync();
        Assert.True(delivery.Succeeded, delivery.ErrorCode);

        // A correctly signed acknowledgement that binds to a different payload hash than the one
        // delivered must never advance the amendment: the (LicenseId, IssuedSequence) → payload
        // hash binding is the replay protection of the whole delivery cycle.
        var wrongHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("not-the-delivered-payload")))
            .ToLowerInvariant();
        using (var response = await fixture.Client.PostAsJsonAsync(
                   $"/api/vendor/license-amendment-requests/{fixture.AmendmentId:D}/acknowledgement",
                   fixture.CreateAcknowledgement(delivery.Delivery!.Nonce, "Applied", fixture.ProcessedAtUtc,
                       null, wrongHash), ct))
        {
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        fixture.Db.ChangeTracker.Clear();
        var request = await fixture.Db.LicenseAmendmentRequests.AsNoTracking().SingleAsync(ct);
        Assert.Equal(VendorLicenseAmendmentRequest.StatusDelivered, request.Status);
        Assert.Null(request.AcknowledgementSha256);
        Assert.Null(request.AcknowledgementResult);
        Assert.Null(request.FailureCode);
        Assert.Empty(await fixture.Db.AuditEvents.AsNoTracking()
            .Where(item => EF.Functions.Like(item.ActionCode, "vendor.amendment.acknowledgement-%"))
            .ToListAsync(ct));
    }

    [Fact]
    public async Task ConcurrentDuplicateAcknowledgementsAreAcceptedExactlyOnce()
    {
        await using var fixture = await AckFixture.OpenAsync();
        if (fixture is null) return;
        var ct = TestContext.Current.CancellationToken;
        var delivery = await fixture.DeliverAsync();
        Assert.True(delivery.Succeeded, delivery.ErrorCode);
        var envelope = fixture.CreateAcknowledgement(
            delivery.Delivery!.Nonce, "Applied", fixture.ProcessedAtUtc, null);
        var route = $"/api/vendor/license-amendment-requests/{fixture.AmendmentId:D}/acknowledgement";

        // Two callers posting the identical signed acknowledgement at the same time: the row
        // version keeps exactly one acceptance. The loser either observes the committed first
        // acknowledgement (idempotent retry) or loses the row-version race (conflict); in both
        // cases the final state is Applied with a single confirmed audit event.
        var firstTask = fixture.Client.PostAsJsonAsync(route, envelope, ct);
        var secondTask = fixture.Client.PostAsJsonAsync(route, envelope, ct);
        using var first = await firstTask;
        using var second = await secondTask;

        var bodies = new List<StatusBody>();
        foreach (var response in new[] { first, second })
        {
            if (response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadFromJsonAsync<StatusBody>(ct);
                Assert.NotNull(body);
                Assert.Equal(VendorLicenseAmendmentRequest.StatusApplied, body!.Status);
                bodies.Add(body);
            }
            else
            {
                Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            }
        }

        Assert.Single(bodies.Where(item => !item.Repeated));

        fixture.Db.ChangeTracker.Clear();
        var request = await fixture.Db.LicenseAmendmentRequests.AsNoTracking().SingleAsync(ct);
        Assert.Equal(VendorLicenseAmendmentRequest.StatusApplied, request.Status);
        Assert.Equal("Applied", request.AcknowledgementResult);
        Assert.NotNull(request.AcknowledgementSha256);
        Assert.NotNull(request.AcknowledgedAtUtc);
        Assert.Single(await fixture.Db.AuditEvents.AsNoTracking()
            .Where(item => item.ActionCode == "vendor.amendment.acknowledgement-confirmed").ToListAsync(ct));
    }

    [Fact]
    public async Task ConcurrentDuplicateDeliveriesStampDeliveryExactlyOnce()
    {
        await using var fixture = await AckFixture.OpenAsync();
        if (fixture is null) return;
        var ct = TestContext.Current.CancellationToken;

        await using var scopeA = fixture.Factory.Services.CreateAsyncScope();
        await using var scopeB = fixture.Factory.Services.CreateAsyncScope();

        // Two operators exporting the same amendment at the same time, each in its own
        // serializable transaction: exactly one stamps the delivery, the other either re-exports
        // the committed packet (idempotent) or loses the row-version race (stale_row_version).
        var firstTask = DeliverInScopeAsync(scopeA, fixture.AmendmentId, ct);
        var secondTask = DeliverInScopeAsync(scopeB, fixture.AmendmentId, ct);
        var results = await Task.WhenAll(firstTask, secondTask);

        Assert.Single(results.Where(item => item.Succeeded && !item.Delivery!.Repeated));
        Assert.All(
            results.Where(item => item.Succeeded),
            item =>
            {
                Assert.Equal(VendorLicenseAmendmentRequest.StatusDelivered, item.Delivery!.Status);
                Assert.Equal(fixture.PayloadSha256, item.Delivery.PayloadSha256);
            });
        Assert.All(results.Where(item => !item.Succeeded),
            item => Assert.Equal("stale_row_version", item.ErrorCode));
        var nonces = results.Where(item => item.Succeeded)
            .Select(item => item.Delivery!.Nonce).Distinct().ToList();
        Assert.Single(nonces);

        fixture.Db.ChangeTracker.Clear();
        var request = await fixture.Db.LicenseAmendmentRequests.AsNoTracking().SingleAsync(ct);
        Assert.Equal(VendorLicenseAmendmentRequest.StatusDelivered, request.Status);
        Assert.Equal(nonces[0], request.DeliveredNonce);
        Assert.Equal(fixture.PayloadSha256, request.DeliveredPayloadSha256);
        Assert.Single(await fixture.Db.AuditEvents.AsNoTracking()
            .Where(item => item.ActionCode == "vendor.amendment.delivered").ToListAsync(ct));
    }

    [Fact]
    public async Task DownMigrationIsBlockedWhenDeliveredOrAppliedRowsExist()
    {
        await using var fixture = await AckFixture.OpenAsync();
        if (fixture is null) return;
        var ct = TestContext.Current.CancellationToken;
        var delivery = await fixture.DeliverAsync();
        Assert.True(delivery.Succeeded, delivery.ErrorCode);

        // Rolling the migration back must fail closed instead of silently rewriting a delivered
        // amendment back to Issued (or failing later with an opaque constraint violation).
        var blocked = await Record.ExceptionAsync(() =>
            fixture.Db.Database.MigrateAsync("20261120135809_VendorLicenseAmendmentRequests", ct));
        Assert.NotNull(blocked);
        var sqlException = FindSqlException(blocked!);
        Assert.NotNull(sqlException);
        Assert.Equal(51000, sqlException!.Number);

        // The failed downgrade leaves the schema and the data untouched.
        var applied = await fixture.Db.Database.GetAppliedMigrationsAsync(ct);
        Assert.Contains("20261128161909_VendorLicenseAmendmentDeliveryAcknowledgement", applied);
        fixture.Db.ChangeTracker.Clear();
        var request = await fixture.Db.LicenseAmendmentRequests.AsNoTracking().SingleAsync(ct);
        Assert.Equal(VendorLicenseAmendmentRequest.StatusDelivered, request.Status);
        Assert.NotNull(request.DeliveredNonce);
    }

    [Fact]
    public async Task MigrationBackfillsSingleLegacyIssuedAmendmentAndKeepsAmbiguousOnesNull()
    {
        var baseConnection = Environment.GetEnvironmentVariable("FARANO_VENDOR_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(baseConnection))
        {
            const string message = "Set FARANO_VENDOR_TEST_SQLSERVER to a disposable local SQL Server.";
            if (string.Equals(Environment.GetEnvironmentVariable("FARANO_REQUIRE_VENDOR_SQLSERVER"), "1",
                    StringComparison.Ordinal))
            {
                Assert.Fail(message);
            }
            Assert.Skip(message);
            return;
        }

        var ct = TestContext.Current.CancellationToken;
        var databaseName = "FaranouVendorAmendMig_" + Guid.NewGuid().ToString("N");
        var adminConnectionString = new SqlConnectionStringBuilder(baseConnection) { InitialCatalog = "master" }.ConnectionString;
        var testConnectionString = new SqlConnectionStringBuilder(baseConnection) { InitialCatalog = databaseName }.ConnectionString;
        await using (var adminConnection = new SqlConnection(adminConnectionString))
        {
            await adminConnection.OpenAsync(ct);
            await using var createDatabase = adminConnection.CreateCommand();
            createDatabase.CommandText = $"CREATE DATABASE [{databaseName}]";
            await createDatabase.ExecuteNonQueryAsync(ct);
        }

        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(webHost =>
            webHost.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:VendorDb"] = testConnectionString
                })));
        var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<VendorDbContext>();
        try
        {
            await db.Database.MigrateAsync(ct);
            var licenses = SeedLegacyAmendmentGraph(db);
            await db.SaveChangesAsync(ct);
            db.ChangeTracker.Clear();

            // Downgrade to the pre-feature migration: the delivery/acknowledgement columns are
            // dropped and the seeded rows become the exact legacy shape (no IssuedSequence).
            await db.Database.MigrateAsync("20261120135809_VendorLicenseAmendmentRequests", ct);
            // Upgrade again: the backfill now runs against real legacy rows.
            await db.Database.MigrateAsync(ct);

            var rows = await db.LicenseAmendmentRequests.AsNoTracking()
                .Select(item => new { item.LicenseId, item.Status, item.IssuedSequence })
                .ToListAsync(ct);

            // License A: the single active amendment with a resolvable last-issued envelope is
            // bound to that envelope.
            Assert.Contains(rows, item => item.LicenseId == licenses.LicenseA &&
                item.Status == VendorLicenseAmendmentRequest.StatusIssued && item.IssuedSequence == 2);
            // License B: two active amendments on the same license are ambiguous; both stay NULL
            // and will fail closed at delivery instead of exporting a possibly different envelope.
            Assert.Equal(2, rows.Count(item => item.LicenseId == licenses.LicenseB && item.IssuedSequence is null));
            // License C: no envelope at all, so nothing can be resolved safely.
            Assert.Contains(rows, item => item.LicenseId == licenses.LicenseC &&
                item.Status == VendorLicenseAmendmentRequest.StatusIssued && item.IssuedSequence is null);
        }
        finally
        {
            await db.DisposeAsync();
            await scope.DisposeAsync();
            await factory.DisposeAsync();
            try
            {
                await using var adminConnection = new SqlConnection(adminConnectionString);
                await adminConnection.OpenAsync(ct);
                await using var drop = adminConnection.CreateCommand();
                drop.CommandText = $"IF EXISTS (SELECT 1 FROM sys.databases WHERE name = N'{databaseName}') DROP DATABASE [{databaseName}]";
                await drop.ExecuteNonQueryAsync(ct);
            }
            catch
            {
                // The disposable test database is best-effort cleanup; CI tears the container down.
            }
        }
    }

    private static async Task<VendorLicenseAmendmentDeliveryResult> DeliverInScopeAsync(
        AsyncServiceScope scope, Guid amendmentId, CancellationToken ct)
    {
        var db = scope.ServiceProvider.GetRequiredService<VendorDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<VendorLicenseAmendmentDeliveryService>();
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        try
        {
            var request = await db.LicenseAmendmentRequests.SingleAsync(item => item.Id == amendmentId, ct);
            var result = await service.DeliverAsync(request, Guid.NewGuid(), Guid.NewGuid().ToString("N"), ct);
            if (result.Succeeded)
            {
                await tx.CommitAsync(ct);
            }
            else
            {
                await tx.RollbackAsync(ct);
            }

            return result;
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    private static SqlException? FindSqlException(Exception exception)
    {
        if (exception is SqlException sqlException) return sqlException;
        return exception.InnerException is null ? null : FindSqlException(exception.InnerException);
    }

    /// <summary>
    /// Seeds three legacy-shaped issuance scenarios through the current model: license A holds the
    /// single active amendment of its license with an envelope, license B holds two active
    /// amendments on the same license, and license C holds one amendment without any envelope.
    /// All amendments are Issued with no IssuedSequence, exactly like rows issued before the
    /// delivery/acknowledgement feature existed.
    /// </summary>
    private static (Guid LicenseA, Guid LicenseB, Guid LicenseC) SeedLegacyAmendmentGraph(VendorDbContext db)
    {
        var now = DateTimeOffset.UtcNow;
        var customer = new VendorCustomer
        {
            Id = Guid.NewGuid(), CustomerNumber = "CUSTOMER-MIG-TEST",
            LegalName = "Migration backfill test", DisplayName = "Migration backfill test",
            Status = "Active", CreatedAtUtc = now, UpdatedAtUtc = now
        };
        var installation = new VendorInstallation
        {
            Id = Guid.NewGuid(), CustomerId = customer.Id, Customer = customer,
            InstallationCode = "INSTALL-MIG-TEST", Name = "Migration test installation",
            Environment = "Test", Status = "Active", CreatedAtUtc = now
        };

        var licenseA = NewMigrationTestLicense(customer, installation, "L-MIG-A", lastIssuedSequence: 2);
        var licenseB = NewMigrationTestLicense(customer, installation, "L-MIG-B", lastIssuedSequence: 2);
        var licenseC = NewMigrationTestLicense(customer, installation, "L-MIG-C", lastIssuedSequence: 0);

        var orderA = NewMigrationTestOrder(customer, "O-MIG-A");
        var lineA = NewMigrationTestOrderLine(orderA, licenseA);
        var orderB = NewMigrationTestOrder(customer, "O-MIG-B");
        var lineB1 = NewMigrationTestOrderLine(orderB, licenseB);
        var lineB2 = NewMigrationTestOrderLine(orderB, licenseB);
        var orderC = NewMigrationTestOrder(customer, "O-MIG-C");
        var lineC = NewMigrationTestOrderLine(orderC, licenseC);

        var amendmentA = NewMigrationTestAmendment(orderA, lineA, licenseA, customer);
        var amendmentB1 = NewMigrationTestAmendment(orderB, lineB1, licenseB, customer);
        var amendmentB2 = NewMigrationTestAmendment(orderB, lineB2, licenseB, customer);
        var amendmentC = NewMigrationTestAmendment(orderC, lineC, licenseC, customer);

        db.AddRange(customer, installation, licenseA, licenseB, licenseC, orderA, orderB, orderC);
        db.SignedLicenseEnvelopes.AddRange(
            NewMigrationTestEnvelope(licenseA, sequence: 2, now),
            NewMigrationTestEnvelope(licenseB, sequence: 2, now));
        db.LicenseAmendmentRequests.AddRange(amendmentA, amendmentB1, amendmentB2, amendmentC);
        return (licenseA.Id, licenseB.Id, licenseC.Id);
    }

    private static VendorLicense NewMigrationTestLicense(
        VendorCustomer customer, VendorInstallation installation, string licenseNumber, int lastIssuedSequence)
    {
        var now = DateTimeOffset.UtcNow;
        return new VendorLicense
        {
            Id = Guid.NewGuid(), CustomerId = customer.Id, Customer = customer,
            InstallationId = installation.Id, Installation = installation,
            LicenseNumber = licenseNumber, ProductCode = "addon.company.seat",
            Status = "Issued", LicenseKind = LicensePayload.KindPerpetual, MaxCompanies = 5,
            SubsystemsJson = "[\"finance\"]", GraceDays = 30, LastIssuedSequence = lastIssuedSequence,
            ValidFromUtc = now.AddMinutes(-1), CreatedAtUtc = now
        };
    }

    private static VendorSalesOrder NewMigrationTestOrder(VendorCustomer customer, string orderNumber) =>
        new()
        {
            Id = Guid.NewGuid(), CustomerId = customer.Id, Customer = customer,
            OrderNumber = orderNumber, TotalAmount = 100m, CurrencyCode = "EUR", CreatedAtUtc = DateTimeOffset.UtcNow
        };

    private static VendorSalesOrderLine NewMigrationTestOrderLine(VendorSalesOrder order, VendorLicense license) =>
        new()
        {
            Id = Guid.NewGuid(), OrderId = order.Id, Order = order,
            ProductCode = "addon.company.seat", Description = "additional companies",
            Quantity = 1, UnitAmount = 100m, LineAmount = 100m, LicenseId = license.Id,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

    private static VendorLicenseAmendmentRequest NewMigrationTestAmendment(
        VendorSalesOrder order, VendorSalesOrderLine line, VendorLicense license, VendorCustomer customer) =>
        new()
        {
            Id = Guid.NewGuid(), OrderId = order.Id, Order = order,
            OrderLineId = line.Id, OrderLine = line,
            CustomerId = customer.Id, Customer = customer,
            LicenseId = license.Id, License = license,
            Kind = VendorLicenseAmendmentRequest.KindMaxCompanies, RequestedMaxCompanies = 8,
            Status = VendorLicenseAmendmentRequest.StatusIssued,
            RequestedByUserId = Guid.NewGuid(), CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow
        };

    private static VendorSignedLicenseEnvelope NewMigrationTestEnvelope(
        VendorLicense license, int sequence, DateTimeOffset createdAtUtc) =>
        new()
        {
            Id = Guid.NewGuid(), LicenseId = license.Id, Sequence = sequence,
            KeyId = LicenseKeyId, Algorithm = LicenseAlgorithms.EcdsaP256Sha256,
            EnvelopeJson = JsonSerializer.Serialize(
                new LicenseEnvelope(
                    LicenseAlgorithms.CurrentFormatVersion, LicenseKeyId,
                    LicenseAlgorithms.EcdsaP256Sha256, "{}",
                    Convert.ToBase64String(new byte[64])),
                WebJson),
            PayloadSha256 = new string('a', 64), CreatedAtUtc = createdAtUtc
        };

    private sealed record StatusBody(string Status, bool Repeated);

    /// <summary>Self-contained disposable database fixture seeded directly through VendorDbContext.</summary>
    private sealed class AckFixture : IAsyncDisposable
    {
        public WebApplicationFactory<Program> Factory { get; private set; } = null!;
        public HttpClient Client { get; private set; } = null!;
        public AsyncServiceScope Scope { get; private set; }
        public VendorDbContext Db { get; private set; } = null!;
        public Guid CustomerId { get; private set; }
        public Guid InstallationId { get; private set; }
        public Guid LicenseId { get; private set; }
        public Guid AmendmentId { get; private set; }
        public Guid OrderLineId { get; private set; }
        public string PayloadJson { get; private set; } = string.Empty;
        public string PayloadSha256 { get; private set; } = string.Empty;
        public DateTimeOffset ProcessedAtUtc { get; private set; }
        private ECDsa SigningKey { get; set; } = null!;
        private string DatabaseName { get; set; } = string.Empty;
        private string AdminConnectionString { get; set; } = string.Empty;

        public async Task<VendorLicenseAmendmentDeliveryResult> DeliverAsync()
        {
            var service = Scope.ServiceProvider.GetRequiredService<VendorLicenseAmendmentDeliveryService>();
            await using var tx = await Db.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
            try
            {
                var request = await Db.LicenseAmendmentRequests.SingleAsync(
                    item => item.Id == AmendmentId, TestContext.Current.CancellationToken);
                var result = await service.DeliverAsync(
                    request, Guid.NewGuid(), Guid.NewGuid().ToString("N"), TestContext.Current.CancellationToken);
                if (result.Succeeded) await tx.CommitAsync(TestContext.Current.CancellationToken);
                else await tx.RollbackAsync(TestContext.Current.CancellationToken);
                return result;
            }
            catch
            {
                await tx.RollbackAsync(TestContext.Current.CancellationToken);
                throw;
            }
        }

        public CustomerProvisioningAcknowledgementEnvelope CreateAcknowledgement(
            string nonce, string result, DateTimeOffset processedAtUtc, string? errorCode,
            string? payloadSha256Override = null)
        {
            var acknowledgement = new CustomerProvisioningAcknowledgement(
                1, AmendmentId.ToString("D"), nonce, payloadSha256Override ?? PayloadSha256, CustomerReference,
                InstallationCode, result, processedAtUtc, errorCode);
            var json = CustomerProvisioningAcknowledgementValidator.Serialize(acknowledgement);
            var signature = SigningKey.SignData(
                Encoding.UTF8.GetBytes(json), HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
            return new CustomerProvisioningAcknowledgementEnvelope(
                1, CustomerAckKeyId, LicenseAlgorithms.EcdsaP256Sha256, json,
                Convert.ToBase64String(signature));
        }

        public static async Task<AckFixture?> OpenAsync()
        {
            var baseConnection = Environment.GetEnvironmentVariable("FARANO_VENDOR_TEST_SQLSERVER");
            if (string.IsNullOrWhiteSpace(baseConnection))
            {
                const string message = "Set FARANO_VENDOR_TEST_SQLSERVER to a disposable local SQL Server.";
                if (string.Equals(Environment.GetEnvironmentVariable("FARANO_REQUIRE_VENDOR_SQLSERVER"), "1",
                        StringComparison.Ordinal))
                {
                    Assert.Fail(message);
                }
                Assert.Skip(message);
                return null;
            }

            var now = DateTimeOffset.UtcNow;
            var databaseName = "FaranouVendorAmendAck_" + Guid.NewGuid().ToString("N");
            var adminConnectionString = new SqlConnectionStringBuilder(baseConnection) { InitialCatalog = "master" }.ConnectionString;
            var testConnectionString = new SqlConnectionStringBuilder(baseConnection) { InitialCatalog = databaseName }.ConnectionString;
            await using (var adminConnection = new SqlConnection(adminConnectionString))
            {
                await adminConnection.OpenAsync(TestContext.Current.CancellationToken);
                await using var createDatabase = adminConnection.CreateCommand();
                createDatabase.CommandText = $"CREATE DATABASE [{databaseName}]";
                await createDatabase.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            }

            var signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var payloadJson = JsonSerializer.Serialize(new
            {
                licenseId = Guid.NewGuid().ToString("D"),
                sequence = 2,
                formatVersion = LicenseAlgorithms.CurrentFormatVersion
            }, WebJson);
            var payloadSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payloadJson))).ToLowerInvariant();

            try
            {
                var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(webHost =>
                {
                    webHost.ConfigureAppConfiguration((_, configuration) =>
                        configuration.AddInMemoryCollection(new Dictionary<string, string?>
                        {
                            ["ConnectionStrings:VendorDb"] = testConnectionString,
                            ["ProvisioningAcknowledgementTrust:Keys:0:KeyId"] = CustomerAckKeyId,
                            ["ProvisioningAcknowledgementTrust:Keys:0:PublicKeySubjectPublicKeyInfoBase64"] =
                                Convert.ToBase64String(signingKey.ExportSubjectPublicKeyInfo()),
                            ["ProvisioningAcknowledgementTrust:Keys:0:Revoked"] = "false"
                        }));
                });

                var scope = factory.Services.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<VendorDbContext>();
                await db.Database.MigrateAsync(TestContext.Current.CancellationToken);

                var customer = new VendorCustomer
                {
                    Id = Guid.NewGuid(), CustomerNumber = CustomerReference,
                    LegalName = "Amendment acknowledgement test", DisplayName = "Amendment ACK test",
                    Status = "Active", CreatedAtUtc = now, UpdatedAtUtc = now
                };
                var installation = new VendorInstallation
                {
                    Id = Guid.NewGuid(), CustomerId = customer.Id, Customer = customer,
                    InstallationCode = InstallationCode, Name = "Test installation",
                    Environment = "Test", Status = "Active", CreatedAtUtc = now
                };
                var license = new VendorLicense
                {
                    Id = Guid.NewGuid(), CustomerId = customer.Id, Customer = customer,
                    InstallationId = installation.Id, Installation = installation,
                    LicenseNumber = $"L-{Guid.NewGuid():N}"[..14], ProductCode = "addon.company.seat",
                    Status = "Issued", LicenseKind = LicensePayload.KindPerpetual, MaxCompanies = 5,
                    SubsystemsJson = "[\"finance\"]", GraceDays = 30, LastIssuedSequence = 2,
                    ValidFromUtc = now.AddMinutes(-1), CreatedAtUtc = now
                };
                var order = new VendorSalesOrder
                {
                    Id = Guid.NewGuid(), CustomerId = customer.Id, Customer = customer,
                    OrderNumber = $"O-{Guid.NewGuid():N}"[..14], TotalAmount = 100m,
                    CurrencyCode = "EUR", CreatedAtUtc = now
                };
                var orderLine = new VendorSalesOrderLine
                {
                    Id = Guid.NewGuid(), OrderId = order.Id, Order = order,
                    ProductCode = "addon.company.seat", Description = "additional companies",
                    Quantity = 1, UnitAmount = 100m, LineAmount = 100m, LicenseId = license.Id,
                    CreatedAtUtc = now
                };
                var envelopeJson = JsonSerializer.Serialize(
                    new LicenseEnvelope(
                        LicenseAlgorithms.CurrentFormatVersion, LicenseKeyId,
                        LicenseAlgorithms.EcdsaP256Sha256, payloadJson,
                        Convert.ToBase64String(new byte[64])),
                    WebJson);
                var amendment = new VendorLicenseAmendmentRequest
                {
                    Id = Guid.NewGuid(), OrderId = order.Id, Order = order,
                    OrderLineId = orderLine.Id, OrderLine = orderLine,
                    CustomerId = customer.Id, Customer = customer,
                    LicenseId = license.Id, License = license,
                    Kind = VendorLicenseAmendmentRequest.KindMaxCompanies,
                    RequestedMaxCompanies = 8, Status = VendorLicenseAmendmentRequest.StatusIssued,
                    RequestedByUserId = Guid.NewGuid(), IssuedSequence = 2,
                    CreatedAtUtc = now, UpdatedAtUtc = now
                };
                db.AddRange(customer, installation, license, order, orderLine);
                db.SignedLicenseEnvelopes.Add(new VendorSignedLicenseEnvelope
                {
                    Id = Guid.NewGuid(), LicenseId = license.Id, Sequence = 2,
                    KeyId = LicenseKeyId, Algorithm = LicenseAlgorithms.EcdsaP256Sha256,
                    EnvelopeJson = envelopeJson, PayloadSha256 = payloadSha256, CreatedAtUtc = now
                });
                db.LicenseAmendmentRequests.Add(amendment);
                await db.SaveChangesAsync(TestContext.Current.CancellationToken);
                db.ChangeTracker.Clear();

                return new AckFixture
                {
                    Factory = factory,
                    Client = factory.CreateClient(),
                    Scope = scope,
                    Db = db,
                    CustomerId = customer.Id,
                    InstallationId = installation.Id,
                    LicenseId = license.Id,
                    AmendmentId = amendment.Id,
                    OrderLineId = orderLine.Id,
                    PayloadJson = payloadJson,
                    PayloadSha256 = payloadSha256,
                    ProcessedAtUtc = DateTimeOffset.UtcNow,
                    SigningKey = signingKey,
                    DatabaseName = databaseName,
                    AdminConnectionString = adminConnectionString
                };
            }
            catch
            {
                signingKey.Dispose();
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            Scope.Dispose();
            await Factory.DisposeAsync();
            SigningKey.Dispose();
            try
            {
                await using var adminConnection = new SqlConnection(AdminConnectionString);
                await adminConnection.OpenAsync(TestContext.Current.CancellationToken);
                await using var drop = adminConnection.CreateCommand();
                drop.CommandText = $"IF EXISTS (SELECT 1 FROM sys.databases WHERE name = N'{DatabaseName}') DROP DATABASE [{DatabaseName}]";
                await drop.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            }
            catch
            {
                // The disposable test database is best-effort cleanup; CI tears the container down.
            }
        }
    }
}
