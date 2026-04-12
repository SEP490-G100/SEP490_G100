using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebSite.Models;
using WebSite.Models.FAQ;
using System.Text.Json.Serialization;

namespace WebSite.Controllers;

[Authorize(Roles = "Moderator")]
[Route("Moderator")]
public class ModeratorFaqController : Controller
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public ModeratorFaqController(IHttpClientFactory httpFactory)
    {
        _http = httpFactory.CreateClient("BackendApi");
    }

    // GET /Moderator/ManageFAQ
    [HttpGet("ManageFAQ")]
    public async Task<IActionResult> ManageFAQ(
        string? search = null,
        bool? isActive = null,
        string? category = null,
        int page = 1)
    {
        ViewBag.Search = search;
        ViewBag.IsActive = isActive?.ToString() ?? "";
        ViewBag.Category = category ?? "";

        var qs = new List<string> { $"page={page}", "pageSize=3" };
        if (!string.IsNullOrWhiteSpace(search)) qs.Add($"search={Uri.EscapeDataString(search)}");
        if (isActive.HasValue) qs.Add($"isActive={isActive.Value.ToString().ToLower()}");
        if (!string.IsNullOrWhiteSpace(category)) qs.Add($"category={Uri.EscapeDataString(category)}");

        var token = HttpContext.Session.GetString("AccessToken");

        var listReq = new HttpRequestMessage(HttpMethod.Get, $"/api/Faq?{string.Join("&", qs)}");
        if (!string.IsNullOrEmpty(token))
            listReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var catReq = new HttpRequestMessage(HttpMethod.Get, "/api/Faq/categories");
        if (!string.IsNullOrEmpty(token))
            catReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            var listResp = await _http.SendAsync(listReq);
            var listJson = await listResp.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResult<FaqListResponse>>(listJson, JsonOpts);

            try
            {
                var catResp = await _http.SendAsync(catReq);
                var catJson = await catResp.Content.ReadAsStringAsync();
                var catResult = JsonSerializer.Deserialize<ApiResult<List<string>>>(catJson, JsonOpts);
                ViewBag.Categories = catResult?.Data ?? new List<string>();
            }
            catch
            {
                ViewBag.Categories = new List<string>();
            }

            return View("~/Views/Moderator/FAQ/ManageFAQ.cshtml", result?.Data ?? new FaqListResponse());
        }
        catch
        {
            TempData["Error"] = "Không thể tải danh sách FAQ.";
            ViewBag.Categories = new List<string>();
            return View("~/Views/Moderator/FAQ/ManageFAQ.cshtml", new FaqListResponse());
        }
    }

    // GET /Moderator/CreateFAQ
    [HttpGet("CreateFAQ")]
    public IActionResult CreateFAQ() => View("~/Views/Moderator/FAQ/CreateFAQ.cshtml", new CreateFaqRequest());

    // POST /Moderator/CreateFAQ
    [HttpPost("CreateFAQ")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateFAQ(CreateFaqRequest model)
    {
        var body = JsonSerializer.Serialize(new
        {
            question = model.Question,
            answer = model.Answer,
            category = model.Category,
            isActive = model.IsActive
        });
        var token = HttpContext.Session.GetString("AccessToken");
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/Faq")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            var response = await _http.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResult>(json, JsonOpts);
            if (result?.Success == true)
            {
                return RedirectToAction(nameof(ManageFAQ), new
                {
                    toastType = "success",
                    toastMessage = "Đã tạo FAQ thành công"
                });
            }
            TempData["Error"] = result?.Message ?? "Tạo FAQ thất bại.";
            return View("~/Views/Moderator/FAQ/CreateFAQ.cshtml", model);
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Lỗi kết nối: {ex.Message}";
            return View("~/Views/Moderator/FAQ/CreateFAQ.cshtml", model);
        }
    }

    // GET /Moderator/ViewFAQDetail/{id}
    [HttpGet("ViewFAQDetail/{id:guid}")]
    public async Task<IActionResult> ViewFAQDetail(Guid id)
    {
        var token = HttpContext.Session.GetString("AccessToken");
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/Faq/{id}");
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            var response = await _http.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResult<FaqDto>>(json, JsonOpts);
            if (result?.Success != true || result.Data == null)
            {
                TempData["Error"] = "Không tìm thấy FAQ.";
                return RedirectToAction(nameof(ManageFAQ));
            }
            return View("~/Views/Moderator/FAQ/ViewFAQDetail.cshtml", result.Data);
        }
        catch
        {
            TempData["Error"] = "Lỗi kết nối đến API.";
            return RedirectToAction(nameof(ManageFAQ));
        }
    }

    // POST /Moderator/ViewFAQDetail/{id}
    [HttpPost("ViewFAQDetail/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ViewFAQDetail(Guid id, UpdateFaqRequest model)
    {
        var body = JsonSerializer.Serialize(new
        {
            question = model.Question,
            answer = model.Answer,
            isActive = model.IsActive
        });
        var token = HttpContext.Session.GetString("AccessToken");
        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/Faq/{id}")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            var response = await _http.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResult>(json, JsonOpts);
            if (result?.Success == true)
            {
                return RedirectToAction(nameof(ManageFAQ), new
                {
                    toastType = "success",
                    toastMessage = "Đã chỉnh sửa FAQ thành công"
                });
            }
            TempData["Error"] = result?.Message ?? "Cập nhật FAQ thất bại.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Lỗi kết nối: {ex.Message}";
        }

        return RedirectToAction(nameof(ViewFAQDetail), new { id });
    }

    // POST /Moderator/ToggleFaqStatus
    [HttpPost("ToggleFaqStatus")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleFaqStatus(Guid id)
    {
        var token = HttpContext.Session.GetString("AccessToken");
        var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/Faq/{id}/toggle-status");
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            var response = await _http.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            return Content(json, "application/json");
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"Lỗi kết nối: {ex.Message}" });
        }
    }
}
