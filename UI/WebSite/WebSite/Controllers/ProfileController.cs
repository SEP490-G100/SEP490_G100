using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebSite.Models.Profile;
using WebSite.Services;
using WebSite.Validation;

namespace WebSite.Controllers;

[Authorize]
public class ProfileController : Controller
{
    private readonly HttpClient _http;
    private readonly string _apiBaseUrl;
    private readonly IAzureBlobStorageService _blobStorageService;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public ProfileController(IHttpClientFactory httpFactory, IConfiguration config, IAzureBlobStorageService blobStorageService)
    {
        _http = httpFactory.CreateClient("BackendApi");
        _apiBaseUrl = (config["ApiSettings:BaseUrl"] ?? "").TrimEnd('/');
        _blobStorageService = blobStorageService;
    }

    // Helper method to get token from session
    private string? GetTokenFromSession()
    {
        return HttpContext.Session.GetString("AccessToken");
    }

    private PersonalProfileViewModel BuildProfileFromClaims()
    {
        var roles = User.Claims
            .Where(c => c.Type == ClaimTypes.Role)
            .Select(c => c.Value)
            .ToList();

        return new PersonalProfileViewModel
        {
            UserId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty,
            Email = User.FindFirstValue(ClaimTypes.Email) ?? "",
            FirstName = User.FindFirstValue(ClaimTypes.GivenName) ?? "",
            LastName = User.FindFirstValue(ClaimTypes.Surname) ?? "",
            Roles = roles
        };
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

    private async Task RefreshAuthClaimsAsync(EditPersonalInfoViewModel model)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
        var authProvider = User.FindFirst("AuthProvider")?.Value ?? "email";
        var roles = User.Claims
            .Where(c => c.Type == ClaimTypes.Role)
            .Select(c => c.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (string.IsNullOrWhiteSpace(userId))
            return;

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.GivenName, model.FirstName ?? string.Empty),
            new(ClaimTypes.Surname, model.LastName ?? string.Empty),
            new("AuthProvider", authProvider)
        };

        var normalizedAvatar = NormalizeAvatarUrl(model.AvatarUrl);
        if (!string.IsNullOrWhiteSpace(normalizedAvatar))
            claims.Add(new Claim("AvatarUrl", normalizedAvatar));

