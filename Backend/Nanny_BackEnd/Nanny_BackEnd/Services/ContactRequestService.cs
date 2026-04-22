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
            return Err(400, "Tai khoan khong phai parent.");

        var nannyProfile = await _nannies.FindByIdWithUserAsync(nannyProfileId);
        if (nannyProfile == null)
            return Err(404, "Khong tim thay ho so nanny.");

        if (nannyProfile.UserId == userId)
            return Err(400, "Ban khong the gui request contact cho chinh minh.");

        message = message?.Trim();
        if (!string.IsNullOrWhiteSpace(message) && message.Length > 1000)
            return Err(400, "Noi dung request contact khong duoc vuot qua 1000 ky tu.");

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
                    Body = new { success = false, message = "Ban da gui request contact den nanny nay va dang cho phan hoi." }
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
            parentName = "Mot parent";

        await _notifications.createNotification(
            nannyProfile.UserId,
            "Ban vua nhan duoc request contact",
            $"{parentName} vua gui request contact cho ho so cua ban.",
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
                    ? "Ban da gui lai request contact. Vui long cho nanny phan hoi."
                    : "Ban da gui request contact thanh cong. Vui long cho nanny phan hoi."
            }
        };
    }

    public async Task<ContactRequestEndpointResult> GetReceivedAsync(Guid userId, int? status)
    {
        if (status.HasValue && (status.Value < 0 || status.Value > 2))
            return Err(400, "Trang thai request contact khong hop le.");

        var nannyProfile = await _nannies.FindByUserIdAsync(userId);
        if (nannyProfile == null)
            return Err(400, "Tai khoan khong phai nanny.");

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
            return Err(400, "Trang thai request contact khong hop le.");

        var parentProfile = await _parents.FindByUserIdAsync(userId);
        if (parentProfile == null)
            return Err(400, "Tai khoan khong phai parent.");

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
            return Err(403, "Ban khong co quyen xem chi tiet request contact.");

        var request = await _contactRequests.GetByIdForDetailNoTrackingAsync(contactRequestId);
        if (request == null)
            return Err(404, "Khong tim thay request contact.");

        if (isParent && request.ParentProfile?.UserId != userId)
            return Err(404, "Khong tim thay request contact hoac ban khong co quyen truy cap.");

        if (isNanny && request.NannyProfile?.UserId != userId)
            return Err(404, "Khong tim thay request contact hoac ban khong co quyen truy cap.");

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
            return Err(400, "Action khong hop le. Dung 1 (accept) hoac 2 (reject).");

        responseMessage = responseMessage?.Trim();
        if (action == 2 && string.IsNullOrWhiteSpace(responseMessage))
            return Err(400, "Vui long nhap ly do khi tu choi request contact.");

        if (!string.IsNullOrWhiteSpace(responseMessage) && responseMessage.Length > 1000)
            return Err(400, "Noi dung phan hoi khong duoc vuot qua 1000 ky tu.");

        var nannyProfile = await _nannies.FindByUserIdAsync(userId);
        if (nannyProfile == null)
            return Err(400, "Tai khoan khong phai nanny.");

        var contactRequest = await _contactRequests.GetByIdForNannyReviewTrackingAsync(
            contactRequestId, nannyProfile.Id);

        if (contactRequest == null)
            return Err(404, "Khong tim thay request contact hoac ban khong co quyen xu ly.");

        if (contactRequest.Status is 1 or 2)
            return Err(400, "Request contact nay da duoc xu ly truoc do.");

        if (contactRequest.Status != 0)
            return Err(400, "Chi request contact dang cho duyet moi co the xu ly.");

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
                ? "Request contact da duoc chap nhan"
                : "Request contact bi tu choi";

            var content = isApproved
                ? $"{nannyName} da chap nhan request contact cua ban."
                : $"{nannyName} da tu choi request contact cua ban. Ly do: {responseMessage}";

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
                    ? "Ban da chap nhan request contact."
                    : "Ban da tu choi request contact."
            }
        };
    }

    private static string GetStatusLabel(int status) => status switch
    {
        0 => "Dang cho duyet",
        1 => "Da duoc chap nhan",
        2 => "Da bi tu choi",
        _ => "Dang cap nhat"
    };

    private static ContactRequestEndpointResult Err(int code, string message) => new()
    {
        StatusCode = code,
        Body = new { success = false, message }
    };
}
