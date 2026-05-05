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
    private const string NannyOnboardingCompletedSessionKey = "NannyOnboardingCompleted";
    private const int MaxAddressLength = 500;

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

    private static bool IsValidAvatarFile(IFormFile file, out string errorMessage)
    {
        errorMessage = string.Empty;

        var ext = Path.GetExtension(file.FileName)?.ToLowerInvariant();
        var allowedExt = new[] { ".jpg", ".jpeg", ".png" };
        if (string.IsNullOrWhiteSpace(ext) || !allowedExt.Contains(ext))
        {
            errorMessage = "Ảnh đại diện chỉ chấp nhận .jpg, .jpeg hoặc .png.";
            return false;
        }

        const long maxSizeBytes = 5 * 1024 * 1024;
        if (file.Length > maxSizeBytes)
        {
            errorMessage = "Ảnh đại diện không được vượt quá 5MB.";
            return false;
        }

        var contentType = file.ContentType?.ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(contentType))
        {
            var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png" };
            if (!allowedTypes.Contains(contentType))
            {
                errorMessage = "Định dạng tệp ảnh không hợp lệ.";
                return false;
            }
        }

        return true;
    }

    private static ApiResultDto? TryDeserializeApiResult(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return null;

        try
        {
            return JsonSerializer.Deserialize<ApiResultDto>(content, JsonOpts);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task<OnboardingStatusViewModel?> GetOnboardingStatusAsync()
    {
        var token = GetToken();
        if (string.IsNullOrWhiteSpace(token))
            return null;

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/onboarding/status");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
            return null;

        var content = await response.Content.ReadAsStringAsync();
        var apiResult = TryDeserializeApiResult(content);
        if (apiResult?.Data is JsonElement element && element.ValueKind == JsonValueKind.Object)
            return JsonSerializer.Deserialize<OnboardingStatusViewModel>(element.GetRawText(), JsonOpts);

        return null;
    }

    private async Task<IActionResult?> GuardOnboardingAccessAsync(string expectedRole)
    {
        if (IsNannyOnboardingLocked())
            return RedirectToAction("Index", "Home");

        var status = await GetOnboardingStatusAsync();
        if (status == null)
            return RedirectToAction("Start", "Onboarding");

        SyncNannyOnboardingCompletedFlag(status);
        if (IsNannyOnboardingLocked())
            return RedirectToAction("Index", "Home");

        if (!string.Equals(status.Role, expectedRole, StringComparison.OrdinalIgnoreCase))
            return RedirectToAction("Start", "Onboarding");

        if (!status.RequiresOnboarding || string.Equals(status.NextStep, "Completed", StringComparison.OrdinalIgnoreCase))
        {
            HttpContext.Session.SetString(NannyOnboardingCompletedSessionKey, "1");
            return RedirectToAction("Index", "Home");
        }

        return null;
    }

    private static bool IsCompletedNannyOnboardingStatus(OnboardingStatusViewModel status) =>
        string.Equals(status.Role, "Nanny", StringComparison.OrdinalIgnoreCase) &&
        (!status.RequiresOnboarding || string.Equals(status.NextStep, "Completed", StringComparison.OrdinalIgnoreCase));

    private void SyncNannyOnboardingCompletedFlag(OnboardingStatusViewModel status)
    {
        if (IsCompletedNannyOnboardingStatus(status))
        {
            HttpContext.Session.SetString(NannyOnboardingCompletedSessionKey, "1");
            return;
        }

        if (string.Equals(status.Role, "Nanny", StringComparison.OrdinalIgnoreCase))
            HttpContext.Session.Remove(NannyOnboardingCompletedSessionKey);
    }

    private bool IsNannyOnboardingLocked() =>
        IsNannyRole() &&
        string.Equals(HttpContext.Session.GetString(NannyOnboardingCompletedSessionKey), "1", StringComparison.Ordinal);

    private bool IsNannyRole()
    {
        if (User.IsInRole("Nanny"))
            return true;

        return User.Claims.Any(c =>
            c.Type == ClaimTypes.Role &&
            string.Equals(c.Value, "Nanny", StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildFallbackHttpErrorMessage(HttpResponseMessage response, string? body)
    {
        var statusText = $"HTTP {(int)response.StatusCode}";
        var trimmed = (body ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return $"Cập nhật thông tin thất bại ({statusText}).";

        if (trimmed.Length > 180)
            trimmed = trimmed[..180] + "...";

        return $"Cập nhật thông tin thất bại ({statusText}): {trimmed}";
    }

    private async Task<EditPersonalInfoViewModel?> LoadCurrentProfileAsync()
    {
        SetAuthHeader();
        var response = await _http.GetAsync("/api/profile");
        if (!response.IsSuccessStatusCode) return null;

        var content = await response.Content.ReadAsStringAsync();
        var apiResult = TryDeserializeApiResult(content);
        if (apiResult?.Data is System.Text.Json.JsonElement element)
        {
            return JsonSerializer.Deserialize<EditPersonalInfoViewModel>(element.GetRawText(), JsonOpts);
        }

        return null;
    }

    private async Task<(bool Success, string? Message)> SaveProfileAsync(NannyBasicInfoWizardViewModel model)
    {
        SetAuthHeader();

        // Upload Avatar
        if (string.IsNullOrWhiteSpace(model.AvatarUrl) && model.AvatarFile != null && model.AvatarFile.Length > 0)
        {
            try
            {
                model.AvatarUrl = await _blobStorageService.UploadUserAvatarAsync(model.AvatarFile);
            }
            catch
            {
                return (false, "Tải ảnh đại diện thất bại. Vui lòng thử lại.");
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
        var apiResult = TryDeserializeApiResult(resContent);
        if (apiResult != null)
            return (apiResult.Success, apiResult.Message);

        return (false, BuildFallbackHttpErrorMessage(response, resContent));
    }

    [HttpGet]
    public async Task<IActionResult> Step1BasicInfo()
    {
        var guard = await GuardOnboardingAccessAsync("Nanny");
        if (guard != null)
            return guard;

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
        var guard = await GuardOnboardingAccessAsync("Nanny");
        if (guard != null)
            return guard;

        var isNext = string.IsNullOrWhiteSpace(direction)
            || direction.Equals("next", StringComparison.OrdinalIgnoreCase);

        if (isNext)
        {
            if (model.AvatarFile != null && model.AvatarFile.Length > 0)
            {
                if (!IsValidAvatarFile(model.AvatarFile, out var avatarError))
                {
                    ModelState.AddModelError(nameof(model.AvatarFile), avatarError);
                }
                else
                {
                    try
                    {
                        // Keep avatar URL across validation round-trips.
                        model.AvatarUrl = await _blobStorageService.UploadUserAvatarAsync(model.AvatarFile);
                    }
                    catch
                    {
                        ModelState.AddModelError(nameof(model.AvatarFile), "Tải ảnh đại diện thất bại. Vui lòng thử lại.");
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(model.FullName))
                ModelState.AddModelError(nameof(model.FullName), "Vui lòng nhập họ tên.");

            if (model.DateOfBirth == null)
                ModelState.AddModelError(nameof(model.DateOfBirth), "Vui lòng chọn ngày sinh.");
            else
            {
                var today = DateOnly.FromDateTime(DateTime.Today);
                var dob = model.DateOfBirth.Value;
                if (dob > today)
                    ModelState.AddModelError(nameof(model.DateOfBirth), "Ngày sinh không được lớn hơn ngày hiện tại.");

                var age = today.Year - dob.Year;
                if (dob > today.AddYears(-age)) age--;
                if (age < 20)
                    ModelState.AddModelError(nameof(model.DateOfBirth), "Bảo mẫu phải đủ 20 tuổi trở lên.");
            }

            if (model.Gender == null)
                ModelState.AddModelError(nameof(model.Gender), "Vui lòng chọn giới tính.");

            if (!string.IsNullOrWhiteSpace(model.PhoneNumber) && !IsValidPhoneNumber(model.PhoneNumber))
                ModelState.AddModelError(nameof(model.PhoneNumber), "Số điện thoại không hợp lệ (9-15 chữ số, cho phép dấu +).");

            if (!string.IsNullOrWhiteSpace(model.Address) && model.Address.Length > MaxAddressLength)
                ModelState.AddModelError(nameof(model.Address), $"Địa chỉ không được vượt quá {MaxAddressLength} ký tự.");

            if (string.IsNullOrWhiteSpace(model.City) || string.IsNullOrWhiteSpace(model.District))
                ModelState.AddModelError(string.Empty, "Vui lòng chọn đầy đủ Tỉnh/Thành và Quận/Huyện/Phường.");

            if (!ModelState.IsValid)
                return View(model);

            var saveResult = await SaveProfileAsync(model);
            if (!saveResult.Success)
            {
                ModelState.AddModelError(string.Empty, saveResult.Message ?? "Cập nhật thông tin thất bại. Vui lòng thử lại.");
                return View(model);
            }

            await RefreshAuthClaimsAsync(model.FullName, model.AvatarUrl);
            return RedirectToAction("Profile", "Nanny");
        }

        return View(model);
    }
}

