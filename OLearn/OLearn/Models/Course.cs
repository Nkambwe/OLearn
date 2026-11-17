namespace OLearn.Models;

public class Course
{
    public int CourseId { get; set; }
    public string CourseCode { get; set; } = "";
    public string CourseName { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public List<CourseModule> Modules { get; set; } = new();
}
