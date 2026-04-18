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

        // 1. Upload Avatar if present
        if (model.AvatarFile != null && model.AvatarFile.Length > 0)
        {
            using var content = new MultipartFormDataContent();
            var streamContent = new StreamContent(model.AvatarFile.OpenReadStream());
            streamContent.Headers.ContentType = new MediaTypeHeaderValue(model.AvatarFile.ContentType);
            content.Add(streamContent, "file", model.AvatarFile.FileName);

            var uploadRes = await _http.PostAsync("/api/profile/upload-avatar", content);
            if (uploadRes.IsSuccessStatusCode)
            {
                var uploadJson = await uploadRes.Content.ReadAsStringAsync();
                var uploadResult = JsonSerializer.Deserialize<ApiResultDto>(uploadJson, JsonOpts);
                if (uploadResult?.Success == true && uploadResult.Data != null)
                {
                    model.AvatarUrl = uploadResult.Data.ToString();
                }
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
        var resContent = await response.Content.ReadAsStringAsync();
        var apiResult = JsonSerializer.Deserialize<ApiResultDto>(resContent, JsonOpts);
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
            Characteristic = model.ChildCharacteristic,
            ChildAgeGroup = model.ChildAgeGroup,
            SpecialNeeds = model.ChildSpecialNeeds,
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
    public async Task<IActionResult> Step1BasicInfo()
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
    public async Task<IActionResult> Step1BasicInfo(ParentOnboardingWizardViewModel model, string? direction)
    {
        if (direction == "next")
        {
            if (string.IsNullOrWhiteSpace(model.FullName))
                ModelState.AddModelError(nameof(model.FullName), "Vui lòng nhập họ tên.");

            if (string.IsNullOrWhiteSpace(model.Address))
                ModelState.AddModelError(nameof(model.Address), "Vui lòng nhập địa chỉ chi tiết.");

            if (string.IsNullOrWhiteSpace(model.City) || string.IsNullOrWhiteSpace(model.District))
                ModelState.AddModelError(string.Empty, "Vui lòng chọn đầy đủ Tỉnh/Thành và Phường/Xã.");

            if (!ModelState.IsValid)
                return View(model);

            var success = await SaveBasicUserInfoAsync(model);
            if (!success)
            {
                ModelState.AddModelError(string.Empty, "Lưu thông tin thất bại. Vui lòng thử lại.");
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
                ModelState.AddModelError(nameof(model.FamilyDescription), "Vui lòng mô tả gia đình.");

            if (!model.NumberOfChildren.HasValue || model.NumberOfChildren < 1)
                ModelState.AddModelError(nameof(model.NumberOfChildren), "Vui lòng nhập số lượng con.");

            if (!model.ChildAgeGroup.HasValue)
                ModelState.AddModelError(nameof(model.ChildAgeGroup), "Vui lòng chọn nhóm tuổi của trẻ.");

            if (!ModelState.IsValid)
                return View(model);

            // Lưu Parent Profile (FamilyDescription & NumberOfChildren)
            var parentSuccess = await SaveParentProfileAsync(model);
            if (!parentSuccess)
            {
                ModelState.AddModelError(string.Empty, "Lưu thông tin gia đình thất bại.");
                return View(model);
            }

            // Gọi API thêm Child Profile đầu tiên
            var childSuccess = await CreateChildAsync(model);
            if (!childSuccess)
            {
                ModelState.AddModelError(string.Empty, "Tao ho so con that bai. Vui long kiem tra thong tin va thu lai.");
                return View(model);
            }

            return RedirectToAction("Index", "Home");
        }

        return View(model);
    }
}

