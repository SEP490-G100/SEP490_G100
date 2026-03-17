using NotificationEntity = Nanny_BackEnd.Models.Notification;

namespace Nanny_BackEnd.DTOs.Notification;

public class NotificationResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public int Type { get; set; }
    public string TypeLabel { get; set; } = "";
    public bool IsRead { get; set; }
    public Guid? CreatedBy { get; set; }
    public string SenderLabel { get; set; } = "";
    public Guid? RelatedEntityId { get; set; }
    public string? RelatedEntityType { get; set; }
    public string? ActionUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }
}
