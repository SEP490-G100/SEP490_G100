using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Linq;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebSite.Models;
using WebSite.Models.Auth;

namespace WebSite.Controllers;

public class AuthController : Controller
{
    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _http;
    private readonly IConfiguration _config;

    public AuthController(IHttpClientFactory httpFactory, IConfiguration config)
    {
        _http = httpFactory.CreateClient("BackendApi");
        _config = config;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true && IsRestrictedArea(returnUrl) && !CanAccessRestrictedArea(User, returnUrl))
            return RedirectToAction(nameof(AccessDenied), new { returnUrl });

        if (User.Identity?.IsAuthenticated == true)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl))
                return LocalRedirect(returnUrl);

            if (User.IsInRole("Admin"))
                return Redirect("/Admin/Dashboard");

            if (User.IsInRole("Moderator"))
                return Redirect("/Moderator/Dashboard");

            return RedirectToAction("Index", "Home");
        }

        ViewBag.ReturnUrl = returnUrl;
        SetGoogleClientId();
        return View();
    }

    [HttpGet]
    public IActionResult AccessDenied(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated != true)
            return RedirectToAction(nameof(Login), new { returnUrl });

        Response.StatusCode = StatusCodes.Status403Forbidden;
        ViewBag.ReturnUrl = returnUrl;
        return View("~/Views/Auth/AccessDenied.cshtml");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        if (!ModelState.IsValid) { SetGoogleClientId(); return View(model); 
        }

        var response = await _http.PostAsJsonAsync("/api/auth/login", new { model.Email, model.Password });
        var result = await ReadApiResult<LoginResponseDto>(response);

        if (result?.NeedsVerification == true)
        {
            TempData["Info"] = result.Message;
            return RedirectToAction("VerifyEmail", new { email = result.Email });
        }

        if (result == null || !result.Success)
        {
            ModelState.AddModelError("", result?.Message ?? "Đăng nhập thất bại.");
            SetGoogleClientId();
            return View(model);
        }

        var loginData = result.Data!;
        await SignInUserAsync(loginData);
        var normalizedRoles = normalizeRoles(loginData.User.Roles);

        if (isAdminArea(returnUrl))
        {
            if (hasRole(normalizedRoles, "Admin"))
                return LocalRedirect(returnUrl!);
            if (hasRole(normalizedRoles, "Moderator"))
                return Redirect("/Moderator/Dashboard");

            return RedirectToAction(nameof(AccessDenied), new { returnUrl });
        }

        if (isModeratorArea(returnUrl))
        {
            if (hasRole(normalizedRoles, "Moderator"))
                return LocalRedirect(returnUrl!);
            if (hasRole(normalizedRoles, "Admin"))
                return Redirect("/Admin/Dashboard");

            return RedirectToAction(nameof(AccessDenied), new { returnUrl });
        }

        if (hasRole(normalizedRoles, "Admin"))
            return Redirect("/Admin/Dashboard");
        if (hasRole(normalizedRoles, "Moderator"))
            return Redirect("/Moderator/Dashboard");

   
        if (!normalizedRoles.Any())
            return RedirectToAction("ChooseRole", "Auth");
        try
        {
            var obRequest = new HttpRequestMessage(HttpMethod.Get, "/api/onboarding/status")
            {
                Headers = { Authorization = new AuthenticationHeaderValue("Bearer", loginData.AccessToken) }
            };
            var ob = 
                await _http.SendAsync(obRequest);
            var obResult = await ReadApiResult<OnboardingStatusViewModel>(ob);
            if (obResult?.Data != null && obResult.Data.RequiresOnboarding && obResult.Data.NextStep != "Completed")
                return RedirectToAction("Start", "Onboarding");
        }
        catch
        {
        }

        return LocalRedirect(returnUrl ?? "/");
    }

    [HttpGet]
    public IActionResult Register() { SetGoogleClientId(); return View(); }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        model.PhoneNumber = NormalizePhoneNumber(model.PhoneNumber);
        if (!string.IsNullOrWhiteSpace(model.PhoneNumber) && !IsValidPhoneNumber(model.PhoneNumber))
            ModelState.AddModelError(nameof(model.PhoneNumber), "Số điện thoại phải gồm 10 chữ số và bắt đầu bằng 0.");

        if (!ModelState.IsValid) { SetGoogleClientId(); return View(model); }

        try
        {
            var response = await _http.PostAsJsonAsync("/api/auth/register", new
            {
                model.FirstName,
                model.LastName,
                PhoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber.Trim(),
                model.Email,
                model.Password,
                model.ConfirmPassword
            });

            var result = await ReadApiResult(response);

            if (result == null || !result.Success)
            {
                ModelState.AddModelError("", result?.Message ?? "Đăng ký thất bại.");
                SetGoogleClientId();
                return View(model);
            }

            return RedirectToAction("VerifyEmail", new { email = model.Email });
        }
        catch (HttpRequestException)
        {
            ModelState.AddModelError("", "Không thể kết nối đến máy chủ. Vui lòng thử lại sau.");
            SetGoogleClientId();
            return View(model);
        }

    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> GoogleLogin(string idToken, string? returnUrl = null)
    {
        var response = await _http.PostAsJsonAsync("/api/auth/google", new { idToken });
        var result = await ReadApiResult<LoginResponseDto>(response);

        if (result == null || !result.Success)
        {
            TempData["AuthError"] = result?.Message ?? "Đăng nhập Google thất bại.";
            return RedirectToAction("Login");
        }

        var loginData = result.Data!;
        await SignInUserAsync(loginData);
        var normalizedRoles = normalizeRoles(loginData.User.Roles);

        if (isAdminArea(returnUrl))
        {
            if (hasRole(normalizedRoles, "Admin"))
                return LocalRedirect(returnUrl!);
            if (hasRole(normalizedRoles, "Moderator"))
                return Redirect("/Moderator/Dashboard");

            return RedirectToAction(nameof(AccessDenied), new { returnUrl });
        }

        if (isModeratorArea(returnUrl))
        {
            if (hasRole(normalizedRoles, "Moderator"))
                return LocalRedirect(returnUrl!);
            if (hasRole(normalizedRoles, "Admin"))
                return Redirect("/Admin/Dashboard");

            return RedirectToAction(nameof(AccessDenied), new { returnUrl });
        }

        if (hasRole(normalizedRoles, "Admin"))
            return Redirect("/Admin/Dashboard");
        if (hasRole(normalizedRoles, "Moderator"))
            return Redirect("/Moderator/Dashboard");

   
        if (!normalizedRoles.Any())
            return RedirectToAction("ChooseRole", "Auth");

        try
        {
            var obRequest = new HttpRequestMessage(HttpMethod.Get, "/api/onboarding/status")
            {
                Headers = { Authorization = new AuthenticationHeaderValue("Bearer", loginData.AccessToken) }
            };
            var ob = await _http.SendAsync(obRequest);
            var obResult = await ReadApiResult<OnboardingStatusViewModel>(ob);
            if (obResult?.Data != null && obResult.Data.RequiresOnboarding && obResult.Data.NextStep != "Completed")
                return RedirectToAction("Start", "Onboarding");
        }
        catch
        {
        }

        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult ForgotPassword() => View();

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        model.Email = NormalizeEmail(model.Email);
        if (!ModelState.IsValid) return View(model);
        if (!IsValidEmail(model.Email))
        {
            ModelState.AddModelError(nameof(model.Email), "Email không hợp lệ.");
            return View(model);
        }

        var response = await _http.PostAsJsonAsync("/api/auth/forgot-password", new { model.Email });
        var result = await ReadApiResult(response);

        if (result == null || !result.Success)
        {
            ModelState.AddModelError("", result?.Message ?? "Có lỗi xảy ra.");
            return View(model);
        }

        TempData["Success"] = result.Message;
        return RedirectToAction("ResetPassword", new { email = model.Email });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendForgotPasswordOtp(string email)
    {
        email = NormalizeEmail(email);
        if (string.IsNullOrWhiteSpace(email))
        {
            TempData["Error"] = "Không xác định được email. Vui lòng thử lại từ đầu.";
            return RedirectToAction("ForgotPassword");
        }
        if (!IsValidEmail(email))
        {
            TempData["Error"] = "Email không hợp lệ. Vui lòng kiểm tra lại.";
            return RedirectToAction("ForgotPassword");
        }

        var response = await _http.PostAsJsonAsync("/api/auth/forgot-password", new { Email = email });
        var result   = await ReadApiResult(response);

        if (result == null || !result.Success)
        {
            TempData["Error"] = result?.Message ?? "Gửi lại mã OTP thất bại. Vui lòng thử lại.";
            return RedirectToAction("ForgotPassword");
        }
        else
            TempData["Success"] = "Đã gửi lại mã OTP. Vui lòng kiểm tra email (kể cả hộp thư spam).";

        return RedirectToAction("ResetPassword", new { email });
    }

    [HttpGet]
    public IActionResult ResetPassword(string? email = null) =>
        View(new ResetPasswordViewModel { Email = email ?? "" });

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        model.Email = NormalizeEmail(model.Email);
        if (!ModelState.IsValid) return View(model);
        if (!IsValidEmail(model.Email))
        {
            ModelState.AddModelError(nameof(model.Email), "Email không hợp lệ.");
            return View(model);
        }

        var response = await _http.PostAsJsonAsync("/api/auth/reset-password", new
        {
            model.Email, model.OtpCode, model.NewPassword
        });

        var result = await ReadApiResult(response);

        if (result == null || !result.Success)
        {
            ModelState.AddModelError("", result?.Message ?? "Đặt lại mật khẩu thất bại.");
            return View(model);
        }

        TempData["AuthSuccess"] = "Đặt lại mật khẩu thành công. Vui lòng đăng nhập.";
        return RedirectToAction("Login");
    }

    [HttpGet]
    public IActionResult VerifyEmail(string? email = null) =>
        View(new VerifyEmailViewModel { Email = email ?? "" });

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendVerifyEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            TempData["Error"] = "Không xác định được email. Vui lòng đăng ký lại.";
            return RedirectToAction("Register");
        }

        var response = await _http.PostAsJsonAsync("/api/auth/resend-verify", new { Email = email });
        var result   = await ReadApiResult(response);

        if (result == null || !result.Success)
            TempData["Error"] = result?.Message ?? "Gửi lại mã OTP thất bại. Vui lòng thử lại.";
        else
            TempData["Success"] = "Đã gửi lại mã OTP. Vui lòng kiểm tra email.";

        return RedirectToAction("VerifyEmail", new { email });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyEmail(VerifyEmailViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var response = await _http.PostAsJsonAsync("/api/auth/verify-email", new { model.Email, model.OtpCode });
        var result = await ReadApiResult(response);

        if (result == null || !result.Success)
        {
            ModelState.AddModelError("", result?.Message ?? "Xác thực email thất bại.");
            return View(model);
        }

        // Xóa session đang giữ token Pending để buộc đăng nhập lại với token mới (đã kích hoạt)
        // Nếu không làm bước này, đăng nhập GET sẽ phát hiện cookie cũ và redirect thẳng "/" bỏ qua onboarding
        HttpContext.Session.Clear();
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        TempData["AuthSuccess"] = "Xác thực email thành công! Vui lòng đăng nhập để tiếp tục.";
        return RedirectToAction("Login");
    }

    /// <summary>
    /// Cho phép user chọn vai trò (Nanny hoặc Parent) khi lần đầu tiên đăng nhập
    /// </summary>
    [Authorize, HttpGet]
    public async Task<IActionResult> ChooseRole()
    {
        if (await HasOnboardingRoleAsync())
            return RedirectToAction("Start", "Onboarding");

        return View();
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChooseRole(string role)
    {
        if (await HasOnboardingRoleAsync())
        {
            TempData["Info"] = "Bạn đã chọn vai trò trước đó.";
            return RedirectToAction("Start", "Onboarding");
        }

        if (string.IsNullOrEmpty(role) || (role != "Nanny" && role != "Parent"))
        {
            ModelState.AddModelError("", "Vui lòng chọn một vai trò hợp lệ.");
            return View();
        }

        var token = HttpContext.Session.GetString("AccessToken");
        if (string.IsNullOrEmpty(token))
        {
            TempData["AuthError"] = "Phiên đăng nhập hết hạn. Vui lòng đăng nhập lại.";
            return RedirectToAction("Login");
        }

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/set-role")
            {
                Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) },
                Content = JsonContent.Create(new { role })
            };
            
            var response = await _http.SendAsync(request);
            var result = await ReadApiResult<LoginResponseDto>(response);

            if (result == null || !result.Success)
            {
                ModelState.AddModelError("", result?.Message ?? "Lỗi khi cập nhật vai trò. Vui lòng thử lại.");
                return View();
            }

            // Backend trả về token mới chứa role đã cập nhật -> refresh session + cookie claims
            if (result.Data != null)
            {
                await SignInUserAsync(result.Data);
            }

            // Sau khi set role thành công, chuyển hướng tới Onboarding/Start
            return RedirectToAction("Start", "Onboarding");
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", $"Có lỗi xảy ra: {ex.Message}");
            return View();
        }
    }

    [Authorize, HttpGet]
    public IActionResult ChangePassword()
    {
        if (IsGoogleUser())
        {
            TempData["Error"] = "Tài khoản Google không sử dụng mật khẩu.";
            return RedirectToAction("Index", "Home");
        }
        return View();
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        if (string.IsNullOrEmpty(HttpContext.Session.GetString("AccessToken")))
        {
            ModelState.AddModelError("", "Phiên đăng nhập hết hạn. Vui lòng đăng nhập lại.");
            return View(model);
        }

        var response = await SendAuthorizedAsync(HttpMethod.Post, "/api/auth/change-password",
            new { currentPassword = model.CurrentPassword, newPassword = model.NewPassword });
        var result = await ReadApiResult(response);

        if (result == null || !result.Success)
        {
            ModelState.AddModelError("", result?.Message ?? "Đổi mật khẩu thất bại.");
            return View(model);
        }

        TempData["Success"] = "Đổi mật khẩu thành công!";
        return RedirectToAction("ChangePassword");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        var refreshToken = HttpContext.Session.GetString("RefreshToken");
        if (!string.IsNullOrEmpty(refreshToken))
            await SendAuthorizedAsync(HttpMethod.Post, "/api/auth/logout", new { refreshToken });

        HttpContext.Session.Clear();
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }

    private async Task SignInUserAsync(LoginResponseDto data)
    {
        var normalizedRoles = normalizeRoles(data.User.Roles);

        HttpContext.Session.SetString("AccessToken", data.AccessToken);
        HttpContext.Session.SetString("RefreshToken", data.RefreshToken);
        HttpContext.Session.SetString("TokenExpiresAt", data.ExpiresAt.ToString("O"));
        if (hasRole(normalizedRoles, "Nanny"))
            HttpContext.Session.SetString("ShowNannyVerifyPrompt", "1");
        else
            HttpContext.Session.Remove("ShowNannyVerifyPrompt");

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, data.User.Id.ToString()),
            new(ClaimTypes.Email, data.User.Email),
            new(ClaimTypes.GivenName, data.User.FirstName),
            new(ClaimTypes.Surname, data.User.LastName),
            new("AuthProvider", data.User.AuthProvider),
        };

        var avatarUrl = NormalizeAvatarUrl(data.User.AvatarUrl);
        if (!string.IsNullOrWhiteSpace(avatarUrl))
            claims.Add(new Claim("AvatarUrl", avatarUrl));

        foreach (var role in normalizedRoles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));
    }

    private string? NormalizeAvatarUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return url;

        if (Uri.TryCreate(url, UriKind.Absolute, out _))
            return url;

        var apiBaseUrl = (_config["ApiSettings:BaseUrl"] ?? string.Empty).TrimEnd('/');
        if (string.IsNullOrWhiteSpace(apiBaseUrl))
            return url;

        if (url.StartsWith("~/", StringComparison.Ordinal))
            url = url[1..];

        return url.StartsWith("/", StringComparison.Ordinal)
            ? apiBaseUrl + url
            : apiBaseUrl + "/" + url.TrimStart('/');
    }

    private bool IsGoogleUser() =>
        User.FindFirst("AuthProvider")?.Value?.Equals("google", StringComparison.OrdinalIgnoreCase) == true;

    private static bool IsRestrictedArea(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
            return false;

        return isAdminArea(returnUrl) || isModeratorArea(returnUrl);
    }

    private static bool CanAccessRestrictedArea(ClaimsPrincipal principal, string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
            return true;

        if (returnUrl.StartsWith("/Admin", StringComparison.OrdinalIgnoreCase))
            return principal.IsInRole("Admin");

        if (returnUrl.StartsWith("/Moderator", StringComparison.OrdinalIgnoreCase))
            return principal.IsInRole("Moderator");

        return true;
    }

    private static bool CanAccessRestrictedArea(IEnumerable<string> roles, string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
            return true;

        var normalizedRoles = normalizeRoles(roles);

        if (isAdminArea(returnUrl))
            return hasRole(normalizedRoles, "Admin");

        if (isModeratorArea(returnUrl))
            return hasRole(normalizedRoles, "Moderator");

        return true;
    }

    private static bool isAdminArea(string? returnUrl) =>
        !string.IsNullOrWhiteSpace(returnUrl) &&
        returnUrl.StartsWith("/Admin", StringComparison.OrdinalIgnoreCase);

    private static bool isModeratorArea(string? returnUrl) =>
        !string.IsNullOrWhiteSpace(returnUrl) &&
        returnUrl.StartsWith("/Moderator", StringComparison.OrdinalIgnoreCase);

    private static List<string> normalizeRoles(IEnumerable<string>? roles) =>
        (roles ?? [])
            .Where(static role => !string.IsNullOrWhiteSpace(role))
            .Select(static role => role.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static bool hasRole(IEnumerable<string> roles, string roleName) =>
        roles.Any(role => string.Equals(role, roleName, StringComparison.OrdinalIgnoreCase));

    private bool HasOnboardingRoleInClaims() =>
        User.IsInRole("Parent") || User.IsInRole("Nanny");

    private async Task<bool> HasOnboardingRoleAsync()
    {
        if (HasOnboardingRoleInClaims())
            return true;

        var token = HttpContext.Session.GetString("AccessToken");
        if (string.IsNullOrWhiteSpace(token))
            return false;

        try
        {
            var req = new HttpRequestMessage(HttpMethod.Get, "/api/onboarding/status")
            {
                Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) }
            };
            var response = await _http.SendAsync(req);
            var result = await ReadApiResult<OnboardingStatusViewModel>(response);
            var role = result?.Data?.Role;

            return string.Equals(role, "Parent", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(role, "Nanny", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static string? NormalizePhoneNumber(string? phoneNumber) =>
        string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber.Trim();

    private static bool IsValidPhoneNumber(string phoneNumber) =>
        Regex.IsMatch(phoneNumber, @"^0\d{9}$");

    private static string NormalizeEmail(string? email) =>
        string.IsNullOrWhiteSpace(email) ? string.Empty : email.Trim();

    private static bool IsValidEmail(string email) =>
        Regex.IsMatch(
            email,
            @"^(?!.*\.\.)(?!\.)(?!.*\.$)[A-Za-z0-9._%+\-']+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}$",
            RegexOptions.IgnoreCase);

    private void SetGoogleClientId() =>
        ViewBag.GoogleClientId = _config["Google:ClientId"];

    private async Task<HttpResponseMessage> SendAuthorizedAsync(HttpMethod method, string url, object body)
    {
        await EnsureTokenFreshAsync();
        var req = new HttpRequestMessage(method, url) { Content = JsonContent.Create(body) };
        var token = HttpContext.Session.GetString("AccessToken");
        if (!string.IsNullOrEmpty(token))
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _http.SendAsync(req);
    }

    /// <summary>
    /// Tự động refresh AccessToken nếu còn dưới 2 phút hết hạn.
    /// </summary>
    private async Task EnsureTokenFreshAsync()
    {
        var expiresAtStr = HttpContext.Session.GetString("TokenExpiresAt");
        if (!DateTime.TryParse(expiresAtStr, null,
                System.Globalization.DateTimeStyles.RoundtripKind, out var expiresAt))
            return;

        if (DateTime.UtcNow < expiresAt.AddMinutes(-2))
            return;

        var accessToken  = HttpContext.Session.GetString("AccessToken");
        var refreshToken = HttpContext.Session.GetString("RefreshToken");
        if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(refreshToken))
            return;

        try
        {
            var response = await _http.PostAsJsonAsync("/api/auth/refresh",
                new { accessToken, refreshToken });
            var result = await ReadApiResult<LoginResponseDto>(response);
            if (result?.Success == true && result.Data != null)
                await SignInUserAsync(result.Data);
        }
        catch { /* silent - không block main flow */ }
    }

    private static async Task<ApiResult?> ReadApiResult(HttpResponseMessage response) =>
        await ReadApiResult<ApiResult>(response) as ApiResult;

    private static async Task<ApiResult<T>?> ReadApiResult<T>(HttpResponseMessage response)
    {
        try
        {
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<ApiResult<T>>(json, _jsonOptions);
        }
        catch
        {
            return new ApiResult<T> { Success = false, Message = $"Lỗi server (HTTP {(int)response.StatusCode})." };
        }
    }
}
