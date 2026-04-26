using System.Net.Http.Headers;
using System.Text.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebSite.Models;

namespace WebSite.Controllers;

[Authorize]
public class OnboardingController : Controller
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };
    private const string NannyOnboardingCompletedSessionKey = "NannyOnboardingCompleted";

    public OnboardingController(IHttpClientFactory httpFactory)
    {
        _http = httpFactory.CreateClient("BackendApi");
    }

    private string? GetToken() => HttpContext.Session.GetString("AccessToken");

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

    private async Task<OnboardingStatusViewModel?> GetStatusAsync()
    {
        var token = GetToken();
        if (string.IsNullOrEmpty(token)) return null;

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _http.GetAsync("/api/onboarding/status");
        if (!response.IsSuccessStatusCode) return null;

        var json = await response.Content.ReadAsStringAsync();
        var apiResult = TryDeserializeApiResult(json);
        if (apiResult?.Data is System.Text.Json.JsonElement element &&
            element.ValueKind == JsonValueKind.Object)
        {
            return JsonSerializer.Deserialize<OnboardingStatusViewModel>(element.GetRawText(), JsonOpts);
        }

        return null;
    }

    [HttpGet]
    public async Task<IActionResult> Start()
    {
        if (IsNannyOnboardingLocked())
            return RedirectToAction("Index", "Home");

        var status = await GetStatusAsync();
        if (status != null)
            SyncNannyOnboardingCompletedFlag(status);

        if (IsNannyOnboardingLocked())
            return RedirectToAction("Index", "Home");

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

    private static bool IsCompletedNannyOnboardingStatus(OnboardingStatusViewModel status) =>
        string.Equals(status.Role, "Nanny", StringComparison.OrdinalIgnoreCase) &&
        (!status.RequiresOnboarding || string.Equals(status.NextStep, "Completed", StringComparison.OrdinalIgnoreCase));

    private void SyncNannyOnboardingCompletedFlag(OnboardingStatusViewModel status)
    {
        if (IsCompletedNannyOnboardingStatus(status))
        {
            HttpContext.Session.SetString(NannyOnboardingCompletedSessionKey, "1");
            return;
        }

        if (string.Equals(status.Role, "Nanny", StringComparison.OrdinalIgnoreCase))
            HttpContext.Session.Remove(NannyOnboardingCompletedSessionKey);
    }

    private bool IsNannyOnboardingLocked() =>
        IsNannyRole() &&
        string.Equals(HttpContext.Session.GetString(NannyOnboardingCompletedSessionKey), "1", StringComparison.Ordinal);

    private bool IsNannyRole()
    {
        if (User.IsInRole("Nanny"))
            return true;

        return User.Claims.Any(c =>
            c.Type == ClaimTypes.Role &&
            string.Equals(c.Value, "Nanny", StringComparison.OrdinalIgnoreCase));
    }
}
