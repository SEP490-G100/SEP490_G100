using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using WebSite.Models.Search;

namespace WebSite.Controllers;

/// <summary>
/// Proxy mọi request từ browser đến Backend API
/// GET  /Search          → View trang tìm kiếm
/// GET  /Search/Jobs     → proxy GET /api/search/jobs
/// POST /Search/CreateJob → proxy POST /api/job-postings
/// PUT  /Search/UpdateJob/{id} → proxy PUT /api/job-postings/{id}
/// DELETE /Search/DeleteJob/{id} → proxy DELETE /api/job-postings/{id}
/// </summary>
public class SearchController : Controller
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public SearchController(IHttpClientFactory httpFactory)
    {
        _http = httpFactory.CreateClient("BackendApi");
    }

    // ── GET /Search ─────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        ViewBag.SkillOptions = await getSkillOptionsForView();
        return View();
    }

    [HttpGet]
    public IActionResult History() => View();

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id)
    {
        ViewBag.JobId = id;
        ViewBag.SkillOptions = await getSkillOptionsForView();
        return View();
    }

    // ── GET /Search/Jobs ────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Jobs([FromQuery] SearchJobRequest req)
    {
        SetAuthHeader();
        try
        {
            var query = new List<string>();
            if (!string.IsNullOrWhiteSpace(req.City))     query.Add($"city={Uri.EscapeDataString(req.City)}");
            if (!string.IsNullOrWhiteSpace(req.District)) query.Add($"district={Uri.EscapeDataString(req.District)}");
            if (req.JobType.HasValue)   query.Add($"jobType={req.JobType}");
            if (req.SalaryMin.HasValue) query.Add($"salaryMin={req.SalaryMin}");
            if (req.MinLat.HasValue)    query.Add($"minLat={req.MinLat.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            if (req.MaxLat.HasValue)    query.Add($"maxLat={req.MaxLat.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            if (req.MinLng.HasValue)    query.Add($"minLng={req.MinLng.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            if (req.MaxLng.HasValue)    query.Add($"maxLng={req.MaxLng.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            query.Add($"page={req.Page}");
            query.Add($"pageSize={Math.Min(req.PageSize, 50)}");

            var response = await _http.GetAsync("/api/search/jobs?" + string.Join("&", query));
            if (!response.IsSuccessStatusCode)
                return Json(new { success = false, total = 0, data = Array.Empty<object>() });

            var json   = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<SearchApiResult>(json, JsonOpts);
            return Json(new { success = result?.Success ?? false, total = result?.Total ?? 0, data = result?.Data ?? [] });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, total = 0, data = Array.Empty<object>(), error = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> MyJobs()
    {
        SetAuthHeader();
        try
        {
            var response = await _http.GetAsync("/api/job-postings/my");
            if (!response.IsSuccessStatusCode)
                return Json(new { success = false, total = 0, data = Array.Empty<object>() });

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<SearchApiResult>(json, JsonOpts);
            return Json(new { success = result?.Success ?? false, total = result?.Total ?? 0, data = result?.Data ?? [] });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, total = 0, data = Array.Empty<object>(), error = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> Prefill()
    {
        SetAuthHeader();
        try
        {
            var response = await _http.GetAsync("/api/job-postings/prefill");
            if (!response.IsSuccessStatusCode)
                return Json(new { success = false, data = (object?)null });

            var json = await response.Content.ReadAsStringAsync();
            return Content(json, "application/json");
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> Skills()
    {
        var skills = await getSkillOptionsForView();
        return Json(new { success = skills.Count > 0, data = skills });
    }

    // ── POST /Search/CreateJob ──────────────────────────────
    [HttpPost]
    public async Task<IActionResult> CreateJob([FromBody] JsonElement body)
    {
        SetAuthHeader();
        try
        {
            var response = await _http.PostAsJsonAsync("/api/job-postings", body);
            return await ProxyApiResponse(response);
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    // ── PUT /Search/UpdateJob/{id} ──────────────────────────
    [HttpPut]
    public async Task<IActionResult> UpdateJob(Guid id, [FromBody] JsonElement body)
    {
        SetAuthHeader();
        try
        {
            var response = await _http.PutAsJsonAsync($"/api/job-postings/{id}", body);
            return await ProxyApiResponse(response);
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    // ── DELETE /Search/DeleteJob/{id} ───────────────────────
    [HttpDelete]
    public async Task<IActionResult> DeleteJob(Guid id)
    {
        SetAuthHeader();
        try
        {
            var response = await _http.DeleteAsync($"/api/job-postings/{id}");
            return await ProxyApiResponse(response);
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    // ── GET /Search/Detail/{id} ────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Detail(Guid id)
    {
        try
        {
            var response = await _http.GetAsync($"/api/job-postings/{id}");
            if (!response.IsSuccessStatusCode) return NotFound();
            var json = await response.Content.ReadAsStringAsync();
            return Content(json, "application/json");
        }
        catch { return StatusCode(500); }
    }

    // ── Helper: đính kèm JWT token từ session ──────────────
    private void SetAuthHeader()
    {
        var token = HttpContext.Session.GetString("AccessToken");
        if (!string.IsNullOrEmpty(token))
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        else
            _http.DefaultRequestHeaders.Authorization = null;
    }

    private static async Task<IActionResult> ProxyApiResponse(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();

        if (string.IsNullOrWhiteSpace(json))
            return new JsonResult(new
            {
                success = response.IsSuccessStatusCode,
                message = response.IsSuccessStatusCode ? "Thành công." : $"Lỗi HTTP {(int)response.StatusCode}."
            })
            { StatusCode = (int)response.StatusCode };

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            string? message = null;
            if (root.TryGetProperty("message", out var messageEl))
                message = messageEl.GetString();
            else if (root.TryGetProperty("title", out var titleEl))
                message = titleEl.GetString();

            object? errors = null;
            if (root.TryGetProperty("errors", out var errorsEl))
            {
                errors = JsonSerializer.Deserialize<object>(errorsEl.GetRawText());
                message ??= getFirstValidationError(errorsEl);
            }

            if (string.IsNullOrWhiteSpace(message))
                message = getFirstStringProperty(root, "detail");

            return new JsonResult(new
            {
                success = response.IsSuccessStatusCode && (!root.TryGetProperty("success", out var successEl) || successEl.GetBoolean()),
                message,
                errors,
                raw = JsonSerializer.Deserialize<object>(json)
            })
            { StatusCode = (int)response.StatusCode };
        }
        catch
        {
            return new JsonResult(new
            {
                success = response.IsSuccessStatusCode,
                message = json
            })
            { StatusCode = (int)response.StatusCode };
        }
    }

    private static string? getFirstValidationError(JsonElement errorsEl)
    {
        if (errorsEl.ValueKind != JsonValueKind.Object)
            return null;

        foreach (var property in errorsEl.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in property.Value.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                        return item.GetString();
                }
            }

            if (property.Value.ValueKind == JsonValueKind.String)
                return property.Value.GetString();
        }

        return null;
    }

    private static string? getFirstStringProperty(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
            return null;

        return property.GetString();
    }

    private async Task<List<JobSkillOption>> getSkillOptionsForView()
    {
        SetAuthHeader();
        try
        {
            var response = await _http.GetAsync("/api/onboarding/skills");
            if (!response.IsSuccessStatusCode)
                return [];

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("data", out var dataEl))
                return [];

            var skills = JsonSerializer.Deserialize<List<JobSkillOption>>(dataEl.GetRawText(), JsonOpts);
            return skills ?? [];
        }
        catch
        {
            return [];
        }
    }
}
