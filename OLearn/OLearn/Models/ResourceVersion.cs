namespace OLearn.Models;

public class ResourceVersion
{
    public int ResourceVersionId { get; set; }
    public int ResourceId { get; set; }
    public Resource? Resource { get; set; }
    public string VersionLabel { get; set; } = "v1.0";
    public string Checksum { get; set; } = "";      // SHA-256 of the file, lower-case hex
    public string FileName { get; set; } = "";
    public string StoredPath { get; set; } = "";    // relative to App_Data/files
    public long SizeBytes { get; set; }
    public DateTime PublishedAt { get; set; } = DateTime.UtcNow;
    public string ApprovedSourceReference { get; set; } = ""; // where the approved file came from
}
