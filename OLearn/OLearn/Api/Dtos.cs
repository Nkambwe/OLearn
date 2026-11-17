namespace OLearn.Api;

public record LeaseRequest(int ResourceId);

public record AuditEntryDto(int Seq, string Type, string LeaseId, int VersionId, string Time, string Detail, string PrevHash, string Hash);

public record SyncProgressItem(int TaskId, bool IsCompleted, string UpdatedAt);

public record LeaseRef(string LeaseId);

public record SyncRequest(
    string DeviceId,
    string? ClientTime,
    bool Baseline,
    List<SyncProgressItem>? Progress,
    List<LeaseRef>? Leases,
    List<AuditEntryDto>? Audit);
