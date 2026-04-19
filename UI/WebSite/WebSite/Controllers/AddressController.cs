using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebSite.Controllers;

/// <summary>
/// Proxy gá»£i Ã½ Ä‘á»‹a chá»‰ (gá»i Backend API) Ä‘á»ƒ trÃ¬nh duyá»‡t gá»i cÃ¹ng origin.
/// </summary>
[Authorize]
[Route("[controller]")]
public class AddressController : Controller
{
    private readonly HttpClient _http;

    public AddressController(IHttpClientFactory httpFactory)
    {
        _http = httpFactory.CreateClient("BackendApi");
    }

    private void SetAuthHeader()
    {
        var token = HttpContext.Session.GetString("AccessToken");
        if (!string.IsNullOrEmpty(token))
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    /// <summary>
    /// Gá»£i Ã½ Ä‘á»‹a Ä‘iá»ƒm táº¡i Viá»‡t Nam. Tráº£ vá» JSON: [{ displayName, latitude, longitude, city, district, ward }].
    /// </summary>
    [AllowAnonymous]
    [HttpGet("Suggest")]
    public async Task<IActionResult> Suggest([FromQuery] string? q, [FromQuery] int limit = 8)
    {
        SetAuthHeader();
        var query = string.IsNullOrEmpty(q) ? "" : Uri.EscapeDataString(q);
        var response = await _http.GetAsync($"/api/address/suggest?q={query}&limit={limit}");
        if (!response.IsSuccessStatusCode)
            return Json(Array.Empty<object>());
        var json = await response.Content.ReadAsStringAsync();
        return Content(json, "application/json");
    }

    [HttpGet("Districts")]
    [AllowAnonymous]
    public async Task<IActionResult> Districts([FromQuery] string? city)
    {
        if (string.IsNullOrWhiteSpace(city))
            return Json(Array.Empty<string>());

        SetAuthHeader();
        try
        {
            var response = await _http.GetAsync("/api/address/location-tree");
            if (!response.IsSuccessStatusCode)
                return Json(Array.Empty<string>());

            var json = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(json);

            var targetKey = NormalizeAdministrativeName(city);
            var root = document.RootElement;
            var provinceArray = root.ValueKind == JsonValueKind.Array
                ? root
                : (root.TryGetProperty("data", out var dataEl) && dataEl.ValueKind == JsonValueKind.Array ? dataEl : default);

            if (provinceArray.ValueKind != JsonValueKind.Array)
                return Json(Array.Empty<string>());

            foreach (var province in provinceArray.EnumerateArray())
            {
                var provinceName = province.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
                if (NormalizeAdministrativeName(provinceName) != targetKey)
                    continue;

                if (!province.TryGetProperty("districts", out var districtsProp) || districtsProp.ValueKind != JsonValueKind.Array)
                    return Json(Array.Empty<string>());

                var districts = districtsProp.EnumerateArray()
                    .Select(item => item.TryGetProperty("name", out var districtName) ? districtName.GetString() : null)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Cast<string>()
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                return Json(districts);
            }

            return Json(Array.Empty<string>());
        }
        catch
        {
            return Json(Array.Empty<string>());
        }
    }

    private static string NormalizeAdministrativeName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category != UnicodeCategory.NonSpacingMark)
                builder.Append(ch);
        }

        return builder.ToString()
            .ToLowerInvariant()
            .Replace("thanh pho ", string.Empty)
            .Replace("tp. ", string.Empty)
            .Replace("tp ", string.Empty)
            .Replace("tinh ", string.Empty)
            .Replace("thua thien hue", "hue")
            .Trim();
    }

    [AllowAnonymous]
    [HttpGet("LocationTree")]
    public async Task<IActionResult> LocationTree(CancellationToken cancellationToken)
    {
        SetAuthHeader();
        var response = await _http.GetAsync("/api/address/location-tree", cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        Response.StatusCode = (int)response.StatusCode;
        return Content(json, "application/json", System.Text.Encoding.UTF8);
    }
}

