namespace OLearn.Models;

/// <summary>A signed permission for one student to keep one version of one resource (FR-13).</summary>
public class Lease
{
    public Guid LeaseId { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    public int ResourceVersionId { get; set; }
    public ResourceVersion? ResourceVersion { get; set; }
    public DateTime IssuedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string Status { get; set; } = LeaseStatus.Active;
    public DateTime? RevokedAt { get; set; }
    public int? RevokedByUserId { get; set; }
    public User? RevokedByUser { get; set; }
    public string? RevokeReason { get; set; }
}

public static class LeaseStatus
{
    public const string Active = "Active";
    public const string Expired = "Expired";
    public const string Revoked = "Revoked";
    public const string Superseded = "Superseded";
}
