namespace DragonTools.Services;

public interface INotificationService
{
    Task ScheduleDueNotificationAsync(Guid id, string title, string message, DateTime dueUtc);
    Task CancelNotificationAsync(Guid id);
}
