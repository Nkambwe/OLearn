using Microsoft.EntityFrameworkCore;
using OLearn.Api;
using OLearn.Data;
using OLearn.Models;

namespace OLearn.Services;

public class ProgressService
{
    private readonly ApplicationDbContext _db;
    public ProgressService(ApplicationDbContext db) => _db = db;

    /// <summary>FR-08: when two devices changed the same task, the most recent change wins.</summary>
    public async Task MergeAsync(int userId, IEnumerable<SyncProgressItem> items)
    {
        foreach (var i in items)
        {
            if (!await _db.LearningTasks.AnyAsync(t => t.TaskId == i.TaskId)) continue;
            var at = Fmt.ParseUtc(i.UpdatedAt, DateTime.UtcNow);
            var row = await _db.TaskProgress.FirstOrDefaultAsync(p => p.UserId == userId && p.TaskId == i.TaskId);
            if (row == null)
                _db.TaskProgress.Add(new StudentTaskProgress { UserId = userId, TaskId = i.TaskId, IsCompleted = i.IsCompleted, UpdatedAt = at, CompletedAt = i.IsCompleted ? at : null });
            else if (at > row.UpdatedAt)
            {
                row.IsCompleted = i.IsCompleted; row.UpdatedAt = at; row.CompletedAt = i.IsCompleted ? at : null;
            }
        }
        await _db.SaveChangesAsync();
    }

    public async Task<List<object>> GetAsync(int userId)
    {
        var rows = await _db.TaskProgress.Where(p => p.UserId == userId).ToListAsync();
        return rows.Select(p => (object)new { taskId = p.TaskId, isCompleted = p.IsCompleted, updatedAt = Fmt.Iso(p.UpdatedAt) }).ToList();
    }
}
