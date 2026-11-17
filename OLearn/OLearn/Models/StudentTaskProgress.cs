namespace OLearn.Models;

public class StudentTaskProgress
{
    public int ProgressId { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    public int TaskId { get; set; }
    public LearningTask? Task { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
