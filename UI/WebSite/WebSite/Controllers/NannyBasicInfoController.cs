using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebSite.Models;
using WebSite.Models.Profile;
using WebSite.Services;

using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace WebSite.Controllers;

[Authorize(Roles = "Nanny")]
public class NannyBasicInfoController : Controller
{
    private readonly HttpClient _http;
    private readonly string _apiBaseUrl;
    private readonly IAzureBlobStorageService _blobStorageService;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public NannyBasicInfoController(
        IHttpClientFactory httpFactory,
        IConfiguration config,
        IAzureBlobStorageService blobStorageService)
    {
        _http = httpFactory.CreateClient("BackendApi");
        _apiBaseUrl = (config["ApiSettings:BaseUrl"] ?? "").TrimEnd('/');
        _blobStorageService = blobStorageService;
    }

    private string? NormalizeAvatarUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return url;
        if (Uri.TryCreate(url, UriKind.Absolute, out _)) return url;
        if (url.StartsWith("/") && !string.IsNullOrWhiteSpace(_apiBaseUrl))
            return _apiBaseUrl + url;
        return url;
    }

    private static (string FirstName, string LastName) SplitFullName(
        string? fullName,
        string fallbackFirstName,
        string fallbackLastName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            return (fallbackFirstName, fallbackLastName);

        var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return (fallbackFirstName, fallbackLastName);

        if (parts.Length == 1)
            return (fallbackFirstName, parts[0]);

        return (string.Join(" ", parts[..^1]), parts[^1]);
    }

    private async Task RefreshAuthClaimsAsync(string? fullName, string? avatarUrl)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return;

        var email = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
        var authProvider = User.FindFirst("AuthProvider")?.Value ?? "email";
        var roles = User.Claims
            .Where(c => c.Type == ClaimTypes.Role)
            .Select(c => c.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var (firstName, lastName) = SplitFullName(fullName, "Nanny", "User");

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.GivenName, firstName),
            new(ClaimTypes.Surname, lastName),
            new("AuthProvider", authProvider)
        };

        var normalizedAvatar = NormalizeAvatarUrl(avatarUrl);
        if (!string.IsNullOrWhiteSpace(normalizedAvatar))
            claims.Add(new Claim("AvatarUrl", normalizedAvatar));

        foreach (var role in roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));
    }

    private string? GetToken() => HttpContext.Session.GetString("AccessToken");

    private void SetAuthHeader()
    {
        var token = GetToken();
        if (!string.IsNullOrEmpty(token))
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private static string? NormalizePhoneNumber(string? phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            return null;

        var normalized = phoneNumber.Trim().Replace(" ", string.Empty);
        if (normalized.StartsWith("00", StringComparison.Ordinal))
            normalized = "+" + normalized[2..];

        return normalized;
    }

    private static bool IsValidPhoneNumber(string phoneNumber)
    {
        var normalized = NormalizePhoneNumber(phoneNumber);
        if (string.IsNullOrWhiteSpace(normalized))
            return false;

        if (normalized.StartsWith("+", StringComparison.Ordinal))
            normalized = normalized[1..];

        return normalized.Length is >= 9 and <= 15 && normalized.All(char.IsDigit);
    }

    private async Task<EditPersonalInfoViewModel?> LoadCurrentProfileAsync()
    {
        SetAuthHeader();
        var response = await _http.GetAsync("/api/profile");
        if (!response.IsSuccessStatusCode) return null;

        var content = await response.Content.ReadAsStringAsync();
        var apiResult = JsonSerializer.Deserialize<ApiResultDto>(content, JsonOpts);
        if (apiResult?.Data is System.Text.Json.JsonElement element)
        {
            return JsonSerializer.Deserialize<EditPersonalInfoViewModel>(element.GetRawText(), JsonOpts);
        }

        return null;
    }

    private async Task<bool> SaveProfileAsync(NannyBasicInfoWizardViewModel model)
    {
        SetAuthHeader();

        // Upload Avatar
        if (model.AvatarFile != null && model.AvatarFile.Length > 0)
        {
            try
            {
                model.AvatarUrl = await _blobStorageService.UploadUserAvatarAsync(model.AvatarFile);
            }
            catch
            {
                return false;
            }
        }

        var (firstName, lastName) = SplitFullName(model.FullName, "Nanny", "User");

        var updateRequest = new
        {
            FirstName = firstName,
            LastName = lastName,
            PhoneNumber = NormalizePhoneNumber(model.PhoneNumber),
            AvatarUrl = model.AvatarUrl,
            model.DateOfBirth,
            model.Gender,
            model.Address,
            model.City,
            model.District,
            model.Ward,
            model.Latitude,
            model.Longitude
        };

        var response = await _http.PutAsJsonAsync("/api/profile", updateRequest);
        var resContent = await response.Content.ReadAsStringAsync();
        var apiResult = JsonSerializer.Deserialize<ApiResultDto>(resContent, JsonOpts);
        return apiResult != null && apiResult.Success;
    }

    [HttpGet]
    public async Task<IActionResult> Step1BasicInfo()
    {
        var existing = await LoadCurrentProfileAsync();
        var vm = new NannyBasicInfoWizardViewModel();

        if (existing != null)
        {
            vm.FullName = $"{existing.FirstName} {existing.LastName}".Trim();
            vm.PhoneNumber = existing.PhoneNumber;
            vm.DateOfBirth = existing.DateOfBirth;
            vm.Gender = existing.Gender;
            vm.Address = existing.Address;
            vm.City = existing.City;
            vm.District = existing.District;
            vm.Ward = existing.Ward;
            vm.Latitude = existing.Latitude;
            vm.Longitude = existing.Longitude;
            vm.AvatarUrl = NormalizeAvatarUrl(existing.AvatarUrl);
        }

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Step1BasicInfo(NannyBasicInfoWizardViewModel model, string? direction)
    {
        if (direction == "next")
        {
            if (string.IsNullOrWhiteSpace(model.FullName))
                ModelState.AddModelError(nameof(model.FullName), "Vui lòng nhập họ tên.");

            if (model.DateOfBirth == null)
                ModelState.AddModelError(nameof(model.DateOfBirth), "Vui lòng chọn ngày sinh.");
            else
            {
                var today = DateOnly.FromDateTime(DateTime.Today);
                var dob = model.DateOfBirth.Value;
                var age = today.Year - dob.Year;
                if (dob > today.AddYears(-age)) age--;
                if (age < 18)
                    ModelState.AddModelError(nameof(model.DateOfBirth), "Bảo mẫu phải đủ 18 tuổi trở lên.");
            }

            if (model.Gender == null)
                ModelState.AddModelError(nameof(model.Gender), "Vui lòng chọn giới tính.");

            if (!string.IsNullOrWhiteSpace(model.PhoneNumber) && !IsValidPhoneNumber(model.PhoneNumber))
                ModelState.AddModelError(nameof(model.PhoneNumber), "Số điện thoại không hợp lệ (9-15 chữ số, cho phép dấu +).");

            if (string.IsNullOrWhiteSpace(model.Address))
                ModelState.AddModelError(nameof(model.Address), "Vui lòng nhập địa chỉ chi tiết.");

            if (string.IsNullOrWhiteSpace(model.City) || string.IsNullOrWhiteSpace(model.District))
                ModelState.AddModelError(string.Empty, "Vui lòng chọn đầy đủ Tỉnh/Thành và Quận/Huyện/Phường.");

            if (!ModelState.IsValid)
                return View(model);

            var success = await SaveProfileAsync(model);
            if (!success)
            {
                ModelState.AddModelError(string.Empty, "Cập nhật thông tin thất bại. Vui lòng thử lại.");
                return View(model);
            }

            await RefreshAuthClaimsAsync(model.FullName, model.AvatarUrl);
            return RedirectToAction("Start", "Onboarding");
        }

        return View(model);
    }
}
