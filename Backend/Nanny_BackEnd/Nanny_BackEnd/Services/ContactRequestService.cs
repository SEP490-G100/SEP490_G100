using Nanny_BackEnd.DTOs.Nanny;
using Nanny_BackEnd.Helpers;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Services;

public class ContactRequestService : IContactRequestService
{
    private readonly IContactRequestRepository _contactRequests;
    private readonly IParentRepository _parents;
    private readonly INannyProfileRepository _nannies;
    private readonly INotificationService _notifications;

    public ContactRequestService(
        IContactRequestRepository contactRequests,
        IParentRepository parents,
        INannyProfileRepository nannies,
        INotificationService notifications)
    {
        _contactRequests = contactRequests;
        _parents = parents;
        _nannies = nannies;
        _notifications = notifications;
    }

    public async Task<ContactRequestEndpointResult> SendAsync(Guid userId, Guid nannyProfileId, string? message)
    {
        var parentProfile = await _parents.FindByUserIdWithUserAsync(userId);
        if (parentProfile == null)
            return Err(400, "Tài khoản không phải parent.");

        var nannyProfile = await _nannies.FindByIdWithUserAsync(nannyProfileId);
        if (nannyProfile == null)
            return Err(404, "Không tìm thấy hồ sơ nanny.");

        if (nannyProfile.UserId == userId)
            return Err(400, "Bạn không thể gửi request contact cho chính mình.");

        message = message?.Trim();
        if (!string.IsNullOrWhiteSpace(message) && message.Length > 1000)
            return Err(400, "Nội dung request contact không được vượt quá 1000 ký tự.");

        var nowUtc = DateTime.UtcNow;
        var existingRequest = await _contactRequests.FindByParentAndNannyNotDeletedAsync(
            parentProfile.Id, nannyProfileId);

        var isResubmitted = false;
        if (existingRequest != null)
        {
            if (existingRequest.Status == 0)
                return new ContactRequestEndpointResult
                {
                    StatusCode = 409,
                    Body = new { success = false, message = "Bạn đã gửi request contact đến nanny này và đang chờ phản hồi." }
                };

            existingRequest.Status = 0;
            existingRequest.Message = message;
            existingRequest.ResponseMessage = null;
            existingRequest.RespondedAt = null;
            existingRequest.CreatedAt = nowUtc;
            existingRequest.UpdatedAt = nowUtc;
            existingRequest.UpdatedBy = userId;
            isResubmitted = true;
        }
        else
        {
            existingRequest = new ContactRequest
            {
                Id = Guid.NewGuid(),
                ParentProfileId = parentProfile.Id,
                NannyProfileId = nannyProfileId,
                Message = message,
                Status = 0,
                ResponseMessage = null,
                RespondedAt = null,
                CreatedAt = nowUtc,
                CreatedBy = userId,
                UpdatedAt = null,
                UpdatedBy = null,
                IsDeleted = false
            };
            _contactRequests.Add(existingRequest);
        }

        await _contactRequests.SaveChangesAsync();

        var parentName = $"{parentProfile.User?.FirstName} {parentProfile.User?.LastName}".Trim();
        if (string.IsNullOrWhiteSpace(parentName))
            parentName = "Một parent";

        await _notifications.createNotification(
            nannyProfile.UserId,
            "Bạn vừa nhận được request contact",
            $"{parentName} vừa gửi request contact cho hồ sơ của bạn.",
            NotificationTypes.ContactRequestReceived,
            existingRequest.Id,
            "ContactRequest",
            userId);

        return new ContactRequestEndpointResult
        {
            StatusCode = 200,
            Body = new
            {
                success = true,
                data = new
                {
                    requestId = existingRequest.Id,
                    parentUserId = userId,
                    nannyUserId = nannyProfile.UserId,
                    status = existingRequest.Status,
                    statusLabel = GetStatusLabel(existingRequest.Status),
                    createdAt = existingRequest.CreatedAt
                },
                message = isResubmitted
                    ? "Bạn đã gửi lại request contact. Vui lòng chờ nanny phản hồi."
                    : "Bạn đã gửi request contact thành công. Vui lòng chờ nanny phản hồi."
            }
        };
    }

