using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebSite.Models;
using WebSite.Models.Profile;

namespace WebSite.Controllers;

[Authorize(Roles = "Parent")]
public class ParentOnboardingController : Controller
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public ParentOnboardingController(IHttpClientFactory httpFactory)
    {
        _http = httpFactory.CreateClient("BackendApi");
    }

    private string? GetToken() => HttpContext.Session.GetString("AccessToken");

    private void SetAuthHeader()
    {
        var token = GetToken();
        if (!string.IsNullOrEmpty(token))
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
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

    private async Task<bool> SaveBasicUserInfoAsync(ParentOnboardingWizardViewModel model)
    {
        SetAuthHeader();

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
            PhoneNumber = (string?)null,
            AvatarUrl = model.AvatarUrl,
            DateOfBirth = (System.DateOnly?)null,
            Gender = (int?)null,
            model.Address,
            model.City,
            model.District,
            model.Ward,
            model.Latitude,
            model.Longitude
        };

        var response = await _http.PutAsJsonAsync("/api/profile", updateRequest);
        var content = await response.Content.ReadAsStringAsync();
        var apiResult = JsonSerializer.Deserialize<ApiResultDto>(content, JsonOpts);
        return apiResult != null && apiResult.Success;
    }

    private async Task<bool> SaveParentProfileAsync(ParentOnboardingWizardViewModel model)
    {
        SetAuthHeader();
        var payload = new
        {
            model.FamilyDescription,
            model.NumberOfChildren
        };

        var response = await _http.PutAsJsonAsync("/api/onboarding/parent/profile", payload);
        var content = await response.Content.ReadAsStringAsync();
        var apiResult = JsonSerializer.Deserialize<ApiResultDto>(content, JsonOpts);
        return apiResult != null && apiResult.Success;
    }

    private async Task<bool> CreateChildAsync(ParentOnboardingWizardViewModel model)
    {
        SetAuthHeader();
        var payload = new
        {
            Name = (string?)null,
            DateOfBirth = model.ChildDateOfBirth,
            Gender = (int?)null,
            SpecialNeeds = model.ChildSpecialNeeds,
            Allergies = model.ChildAllergies,
            Notes = model.ChildNotes
        };

        // Tận dụng endpoint tạo con đã hoạt động trong ProfileController
        var response = await _http.PostAsJsonAsync("/api/profile/children", payload);

        // Nếu backend trả lỗi (500 HTML, 400 validation, v.v.) thì không cố Deserialize JSON
        if (!response.IsSuccessStatusCode)
        {
            return false;
        }

        var content = await response.Content.ReadAsStringAsync();

        try
        {
            var apiResult = JsonSerializer.Deserialize<ApiResultDto>(content, JsonOpts);
            return apiResult != null && apiResult.Success;
        }
        catch
        {
            // Response không phải JSON đúng định dạng ApiResultDto
            return false;
        }
    }

    [HttpGet]
    public async Task<IActionResult> Step1Name()
    {
        var existing = await LoadCurrentProfileAsync();
        var vm = new ParentOnboardingWizardViewModel();

        if (existing != null)
        {
            vm.FullName = $"{existing.FirstName} {existing.LastName}".Trim();
            vm.Address = existing.Address;
            vm.City = existing.City;
            vm.District = existing.District;
            vm.Ward = existing.Ward;
            vm.Latitude = existing.Latitude;
            vm.Longitude = existing.Longitude;
            vm.AvatarUrl = existing.AvatarUrl;
        }

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Step1Name(ParentOnboardingWizardViewModel model, string? direction)
    {
        if (direction == "next" && string.IsNullOrWhiteSpace(model.FullName))
        {
            ModelState.AddModelError(nameof(model.FullName), "Vui lòng nhập họ tên.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        return View("Step2Address", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Step2Address(ParentOnboardingWizardViewModel model, string? direction)
    {
        if (direction == "back")
        {
            return View("Step1Name", model);
        }

        if (direction == "next" && string.IsNullOrWhiteSpace(model.Address))
        {
            ModelState.AddModelError(nameof(model.Address), "Vui lòng nhập địa chỉ.");
        }

        if (!ModelState.IsValid)
        {
            return View("Step2Address", model);
        }

        return View("Step3Avatar", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Step3Avatar(ParentOnboardingWizardViewModel model, string? direction)
    {
        if (direction == "back")
        {
            return View("Step2Address", model);
        }

        if (direction == "next")
        {
            var success = await SaveBasicUserInfoAsync(model);
            if (!success)
            {
                ModelState.AddModelError(string.Empty, "Cập nhật thông tin cơ bản thất bại. Vui lòng thử lại.");
                return View("Step3Avatar", model);
            }

            return View("Step4Family", model);
        }

        return View("Step3Avatar", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Step4Family(ParentOnboardingWizardViewModel model, string? direction)
    {
        if (direction == "back")
        {
            return View("Step3Avatar", model);
        }

        if (direction == "next")
        {
            if (string.IsNullOrWhiteSpace(model.FamilyDescription))
            {
                ModelState.AddModelError(nameof(model.FamilyDescription), "Vui lòng mô tả gia đình.");
            }
            if (model.NumberOfChildren == null || model.NumberOfChildren <= 0)
            {
                ModelState.AddModelError(nameof(model.NumberOfChildren), "Vui lòng nhập số lượng con hợp lệ.");
            }

            if (!ModelState.IsValid)
            {
                return View("Step4Family", model);
            }

            var success = await SaveParentProfileAsync(model);
            if (!success)
            {
                ModelState.AddModelError(string.Empty, "Cập nhật hồ sơ parent thất bại. Vui lòng thử lại.");
                return View("Step4Family", model);
            }

            return View("Step5Child", model);
        }

        return View("Step4Family", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Step5Child(ParentOnboardingWizardViewModel model, string? direction)
    {
        if (direction == "back")
        {
            return View("Step4Family", model);
        }

        if (direction == "next")
        {
            if (model.ChildDateOfBirth == null)
            {
                ModelState.AddModelError(nameof(model.ChildDateOfBirth), "Vui lòng chọn ngày sinh của con.");
            }

            if (!ModelState.IsValid)
            {
                return View("Step5Child", model);
            }

            var success = await CreateChildAsync(model);
            if (!success)
            {
                ModelState.AddModelError(string.Empty, "Tạo hồ sơ con thất bại. Vui lòng thử lại.");
                return View("Step5Child", model);
            }

            // Sau khi tạo child profile thành công, quay về trang chủ
            return RedirectToAction("Index", "Home");
        }

        return View("Step5Child", model);
    }
}

