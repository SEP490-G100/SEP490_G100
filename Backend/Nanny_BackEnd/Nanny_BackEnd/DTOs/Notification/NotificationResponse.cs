using NotificationEntity = Nanny_BackEnd.Models.Notification;

namespace Nanny_BackEnd.DTOs.Notification;

public class NotificationResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public int Type { get; set; }
    public bool IsRead { get; set; }
    public Guid? RelatedEntityId { get; set; }
    public string? RelatedEntityType { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }

    public static NotificationResponse fromEntity(NotificationEntity notification) => new()
    {
        Id = notification.Id,
        Title = notification.Title,
        Content = notification.Content,
        Type = notification.Type,
        IsRead = notification.IsRead,
        RelatedEntityId = notification.RelatedEntityId,
        RelatedEntityType = notification.RelatedEntityType,
        CreatedAt = notification.CreatedAt,
        ReadAt = notification.ReadAt
    };
}
