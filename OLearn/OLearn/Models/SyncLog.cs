namespace OLearn.Models;

public class SyncLog
{
    public int SyncLogId { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public string Status { get; set; } = "Completed"; // Completed, AuditRejected
    public string Message { get; set; } = "";
}
