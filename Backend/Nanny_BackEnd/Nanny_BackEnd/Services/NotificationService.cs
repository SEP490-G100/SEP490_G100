using Nanny_BackEnd.DTOs.Notification;
using Nanny_BackEnd.Helpers;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories;

namespace Nanny_BackEnd.Services;

public class NotificationService
{
    private readonly SubscriptionRepository _subscriptionRepo;

    public NotificationService(SubscriptionRepository subscriptionRepo)
    {
        _subscriptionRepo = subscriptionRepo;
    }

    public async Task<NotificationListResponse> getMyNotifications(Guid userId, int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var notifications = await _subscriptionRepo.getNotifications(userId, page, pageSize);
        var total = await _subscriptionRepo.countNotifications(userId);
        var unreadCount = await _subscriptionRepo.countUnreadNotifications(userId);
        var senderUsers = await _subscriptionRepo.getUsersByIds(
            notifications.Where(n => n.CreatedBy.HasValue).Select(n => n.CreatedBy!.Value));
        var senderMap = senderUsers.ToDictionary(u => u.Id, getDisplayName);

        return new NotificationListResponse
        {
            Total = total,
            UnreadCount = unreadCount,
            Items = notifications.Select(n => mapNotification(n, senderMap)).ToList()
        };
    }

    public async Task<int> getUnreadCount(Guid userId) =>
        await _subscriptionRepo.countUnreadNotifications(userId);

    public async Task<NotificationResponse> markAsRead(Guid userId, Guid notificationId)
    {
        var notification = await _subscriptionRepo.findNotificationById(notificationId, userId)
            ?? throw new KeyNotFoundException("Khong tim thay thong bao can cap nhat.");

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
            notification.UpdatedAt = DateTime.UtcNow;
            notification.UpdatedBy = userId;
            await _subscriptionRepo.saveChanges();
        }

        var senderMap = new Dictionary<Guid, string>();
        if (notification.CreatedBy.HasValue)
        {
            var senders = await _subscriptionRepo.getUsersByIds([notification.CreatedBy.Value]);
            senderMap = senders.ToDictionary(u => u.Id, getDisplayName);
        }

