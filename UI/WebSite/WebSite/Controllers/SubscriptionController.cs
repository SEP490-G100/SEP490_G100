using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebSite.Models;
using WebSite.Models.Subscription;

namespace WebSite.Controllers;

[Authorize]
public class SubscriptionController : Controller
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _http;

    public SubscriptionController(IHttpClientFactory httpFactory)
    {
        _http = httpFactory.CreateClient("BackendApi");
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var token = getToken();
        if (string.IsNullOrWhiteSpace(token))
            return RedirectToAction("Login", "Auth", new { returnUrl = Url.Action("Index", "Subscription") });

        var vm = await buildPageModel(token);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Purchase(Guid subscriptionPlanId)
    {
        var token = getToken();
        if (string.IsNullOrWhiteSpace(token))
            return RedirectToAction("Login", "Auth", new { returnUrl = Url.Action("Index", "Subscription") });

        setAuthHeader(token);

        var response = await _http.PostAsJsonAsync("/api/subscriptions/create-payment", new
        {
            subscriptionPlanId
        });

        var result = await readApiResult<SubscriptionPaymentSessionViewModel>(response);
        if (result == null || !result.Success)
        {
            var message = result?.Message ?? "Khong the mua goi luc nay.";
            if (isAjaxRequest())
                return Json(new { success = false, message });

            TempData["SubscriptionError"] = message;
            return RedirectToAction(nameof(Index));
        }

        if (string.IsNullOrWhiteSpace(result.Data?.CheckoutUrl))
        {
            const string message = "Khong tao duoc lien ket thanh toan.";
            if (isAjaxRequest())
                return Json(new { success = false, message });

            TempData["SubscriptionError"] = message;
            return RedirectToAction(nameof(Index));
        }

        if (isAjaxRequest())
            return Json(new
            {
                success = true,
                message = "Da tao giao dich cho thanh toan.",
                data = result.Data
            });

        return Redirect(result.Data.CheckoutUrl);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelCurrent()
    {
        var token = getToken();
        if (string.IsNullOrWhiteSpace(token))
            return RedirectToAction("Login", "Auth", new { returnUrl = Url.Action("Index", "Subscription") });

        setAuthHeader(token);

        var response = await _http.PostAsync("/api/subscriptions/cancel-current", content: null);
        var result = await readApiResult<UserSubscriptionViewModel>(response);
        if (result == null || !result.Success)
        {
            TempData["SubscriptionError"] = result?.Message ?? "Khong the huy goi hien tai.";
            return RedirectToAction(nameof(Index));
        }

        TempData["SubscriptionSuccess"] = "Da huy goi hien tai.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> PaymentResult(Guid transactionId, bool cancelled = false)
    {
        var token = getToken();
        if (string.IsNullOrWhiteSpace(token))
            return RedirectToAction("Login", "Auth", new { returnUrl = Url.Action(nameof(PaymentResult), "Subscription", new { transactionId, cancelled }) });

        setAuthHeader(token);
        var vm = new SubscriptionPaymentResultPageViewModel
        {
            TransactionId = transactionId,
            Cancelled = cancelled
        };

        if (transactionId != Guid.Empty)
        {
            var response = await _http.GetAsync($"/api/subscriptions/payment-status/{transactionId}");
            var result = await readApiResult<SubscriptionPaymentStatusViewModel>(response);
            if (result?.Success == true)
                vm.PaymentStatus = result.Data;
        }

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> PaymentStatus(Guid transactionId)
    {
        var token = getToken();
        if (string.IsNullOrWhiteSpace(token))
            return Json(new { success = false, message = "Ban can dang nhap de xem trang thai thanh toan." });

        setAuthHeader(token);
        var response = await _http.GetAsync($"/api/subscriptions/payment-status/{transactionId}");
        var result = await readApiResult<SubscriptionPaymentStatusViewModel>(response);
        return Json(result ?? new ApiResult<SubscriptionPaymentStatusViewModel>
        {
            Success = false,
            Message = "Khong the lay trang thai thanh toan."
        });
    }

    private async Task<SubscriptionPageViewModel> buildPageModel(string token)
    {
        setAuthHeader(token);

        var role = getCurrentRole();
        var page = new SubscriptionPageViewModel
        {
            CurrentRole = role,
            RoleLabel = getRoleLabel(role),
            Headline = getHeadline(role),
            Summary = getSummary(role),
            FreeBenefits = getFreeBenefits(role)
        };

        var planResponse = await _http.GetAsync("/api/subscriptions/plans");
        var planResult = await readApiResult<List<SubscriptionPlanViewModel>>(planResponse);
        if (planResult?.Success == true && planResult.Data != null)
        {
            page.Plans = planResult.Data
                .Where(p => string.Equals(p.TargetRole, role, StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p.SortOrder)
                .ThenBy(p => p.Price)
                .ToList();
        }

        var currentResponse = await _http.GetAsync("/api/subscriptions/me");
        var currentResult = await readApiResult<UserSubscriptionViewModel>(currentResponse);
        if (currentResult?.Success == true)
            page.CurrentSubscription = currentResult.Data;

        var transactionResponse = await _http.GetAsync("/api/subscriptions/transactions");
        var transactionResult = await readApiResult<List<SubscriptionTransactionViewModel>>(transactionResponse);
        if (transactionResult?.Success == true && transactionResult.Data != null)
            page.Transactions = transactionResult.Data;

        return page;
    }

    private string? getToken() => HttpContext.Session.GetString("AccessToken");

    private void setAuthHeader(string token)
    {
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private string getCurrentRole()
    {
        if (User.IsInRole("Parent"))
            return "Parent";

        if (User.IsInRole("Nanny"))
            return "Nanny";

        return User.Claims
            .Where(c => c.Type == System.Security.Claims.ClaimTypes.Role)
            .Select(c => c.Value)
            .FirstOrDefault() ?? "Guest";
    }

    private static string getRoleLabel(string role) => role switch
    {
        "Parent" => "Phu huynh",
        "Nanny" => "Bao mau",
        _ => "Nguoi dung"
    };

    private static string getHeadline(string role) => role switch
    {
        "Parent" => "Chon goi dang tin phu hop cho gia dinh",
        "Nanny" => "Chon goi ung tuyen phu hop cho ho so cua ban",
        _ => "Subscription cua NannyMatch"
    };

    private static string getSummary(string role) => role switch
    {
        "Parent" => "Plus va Pro giup phu huynh dang them bai, giu bai lau hon va tang do noi bat khi tim bao mau.",
        "Nanny" => "Plus va Pro giup bao mau co them luot ung tuyen, ho so noi bat hon va de duoc uu tien hien thi hon.",
        _ => "Dang nhap bang tai khoan Parent hoac Nanny de xem cac goi phu hop."
    };

    private static SubscriptionBenefitViewModel getFreeBenefits(string role) => role switch
    {
        "Parent" => new SubscriptionBenefitViewModel
        {
            MonthlyJobPostLimit = 2,
            MonthlyApplicationLimit = 0,
            FeaturedBadge = false,
            SearchPriority = false,
            ListingDurationDays = 30
        },
        "Nanny" => new SubscriptionBenefitViewModel
        {
            MonthlyJobPostLimit = 0,
            MonthlyApplicationLimit = 2,
            FeaturedBadge = false,
            SearchPriority = false,
            ListingDurationDays = 0
        },
        _ => new SubscriptionBenefitViewModel()
    };

    private static async Task<ApiResult<T>?> readApiResult<T>(HttpResponseMessage response)
    {
        try
        {
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<ApiResult<T>>(json, JsonOptions);
        }
        catch
        {
            return new ApiResult<T> { Success = false, Message = $"Loi server (HTTP {(int)response.StatusCode})." };
        }
    }

    private bool isAjaxRequest() =>
        string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);
}
