using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nanny_BackEnd.DTOs.Subscription;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Controllers;

[ApiController]
[Route("api/subscriptions")]
public class SubscriptionController : ControllerBase
{
    private readonly ISubscriptionService _subscriptionService;
    private readonly ICassoService _cassoService;
    private readonly IPayOsService _payOsService;

    public SubscriptionController(
        ISubscriptionService subscriptionService,
        ICassoService cassoService,
        IPayOsService payOsService)
    {
        _subscriptionService = subscriptionService;
        _cassoService = cassoService;
        _payOsService = payOsService;
    }

    [AllowAnonymous]
    [HttpGet("plans")]
    public async Task<IActionResult> GetPlans()
    {
        var plans = await _subscriptionService.getPlans();
        return Ok(success(plans, plans.Count));
    }

    [AllowAnonymous]
    [HttpGet("plans/{code}")]
    public async Task<IActionResult> GetPlanByCode(string code)
    {
        var plan = await _subscriptionService.getPlanByCode(code);
        return plan == null
            ? NotFound(fail("Không tìm thấy gói dịch vụ yêu cầu."))
            : Ok(success(plan));
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentSubscription()
    {
        var userId = getCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized(fail("Không xác định được người dùng hiện tại."));

        var subscription = await _subscriptionService.getCurrentSubscription(userId.Value);
        return Ok(success(subscription));
    }

    [Authorize]
    [HttpGet("history")]
    public async Task<IActionResult> GetHistory()
    {
        var userId = getCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized(fail("Không xác định được người dùng hiện tại."));

        var history = await _subscriptionService.getHistory(userId.Value);
        return Ok(success(history, history.Count));
    }

    [Authorize]
    [HttpGet("transactions")]
    public async Task<IActionResult> GetTransactionHistory()
    {
        var userId = getCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized(fail("Không xác định được người dùng hiện tại."));

        var history = await _subscriptionService.getTransactionHistory(userId.Value);
        return Ok(success(history, history.Count));
    }

    [Authorize]
    [HttpPost("subscribe")]
    public async Task<IActionResult> Subscribe([FromBody] SubscribeRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(failValidation(ModelState));

        var userId = getCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized(fail("Không xác định được người dùng hiện tại."));

        try
        {
            var subscription = await _subscriptionService.subscribe(userId.Value, request);
            return Ok(new
            {
                success = true,
                message = "Đăng ký gói dịch vụ thành công.",
                data = subscription
            });
        }
        catch (KeyNotFoundException ex) { return NotFound(fail(ex.Message)); }
        catch (InvalidOperationException ex) { return BadRequest(fail(ex.Message)); }
    }

    [Authorize]
    [HttpPost("create-payment")]
    public async Task<IActionResult> CreatePayment([FromBody] CreateSubscriptionPaymentRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(failValidation(ModelState));

        var userId = getCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized(fail("Không xác định được người dùng hiện tại."));

        request.ClientIp ??= HttpContext.Connection.RemoteIpAddress?.ToString();

        try
        {
            var session = await _subscriptionService.createPayment(userId.Value, request);
            return Ok(new
            {
                success = true,
                message = "Đã tạo phiên thanh toán gói dịch vụ thành công.",
                data = session
            });
        }
        catch (KeyNotFoundException ex) { return NotFound(fail(ex.Message)); }
        catch (InvalidOperationException ex) { return BadRequest(fail(ex.Message)); }
    }

    [Authorize]
    [HttpGet("payment-status/{transactionId:guid}")]
    public async Task<IActionResult> GetPaymentStatus(Guid transactionId)
    {
        var userId = getCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized(fail("Không xác định được người dùng hiện tại."));

        try
        {
            var status = await _subscriptionService.getPaymentStatus(userId.Value, transactionId);
            return Ok(success(status));
        }
        catch (KeyNotFoundException ex) { return NotFound(fail(ex.Message)); }
    }

    [Authorize]
    [HttpPost("mark-transferred/{transactionId:guid}")]
    public async Task<IActionResult> MarkTransferred(Guid transactionId)
    {
        var userId = getCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized(fail("Không xác định được người dùng hiện tại."));

        try
        {
            var result = await _subscriptionService.markTransferred(userId.Value, transactionId);
            return Ok(new { success = true, message = result.Message, data = result });
        }
        catch (KeyNotFoundException ex) { return NotFound(fail(ex.Message)); }
        catch (InvalidOperationException ex) { return BadRequest(fail(ex.Message)); }
    }

    [AllowAnonymous]
    [HttpPost("payos/webhook")]
    public async Task<IActionResult> PayOsWebhook([FromBody] PayOsWebhookRequest request)
    {
        if (!_payOsService.isWebhookValid(request))
            return Unauthorized(fail("Chữ ký PayOS không hợp lệ."));

        var processed = await _subscriptionService.handlePayOsWebhook(request);
        return Ok(new { success = true, processed });
    }

    [AllowAnonymous]
    [HttpPost("casso/webhook")]
    public async Task<IActionResult> CassoWebhook([FromBody] CassoWebhookRequest request)
    {
        var secureToken = Request.Headers["secure-token"].FirstOrDefault();
        if (!_cassoService.isWebhookAuthorized(secureToken))
            return Unauthorized(fail("Secure token của Casso không hợp lệ."));

        var processed = await _subscriptionService.handleCassoWebhook(request);
        return Ok(new { success = true, processed });
    }

    [Authorize]
    [HttpPost("cancel-current")]
    public async Task<IActionResult> CancelCurrent()
    {
        var userId = getCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized(fail("Không xác định được người dùng hiện tại."));

        try
        {
            var subscription = await _subscriptionService.cancelCurrentSubscription(userId.Value);
            return Ok(new
            {
                success = true,
                message = "Hủy gói dịch vụ hiện tại thành công.",
                data = subscription
            });
        }
        catch (KeyNotFoundException ex) { return NotFound(fail(ex.Message)); }
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("/api/Admin/admin-view-subscription-plan-list")]
    public async Task<IActionResult> AdminViewSubscriptionPlanList(
        [FromQuery] string? search = null,
        [FromQuery] string? targetRole = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 3)
    {
        var result = await _subscriptionService.getAdminPlans(
            search,
            targetRole,
            isActive,
            page,
            pageSize);

        return Ok(new { success = true, data = result });
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("/api/Admin/admin-view-subscription-plan-detail/{id:guid}")]
    public async Task<IActionResult> AdminViewSubscriptionPlanDetail(Guid id)
    {
        var plan = await _subscriptionService.getAdminPlanDetail(id);
        return plan == null
            ? NotFound(new { success = false, message = "Không tìm thấy gói dịch vụ." })
            : Ok(new { success = true, data = plan });
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("/api/Admin/admin-create-subscription-plan")]
    public async Task<IActionResult> AdminCreateSubscriptionPlan([FromBody] AdminSubscriptionPlanUpsertRequest request)
    {
        var adminUserId = getCurrentUserId();
        if (!adminUserId.HasValue)
            return Unauthorized(new { success = false, message = "Khong xac dinh duoc admin hien tai." });

        try
        {
            var plan = await _subscriptionService.createAdminPlan(adminUserId.Value, request);
            return Ok(new { success = true, message = "Tạo gói dịch vụ thành công.", data = plan });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [Authorize(Roles = "Admin")]
    [HttpPatch("/api/Admin/admin-update-subscription-plan/{id:guid}")]
    public async Task<IActionResult> AdminUpdateSubscriptionPlan(Guid id, [FromBody] AdminSubscriptionPlanUpsertRequest request)
    {
        var adminUserId = getCurrentUserId();
        if (!adminUserId.HasValue)
            return Unauthorized(new { success = false, message = "Khong xac dinh duoc admin hien tai." });

        try
        {
            var plan = await _subscriptionService.updateAdminPlan(id, adminUserId.Value, request);
            return Ok(new { success = true, message = "Cập nhật gói dịch vụ thành công.", data = plan });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { success = false, message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [Authorize(Roles = "Admin")]
    [HttpPatch("/api/Admin/admin-update-subscription-plan-status/{id:guid}")]
    public async Task<IActionResult> AdminUpdateSubscriptionPlanStatus(
        Guid id,
        [FromBody] AdminSubscriptionPlanStatusRequest? request,
        [FromQuery] bool? isActive)
    {
        var adminUserId = getCurrentUserId();
        if (!adminUserId.HasValue)
            return Unauthorized(new { success = false, message = "Khong xac dinh duoc admin hien tai." });

        var targetIsActive = isActive ?? request?.IsActive;
        if (!targetIsActive.HasValue)
            return BadRequest(new { success = false, message = "Thiếu trạng thái kích hoạt của gói dịch vụ." });

        try
        {
            await _subscriptionService.toggleAdminPlanStatus(
                id,
                adminUserId.Value,
                targetIsActive.Value);

            return Ok(new
            {
                success = true,
                message = targetIsActive.Value
                    ? "Đã kích hoạt gói dịch vụ."
                    : "Đã vô hiệu hóa gói dịch vụ."
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { success = false, message = ex.Message });
        }
    }

    private Guid? getCurrentUserId()
    {
        var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
               ?? User.FindFirst("sub")?.Value;

        return Guid.TryParse(sub, out var userId) ? userId : null;
    }

    private static object success(object? data, int? total = null) =>
        total.HasValue
            ? new { success = true, total, data }
            : new { success = true, data };

    private static object fail(string message) => new { success = false, message };

    private static object failValidation(Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary modelState) =>
        new
        {
            success = false,
            message = "Dữ liệu không hợp lệ. Vui lòng kiểm tra lại.",
            errors = modelState
                .Where(e => e.Value?.Errors.Count > 0)
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage).ToArray()
                )
        };
}
