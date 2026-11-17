using Microsoft.EntityFrameworkCore;
using OLearn.Data;
using OLearn.Models;

namespace OLearn.Services;

public class ResourceService
{
    private readonly ApplicationDbContext _db;
    private readonly VersionService _versions;

    public ResourceService(ApplicationDbContext db, VersionService versions) { _db = db; _versions = versions; }

    /// <summary>The catalogue a device caches for offline use: only resources an administrator has released.</summary>
    public async Task<object> BuildCatalogAsync(int userId)
    {
        var resources = await _db.Resources.Include(r => r.Module).ThenInclude(m => m!.Course)
            .Where(r => r.IsActive && r.IsAuthorised && r.Module!.Course!.IsActive).ToListAsync();
        var ids = resources.Select(r => r.ResourceId).ToList();
        var latest = (await _db.ResourceVersions.Where(v => ids.Contains(v.ResourceId)).ToListAsync())
            .GroupBy(v => v.ResourceId).ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.ResourceVersionId).First());

        var items = resources.Where(r => latest.ContainsKey(r.ResourceId)).Select(r =>
        {
            var v = latest[r.ResourceId];
            return new
            {
                resourceId = r.ResourceId, title = r.Title, fileType = r.FileType, description = r.Description,
                courseId = r.Module!.CourseId, courseCode = r.Module.Course!.CourseCode, courseName = r.Module.Course.CourseName,
                moduleId = r.ModuleId, moduleName = r.Module.ModuleName,
                versionId = v.ResourceVersionId, versionLabel = v.VersionLabel, checksum = v.Checksum, sizeBytes = v.SizeBytes,
                leaseDurationDays = r.LeaseDurationDays, publishedAt = Fmt.Iso(v.PublishedAt)
            };
        }).OrderBy(x => x.courseCode).ThenBy(x => x.moduleName).ThenBy(x => x.title).ToList();

        var moduleIds = items.Select(i => i.moduleId).Distinct().ToList();
        var tasks = await _db.LearningTasks.Include(t => t.Module).Include(t => t.Resource)
            .Where(t => t.IsActive && moduleIds.Contains(t.ModuleId)).OrderBy(t => t.TaskId).ToListAsync();

        return new
        {
            serverTime = Fmt.Iso(DateTime.UtcNow),
            resources = items,
            tasks = tasks.Select(t => new { taskId = t.TaskId, moduleId = t.ModuleId, moduleName = t.Module!.ModuleName, taskTitle = t.TaskTitle, resourceTitle = t.Resource?.Title })
        };
    }

    public static string FileTypeOf(string fileName) =>
        Path.GetExtension(fileName).Equals(".pptx", StringComparison.OrdinalIgnoreCase) ? "PPTX" : "PDF";

    public async Task<Resource> CreateWithFileAsync(int moduleId, string title, string description, int leaseDays, bool authorised,
        Stream content, string fileName, string? label, string? sourceReference)
    {
        var res = new Resource
        {
            ModuleId = moduleId, Title = title.Trim(), Description = description ?? "", FileType = FileTypeOf(fileName),
            LeaseDurationDays = Math.Clamp(leaseDays, 1, 90), IsAuthorised = authorised
        };
        _db.Resources.Add(res);
        await _db.SaveChangesAsync();
        await _versions.PublishAsync(res.ResourceId, content, fileName, label, sourceReference);
        return res;
    }
}
