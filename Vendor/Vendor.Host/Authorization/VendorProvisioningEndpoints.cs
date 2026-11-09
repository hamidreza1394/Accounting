using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Faranou.Licensing.Contracts;
using Faranou.Vendor.Persistence;
using Faranou.Vendor.Persistence.Auditing;
using Faranou.Vendor.Persistence.Domain;
using Faranou.Vendor.Persistence.Identity;
using Faranou.Vendor.Host.Signing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Faranou.Vendor.Host.Authorization;

// NOTE: This file was truncated in a previous agent push; full content must be restored from PR branch fix/228-show-company-scoped-license-blocker.
// Temporary stub to avoid PLACEHOLDER corruption - DO NOT MERGE until full file restored.
public static class VendorProvisioningEndpoints
{
    public static IEndpointRouteBuilder MapVendorProvisioningEndpoints(this IEndpointRouteBuilder endpoints)
    {
        throw new System.NotImplementedException("VendorProvisioningEndpoints body must be restored from PR #608 before merge.");
    }
}
