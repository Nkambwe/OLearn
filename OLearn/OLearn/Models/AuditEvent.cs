namespace OLearn.Models;

/// <summary>An offline event uploaded from a device. Each entry carries the hash of the previous entry (FR-19).</summary>
public class AuditEvent
{
    public long AuditEventId { get; set; }
    public Guid? LeaseId { get; set; }
    public Lease? Lease { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    public int ResourceVersionId { get; set; }
    public string DeviceId { get; set; } = "";
    public int Sequence { get; set; }
    public string EventType { get; set; } = "";
    public DateTime EventTime { get; set; }
    public string RawTime { get; set; } = "";   // exactly as hashed on the device
    public string Detail { get; set; } = "";
    public string PreviousHash { get; set; } = "";
    public string EntryHash { get; set; } = "";
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
}
