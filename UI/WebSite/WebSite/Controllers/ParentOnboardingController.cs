using System.Net.Http.Headers;
using System.Text.Json;
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
        _apiBaseUrl = (config["ApiSettings:BaseUrl"] ?? "").TrimEnd('/');
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

        var firstName = string.Empty;
        var lastName = string.Empty;

        if (!string.IsNullOrWhiteSpace(model.FullName))
        {
            var parts = model.FullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1)
            {
                lastName = parts[0];
            }
            else
            {
                firstName = string.Join(" ", parts[..^1]);
                lastName = parts[^1];
            }
        }

        var updateRequest = new
        {
            FirstName = string.IsNullOrWhiteSpace(firstName) ? "Parent" : firstName,
            LastName = string.IsNullOrWhiteSpace(lastName) ? "User" : lastName,
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
        return apiResult != null && apiResult.Success;
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
        return (apiResult != null && apiResult.Success, apiResult?.Message);
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
            return apiResult != null && apiResult.Success;
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
        if (direction == "next")
        {
            if (string.IsNullOrWhiteSpace(model.FullName))
                ModelState.AddModelError(nameof(model.FullName), "Vui lòng nhap ho ten.");

            if (!model.DateOfBirth.HasValue)
            {
                ModelState.AddModelError(nameof(model.DateOfBirth), "Vui lòng chon ngay sinh.");
            }
            else if (model.DateOfBirth.Value > DateOnly.FromDateTime(DateTime.Today))
            {
                ModelState.AddModelError(nameof(model.DateOfBirth), "Ngày sinh không được lon hon ngay hiện tại.");
            }
            else
            {
                var today = DateOnly.FromDateTime(DateTime.Today);
                var age = today.Year - model.DateOfBirth.Value.Year;
                if (model.DateOfBirth.Value > today.AddYears(-age))
                    age--;

                if (age < 18)
                    ModelState.AddModelError(nameof(model.DateOfBirth), "Parent phai du 18 tuoi tro len.");
            }

            if (!string.IsNullOrWhiteSpace(model.PhoneNumber) && !IsValidPhoneNumber(model.PhoneNumber))
                ModelState.AddModelError(nameof(model.PhoneNumber), "S? điện thoại không hop le (9-15 chu so, cho phep dau +).");

            if (model.AvatarFile != null && model.AvatarFile.Length > 0)
            {
                var ext = Path.GetExtension(model.AvatarFile.FileName)?.ToLowerInvariant();
                var allowedExt = new[] { ".jpg", ".jpeg", ".png" };
                if (string.IsNullOrWhiteSpace(ext) || !allowedExt.Contains(ext))
                    ModelState.AddModelError(nameof(model.AvatarFile), "Anh dai dien chi chap nhan .jpg, .jpeg hoac .png.");

                const long maxSizeBytes = 5 * 1024 * 1024;
                if (model.AvatarFile.Length > maxSizeBytes)
                    ModelState.AddModelError(nameof(model.AvatarFile), "Anh dai dien không được vuot qua 5MB.");

                var contentType = model.AvatarFile.ContentType?.ToLowerInvariant();
                if (!string.IsNullOrWhiteSpace(contentType))
                {
                    var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png" };
                    if (!allowedTypes.Contains(contentType))
                        ModelState.AddModelError(nameof(model.AvatarFile), "Dinh ?ang tep anh không hop le.");
                }
            }

            if (string.IsNullOrWhiteSpace(model.Address))
                ModelState.AddModelError(nameof(model.Address), "Vui lòng nhap địa chỉ chi tiết.");

            if (string.IsNullOrWhiteSpace(model.City) || string.IsNullOrWhiteSpace(model.District))
                ModelState.AddModelError(string.Empty, "Vui lòng chon day du Tỉnh/Th?nh va Quận/Huyện/Phường.");

            if (!ModelState.IsValid)
                return View(model);

            var success = await SaveBasicUserInfoAsync(model);
            if (!success)
            {
                ModelState.AddModelError(string.Empty, "Luu thong tin thất bại. Vui lòng thử lại.");
                return View(model);
            }

            return RedirectToAction("Step2Family");
        }

        return View(model);
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
        {
            return RedirectToAction("Step1BasicInfo");
        }

        if (direction == "next")
        {
            if (string.IsNullOrWhiteSpace(model.FamilyDescription))
                ModelState.AddModelError(nameof(model.FamilyDescription), "Vui lòng m? t? gia đình.");

            if (!model.NumberOfChildren.HasValue || model.NumberOfChildren < 1)
                ModelState.AddModelError(nameof(model.NumberOfChildren), "Vui lòng nhap so luong con.");

            if (!model.ChildAgeGroup.HasValue)
                ModelState.AddModelError(nameof(model.ChildAgeGroup), "Vui lòng chon nhom tuoi cua tre.");

            if (!ModelState.IsValid)
                return View(model);

            var parentSaveResult = await SaveParentProfileAsync(model);
            if (!parentSaveResult.Success)
            {
                ModelState.AddModelError(string.Empty, parentSaveResult.Message ?? "Luu thong tin gia đình thất bại.");
                return View(model);
            }

            var childSuccess = await CreateChildAsync(model);
            if (!childSuccess)
            {
                ModelState.AddModelError(string.Empty, "Tao profile con thất bại. Vui lòng kiem tra thong tin va thử lại.");
                return View(model);
            }

            return RedirectToAction("Index", "Home");
        }

        return View(model);
    }
}

