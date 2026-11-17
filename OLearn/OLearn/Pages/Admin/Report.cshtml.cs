using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OLearn.Data;
using OLearn.Models;
using OLearn.Services;

namespace OLearn.Pages.Admin;

public class ReportModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public ReportModel(ApplicationDbContext db) => _db = db;

    public record Row(string Student, string Resource, string Version, DateTime Issued, DateTime Expires, string Status,
        DateTime? LastOpen, int Opens, int Expired, int Integrity);

    public List<Row> Rows { get; set; } = new();
    public List<SyncLog> RejectedLogs { get; set; } = new();
    public int Opens, ExpiredRefusals, IntegrityFailures, OtherRefusals, Rejected;

    public async Task OnGetAsync()
    {
        var now = DateTime.UtcNow;
        var leases = await _db.Leases.Include(l => l.User)
            .Include(l => l.ResourceVersion).ThenInclude(v => v!.Resource)
            .OrderByDescending(l => l.IssuedAt).Take(500).ToListAsync();
        var events = await _db.AuditEvents.Where(e => e.LeaseId != null)
            .Select(e => new { e.LeaseId, e.EventType, e.EventTime }).ToListAsync();
        var byLease = events.GroupBy(e => e.LeaseId!.Value).ToDictionary(g => g.Key, g => g.ToList());

        foreach (var l in leases)
        {
            var ev = byLease.GetValueOrDefault(l.LeaseId) ?? new();
            var opens = ev.Where(e => e.EventType == "OPEN").ToList();
            Rows.Add(new Row(l.User!.DisplayName, l.ResourceVersion!.Resource!.Title, l.ResourceVersion.VersionLabel,
                l.IssuedAt, l.ExpiresAt, LeaseService.Effective(l, now),
                opens.Count == 0 ? null : opens.Max(e => e.EventTime), opens.Count,
                ev.Count(e => e.EventType == "REFUSE_EXPIRED"), ev.Count(e => e.EventType == "REFUSE_INTEGRITY")));
        }
        Opens = await _db.AuditEvents.CountAsync(e => e.EventType == "OPEN");
        ExpiredRefusals = await _db.AuditEvents.CountAsync(e => e.EventType == "REFUSE_EXPIRED");
        IntegrityFailures = await _db.AuditEvents.CountAsync(e => e.EventType == "REFUSE_INTEGRITY");
        OtherRefusals = await _db.AuditEvents.CountAsync(e => e.EventType == "REFUSE_SIGNATURE" || e.EventType == "REFUSE_CLOCK");
        Rejected = await _db.SyncLogs.CountAsync(s => s.Status == "AuditRejected");
        RejectedLogs = await _db.SyncLogs.Include(s => s.User).Where(s => s.Status == "AuditRejected")
            .OrderByDescending(s => s.StartedAt).Take(50).ToListAsync();
    }
}
