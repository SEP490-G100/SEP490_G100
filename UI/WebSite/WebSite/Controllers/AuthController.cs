using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
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
            ModelState.AddModelError("", result?.Message ?? "ÄÄƒng nháº­p tháº¥t báº¡i.");
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

        // --- Staff roles: skip onboarding, redirect directly to their dashboards ---
        if (hasRole(normalizedRoles, "Admin"))
            return Redirect("/Admin/Dashboard");
        if (hasRole(normalizedRoles, "Moderator"))
            return Redirect("/Moderator/Dashboard");

        // Náº¿u user chÆ°a cÃ³ role (Ä‘áº·c biá»‡t case Ä‘Äƒng kÃ½/Ä‘Äƒng nháº­p Google láº§n Ä‘áº§u),
        // luÃ´n báº¯t buá»™c chá»n role trÆ°á»›c khi cháº¡y onboarding theo role.
        if (!normalizedRoles.Any())
            return RedirectToAction("ChooseRole", "Auth");

        // Sau khi Ä‘Äƒng nháº­p, kiá»ƒm tra tráº¡ng thÃ¡i onboarding (kÃ¨m Bearer token)
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
            // Náº¿u cÃ³ lá»—i khi gá»i onboarding, bá» qua vÃ  cho vÃ o trang Ä‘Ã­ch máº·c Ä‘á»‹nh
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
                PhoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber.Trim(),
                model.Email,
                model.Password,
                model.ConfirmPassword
            });

            var result = await ReadApiResult(response);

            if (result == null || !result.Success)
            {
                ModelState.AddModelError("", result?.Message ?? "ÄÄƒng kÃ½ tháº¥t báº¡i.");
                SetGoogleClientId();
                return View(model);
            }

            return RedirectToAction("VerifyEmail", new { email = model.Email });
        }
        catch (HttpRequestException)
        {
            ModelState.AddModelError("", "KhÃ´ng thá»ƒ káº¿t ná»‘i Ä‘áº¿n mÃ¡y chá»§. Vui lÃ²ng thá»­ láº¡i sau.");
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
            TempData["Error"] = result?.Message ?? "ÄÄƒng nháº­p Google tháº¥t báº¡i.";
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

        // --- Staff roles: skip onboarding, redirect directly to their dashboards ---
        if (hasRole(normalizedRoles, "Admin"))
            return Redirect("/Admin/Dashboard");
        if (hasRole(normalizedRoles, "Moderator"))
            return Redirect("/Moderator/Dashboard");

        // Náº¿u user chÆ°a cÃ³ role (Ä‘áº·c biá»‡t case Ä‘Äƒng kÃ½ Google láº§n Ä‘áº§u),
        // luÃ´n báº¯t buá»™c chá»n role trÆ°á»›c khi cháº¡y onboarding theo role.
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
        if (!ModelState.IsValid) return View(model);

        var response = await _http.PostAsJsonAsync("/api/auth/forgot-password", new { model.Email });
        var result = await ReadApiResult(response);

        if (result == null || !result.Success)
        {
            ModelState.AddModelError("", result?.Message ?? "CÃ³ lá»—i xáº£y ra.");
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
            TempData["Error"] = "KhÃ´ng xÃ¡c Ä‘á»‹nh Ä‘Æ°á»£c email. Vui lÃ²ng thá»­ láº¡i tá»« Ä‘áº§u.";
            return RedirectToAction("ForgotPassword");
        }

        var response = await _http.PostAsJsonAsync("/api/auth/forgot-password", new { Email = email });
        var result   = await ReadApiResult(response);

        if (result == null || !result.Success)
            TempData["Error"] = result?.Message ?? "Gửi lại mã OTP thất bại. Vui lòng thử lại.";
        else
            TempData["Success"] = "ÄÃ£ gá»­i láº¡i mÃ£ OTP. Vui lÃ²ng kiá»ƒm tra email (ká»ƒ cáº£ há»™p thÆ° spam).";

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
            ModelState.AddModelError("", result?.Message ?? "Äáº·t láº¡i máº­t kháº©u tháº¥t báº¡i.");
            return View(model);
        }

        TempData["Success"] = "Äáº·t láº¡i máº­t kháº©u thÃ nh cÃ´ng. Vui lÃ²ng Ä‘Äƒng nháº­p.";
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
            TempData["Error"] = "KhÃ´ng xÃ¡c Ä‘á»‹nh Ä‘Æ°á»£c email. Vui lÃ²ng Ä‘Äƒng kÃ½ láº¡i.";
            return RedirectToAction("Register");
        }

        var response = await _http.PostAsJsonAsync("/api/auth/resend-verify", new { Email = email });
        var result   = await ReadApiResult(response);

        if (result == null || !result.Success)
            TempData["Error"] = result?.Message ?? "Gửi lại mã OTP thất bại. Vui lòng thử lại.";
        else
            TempData["Success"] = "ÄÃ£ gá»­i láº¡i mÃ£ OTP. Vui lÃ²ng kiá»ƒm tra email.";

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

        // XoÃ¡ session Ä‘ang giá»¯ token Pending Ä‘á»ƒ buá»™c Ä‘Äƒng nháº­p láº¡i vá»›i token má»›i (Ä‘Ã£ kÃ­ch hoáº¡t)
        // Náº¿u khÃ´ng lÃ m bÆ°á»›c nÃ y, đăng nhập GET sáº½ phÃ¡t hiá»‡n cookie cÅ© vÃ  redirect tháº³ng "/" bá» qua onboarding
        HttpContext.Session.Clear();
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        TempData["Success"] = "XÃ¡c thá»±c email thÃ nh cÃ´ng! Vui lÃ²ng Ä‘Äƒng nháº­p Ä‘á»ƒ tiáº¿p tá»¥c.";
        return RedirectToAction("Login");
    }

    /// <summary>
    /// Cho phÃ©p user chá»n vai trÃ² (Nanny hoáº·c Parent) khi láº§n Ä‘áº§u tiÃªn Ä‘Äƒng nháº­p
    /// </summary>
    [Authorize, HttpGet]
    public IActionResult ChooseRole() => View();

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChooseRole(string role)
    {
        if (string.IsNullOrEmpty(role) || (role != "Nanny" && role != "Parent"))
        {
            ModelState.AddModelError("", "Vui lÃ²ng chá»n má»™t vai trÃ² há»£p lá»‡.");
            return View();
        }

        var token = HttpContext.Session.GetString("AccessToken");
        if (string.IsNullOrEmpty(token))
        {
            TempData["Error"] = "PhiÃªn Ä‘Äƒng nháº­p háº¿t háº¡n. Vui lÃ²ng Ä‘Äƒng nháº­p láº¡i.";
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
                ModelState.AddModelError("", result?.Message ?? "Lá»—i khi cáº­p nháº­t vai trÃ². Vui lÃ²ng thá»­ láº¡i.");
                return View();
            }

            // Backend tráº£ vá» token má»›i chá»©a role Ä‘Ã£ cáº­p nháº­t â†’ refresh session + cookie claims
            if (result.Data != null)
            {
                await SignInUserAsync(result.Data);
            }

            // Sau khi set role thÃ nh cÃ´ng, chuyá»ƒn hÆ°á»›ng tá»›i Onboarding/Start
            return RedirectToAction("Start", "Onboarding");
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", $"CÃ³ lá»—i xáº£y ra: {ex.Message}");
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
            ModelState.AddModelError("", "PhiÃªn Ä‘Äƒng nháº­p háº¿t háº¡n. Vui lÃ²ng Ä‘Äƒng nháº­p láº¡i.");
            return View(model);
        }

        var response = await SendAuthorizedAsync(HttpMethod.Post, "/api/auth/change-password",
            new { currentPassword = model.CurrentPassword, newPassword = model.NewPassword });
        var result = await ReadApiResult(response);

        if (result == null || !result.Success)
        {
            ModelState.AddModelError("", result?.Message ?? "Äá»•i máº­t kháº©u tháº¥t báº¡i.");
            return View(model);
        }

        TempData["Success"] = "Äá»•i máº­t kháº©u thÃ nh cÃ´ng!";
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
    /// Tá»± Ä‘á»™ng refresh AccessToken náº¿u cÃ²n dÆ°á»›i 2 phÃºt háº¿t háº¡n.
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
        catch { /* silent â€” khÃ´ng block main flow */ }
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
            return new ApiResult<T> { Success = false, Message = $"Lá»—i server (HTTP {(int)response.StatusCode})." };
        }
    }
}


