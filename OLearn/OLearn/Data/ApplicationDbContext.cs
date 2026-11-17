using Microsoft.EntityFrameworkCore;
using OLearn.Models;

namespace OLearn.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<CourseModule> CourseModules => Set<CourseModule>();
    public DbSet<Resource> Resources => Set<Resource>();
    public DbSet<ResourceVersion> ResourceVersions => Set<ResourceVersion>();
    public DbSet<LearningTask> LearningTasks => Set<LearningTask>();
    public DbSet<StudentTaskProgress> TaskProgress => Set<StudentTaskProgress>();
    public DbSet<StudentResource> StudentResources => Set<StudentResource>();
    public DbSet<SyncLog> SyncLogs => Set<SyncLog>();
    public DbSet<Lease> Leases => Set<Lease>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        // SQL Server rejects multiple cascade paths, so no relationship cascades deletes.
        foreach (var fk in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
            fk.DeleteBehavior = DeleteBehavior.Restrict;
    }
}
