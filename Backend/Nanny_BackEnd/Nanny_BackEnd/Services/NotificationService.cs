using Nanny_BackEnd.DTOs.Notification;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories;

namespace Nanny_BackEnd.Services;

public class NotificationService
{
    private const int SubscriptionReminderType = 1;

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

        return new NotificationListResponse
        {
            Total = total,
            UnreadCount = unreadCount,
            Items = notifications.Select(NotificationResponse.fromEntity).ToList()
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

        return NotificationResponse.fromEntity(notification);
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
                Type = SubscriptionReminderType,
                IsRead = false,
                RelatedEntityId = subscription.Id,
                RelatedEntityType = "UserSubscription",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = subscription.UserId,
                IsDeleted = false
            });

            createdCount++;
        }

        if (createdCount > 0)
            await _subscriptionRepo.saveChanges();

        return createdCount;
    }
}
