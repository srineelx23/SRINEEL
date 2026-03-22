using System.Threading.Tasks;

namespace VIMS.Application.Interfaces.Services
{
    public interface IPushNotificationService
    {
        Task NotifyUserAsync(int userId, object notification);
        Task NotifyAdminsAsync(object notification);
        Task NotifyGroupAsync(string groupName, object notification);
    }
}
