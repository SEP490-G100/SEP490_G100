using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebSite.Models;
using WebSite.Models.Nanny;

namespace WebSite.Controllers;

[Authorize]
public class NannyController : Controller
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public NannyController(IHttpClientFactory httpFactory)
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

    [HttpGet]
    public IActionResult Profile() => View(new NannyProfileViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(NannyProfileViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        SetAuthHeader();
        var response = await _http.PutAsJsonAsync("/api/onboarding/nanny/profile", new
        {
            model.Bio,
            model.YearsOfExperience,
            model.EducationLevel,
            model.ExpectedSalaryMin,
            model.ExpectedSalaryMax,
            model.MaxTravelDistance
        });

        var json = await response.Content.ReadAsStringAsync();
        var apiResult = JsonSerializer.Deserialize<ApiResultDto>(json, JsonOpts);
        if (apiResult == null || !apiResult.Success)
        {
            ModelState.AddModelError("", apiResult?.Message ?? "Cập nhật thất bại.");
            return View(model);
        }

        return RedirectToAction("Start", "Onboarding");
    }

    [HttpGet]
    public async Task<IActionResult> Skills()
    {
        SetAuthHeader();
        var vm = new NannySkillsViewModel();

        var response = await _http.GetAsync("/api/onboarding/skills");
        if (response.IsSuccessStatusCode)
        {
            var json = await response.Content.ReadAsStringAsync();
            var apiResult = JsonSerializer.Deserialize<ApiResultDto>(json, JsonOpts);

            if (apiResult?.Data is System.Text.Json.JsonElement element &&
                element.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                var skills = JsonSerializer.Deserialize<List<NannySkillSelectionViewModel>>(
                    element.GetRawText(), JsonOpts) ?? new();
                vm.AvailableSkills = skills;
            }
        }

        vm.SelectedSkills.Add(new NannySkillSelectionViewModel());
        return View(vm);
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Skills(NannySkillsViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        SetAuthHeader();
        var payload = new
        {
            Skills = model.SelectedSkills.Select(s => new { SkillId = s.SkillId, ProficiencyLevel = s.ProficiencyLevel }).ToList()
        };

        var response = await _http.PutAsJsonAsync("/api/onboarding/nanny/skills", payload);
        var json = await response.Content.ReadAsStringAsync();
        var apiResult = JsonSerializer.Deserialize<ApiResultDto>(json, JsonOpts);
        if (apiResult == null || !apiResult.Success)
        {
            ModelState.AddModelError("", apiResult?.Message ?? "Cập nhật thất bại.");
            return View(model);
        }

        return RedirectToAction("Start", "Onboarding");
    }

    [HttpGet]
    public IActionResult Availability() => View(new NannyAvailabilityViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Availability(NannyAvailabilityViewModel model)
    {
        SetAuthHeader();
        var payload = new
        {
            Days = model.Days.Select(d => new
            {
                d.DayOfWeek,
                d.Morning,
                d.Afternoon,
                d.Evening,
                d.Night
            }).ToList()
        };

        var response = await _http.PutAsJsonAsync("/api/onboarding/nanny/availability", payload);
        var json = await response.Content.ReadAsStringAsync();
        var apiResult = JsonSerializer.Deserialize<ApiResultDto>(json, JsonOpts);
        if (apiResult == null || !apiResult.Success)
        {
            ModelState.AddModelError("", apiResult?.Message ?? "Cập nhật thất bại.");
            return View(model);
        }

        return RedirectToAction("Start", "Onboarding");
    }
}