        foreach (var role in roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));
    }

    private static TModel? DeserializeApiData<TModel>(ApiResultDto? apiResult) where TModel : class
    {
        if (apiResult?.Success != true || apiResult.Data == null)
            return null;

        if (apiResult.Data is JsonElement element)
        {
            if (element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                return null;

            return JsonSerializer.Deserialize<TModel>(element.GetRawText(), JsonOpts);
        }

        return JsonSerializer.Deserialize<TModel>(
            JsonSerializer.Serialize(apiResult.Data), JsonOpts);
    }

    private static bool TryGetNamedPropertyRecursive(
        JsonElement root,
        IReadOnlyCollection<string> propertyNames,
        out JsonElement matchedValue)
    {
        var queue = new Queue<JsonElement>();
        queue.Enqueue(root);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in current.EnumerateObject())
                {
                    if (propertyNames.Any(name => property.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                    {
                        matchedValue = property.Value;
                        return true;
                    }

                    if (property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                        queue.Enqueue(property.Value);
                }
            }
            else if (current.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in current.EnumerateArray())
                {
                    if (item.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                        queue.Enqueue(item);
                }
            }
        }

        matchedValue = default;
        return false;
    }

    private static string? ExtractStringFromJson(JsonElement root, params string[] propertyNames)
    {
        if (!TryGetNamedPropertyRecursive(root, propertyNames, out var valueElement))
            return null;

        if (valueElement.ValueKind != JsonValueKind.String)
            return null;

        var value = valueElement.GetString();
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static DateOnly? ExtractDateOnlyFromJson(JsonElement root, params string[] propertyNames)
    {
        if (!TryGetNamedPropertyRecursive(root, propertyNames, out var valueElement))
            return null;

        if (valueElement.ValueKind != JsonValueKind.String)
            return null;

        var raw = valueElement.GetString();
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        if (DateOnly.TryParse(raw, out var dateOnly))
            return dateOnly;

        if (DateTime.TryParse(raw, out var dateTime))
            return DateOnly.FromDateTime(dateTime);

        return null;
    }

    private EditPersonalInfoViewModel BuildEditProfileFromClaims()
    {
        var roles = User.Claims
            .Where(c => c.Type == ClaimTypes.Role)
            .Select(c => c.Value)
            .ToList();

        return new EditPersonalInfoViewModel
        {
            FirstName = User.FindFirstValue(ClaimTypes.GivenName) ?? "",
            LastName = User.FindFirstValue(ClaimTypes.Surname) ?? "",
            Roles = roles
        };
    }

    private void ApplyRolesToEditModel(EditPersonalInfoViewModel model)
    {
        model.Roles = User.Claims
            .Where(c => c.Type == ClaimTypes.Role)
            .Select(c => c.Value)
            .ToList();
    }

    private async Task PopulateAvailableSkillsAsync(EditPersonalInfoViewModel model)
    {
        if (!model.IsNanny) return;

        var response = await _http.GetAsync("/api/onboarding/skills");
        if (!response.IsSuccessStatusCode) return;

        var content = await response.Content.ReadAsStringAsync();
        var apiResult = JsonSerializer.Deserialize<ApiResultDto>(content, JsonOpts);
        if (apiResult?.Data is not JsonElement element || element.ValueKind != JsonValueKind.Array)
            return;

        model.AvailableSkills = JsonSerializer.Deserialize<List<SelectableSkillViewModel>>(
            element.GetRawText(), JsonOpts) ?? new();
    }

    // Get and display personal profile
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        try
        {
            var token = GetTokenFromSession();
            if (string.IsNullOrEmpty(token))
            {
                ViewBag.Warning = "Phiên đăng nhập đã hết hạn, đang hiển thị thông tin cơ bản từ cookie.";
                return View(BuildProfileFromClaims());
            }

            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _http.GetAsync("/api/profile");
            if (!response.IsSuccessStatusCode)
            {
                ViewBag.Warning = "Không thể tải hồ sơ từ API, đang hiển thị thông tin cơ bản từ cookie.";
                return View(BuildProfileFromClaims());
            }

            var content = await response.Content.ReadAsStringAsync();
            var apiResult = JsonSerializer.Deserialize<ApiResultDto>(content, JsonOpts);

            if (apiResult == null || !apiResult.Success)
            {
                ViewBag.Warning = "Không thể tải toàn bộ hồ sơ, đang hiển thị thông tin cơ bản từ cookie.";
                return View(BuildProfileFromClaims());
            }

            var profile = DeserializeApiData<PersonalProfileViewModel>(apiResult);

            if (profile != null)
                profile.AvatarUrl = NormalizeAvatarUrl(profile.AvatarUrl);

            if (profile?.IsNanny == true)
                await LoadNannyReviewsAsync(profile, profile.UserId);

            return View(profile ?? BuildProfileFromClaims());
        }
        catch (Exception ex)
        {
            TempData["Error"] = "Lỗi tải thông tin: " + ex.Message;
            return View(BuildProfileFromClaims());
        }
    }

    [HttpGet]
    public async Task<IActionResult> ViewUser(
        Guid userId,
        Guid? jobPostingId = null,
        Guid? jobApplicationId = null,
        Guid? nannyProfileId = null,
        Guid? contactRequestId = null,
        string? source = null)
    {
        if (userId == Guid.Empty)
            return RedirectToAction(nameof(Index));

        try
        {
            var token = GetTokenFromSession();
            if (string.IsNullOrEmpty(token))
            {
                TempData["Error"] = "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.";
                return RedirectToAction("Login", "Auth");
            }

            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await _http.GetAsync($"/api/profile/public/{userId}");
            if (!response.IsSuccessStatusCode)
                return RedirectToAction(nameof(Index));

            var content = await response.Content.ReadAsStringAsync();
            var apiResult = JsonSerializer.Deserialize<ApiResultDto>(content, JsonOpts);
            if (apiResult == null || !apiResult.Success)
                return RedirectToAction(nameof(Index));

            var profile = DeserializeApiData<PersonalProfileViewModel>(apiResult) ?? BuildProfileFromClaims();

            profile.AvatarUrl = NormalizeAvatarUrl(profile.AvatarUrl);
            profile.IsReadOnlyView = true;

            // Load reviews nếu là nanny
            if (profile.IsNanny)
                await LoadNannyReviewsAsync(profile, userId);

            var hasHiringContext =
                User.IsInRole("Parent")
                && profile.IsNanny
                && jobPostingId.HasValue
                && jobApplicationId.HasValue;
            var sourceKey = (source ?? string.Empty).Trim().ToLowerInvariant();
            var suppressEngagementActions = sourceKey is "history" or "contact_request" or "contactrequest";
            var resolvedNannyProfileId = nannyProfileId.HasValue && nannyProfileId.Value != Guid.Empty
                ? nannyProfileId.Value
                : (profile.NannyProfileId ?? Guid.Empty);
            if (resolvedNannyProfileId != Guid.Empty)
                profile.ContactNannyProfileId = resolvedNannyProfileId;

            var isContactAccepted = false;
            var isContactRequestPending = false;
            Guid? resolvedContactRequestId = contactRequestId;
            if (User.IsInRole("Parent")
                && profile.IsNanny
                && !hasHiringContext
                && profile.ContactNannyProfileId.HasValue
                && profile.ContactNannyProfileId.Value != Guid.Empty)
            {
                var (contactRequestStatus, foundRequestId) = await GetContactRequestStateAsync(profile.ContactNannyProfileId.Value);
                isContactAccepted = contactRequestStatus == 1;
                isContactRequestPending = contactRequestStatus == 0;
                resolvedContactRequestId ??= foundRequestId;
            }

            ViewBag.HasHiringContext = hasHiringContext;
            ViewBag.IsContactAccepted = isContactAccepted;
            ViewBag.IsContactRequestPending = isContactRequestPending;
            ViewBag.HiringJobPostingId = hasHiringContext ? jobPostingId!.Value.ToString() : "";
            ViewBag.HiringJobApplicationId = hasHiringContext ? jobApplicationId!.Value.ToString() : "";
            ViewBag.ContactRequestId = resolvedContactRequestId?.ToString() ?? "";
            ViewBag.SuppressEngagementActions = suppressEngagementActions;

            return View("Index", profile);
        }
        catch
        {
            return RedirectToAction(nameof(Index));
        }
    }

    private async Task LoadNannyReviewsAsync(PersonalProfileViewModel profile, Guid nannyUserId)
    {
        try
        {
            var resp = await _http.GetAsync($"/api/reviews/nanny/{nannyUserId}?page=1&pageSize=10");
            if (!resp.IsSuccessStatusCode) return;

            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("data", out var data)) return;

            if (data.TryGetProperty("items", out var items))
                profile.Reviews = JsonSerializer.Deserialize<List<ReviewItemViewModel>>(
                    items.GetRawText(), JsonOpts) ?? [];

            if (data.TryGetProperty("totalCount", out var totalEl) && totalEl.TryGetInt32(out var total))
                profile.ReviewTotalCount = total;
        }
        catch
        {
            // silent â€” review failure should not block profile load
        }
    }

    private async Task<(int? Status, Guid? ContactRequestId)> GetContactRequestStateAsync(Guid nannyProfileId)
    {
        var response = await _http.GetAsync("/api/nannies/contact-requests/sent");
        if (!response.IsSuccessStatusCode)
            return (null, null);

        var json = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(json))
            return (null, null);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (!root.TryGetProperty("success", out var successEl) || successEl.ValueKind != JsonValueKind.True)
            return (null, null);
        if (!root.TryGetProperty("data", out var dataEl) || dataEl.ValueKind != JsonValueKind.Object)
            return (null, null);
        if (!dataEl.TryGetProperty("requests", out var requestsEl) || requestsEl.ValueKind != JsonValueKind.Array)
            return (null, null);

        foreach (var requestEl in requestsEl.EnumerateArray())
        {
            if (!requestEl.TryGetProperty("nanny", out var nannyEl) || nannyEl.ValueKind != JsonValueKind.Object)
                continue;
            if (!nannyEl.TryGetProperty("profileId", out var profileIdEl) || profileIdEl.ValueKind != JsonValueKind.String)
                continue;

            var profileIdStr = profileIdEl.GetString();
            if (!Guid.TryParse(profileIdStr, out var parsedProfileId))
                continue;
            if (parsedProfileId != nannyProfileId)
                continue;

            if (!requestEl.TryGetProperty("status", out var statusEl) || statusEl.ValueKind != JsonValueKind.Number)
                return (null, null);

            if (!statusEl.TryGetInt32(out var status))
                return (null, null);

            Guid? requestId = null;
            if (requestEl.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String)
                if (Guid.TryParse(idEl.GetString(), out var parsedId))
                    requestId = parsedId;

            return (status, requestId);
        }

        return (null, null);
    }

    // Edit personal information
    [HttpGet]
    public async Task<IActionResult> Edit()
    {
        try
        {
            var token = GetTokenFromSession();
            if (string.IsNullOrEmpty(token))
            {
                TempData["Error"] = "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.";
                return RedirectToAction("Login", "Auth");
            }
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _http.GetAsync("/api/profile");
            if (!response.IsSuccessStatusCode)
                return RedirectToAction("đăng nhập", "Auth");

            var content = await response.Content.ReadAsStringAsync();
            var apiResult = JsonSerializer.Deserialize<ApiResultDto>(content, JsonOpts);
            EditPersonalInfoViewModel? profile = null;
            JsonElement? rawProfileElement = null;

            if (apiResult?.Success == true && apiResult.Data is JsonElement profileData && profileData.ValueKind == JsonValueKind.Object)
            {
                rawProfileElement = profileData;
                profile = JsonSerializer.Deserialize<EditPersonalInfoViewModel>(profileData.GetRawText(), JsonOpts);
            }
            else if (apiResult?.Success == true && apiResult.Data != null)
            {
                profile = JsonSerializer.Deserialize<EditPersonalInfoViewModel>(
                    JsonSerializer.Serialize(apiResult.Data), JsonOpts);
            }

            // Fallback: some API payloads still return avatar in nested/alternate keys.
            if (profile != null && string.IsNullOrWhiteSpace(profile.AvatarUrl) && rawProfileElement.HasValue)
            {
                profile.AvatarUrl = ExtractStringFromJson(
                    rawProfileElement.Value,
                    "avatarUrl",
                    "avatar",
                    "avatarPath",
                    "avatar_url");
            }

            if (profile != null)
                profile.AvatarUrl = NormalizeAvatarUrl(profile.AvatarUrl);

            var vm = profile ?? BuildEditProfileFromClaims();
            if (vm.Roles.Count == 0)
                ApplyRolesToEditModel(vm);

            if (apiResult?.Data is JsonElement root &&
                root.TryGetProperty("skills", out var skillsElement) &&
                skillsElement.ValueKind == JsonValueKind.Array)
            {
                vm.SelectedSkillIds = skillsElement
                    .EnumerateArray()
                    .Select(s => s.TryGetProperty("skillId", out var idEl) ? idEl.GetGuid() : Guid.Empty)
                    .Where(id => id != Guid.Empty)
                    .ToList();
            }
            await PopulateAvailableSkillsAsync(vm);
            return View(vm);
        }
        catch (Exception ex)
        {
            TempData["Error"] = "Lỗi tải thông tin: " + ex.Message;
            return RedirectToAction("Index");
        }
    }

