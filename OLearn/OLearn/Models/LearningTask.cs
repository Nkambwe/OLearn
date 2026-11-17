namespace OLearn.Models;

public class LearningTask
{
    public int TaskId { get; set; }
    public int ModuleId { get; set; }
    public CourseModule? Module { get; set; }
    public int? ResourceId { get; set; }
    public Resource? Resource { get; set; }
    public string TaskTitle { get; set; } = "";
    public bool IsActive { get; set; } = true;
}
