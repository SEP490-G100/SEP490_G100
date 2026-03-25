using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebSite.Models;

namespace WebSite.Controllers;

[Authorize]
public class OnboardingController : Controller
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public OnboardingController(IHttpClientFactory httpFactory)
    {
        _http = httpFactory.CreateClient("BackendApi");
    }

    private string? GetToken() => HttpContext.Session.GetString("AccessToken");

    private async Task<OnboardingStatusViewModel?> GetStatusAsync()
    {
        var token = GetToken();
        if (string.IsNullOrEmpty(token)) return null;

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _http.GetAsync("/api/onboarding/status");
        if (!response.IsSuccessStatusCode) return null;

        var json = await response.Content.ReadAsStringAsync();
        var apiResult = JsonSerializer.Deserialize<ApiResultDto>(json, JsonOpts);
        if (apiResult?.Data is System.Text.Json.JsonElement element)
        {
            return JsonSerializer.Deserialize<OnboardingStatusViewModel>(element.GetRawText(), JsonOpts);
        }

        return null;
    }

    [HttpGet]
    public async Task<IActionResult> Start()
    {
        var status = await GetStatusAsync();
        if (status == null || !status.RequiresOnboarding || status.NextStep == "Completed")
            return RedirectToAction("Index", "Home");

        // Defensive: nếu backend trả NextStep theo Parent/Nanny nhưng role chưa được chọn,
        // ép user quay về màn chọn role.
        if (string.IsNullOrWhiteSpace(status.Role) ||
            status.Role.Equals("User", StringComparison.OrdinalIgnoreCase))
        {
            return RedirectToAction("ChooseRole", "Auth");
        }

        return status.NextStep switch
        {
            "SelectRole" => RedirectToAction("ChooseRole", "Auth"),
            "ParentBasicInfo" => RedirectToAction("Step1BasicInfo", "ParentOnboarding"),
            "ParentFamily" => RedirectToAction("Step2Family", "ParentOnboarding"),
            "ParentChildren" => RedirectToAction("Children", "Profile"),
            "NannyBasicInfo" => RedirectToAction("Step1BasicInfo", "NannyBasicInfo"),
            "NannyProfile" => RedirectToAction("Profile", "Nanny"),
            "NannySkills" => RedirectToAction("Skills", "Nanny"),
            "NannyAvailability" => RedirectToAction("Availability", "Nanny"),
            _ => RedirectToAction("Index", "Home")
        };
    }
}

