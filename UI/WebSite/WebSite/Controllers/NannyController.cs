using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
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
        else
            _http.DefaultRequestHeaders.Authorization = null;
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> List()
    {
        var vm = new NannyBrowsePageViewModel();
        SetAuthHeader();

        try
        {
            var response = await _http.GetAsync("/api/onboarding/skills");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var apiResult = JsonSerializer.Deserialize<ApiResult<List<NannySkillOptionViewModel>>>(json, JsonOpts);
                vm.SkillOptions = apiResult?.Data ?? new();
            }
        }
        catch
        {
            vm.SkillOptions = new();
        }

        return View(vm);
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> BrowseData([FromQuery] NannySearchRequestViewModel request)
    {
        SetAuthHeader();

        var query = new Dictionary<string, string?>();
        AddQuery(query, "keyword", request.Keyword);
        AddQuery(query, "city", request.City);
        AddQuery(query, "district", request.District);
        AddQuery(query, "minAge", request.MinAge);
        AddQuery(query, "maxAge", request.MaxAge);
        AddQuery(query, "minExperience", request.MinExperience);
        AddQuery(query, "minExpectedSalary", request.MinExpectedSalary);
        AddQuery(query, "maxExpectedSalary", request.MaxExpectedSalary);
        AddQuery(query, "verificationStatus", request.VerificationStatus);
        AddQuery(query, "dayOfWeek", request.DayOfWeek);
        AddQuery(query, "timeSlot", request.TimeSlot);
        AddQuery(query, "skillIds", request.SkillIds);
        AddQuery(query, "page", request.Page);
        AddQuery(query, "pageSize", request.PageSize);

        var url = QueryHelpers.AddQueryString("/api/nannies", query!);
        var response = await _http.GetAsync(url);
        var json = await response.Content.ReadAsStringAsync();
        return Content(json, "application/json");
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> DetailData(Guid id)
    {
        SetAuthHeader();
        var response = await _http.GetAsync($"/api/nannies/{id}");
        var json = await response.Content.ReadAsStringAsync();
        return Content(json, "application/json");
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

    private static void AddQuery<T>(IDictionary<string, string?> query, string key, T? value)
    {
        if (value == null) return;

        var stringValue = value switch
        {
            string s when string.IsNullOrWhiteSpace(s) => null,
            string s => s,
            _ => Convert.ToString(value)
        };

        if (!string.IsNullOrWhiteSpace(stringValue))
            query[key] = stringValue;
    }
}