        return mapNotification(notification, senderMap);
    }

    public async Task<int> markAllAsRead(Guid userId)
    {
        var unreadNotifications = await _subscriptionRepo.getUnreadNotifications(userId);
        if (unreadNotifications.Count == 0)
            return 0;

        var nowUtc = DateTime.UtcNow;
        foreach (var notification in unreadNotifications)
        {
            notification.IsRead = true;
            notification.ReadAt = nowUtc;
            notification.UpdatedAt = nowUtc;
            notification.UpdatedBy = userId;
        }

        await _subscriptionRepo.saveChanges();
        return unreadNotifications.Count;
    }

    public async Task<int> createSubscriptionExpiryReminders()
    {
        var createdCount = 0;
        createdCount += await createSubscriptionExpiryReminders(7);
        createdCount += await createSubscriptionExpiryReminders(3);
        return createdCount;
    }

    public async Task createNotification(
        Guid userId,
        string title,
        string content,
        int type,
        Guid? relatedEntityId = null,
        string? relatedEntityType = null,
        Guid? createdBy = null)
    {
        _subscriptionRepo.addNotification(new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = title,
            Content = content,
            Type = type,
            IsRead = false,
            RelatedEntityId = relatedEntityId,
            RelatedEntityType = relatedEntityType,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdBy,
            IsDeleted = false
        });

        await _subscriptionRepo.saveChanges();
    }

    private async Task<int> createSubscriptionExpiryReminders(int daysBeforeExpiry)
    {
        var targetDate = DateTime.UtcNow.Date.AddDays(daysBeforeExpiry);
        var subscriptions = await _subscriptionRepo.getActiveSubscriptionsExpiringOnDate(targetDate);
        if (subscriptions.Count == 0)
            return 0;

        var createdCount = 0;
        foreach (var subscription in subscriptions)
        {
            var title = daysBeforeExpiry == 1
                ? "Goi subscription cua ban se het han sau 1 ngay"
                : $"Goi subscription cua ban se het han sau {daysBeforeExpiry} ngay";

            var exists = await _subscriptionRepo.hasNotificationForSubscription(subscription.UserId, subscription.Id, title);
            if (exists)
                continue;

            var planName = subscription.SubscriptionPlan?.Name ?? "hien tai";
            var content =
                $"Goi {planName} cua ban se het han vao ngay {subscription.EndDate:dd/MM/yyyy}. Vui long gia han neu ban muon tiep tuc su dung quyen loi hien tai.";

            _subscriptionRepo.addNotification(new Notification
            {
                Id = Guid.NewGuid(),
                UserId = subscription.UserId,
                Title = title,
                Content = content,
                Type = NotificationTypes.SubscriptionReminder,
                IsRead = false,
                RelatedEntityId = subscription.Id,
                RelatedEntityType = "UserSubscription",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = null,
                IsDeleted = false
            });

            createdCount++;
        }

        if (createdCount > 0)
            await _subscriptionRepo.saveChanges();

        return createdCount;
    }

    private static NotificationResponse mapNotification(Notification notification, IReadOnlyDictionary<Guid, string> senderMap) => new()
    {
        Id = notification.Id,
        Title = notification.Title,
        Content = notification.Content,
        Type = notification.Type,
        TypeLabel = NotificationTypes.getLabel(notification.Type),
        IsRead = notification.IsRead,
        CreatedBy = notification.CreatedBy,
        SenderLabel = getSenderLabel(notification, senderMap),
        RelatedEntityId = notification.RelatedEntityId,
        RelatedEntityType = notification.RelatedEntityType,
        ActionUrl = getActionUrl(notification),
        CreatedAt = notification.CreatedAt,
        ReadAt = notification.ReadAt
    };

    private static string getSenderLabel(Notification notification, IReadOnlyDictionary<Guid, string> senderMap)
    {
        if (!notification.CreatedBy.HasValue)
            return notification.Type == NotificationTypes.AdminBroadcast ? "Admin he thong" : "He thong";

        return senderMap.TryGetValue(notification.CreatedBy.Value, out var senderName)
            ? senderName
            : "Nguoi dung";
    }

    private static string getDisplayName(User user)
    {
        var fullName = $"{user.FirstName} {user.LastName}".Trim();
        return string.IsNullOrWhiteSpace(fullName) ? user.Email : fullName;
    }

    private static string? getActionUrl(Notification notification)
    {
        return notification.Type switch
        {
            NotificationTypes.SubscriptionReminder => "/Subscription",
            NotificationTypes.JobApplicationReceived when notification.RelatedEntityId.HasValue =>
                $"/Search/History?jobId={notification.RelatedEntityId.Value}",
            NotificationTypes.JobApplicationApproved when notification.RelatedEntityId.HasValue =>
                $"/Search?applicationId={notification.RelatedEntityId.Value}",
            NotificationTypes.JobApplicationRejected when notification.RelatedEntityId.HasValue =>
                $"/Search?applicationId={notification.RelatedEntityId.Value}",
            NotificationTypes.JobPostingApproved when notification.RelatedEntityId.HasValue =>
                $"/Search?jobId={notification.RelatedEntityId.Value}",
            NotificationTypes.JobPostingRejected when notification.RelatedEntityId.HasValue =>
                $"/Search/History?jobId={notification.RelatedEntityId.Value}",
            NotificationTypes.JobPostingPending when notification.RelatedEntityId.HasValue =>
                $"/Search/History?jobId={notification.RelatedEntityId.Value}",
            NotificationTypes.NannyProfileFavorited =>
                "/Nanny/Profile",
            NotificationTypes.JobApplicationSubmitted =>
                "/Search/Applications",
            NotificationTypes.ContactRequestReceived =>
                "/Nanny/ReceivedContactRequests",
            NotificationTypes.ContactRequestAccepted =>
                "/Nanny/ContactRequests",
            NotificationTypes.ContactRequestRejected =>
                "/Nanny/ContactRequests",
            _ => null
        };
    }
}
