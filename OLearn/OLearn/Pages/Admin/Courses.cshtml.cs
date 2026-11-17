using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OLearn.Data;
using OLearn.Models;

namespace OLearn.Pages.Admin;

public class CoursesModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public CoursesModel(ApplicationDbContext db) => _db = db;
    public List<Course> Courses { get; set; } = new();
    public List<CourseModule> Modules { get; set; } = new();

    public async Task OnGetAsync()
    {
        Courses = await _db.Courses.Include(c => c.Modules).OrderBy(c => c.CourseCode).ToListAsync();
        Modules = await _db.CourseModules.Include(m => m.Course).OrderBy(m => m.CourseId).ThenBy(m => m.SortOrder).ToListAsync();
    }

    public async Task<IActionResult> OnPostCourseAsync(string code, string name)
    {
        _db.Courses.Add(new Course { CourseCode = code.Trim(), CourseName = name.Trim() });
        await _db.SaveChangesAsync();
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostModuleAsync(int courseId, string name)
    {
        var next = (await _db.CourseModules.Where(m => m.CourseId == courseId).MaxAsync(m => (int?)m.SortOrder) ?? 0) + 1;
        _db.CourseModules.Add(new CourseModule { CourseId = courseId, ModuleName = name.Trim(), SortOrder = next });
        await _db.SaveChangesAsync();
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostTaskAsync(int moduleId, string title)
    {
        _db.LearningTasks.Add(new LearningTask { ModuleId = moduleId, TaskTitle = title.Trim() });
        await _db.SaveChangesAsync();
        return RedirectToPage();
    }
}
