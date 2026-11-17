using Microsoft.EntityFrameworkCore;
using OLearn.Api;
using OLearn.Data;
using OLearn.Models;

namespace OLearn.Services;

/// <summary>One round trip per synchronisation: progress, lease statuses and the audit log (FR-06, FR-08, FR-16, FR-19).</summary>
public class SyncService
{
    private readonly ApplicationDbContext _db;
    private readonly ProgressService _progress;
    private readonly AuditService _audit;

    public SyncService(ApplicationDbContext db, ProgressService progress, AuditService audit)
    {
        _db = db; _progress = progress; _audit = audit;
    }

    public async Task<object> SyncAsync(int userId, SyncRequest req)
    {
        var log = new SyncLog { UserId = userId, StartedAt = DateTime.UtcNow };
        var now = DateTime.UtcNow;
        var notes = new List<string>();

        var clientTime = Fmt.ParseUtc(req.ClientTime, now);
        var skew = Math.Abs((clientTime - now).TotalMinutes);
        if (skew > 10) notes.Add($"Clock skew of {skew:F0} minutes.");

        await _progress.MergeAsync(userId, req.Progress ?? new());

        var statuses = new List<object>();
        foreach (var r in req.Leases ?? new())
        {
            if (!Guid.TryParse(r.LeaseId, out var id)) continue;
            var lease = await _db.Leases.FirstOrDefaultAsync(l => l.LeaseId == id && l.UserId == userId);
            if (lease == null) continue;
            var status = LeaseService.Effective(lease, now);
            if (status == LeaseStatus.Expired && lease.Status == LeaseStatus.Active) lease.Status = LeaseStatus.Expired;
            statuses.Add(new { leaseId = lease.LeaseId, status });
        }

        var audit = req.Baseline ? new AuditResult(true, 0, null) : await _audit.VerifyAndStoreAsync(userId, req.DeviceId, req.Audit ?? new());
        if (!audit.Accepted) { log.Status = "AuditRejected"; notes.Add("Audit log rejected: " + audit.Reason); }
        if (req.Baseline) notes.Add("Baseline mode device.");

        log.CompletedAt = DateTime.UtcNow; log.Message = string.Join(" ", notes);
        _db.SyncLogs.Add(log);
        await _db.SaveChangesAsync();

        return new
        {
            serverTime = Fmt.Iso(DateTime.UtcNow),
            progress = await _progress.GetAsync(userId),
            leaseStatuses = statuses,
            audit = new { accepted = audit.Accepted, count = audit.Count, reason = audit.Reason }
        };
    }
}
