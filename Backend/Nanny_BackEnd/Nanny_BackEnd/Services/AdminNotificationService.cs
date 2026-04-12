using System.ComponentModel.DataAnnotations;
using Nanny_BackEnd.DTOs.Notification;
using Nanny_BackEnd.Helpers;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories;

namespace Nanny_BackEnd.Services;

public class AdminNotificationService
{
    private const string AdminNotificationScopePrefix = "AdminNotification";
    private readonly AdminNotificationRepository _adminNotificationRepository;

    public AdminNotificationService(AdminNotificationRepository adminNotificationRepository)
    {
        _adminNotificationRepository = adminNotificationRepository;
    }

    public async Task<AdminNotificationListResponse> AdminViewNotificationListAsync(
        string? search,
        bool? isDeleted,
        int page,
        int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var rows = await _adminNotificationRepository.GetAdminNotificationRowsAsync(search, isDeleted);
        var grouped = rows
            .Where(n => n.RelatedEntityId.HasValue)
            .GroupBy(n => n.RelatedEntityId!.Value)
            .Select(MapAdminNotificationGroup)
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

    public async Task<List<string>> AdminViewNotificationRoleListAsync() =>
        await _adminNotificationRepository.GetNotificationAssignableRolesAsync();

    public async Task<AdminNotificationDetailResponse?> AdminViewNotificationDetailAsync(Guid broadcastId)
    {
        var rows = await _adminNotificationRepository.GetAdminNotificationRowsByBroadcastIdAsync(broadcastId);
        if (rows.Count == 0)
            return null;

        var group = MapAdminNotificationGroup(rows);
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

    public async Task<AdminNotificationDetailResponse> AdminCreateNotificationAsync(
        Guid adminUserId,
        AdminNotificationUpsertRequest request)
    {
        ValidateUpsertRequest(request);

        var title = request.Title.Trim();
        var content = request.Content.Trim();
        var targetType = NormalizeTargetType(request.TargetType);
        var targetRole = NormalizeTargetRole(request.TargetRole);

        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(content))
            throw new InvalidOperationException("Tieu de va noi dung thong bao la bat buoc.");

        var recipientIds = await ResolveRecipientsAsync(targetType, targetRole);
        if (recipientIds.Count == 0)
            throw new InvalidOperationException("Khong tim thay nguoi nhan phu hop de gui thong bao.");

        var broadcastId = Guid.NewGuid();
        var nowUtc = DateTime.UtcNow;
        var notifications = recipientIds.Select(userId => new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = title,
            Content = content,
            Type = NotificationTypes.AdminBroadcast,
            IsRead = false,
            RelatedEntityId = broadcastId,
            RelatedEntityType = BuildAdminNotificationScope(targetType, targetRole),
            CreatedAt = nowUtc,
            CreatedBy = adminUserId,
            IsDeleted = false
        }).ToList();

        _adminNotificationRepository.AddNotifications(notifications);
        await _adminNotificationRepository.SaveChangesAsync();

        return (await AdminViewNotificationDetailAsync(broadcastId))!;
    }

    public async Task<AdminNotificationDetailResponse> AdminUpdateNotificationAsync(
        Guid broadcastId,
        Guid adminUserId,
        AdminNotificationUpsertRequest request)
    {
        ValidateUpsertRequest(request);

        var rows = await _adminNotificationRepository.GetAdminNotificationRowsByBroadcastIdAsync(broadcastId);
        if (rows.Count == 0)
            throw new KeyNotFoundException("Khong tim thay thong bao admin can cap nhat.");

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

        await _adminNotificationRepository.SaveChangesAsync();
        return (await AdminViewNotificationDetailAsync(broadcastId))!;
    }

    public async Task AdminUpdateNotificationStatusAsync(Guid broadcastId, Guid adminUserId, bool isDeleted)
    {
        var rows = await _adminNotificationRepository.GetAdminNotificationRowsByBroadcastIdAsync(broadcastId);
        if (rows.Count == 0)
            throw new KeyNotFoundException("Khong tim thay thong bao admin can cap nhat.");

        var nowUtc = DateTime.UtcNow;
        foreach (var row in rows)
        {
            row.IsDeleted = isDeleted;
            row.UpdatedAt = nowUtc;
            row.UpdatedBy = adminUserId;
        }

        await _adminNotificationRepository.SaveChangesAsync();
    }

    private async Task<List<Guid>> ResolveRecipientsAsync(string targetType, string? targetRole)
    {
        if (targetType == "Role")
            return await _adminNotificationRepository.GetActiveUserIdsByRoleAsync(targetRole!);

        return await _adminNotificationRepository.GetActiveUserIdsByRolesAsync(["Parent", "Nanny", "Moderator"]);
    }

    private static string NormalizeTargetType(string targetType) =>
        string.Equals(targetType?.Trim(), "Role", StringComparison.OrdinalIgnoreCase) ? "Role" : "All";

    private static string? NormalizeTargetRole(string? targetRole)
    {
        if (string.IsNullOrWhiteSpace(targetRole))
            return null;

        var normalized = targetRole.Trim();
        return normalized is "Parent" or "Nanny" or "Moderator" or "Admin" ? normalized : null;
    }

    private static string BuildAdminNotificationScope(string targetType, string? targetRole) =>
        targetType == "Role"
            ? $"{AdminNotificationScopePrefix}:Role:{targetRole}"
            : $"{AdminNotificationScopePrefix}:All";

    private static (string TargetType, string? TargetRole) ParseAdminNotificationScope(string? relatedEntityType)
    {
        if (string.IsNullOrWhiteSpace(relatedEntityType))
            return ("All", null);

        var parts = relatedEntityType.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length >= 2 && string.Equals(parts[1], "Role", StringComparison.OrdinalIgnoreCase))
            return ("Role", parts.Length >= 3 ? parts[2] : null);

        return ("All", null);
    }

    private static AdminNotificationGroupProjection MapAdminNotificationGroup(IEnumerable<Notification> rows)
    {
        var materialized = rows.ToList();
        var sample = materialized
            .OrderByDescending(n => n.UpdatedAt ?? n.CreatedAt)
            .First();
        var scope = ParseAdminNotificationScope(sample.RelatedEntityType);

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

    private static void ValidateUpsertRequest(AdminNotificationUpsertRequest request)
    {
        var context = new ValidationContext(request);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(request, context, validationResults, true);
        if (isValid)
            return;

        var firstMessage = validationResults.FirstOrDefault()?.ErrorMessage ?? "Du lieu khong hop le.";
        throw new InvalidOperationException(firstMessage);
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
