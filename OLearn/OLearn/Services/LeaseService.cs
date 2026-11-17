using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OLearn.Data;
using OLearn.Models;

namespace OLearn.Services;

public class LeaseIssueResult
{
    public bool Ok { get; init; }
    public int HttpStatus { get; init; } = 200;
    public string Code { get; init; } = "";
    public string Message { get; init; } = "";
    public string Manifest { get; init; } = "";
    public string Signature { get; init; } = "";
    public string Key { get; init; } = "";
    public static LeaseIssueResult Fail(string code, string message, int status) =>
        new() { Ok = false, Code = code, Message = message, HttpStatus = status };
}

/// <summary>Issues, renews, revokes and supersedes leases (FR-13, FR-16, FR-17, NFR-09).</summary>
public class LeaseService
{
    private readonly ApplicationDbContext _db;
    private readonly LeaseCryptoService _crypto;
    private readonly RuntimeSettings _runtime;

    public LeaseService(ApplicationDbContext db, LeaseCryptoService crypto, RuntimeSettings runtime)
    {
        _db = db; _crypto = crypto; _runtime = runtime;
    }

    public static string Effective(Lease l, DateTime now) =>
        l.Status == LeaseStatus.Active && l.ExpiresAt <= now ? LeaseStatus.Expired : l.Status;

    /// <summary>Issues a lease for the current version. Used for both a first save and a renewal.</summary>
    public async Task<LeaseIssueResult> IssueAsync(int userId, int resourceId)
    {
        var res = await _db.Resources.Include(r => r.Module).ThenInclude(m => m!.Course)
            .FirstOrDefaultAsync(r => r.ResourceId == resourceId);
        if (res == null || !res.IsActive || !res.IsAuthorised || res.Module?.Course?.IsActive != true)
            return LeaseIssueResult.Fail("not_released", "This resource has not been released for offline use.", 403);

        var ver = await _db.ResourceVersions.Where(v => v.ResourceId == resourceId)
            .OrderByDescending(v => v.ResourceVersionId).FirstOrDefaultAsync();
        if (ver == null) return LeaseIssueResult.Fail("no_version", "This resource has no file yet.", 404);

        var revoked = await _db.Leases.AnyAsync(l => l.UserId == userId && l.ResourceVersionId == ver.ResourceVersionId && l.Status == LeaseStatus.Revoked);
        if (revoked) return LeaseIssueResult.Fail("revoked", "The University has withdrawn this resource for you. Contact the administrator.", 403);

        var older = await (from l in _db.Leases
                           join v in _db.ResourceVersions on l.ResourceVersionId equals v.ResourceVersionId
                           where l.UserId == userId && v.ResourceId == resourceId
                                 && (l.Status == LeaseStatus.Active || l.Status == LeaseStatus.Expired)
                           select l).ToListAsync();
        foreach (var l in older)
            l.Status = l.ResourceVersionId == ver.ResourceVersionId ? LeaseStatus.Expired : LeaseStatus.Superseded;

        var now = DateTime.UtcNow;
        var expires = _runtime.TestMode && _runtime.TestLeaseMinutes > 0
            ? now.AddMinutes(_runtime.TestLeaseMinutes)
            : now.AddDays(Math.Clamp(res.LeaseDurationDays, 1, 90));

        var lease = new Lease { LeaseId = Guid.NewGuid(), UserId = userId, ResourceVersionId = ver.ResourceVersionId, IssuedAt = now, ExpiresAt = expires, Status = LeaseStatus.Active };
        _db.Leases.Add(lease);

        var sr = await _db.StudentResources.FirstOrDefaultAsync(s => s.UserId == userId && s.ResourceId == resourceId);
        if (sr == null) _db.StudentResources.Add(new StudentResource { UserId = userId, ResourceId = resourceId, LocalStatus = "Saved", UpdatedAt = now });
        else { sr.LocalStatus = "Saved"; sr.UpdatedAt = now; }
        await _db.SaveChangesAsync();

        // The device verifies the signature over exactly this string, so it is sent as-is.
        var manifest = JsonSerializer.Serialize(new
        {
            leaseId = lease.LeaseId,
            userId,
            resourceId,
            resourceVersionId = ver.ResourceVersionId,
            versionLabel = ver.VersionLabel,
            sha256 = ver.Checksum,
            issuedAt = Fmt.Iso(now),
            expiresAt = Fmt.Iso(expires),
            sizeBytes = ver.SizeBytes
        });
        return new LeaseIssueResult
        {
            Ok = true, Manifest = manifest, Signature = _crypto.Sign(manifest),
            Key = Convert.ToBase64String(_crypto.DeriveFileKey(userId, ver.ResourceVersionId))
        };
    }

    public async Task<bool> RevokeAsync(Guid leaseId, int adminId, string? reason)
    {
        var l = await _db.Leases.FindAsync(leaseId);
        if (l == null) return false;
        l.Status = LeaseStatus.Revoked; l.RevokedAt = DateTime.UtcNow; l.RevokedByUserId = adminId; l.RevokeReason = reason;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<int> RevokeAllForResourceAsync(int resourceId, int adminId, string? reason)
    {
        var leases = await (from l in _db.Leases
                            join v in _db.ResourceVersions on l.ResourceVersionId equals v.ResourceVersionId
                            where v.ResourceId == resourceId && (l.Status == LeaseStatus.Active || l.Status == LeaseStatus.Expired)
                            select l).ToListAsync();
        foreach (var l in leases) { l.Status = LeaseStatus.Revoked; l.RevokedAt = DateTime.UtcNow; l.RevokedByUserId = adminId; l.RevokeReason = reason; }
        await _db.SaveChangesAsync();
        return leases.Count;
    }

    /// <summary>Lets a student renew again after a revocation was lifted.</summary>
    public async Task<bool> ReinstateAsync(Guid leaseId)
    {
        var l = await _db.Leases.FindAsync(leaseId);
        if (l == null || l.Status != LeaseStatus.Revoked) return false;
        l.Status = LeaseStatus.Expired; l.RevokedAt = null; l.RevokedByUserId = null; l.RevokeReason = null;
        await _db.SaveChangesAsync();
        return true;
    }

    /// <summary>Called when a new version is published: leases of older versions become Superseded.</summary>
    public async Task SupersedeOlderAsync(int resourceId, int newVersionId)
    {
        var leases = await (from l in _db.Leases
                            join v in _db.ResourceVersions on l.ResourceVersionId equals v.ResourceVersionId
                            where v.ResourceId == resourceId && l.ResourceVersionId != newVersionId
                                  && (l.Status == LeaseStatus.Active || l.Status == LeaseStatus.Expired)
                            select l).ToListAsync();
        foreach (var l in leases) l.Status = LeaseStatus.Superseded;
        await _db.SaveChangesAsync();
    }
}
