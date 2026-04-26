using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using WebSite.Models;
using WebSite.Hubs;
using WebSite.Models.Nanny;
using WebSite.Validation;

namespace WebSite.Controllers;

[Authorize]
public class NannyController : Controller
{
    private readonly HttpClient _http;
    private readonly IHubContext<NotificationHub> _notificationHub;
    private readonly ILogger<NannyController> _logger;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };
    private const string NannyOnboardingCompletedSessionKey = "NannyOnboardingCompleted";

    public NannyController(
        IHttpClientFactory httpFactory,
        IHubContext<NotificationHub> notificationHub,
        ILogger<NannyController> logger)
    {
        _http = httpFactory.CreateClient("BackendApi");
        _notificationHub = notificationHub;
        _logger = logger;
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

    private async Task<OnboardingStatusViewModel?> GetOnboardingStatusAsync()
    {
        var token = GetToken();
        if (string.IsNullOrWhiteSpace(token))
            return null;

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/onboarding/status");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                return null;

            var content = await response.Content.ReadAsStringAsync();
            var apiResult = TryDeserializeApiResult(content);
            if (apiResult?.Data is JsonElement element && element.ValueKind == JsonValueKind.Object)
                return JsonSerializer.Deserialize<OnboardingStatusViewModel>(element.GetRawText(), JsonOpts);

            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Không gọi được API onboarding/status (kiểm tra ApiSettings:BaseUrl và backend đang chạy).");
            return null;
        }
        catch (TaskCanceledException ex) when (ex.CancellationToken.IsCancellationRequested == false)
        {
            _logger.LogWarning(ex, "Timeout khi gọi API onboarding/status.");
            return null;
        }
    }

    private async Task<IActionResult?> GuardNannyOnboardingAccessAsync()
    {
        if (IsNannyOnboardingLocked())
            return RedirectToAction("Index", "Home");

        var status = await GetOnboardingStatusAsync();
        if (status == null)
            return RedirectToAction("Start", "Onboarding");

        SyncNannyOnboardingCompletedFlag(status);
        if (IsNannyOnboardingLocked())
            return RedirectToAction("Index", "Home");

        if (!string.Equals(status.Role, "Nanny", StringComparison.OrdinalIgnoreCase))
            return RedirectToAction("Start", "Onboarding");

        if (!status.RequiresOnboarding || string.Equals(status.NextStep, "Completed", StringComparison.OrdinalIgnoreCase))
        {
            HttpContext.Session.SetString(NannyOnboardingCompletedSessionKey, "1");
            return RedirectToAction("Index", "Home");
        }

        return null;
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

    private static string BuildFallbackHttpErrorMessage(HttpResponseMessage response, string? body, string defaultMessage)
    {
        var trimmed = (body ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return defaultMessage;

        if (trimmed.Length > 180)
            trimmed = trimmed[..180] + "...";

        return $"{defaultMessage} (HTTP {(int)response.StatusCode}): {trimmed}";
    }

    private async Task<NannyProfileViewModel> LoadCurrentNannyProfileAsync()
    {
        SetAuthHeader();

        try
        {
            var response = await _http.GetAsync("/api/profile");
            if (!response.IsSuccessStatusCode)
                return new NannyProfileViewModel();

            var json = await response.Content.ReadAsStringAsync();
            var apiResult = TryDeserializeApiResult(json);
            if (apiResult?.Data is System.Text.Json.JsonElement element &&
                element.ValueKind == JsonValueKind.Object)
            {
                return JsonSerializer.Deserialize<NannyProfileViewModel>(element.GetRawText(), JsonOpts)
                       ?? new NannyProfileViewModel();
            }
        }
        catch
        {
            // Keep onboarding page available even when profile prefill fails.
        }

        return new NannyProfileViewModel();
    }

    private async Task<NannySkillsViewModel> BuildSkillsViewModelAsync(IEnumerable<Guid>? preselectedIds = null)
    {
        var vm = new NannySkillsViewModel();

        SetAuthHeader();
        try
        {
            var response = await _http.GetAsync("/api/onboarding/skills");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var apiResult = TryDeserializeApiResult(json);

                if (apiResult?.Data is System.Text.Json.JsonElement element &&
                    element.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    vm.AvailableSkills = JsonSerializer.Deserialize<List<NannySkillSelectionViewModel>>(
                        element.GetRawText(), JsonOpts) ?? new();
                }
            }
        }
        catch
        {
            vm.AvailableSkills = new();
        }

        var selectedSet = new HashSet<Guid>((preselectedIds ?? Enumerable.Empty<Guid>())
            .Where(id => id != Guid.Empty));

        if (selectedSet.Count == 0)
        {
            try
            {
                var profileResponse = await _http.GetAsync("/api/profile");
                if (profileResponse.IsSuccessStatusCode)
                {
                    var profileJson = await profileResponse.Content.ReadAsStringAsync();
                    var profileResult = TryDeserializeApiResult(profileJson);
                    if (profileResult?.Data is System.Text.Json.JsonElement profileElement &&
                        profileElement.ValueKind == System.Text.Json.JsonValueKind.Object &&
                        profileElement.TryGetProperty("skills", out var skillsElement) &&
                        skillsElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        foreach (var skillElement in skillsElement.EnumerateArray())
                        {
                            if (!skillElement.TryGetProperty("skillId", out var skillIdElement))
                                continue;

                            Guid skillId;
                            if (skillIdElement.ValueKind == System.Text.Json.JsonValueKind.String &&
                                Guid.TryParse(skillIdElement.GetString(), out skillId))
                            {
                                selectedSet.Add(skillId);
                            }
                            else if (Guid.TryParse(skillIdElement.ToString(), out skillId))
                            {
                                selectedSet.Add(skillId);
                            }
                        }
                    }
                }
            }
            catch
            {
                // ignore prefill errors
            }
        }

        vm.SelectedSkillIds = selectedSet.ToList();
        return vm;
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

    [HttpPost]
    public async Task<IActionResult> ToggleFavorite(Guid id)
    {
        if (!IsParentRole())
            return StatusCode(403, new { success = false, message = "Bạn không có quyền yêu thích bảo mẫu." });

        SetAuthHeader();
        try
        {
            var response = await _http.PostAsync($"/api/nannies/{id}/favorite/toggle", null);
            var json = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode &&
                tryParseFavoriteEventPayload(json, out var isFavorite, out var nannyUserId) &&
                isFavorite && nannyUserId != Guid.Empty)
            {
                await _notificationHub.Clients.User(nannyUserId.ToString()).SendAsync("notification:new", new
                {
                    title = "Hồ sơ của bạn vừa được yêu thích",
                    message = "Có phụ huynh vừa tìm hồ sơ của bạn.",
                    type = "nanny-profile-favorited",
                    relatedId = id
                });
            }

            return Content(json, "application/json");
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpGet]
    [Authorize]
    public IActionResult Favorites()
    {
        if (!IsParentRole())
            return RedirectToAction(nameof(List));

        return View();
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> FavoriteData([FromQuery] int page = 1, [FromQuery] int pageSize = 12)
    {
        if (!IsParentRole())
            return StatusCode(403, new { success = false, message = "Bạn không có quyền xem danh sách bảo mẫu yêu thích." });

        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 12 : Math.Min(pageSize, 50);

        SetAuthHeader();
        try
        {
            var response = await _http.GetAsync($"/api/nannies/favorites/me?page={page}&pageSize={pageSize}");
            var json = await response.Content.ReadAsStringAsync();
            return Content(json, "application/json");
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message, data = Array.Empty<object>() });
        }
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> SendContactRequest([FromBody] SendContactRequestViewModel request)
    {
        if (!IsParentRole())
            return StatusCode(403, new { success = false, message = "Bạn không có quyền gửi yêu cầu liên hệ." });

        if (request == null || request.NannyProfileId == Guid.Empty)
            return BadRequest(new { success = false, message = "Dữ liệu yêu cầu liên hệ không hợp lệ." });

        SetAuthHeader();
        try
        {
            var response = await _http.PostAsJsonAsync(
                $"/api/nannies/{request.NannyProfileId}/contact-request",
                new { message = request.Message });

            var json = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode &&
                tryParseContactRequestEventPayload(json, out var requestId, out _, out var nannyUserId))
            {
                if (nannyUserId != Guid.Empty)
                {
                    await _notificationHub.Clients.User(nannyUserId.ToString()).SendAsync("notification:new", new
                    {
                        title = "Bạn vừa nhận yêu cầu liên hệ",
                        message = "Có phụ huynh vừa gửi yêu cầu liên hệ cho hồ sơ của bạn.",
                        type = "contact-request-received",
                        relatedId = requestId
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
    public IActionResult ContactRequests()
    {
        if (!IsParentRole())
            return RedirectToAction(nameof(List));

        return View();
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> SentContactRequestsData([FromQuery] int? status = null)
    {
        if (!IsParentRole())
            return StatusCode(403, new { success = false, message = "Bạn không có quyền xem lịch sử yêu cầu liên hệ." });

        SetAuthHeader();
        try
        {
            var query = status.HasValue ? $"?status={status.Value}" : string.Empty;
            var response = await _http.GetAsync($"/api/nannies/contact-requests/sent{query}");
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
            return Json(new { success = false, message = ex.Message, data = (object?)null });
        }
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> ContactRequestDetailData(Guid requestId)
    {
        if (!IsParentRole())
            return StatusCode(403, new { success = false, message = "Bạn không có quyền xem chi tiết yêu cầu liên hệ." });

        if (requestId == Guid.Empty)
            return BadRequest(new { success = false, message = "Mã yêu cầu không hợp lệ." });

        SetAuthHeader();
        try
        {
            var response = await _http.GetAsync($"/api/nannies/contact-requests/{requestId}");
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
            return Json(new { success = false, message = ex.Message, data = (object?)null });
        }
    }

    [HttpGet]
    [Authorize]
    public IActionResult ReceivedContactRequests()
    {
        if (!IsNannyRole())
            return RedirectToAction(nameof(List));

        return View();
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> ReceivedContactRequestsData([FromQuery] int? status = null)
    {
        if (!IsNannyRole())
            return StatusCode(403, new { success = false, message = "Bạn không có quyền xem yêu cầu liên hệ đã nhận." });

        SetAuthHeader();
        try
        {
            var query = status.HasValue ? $"?status={status.Value}" : string.Empty;
            var response = await _http.GetAsync($"/api/nannies/contact-requests/received{query}");
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
            return Json(new { success = false, message = ex.Message, data = (object?)null });
        }
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> ReceivedContactRequestDetailData(Guid requestId)
    {
        if (!IsNannyRole())
            return StatusCode(403, new { success = false, message = "Bạn không có quyền xem chi tiết yêu cầu liên hệ." });

        if (requestId == Guid.Empty)
            return BadRequest(new { success = false, message = "Mã yêu cầu không hợp lệ." });

        SetAuthHeader();
        try
        {
            var response = await _http.GetAsync($"/api/nannies/contact-requests/{requestId}");
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
            return Json(new { success = false, message = ex.Message, data = (object?)null });
        }
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> ReviewReceivedContactRequest(Guid requestId, [FromBody] ReviewContactRequestViewModel request)
    {
        if (!IsNannyRole())
            return StatusCode(403, new { success = false, message = "Bạn không có quyền xử lý yêu cầu liên hệ." });

        if (requestId == Guid.Empty || request == null)
            return BadRequest(new { success = false, message = "Dữ liệu xử lý yêu cầu liên hệ không hợp lệ." });

        SetAuthHeader();
        try
        {
            var response = await _http.PostAsJsonAsync($"/api/nannies/contact-requests/{requestId}/review", new
            {
                action = request.Action,
                responseMessage = request.ResponseMessage
            });

            var json = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode &&
                tryParseContactReviewEventPayload(json, out var parsedRequestId, out var parentUserId, out var status))
            {
                if (parentUserId != Guid.Empty)
                {
                    var approved = status == 1;
                    await _notificationHub.Clients.User(parentUserId.ToString()).SendAsync("notification:new", new
                    {
                        title = approved ? "Yêu cầu liên hệ đã được chấp nhận" : "Yêu cầu liên hệ bị từ chối",
                        message = approved
                            ? "Bảo mẫu đã chấp nhận yêu cầu liên hệ của bạn."
                            : "Bảo mẫu đã từ chối yêu cầu liên hệ của bạn.",
                        type = approved ? "contact-request-accepted" : "contact-request-rejected",
                        relatedId = parsedRequestId == Guid.Empty ? requestId : parsedRequestId
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
    public async Task<IActionResult> Profile()
    {
        var guard = await GuardNannyOnboardingAccessAsync();
        if (guard != null)
            return guard;

        var vm = await LoadCurrentNannyProfileAsync();
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(NannyProfileViewModel model)
    {
        var guard = await GuardNannyOnboardingAccessAsync();
        if (guard != null)
            return guard;

        foreach (var error in SalaryValidationRules.Validate(
                     model.ExpectedSalaryMin,
                     model.ExpectedSalaryMax,
                     nameof(model.ExpectedSalaryMin),
                     nameof(model.ExpectedSalaryMax),
                     "Lương từ",
                     "Đến"))
        {
            var memberNames = error.MemberNames?.Any() == true
                ? error.MemberNames
                : new[] { string.Empty };

            foreach (var memberName in memberNames)
                ModelState.AddModelError(memberName, error.ErrorMessage ?? "Lương không hợp lệ.");
        }

        if (!ModelState.IsValid) return View(model);

        SetAuthHeader();
        HttpResponseMessage response;
        try
        {
            response = await _http.PutAsJsonAsync("/api/onboarding/nanny/profile", new
            {
                model.Bio,
                model.YearsOfExperience,
                model.EducationLevel,
                model.ExpectedSalaryMin,
                model.ExpectedSalaryMax,
                model.MaxTravelDistance
            });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Không kết nối được API khi cập nhật hồ sơ nanny.");
            ModelState.AddModelError(
                string.Empty,
                "Không kết nối được máy chủ. Hãy kiểm tra API backend đang chạy và cấu hình ApiSettings:BaseUrl trong Web.");
            return View(model);
        }
        catch (TaskCanceledException ex) when (ex.CancellationToken.IsCancellationRequested == false)
        {
            _logger.LogWarning(ex, "Timeout khi cập nhật hồ sơ nanny.");
            ModelState.AddModelError(string.Empty, "Hệ thống phản hồi quá lâu. Vui lòng thử lại.");
            return View(model);
        }

        var json = await response.Content.ReadAsStringAsync();
        var apiResult = TryDeserializeApiResult(json);
        if (apiResult == null || !apiResult.Success)
        {
            var fallback = BuildFallbackHttpErrorMessage(response, json, "Cập nhật thất bại.");
            var errorMessage = apiResult?.Message ?? fallback;
            if (errorMessage.Contains("Số năm kinh nghiệm", StringComparison.OrdinalIgnoreCase))
                ModelState.AddModelError(nameof(model.YearsOfExperience), errorMessage);
            else
                ModelState.AddModelError(string.Empty, errorMessage);
            return View(model);
        }

        return RedirectToAction("Skills", "Nanny");
    }

    [HttpGet]
    public async Task<IActionResult> Skills()
    {
        var guard = await GuardNannyOnboardingAccessAsync();
        if (guard != null)
            return guard;

        var vm = await BuildSkillsViewModelAsync();
        return View(vm);
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Skills(NannySkillsViewModel model)
    {
        var guard = await GuardNannyOnboardingAccessAsync();
        if (guard != null)
            return guard;

        var selectedSkillIds = (model.SelectedSkillIds ?? new List<Guid>())
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        if (!selectedSkillIds.Any())
            ModelState.AddModelError(nameof(model.SelectedSkillIds), "Vui lòng chọn ít nhất một kỹ năng.");

        if (!ModelState.IsValid)
        {
            var vm = await BuildSkillsViewModelAsync(selectedSkillIds);
            return View(vm);
        }

        SetAuthHeader();
        var payload = new
        {
            Skills = selectedSkillIds
                .Select(skillId => new { SkillId = skillId, ProficiencyLevel = (int?)null })
                .ToList()
        };

        var response = await _http.PutAsJsonAsync("/api/onboarding/nanny/skills", payload);
        var json = await response.Content.ReadAsStringAsync();
        var apiResult = TryDeserializeApiResult(json);
        if (apiResult == null || !apiResult.Success)
        {
            var fallback = BuildFallbackHttpErrorMessage(response, json, "Cập nhật thất bại.");
            ModelState.AddModelError("", apiResult?.Message ?? fallback);
            var vm = await BuildSkillsViewModelAsync(selectedSkillIds);
            return View(vm);
        }

        return RedirectToAction("Availability", "Nanny");
    }

    [HttpGet]
    public async Task<IActionResult> Availability()
    {
        var guard = await GuardNannyOnboardingAccessAsync();
        if (guard != null)
            return guard;

        return View(new NannyAvailabilityViewModel());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Availability(NannyAvailabilityViewModel model)
    {
        var guard = await GuardNannyOnboardingAccessAsync();
        if (guard != null)
            return guard;

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
        var apiResult = TryDeserializeApiResult(json);
        if (apiResult == null || !apiResult.Success)
        {
            var fallback = BuildFallbackHttpErrorMessage(response, json, "Cập nhật thất bại.");
            ModelState.AddModelError("", apiResult?.Message ?? fallback);
            return View(model);
        }
        HttpContext.Session.SetString(NannyOnboardingCompletedSessionKey, "1");
        return RedirectToAction("Index", "Home");
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

    private bool IsParentRole()
    {
        if (User.IsInRole("Parent"))
            return true;

        return User.Claims.Any(c =>
            c.Type == System.Security.Claims.ClaimTypes.Role &&
            string.Equals(c.Value, "Parent", StringComparison.OrdinalIgnoreCase));
    }

    private bool IsNannyRole()
    {
        if (User.IsInRole("Nanny"))
            return true;

        return User.Claims.Any(c =>
            c.Type == System.Security.Claims.ClaimTypes.Role &&
            string.Equals(c.Value, "Nanny", StringComparison.OrdinalIgnoreCase));
    }

    private static bool tryParseFavoriteEventPayload(
        string json,
        out bool isFavorite,
        out Guid nannyUserId)
    {
        isFavorite = false;
        nannyUserId = Guid.Empty;

        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("success", out var successEl) || successEl.ValueKind != JsonValueKind.True)
                return false;

            if (!root.TryGetProperty("isFavorite", out var isFavoriteEl) || isFavoriteEl.ValueKind != JsonValueKind.True)
                return false;

            if (root.TryGetProperty("nannyUserId", out var userIdEl))
            {
                if (userIdEl.ValueKind == JsonValueKind.String &&
                    Guid.TryParse(userIdEl.GetString(), out var parsedUserId))
                    nannyUserId = parsedUserId;
            }

            isFavorite = true;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool tryParseContactRequestEventPayload(
        string json,
        out Guid requestId,
        out Guid parentUserId,
        out Guid nannyUserId)
    {
        requestId = Guid.Empty;
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

            if (dataEl.TryGetProperty("requestId", out var requestIdEl) &&
                requestIdEl.ValueKind == JsonValueKind.String &&
                Guid.TryParse(requestIdEl.GetString(), out var parsedRequestId))
                requestId = parsedRequestId;

            if (dataEl.TryGetProperty("parentUserId", out var parentIdEl) &&
                parentIdEl.ValueKind == JsonValueKind.String &&
                Guid.TryParse(parentIdEl.GetString(), out var parsedParentUserId))
                parentUserId = parsedParentUserId;

            if (dataEl.TryGetProperty("nannyUserId", out var nannyIdEl) &&
                nannyIdEl.ValueKind == JsonValueKind.String &&
                Guid.TryParse(nannyIdEl.GetString(), out var parsedNannyUserId))
                nannyUserId = parsedNannyUserId;

            return requestId != Guid.Empty || parentUserId != Guid.Empty || nannyUserId != Guid.Empty;
        }
        catch
        {
            return false;
        }
    }

    private static bool tryParseContactReviewEventPayload(
        string json,
        out Guid requestId,
        out Guid parentUserId,
        out int status)
    {
        requestId = Guid.Empty;
        parentUserId = Guid.Empty;
        status = 0;

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

            if (dataEl.TryGetProperty("requestId", out var requestIdEl) &&
                requestIdEl.ValueKind == JsonValueKind.String &&
                Guid.TryParse(requestIdEl.GetString(), out var parsedRequestId))
                requestId = parsedRequestId;

            if (dataEl.TryGetProperty("parentUserId", out var parentIdEl) &&
                parentIdEl.ValueKind == JsonValueKind.String &&
                Guid.TryParse(parentIdEl.GetString(), out var parsedParentId))
                parentUserId = parsedParentId;

            if (dataEl.TryGetProperty("status", out var statusEl) &&
                statusEl.ValueKind == JsonValueKind.Number &&
                statusEl.TryGetInt32(out var parsedStatus))
                status = parsedStatus;

            return requestId != Guid.Empty || parentUserId != Guid.Empty;
        }
        catch
        {
            return false;
        }
    }

    public sealed class SendContactRequestViewModel
    {
        public Guid NannyProfileId { get; set; }
        public string? Message { get; set; }
    }

    public sealed class ReviewContactRequestViewModel
    {
        public int Action { get; set; } // 1 = Accept, 2 = Reject
        public string? ResponseMessage { get; set; }
    }
}
