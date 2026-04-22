using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nanny_BackEnd.DTOs.Nanny;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NanniesController : ControllerBase
{
    private readonly INannyService _nannyService;
    private readonly IProfileService _profileService;
    private readonly IContactRequestService _contactRequestService;

    public NanniesController(
        INannyService nannyService,
        IProfileService profileService,
        IContactRequestService contactRequestService)
    {
        _nannyService = nannyService;
        _profileService = profileService;
        _contactRequestService = contactRequestService;
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
            var parentProfile = await _profileService.GetParentProfileByUserIdAsync(userId);
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
            var parentProfile = await _profileService.GetParentProfileByUserIdAsync(userId);
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
            var r = await _contactRequestService.SendAsync(userId, nannyProfileId, request?.Message);
            return MapContactResult(r);
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

            var userId = GetCurrentUserId();
            var r = await _contactRequestService.GetReceivedAsync(userId, status);
            return MapContactResult(r);
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

            var userId = GetCurrentUserId();
            var r = await _contactRequestService.GetSentAsync(userId, status);
            return MapContactResult(r);
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
            var userId = GetCurrentUserId();
            var r = await _contactRequestService.GetDetailAsync(userId, contactRequestId, isParent, isNanny);
            return MapContactResult(r);
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

            var userId = GetCurrentUserId();
            var r = await _contactRequestService.ReviewAsync(userId, contactRequestId, request.Action, request.ResponseMessage);
            return MapContactResult(r);
        }
        catch (Exception ex)
        {
            return StatusCode(500, Fail(ex.Message));
        }
    }

    private static IActionResult MapContactResult(ContactRequestEndpointResult r) => r.StatusCode switch
    {
        200 => new OkObjectResult(r.Body),
        400 => new BadRequestObjectResult(r.Body),
        403 => new ObjectResult(r.Body) { StatusCode = 403 },
        404 => new NotFoundObjectResult(r.Body),
        409 => new ConflictObjectResult(r.Body),
        _ => new ObjectResult(r.Body) { StatusCode = r.StatusCode }
    };

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

        return await _profileService.GetParentProfileIdByUserIdAsync(userId);
    }

    private static object Fail(string message) => new { success = false, message };

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
