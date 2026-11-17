using Microsoft.EntityFrameworkCore;
using OLearn.Models;
using OLearn.Services;

namespace OLearn.Data;

/// <summary>Creates the database and sample data on first run. Accounts: admin / Admin@12345 and student1..student8 / Student@12345.</summary>
public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var resources = scope.ServiceProvider.GetRequiredService<ResourceService>();
        var runtime = scope.ServiceProvider.GetRequiredService<RuntimeSettings>();

        await db.Database.EnsureCreatedAsync();
        if (await db.Users.AnyAsync()) return;

        db.Users.Add(new User { Username = "admin", DisplayName = "Administrator", Role = "Admin", PasswordHash = PasswordService.Hash("Admin@12345") });
        for (var i = 1; i <= 8; i++)
            db.Users.Add(new User { Username = $"student{i}", DisplayName = $"Student {i}", Role = "Student", PasswordHash = PasswordService.Hash("Student@12345") });

        var c1 = new Course { CourseCode = "BIT301", CourseName = "Mobile Application Development" };
        var c2 = new Course { CourseCode = "BIT305", CourseName = "Database Systems" };
        db.Courses.AddRange(c1, c2);
        await db.SaveChangesAsync();

        var m1 = new CourseModule { CourseId = c1.CourseId, ModuleName = "Progressive Web Apps", SortOrder = 1 };
        var m2 = new CourseModule { CourseId = c1.CourseId, ModuleName = "Offline Storage", SortOrder = 2 };
        var m3 = new CourseModule { CourseId = c2.CourseId, ModuleName = "Normalisation", SortOrder = 1 };
        db.CourseModules.AddRange(m1, m2, m3);
        await db.SaveChangesAsync();

        async Task Add(CourseModule m, string title, bool released, long padding = 0)
        {
            using var ms = new MemoryStream();
            PdfFactory.Write(ms, title, $"{m.ModuleName} - sample resource v1.0", padding);
            ms.Position = 0;
            await resources.CreateWithFileAsync(m.ModuleId, title, "Sample resource for testing.", 14, released, ms, title + ".pdf", "v1.0", "Seed data");
        }
        await Add(m1, "Week 1 Lecture Notes", true);
        await Add(m1, "Week 2 Service Worker Guide", true);
        await Add(m2, "IndexedDB Lab Exercise", true);
        await Add(m3, "Normalisation Notes", true);
        await Add(m3, "SQL Practice Sheet", true);
        await Add(m3, "Unreleased Draft (not for students)", false);
        if (runtime.TestMode) await Add(m2, "Large Test File 50 MB", true, 50L * 1024 * 1024);

        var r1 = await db.Resources.FirstAsync(r => r.Title == "Week 1 Lecture Notes");
        var r3 = await db.Resources.FirstAsync(r => r.Title == "IndexedDB Lab Exercise");
        db.LearningTasks.AddRange(
            new LearningTask { ModuleId = m1.ModuleId, ResourceId = r1.ResourceId, TaskTitle = "Read the Week 1 lecture notes" },
            new LearningTask { ModuleId = m1.ModuleId, TaskTitle = "Install a PWA on your phone" },
            new LearningTask { ModuleId = m2.ModuleId, ResourceId = r3.ResourceId, TaskTitle = "Complete the IndexedDB lab" },
            new LearningTask { ModuleId = m3.ModuleId, TaskTitle = "Normalise the sample table to 3NF" });
        await db.SaveChangesAsync();
    }
}
