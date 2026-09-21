namespace CITHub.Windows.Models;

public sealed record PersonalTodo(
    string Id,
    string Title,
    string Details,
    DateTimeOffset? Deadline,
    DateTimeOffset? NotifyAt,
    bool Weekly,
    bool IsCompleted = false);
