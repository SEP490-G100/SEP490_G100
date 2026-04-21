using Nanny_BackEnd.DTOs.Notification;
using Nanny_BackEnd.Helpers;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories;

namespace Nanny_BackEnd.Services;

public class NotificationService
{
    private const string AdminNotificationScopePrefix = "AdminNotification";
    private readonly SubscriptionRepository _subscriptionRepo;
    private readonly UserRepository _userRepo;

    public NotificationService(SubscriptionRepository subscriptionRepo, UserRepository userRepo)
    {
        _subscriptionRepo = subscriptionRepo;
        _userRepo = userRepo;
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
            ?? throw new KeyNotFoundException("Không tìm thấy thông báo cần cập nhật.");

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

    public async Task createNotificationForUsers(
        IEnumerable<Guid> userIds,
        string title,
        string content,
        int type,
        Guid? relatedEntityId = null,
        string? relatedEntityType = null,
        Guid? createdBy = null)
    {
        var distinctUserIds = userIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        if (distinctUserIds.Count == 0)
            return;

        foreach (var userId in distinctUserIds)
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
        }

        await _subscriptionRepo.saveChanges();
    }

    public async Task createNotificationForModerators(
        string title,
        string content,
        int type,
        Guid? relatedEntityId = null,
        string? relatedEntityType = null,
        Guid? createdBy = null)
    {
        var moderatorIds = await _userRepo.GetActiveUserIdsByRoleAsync("Moderator");
        await createNotificationForUsers(
            moderatorIds,
            title,
            content,
            type,
            relatedEntityId,
            relatedEntityType,
            createdBy);
    }

    public async Task<AdminNotificationListResponse> getAdminNotifications(string? search, bool? isDeleted, int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var rows = await _subscriptionRepo.getAdminNotificationRows(search, isDeleted);
        var grouped = rows
            .Where(n => n.RelatedEntityId.HasValue)
            .GroupBy(n => n.RelatedEntityId!.Value)
            .Select(mapAdminNotificationGroup)
            .OrderByDescending(item => item.CreatedAt)
            .ToList();

        var totalCount = grouped.Count;
        var items = grouped
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new AdminNotificationListItemResponse
            {
                Id = item.Id,
                Title = item.Title,
                ContentPreview = item.Content.Length <= 120 ? item.Content : $"{item.Content[..117]}...",
                TargetType = item.TargetType,
                TargetRole = item.TargetRole,
                IsDeleted = item.IsDeleted,
                RecipientCount = item.RecipientCount,
                CreatedAt = item.CreatedAt,
                UpdatedAt = item.UpdatedAt
            })
            .ToList();