    public async Task<ContactRequestEndpointResult> GetReceivedAsync(Guid userId, int? status)
    {
        if (status.HasValue && (status.Value < 0 || status.Value > 2))
            return Err(400, "Trạng thái request contact không hợp lệ.");

        var nannyProfile = await _nannies.FindByUserIdAsync(userId);
        if (nannyProfile == null)
            return Err(400, "Tài khoản không phải nanny.");

        var (items, total, pending, accepted, rejected) =
            await _contactRequests.GetReceivedListForNannyAsync(nannyProfile.Id, status);

        var data = items.Select(r =>
        {
            var parentUser = r.ParentProfile?.User;
            var parentName = $"{parentUser?.FirstName} {parentUser?.LastName}".Trim();
            if (string.IsNullOrWhiteSpace(parentName))
                parentName = "Parent";

            return new
            {
                id = r.Id,
                status = r.Status,
                statusLabel = GetStatusLabel(r.Status),
                message = r.Message,
                responseMessage = r.ResponseMessage,
                requestedAt = r.CreatedAt,
                respondedAt = r.RespondedAt,
                canReview = r.Status == 0,
                parent = new
                {
                    profileId = r.ParentProfileId,
                    userId = parentUser?.Id,
                    fullName = parentName,
                    avatarUrl = parentUser?.AvatarUrl,
                    phoneNumber = parentUser?.PhoneNumber,
                    city = parentUser?.City,
                    district = parentUser?.District,
                    address = parentUser?.Address
                }
            };
        }).ToList();

        return new ContactRequestEndpointResult
        {
            StatusCode = 200,
            Body = new
            {
                success = true,
                data = new
                {
                    totalRequests = total,
                    pendingRequests = pending,
                    acceptedRequests = accepted,
                    rejectedRequests = rejected,
                    requests = data
                }
            }
        };
    }

    public async Task<ContactRequestEndpointResult> GetSentAsync(Guid userId, int? status)
    {
        if (status.HasValue && (status.Value < 0 || status.Value > 2))
            return Err(400, "Trạng thái request contact không hợp lệ.");

        var parentProfile = await _parents.FindByUserIdAsync(userId);
        if (parentProfile == null)
            return Err(400, "Tài khoản không phải parent.");

        var (items, total, pending, accepted, rejected) =
            await _contactRequests.GetSentListForParentAsync(parentProfile.Id, status);

        var data = items.Select(r =>
        {
            var nannyUser = r.NannyProfile?.User;
            var nannyName = $"{nannyUser?.FirstName} {nannyUser?.LastName}".Trim();
            if (string.IsNullOrWhiteSpace(nannyName))
                nannyName = "Nanny";

            return new
            {
                id = r.Id,
                status = r.Status,
                statusLabel = GetStatusLabel(r.Status),
                message = r.Message,
                responseMessage = r.ResponseMessage,
                requestedAt = r.CreatedAt,
                respondedAt = r.RespondedAt,
                nanny = new
                {
                    profileId = r.NannyProfileId,
                    userId = nannyUser?.Id,
                    fullName = nannyName,
                    avatarUrl = nannyUser?.AvatarUrl,
                    phoneNumber = nannyUser?.PhoneNumber,
                    city = nannyUser?.City,
                    district = nannyUser?.District,
                    address = nannyUser?.Address,
                    yearsOfExperience = r.NannyProfile?.YearsOfExperience,
                    expectedSalaryMin = r.NannyProfile?.ExpectedSalaryMin,
                    expectedSalaryMax = r.NannyProfile?.ExpectedSalaryMax
                }
            };
        }).ToList();

        return new ContactRequestEndpointResult
        {
            StatusCode = 200,
            Body = new
            {
                success = true,
                data = new
                {
                    totalRequests = total,
                    pendingRequests = pending,
                    acceptedRequests = accepted,
                    rejectedRequests = rejected,
                    requests = data
                }
            }
        };
    }

    public async Task<ContactRequestEndpointResult> GetDetailAsync(
        Guid userId, Guid contactRequestId, bool isParent, bool isNanny)
    {
        if (!isParent && !isNanny)
            return Err(403, "Bạn không có quyền xem chi tiết request contact.");

        var request = await _contactRequests.GetByIdForDetailNoTrackingAsync(contactRequestId);
        if (request == null)
            return Err(404, "Không tìm thấy request contact.");

        if (isParent && request.ParentProfile?.UserId != userId)
            return Err(404, "Không tìm thấy request contact hoặc bạn không có quyền truy cập.");

        if (isNanny && request.NannyProfile?.UserId != userId)
            return Err(404, "Không tìm thấy request contact hoặc bạn không có quyền truy cập.");

        var parentUser = request.ParentProfile?.User;
        var nannyUser = request.NannyProfile?.User;

        var parentName = $"{parentUser?.FirstName} {parentUser?.LastName}".Trim();
        if (string.IsNullOrWhiteSpace(parentName))
            parentName = "Parent";

        var nannyName = $"{nannyUser?.FirstName} {nannyUser?.LastName}".Trim();
        if (string.IsNullOrWhiteSpace(nannyName))
            nannyName = "Nanny";

        return new ContactRequestEndpointResult
        {
            StatusCode = 200,
            Body = new
            {
                success = true,
                data = new
                {
                    id = request.Id,
                    status = request.Status,
                    statusLabel = GetStatusLabel(request.Status),
                    message = request.Message,
                    responseMessage = request.ResponseMessage,
                    requestedAt = request.CreatedAt,
                    respondedAt = request.RespondedAt,
                    canReview = isNanny && request.Status == 0,
                    parent = new
                    {
                        profileId = request.ParentProfileId,
                        userId = parentUser?.Id,
                        fullName = parentName,
                        avatarUrl = parentUser?.AvatarUrl,
                        phoneNumber = parentUser?.PhoneNumber,
                        city = parentUser?.City,
                        district = parentUser?.District,
                        address = parentUser?.Address
                    },
                    nanny = new
                    {
                        profileId = request.NannyProfileId,
                        userId = nannyUser?.Id,
                        fullName = nannyName,
                        avatarUrl = nannyUser?.AvatarUrl,
                        phoneNumber = nannyUser?.PhoneNumber,
                        city = nannyUser?.City,
                        district = nannyUser?.District,
                        address = nannyUser?.Address,
                        yearsOfExperience = request.NannyProfile?.YearsOfExperience,
                        expectedSalaryMin = request.NannyProfile?.ExpectedSalaryMin,
                        expectedSalaryMax = request.NannyProfile?.ExpectedSalaryMax
                    }
                }
            }
        };
    }

