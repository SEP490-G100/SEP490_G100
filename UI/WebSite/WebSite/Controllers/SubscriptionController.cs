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
            var message = result?.Message ?? "Không thể mua gói lúc này.";
            if (isAjaxRequest())
                return Json(new { success = false, message });

            TempData["SubscriptionError"] = message;
            return RedirectToAction(nameof(Index));
        }

        if (string.IsNullOrWhiteSpace(result.Data?.QrCodeUrl) &&
            string.IsNullOrWhiteSpace(result.Data?.CheckoutUrl))
        {
            const string message = "Không tạo được phiên thanh toán PayOS.";
            if (isAjaxRequest())
                return Json(new { success = false, message });

            TempData["SubscriptionError"] = message;
            return RedirectToAction(nameof(Index));
        }

        if (isAjaxRequest())
            return Json(new
            {
                success = true,
                message = "Đã tạo giao dịch thanh toán PayOS.",
                data = result.Data
            });

        return RedirectToAction(nameof(PaymentResult), new { transactionId = result.Data.TransactionId });
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
            TempData["SubscriptionError"] = result?.Message ?? "Không thể hủy gói hiện tại.";
            return RedirectToAction(nameof(Index));
        }

        TempData["SubscriptionSuccess"] = "Đã hủy gói hiện tại.";
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
            return Json(new { success = false, message = "Bạn cần đăng nhập để xem trạng thái thanh toán." });

        setAuthHeader(token);
        var response = await _http.GetAsync($"/api/subscriptions/payment-status/{transactionId}");
        var result = await readApiResult<SubscriptionPaymentStatusViewModel>(response);
        return Json(result ?? new ApiResult<SubscriptionPaymentStatusViewModel>
        {
            Success = false,
            Message = "Không thể lấy trạng thái thanh toán."
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkTransferred(Guid transactionId)
    {
        var token = getToken();
        if (string.IsNullOrWhiteSpace(token))
            return Json(new { success = false, message = "Bạn cần đăng nhập để xác nhận thanh toán." });

        setAuthHeader(token);
        var response = await _http.PostAsync($"/api/subscriptions/mark-transferred/{transactionId}", content: null);
        var result = await readApiResult<MarkSubscriptionTransferredViewModel>(response);
        return Json(result ?? new ApiResult<MarkSubscriptionTransferredViewModel>
        {
            Success = false,
            Message = "Không thể ghi nhận xác nhận chuyển khoản."
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
            FreeBenefits = getFreeBenefits(role),
            FreeFeatures = getFreeFeatures(role)
        };

        var planResponse = await _http.GetAsync("/api/subscriptions/plans");
        var planResult = await readApiResult<List<SubscriptionPlanViewModel>>(planResponse);
        if (planResult?.Success == true && planResult.Data != null)
        {
            page.Plans = planResult.Data
                .Where(p => roleMatches(p, role))
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
        "Parent" => "Phụ huynh",
        "Nanny" => "Bảo mẫu",
        _ => "Người dùng"
    };

    private static string getHeadline(string role) => role switch
    {
        "Parent" => "Chọn gói đăng tin phù hợp cho gia đình",
        "Nanny" => "Chọn gói ứng tuyển phù hợp cho hồ sơ của bạn",
        _ => "Subscription của NannyMatch"
    };

    private static string getSummary(string role) => role switch
    {
        "Parent" => "Các gói subscription được lấy trực tiếp từ hệ thống quản trị, giúp phụ huynh mở rộng quyền đăng tin và tăng khả năng tiếp cận bảo mẫu.",
        "Nanny" => "Các gói subscription được lấy trực tiếp từ hệ thống quản trị, giúp bảo mẫu tăng quyền ứng tuyển và cải thiện độ nổi bật của hồ sơ.",
        _ => "Đăng nhập bằng tài khoản Parent hoặc Nanny để xem các gói phù hợp."
    };

    private static SubscriptionBenefitViewModel getFreeBenefits(string role) => role switch
    {
        "Parent" => new SubscriptionBenefitViewModel
        {
            MonthlyJobPostLimit = 3,
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

    private static List<string> getFreeFeatures(string role) => role switch
    {
        "Parent" =>
        [
            "Tối đa 3 bài đăng đang hoạt động theo thiết lập Free hiện tại",
            "Thời gian hiển thị bài đăng cơ bản 30 ngày",
            "Không có badge nổi bật",
            "Không ưu tiên trong kết quả tìm kiếm"
        ],
        "Nanny" =>
        [
            "Tối đa 2 lượt ứng tuyển mỗi tháng theo thiết lập Free hiện tại",
            "Hồ sơ hiển thị cơ bản",
            "Không có badge nổi bật",
            "Không ưu tiên trong kết quả tìm kiếm"
        ],
        _ => []
    };

    private static bool roleMatches(SubscriptionPlanViewModel plan, string currentRole)
    {
        if (string.IsNullOrWhiteSpace(plan.TargetRole) ||
            string.Equals(plan.TargetRole, "Unknown", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(plan.TargetRole, currentRole, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<ApiResult<T>?> readApiResult<T>(HttpResponseMessage response)
    {
        try
        {
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<ApiResult<T>>(json, JsonOptions);
        }
        catch
        {
            return new ApiResult<T> { Success = false, Message = $"Lỗi server (HTTP {(int)response.StatusCode})." };
        }
    }

    private bool isAjaxRequest() =>
        string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);
}
