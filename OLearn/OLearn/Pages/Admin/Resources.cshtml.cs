using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OLearn.Data;
using OLearn.Models;
using OLearn.Services;

namespace OLearn.Pages.Admin;

[RequestSizeLimit(250_000_000)]
[RequestFormLimits(MultipartBodyLengthLimit = 250_000_000)]
public class ResourcesModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly ResourceService _resources;
    private readonly VersionService _versions;
    private readonly LeaseService _leases;

    public ResourcesModel(ApplicationDbContext db, ResourceService resources, VersionService versions, LeaseService leases)
    {
        _db = db; _resources = resources; _versions = versions; _leases = leases;
    }

    public record Row(Resource Resource, ResourceVersion? Version);
    public List<Row> Rows { get; set; } = new();
    public List<CourseModule> Modules { get; set; } = new();
    [TempData] public string? Message { get; set; }

    public async Task OnGetAsync()
    {
        Modules = await _db.CourseModules.Include(m => m.Course).OrderBy(m => m.CourseId).ThenBy(m => m.SortOrder).ToListAsync();
        var res = await _db.Resources.Include(r => r.Module).ThenInclude(m => m!.Course).OrderBy(r => r.ResourceId).ToListAsync();
        var vers = (await _db.ResourceVersions.ToListAsync()).GroupBy(v => v.ResourceId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.ResourceVersionId).First());
        Rows = res.Select(r => new Row(r, vers.GetValueOrDefault(r.ResourceId))).ToList();
    }

    public async Task<IActionResult> OnPostCreateAsync(int moduleId, string title, IFormFile file, string source, int days, bool release)
    {
        await using var s = file.OpenReadStream();
        await _resources.CreateWithFileAsync(moduleId, title, "", days, release, s, file.FileName, null, source);
        Message = "Resource added.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostVersionAsync(int id, IFormFile file, string? label)
    {
        await using var s = file.OpenReadStream();
        var v = await _versions.PublishAsync(id, s, file.FileName, label, "Admin upload");
        Message = $"Published {v.VersionLabel}. Leases for older versions are now Superseded; devices apply this at their next synchronisation.";
        return RedirectToPage();
    }

    /// <summary>Test mode: publishes a new generated PDF so version detection and supersession can be tested quickly.</summary>
    public async Task<IActionResult> OnPostSampleAsync(int id)
    {
        var r = await _db.Resources.FindAsync(id);
        if (r == null) return RedirectToPage();
        var n = await _db.ResourceVersions.CountAsync(v => v.ResourceId == id) + 1;
        using var ms = new MemoryStream();
        PdfFactory.Write(ms, r.Title, $"Revised sample version {n}.0");
        ms.Position = 0;
        var v = await _versions.PublishAsync(id, ms, r.Title + ".pdf", $"v{n}.0", "Generated test version");
        Message = $"Published revised sample {v.VersionLabel}.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostReleaseAsync(int id)
    {
        var r = await _db.Resources.FindAsync(id);
        if (r != null) { r.IsAuthorised = !r.IsAuthorised; await _db.SaveChangesAsync(); }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDaysAsync(int id, int days)
    {
        var r = await _db.Resources.FindAsync(id);
        if (r != null) { r.LeaseDurationDays = Math.Clamp(days, 1, 90); await _db.SaveChangesAsync(); Message = "Lease duration saved. It applies to leases issued from now on."; }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRevokeAllAsync(int id)
    {
        var admin = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var n = await _leases.RevokeAllForResourceAsync(id, admin, "Revoked by administrator");
        Message = $"{n} lease(s) revoked. Devices apply this at their next synchronisation.";
        return RedirectToPage();
    }
}
