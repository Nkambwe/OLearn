using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using OLearn.Data;
using OLearn.Models;
using OLearn.Services;

namespace OLearn.Api;

public static class ApiEndpoints
{
    private static int UserId(ClaimsPrincipal u) => int.Parse(u.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public static void MapOLearnApi(this WebApplication app)
    {
        // Connectivity check: anonymous so it works even when the sign-in cookie has lapsed.
        app.MapGet("/api/ping", () => Results.Ok(new { ok = true, serverTime = Fmt.Iso(DateTime.UtcNow) })).AllowAnonymous();

        var api = app.MapGroup("/api").RequireAuthorization();

        api.MapGet("/catalog", async (ClaimsPrincipal user, ResourceService svc) =>
            Results.Ok(await svc.BuildCatalogAsync(UserId(user))));

        // FR-13: issue (or renew) a signed lease
        api.MapPost("/leases", async (LeaseRequest req, ClaimsPrincipal user, LeaseService leases) =>
        {
            var r = await leases.IssueAsync(UserId(user), req.ResourceId);
            if (!r.Ok) return Results.Json(new { code = r.Code, message = r.Message }, statusCode: r.HttpStatus);
            return Results.Ok(new { manifest = r.Manifest, signature = r.Signature, key = r.Key, serverTime = Fmt.Iso(DateTime.UtcNow) });
        });

        // The file is released only while the student holds an active lease for that version.
        api.MapGet("/files/{versionId:int}", async (int versionId, ClaimsPrincipal user, ApplicationDbContext db, VersionService versions) =>
        {
            var uid = UserId(user);
            var now = DateTime.UtcNow;
            var ok = await db.Leases.AnyAsync(l => l.UserId == uid && l.ResourceVersionId == versionId
                                                   && l.Status == LeaseStatus.Active && l.ExpiresAt > now);
            if (!ok) return Results.Json(new { code = "no_lease", message = "No active lease for this file." }, statusCode: 403);
            var v = await db.ResourceVersions.FindAsync(versionId);
            if (v == null || !File.Exists(versions.PathFor(v))) return Results.NotFound();
            return Results.File(versions.PathFor(v), "application/octet-stream");
        });

        api.MapPost("/sync", async (SyncRequest req, ClaimsPrincipal user, SyncService sync) =>
            Results.Ok(await sync.SyncAsync(UserId(user), req)));

        // Test mode only: lets you confirm that the C# hash chain matches the browser's (see docs/TEST_RUNBOOK.md).
        app.MapGet("/api/diag/audit-vector", (RuntimeSettings rt) =>
            !rt.TestMode ? (IResult)Results.NotFound() : Results.Ok(new
            {
                input = "device-1|1|SAVE|11111111-1111-1111-1111-111111111111|7|2026-11-17T08:00:00.000Z|x|" + AuditChain.Genesis,
                hash = AuditChain.Hash("device-1", 1, "SAVE", "11111111-1111-1111-1111-111111111111", 7, "2026-11-17T08:00:00.000Z", "x", AuditChain.Genesis)
            })).AllowAnonymous();
    }
}
