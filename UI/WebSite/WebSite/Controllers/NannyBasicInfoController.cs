using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebSite.Models;
using WebSite.Models.Profile;

namespace WebSite.Controllers;

[Authorize(Roles = "Nanny")]
public class NannyBasicInfoController : Controller
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public NannyBasicInfoController(IHttpClientFactory httpFactory)
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

    private async Task<bool> SaveProfileAsync(NannyBasicInfoWizardViewModel model)
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
            FirstName = string.IsNullOrWhiteSpace(firstName) ? "Nanny" : firstName,
            LastName = string.IsNullOrWhiteSpace(lastName) ? "User" : lastName,
            PhoneNumber = (string?)null,
            AvatarUrl = (string?)null,
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
        var content = await response.Content.ReadAsStringAsync();
        var apiResult = JsonSerializer.Deserialize<ApiResultDto>(content, JsonOpts);
        return apiResult != null && apiResult.Success;
    }

    [HttpGet]
    public async Task<IActionResult> Step1Name()
    {
        var existing = await LoadCurrentProfileAsync();
        var vm = new NannyBasicInfoWizardViewModel();

        if (existing != null)
        {
            vm.FullName = $"{existing.FirstName} {existing.LastName}".Trim();
            vm.DateOfBirth = existing.DateOfBirth;
            vm.Gender = existing.Gender;
            vm.Address = existing.Address;
            vm.City = existing.City;
            vm.District = existing.District;
            vm.Ward = existing.Ward;
            vm.Latitude = existing.Latitude;
            vm.Longitude = existing.Longitude;
        }

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Step1Name(NannyBasicInfoWizardViewModel model, string? direction)
    {
        if (direction == "next" && string.IsNullOrWhiteSpace(model.FullName))
        {
            ModelState.AddModelError(nameof(model.FullName), "Vui lòng nhập họ tên.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        return View("Step2DateOfBirth", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Step2DateOfBirth(NannyBasicInfoWizardViewModel model, string? direction)
    {
        if (direction == "back")
        {
            return View("Step1Name", model);
        }

        if (direction == "next" && model.DateOfBirth == null)
        {
            ModelState.AddModelError(nameof(model.DateOfBirth), "Vui lòng chọn ngày sinh.");
        }

        if (!ModelState.IsValid)
        {
            return View("Step2DateOfBirth", model);
        }

        return View("Step3Gender", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Step3Gender(NannyBasicInfoWizardViewModel model, string? direction)
    {
        if (direction == "back")
        {
            return View("Step2DateOfBirth", model);
        }

        if (direction == "next" && model.Gender == null)
        {
            ModelState.AddModelError(nameof(model.Gender), "Vui lòng chọn giới tính.");
        }

        if (!ModelState.IsValid)
        {
            return View("Step3Gender", model);
        }

        return View("Step4Address", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Step4Address(NannyBasicInfoWizardViewModel model, string? direction)
    {
        if (direction == "back")
        {
            return View("Step3Gender", model);
        }

        if (direction == "next")
        {
            if (string.IsNullOrWhiteSpace(model.Address))
            {
                ModelState.AddModelError(nameof(model.Address), "Vui lòng nhập địa chỉ.");
            }

            if (!ModelState.IsValid)
            {
                return View("Step4Address", model);
            }

            var success = await SaveProfileAsync(model);
            if (!success)
            {
                ModelState.AddModelError(string.Empty, "Cập nhật thông tin thất bại. Vui lòng thử lại.");
                return View("Step4Address", model);
            }

            return RedirectToAction("Start", "Onboarding");
        }

        return View("Step4Address", model);
    }
}

