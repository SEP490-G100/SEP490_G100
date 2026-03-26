using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebSite.Controllers;

/// <summary>
/// Proxy gợi ý địa chỉ (gọi Backend API) để trình duyệt gọi cùng origin.
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
    /// Gợi ý địa điểm tại Việt Nam. Trả về JSON: [{ displayName, latitude, longitude, city, district, ward }].
    /// </summary>
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
