using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OLearn.Data;
using OLearn.Models;

namespace OLearn.Pages.Admin;

public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public IndexModel(ApplicationDbContext db) => _db = db;
    public int Students, Released, ActiveLeases, AuditEvents, Rejected;

    public async Task OnGetAsync()
    {
        var now = DateTime.UtcNow;
        Students = await _db.Users.CountAsync(u => u.Role == "Student");
        Released = await _db.Resources.CountAsync(r => r.IsAuthorised && r.IsActive);
        ActiveLeases = await _db.Leases.CountAsync(l => l.Status == LeaseStatus.Active && l.ExpiresAt > now);
        AuditEvents = await _db.AuditEvents.CountAsync();
        Rejected = await _db.SyncLogs.CountAsync(s => s.Status == "AuditRejected");
    }
}
