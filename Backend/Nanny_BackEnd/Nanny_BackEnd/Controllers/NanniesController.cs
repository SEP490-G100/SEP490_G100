using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nanny_BackEnd.Data;
using Nanny_BackEnd.DTOs.Nanny;
using Nanny_BackEnd.Helpers;
using Nanny_BackEnd.Repositories;
using Nanny_BackEnd.Services;

namespace Nanny_BackEnd.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NanniesController : ControllerBase
{
    private readonly NannyService _nannyService;
    private readonly ParentRepository _parentRepository;
    private readonly Sep490NannyDbContext _db;
    private readonly NotificationService _notificationService;

    public NanniesController(
        NannyService nannyService,
        ParentRepository parentRepository,
        Sep490NannyDbContext db,
        NotificationService notificationService)
    {
        _nannyService = nannyService;
        _parentRepository = parentRepository;
        _db = db;
        _notificationService = notificationService;
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> GetNannies([FromQuery] NannyListRequest request)
    {
        try
        {
            var currentParentProfileId = await TryGetCurrentParentProfileId();
            var result = await _nannyService.GetListAsync(request, currentParentProfileId);
            return Ok(new
            {
                success = true,
                data = result.Items,
                totalCount = result.TotalCount,
                page = result.Page,
                pageSize = result.PageSize
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, Fail(ex.Message));
        }
    }

    [AllowAnonymous]
    [HttpGet("{nannyProfileId:guid}")]
    public async Task<IActionResult> GetNannyDetail(Guid nannyProfileId)
    {
        try
        {
            var currentParentProfileId = await TryGetCurrentParentProfileId();
            var detail = await _nannyService.GetDetailAsync(nannyProfileId, currentParentProfileId);
            return Ok(new { success = true, data = detail });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(Fail(ex.Message));
        }
        catch (Exception ex)
        {
            return StatusCode(500, Fail(ex.Message));
        }
    }

    [Authorize]
    [HttpGet("favorites/me")]
    public async Task<IActionResult> GetMyFavoriteNannies([FromQuery] int page = 1, [FromQuery] int pageSize = 12)
    {
        try
        {
            if (!User.IsInRole("Parent"))
                return StatusCode(403, Fail("Chỉ phụ huynh mới có quyền xem danh sách bảo mẫu yêu thích."));

            var userId = GetCurrentUserId();
            var parentProfile = await _parentRepository.FindByUserIdAsync(userId);
            if (parentProfile == null)
                return BadRequest(Fail("Tài khoản không phải phụ huynh."));

            var result = await _nannyService.GetFavoritesAsync(parentProfile.Id, page, pageSize);
            return Ok(new
            {
                success = true,
                data = result.Items,
                totalCount = result.TotalCount,
                page = result.Page,
                pageSize = result.PageSize
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, Fail(ex.Message));
        }
    }

    [Authorize]
    [HttpPost("{nannyProfileId:guid}/favorite/toggle")]
    public async Task<IActionResult> ToggleFavorite(Guid nannyProfileId)
    {
        try
        {
            if (!User.IsInRole("Parent"))
                return StatusCode(403, Fail("Chỉ phụ huynh mới có quyền yêu thích bảo mẫu."));

            var userId = GetCurrentUserId();
            var parentProfile = await _parentRepository.FindByUserIdAsync(userId);
            if (parentProfile == null)
                return BadRequest(Fail("Tài khoản không phải phụ huynh."));

            var favoriteResult = await _nannyService.ToggleFavoriteAsync(parentProfile.Id, nannyProfileId, userId);
            return Ok(new
            {
                success = true,
                isFavorite = favoriteResult.IsFavorite,
                nannyUserId = favoriteResult.NannyUserId,
                message = favoriteResult.IsFavorite ? "Đã yêu thích bảo mẫu." : "Đã bỏ yêu thích bảo mẫu."
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(Fail(ex.Message));
        }
        catch (Exception ex)
        {
            return StatusCode(500, Fail(ex.Message));
        }
    }

    [Authorize]
    [HttpPost("{nannyProfileId:guid}/contact-request")]
    public async Task<IActionResult> SendContactRequest(Guid nannyProfileId, [FromBody] SendContactRequestPayload? request)
    {
        try
        {
            if (!User.IsInRole("Parent"))
                return StatusCode(403, Fail("Chỉ phụ huynh mới có quyền gửi yêu cầu liên hệ."));

            var userId = GetCurrentUserId();
            var parentProfile = await _db.ParentProfiles
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.UserId == userId && !p.IsDeleted);

            if (parentProfile == null)
                return BadRequest(Fail("Tài khoản không phải phụ huynh."));

            var nannyProfile = await _db.NannyProfiles
                .Include(n => n.User)
                .FirstOrDefaultAsync(n => n.Id == nannyProfileId && !n.IsDeleted);

            if (nannyProfile == null)
                return NotFound(Fail("Không tìm thấy hồ sơ bảo mẫu."));

            if (nannyProfile.UserId == userId)
                return BadRequest(Fail("Bạn không thể gửi yêu cầu liên hệ cho chính mình."));

            var message = request?.Message?.Trim();
            if (!string.IsNullOrWhiteSpace(message) && message.Length > 1000)
                return BadRequest(Fail("Nội dung yêu cầu liên hệ không được vượt quá 1000 ký tự."));

            var nowUtc = DateTime.UtcNow;
            var existingRequest = await _db.ContactRequests
                .FirstOrDefaultAsync(r =>
                    r.ParentProfileId == parentProfile.Id &&
                    r.NannyProfileId == nannyProfileId &&
                    !r.IsDeleted);

            var isResubmitted = false;
            if (existingRequest != null)
            {
                if (existingRequest.Status == 0)
                    return Conflict(Fail("Bạn đã gửi yêu cầu liên hệ đến bảo mẫu này và đang chờ phản hồi."));

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
                existingRequest = new Models.ContactRequest
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

                _db.ContactRequests.Add(existingRequest);
            }

            await _db.SaveChangesAsync();

            var parentName = $"{parentProfile.User?.FirstName} {parentProfile.User?.LastName}".Trim();
            if (string.IsNullOrWhiteSpace(parentName))
                parentName = "Một phụ huynh";

            await _notificationService.createNotification(
                nannyProfile.UserId,
                "Bạn vừa nhận được yêu cầu liên hệ",
                $"{parentName} vừa gửi yêu cầu liên hệ cho hồ sơ của bạn.",
                NotificationTypes.ContactRequestReceived,
                existingRequest.Id,
                "ContactRequest",
                userId);

            return Ok(new
            {
                success = true,
                data = new
                {
                    requestId = existingRequest.Id,
                    parentUserId = userId,
                    nannyUserId = nannyProfile.UserId,
                    status = existingRequest.Status,
                    statusLabel = getContactRequestStatusLabel(existingRequest.Status),
                    createdAt = existingRequest.CreatedAt
                },
                message = isResubmitted
                    ? "Bạn đã gửi lại yêu cầu liên hệ. Vui lòng chờ bảo mẫu phản hồi."
                    : "Bạn đã gửi yêu cầu liên hệ thành công. Vui lòng chờ bảo mẫu phản hồi."
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, Fail(ex.Message));
        }
    }

    [Authorize]
    [HttpGet("contact-requests/received")]
    public async Task<IActionResult> GetReceivedContactRequests([FromQuery] int? status = null)
    {
        try
        {
            if (!User.IsInRole("Nanny"))
                return StatusCode(403, Fail("Chỉ bảo mẫu mới có quyền xem yêu cầu liên hệ đã nhận."));

            if (status.HasValue && (status.Value < 0 || status.Value > 2))
                return BadRequest(Fail("Trạng thái yêu cầu liên hệ không hợp lệ."));

            var userId = GetCurrentUserId();
            var nannyProfile = await _db.NannyProfiles
                .FirstOrDefaultAsync(n => n.UserId == userId && !n.IsDeleted);
            if (nannyProfile == null)
                return BadRequest(Fail("Tài khoản không phải bảo mẫu."));

            var baseQuery = _db.ContactRequests
                .Where(r => r.NannyProfileId == nannyProfile.Id && !r.IsDeleted)
                .Include(r => r.ParentProfile)
                    .ThenInclude(p => p.User)
                .AsNoTracking();

            var totalRequests = await baseQuery.CountAsync();
            var pendingRequests = await baseQuery.CountAsync(r => r.Status == 0);
            var acceptedRequests = await baseQuery.CountAsync(r => r.Status == 1);
            var rejectedRequests = await baseQuery.CountAsync(r => r.Status == 2);

            var query = baseQuery;
            if (status.HasValue)
                query = query.Where(r => r.Status == status.Value);

            var requests = await query
                .OrderBy(r => r.Status == 0 ? 0 : 1)
                .ThenByDescending(r => r.CreatedAt)
                .ToListAsync();

            var data = requests.Select(r =>
            {
                var parentUser = r.ParentProfile?.User;
                var parentName = $"{parentUser?.FirstName} {parentUser?.LastName}".Trim();
                if (string.IsNullOrWhiteSpace(parentName))
                    parentName = "Parent";

                return new
                {
                    id = r.Id,
                    status = r.Status,
                    statusLabel = getContactRequestStatusLabel(r.Status),
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

            return Ok(new
            {
                success = true,
                data = new
                {
                    totalRequests,
                    pendingRequests,
                    acceptedRequests,
                    rejectedRequests,
                    requests = data
                }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, Fail(ex.Message));
        }
    }

    [Authorize]
    [HttpGet("contact-requests/sent")]
    public async Task<IActionResult> GetSentContactRequests([FromQuery] int? status = null)
    {
        try
        {
            if (!User.IsInRole("Parent"))
                return StatusCode(403, Fail("Chỉ phụ huynh mới có quyền xem yêu cầu liên hệ đã gửi."));

            if (status.HasValue && (status.Value < 0 || status.Value > 2))
                return BadRequest(Fail("Trạng thái yêu cầu liên hệ không hợp lệ."));

            var userId = GetCurrentUserId();
            var parentProfile = await _db.ParentProfiles
                .FirstOrDefaultAsync(p => p.UserId == userId && !p.IsDeleted);
            if (parentProfile == null)
                return BadRequest(Fail("Tài khoản không phải phụ huynh."));

            var baseQuery = _db.ContactRequests
                .Where(r => r.ParentProfileId == parentProfile.Id && !r.IsDeleted)
                .Include(r => r.NannyProfile)
                    .ThenInclude(n => n.User)
                .AsNoTracking();

            var totalRequests = await baseQuery.CountAsync();
            var pendingRequests = await baseQuery.CountAsync(r => r.Status == 0);
            var acceptedRequests = await baseQuery.CountAsync(r => r.Status == 1);
            var rejectedRequests = await baseQuery.CountAsync(r => r.Status == 2);

            var query = baseQuery;
            if (status.HasValue)
                query = query.Where(r => r.Status == status.Value);

            var requests = await query
                .OrderBy(r => r.Status == 0 ? 0 : 1)
                .ThenByDescending(r => r.CreatedAt)
                .ToListAsync();

            var data = requests.Select(r =>
            {
                var nannyUser = r.NannyProfile?.User;
                var nannyName = $"{nannyUser?.FirstName} {nannyUser?.LastName}".Trim();
                if (string.IsNullOrWhiteSpace(nannyName))
                    nannyName = "Nanny";

                return new
                {
                    id = r.Id,
                    status = r.Status,
                    statusLabel = getContactRequestStatusLabel(r.Status),
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

            return Ok(new
            {
                success = true,
                data = new
                {
                    totalRequests,
                    pendingRequests,
                    acceptedRequests,
                    rejectedRequests,
                    requests = data
                }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, Fail(ex.Message));
        }
    }

    [Authorize]
    [HttpGet("contact-requests/{contactRequestId:guid}")]
    public async Task<IActionResult> GetContactRequestDetail(Guid contactRequestId)
    {
        try
        {
            var isParent = User.IsInRole("Parent");
            var isNanny = User.IsInRole("Nanny");
            if (!isParent && !isNanny)
                return StatusCode(403, Fail("Bạn không có quyền xem chi tiết yêu cầu liên hệ."));

            var userId = GetCurrentUserId();
            var request = await _db.ContactRequests
                .Include(r => r.ParentProfile)
                    .ThenInclude(p => p.User)
                .Include(r => r.NannyProfile)
                    .ThenInclude(n => n.User)
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == contactRequestId && !r.IsDeleted);

            if (request == null)
                return NotFound(Fail("Không tìm thấy yêu cầu liên hệ."));

            if (isParent && request.ParentProfile?.UserId != userId)
                return NotFound(Fail("Không tìm thấy yêu cầu liên hệ hoặc bạn không có quyền truy cập."));

            if (isNanny && request.NannyProfile?.UserId != userId)
                return NotFound(Fail("Không tìm thấy yêu cầu liên hệ hoặc bạn không có quyền truy cập."));

            var parentUser = request.ParentProfile?.User;
            var nannyUser = request.NannyProfile?.User;

            var parentName = $"{parentUser?.FirstName} {parentUser?.LastName}".Trim();
            if (string.IsNullOrWhiteSpace(parentName))
                parentName = "Parent";

            var nannyName = $"{nannyUser?.FirstName} {nannyUser?.LastName}".Trim();
            if (string.IsNullOrWhiteSpace(nannyName))
                nannyName = "Nanny";

            return Ok(new
            {
                success = true,
                data = new
                {
                    id = request.Id,
                    status = request.Status,
                    statusLabel = getContactRequestStatusLabel(request.Status),
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
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, Fail(ex.Message));
        }
    }

    [Authorize]
    [HttpPost("contact-requests/{contactRequestId:guid}/review")]
    public async Task<IActionResult> ReviewContactRequest(Guid contactRequestId, [FromBody] ReviewContactRequestPayload? request)
    {
        try
        {
            if (!User.IsInRole("Nanny"))
                return StatusCode(403, Fail("Chỉ bảo mẫu mới có quyền xử lý yêu cầu liên hệ."));

            if (request == null)
                return BadRequest(Fail("Dữ liệu xử lý yêu cầu liên hệ không hợp lệ."));

            if (request.Action is not 1 and not 2)
                return BadRequest(Fail("Thao tác không hợp lệ. Dùng 1 (chấp nhận) hoặc 2 (từ chối)."));

            var responseMessage = request.ResponseMessage?.Trim();
            if (request.Action == 2 && string.IsNullOrWhiteSpace(responseMessage))
                return BadRequest(Fail("Vui lòng nhập lý do khi từ chối yêu cầu liên hệ."));

            if (!string.IsNullOrWhiteSpace(responseMessage) && responseMessage.Length > 1000)
                return BadRequest(Fail("Nội dung phản hồi không được vượt quá 1000 ký tự."));

            var userId = GetCurrentUserId();
            var nannyProfileId = await _db.NannyProfiles
                .Where(n => n.UserId == userId && !n.IsDeleted)
                .Select(n => (Guid?)n.Id)
                .FirstOrDefaultAsync();

            if (!nannyProfileId.HasValue)
                return BadRequest(Fail("Tài khoản không phải bảo mẫu."));

            var contactRequest = await _db.ContactRequests
                .Include(r => r.ParentProfile)
                    .ThenInclude(p => p.User)
                .Include(r => r.NannyProfile)
                    .ThenInclude(n => n.User)
                .FirstOrDefaultAsync(r => r.Id == contactRequestId && !r.IsDeleted);

            if (contactRequest == null || contactRequest.NannyProfileId != nannyProfileId.Value)
                return NotFound(Fail("Không tìm thấy yêu cầu liên hệ hoặc bạn không có quyền xử lý."));

            if (contactRequest.Status is 1 or 2)
                return BadRequest(Fail("Yêu cầu liên hệ này đã được xử lý trước đó."));

            if (contactRequest.Status != 0)
                return BadRequest(Fail("Chỉ yêu cầu liên hệ đang chờ duyệt mới có thể xử lý."));

            var nowUtc = DateTime.UtcNow;
            var isApproved = request.Action == 1;

            contactRequest.Status = isApproved ? 1 : 2;
            contactRequest.ResponseMessage = responseMessage;
            contactRequest.RespondedAt = nowUtc;
            contactRequest.UpdatedAt = nowUtc;
            contactRequest.UpdatedBy = userId;

            await _db.SaveChangesAsync();

            var parentUserId = contactRequest.ParentProfile?.UserId ?? Guid.Empty;
            var nannyUserId = contactRequest.NannyProfile?.UserId ?? Guid.Empty;
            if (parentUserId != Guid.Empty)
            {
                var nannyName = $"{contactRequest.NannyProfile?.User?.FirstName} {contactRequest.NannyProfile?.User?.LastName}".Trim();
                if (string.IsNullOrWhiteSpace(nannyName))
                    nannyName = "Nanny";

                var title = isApproved
                    ? "Yêu cầu liên hệ đã được chấp nhận"
                    : "Yêu cầu liên hệ bị từ chối";

                var content = isApproved
                    ? $"{nannyName} đã chấp nhận yêu cầu liên hệ của bạn."
                    : $"{nannyName} đã từ chối yêu cầu liên hệ của bạn. Lý do: {responseMessage}";

                await _notificationService.createNotification(
                    parentUserId,
                    title,
                    content,
                    isApproved ? NotificationTypes.ContactRequestAccepted : NotificationTypes.ContactRequestRejected,
                    contactRequest.Id,
                    "ContactRequest",
                    userId);
            }

            return Ok(new
            {
                success = true,
                data = new
                {
                    requestId = contactRequest.Id,
                    parentUserId,
                    nannyUserId,
                    status = contactRequest.Status,
                    statusLabel = getContactRequestStatusLabel(contactRequest.Status),
                    responseMessage = contactRequest.ResponseMessage,
                    respondedAt = contactRequest.RespondedAt
                },
                message = isApproved
                    ? "Bạn đã chấp nhận yêu cầu liên hệ."
                    : "Bạn đã từ chối yêu cầu liên hệ."
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, Fail(ex.Message));
        }
    }

    private Guid GetCurrentUserId()
    {
        var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
               ?? User.FindFirst("sub")?.Value;
        return Guid.Parse(sub!);
    }

    private async Task<Guid?> TryGetCurrentParentProfileId()
    {
        if (!User.Identity?.IsAuthenticated ?? true)
            return null;
        if (!User.IsInRole("Parent"))
            return null;

        var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
               ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(sub, out var userId))
            return null;

        var parentProfile = await _parentRepository.FindByUserIdAsync(userId);
        return parentProfile?.Id;
    }

    private static object Fail(string message) => new { success = false, message };

    private static string getContactRequestStatusLabel(int status) => status switch
    {
        0 => "Đang chờ duyệt",
        1 => "Đã được chấp nhận",
        2 => "Đã bị từ chối",
        _ => "Đang cập nhật"
    };

    public sealed class SendContactRequestPayload
    {
        public string? Message { get; set; }
    }

    public sealed class ReviewContactRequestPayload
    {
        public int Action { get; set; } // 1 = Accept, 2 = Reject
        public string? ResponseMessage { get; set; }
    }
}
