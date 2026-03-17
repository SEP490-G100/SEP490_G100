using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nanny_BackEnd.DTOs.Subscription;
using Nanny_BackEnd.Services;

namespace Nanny_BackEnd.Controllers;

[ApiController]
[Route("api/subscriptions")]
public class SubscriptionController : ControllerBase
{
    private readonly SubscriptionService _subscriptionService;

    public SubscriptionController(SubscriptionService subscriptionService)
    {
        _subscriptionService = subscriptionService;
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
            ? NotFound(fail("Không tìm thấy gói subscription yêu cầu."))
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
                message = "Đăng ký gói subscription thành công.",
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
            return Unauthorized(fail("Khong xac dinh duoc nguoi dung hien tai."));

        try
        {
            var session = await _subscriptionService.createPayment(userId.Value, request);
            return Ok(new
            {
                success = true,
                message = "Da tao lien ket thanh toan thanh cong.",
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
            return Unauthorized(fail("Khong xac dinh duoc nguoi dung hien tai."));

        try
        {
            var status = await _subscriptionService.getPaymentStatus(userId.Value, transactionId);
            return Ok(success(status));
        }
        catch (KeyNotFoundException ex) { return NotFound(fail(ex.Message)); }
    }

    [AllowAnonymous]
    [HttpPost("vietqr/callback")]
    public async Task<IActionResult> VietQrCallback(
        [FromBody] VietQrWebhookRequest request,
        [FromHeader(Name = "x-webhook-token")] string? webhookToken,
        [FromHeader(Name = "secure-token")] string? secureToken)
    {
        if (!_subscriptionService.isVietQrWebhookAuthorized(webhookToken ?? secureToken))
            return Unauthorized(fail("Webhook token khong hop le."));

        var processed = await _subscriptionService.handleVietQrWebhook(request);
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
                message = "Hủy gói subscription hiện tại thành công.",
                data = subscription
            });
        }
        catch (KeyNotFoundException ex) { return NotFound(fail(ex.Message)); }
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
