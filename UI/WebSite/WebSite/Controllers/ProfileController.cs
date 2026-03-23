using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebSite.Models.Profile;

namespace WebSite.Controllers;

[Authorize]
public class ProfileController : Controller
{
    private readonly HttpClient _http;
    private readonly string _apiBaseUrl;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public ProfileController(IHttpClientFactory httpFactory, IConfiguration config)
    {
        _http = httpFactory.CreateClient("BackendApi");
        _apiBaseUrl = (config["ApiSettings:BaseUrl"] ?? "").TrimEnd('/');
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
        if (url.StartsWith("/") && !string.IsNullOrWhiteSpace(_apiBaseUrl))
            return _apiBaseUrl + url;
        return url;
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
                ViewBag.Warning = "PhiÃªn Ä‘Äƒng nháº­p Ä‘Ã£ háº¿t háº¡n, Ä‘ang hiá»ƒn thá»‹ thÃ´ng tin cÆ¡ báº£n tá»« cookie.";
                return View(BuildProfileFromClaims());
            }

            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _http.GetAsync("/api/profile");
            if (!response.IsSuccessStatusCode)
            {
                ViewBag.Warning = "Could not load profile from API, showing basic info from cookie.";
                return View(BuildProfileFromClaims());
            }

            var content = await response.Content.ReadAsStringAsync();
            var apiResult = JsonSerializer.Deserialize<ApiResultDto>(content, JsonOpts);

            if (apiResult == null || !apiResult.Success)
            {
                ViewBag.Warning = "Could not load full profile, showing basic info from cookie.";
                return View(BuildProfileFromClaims());
            }

            var profile = JsonSerializer.Deserialize<PersonalProfileViewModel>(
                JsonSerializer.Serialize(apiResult.Data), JsonOpts);

            if (profile != null)
                profile.AvatarUrl = NormalizeAvatarUrl(profile.AvatarUrl);

            return View(profile ?? BuildProfileFromClaims());
        }
        catch (Exception ex)
        {
            TempData["Error"] = "Error loading profile: " + ex.Message;
            return View(BuildProfileFromClaims());
        }
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
                TempData["Error"] = "Session expired. Please log in again.";
                return RedirectToAction("Login", "Auth");
            }
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _http.GetAsync("/api/profile");
            if (!response.IsSuccessStatusCode)
                return RedirectToAction("Login", "Auth");

            var content = await response.Content.ReadAsStringAsync();
            var apiResult = JsonSerializer.Deserialize<ApiResultDto>(content, JsonOpts);

            var profile = JsonSerializer.Deserialize<EditPersonalInfoViewModel>(
                JsonSerializer.Serialize(apiResult?.Data), JsonOpts);

            if (profile != null)
                profile.AvatarUrl = NormalizeAvatarUrl(profile.AvatarUrl);

            var vm = profile ?? BuildEditProfileFromClaims();
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
            TempData["Error"] = "Error loading profile: " + ex.Message;
            return RedirectToAction("Index");
        }
    }

