using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nanny_BackEnd.DTOs.Notification;

namespace Nanny_BackEnd.Services.Interfaces;

public interface IAdminNotificationService
{
    Task<AdminNotificationListResponse> AdminViewNotificationListAsync(
        string? search,
        bool? isDeleted,
        int page,
        int pageSize);
    Task<List<string>> AdminViewNotificationRoleListAsync();
    Task<AdminNotificationDetailResponse?> AdminViewNotificationDetailAsync(Guid broadcastId);
    Task<AdminNotificationDetailResponse> AdminCreateNotificationAsync(
        Guid adminUserId,
        AdminNotificationUpsertRequest request);
    Task<AdminNotificationDetailResponse> AdminUpdateNotificationAsync(
        Guid broadcastId,
        Guid adminUserId,
        AdminNotificationUpsertRequest request);
    Task AdminUpdateNotificationStatusAsync(Guid broadcastId, Guid adminUserId, bool isDeleted);
}
