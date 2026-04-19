using System.Net.Http.Headers;
using System.Text.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using WebSite.Hubs;
using WebSite.Models.Search;

namespace WebSite.Controllers;

/// <summary>
/// Proxy má»i request tá»« browser Ä‘áº¿n Backend API
/// GET  /Search          â†’ View trang tÃ¬m kiáº¿m
/// GET  /Search/Jobs     â†’ proxy GET /api/search/jobs
/// POST /Search/CreateJob â†’ proxy POST /api/job-postings
/// PUT  /Search/UpdateJob/{id} â†’ proxy PUT /api/job-postings/{id}
/// DELETE /Search/DeleteJob/{id} â†’ proxy DELETE /api/job-postings/{id}
/// </summary>
public class SearchController : Controller
{
    private readonly HttpClient _http;
    private readonly IHubContext<NotificationHub> _notificationHub;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public SearchController(
        IHttpClientFactory httpFactory,
        IHubContext<NotificationHub> notificationHub)
    {
        _http = httpFactory.CreateClient("BackendApi");
        _notificationHub = notificationHub;
    }

    // â”€â”€ GET /Search â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        ViewBag.SkillOptions = await getSkillOptionsForView();
        return View();
    }

    [HttpGet]
    [Authorize]
    public IActionResult History() => View();

    [HttpGet]
    [Authorize]
    public IActionResult SavedJobs()
    {
        if (!isNannyRole())
            return RedirectToAction(nameof(Index));

        return View();
    }

    [HttpGet]
    [Authorize]
    public IActionResult Applications()
    {
        if (!isNannyRole())
            return RedirectToAction(nameof(Index));

        return View();
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Edit(Guid id)
    {
        if (!await canEditJob(id))
            return RedirectToAction(nameof(Index));

        ViewBag.JobId = id;
        ViewBag.SkillOptions = await getSkillOptionsForView();
        return View();
    }

    // â”€â”€ GET /Search/Jobs â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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
    [Authorize]
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
    [Authorize]
    public async Task<IActionResult> SavedJobsData([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (!isNannyRole())
            return StatusCode(403, new { success = false, total = 0, data = Array.Empty<object>(), message = "Ban không co quyen xem bài đăng da luu." });

        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 20 : Math.Min(pageSize, 50);

        SetAuthHeader();
        try
        {
            var response = await _http.GetAsync($"/api/search/jobs/favorite/me?page={page}&pageSize={pageSize}");
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
    [Authorize]
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

    // â”€â”€ POST /Search/CreateJob â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> CreateJob([FromBody] JsonElement body)
    {
        if (!isParentRole())
            return StatusCode(403, new { success = false, message = "Ban không co quyen ?ang tin tim b?o m?u." });

        SetAuthHeader();
        try
        {
            var response = await _http.PostAsJsonAsync("/api/job-postings", body);
            if (response.IsSuccessStatusCode)
            {
                await _notificationHub.Clients.Group("role:Moderator").SendAsync("notification:new", new
                {
                    type = "job-posting-review-required",
                    title = "Co bài đăng mới can duy?t",
                    message = "Mot parent vua tao job posting mới đang chờ moderator duy?t.",
                    toastType = "info"
                });

                var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!string.IsNullOrWhiteSpace(currentUserId))
                {
                    await _notificationHub.Clients.Group($"user:{currentUserId}").SendAsync("notification:new", new
                    {
                        type = "job-posting-pending",
                        title = "Bài đăng da được tao",
                        message = "Bài đăng cua ban đang chờ moderator duy?t.",
                        toastType = "success"
                    });
                }
            }
            return await ProxyApiResponse(response);
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    // â”€â”€ PUT /Search/UpdateJob/{id} â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    [HttpPut]
    [Authorize]
    public async Task<IActionResult> UpdateJob(Guid id, [FromBody] JsonElement body)
    {
        if (!isParentRole())
            return StatusCode(403, new { success = false, message = "Ban không co quyen chỉnh sửa bài đăng nay." });

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

    // â”€â”€ DELETE /Search/DeleteJob/{id} â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    [HttpDelete]
    [Authorize]
    public async Task<IActionResult> DeleteJob(Guid id)
    {
        if (!await canEditJob(id))
            return StatusCode(403, new { success = false, message = "Ban không co quyen xoa bài đăng nay." });

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

    // â”€â”€ GET /Search/Detail/{id} â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> ToggleFavoriteJob(Guid jobPostingId)
    {
        if (!isNannyRole())
            return StatusCode(403, new { success = false, message = "Ban không co quyen luu bai dang." });

        SetAuthHeader();
        try
        {
            var response = await _http.PostAsync($"/api/search/jobs/favorite/{jobPostingId}/toggle", null);
            return await ProxyApiResponse(response);
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> ApplyJob(Guid jobPostingId)
    {
        if (!isNannyRole())
            return StatusCode(403, new { success = false, message = "Ban không co quyen ứng tuyển bai dang." });

        SetAuthHeader();
        try
        {
            var response = await _http.PostAsync($"/api/search/jobs/{jobPostingId}/apply", null);
            var json = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode &&
                tryParseApplyEventPayload(json, out var parentUserId, out var nannyUserId))
            {
                if (parentUserId != Guid.Empty)
                {
                    await _notificationHub.Clients.User(parentUserId.ToString()).SendAsync("notification:new", new
                    {
                        title = "Co nanny vua ứng tuyển bài đăng cua ban",
                        message = "Hay vao notification ?? xem chi tiết don ứng tuyển.",
                        type = "job-application-received",
                        relatedId = jobPostingId
                    });
                }

                if (nannyUserId != Guid.Empty)
                {
                    await _notificationHub.Clients.User(nannyUserId.ToString()).SendAsync("notification:new", new
                    {
                        title = "Ban da gui don ứng tuyển",
                        message = "Don ứng tuyển da được gui. Vui lòng cho Parent phan hoi.",
                        type = "job-application-submitted",
                        relatedId = jobPostingId
                    });
                }
            }

            return new ContentResult
            {
                Content = string.IsNullOrWhiteSpace(json) ? "{}" : json,
                ContentType = "application/json",
                StatusCode = (int)response.StatusCode
            };
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> MyApplicationsData([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (!isNannyRole())
            return StatusCode(403, new { success = false, total = 0, data = Array.Empty<object>(), message = "Ban không co quyen xem lich su ứng tuyển." });

        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 20 : Math.Min(pageSize, 50);

        SetAuthHeader();
        try
        {
            var response = await _http.GetAsync($"/api/search/jobs/applications/me?page={page}&pageSize={pageSize}");
            var json = await response.Content.ReadAsStringAsync();
            return new ContentResult
            {
                Content = string.IsNullOrWhiteSpace(json) ? "{}" : json,
                ContentType = "application/json",
                StatusCode = (int)response.StatusCode
            };
        }
        catch (Exception ex)
        {
            return Json(new { success = false, total = 0, data = Array.Empty<object>(), message = ex.Message });
        }
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> WithdrawApplication(Guid applicationId)
    {
        if (!isNannyRole())
            return StatusCode(403, new { success = false, message = "Ban không co quyen huy don ứng tuyển." });

        SetAuthHeader();
        try
        {
            var response = await _http.PostAsync($"/api/search/jobs/applications/{applicationId}/withdraw", null);
            var json = await response.Content.ReadAsStringAsync();
            return new ContentResult
            {
                Content = string.IsNullOrWhiteSpace(json) ? "{}" : json,
                ContentType = "application/json",
                StatusCode = (int)response.StatusCode
            };
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> JobApplicationsByJob(Guid jobPostingId, [FromQuery] int? status = null)
    {
        if (!isParentRole())
            return StatusCode(403, new { success = false, message = "Ban không co quyen xem request ứng tuyển." });

        SetAuthHeader();
        try
        {
            var query = status.HasValue ? $"?status={status.Value}" : string.Empty;
            var response = await _http.GetAsync($"/api/search/jobs/{jobPostingId}/applications{query}");
            var json = await response.Content.ReadAsStringAsync();
            return new ContentResult
            {
                Content = string.IsNullOrWhiteSpace(json) ? "{}" : json,
                ContentType = "application/json",
                StatusCode = (int)response.StatusCode
            };
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> ReviewJobApplication(Guid applicationId, [FromBody] JsonElement body)
    {
        if (!isParentRole())
            return StatusCode(403, new { success = false, message = "Ban không co quyen duy?t request ứng tuyển." });

        SetAuthHeader();
        try
        {
            var response = await _http.PostAsJsonAsync($"/api/search/jobs/applications/{applicationId}/review", body);
            var json = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode &&
                tryParseReviewEventPayload(json, out var nannyUserId, out var status, out var relatedJobId))
            {
                if (nannyUserId != Guid.Empty)
                {
                    var approved = status == 1;
                    await _notificationHub.Clients.User(nannyUserId.ToString()).SendAsync("notification:new", new
                    {
                        title = approved ? "Don ứng tuyển được chap nhan" : "Don ứng tuyển bi từ chối",
                        message = approved
                            ? "Parent da chap nhan don ứng tuyển cua ban."
                            : "Parent da từ chối don ứng tuyển cua ban.",
                        type = approved ? "job-application-approved" : "job-application-rejected",
                        relatedId = relatedJobId == Guid.Empty ? applicationId : relatedJobId
                    });
                }
            }

            return new ContentResult
            {
                Content = string.IsNullOrWhiteSpace(json) ? "{}" : json,
                ContentType = "application/json",
                StatusCode = (int)response.StatusCode
            };
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    // â”€â”€ Helper: Ä‘Ã­nh kÃ¨m JWT token tá»« session â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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
                message = response.IsSuccessStatusCode ? "ThÃ nh cÃ´ng." : $"Lá»—i HTTP {(int)response.StatusCode}."
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

    private async Task<bool> canEditJob(Guid jobId)
    {
        SetAuthHeader();
        try
        {
            var response = await _http.GetAsync("/api/job-postings/my");
            if (!response.IsSuccessStatusCode) return false;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("data", out var dataEl) || dataEl.ValueKind != JsonValueKind.Array)
                return false;

            foreach (var item in dataEl.EnumerateArray())
            {
                if (!item.TryGetProperty("id", out var idEl)) continue;
                if (Guid.TryParse(idEl.GetString(), out var ownedId) && ownedId == jobId)
                    return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private bool isParentRole()
    {
        if (User.IsInRole("Parent"))
            return true;

        return User.Claims.Any(c =>
            c.Type == System.Security.Claims.ClaimTypes.Role &&
            string.Equals(c.Value, "Parent", StringComparison.OrdinalIgnoreCase));
    }

    private bool isNannyRole()
    {
        if (User.IsInRole("Nanny"))
            return true;

        return User.Claims.Any(c =>
            c.Type == System.Security.Claims.ClaimTypes.Role &&
            string.Equals(c.Value, "Nanny", StringComparison.OrdinalIgnoreCase));
    }

    private static bool tryParseApplyEventPayload(string json, out Guid parentUserId, out Guid nannyUserId)
    {
        parentUserId = Guid.Empty;
        nannyUserId = Guid.Empty;
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("success", out var successEl) || successEl.ValueKind != JsonValueKind.True)
                return false;

            if (!root.TryGetProperty("data", out var dataEl) || dataEl.ValueKind != JsonValueKind.Object)
                return false;

            if (dataEl.TryGetProperty("parentUserId", out var parentIdEl) &&
                parentIdEl.ValueKind == JsonValueKind.String &&
                Guid.TryParse(parentIdEl.GetString(), out var parsedParentId))
                parentUserId = parsedParentId;

            if (dataEl.TryGetProperty("nannyUserId", out var nannyIdEl) &&
                nannyIdEl.ValueKind == JsonValueKind.String &&
                Guid.TryParse(nannyIdEl.GetString(), out var parsedNannyId))
                nannyUserId = parsedNannyId;

            return parentUserId != Guid.Empty || nannyUserId != Guid.Empty;
        }
        catch
        {
            return false;
        }
    }

    private static bool tryParseReviewEventPayload(string json, out Guid nannyUserId, out int status, out Guid relatedJobId)
    {
        nannyUserId = Guid.Empty;
        status = 0;
        relatedJobId = Guid.Empty;
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("success", out var successEl) || successEl.ValueKind != JsonValueKind.True)
                return false;

            if (!root.TryGetProperty("data", out var dataEl) || dataEl.ValueKind != JsonValueKind.Object)
                return false;

            if (dataEl.TryGetProperty("nannyUserId", out var nannyIdEl) &&
                nannyIdEl.ValueKind == JsonValueKind.String &&
                Guid.TryParse(nannyIdEl.GetString(), out var parsedNannyId))
                nannyUserId = parsedNannyId;

            if (dataEl.TryGetProperty("status", out var statusEl) &&
                statusEl.ValueKind == JsonValueKind.Number &&
                statusEl.TryGetInt32(out var parsedStatus))
                status = parsedStatus;

            if (dataEl.TryGetProperty("jobPostingId", out var jobIdEl) &&
                jobIdEl.ValueKind == JsonValueKind.String &&
                Guid.TryParse(jobIdEl.GetString(), out var parsedJobId))
                relatedJobId = parsedJobId;

            return nannyUserId != Guid.Empty;
        }
        catch
        {
            return false;
        }
    }
}

