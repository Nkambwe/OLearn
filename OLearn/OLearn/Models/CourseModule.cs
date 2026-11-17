namespace OLearn.Models;

public class CourseModule
{
    public int ModuleId { get; set; }
    public int CourseId { get; set; }
    public Course? Course { get; set; }
    public string ModuleName { get; set; } = "";
    public int SortOrder { get; set; }
}
