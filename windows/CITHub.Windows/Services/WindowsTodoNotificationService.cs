using CITHub.Windows.Models;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace CITHub.Windows.Services;

public static class WindowsTodoNotificationService
{
    // Windows permits a bounded queue of scheduled notifications. Eight weekly instances
    // keep recurring reminders useful without relying on a background process.
    public static void Reschedule(IEnumerable<PersonalTodo> todos)
    {
        try
        {
            var notifier = ToastNotificationManager.CreateToastNotifier();
            foreach (var scheduled in notifier.GetScheduledToastNotifications()) notifier.RemoveFromSchedule(scheduled);
            if (LocalStore.GetString("personal-todo-reminders", "true") != "true") return;
            var now = DateTimeOffset.Now;
            foreach (var todo in todos.Where(todo => !todo.IsCompleted && todo.NotifyAt is not null))
            {
                var first = todo.NotifyAt!.Value;
                if (todo.Weekly)
                {
                    while (first <= now) first = first.AddDays(7);
                    for (var index = 0; index < 8; index++) Add(notifier, todo, first.AddDays(index * 7));
                }
                else if (first > now && (todo.Deadline is null || first <= todo.Deadline)) Add(notifier, todo, first);
            }
        }
        catch { /* Notification permission or queue availability is controlled by Windows. */ }
    }

    private static void Add(ToastNotifier notifier, PersonalTodo todo, DateTimeOffset when)
    {
        var document = ToastNotificationManager.GetTemplateContent(ToastTemplateType.ToastText02);
        var texts = document.GetElementsByTagName("text");
        texts[0].AppendChild(document.CreateTextNode("CIT Hub ToDo"));
        texts[1].AppendChild(document.CreateTextNode(todo.Title));
        var scheduled = new ScheduledToastNotification(document, when);
        notifier.AddToSchedule(scheduled);
    }
}
