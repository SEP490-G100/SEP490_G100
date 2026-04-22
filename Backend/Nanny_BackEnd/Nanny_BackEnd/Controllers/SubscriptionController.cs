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
    [HttpGet("transactions")]
    public async Task<IActionResult> GetTransactionHistory()
    {
        var userId = getCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized(fail("Khong xac dinh duoc nguoi dung hien tai."));

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

        request.ClientIp ??= HttpContext.Connection.RemoteIpAddress?.ToString();

        try
        {
            var session = await _subscriptionService.createPayment(userId.Value, request);
            return Ok(new
            {
                success = true,
                message = "Da tao phien thanh toan subscription thanh cong.",
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

    [Authorize]
    [HttpPost("mark-transferred/{transactionId:guid}")]
    public async Task<IActionResult> MarkTransferred(Guid transactionId)
    {
        var userId = getCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized(fail("Khong xac dinh duoc nguoi dung hien tai."));

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
            return Unauthorized(fail("PayOS signature khong hop le."));

        var processed = await _subscriptionService.handlePayOsWebhook(request);
        return Ok(new { success = true, processed });
    }

    [AllowAnonymous]
    [HttpPost("casso/webhook")]
    public async Task<IActionResult> CassoWebhook([FromBody] CassoWebhookRequest request)
    {
        var secureToken = Request.Headers["secure-token"].FirstOrDefault();
        if (!_cassoService.isWebhookAuthorized(secureToken))
            return Unauthorized(fail("Casso secure token khong hop le."));

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
