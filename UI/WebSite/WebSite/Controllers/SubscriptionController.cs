using System.Net.Http.Headers;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebSite.Models;
using WebSite.Models.Admin;
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
            return RedirectToAction("đăng nhập", "Auth", new { returnUrl = Url.Action("Index", "Subscription") });

        var vm = await buildPageModel(token);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Purchase(Guid subscriptionPlanId)
    {
        var token = getToken();
        if (string.IsNullOrWhiteSpace(token))
            return RedirectToAction("đăng nhập", "Auth", new { returnUrl = Url.Action("Index", "Subscription") });

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
            return RedirectToAction("đăng nhập", "Auth", new { returnUrl = Url.Action("Index", "Subscription") });

        setAuthHeader(token);

        var response = await _http.PostAsync("/api/subscriptions/cancel-current", content: null);
        var result = await readApiResult<UserSubscriptionViewModel>(response);
        if (result == null || !result.Success)
        {
            TempData["SubscriptionError"] = result?.Message ?? "Không thể hủy gói hiện tại.";
            return RedirectToAction(nameof(Index));
        }

        TempData["SubscriptionSuccess"] = "Đã dừng gói. Bạn có thể mua gói khác khi cần.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> PaymentResult(Guid transactionId, bool cancelled = false)
    {
        var token = getToken();
        if (string.IsNullOrWhiteSpace(token))
            return RedirectToAction("đăng nhập", "Auth", new { returnUrl = Url.Action(nameof(PaymentResult), "Subscription", new { transactionId, cancelled }) });

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

    [Authorize(Roles = "Admin")]
    [HttpGet("/Admin/ManageSubscriptionPlan")]
    public async Task<IActionResult> ManageSubscriptionPlan(
        string? search = null,
        string? targetRole = null,
        bool? isActive = null,
        int page = 1)
    {
        ViewBag.Search = search ?? "";
        ViewBag.TargetRole = targetRole ?? "";
        ViewBag.IsActive = isActive;

        var qs = new List<string> { $"page={page}", "pageSize=3" };
        if (!string.IsNullOrWhiteSpace(search)) qs.Add($"search={Uri.EscapeDataString(search)}");
        if (!string.IsNullOrWhiteSpace(targetRole)) qs.Add($"targetRole={Uri.EscapeDataString(targetRole)}");
        if (isActive.HasValue) qs.Add($"isActive={isActive.Value.ToString().ToLowerInvariant()}");

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/Admin/admin-view-subscription-plan-list?{string.Join("&", qs)}");
        AttachToken(request);
        try
        {
            var response = await _http.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResult<AdminSubscriptionPlanListResponse>>(json, JsonOptions);
            return View("~/Views/Admin/SubscriptionPlan/ManageSubscriptionPlan.cshtml", result?.Data ?? new AdminSubscriptionPlanListResponse());
        }
        catch
        {
            TempData["Error"] = "Không thể tải danh sách gói dịch vụ.";
            return View("~/Views/Admin/SubscriptionPlan/ManageSubscriptionPlan.cshtml", new AdminSubscriptionPlanListResponse());
        }
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("/Admin/CreateSubscriptionPlan")]
    public IActionResult CreateSubscriptionPlan() =>
        View("~/Views/Admin/SubscriptionPlan/CreateSubscriptionPlan.cshtml", new AdminSubscriptionPlanFormViewModel());

    [Authorize(Roles = "Admin")]
    [HttpPost("/Admin/CreateSubscriptionPlan")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateSubscriptionPlan(AdminSubscriptionPlanFormViewModel model)
    {
        ValidateSubscriptionPlanForm(model);
        if (!ModelState.IsValid)
            return View("~/Views/Admin/SubscriptionPlan/CreateSubscriptionPlan.cshtml", model);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/Admin/admin-create-subscription-plan")
        {
            Content = new StringContent(JsonSerializer.Serialize(BuildSubscriptionPlanPayload(model)), Encoding.UTF8, "application/json")
        };
        AttachToken(request);

        try
        {
            var response = await _http.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResult<AdminSubscriptionPlanDetailViewModel>>(json, JsonOptions);

            if (result?.Success == true && result.Data != null)
            {
                return RedirectToAction(nameof(ManageSubscriptionPlan), new
                {
                    toastType = "success",
                    toastMessage = "Bạn đã tạo gói subscription thành công"
                });
            }

            ModelState.AddModelError(nameof(model.Name), result?.Message ?? "Không thể tạo gói đăng ký.");
            return View("~/Views/Admin/SubscriptionPlan/CreateSubscriptionPlan.cshtml", model);
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(nameof(model.Name), $"Lỗi kết nối: {ex.Message}");
            return View("~/Views/Admin/SubscriptionPlan/CreateSubscriptionPlan.cshtml", model);
        }
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("/Admin/ViewSubscriptionPlanDetail/{id:guid}")]
    public async Task<IActionResult> ViewSubscriptionPlanDetail(Guid id)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/Admin/admin-view-subscription-plan-detail/{id}");
        AttachToken(request);
        try
        {
            var response = await _http.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResult<AdminSubscriptionPlanDetailViewModel>>(json, JsonOptions);
            if (result?.Success != true || result.Data == null)
            {
            TempData["Error"] = result?.Message ?? "Không tìm thấy gói đăng ký.";
                return RedirectToAction(nameof(ManageSubscriptionPlan));
            }

            return View("~/Views/Admin/SubscriptionPlan/ViewSubscriptionPlanDetail.cshtml", AdminSubscriptionPlanFormViewModel.FromDetail(result.Data));
        }
        catch
        {
            TempData["Error"] = "Lỗi kết nối đến API.";
            return RedirectToAction(nameof(ManageSubscriptionPlan));
        }
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("/Admin/UpdateSubscriptionPlan/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateSubscriptionPlan(Guid id, AdminSubscriptionPlanFormViewModel model)
    {
        ValidateSubscriptionPlanForm(model);
        if (!ModelState.IsValid)
        {
            model.Id = id;
            return View("~/Views/Admin/SubscriptionPlan/ViewSubscriptionPlanDetail.cshtml", model);
        }

        var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/Admin/admin-update-subscription-plan/{id}")
        {
            Content = new StringContent(JsonSerializer.Serialize(BuildSubscriptionPlanPayload(model)), Encoding.UTF8, "application/json")
        };
        AttachToken(request);

        try
        {
            var response = await _http.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResult<AdminSubscriptionPlanDetailViewModel>>(json, JsonOptions);

            if (result?.Success == true)
            {
                return RedirectToAction(nameof(ManageSubscriptionPlan), new
                {
                    toastType = "success",
                    toastMessage = "Bạn đã chỉnh sửa gói subscription thành công"
                });
            }

            ModelState.AddModelError(nameof(model.Name), result?.Message ?? "Không thể cập nhật gói đăng ký.");
            model.Id = id;
            return View("~/Views/Admin/SubscriptionPlan/ViewSubscriptionPlanDetail.cshtml", model);
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(nameof(model.Name), $"Lỗi kết nối: {ex.Message}");
            model.Id = id;
            return View("~/Views/Admin/SubscriptionPlan/ViewSubscriptionPlanDetail.cshtml", model);
        }
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("/Admin/ToggleSubscriptionPlanStatus")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleSubscriptionPlanStatus(
        [FromForm] Guid id,
        [FromForm] bool isActive,
        [FromForm] string? returnUrl = null)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Patch,
            $"/api/Admin/admin-update-subscription-plan-status/{id}?isActive={isActive.ToString().ToLowerInvariant()}")
        {
            Content = new StringContent(JsonSerializer.Serialize(new { isActive }), Encoding.UTF8, "application/json")
        };
        AttachToken(request);
        try
        {
            var response = await _http.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResult>(json, JsonOptions);

            if (result?.Success == true)
            {
                var toastMessage = isActive
                ? "Đã kích hoạt gói thành công"
                : "Đã vô hiệu hóa gói thành công";
                var toastType = isActive ? "success" : "warning";
                return RedirectToReturnUrlOrList(
                    returnUrl,
                    toastType,
                    toastMessage);
            }

            return RedirectToReturnUrlOrList(
                returnUrl,
                "error",
                result?.Message ?? "Không thể cập nhật trạng thái gói đăng ký.");
        }
        catch (Exception ex)
        {
            return RedirectToReturnUrlOrList(returnUrl, "error", $"Lỗi kết nối: {ex.Message}");
        }
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

    private void AttachToken(HttpRequestMessage request)
    {
        var token = HttpContext.Session.GetString("AccessToken");
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private void ValidateSubscriptionPlanForm(AdminSubscriptionPlanFormViewModel model)
    {
        ValidatePriceField(model);
        ValidateIntegerField(
            fieldName: nameof(model.DurationDays),
            requiredMessage: "Thời hạn là bắt buộc",
            invalidMessage: "Nhập chữ số nguyên dương lớn hơn hoặc bằng 1.",
            minValue: 1);
        ValidateIntegerField(
            fieldName: nameof(model.MonthlyJobPostLimit),
            requiredMessage: "Giới Hạn Đăng Tin Hàng Tháng là bắt buộc",
            invalidMessage: "Nhập chữ số nguyên dương lớn hơn hoặc bằng 0.",
            minValue: 0);
        ValidateIntegerField(
            fieldName: nameof(model.MonthlyApplicationLimit),
            requiredMessage: "Giới Hạn Ứng Tuyển Hàng Tháng  là bắt buộc",
            invalidMessage: "Nhập chữ số nguyên dương lớn hơn hoặc bằng 0.",
            minValue: 0);
        ValidateIntegerField(
            fieldName: nameof(model.ListingDurationDays),
            requiredMessage: "Thời Hạn Hiển Thị là bắt buộc",
            invalidMessage: "Nhập chữ số nguyên dương lớn hơn hoặc bằng 0.",
            minValue: 0);

        if (string.IsNullOrWhiteSpace(model.Description))
            ModelState.AddModelError(nameof(model.Description), "Vui lòng nhập mô tả.");

        if (model.GetFeatures().Count == 0)
            ModelState.AddModelError(nameof(model.FeatureLines), "Vui lòng nhập ít nhất một tính năng.");
    }

    private void ValidatePriceField(AdminSubscriptionPlanFormViewModel model)
    {
        var fieldName = nameof(model.Price);
        ModelState.Remove(fieldName);

        var rawValue = GetRawFormValue(fieldName);
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            ModelState.AddModelError(fieldName, "Giá gói là bắt buộc");
            return;
        }

        if (!TryParseDecimal(rawValue, out var parsedValue) || parsedValue < 0 || parsedValue % 1000 != 0)
        {
            ModelState.AddModelError(fieldName, "Nhập giá gói là bội số của 1000");
            return;
        }

        model.Price = parsedValue;
    }

    private void ValidateIntegerField(string fieldName, string requiredMessage, string invalidMessage, int minValue)
    {
        ModelState.Remove(fieldName);

        var rawValue = GetRawFormValue(fieldName);
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            ModelState.AddModelError(fieldName, requiredMessage);
            return;
        }

        // Reject signed values like "-0" for non-negative integer fields.
        if (!isValidUnsignedIntegerFormat(rawValue))
        {
            ModelState.AddModelError(fieldName, invalidMessage);
            return;
        }

        if (!int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedValue) || parsedValue < minValue)
        {
            ModelState.AddModelError(fieldName, invalidMessage);
        }
    }

    private string GetRawFormValue(string fieldName)
    {
        if (!Request.HasFormContentType)
            return string.Empty;

        return Request.Form.TryGetValue(fieldName, out var values)
            ? values.ToString().Trim()
            : string.Empty;
    }

    private static bool TryParseDecimal(string input, out decimal value)
    {
        return decimal.TryParse(input, NumberStyles.Number, CultureInfo.InvariantCulture, out value)
               || decimal.TryParse(input, NumberStyles.Number, CultureInfo.GetCultureInfo("vi-VN"), out value)
               || decimal.TryParse(input, NumberStyles.Number, CultureInfo.CurrentCulture, out value);
    }

    private static bool isValidUnsignedIntegerFormat(string input) =>
        Regex.IsMatch(input.Trim(), @"^\d+$");

    private static object BuildSubscriptionPlanPayload(AdminSubscriptionPlanFormViewModel model) => new
    {
        name = model.Name.Trim(),
        description = model.Description.Trim(),
        targetRole = model.TargetRole,
        price = model.Price ?? 0,
        durationDays = model.DurationDays ?? 0,
        sortOrder = model.SortOrder,
        features = model.GetFeatures(),
        canUseRecommendation = model.CanUseRecommendation,
        benefits = new
        {
            monthlyJobPostLimit = model.MonthlyJobPostLimit ?? 0,
            monthlyApplicationLimit = model.MonthlyApplicationLimit ?? 0,
            featuredBadge = model.FeaturedBadge,
            searchPriority = model.SearchPriority,
            listingDurationDays = model.ListingDurationDays ?? 0
        }
    };

    private IActionResult RedirectToReturnUrlOrList(
        string? returnUrl,
        string? toastType = null,
        string? toastMessage = null)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(AppendToastQuery(returnUrl, toastType, toastMessage));

        if (!string.IsNullOrWhiteSpace(toastMessage))
        {
            return RedirectToAction(
                nameof(ManageSubscriptionPlan),
                new { toastType = toastType ?? "info", toastMessage });
        }

        return RedirectToAction(nameof(ManageSubscriptionPlan));
    }

    private static string AppendToastQuery(string url, string? toastType, string? toastMessage)
    {
        var updatedUrl = url;

        if (!string.IsNullOrWhiteSpace(toastType))
            updatedUrl = AppendQuery(updatedUrl, "toastType", toastType);

        if (!string.IsNullOrWhiteSpace(toastMessage))
            updatedUrl = AppendQuery(updatedUrl, "toastMessage", toastMessage);

        return updatedUrl;
    }

    private static string AppendQuery(string url, string key, string value)
    {
        var separator = url.Contains('?') ? "&" : "?";
        return $"{url}{separator}{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}";
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
        "Parent" => "Các gói dành cho phụ huynh",
        "Nanny" => "Các gói dành cho bảo mẫu",
        _ => "Gói thành viên"
    };

    private static string getSummary(string role) => role switch
    {
        "Parent" => "Nâng cấp khi cần nhiều bài đăng hơn, thời gian hiển thị dài hơn và ưu tiên xuất hiện trước bảo mẫu. Giá và quyền lợi cập nhật theo từng thời điểm trên hệ thống.",
        "Nanny" => "Nâng cấp để ứng tuyển nhiều hơn mỗi tháng và tăng độ nổi bật hồ sơ. Giá và quyền lợi cập nhật theo từng thời điểm trên hệ thống.",
        _ => "Vui lòng đăng nhập bằng tài khoản phụ huynh hoặc bảo mẫu để xem gói phù hợp."
    };

    private static SubscriptionBenefitViewModel getFreeBenefits(string role) => role switch
    {
        "Parent" => new SubscriptionBenefitViewModel
        {
            MonthlyJobPostLimit = 1,
            MonthlyApplicationLimit = 0,
            FeaturedBadge = false,
            SearchPriority = false,
            ListingDurationDays = 7
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
            "Tối đa 1 tin đang hiển thị cùng lúc",
            "Mỗi tin hiển thị tối đa 7 ngày",
            "Bài đăng không kèm huy hiệu nổi bật",
            "Vị trí tìm kiếm: mức cơ bản, không ưu tiên"
        ],
        "Nanny" =>
        [
            "Tối đa 2 lượt ứng tuyển mỗi tháng (gói miễn phí)",
            "Hồ sơ hiển thị dạng thường",
            "Hồ sơ không kèm huy hiệu nổi bật",
            "Vị trí tìm kiếm: mức cơ bản, không ưu tiên"
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