[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Edit(EditPersonalInfoViewModel model)
{
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
                TempData["Error"] = "Session expired. Please log in again.";
                return RedirectToAction("Login", "Auth");
            }
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Upload Avatar if present (same behavior as onboarding step)
            if (model.AvatarFile != null && model.AvatarFile.Length > 0)
            {
                using var form = new MultipartFormDataContent();
                var streamContent = new StreamContent(model.AvatarFile.OpenReadStream());
                streamContent.Headers.ContentType = new MediaTypeHeaderValue(model.AvatarFile.ContentType);
                form.Add(streamContent, "file", model.AvatarFile.FileName);

                var uploadRes = await _http.PostAsync("/api/profile/upload-avatar", form);
                if (uploadRes.IsSuccessStatusCode)
                {
                    var uploadJson = await uploadRes.Content.ReadAsStringAsync();
                    var uploadResult = JsonSerializer.Deserialize<ApiResultDto>(uploadJson, JsonOpts);
                    if (uploadResult?.Success == true && uploadResult.Data != null)
                        model.AvatarUrl = uploadResult.Data.ToString();
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
            var content = await response.Content.ReadAsStringAsync();
            var apiResult = JsonSerializer.Deserialize<ApiResultDto>(content, JsonOpts);

            if (apiResult == null || !apiResult.Success)
            {
                ModelState.AddModelError("", apiResult?.Message ?? "Cáº­p nháº­t tháº¥t báº¡i.");
                return View(model);
            }

            TempData["Success"] = "Cáº­p nháº­t thÃ´ng tin cÃ¡ nhÃ¢n thÃ nh cÃ´ng.";
            return RedirectToAction("Index");
        }
        catch (Exception ex)
        {
            TempData["Error"] = "Lá»—i khi cáº­p nháº­t: " + ex.Message;
            return View(model);
        }
    }

    [HttpGet]
    public IActionResult Verify() => Redirect("/verification");

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
                TempData["Error"] = "Session expired. Please log in again.";
                return RedirectToAction("Login", "Auth");
            }
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _http.GetAsync("/api/profile/children");
            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                TempData["Error"] = "Báº¡n khÃ´ng cÃ³ quyá»n xem danh sÃ¡ch con em.";
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

            return View(children);
        }
        catch (Exception ex)
        {
            TempData["Error"] = "Lá»—i khi táº£i danh sÃ¡ch con em: " + ex.Message;
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
                TempData["Error"] = "Session expired. Please log in again.";
                return RedirectToAction("Login", "Auth");
            }
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _http.PostAsJsonAsync("/api/profile/children", model);
            var content = await response.Content.ReadAsStringAsync();
            var apiResult = JsonSerializer.Deserialize<ApiResultDto>(content, JsonOpts);

            if (apiResult == null || !apiResult.Success)
            {
                ModelState.AddModelError("", apiResult?.Message ?? "ThÃªm con em tháº¥t báº¡i.");
                return View(model);
            }

            TempData["Success"] = "ThÃªm con em thÃ nh cÃ´ng.";
            return RedirectToAction("Children");
        }
        catch (Exception ex)
        {
            TempData["Error"] = "Lá»—i khi thÃªm con em: " + ex.Message;
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
                TempData["Error"] = "Session expired. Please log in again.";
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
            TempData["Error"] = "Lá»—i khi táº£i thÃ´ng tin con em: " + ex.Message;
            return RedirectToAction("Children");
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
                TempData["Error"] = "Session expired. Please log in again.";
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
                ModelState.AddModelError("", apiResult?.Message ?? "Cáº­p nháº­t tháº¥t báº¡i.");
                return View(model);
            }

            TempData["Success"] = "Cáº­p nháº­t thÃ´ng tin con em thÃ nh cÃ´ng.";
            return RedirectToAction("Children");
        }
        catch (Exception ex)
        {
            TempData["Error"] = "Lá»—i khi cáº­p nháº­t: " + ex.Message;
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
                TempData["Error"] = "Session expired. Please log in again.";
                return RedirectToAction("Login", "Auth");
            }
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _http.DeleteAsync($"/api/profile/children/{childId}");
            var content = await response.Content.ReadAsStringAsync();
            var apiResult = JsonSerializer.Deserialize<ApiResultDto>(content, JsonOpts);

            if (apiResult == null || !apiResult.Success)
            {
                TempData["Error"] = apiResult?.Message ?? "XÃ³a tháº¥t báº¡i.";
                return RedirectToAction("Children");
            }

            TempData["Success"] = "XÃ³a con em thÃ nh cÃ´ng.";
            return RedirectToAction("Children");
        }
        catch (Exception ex)
        {
            TempData["Error"] = "Lá»—i khi xÃ³a: " + ex.Message;
            return RedirectToAction("Children");
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
