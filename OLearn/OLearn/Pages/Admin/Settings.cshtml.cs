using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OLearn.Data;
using OLearn.Models;
using OLearn.Services;

namespace OLearn.Pages.Admin;

public class SettingsModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly RuntimeSettings _runtime;
    public SettingsModel(ApplicationDbContext db, RuntimeSettings runtime) { _db = db; _runtime = runtime; }

    public bool TestMode => _runtime.TestMode;
    public int Minutes => _runtime.TestLeaseMinutes;
    public List<SyncLog> Logs { get; set; } = new();
    [TempData] public string? Message { get; set; }

    public async Task OnGetAsync() =>
        Logs = await _db.SyncLogs.Include(s => s.User).OrderByDescending(s => s.StartedAt).Take(50).ToListAsync();

    public IActionResult OnPost(int minutes)
    {
        if (_runtime.TestMode) { _runtime.TestLeaseMinutes = Math.Clamp(minutes, 0, 1440); Message = "Saved. New leases use this length."; }
        return RedirectToPage();
    }
}
