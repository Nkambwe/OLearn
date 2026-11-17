using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OLearn.Models;

namespace OLearn.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.HasIndex(u => u.Username).IsUnique();
        b.Property(u => u.Username).HasMaxLength(60);
        b.Property(u => u.Role).HasMaxLength(20);
    }
}

public class ResourceVersionConfiguration : IEntityTypeConfiguration<ResourceVersion>
{
    public void Configure(EntityTypeBuilder<ResourceVersion> b)
    {
        b.Property(v => v.Checksum).HasMaxLength(64);
        b.Property(v => v.VersionLabel).HasMaxLength(40);
    }
}

public class LeaseConfiguration : IEntityTypeConfiguration<Lease>
{
    public void Configure(EntityTypeBuilder<Lease> b)
    {
        b.HasKey(l => l.LeaseId);
        b.Property(l => l.Status).HasMaxLength(20);
        b.HasIndex(l => new { l.UserId, l.ResourceVersionId });
        b.HasOne(l => l.User).WithMany().HasForeignKey(l => l.UserId);
        b.HasOne(l => l.RevokedByUser).WithMany().HasForeignKey(l => l.RevokedByUserId);
        b.HasOne(l => l.ResourceVersion).WithMany().HasForeignKey(l => l.ResourceVersionId);
    }
}

public class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> b)
    {
        b.Property(a => a.DeviceId).HasMaxLength(64);
        b.Property(a => a.EventType).HasMaxLength(40);
        b.Property(a => a.PreviousHash).HasMaxLength(64);
        b.Property(a => a.EntryHash).HasMaxLength(64);
        b.Property(a => a.RawTime).HasMaxLength(40);
        b.Property(a => a.Detail).HasMaxLength(200);
        b.HasIndex(a => new { a.UserId, a.DeviceId, a.Sequence }).IsUnique();
        b.HasOne(a => a.Lease).WithMany().HasForeignKey(a => a.LeaseId);
        b.HasOne(a => a.User).WithMany().HasForeignKey(a => a.UserId);
    }
}

public class CourseModuleConfiguration : IEntityTypeConfiguration<CourseModule> {
    public void Configure(EntityTypeBuilder<CourseModule> b) => b.HasKey(m => m.ModuleId);
}
public class LearningTaskConfiguration : IEntityTypeConfiguration<LearningTask> {
    public void Configure(EntityTypeBuilder<LearningTask> b) => b.HasKey(t => t.TaskId);
}
public class StudentTaskProgressConfiguration : IEntityTypeConfiguration<StudentTaskProgress> {
    public void Configure(EntityTypeBuilder<StudentTaskProgress> b) => b.HasKey(p => p.ProgressId);
}
