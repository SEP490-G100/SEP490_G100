using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
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
        if (User.Identity?.IsAuthenticated == true)
            return LocalRedirect(returnUrl ?? "/");

        ViewBag.ReturnUrl = returnUrl;
        SetGoogleClientId();
        return View();
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

        // Sau khi đăng nhập, kiểm tra trạng thái onboarding (kèm Bearer token)
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
            // Nếu có lỗi khi gọi onboarding, bỏ qua và cho vào trang đích mặc định
        }

        return LocalRedirect(returnUrl ?? "/");
    }

    [HttpGet]
    public IActionResult Register() { SetGoogleClientId(); return View(); }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid) { SetGoogleClientId(); return View(model); }

        try
        {
            var response = await _http.PostAsJsonAsync("/api/auth/register", new
            {
                model.FirstName,
                model.LastName,
                model.Email,
                model.Password,
                model.ConfirmPassword
            });

            var result = await ReadApiResult<LoginResponseDto>(response);

            if (result == null || !result.Success)
            {
                ModelState.AddModelError("", result?.Message ?? "Đăng ký thất bại.");
                SetGoogleClientId();
                return View(model);
            }

            await SignInUserAsync(result.Data!);
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
    public async Task<IActionResult> GoogleLogin(string idToken)
    {
        var response = await _http.PostAsJsonAsync("/api/auth/google", new { idToken });
        var result = await ReadApiResult<LoginResponseDto>(response);

        if (result == null || !result.Success)
        {
            TempData["Error"] = result?.Message ?? "Đăng nhập Google thất bại.";
            return RedirectToAction("Login");
        }

        var loginData = result.Data!;
        await SignInUserAsync(loginData);

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
        if (!ModelState.IsValid) return View(model);

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
        if (string.IsNullOrWhiteSpace(email))
        {
            TempData["Error"] = "Không xác định được email. Vui lòng thử lại từ đầu.";
            return RedirectToAction("ForgotPassword");
        }

        var response = await _http.PostAsJsonAsync("/api/auth/forgot-password", new { Email = email });
        var result   = await ReadApiResult(response);

        if (result == null || !result.Success)
            TempData["Error"] = result?.Message ?? "Gửi lại mã OTP thất bại. Vui lòng thử lại.";
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
        if (!ModelState.IsValid) return View(model);

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

        TempData["Success"] = "Đặt lại mật khẩu thành công. Vui lòng đăng nhập.";
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

        // Xoá session đang giữ token Pending để buộc đăng nhập lại với token mới (đã kích hoạt)
        // Nếu không làm bước này, Login GET sẽ phát hiện cookie cũ và redirect thẳng "/" bỏ qua onboarding
        HttpContext.Session.Clear();
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        TempData["Success"] = "Xác thực email thành công! Vui lòng đăng nhập để tiếp tục.";
        return RedirectToAction("Login");
    }

    /// <summary>
    /// Cho phép user chọn vai trò (Nanny hoặc Parent) khi lần đầu tiên đăng nhập
    /// </summary>
    [Authorize, HttpGet]
    public IActionResult ChooseRole() => View();

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChooseRole(string role)
    {
        if (string.IsNullOrEmpty(role) || (role != "Nanny" && role != "Parent"))
        {
            ModelState.AddModelError("", "Vui lòng chọn một vai trò hợp lệ.");
            return View();
        }

        var token = HttpContext.Session.GetString("AccessToken");
        if (string.IsNullOrEmpty(token))
        {
            TempData["Error"] = "Phiên đăng nhập hết hạn. Vui lòng đăng nhập lại.";
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

            // Backend trả về token mới chứa role đã cập nhật → refresh session + cookie claims
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
        HttpContext.Session.SetString("AccessToken", data.AccessToken);
        HttpContext.Session.SetString("RefreshToken", data.RefreshToken);
        HttpContext.Session.SetString("TokenExpiresAt", data.ExpiresAt.ToString("O"));

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, data.User.Id.ToString()),
            new(ClaimTypes.Email, data.User.Email),
            new(ClaimTypes.GivenName, data.User.FirstName),
            new(ClaimTypes.Surname, data.User.LastName),
            new("AuthProvider", data.User.AuthProvider),
        };

        foreach (var role in data.User.Roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));
    }

    private bool IsGoogleUser() =>
        User.FindFirst("AuthProvider")?.Value?.Equals("google", StringComparison.OrdinalIgnoreCase) == true;

    private void SetGoogleClientId() =>
        ViewBag.GoogleClientId = _config["Google:ClientId"];

    private async Task<HttpResponseMessage> SendAuthorizedAsync(HttpMethod method, string url, object body)
    {
        var req = new HttpRequestMessage(method, url) { Content = JsonContent.Create(body) };
        var token = HttpContext.Session.GetString("AccessToken");
        if (!string.IsNullOrEmpty(token))
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _http.SendAsync(req);
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
