using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebSite.Models;

namespace WebSite.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public AdminController(IHttpClientFactory httpFactory)
    {
        _http = httpFactory.CreateClient("BackendApi");
    }
    [HttpGet("/Admin/ExportData")]
    public async Task<IActionResult> ExportData()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/Admin/export-system-data");
        AttachToken(request);
        try
        {
            var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                TempData["Error"] = $"Loi khi xuat du lieu: HTTP {(int)response.StatusCode}";
                return RedirectToAction("Dashboard", "AdminDashboard");
            }

            var stream = await response.Content.ReadAsStreamAsync();
            var contentType = response.Content.Headers.ContentType?.ToString()
                              ?? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
                           ?? response.Content.Headers.ContentDisposition?.FileName?.Replace("\"", "")
                           ?? $"NannyMatch_SystemData_{DateTime.Now:yyyyMMdd}.xlsx";

            return File(stream, contentType, fileName);
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Loi ket noi khi xuat Excel: {ex.Message}";
            return RedirectToAction("Dashboard", "AdminDashboard");
        }
    }

    // ── Recommendation Config ───────────────────────────

    public async Task<IActionResult> RecommendationConfig()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/recommendation/config/weights");
        AttachToken(request);
        try
        {
            var response = await _http.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResult<ScoringWeightsDto>>(json, JsonOpts);
            return View(result?.Data ?? new ScoringWeightsDto());
        }
        catch
        {
            TempData["Error"] = "Không thể tải cấu hình recommendation.";
            return View(new ScoringWeightsDto());
        }
    }

    [HttpPost]
    public async Task<IActionResult> UpdateWeight([FromBody] UpdateWeightDto body)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, "/api/recommendation/config/weights")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { key = body.Key, value = body.Value }),
                Encoding.UTF8, "application/json")
        };
        AttachToken(request);
        var response = await _http.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();
        return Content(json, "application/json");
    }

    [HttpPost]
    public async Task<IActionResult> ReembedBatch([FromQuery] bool force = false)
    {
        var request = new HttpRequestMessage(HttpMethod.Post,
            $"/api/recommendation/reembed/batch?force={force.ToString().ToLower()}");
        AttachToken(request);
        var response = await _http.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();
        return Content(json, "application/json");
    }

    // ── Helper ─────────────────────────────────────────
    private void AttachToken(HttpRequestMessage req)
    {
        var token = HttpContext.Session.GetString("AccessToken");
        if (!string.IsNullOrEmpty(token))
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    // ════════════════════════════════════════════════════
    // SUBSCRIPTION PLAN MANAGEMENT
    // ════════════════════════════════════════════════════

    [HttpGet]
    public async Task<IActionResult> ManagePlans()
    {
        var req = new HttpRequestMessage(HttpMethod.Get, "/api/admin/subscription-plans");
        AttachToken(req);
        try
        {
            var response = await _http.SendAsync(req);
            var json     = await response.Content.ReadAsStringAsync();
            var result   = JsonSerializer.Deserialize<ApiResult<List<AdminPlanDto>>>(json, JsonOpts);
            return View("~/Views/Admin/ManagePlans.cshtml", result?.Data ?? new List<AdminPlanDto>());
        }
        catch
        {
            TempData["Error"] = "Không thể tải danh sách gói subscription.";
            return View("~/Views/Admin/ManagePlans.cshtml", new List<AdminPlanDto>());
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreatePlan(AdminPlanDto model)
    {
        var features = (model.FeaturesRaw ?? string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        var payload = JsonSerializer.Serialize(new
        {
            name        = model.Name?.Trim(),
            description = model.Description?.Trim(),
            price       = model.Price,
            durationDays= model.DurationDays,
            features,
            isActive    = model.IsActive,
            sortOrder   = model.SortOrder
        });
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/admin/subscription-plans")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        AttachToken(req);
        try
        {
            var response = await _http.SendAsync(req);
            var result   = JsonSerializer.Deserialize<ApiResult>(await response.Content.ReadAsStringAsync(), JsonOpts);
            TempData[result?.Success == true ? "Success" : "Error"] = result?.Message ?? "Thao tác thất bại.";
        }
        catch (Exception ex) { TempData["Error"] = $"Lỗi kết nối: {ex.Message}"; }
        return RedirectToAction(nameof(ManagePlans));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditPlan(Guid id, AdminPlanDto model)
    {
        var features = (model.FeaturesRaw ?? string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        var payload = JsonSerializer.Serialize(new
        {
            name        = model.Name?.Trim(),
            description = model.Description?.Trim(),
            price       = model.Price,
            durationDays= model.DurationDays,
            features,
            isActive    = model.IsActive,
            sortOrder   = model.SortOrder
        });
        var req = new HttpRequestMessage(HttpMethod.Patch, $"/api/admin/subscription-plans/{id}")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        AttachToken(req);
        try
        {
            var response = await _http.SendAsync(req);
            var result   = JsonSerializer.Deserialize<ApiResult>(await response.Content.ReadAsStringAsync(), JsonOpts);
            TempData[result?.Success == true ? "Success" : "Error"] = result?.Message ?? "Thao tác thất bại.";
        }
        catch (Exception ex) { TempData["Error"] = $"Lỗi kết nối: {ex.Message}"; }
        return RedirectToAction(nameof(ManagePlans));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeletePlan(Guid id)
    {
        var req = new HttpRequestMessage(HttpMethod.Delete, $"/api/admin/subscription-plans/{id}");
        AttachToken(req);
        try
        {
            var response = await _http.SendAsync(req);
            var result   = JsonSerializer.Deserialize<ApiResult>(await response.Content.ReadAsStringAsync(), JsonOpts);
            TempData[result?.Success == true ? "Success" : "Error"] = result?.Message ?? "Thao tác thất bại.";
        }
        catch (Exception ex) { TempData["Error"] = $"Lỗi kết nối: {ex.Message}"; }
        return RedirectToAction(nameof(ManagePlans));
    }
}

// ── Internal DTOs ───────────────────────────────────────
public class ScoringWeightsDto
{
    public double SemanticWeight { get; set; } = 0.80;
    public double SalaryWeight { get; set; } = 0.12;
    public double DistanceWeight { get; set; } = 0.08;
    public double ColdStartScore { get; set; } = 0.75;
}

public class UpdateWeightDto
{
    public string Key { get; set; } = string.Empty;
    public double Value { get; set; }
}
