namespace OLearn.Models;

public class StudentResource
{
    public int StudentResourceId { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    public int ResourceId { get; set; }
    public Resource? Resource { get; set; }
    public string LocalStatus { get; set; } = "Saved";
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
