using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using OLearn.Data;
using OLearn.Models;

namespace OLearn.Services;

/// <summary>Stores approved files, records their SHA-256 and publishes new versions (FR-07, FR-11).</summary>
public class VersionService
{
    private readonly ApplicationDbContext _db;
    private readonly LeaseService _leases;
    private readonly string _filesDir;

    public VersionService(ApplicationDbContext db, LeaseService leases, IWebHostEnvironment env)
    {
        _db = db; _leases = leases;
        _filesDir = Path.Combine(env.ContentRootPath, "App_Data", "files");
        Directory.CreateDirectory(_filesDir);
    }

    public string PathFor(ResourceVersion v) => Path.Combine(_filesDir, v.StoredPath);

    public Task<ResourceVersion?> CurrentAsync(int resourceId) =>
        _db.ResourceVersions.Where(v => v.ResourceId == resourceId).OrderByDescending(v => v.ResourceVersionId).FirstOrDefaultAsync();

    public async Task<ResourceVersion> PublishAsync(int resourceId, Stream content, string fileName, string? label, string? sourceReference)
    {
        var stored = Guid.NewGuid().ToString("N") + ".bin";
        var path = Path.Combine(_filesDir, stored);
        long size = 0;
        string hash;
        using (var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
        await using (var fs = File.Create(path))
        {
            var buffer = new byte[81920];
            int n;
            while ((n = await content.ReadAsync(buffer.AsMemory(0, buffer.Length))) > 0)
            {
                sha.AppendData(buffer, 0, n);
                await fs.WriteAsync(buffer.AsMemory(0, n));
                size += n;
            }
            hash = Convert.ToHexString(sha.GetHashAndReset()).ToLowerInvariant();
        }

        var count = await _db.ResourceVersions.CountAsync(v => v.ResourceId == resourceId);
        var version = new ResourceVersion
        {
            ResourceId = resourceId,
            VersionLabel = string.IsNullOrWhiteSpace(label) ? $"v{count + 1}.0" : label.Trim(),
            Checksum = hash, FileName = fileName, StoredPath = stored, SizeBytes = size,
            PublishedAt = DateTime.UtcNow, ApprovedSourceReference = sourceReference ?? ""
        };
        _db.ResourceVersions.Add(version);
        await _db.SaveChangesAsync();
        // Leases for older versions become Superseded; devices apply this at their next synchronisation.
        await _leases.SupersedeOlderAsync(resourceId, version.ResourceVersionId);
        return version;
    }
}
