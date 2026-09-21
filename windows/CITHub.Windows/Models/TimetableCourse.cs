namespace CITHub.Windows.Models;

public sealed class TimetableCourse
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Day { get; set; } = "";
    public string Period { get; set; } = "";
    public string Title { get; set; } = "";
    public string Teacher { get; set; } = "";
    public string Classroom { get; set; } = "";
    public string DetailUrl { get; set; } = "";
    public string Color { get; set; } = "";
    public string Note { get; set; } = "";
    public string DisplayName => string.IsNullOrWhiteSpace(Title) ? "授業" : Title;
    public string Subtitle => string.Join("  ", new[] { Period, Classroom, Teacher }.Where(value => !string.IsNullOrWhiteSpace(value)));
}