        return new AdminNotificationListResponse
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)pageSize)
        };
    }

    public async Task<AdminNotificationDetailResponse?> getAdminNotificationDetail(Guid broadcastId)
    {
        var rows = await _subscriptionRepo.getAdminNotificationRowsByBroadcastId(broadcastId);
        if (rows.Count == 0)
            return null;

        var group = mapAdminNotificationGroup(rows);
        return new AdminNotificationDetailResponse
        {
            Id = group.Id,
            Title = group.Title,
            Content = group.Content,
            TargetType = group.TargetType,
            TargetRole = group.TargetRole,
            IsDeleted = group.IsDeleted,
            RecipientCount = group.RecipientCount,
            CreatedAt = group.CreatedAt,
            UpdatedAt = group.UpdatedAt,
            CreatedBy = group.CreatedBy,
            UpdatedBy = group.UpdatedBy
        };
    }

    public async Task<AdminNotificationDetailResponse> createAdminNotification(Guid adminUserId, AdminNotificationUpsertRequest request)
    {
        var title = request.Title.Trim();
        var content = request.Content.Trim();
        var targetType = normalizeTargetType(request.TargetType);
        var targetRole = normalizeTargetRole(request.TargetRole);

        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(content))
            throw new InvalidOperationException("Tieu de va noi dung thong bao la bat buoc.");

        var recipientIds = await resolveAdminNotificationRecipients(targetType, targetRole);
        if (recipientIds.Count == 0)
            throw new InvalidOperationException("Không tìm thấy người nhận phù hợp để gửi thông báo.");

        var broadcastId = Guid.NewGuid();
        await createNotificationForUsers(
            recipientIds,
            title,
            content,
            NotificationTypes.AdminBroadcast,
            broadcastId,
            buildAdminNotificationScope(targetType, targetRole),
            adminUserId);

        return (await getAdminNotificationDetail(broadcastId))!;
    }

    public async Task<AdminNotificationDetailResponse> updateAdminNotification(
        Guid broadcastId,
        Guid adminUserId,
        AdminNotificationUpsertRequest request)
    {
        var rows = await _subscriptionRepo.getAdminNotificationRowsByBroadcastId(broadcastId);
        if (rows.Count == 0)
            throw new KeyNotFoundException("Không tìm thấy thông báo admin cần cập nhật.");

        var title = request.Title.Trim();
        var content = request.Content.Trim();

        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(content))
            throw new InvalidOperationException("Tieu de va noi dung thong bao la bat buoc.");

        var nowUtc = DateTime.UtcNow;
        foreach (var row in rows)
        {
            row.Title = title;
            row.Content = content;
            row.UpdatedAt = nowUtc;
            row.UpdatedBy = adminUserId;
        }

        await _subscriptionRepo.saveChanges();
        return (await getAdminNotificationDetail(broadcastId))!;
    }

    public async Task toggleAdminNotificationStatus(Guid broadcastId, Guid adminUserId, bool isDeleted)
    {
        var rows = await _subscriptionRepo.getAdminNotificationRowsByBroadcastId(broadcastId);
        if (rows.Count == 0)
            throw new KeyNotFoundException("Không tìm thấy thông báo admin cần cập nhật.");

        var nowUtc = DateTime.UtcNow;
        foreach (var row in rows)
        {
            row.IsDeleted = isDeleted;
            row.UpdatedAt = nowUtc;
            row.UpdatedBy = adminUserId;
        }

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
                ? "Gói subscription của bạn sẽ hết hạn sau 1 ngày"
                : $"Gói subscription của bạn sẽ hết hạn sau {daysBeforeExpiry} ngày";

            var exists = await _subscriptionRepo.hasNotificationForSubscription(subscription.UserId, subscription.Id, title);
            if (exists)
                continue;

            var planName = subscription.SubscriptionPlan?.Name ?? "hiện tại";
            var content =
                $"Gói {planName} của bạn sẽ hết hạn vào ngày {subscription.EndDate:dd/MM/yyyy}. Vui lòng gia hạn nếu bạn muốn tiếp tục sử dụng quyền lợi hiện tại.";

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

    private async Task<List<Guid>> resolveAdminNotificationRecipients(string targetType, string? targetRole)
    {
        if (targetType == "Role")
            return await _userRepo.GetActiveUserIdsByRoleAsync(targetRole!);

        return await _userRepo.GetActiveUserIdsByRolesAsync(["Parent", "Nanny", "Moderator"]);
    }

    private static string normalizeTargetType(string targetType) =>
        string.Equals(targetType?.Trim(), "Role", StringComparison.OrdinalIgnoreCase) ? "Role" : "All";

    private static string? normalizeTargetRole(string? targetRole)
    {
        if (string.IsNullOrWhiteSpace(targetRole))
            return null;

        var normalized = targetRole.Trim();
        return normalized is "Parent" or "Nanny" or "Moderator" or "Admin" ? normalized : null;
    }

    private static string buildAdminNotificationScope(string targetType, string? targetRole) =>
        targetType == "Role"
            ? $"{AdminNotificationScopePrefix}:Role:{targetRole}"
            : $"{AdminNotificationScopePrefix}:All";

    private static (string TargetType, string? TargetRole) parseAdminNotificationScope(string? relatedEntityType)
    {
        if (string.IsNullOrWhiteSpace(relatedEntityType))
            return ("All", null);

        var parts = relatedEntityType.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length >= 2 && string.Equals(parts[1], "Role", StringComparison.OrdinalIgnoreCase))
            return ("Role", parts.Length >= 3 ? parts[2] : null);

        return ("All", null);
    }

    private static AdminNotificationGroupProjection mapAdminNotificationGroup(IEnumerable<Notification> rows)
    {
        var materialized = rows.ToList();
        var sample = materialized
            .OrderByDescending(n => n.UpdatedAt ?? n.CreatedAt)
            .First();
        var scope = parseAdminNotificationScope(sample.RelatedEntityType);

        return new AdminNotificationGroupProjection
        {
            Id = sample.RelatedEntityId!.Value,
            Title = sample.Title,
            Content = sample.Content,
            TargetType = scope.TargetType,
            TargetRole = scope.TargetRole,
            IsDeleted = materialized.All(n => n.IsDeleted),
            RecipientCount = materialized.Count,
            CreatedAt = materialized.Min(n => n.CreatedAt),
            UpdatedAt = materialized.Max(n => n.UpdatedAt),
            CreatedBy = sample.CreatedBy,
            UpdatedBy = materialized
                .OrderByDescending(n => n.UpdatedAt ?? DateTime.MinValue)
                .Select(n => n.UpdatedBy)
                .FirstOrDefault()
        };
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
            NotificationTypes.VerificationRequestSubmitted when notification.RelatedEntityId.HasValue =>
                $"/Moderator/ViewNannyVerificationDetail/{notification.RelatedEntityId.Value}",
            NotificationTypes.VerificationRequestApproved when notification.RelatedEntityId.HasValue =>
                $"/NannyVerificationRequest/NannyViewVerificationRequestDetail/{notification.RelatedEntityId.Value}",
            NotificationTypes.VerificationRequestRejected when notification.RelatedEntityId.HasValue =>
                $"/NannyVerificationRequest/NannyViewVerificationRequestDetail/{notification.RelatedEntityId.Value}",
            NotificationTypes.VerificationRequestCreated when notification.RelatedEntityId.HasValue =>
                $"/NannyVerificationRequest/NannyViewVerificationRequestDetail/{notification.RelatedEntityId.Value}",
            NotificationTypes.ReportSubmitted when notification.RelatedEntityId.HasValue =>
                $"/Moderator/ViewComplaintDetail/{notification.RelatedEntityId.Value}",
            NotificationTypes.ReportSubmitted =>
                "/Moderator/ManageComplaint",
            NotificationTypes.MessageToModerator when notification.RelatedEntityId.HasValue =>
                $"/Communication?conversationId={notification.RelatedEntityId.Value}",
            NotificationTypes.JobPostingReviewRequired when notification.RelatedEntityId.HasValue =>
                $"/Moderator/ViewJobPostingDetail/{notification.RelatedEntityId.Value}",
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

    private sealed class AdminNotificationGroupProjection
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = "";
        public string Content { get; set; } = "";
        public string TargetType { get; set; } = "All";
        public string? TargetRole { get; set; }
        public bool IsDeleted { get; set; }
        public int RecipientCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public Guid? CreatedBy { get; set; }
        public Guid? UpdatedBy { get; set; }
    }
}
