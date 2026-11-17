namespace OLearn.Models;

public class Resource
{
    public int ResourceId { get; set; }
    public int ModuleId { get; set; }
    public CourseModule? Module { get; set; }
    public string Title { get; set; } = "";
    public string FileType { get; set; } = "PDF"; // PDF or PPTX
    public string Description { get; set; } = "";
    public bool IsAuthorised { get; set; }          // released for offline use by an administrator (FR-02)
    public int LeaseDurationDays { get; set; } = 14; // policy for each resource, 1 to 90 days (NFR-09)
    public bool IsActive { get; set; } = true;
    public List<ResourceVersion> Versions { get; set; } = new();
}
