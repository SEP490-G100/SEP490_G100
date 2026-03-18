namespace Nanny_BackEnd.DTOs.Notification;

public class NotificationListResponse
{
    public int Total { get; set; }
    public int UnreadCount { get; set; }
    public List<NotificationResponse> Items { get; set; } = [];
}
