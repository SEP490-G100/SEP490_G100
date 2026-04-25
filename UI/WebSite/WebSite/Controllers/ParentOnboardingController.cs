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
    private const int MinParentAge = 18;
    private const int MaxNameLength = 100;
    private const int MaxAddressLength = 500;
    private const int MaxLocationLength = 100;
    private const int MaxFamilyDescriptionLength = 1000;
    private const int MaxChildTextLength = 1000;
    private const int MaxChildren = 20;

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
        var status = await GetOnboardingStatusAsync();
        if (status == null)
            return RedirectToAction("Start", "Onboarding");

        if (!string.Equals(status.Role, expectedRole, StringComparison.OrdinalIgnoreCase))
            return RedirectToAction("Start", "Onboarding");

        if (!status.RequiresOnboarding || string.Equals(status.NextStep, "Completed", StringComparison.OrdinalIgnoreCase))
            return RedirectToAction("Index", "Home");

        return null;
    }

    private static string? NormalizePhoneNumber(string? phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            return null;

        return phoneNumber.Trim();
    }

    private static bool IsValidPhoneNumber(string phoneNumber)
    {
        var normalized = NormalizePhoneNumber(phoneNumber);
        if (string.IsNullOrWhiteSpace(normalized))
            return false;

        return System.Text.RegularExpressions.Regex.IsMatch(normalized, @"^0\d{9}$");
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

    private static string? NormalizeOptionalText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsValidLength(string? value, int maxLength) =>
        string.IsNullOrWhiteSpace(value) || value.Trim().Length <= maxLength;

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
        var normalizedAddress = NormalizeOptionalText(model.Address);
        var normalizedCity = NormalizeOptionalText(model.City);
        var normalizedDistrict = NormalizeOptionalText(model.District);
        var normalizedWard = NormalizeOptionalText(model.Ward);

        var updateRequest = new
        {
            FirstName = firstName,
            LastName = lastName,
            PhoneNumber = NormalizePhoneNumber(model.PhoneNumber),
            AvatarUrl = model.AvatarUrl,
            DateOfBirth = model.DateOfBirth,
            Gender = (int?)null,
            Address = normalizedAddress,
            City = normalizedCity,
            District = normalizedDistrict,
            Ward = normalizedWard,
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
            FamilyDescription = NormalizeOptionalText(model.FamilyDescription),
            model.NumberOfChildren
        };

        var response = await _http.PutAsJsonAsync("/api/onboarding/parent/profile", payload);
        var content = await response.Content.ReadAsStringAsync();
        var apiResult = JsonSerializer.Deserialize<ApiResult>(content, JsonOpts);
        return (apiResult is { Success: true }, apiResult?.Message);
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in element.EnumerateObject())
            {
                if (string.Equals(prop.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    value = prop.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private static void EnsureChildrenCount(ParentOnboardingWizardViewModel model, int? desiredCount = null)
    {
        model.Children ??= new List<ParentOnboardingChildInputViewModel>();

        var count = desiredCount
                    ?? model.NumberOfChildren
                    ?? model.Children.Count;

        if (count < 1) count = 1;
        if (count > MaxChildren) count = MaxChildren;

        while (model.Children.Count < count)
            model.Children.Add(new ParentOnboardingChildInputViewModel());

        if (model.Children.Count > count)
            model.Children = model.Children.Take(count).ToList();
    }

    private async Task<List<ChildProfileViewModel>> LoadCurrentChildrenAsync()
    {
        SetAuthHeader();
        var response = await _http.GetAsync("/api/profile/children");
        if (!response.IsSuccessStatusCode)
            return new List<ChildProfileViewModel>();

        var content = await response.Content.ReadAsStringAsync();
        var apiResult = JsonSerializer.Deserialize<ApiResultDto>(content, JsonOpts);
        if (apiResult?.Data is not JsonElement element || element.ValueKind != JsonValueKind.Array)
            return new List<ChildProfileViewModel>();

        return JsonSerializer.Deserialize<List<ChildProfileViewModel>>(element.GetRawText(), JsonOpts)
               ?? new List<ChildProfileViewModel>();
    }

    private async Task<ParentOnboardingWizardViewModel> LoadFamilyStepModelAsync()
    {
        var vm = new ParentOnboardingWizardViewModel();

        SetAuthHeader();
        var profileResponse = await _http.GetAsync("/api/profile");
        if (profileResponse.IsSuccessStatusCode)
        {
            var profileContent = await profileResponse.Content.ReadAsStringAsync();
            var profileApiResult = JsonSerializer.Deserialize<ApiResultDto>(profileContent, JsonOpts);
            if (profileApiResult?.Data is JsonElement profileElement &&
                profileElement.ValueKind == JsonValueKind.Object)
            {
                if (TryGetPropertyIgnoreCase(profileElement, "familyDescription", out var familyDescriptionEl) &&
                    familyDescriptionEl.ValueKind == JsonValueKind.String)
                {
                    vm.FamilyDescription = familyDescriptionEl.GetString();
                }

                if (TryGetPropertyIgnoreCase(profileElement, "numberOfChildren", out var numberOfChildrenEl) &&
                    numberOfChildrenEl.ValueKind == JsonValueKind.Number &&
                    numberOfChildrenEl.TryGetInt32(out var numberOfChildren))
                {
                    vm.NumberOfChildren = numberOfChildren;
                }
            }
        }

        var existingChildren = await LoadCurrentChildrenAsync();
        if (existingChildren.Count > 0)
        {
            vm.Children = existingChildren
                .Select(child => new ParentOnboardingChildInputViewModel
                {
                    ChildAgeGroup = child.ChildAgeGroup,
                    ChildCharacteristic = child.Characteristic,
                    ChildSpecialNeeds = child.SpecialNeeds,
                    ChildNotes = child.Notes
                })
                .ToList();
        }

        var desired = vm.NumberOfChildren ?? vm.Children.Count;
        if (desired < 1)
            desired = 1;
        if (desired < vm.Children.Count)
            desired = vm.Children.Count;

        EnsureChildrenCount(vm, desired);
        return vm;
    }

    private async Task<(bool Success, string? Message)> UpsertChildrenAsync(ParentOnboardingWizardViewModel model)
    {
        SetAuthHeader();
        var desiredCount = model.NumberOfChildren ?? 1;
        if (desiredCount < 1)
            desiredCount = 1;

        EnsureChildrenCount(model, desiredCount);
        var existingChildren = await LoadCurrentChildrenAsync();

        for (var i = 0; i < desiredCount; i++)
        {
            var child = model.Children[i];
            var payload = new
            {
                Characteristic = NormalizeOptionalText(child.ChildCharacteristic),
                ChildAgeGroup = child.ChildAgeGroup,
                SpecialNeeds = NormalizeOptionalText(child.ChildSpecialNeeds),
                Notes = NormalizeOptionalText(child.ChildNotes)
            };

            HttpResponseMessage response;
            if (i < existingChildren.Count)
            {
                response = await _http.PutAsJsonAsync($"/api/profile/children/{existingChildren[i].Id}", payload);
            }
            else
            {
                response = await _http.PostAsJsonAsync("/api/profile/children", payload);
            }

            var content = await response.Content.ReadAsStringAsync();
            ApiResultDto? apiResult = null;
            try
            {
                apiResult = JsonSerializer.Deserialize<ApiResultDto>(content, JsonOpts);
            }
            catch
            {
                // fallback message handled below
            }

            var requestSuccess = response.IsSuccessStatusCode && (apiResult == null || apiResult.Success);
            if (!requestSuccess)
            {
                var defaultMessage = $"Lưu thông tin trẻ thứ {i + 1} thất bại.";
                return (false, apiResult?.Message ?? defaultMessage);
            }
        }

        return (true, null);
    }

    [HttpGet]
    public async Task<IActionResult> Step1BasicInfo()
    {
        var guard = await GuardOnboardingAccessAsync("Parent");
        if (guard != null)
            return guard;

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
        var guard = await GuardOnboardingAccessAsync("Parent");
        if (guard != null)
            return guard;

        if (direction != "next")
            return View(model);

        var normalizedFullName = NormalizeOptionalText(model.FullName);
        model.FullName = normalizedFullName;

        if (string.IsNullOrWhiteSpace(normalizedFullName))
        {
            ModelState.AddModelError(nameof(model.FullName), "Vui lòng nhập họ tên.");
        }
        else
        {
            var (firstName, lastName) = SplitFullName(normalizedFullName, "Parent", "User");
            if (string.IsNullOrWhiteSpace(firstName) || firstName.Length > MaxNameLength)
                ModelState.AddModelError(nameof(model.FullName), $"Họ không được rỗng và tối đa {MaxNameLength} ký tự.");

            if (string.IsNullOrWhiteSpace(lastName) || lastName.Length > MaxNameLength)
                ModelState.AddModelError(nameof(model.FullName), $"Tên không được rỗng và tối đa {MaxNameLength} ký tự.");
        }

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
            if (age < MinParentAge)
                ModelState.AddModelError(nameof(model.DateOfBirth), $"Phụ huynh phải đủ {MinParentAge} tuổi trở lên.");
        }

        model.PhoneNumber = NormalizePhoneNumber(model.PhoneNumber);
        if (!string.IsNullOrWhiteSpace(model.PhoneNumber) && !IsValidPhoneNumber(model.PhoneNumber))
            ModelState.AddModelError(nameof(model.PhoneNumber), "Số điện thoại phải gồm 10 chữ số và bắt đầu bằng 0.");

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

        model.Address = NormalizeOptionalText(model.Address);
        model.City = NormalizeOptionalText(model.City);
        model.District = NormalizeOptionalText(model.District);
        model.Ward = NormalizeOptionalText(model.Ward);

        if (string.IsNullOrWhiteSpace(model.Address))
            ModelState.AddModelError(nameof(model.Address), "Vui lòng nhập địa chỉ chi tiết.");
        else if (!IsValidLength(model.Address, MaxAddressLength))
            ModelState.AddModelError(nameof(model.Address), $"Địa chỉ không được vượt quá {MaxAddressLength} ký tự.");

        if (string.IsNullOrWhiteSpace(model.City) || string.IsNullOrWhiteSpace(model.District))
        {
            ModelState.AddModelError(string.Empty, "Vui lòng chọn đầy đủ Tỉnh/Thành và Quận/Huyện.");
        }
        else
        {
            if (!IsValidLength(model.City, MaxLocationLength))
                ModelState.AddModelError(nameof(model.City), $"Tỉnh/Thành tối đa {MaxLocationLength} ký tự.");

            if (!IsValidLength(model.District, MaxLocationLength))
                ModelState.AddModelError(nameof(model.District), $"Quận/Huyện tối đa {MaxLocationLength} ký tự.");
        }

        if (!IsValidLength(model.Ward, MaxLocationLength))
            ModelState.AddModelError(nameof(model.Ward), $"Phường/Xã tối đa {MaxLocationLength} ký tự.");

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
    public async Task<IActionResult> Step2Family()
    {
        var guard = await GuardOnboardingAccessAsync("Parent");
        if (guard != null)
            return guard;

        var vm = await LoadFamilyStepModelAsync();
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Step2Family(ParentOnboardingWizardViewModel model, string? direction)
    {
        var guard = await GuardOnboardingAccessAsync("Parent");
        if (guard != null)
            return guard;

        if (direction == "back")
            return RedirectToAction("Step1BasicInfo");

        if (direction != "next")
            return View(model);

        model.FamilyDescription = NormalizeOptionalText(model.FamilyDescription);
        var selectedChildCount = model.NumberOfChildren ?? 1;
        if (selectedChildCount < 1) selectedChildCount = 1;
        if (selectedChildCount > MaxChildren) selectedChildCount = MaxChildren;
        EnsureChildrenCount(model, selectedChildCount);

        if (string.IsNullOrWhiteSpace(model.FamilyDescription))
            ModelState.AddModelError(nameof(model.FamilyDescription), "Vui lòng mô tả gia đình.");
        else if (!IsValidLength(model.FamilyDescription, MaxFamilyDescriptionLength))
            ModelState.AddModelError(nameof(model.FamilyDescription), $"Mô tả gia đình tối đa {MaxFamilyDescriptionLength} ký tự.");

        if (!model.NumberOfChildren.HasValue || model.NumberOfChildren < 1 || model.NumberOfChildren > MaxChildren)
            ModelState.AddModelError(nameof(model.NumberOfChildren), $"Số lượng con phải trong khoảng 1-{MaxChildren}.");

        for (var i = 0; i < selectedChildCount; i++)
        {
            var child = model.Children[i];
            child.ChildCharacteristic = NormalizeOptionalText(child.ChildCharacteristic);
            child.ChildSpecialNeeds = NormalizeOptionalText(child.ChildSpecialNeeds);
            child.ChildNotes = NormalizeOptionalText(child.ChildNotes);

            if (!child.ChildAgeGroup.HasValue || !Enum.IsDefined(child.ChildAgeGroup.Value))
                ModelState.AddModelError($"Children[{i}].ChildAgeGroup", $"Vui lòng chọn nhóm tuổi cho trẻ thứ {i + 1}.");

            if (!IsValidLength(child.ChildCharacteristic, MaxChildTextLength))
                ModelState.AddModelError($"Children[{i}].ChildCharacteristic", $"Đặc điểm của trẻ thứ {i + 1} tối đa {MaxChildTextLength} ký tự.");

            if (!IsValidLength(child.ChildSpecialNeeds, MaxChildTextLength))
                ModelState.AddModelError($"Children[{i}].ChildSpecialNeeds", $"Nhu cầu đặc biệt của trẻ thứ {i + 1} tối đa {MaxChildTextLength} ký tự.");

            if (!IsValidLength(child.ChildNotes, MaxChildTextLength))
                ModelState.AddModelError($"Children[{i}].ChildNotes", $"Ghi chú của trẻ thứ {i + 1} tối đa {MaxChildTextLength} ký tự.");
        }

        if (!ModelState.IsValid)
            return View(model);

        var parentSaveResult = await SaveParentProfileAsync(model);
        if (!parentSaveResult.Success)
        {
            ModelState.AddModelError(string.Empty, parentSaveResult.Message ?? "Lưu thông tin gia đình thất bại.");
            return View(model);
        }

        var childSaveResult = await UpsertChildrenAsync(model);
        if (!childSaveResult.Success)
        {
            ModelState.AddModelError(string.Empty, childSaveResult.Message ?? "Lưu thông tin con thất bại. Vui lòng kiểm tra lại.");
            return View(model);
        }

        return RedirectToAction("Index", "Home");
    }
}