    public async Task<ContactRequestEndpointResult> ReviewAsync(
        Guid userId,
        Guid contactRequestId,
        int action,
        string? responseMessage)
    {
        if (action is not 1 and not 2)
            return Err(400, "Action không hợp lệ. Dùng 1 (accept) hoặc 2 (reject).");

        responseMessage = responseMessage?.Trim();
        if (action == 2 && string.IsNullOrWhiteSpace(responseMessage))
            return Err(400, "Vui lòng nhập lý do khi từ chối request contact.");

        if (!string.IsNullOrWhiteSpace(responseMessage) && responseMessage.Length > 1000)
            return Err(400, "Nội dung phản hồi không được vượt quá 1000 ký tự.");

        var nannyProfile = await _nannies.FindByUserIdAsync(userId);
        if (nannyProfile == null)
            return Err(400, "Tài khoản không phải nanny.");

        var contactRequest = await _contactRequests.GetByIdForNannyReviewTrackingAsync(
            contactRequestId, nannyProfile.Id);

        if (contactRequest == null)
            return Err(404, "Không tìm thấy request contact hoặc bạn không có quyền xử lý.");

        if (contactRequest.Status is 1 or 2)
            return Err(400, "Request contact này đã được xử lý trước đó.");

        if (contactRequest.Status != 0)
            return Err(400, "Chỉ request contact đang chờ duyệt mới có thể xử lý.");

        var nowUtc = DateTime.UtcNow;
        var isApproved = action == 1;

        contactRequest.Status = isApproved ? 1 : 2;
        contactRequest.ResponseMessage = responseMessage;
        contactRequest.RespondedAt = nowUtc;
        contactRequest.UpdatedAt = nowUtc;
        contactRequest.UpdatedBy = userId;

        await _contactRequests.SaveChangesAsync();

        var parentUserId = contactRequest.ParentProfile?.UserId ?? Guid.Empty;
        if (parentUserId != Guid.Empty)
        {
            var nannyName = $"{contactRequest.NannyProfile?.User?.FirstName} {contactRequest.NannyProfile?.User?.LastName}".Trim();
            if (string.IsNullOrWhiteSpace(nannyName))
                nannyName = "Nanny";

            var title = isApproved
                ? "Request contact đã được chấp nhận"
                : "Request contact bị từ chối";

            var content = isApproved
                ? $"{nannyName} đã chấp nhận request contact của bạn."
                : $"{nannyName} đã từ chối request contact của bạn. Lý do: {responseMessage}";

            await _notifications.createNotification(
                parentUserId,
                title,
                content,
                isApproved ? NotificationTypes.ContactRequestAccepted : NotificationTypes.ContactRequestRejected,
                contactRequest.Id,
                "ContactRequest",
                userId);
        }

        return new ContactRequestEndpointResult
        {
            StatusCode = 200,
            Body = new
            {
                success = true,
                data = new
                {
                    requestId = contactRequest.Id,
                    parentUserId,
                    nannyUserId = contactRequest.NannyProfile?.UserId,
                    status = contactRequest.Status,
                    statusLabel = GetStatusLabel(contactRequest.Status),
                    responseMessage = contactRequest.ResponseMessage,
                    respondedAt = contactRequest.RespondedAt
                },
                message = isApproved
                    ? "Bạn đã chấp nhận request contact."
                    : "Bạn đã từ chối request contact."
            }
        };
    }

    private static string GetStatusLabel(int status) => status switch
    {
        0 => "Đang chờ duyệt",
        1 => "Đã được chấp nhận",
        2 => "Đã bị từ chối",
        _ => "Đang cập nhật"
    };

    private static ContactRequestEndpointResult Err(int code, string message) => new()
    {
        StatusCode = code,
        Body = new { success = false, message }
    };
}
