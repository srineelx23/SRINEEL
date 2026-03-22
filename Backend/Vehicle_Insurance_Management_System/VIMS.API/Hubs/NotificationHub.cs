using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace VIMS.API.Hubs
{
    public class NotificationHub : Hub
    {
        public async Task SendNotification(int userId, object notification)
        {
            await Clients.User(userId.ToString()).SendAsync("ReceiveNotification", notification);
        }

        // You can join groups by role if needed
        public async Task JoinRoleGroup(string role)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, role);
        }
    }
}
