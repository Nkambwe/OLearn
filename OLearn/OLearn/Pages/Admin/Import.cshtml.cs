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
public class ImportModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly ResourceService _resources;
    public ImportModel(ApplicationDbContext db, ResourceService resources) { _db = db; _resources = resources; }

    public List<CourseModule> Modules { get; set; } = new();
    [TempData] public string? Message { get; set; }

    public async Task OnGetAsync() =>
        Modules = await _db.CourseModules.Include(m => m.Course).OrderBy(m => m.CourseId).ThenBy(m => m.SortOrder).ToListAsync();

    public async Task<IActionResult> OnPostAsync(int moduleId, List<IFormFile> files, string source, int days, bool release)
    {
        var n = 0;
        foreach (var f in files)
        {
            await using var s = f.OpenReadStream();
            await _resources.CreateWithFileAsync(moduleId, Path.GetFileNameWithoutExtension(f.FileName), "", days, release, s, f.FileName, null, source);
            n++;
        }
        Message = $"{n} file(s) imported with source reference \"{source}\".";
        return RedirectToPage();
    }
}
