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
                return StatusCode(403, Fail("Chi parent moi co quyen xem danh sach nanny yeu thich."));

            var userId = GetCurrentUserId();
            var parentProfile = await _parentRepository.FindByUserIdAsync(userId);
            if (parentProfile == null)
                return BadRequest(Fail("Tai khoan khong phai parent."));

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
                return StatusCode(403, Fail("Chi parent moi co quyen yeu thich nanny."));

            var userId = GetCurrentUserId();
            var parentProfile = await _parentRepository.FindByUserIdAsync(userId);
            if (parentProfile == null)
                return BadRequest(Fail("Tai khoan khong phai parent."));

            var favoriteResult = await _nannyService.ToggleFavoriteAsync(parentProfile.Id, nannyProfileId, userId);
            return Ok(new
            {
                success = true,
                isFavorite = favoriteResult.IsFavorite,
                nannyUserId = favoriteResult.NannyUserId,
                message = favoriteResult.IsFavorite ? "Da yeu thich nanny." : "Da bo yeu thich nanny."
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
                return StatusCode(403, Fail("Chi parent moi co quyen gui request contact."));

            var userId = GetCurrentUserId();
            var parentProfile = await _db.ParentProfiles
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.UserId == userId && !p.IsDeleted);

            if (parentProfile == null)
                return BadRequest(Fail("Tai khoan khong phai parent."));

            var nannyProfile = await _db.NannyProfiles
                .Include(n => n.User)
                .FirstOrDefaultAsync(n => n.Id == nannyProfileId && !n.IsDeleted);

            if (nannyProfile == null)
                return NotFound(Fail("Khong tim thay ho so nanny."));

            if (nannyProfile.UserId == userId)
                return BadRequest(Fail("Ban khong the gui request contact cho chinh minh."));

            var message = request?.Message?.Trim();
            if (!string.IsNullOrWhiteSpace(message) && message.Length > 1000)
                return BadRequest(Fail("Noi dung request contact khong duoc vuot qua 1000 ky tu."));

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
                    return Conflict(Fail("Ban da gui request contact den nanny nay va dang cho phan hoi."));

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
                parentName = "Mot parent";

            await _notificationService.createNotification(
                nannyProfile.UserId,
                "Ban vua nhan duoc request contact",
                $"{parentName} vua gui request contact cho ho so cua ban.",
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
                    ? "Ban da gui lai request contact. Vui long cho nanny phan hoi."
                    : "Ban da gui request contact thanh cong. Vui long cho nanny phan hoi."
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
                return StatusCode(403, Fail("Chi nanny moi co quyen xem request contact da nhan."));

            if (status.HasValue && (status.Value < 0 || status.Value > 2))
                return BadRequest(Fail("Trang thai request contact khong hop le."));

            var userId = GetCurrentUserId();
            var nannyProfile = await _db.NannyProfiles
                .FirstOrDefaultAsync(n => n.UserId == userId && !n.IsDeleted);
            if (nannyProfile == null)
                return BadRequest(Fail("Tai khoan khong phai nanny."));

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
                return StatusCode(403, Fail("Chi parent moi co quyen xem request contact da gui."));

            if (status.HasValue && (status.Value < 0 || status.Value > 2))
                return BadRequest(Fail("Trang thai request contact khong hop le."));

            var userId = GetCurrentUserId();
            var parentProfile = await _db.ParentProfiles
                .FirstOrDefaultAsync(p => p.UserId == userId && !p.IsDeleted);
            if (parentProfile == null)
                return BadRequest(Fail("Tai khoan khong phai parent."));

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
                return StatusCode(403, Fail("Ban khong co quyen xem chi tiet request contact."));

            var userId = GetCurrentUserId();
            var request = await _db.ContactRequests
                .Include(r => r.ParentProfile)
                    .ThenInclude(p => p.User)
                .Include(r => r.NannyProfile)
                    .ThenInclude(n => n.User)
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == contactRequestId && !r.IsDeleted);

            if (request == null)
                return NotFound(Fail("Khong tim thay request contact."));

            if (isParent && request.ParentProfile?.UserId != userId)
                return NotFound(Fail("Khong tim thay request contact hoac ban khong co quyen truy cap."));

            if (isNanny && request.NannyProfile?.UserId != userId)
                return NotFound(Fail("Khong tim thay request contact hoac ban khong co quyen truy cap."));

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
                return StatusCode(403, Fail("Chi nanny moi co quyen xu ly request contact."));

            if (request == null)
                return BadRequest(Fail("Du lieu xu ly request contact khong hop le."));

            if (request.Action is not 1 and not 2)
                return BadRequest(Fail("Action khong hop le. Dung 1 (accept) hoac 2 (reject)."));

            var responseMessage = request.ResponseMessage?.Trim();
            if (request.Action == 2 && string.IsNullOrWhiteSpace(responseMessage))
                return BadRequest(Fail("Vui long nhap ly do khi tu choi request contact."));

            if (!string.IsNullOrWhiteSpace(responseMessage) && responseMessage.Length > 1000)
                return BadRequest(Fail("Noi dung phan hoi khong duoc vuot qua 1000 ky tu."));

            var userId = GetCurrentUserId();
            var nannyProfileId = await _db.NannyProfiles
                .Where(n => n.UserId == userId && !n.IsDeleted)
                .Select(n => (Guid?)n.Id)
                .FirstOrDefaultAsync();

            if (!nannyProfileId.HasValue)
                return BadRequest(Fail("Tai khoan khong phai nanny."));

            var contactRequest = await _db.ContactRequests
                .Include(r => r.ParentProfile)
                    .ThenInclude(p => p.User)
                .Include(r => r.NannyProfile)
                    .ThenInclude(n => n.User)
                .FirstOrDefaultAsync(r => r.Id == contactRequestId && !r.IsDeleted);

            if (contactRequest == null || contactRequest.NannyProfileId != nannyProfileId.Value)
                return NotFound(Fail("Khong tim thay request contact hoac ban khong co quyen xu ly."));

            if (contactRequest.Status is 1 or 2)
                return BadRequest(Fail("Request contact nay da duoc xu ly truoc do."));

            if (contactRequest.Status != 0)
                return BadRequest(Fail("Chi request contact dang cho duyet moi co the xu ly."));

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
                    ? "Request contact da duoc chap nhan"
                    : "Request contact bi tu choi";

                var content = isApproved
                    ? $"{nannyName} da chap nhan request contact cua ban."
                    : $"{nannyName} da tu choi request contact cua ban. Ly do: {responseMessage}";

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
                    ? "Ban da chap nhan request contact."
                    : "Ban da tu choi request contact."
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
        0 => "Dang cho duyet",
        1 => "Da duoc chap nhan",
        2 => "Da bi tu choi",
        _ => "Dang cap nhat"
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
