using System.Net.Http.Headers;
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
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public AuthController(IHttpClientFactory httpFactory, IConfiguration config)
    {
        _http = httpFactory.CreateClient("BackendApi");
        _config = config;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewBag.ReturnUrl = returnUrl;
        SetGoogleClientId();
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        if (!ModelState.IsValid) { SetGoogleClientId(); return View(model); }

        try
        {
            var response = await _http.PostAsJsonAsync("/api/auth/login", new { model.Email, model.Password });
            var result = await ReadApiResult<LoginResponseDto>(response);

            if (result == null || !result.Success)
            {
                ModelState.AddModelError("", result?.Message ?? "Đăng nhập thất bại.");
                SetGoogleClientId();
                return View(model);
            }

            await SignInUserAsync(result.Data!);
            return LocalRedirect(returnUrl ?? "/");
        }
        catch (HttpRequestException)
        {
            ModelState.AddModelError("", "Không thể kết nối đến máy chủ. Vui lòng thử lại sau.");
            SetGoogleClientId();
            return View(model);
        }
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
                model.Email, model.Password, model.FirstName, model.LastName, model.PhoneNumber
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

        await SignInUserAsync(result.Data!);
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
        if (!string.IsNullOrWhiteSpace(email))
            await _http.PostAsJsonAsync("/api/auth/resend-verify", new { Email = email });

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

        TempData["Success"] = "Xác thực email thành công!";
        return RedirectToAction("Index", "Home");
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
    public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword)
    {
        var token = HttpContext.Session.GetString("AccessToken");
        if (string.IsNullOrEmpty(token))
        {
            ModelState.AddModelError("", "Phiên đăng nhập hết hạn. Vui lòng đăng nhập lại.");
            return View();
        }

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/change-password")
        {
            Content = JsonContent.Create(new { currentPassword, newPassword }),
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) }
        };

        var response = await _http.SendAsync(request);
        var result = await ReadApiResult(response);

        if (result == null || !result.Success)
        {
            ModelState.AddModelError("", result?.Message ?? "Đổi mật khẩu thất bại.");
            return View();
        }

        TempData["Success"] = "Đổi mật khẩu thành công!";
        return RedirectToAction("ChangePassword");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        var refreshToken = HttpContext.Session.GetString("RefreshToken");
        if (!string.IsNullOrEmpty(refreshToken))
        {
            var accessToken = HttpContext.Session.GetString("AccessToken");
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout")
            {
                Content = JsonContent.Create(new { refreshToken }),
                Headers = { Authorization = new AuthenticationHeaderValue("Bearer", accessToken) }
            };
            await _http.SendAsync(request);
        }

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
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
    }

    private bool IsGoogleUser() =>
        User.FindFirst("AuthProvider")?.Value?.Equals("google", StringComparison.OrdinalIgnoreCase) == true;

    private void SetGoogleClientId() =>
        ViewBag.GoogleClientId = _config["Google:ClientId"];

    private static async Task<ApiResult?> ReadApiResult(HttpResponseMessage response) =>
        await ReadApiResult<ApiResult>(response) as ApiResult;

    private static async Task<ApiResult<T>?> ReadApiResult<T>(HttpResponseMessage response)
    {
        try
        {
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<ApiResult<T>>(json, JsonOpts);
        }
        catch
        {
            return new ApiResult<T> { Success = false, Message = $"Lỗi server (HTTP {(int)response.StatusCode})." };
        }
    }
}
