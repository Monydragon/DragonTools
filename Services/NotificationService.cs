using System;
using System.Threading.Tasks;
using Plugin.LocalNotification;

namespace DragonTools.Services
{
    public sealed class NotificationService : INotificationService
    {
        public async Task ScheduleDueNotificationAsync(Guid id, string title, string message, DateTime dueUtc)
        {
            var localNotifyTime = DateTime.SpecifyKind(dueUtc, DateTimeKind.Utc).ToLocalTime();
            var request = new NotificationRequest
            {
                NotificationId = IdToInt(id),
                Title = title,
                Description = message,
                Schedule = new NotificationRequestSchedule
                {
                    NotifyTime = localNotifyTime
                }
            };
            await LocalNotificationCenter.Current.Show(request);
        }

        public Task CancelNotificationAsync(Guid id)
        {
            LocalNotificationCenter.Current.Cancel(IdToInt(id));
            return Task.CompletedTask;
        }

        static int IdToInt(Guid id)
        {
            var bytes = id.ToByteArray();
            unchecked
            {
                int hash = 17;
                foreach (var b in bytes) hash = hash * 31 + b;
                return Math.Abs(hash);
            }
        }
    }
}
