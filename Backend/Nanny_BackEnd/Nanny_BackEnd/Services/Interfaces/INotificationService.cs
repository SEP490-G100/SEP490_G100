using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nanny_BackEnd.DTOs.Notification;
using Nanny_BackEnd.Models;

namespace Nanny_BackEnd.Services.Interfaces;

public interface INotificationService
{
    Task<NotificationListResponse> getMyNotifications(Guid userId, int page, int pageSize);
    Task<int> getUnreadCount(Guid userId);
    Task<NotificationResponse> markAsRead(Guid userId, Guid notificationId);
    Task<int> markAllAsRead(Guid userId);
    Task<int> createSubscriptionExpiryReminders();
    Task createNotification(
        Guid userId,
        string title,
        string content,
        int type,
        Guid? relatedEntityId = null,
        string? relatedEntityType = null,
        Guid? createdBy = null);
    Task createNotificationForUsers(
        IEnumerable<Guid> userIds,
        string title,
        string content,
        int type,
        Guid? relatedEntityId = null,
        string? relatedEntityType = null,
        Guid? createdBy = null);
    Task createNotificationForModerators(
        string title,
        string content,
        int type,
        Guid? relatedEntityId = null,
        string? relatedEntityType = null,
        Guid? createdBy = null);
    Task<AdminNotificationListResponse> getAdminNotifications(string? search, bool? isDeleted, int page, int pageSize);
    Task<AdminNotificationDetailResponse?> getAdminNotificationDetail(Guid broadcastId);
    Task<AdminNotificationDetailResponse> createAdminNotification(Guid adminUserId, AdminNotificationUpsertRequest request);
    Task<AdminNotificationDetailResponse> updateAdminNotification(
        Guid broadcastId,
        Guid adminUserId,
        AdminNotificationUpsertRequest request);
    Task toggleAdminNotificationStatus(Guid broadcastId, Guid adminUserId, bool isDeleted);
}
