using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using VIMS.Application.Interfaces.Services;
using VIMS.API.Hubs;

namespace VIMS.API.Hubs
{
    public class PushNotificationService : IPushNotificationService
    {
        private readonly IHubContext<NotificationHub> _hubContext;

        public PushNotificationService(IHubContext<NotificationHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public async Task NotifyUserAsync(int userId, object notification)
        {
            await _hubContext.Clients.User(userId.ToString()).SendAsync("ReceiveNotification", notification);
        }

        public async Task NotifyAdminsAsync(object notification)
        {
            await _hubContext.Clients.Group("Admin").SendAsync("ReceiveNotification", notification);
        }

        public async Task NotifyGroupAsync(string groupName, object notification)
        {
            await _hubContext.Clients.Group(groupName).SendAsync("ReceiveNotification", notification);
        }
    }
}
