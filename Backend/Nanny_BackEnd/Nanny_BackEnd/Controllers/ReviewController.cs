using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nanny_BackEnd.DTOs.Review;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Controllers;

[ApiController]
[Route("api/reviews")]
public class ReviewController : ControllerBase
{
    private readonly IReviewService _reviewSvc;
    private readonly IProfileService _profileService;

    public ReviewController(IReviewService reviewSvc, IProfileService profileService)
    {
        _reviewSvc = reviewSvc;
        _profileService = profileService;
    }

    /// <summary>GET /api/reviews/nanny/{nannyUserId} — public, lấy đánh giá của nanny</summary>
    [AllowAnonymous]
    [HttpGet("nanny/{nannyUserId:guid}")]
    public async Task<IActionResult> GetNannyReviews(
        Guid nannyUserId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var result = await _reviewSvc.GetNannyReviewsAsync(nannyUserId, page, pageSize);
        return Ok(Success(result));
    }

    /// <summary>GET /api/reviews/reviewable — lấy các hợp đồng parent chưa đánh giá</summary>
    [Authorize]
    [HttpGet("reviewable")]
    public async Task<IActionResult> GetReviewable()
    {
        var userId = getCurrentUserId();
        if (!userId.HasValue) return Unauthorized(Fail("Chưa đăng nhập."));

        var parent = await getParent(userId.Value);
        if (parent is null) return BadRequest(Fail("Tài khoản hiện tại không phải Phụ Huynh."));

        var result = await _reviewSvc.GetReviewableHiringRecordsAsync(userId.Value);
        return Ok(Success(result, result.Count));
    }

    /// <summary>GET /api/reviews/mine — lấy tất cả đánh giá parent đã gửi</summary>
    [Authorize]
    [HttpGet("mine")]
    public async Task<IActionResult> GetMine()
    {
        var userId = getCurrentUserId();
        if (!userId.HasValue) return Unauthorized(Fail("Chưa đăng nhập."));

        var result = await _reviewSvc.GetMyReviewsAsync(userId.Value);
        return Ok(Success(result, result.Count));
    }

    /// <summary>POST /api/reviews — parent tạo đánh giá</summary>
    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateReviewRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new
            {
                success = false,
                message = "Dữ liệu không hợp lệ.",
                errors = ModelState
                    .Where(x => x.Value?.Errors.Count > 0)
                    .ToDictionary(x => x.Key, x => x.Value!.Errors.Select(e => e.ErrorMessage))
            });

        var userId = getCurrentUserId();
        if (!userId.HasValue) return Unauthorized(Fail("Chưa đăng nhập."));

        var parent = await getParent(userId.Value);
        if (parent is null) return BadRequest(Fail("Tài khoản hiện tại không phải Phụ Huynh."));

        try
        {
            var result = await _reviewSvc.CreateReviewAsync(userId.Value, request);
            return Ok(Success(result));
        }
        catch (KeyNotFoundException ex) { return NotFound(Fail(ex.Message)); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { return BadRequest(Fail(ex.Message)); }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private Guid? getCurrentUserId()
    {
        var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
               ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(sub, out var id) ? id : null;
    }

    private async Task<Models.ParentProfile?> getParent(Guid userId)
        => await _profileService.GetParentProfileByUserIdAsync(userId);

    private static object Success(object data, int? total = null) =>
        total.HasValue
            ? new { success = true, data, total }
            : new { success = true, data };

    private static object Fail(string message) => new { success = false, message };
}