[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Edit(EditPersonalInfoViewModel model)
{
    ApplyRolesToEditModel(model);

    if (model.IsNanny)
    {
        foreach (var error in SalaryValidationRules.Validate(
                     model.ExpectedSalaryMin,
                     model.ExpectedSalaryMax,
                     nameof(model.ExpectedSalaryMin),
                     nameof(model.ExpectedSalaryMax)))
        {
            var memberNames = error.MemberNames?.Any() == true
                ? error.MemberNames
                : new[] { string.Empty };

            foreach (var memberName in memberNames)
                ModelState.AddModelError(memberName, error.ErrorMessage ?? "Lương không hợp lệ.");
        }
    }

    if (model.AvatarFile != null && model.AvatarFile.Length > 0)
    {
        var ext = Path.GetExtension(model.AvatarFile.FileName)?.ToLowerInvariant();
        if (ext != ".jpg" && ext != ".jpeg" && ext != ".png")
            ModelState.AddModelError(nameof(model.AvatarFile), "Chỉ cho phép ảnh .jpg, .jpeg hoặc .png.");
    }

    var today = DateOnly.FromDateTime(DateTime.Today);
    if (model.DateOfBirth.HasValue && model.DateOfBirth.Value > today)
    {
        ModelState.AddModelError(nameof(model.DateOfBirth), "Ngày sinh không được lớn hơn ngày hiện tại.");
    }

    if (User.IsInRole("Nanny") && !model.DateOfBirth.HasValue)
    {
        ModelState.AddModelError(nameof(model.DateOfBirth), "Vui lòng chọn ngày sinh.");
    }
    else if (User.IsInRole("Nanny") && model.DateOfBirth.HasValue)
    {
        var age = today.Year - model.DateOfBirth.Value.Year;
        if (model.DateOfBirth.Value > today.AddYears(-age)) age--;
        if (age <= 30)
            ModelState.AddModelError(nameof(model.DateOfBirth), "Bảo mẫu phải lớn hơn 30 tuổi.");
    }

    if (!ModelState.IsValid)
    {
        var tokenForSkill = GetTokenFromSession();
        if (!string.IsNullOrEmpty(tokenForSkill))
        {
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenForSkill);
            await PopulateAvailableSkillsAsync(model);
        }
        return View(model);
    }

    try
    {
        var token = GetTokenFromSession();
        if (string.IsNullOrEmpty(token))
        {
            TempData["Error"] = "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.";
            return RedirectToAction("Login", "Auth");
        }

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        if (model.AvatarFile != null && model.AvatarFile.Length > 0)
        {
            try
            {
                model.AvatarUrl = await _blobStorageService.UploadUserAvatarAsync(model.AvatarFile);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(nameof(model.AvatarFile), $"Không thể tải ảnh đại diện lên: {ex.Message}");
                await PopulateAvailableSkillsAsync(model);
                return View(model);
            }
        }

        var updateRequest = new
        {
            model.FirstName,
            model.LastName,
            model.PhoneNumber,
            model.AvatarUrl,
            model.DateOfBirth,
            model.Gender,
            model.Address,
            model.City,
            model.District,
            model.Ward,
            model.Latitude,
            model.Longitude,
            model.Bio,
            model.YearsOfExperience,
            model.EducationLevel,
            model.ExpectedSalaryMin,
            model.ExpectedSalaryMax,
            model.MaxTravelDistance,
            SkillIds = model.SelectedSkillIds
        };

        var response = await _http.PutAsJsonAsync("/api/profile", updateRequest);
        var responseContent = await response.Content.ReadAsStringAsync();
        var apiResult = JsonSerializer.Deserialize<ApiResultDto>(responseContent, JsonOpts);

        if (apiResult == null || !apiResult.Success)
        {
            ModelState.AddModelError("", apiResult?.Message ?? "Cập nhật thất bại.");
            await PopulateAvailableSkillsAsync(model);
            return View(model);
        }

        await RefreshAuthClaimsAsync(model);
        TempData["Success"] = "Cập nhật thông tin thành công.";
        return RedirectToAction("Edit");
    }
    catch (Exception ex)
    {
        TempData["Error"] = "Lỗi khi cập nhật: " + ex.Message;
        await PopulateAvailableSkillsAsync(model);
        return View(model);
    }
}

    [HttpGet]
    public IActionResult Verify() =>
        RedirectToAction("NannyGetVerificationRequestList", "NannyVerificationRequest");

    [NonAction]
    public async Task<IActionResult> Verify(CreateCertificateViewModel model)
    {
        var token = GetTokenFromSession();
        if (string.IsNullOrEmpty(token))
            return RedirectToAction("Login", "Auth");

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _http.PostAsJsonAsync("/api/profile/certificates", new
        {
            model.Name,
            model.IssuingOrganization,
            model.CertificateUrl
        });

        var content = await response.Content.ReadAsStringAsync();
        var apiResult = JsonSerializer.Deserialize<ApiResultDto>(content, JsonOpts);
        if (apiResult == null || !apiResult.Success)
        {
            TempData["Error"] = apiResult?.Message ?? "Không thể thêm chứng chỉ.";
            return RedirectToAction(nameof(Verify));
        }

        TempData["Success"] = "Đã thêm chứng chỉ thành công.";
        return RedirectToAction(nameof(Index));
    }

    // View child profiles
    [HttpGet]
    public async Task<IActionResult> Children()
    {
        try
        {
            var token = GetTokenFromSession();
            if (string.IsNullOrEmpty(token))
            {
                TempData["Error"] = "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.";
                return RedirectToAction("Login", "Auth");
            }
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _http.GetAsync("/api/profile/children");
            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                TempData["Error"] = "Bạn không có quyền xem danh sách trẻ em.";
                return RedirectToAction("Index");
            }

            if (!response.IsSuccessStatusCode)
                return BadRequest();

            var content = await response.Content.ReadAsStringAsync();
            var apiResult = JsonSerializer.Deserialize<ApiResultDto>(content, JsonOpts);

            var children = new List<ChildProfileViewModel>();
            if (apiResult?.Data is System.Text.Json.JsonElement element && element.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                children = JsonSerializer.Deserialize<List<ChildProfileViewModel>>(element.GetRawText(), JsonOpts) ?? new();
            }

            // Display the real number of child profiles currently available.
            ViewBag.DisplayChildrenCount = children.Count;

            return View(children);
        }
        catch (Exception ex)
        {
            TempData["Error"] = "Lỗi khi tải danh sách trẻ em: " + ex.Message;
            return RedirectToAction("Index");
        }
    }

    // Add child profile
    [HttpGet]
    public IActionResult AddChild()
    {
        return View(new CreateChildProfileViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddChild(CreateChildProfileViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        try
        {
            var token = GetTokenFromSession();
            if (string.IsNullOrEmpty(token))
            {
                TempData["Error"] = "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.";
                return RedirectToAction("Login", "Auth");
            }
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _http.PostAsJsonAsync("/api/profile/children", model);
            var content = await response.Content.ReadAsStringAsync();
            var apiResult = JsonSerializer.Deserialize<ApiResultDto>(content, JsonOpts);

            if (apiResult == null || !apiResult.Success)
            {
                ModelState.AddModelError("", apiResult?.Message ?? "Thêm trẻ em thất bại.");
                return View(model);
            }

            return RedirectToAction("Children", new
            {
                toastType = "success",
                toastMessage = "Thêm trẻ em thành công."
            });
        }
        catch (Exception ex)
        {
            TempData["Error"] = "Lỗi khi thêm trẻ em: " + ex.Message;
            return View(model);
        }
    }

    // Edit child profile
    [HttpGet]
    public async Task<IActionResult> EditChild(Guid childId)
    {
        try
        {
            var token = GetTokenFromSession();
            if (string.IsNullOrEmpty(token))
            {
                TempData["Error"] = "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.";
                return RedirectToAction("Login", "Auth");
            }
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _http.GetAsync("/api/profile/children");
            if (!response.IsSuccessStatusCode)
                return NotFound();

            var content = await response.Content.ReadAsStringAsync();
            var apiResult = JsonSerializer.Deserialize<ApiResultDto>(content, JsonOpts);

            var children = JsonSerializer.Deserialize<List<ChildProfileViewModel>>(
                JsonSerializer.Serialize(apiResult?.Data), JsonOpts) ?? new();

            var child = children.FirstOrDefault(c => c.Id == childId);
            if (child == null)
                return NotFound();

            var viewModel = new UpdateChildProfileViewModel
            {
                Id = child.Id,
                SpecialNeeds = child.SpecialNeeds,
                Notes = child.Notes,
                Characteristic = child.Characteristic,
                ChildAgeGroup = child.ChildAgeGroup
            };

            return View(viewModel);
        }
        catch (Exception ex)
        {
            return RedirectToAction("Children", new
            {
                toastType = "error",
                toastMessage = "Lỗi khi tải thông tin trẻ em: " + ex.Message
            });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditChild(UpdateChildProfileViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        try
        {
            var token = GetTokenFromSession();
            if (string.IsNullOrEmpty(token))
            {
                TempData["Error"] = "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.";
                return RedirectToAction("Login", "Auth");
            }
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var updateRequest = new
            {
                model.SpecialNeeds,
                model.Notes,
                model.Characteristic,
                model.ChildAgeGroup
            };

            var response = await _http.PutAsJsonAsync($"/api/profile/children/{model.Id}", updateRequest);
            var content = await response.Content.ReadAsStringAsync();
            var apiResult = JsonSerializer.Deserialize<ApiResultDto>(content, JsonOpts);

            if (apiResult == null || !apiResult.Success)
            {
                ModelState.AddModelError("", apiResult?.Message ?? "Cập nhật thất bại.");
                return View(model);
            }

            return RedirectToAction("Children", new
            {
                toastType = "success",
                toastMessage = "Cập nhật thông tin trẻ em thành công."
            });
        }
        catch (Exception ex)
        {
            TempData["Error"] = "Lỗi khi cập nhật: " + ex.Message;
            return View(model);
        }
    }

    // Delete child profile
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteChild(Guid childId)
    {
        try
        {
            var token = GetTokenFromSession();
            if (string.IsNullOrEmpty(token))
            {
                TempData["Error"] = "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.";
                return RedirectToAction("Login", "Auth");
            }
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _http.DeleteAsync($"/api/profile/children/{childId}");
            var content = await response.Content.ReadAsStringAsync();
            var apiResult = JsonSerializer.Deserialize<ApiResultDto>(content, JsonOpts);

            if (apiResult == null || !apiResult.Success)
            {
                return RedirectToAction("Children", new
                {
                    toastType = "error",
                    toastMessage = apiResult?.Message ?? "Xóa thất bại."
                });
            }

            return RedirectToAction("Children", new
            {
                toastType = "success",
                toastMessage = "Xóa trẻ em thành công."
            });
        }
        catch (Exception ex)
        {
            return RedirectToAction("Children", new
            {
                toastType = "error",
                toastMessage = "Lỗi khi xóa: " + ex.Message
            });
        }
    }
}

// View Models









public class ApiResultDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public object? Data { get; set; }
}

public class CreateCertificateViewModel
{
    public string Name { get; set; } = string.Empty;
    public string? IssuingOrganization { get; set; }
    public string? CertificateUrl { get; set; }
}
