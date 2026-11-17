using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OLearn.Data;
using OLearn.Models;
using OLearn.Services;

namespace OLearn.Pages.Admin;

public class LeasesModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly LeaseService _leases;
    public LeasesModel(ApplicationDbContext db, LeaseService leases) { _db = db; _leases = leases; }

    public List<Lease> Rows { get; set; } = new();
    [TempData] public string? Message { get; set; }

    public async Task OnGetAsync()
    {
        Rows = await _db.Leases.Include(l => l.User)
            .Include(l => l.ResourceVersion).ThenInclude(v => v!.Resource)
            .OrderByDescending(l => l.IssuedAt).Take(300).ToListAsync();
    }

    public async Task<IActionResult> OnPostRevokeAsync(Guid id)
    {
        var admin = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        Message = await _leases.RevokeAsync(id, admin, "Revoked by administrator") ? "Lease revoked." : "Lease not found.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostReinstateAsync(Guid id)
    {
        Message = await _leases.ReinstateAsync(id) ? "Lease reinstated." : "Lease could not be reinstated (expired or superseded).";
        return RedirectToPage();
    }
}
