using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebSite.Models;
using WebSite.Models.Profile;
using WebSite.Services;

namespace WebSite.Controllers;

[Authorize(Roles = "Parent")]
public class ParentOnboardingController : Controller
{
    private readonly HttpClient _http;
    private readonly string _apiBaseUrl;
    private readonly IAzureBlobStorageService _blobStorageService;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public ParentOnboardingController(
        IHttpClientFactory httpFactory,
        IConfiguration config,
        IAzureBlobStorageService blobStorageService)
    {
        _http = httpFactory.CreateClient("BackendApi");
        _apiBaseUrl = (config["ApiSettings:BaseUrl"] ?? string.Empty).TrimEnd('/');
        _blobStorageService = blobStorageService;
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

    private string? NormalizeAvatarUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return url;
        if (Uri.TryCreate(url, UriKind.Absolute, out _)) return url;
        if (url.StartsWith("~/", StringComparison.Ordinal))
            url = url[1..];
        if (url.StartsWith("/") && !string.IsNullOrWhiteSpace(_apiBaseUrl))
            return _apiBaseUrl + url;
        if (!string.IsNullOrWhiteSpace(_apiBaseUrl))
            return _apiBaseUrl + "/" + url.TrimStart('/');
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
        var (firstName, lastName) = SplitFullName(fullName, "Parent", "User");

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

    private async Task<EditPersonalInfoViewModel?> LoadCurrentProfileAsync()
    {
        SetAuthHeader();
        var response = await _http.GetAsync("/api/profile");
        if (!response.IsSuccessStatusCode) return null;

        var content = await response.Content.ReadAsStringAsync();
        var apiResult = JsonSerializer.Deserialize<ApiResultDto>(content, JsonOpts);
        if (apiResult?.Data is JsonElement element)
        {
            var profile = JsonSerializer.Deserialize<EditPersonalInfoViewModel>(element.GetRawText(), JsonOpts)
                          ?? new EditPersonalInfoViewModel();

            if (string.IsNullOrWhiteSpace(profile.AvatarUrl) &&
                element.TryGetProperty("avatarUrl", out var avatarElement) &&
                avatarElement.ValueKind == JsonValueKind.String)
            {
                profile.AvatarUrl = avatarElement.GetString();
            }

            return profile;
        }

        return null;
    }

    private async Task<bool> SaveBasicUserInfoAsync(ParentOnboardingWizardViewModel model)
    {
        SetAuthHeader();

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

        var (firstName, lastName) = SplitFullName(model.FullName, "Parent", "User");

        var updateRequest = new
        {
            FirstName = firstName,
            LastName = lastName,
            PhoneNumber = NormalizePhoneNumber(model.PhoneNumber),
            AvatarUrl = model.AvatarUrl,
            DateOfBirth = model.DateOfBirth,
            Gender = (int?)null,
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
        return apiResult is { Success: true };
    }

    private async Task<(bool Success, string? Message)> SaveParentProfileAsync(ParentOnboardingWizardViewModel model)
    {
        SetAuthHeader();
        var payload = new
        {
            model.FamilyDescription,
            model.NumberOfChildren
        };

        var response = await _http.PutAsJsonAsync("/api/onboarding/parent/profile", payload);
        var content = await response.Content.ReadAsStringAsync();
        var apiResult = JsonSerializer.Deserialize<ApiResult>(content, JsonOpts);
        return (apiResult is { Success: true }, apiResult?.Message);
    }

    private async Task<bool> CreateChildAsync(ParentOnboardingWizardViewModel model)
    {
        SetAuthHeader();
        var payload = new
        {
            Characteristic = model.ChildCharacteristic,
            ChildAgeGroup = model.ChildAgeGroup,
            SpecialNeeds = model.ChildSpecialNeeds,
            Notes = model.ChildNotes
        };

        var response = await _http.PostAsJsonAsync("/api/profile/children", payload);
        if (!response.IsSuccessStatusCode)
            return false;

        var content = await response.Content.ReadAsStringAsync();
        try
        {
            var apiResult = JsonSerializer.Deserialize<ApiResultDto>(content, JsonOpts);
            return apiResult is { Success: true };
        }
        catch
        {
            return false;
        }
    }

    [HttpGet]
    public async Task<IActionResult> Step1BasicInfo()
    {
        var existing = await LoadCurrentProfileAsync();
        var vm = new ParentOnboardingWizardViewModel();

        if (existing != null)
        {
            vm.FullName = $"{existing.FirstName} {existing.LastName}".Trim();
            vm.PhoneNumber = existing.PhoneNumber;
            vm.DateOfBirth = existing.DateOfBirth;
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
    public async Task<IActionResult> Step1BasicInfo(ParentOnboardingWizardViewModel model, string? direction)
    {
        if (direction != "next")
            return View(model);

        if (string.IsNullOrWhiteSpace(model.FullName))
            ModelState.AddModelError(nameof(model.FullName), "Vui lòng nhập họ tên.");

        if (!model.DateOfBirth.HasValue)
        {
            ModelState.AddModelError(nameof(model.DateOfBirth), "Vui lòng chọn ngày sinh.");
        }
        else if (model.DateOfBirth.Value > DateOnly.FromDateTime(DateTime.Today))
        {
            ModelState.AddModelError(nameof(model.DateOfBirth), "Ngày sinh không được lớn hơn ngày hiện tại.");
        }
        else
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var age = today.Year - model.DateOfBirth.Value.Year;
            if (model.DateOfBirth.Value > today.AddYears(-age))
                age--;
            if (age < 18)
                ModelState.AddModelError(nameof(model.DateOfBirth), "Phụ huynh phải đủ 18 tuổi trở lên.");
        }

        if (!string.IsNullOrWhiteSpace(model.PhoneNumber) && !IsValidPhoneNumber(model.PhoneNumber))
            ModelState.AddModelError(nameof(model.PhoneNumber), "Số điện thoại không hợp lệ (10 chữ số).");

        if (model.AvatarFile != null && model.AvatarFile.Length > 0)
        {
            var ext = Path.GetExtension(model.AvatarFile.FileName)?.ToLowerInvariant();
            var allowedExt = new[] { ".jpg", ".jpeg", ".png" };
            if (string.IsNullOrWhiteSpace(ext) || !allowedExt.Contains(ext))
                ModelState.AddModelError(nameof(model.AvatarFile), "Ảnh đại diện chỉ chấp nhận .jpg, .jpeg hoặc .png.");

            const long maxSizeBytes = 5 * 1024 * 1024;
            if (model.AvatarFile.Length > maxSizeBytes)
                ModelState.AddModelError(nameof(model.AvatarFile), "Ảnh đại diện không được vượt quá 5MB.");

            var contentType = model.AvatarFile.ContentType?.ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(contentType))
            {
                var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png" };
                if (!allowedTypes.Contains(contentType))
                    ModelState.AddModelError(nameof(model.AvatarFile), "Định dạng tệp ảnh không hợp lệ.");
            }
        }

        if (string.IsNullOrWhiteSpace(model.Address))
            ModelState.AddModelError(nameof(model.Address), "Vui lòng nhập địa chỉ chi tiết.");

        if (string.IsNullOrWhiteSpace(model.City) || string.IsNullOrWhiteSpace(model.District))
            ModelState.AddModelError(string.Empty, "Vui lòng chọn đầy đủ Tỉnh/Thành và Quận/Phường.");

        if (!ModelState.IsValid)
            return View(model);

        var success = await SaveBasicUserInfoAsync(model);
        if (!success)
        {
            ModelState.AddModelError(string.Empty, "Lưu thông tin thất bại. Vui lòng thử lại.");
            return View(model);
        }

        await RefreshAuthClaimsAsync(model.FullName, model.AvatarUrl);
        return RedirectToAction("Step2Family");
    }

    [HttpGet]
    public IActionResult Step2Family()
    {
        return View(new ParentOnboardingWizardViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Step2Family(ParentOnboardingWizardViewModel model, string? direction)
    {
        if (direction == "back")
            return RedirectToAction("Step1BasicInfo");

        if (direction != "next")
            return View(model);

        if (string.IsNullOrWhiteSpace(model.FamilyDescription))
            ModelState.AddModelError(nameof(model.FamilyDescription), "Vui lòng mô tả gia đình.");

        if (!model.NumberOfChildren.HasValue || model.NumberOfChildren < 1)
            ModelState.AddModelError(nameof(model.NumberOfChildren), "Vui lòng nhập số lượng con.");

        if (!model.ChildAgeGroup.HasValue)
            ModelState.AddModelError(nameof(model.ChildAgeGroup), "Vui lòng chọn nhóm tuổi của trẻ.");

        if (!ModelState.IsValid)
            return View(model);

        var parentSaveResult = await SaveParentProfileAsync(model);
        if (!parentSaveResult.Success)
        {
            ModelState.AddModelError(string.Empty, parentSaveResult.Message ?? "Lưu thông tin gia đình thất bại.");
            return View(model);
        }

        var childSuccess = await CreateChildAsync(model);
        if (!childSuccess)
        {
            ModelState.AddModelError(string.Empty, "Tạo hồ sơ con thất bại. Vui lòng kiểm tra thông tin và thử lại.");
            return View(model);
        }

        return RedirectToAction("Index", "Home");
    }
}
