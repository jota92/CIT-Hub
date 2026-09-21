namespace CITHub.Windows.Models;

public sealed class ManabaAssignment
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Course { get; set; } = "";
    public string DeadlineText { get; set; } = "";
    public string Url { get; set; } = "";
    public string DisplayTitle => Title;
    public string Details => string.Join("  ", new[] { Course, DeadlineText }.Where(value => !string.IsNullOrWhiteSpace(value)));
}
