using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using OLearn.Api;
using OLearn.Data;
using OLearn.Models;

namespace OLearn.Services;

public static class AuditChain
{
    public static readonly string Genesis = new string('0', 64);

    /// <summary>Must match entryHash() in wwwroot/js/audit.js exactly.</summary>
    public static string Hash(string deviceId, int seq, string type, string leaseId, int versionId, string time, string detail, string prev)
    {
        var text = string.Join("|", deviceId, seq, type, leaseId, versionId, time, detail, prev);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    }
}

public record AuditResult(bool Accepted, int Count, string? Reason);

public class AuditService
{
    private readonly ApplicationDbContext _db;
    public AuditService(ApplicationDbContext db) => _db = db;

    /// <summary>
    /// FR-19: checks that a device's uploaded entries continue its accepted chain and that every hash is correct.
    /// The whole batch is rejected when an entry was removed, altered or reordered.
    /// </summary>
    public async Task<AuditResult> VerifyAndStoreAsync(int userId, string deviceId, List<AuditEntryDto> entries)
    {
        if (entries.Count == 0) return new AuditResult(true, 0, null);
        if (string.IsNullOrWhiteSpace(deviceId) || deviceId.Length > 64) return new AuditResult(false, 0, "Invalid device identifier.");

        var last = await _db.AuditEvents.Where(a => a.UserId == userId && a.DeviceId == deviceId)
            .OrderByDescending(a => a.Sequence).FirstOrDefaultAsync();
        var lastSeq = last?.Sequence ?? 0;
        var lastHash = last?.EntryHash ?? AuditChain.Genesis;
        var toStore = new List<AuditEvent>();

        foreach (var e in entries.OrderBy(x => x.Seq))
        {
            var expected = AuditChain.Hash(deviceId, e.Seq, e.Type ?? "", e.LeaseId ?? "", e.VersionId, e.Time ?? "", e.Detail ?? "", e.PrevHash ?? "");
            if (!string.Equals(expected, e.Hash, StringComparison.OrdinalIgnoreCase))
                return new AuditResult(false, 0, $"Entry {e.Seq} was altered (its hash does not match its content).");

            if (e.Seq <= lastSeq)
            {
                // Already accepted earlier (a retry after a lost response). It must be identical.
                var stored = await _db.AuditEvents.FirstOrDefaultAsync(a => a.UserId == userId && a.DeviceId == deviceId && a.Sequence == e.Seq);
                if (stored == null || stored.EntryHash != e.Hash)
                    return new AuditResult(false, 0, $"Entry {e.Seq} conflicts with an entry already accepted.");
                continue;
            }
            if (e.Seq != lastSeq + 1)
                return new AuditResult(false, 0, $"Entries are missing: expected {lastSeq + 1} but received {e.Seq}.");
            if (e.PrevHash != lastHash)
                return new AuditResult(false, 0, $"Entry {e.Seq} does not continue the previous entry (chain broken).");

            Guid? leaseId = null;
            if (Guid.TryParse(e.LeaseId, out var g) && await _db.Leases.AnyAsync(l => l.LeaseId == g && l.UserId == userId)) leaseId = g;

            toStore.Add(new AuditEvent
            {
                LeaseId = leaseId, UserId = userId, ResourceVersionId = e.VersionId, DeviceId = deviceId, Sequence = e.Seq,
                EventType = e.Type ?? "", RawTime = e.Time ?? "", EventTime = Fmt.ParseUtc(e.Time, DateTime.UtcNow),
                Detail = e.Detail ?? "", PreviousHash = e.PrevHash ?? "", EntryHash = e.Hash ?? "", ReceivedAt = DateTime.UtcNow
            });
            lastSeq = e.Seq; lastHash = e.Hash!;
        }
        _db.AuditEvents.AddRange(toStore);
        await _db.SaveChangesAsync();
        return new AuditResult(true, toStore.Count, null);
    }
}
