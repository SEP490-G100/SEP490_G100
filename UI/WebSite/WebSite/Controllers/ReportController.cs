using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.WebUtilities;
using WebSite.Hubs;
using WebSite.Models;
using WebSite.Models.Moderator;
using WebSite.Models.Profile;
using WebSite.Models.Search;
using WebSite.Services;

namespace WebSite.Controllers;

[Authorize]
public class ReportController : Controller
{
    private readonly HttpClient _http;
    private readonly IHubContext<NotificationHub> _notificationHub;
    private readonly IAzureBlobStorageService _blobStorageService;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public ReportController(
        IHttpClientFactory httpFactory,
        IHubContext<NotificationHub> notificationHub,
        IAzureBlobStorageService blobStorageService)
    {
        _http = httpFactory.CreateClient("BackendApi");
        _notificationHub = notificationHub;
        _blobStorageService = blobStorageService;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReportJobPosting(JobPostingReportFormModel model, CancellationToken cancellationToken)
    {
        if (model.JobPostingId == Guid.Empty)
        {
            return RedirectToAction("Index", "Search", new
            {
                toastType = "error",
                toastMessage = "Không tìm thấy bài đăng cần khiếu nại."
            });
        }

        var reason = ExtractPlainText(model.Reason);
        if (reason.Length < 5 || reason.Length > 500)
        {
            return RedirectToAction("Index", "Search", new
            {
                toastType = "warning",
                toastMessage = "Lý do khiếu nại phải từ 5 đến 500 ký tự."
            });
        }

        var evidence = NormalizeEvidence(model.Evidence);
        if (!string.IsNullOrEmpty(evidence) && evidence.Length > 2000)
        {
            return RedirectToAction("Index", "Search", new
            {
                toastType = "warning",
                toastMessage = "Bằng chứng không được vượt quá 2000 ký tự."
            });
        }

        SetAuthHeader();
        try
        {
            var response = await _http.PostAsJsonAsync(
                $"/api/reports/job-postings/{model.JobPostingId}",
                new
                {
                    reason,
                    evidence
                },
                cancellationToken);

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var message = TryExtractMessage(json);
            var isBusinessSuccess = TryExtractSuccessFlag(json);

            if (response.IsSuccessStatusCode && isBusinessSuccess != false)
            {
                await _notificationHub.Clients.Group("role:Moderator").SendAsync("notification:new", new
                {
                    type = "report-submitted",
                    title = "Có báo cáo bài đăng mới",
                    message = "Một báo cáo tin đăng mới vừa được gửi và cần điều hành viên xử lý.",
                    toastType = "warning"
                }, cancellationToken);

                return RedirectToAction("Index", "Search", new
                {
                    toastType = "success",
                    toastMessage = message ?? "Gửi khiếu nại thành công."
                });
            }

            return RedirectToAction("Index", "Search", new
            {
                toastType = "error",
                toastMessage = message ?? "Không thể gửi khiếu nại bài đăng."
            });
        }
        catch (Exception)
        {
            return RedirectToAction("Index", "Search", new
            {
                toastType = "error",
                toastMessage = "Không thể gửi khiếu nại bài đăng."
            });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReportProfile(ProfileReportFormModel model, CancellationToken cancellationToken)
    {
        if (model.ReportedUserId == Guid.Empty)
        {
            return RedirectToProfileReportReturn(
                model.ReturnUrl,
                model.ReportedUserId,
                "error",
                "Không tìm thấy hồ sơ cần khiếu nại.");
        }

        var currentUserIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(currentUserIdValue, out var currentUserId) && currentUserId == model.ReportedUserId)
        {
            return RedirectToProfileReportReturn(
                model.ReturnUrl,
                model.ReportedUserId,
                "warning",
                "Bạn không thể khiếu nại chính hồ sơ của mình.");
        }

        var reason = ExtractPlainText(model.Reason);
        if (reason.Length < 5 || reason.Length > 500)
        {
            return RedirectToProfileReportReturn(
                model.ReturnUrl,
                model.ReportedUserId,
                "warning",
                "Lý do khiếu nại phải từ 5 đến 500 ký tự.");
        }

        var evidence = NormalizeEvidence(model.Evidence);
        if (!string.IsNullOrEmpty(evidence) && evidence.Length > 2000)
        {
            return RedirectToProfileReportReturn(
                model.ReturnUrl,
                model.ReportedUserId,
                "warning",
                "Bằng chứng không được vượt quá 2000 ký tự.");
        }

        SetAuthHeader();
        try
        {
            var response = await _http.PostAsJsonAsync(
                $"/api/reports/profiles/{model.ReportedUserId}",
                new
                {
                    reason,
                    evidence
                },
                cancellationToken);

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var message = TryExtractMessage(json);
            var isBusinessSuccess = TryExtractSuccessFlag(json);

            if (response.IsSuccessStatusCode && isBusinessSuccess != false)
            {
                await _notificationHub.Clients.Group("role:Moderator").SendAsync("notification:new", new
                {
                    type = "report-submitted",
                    title = "Có báo cáo hồ sơ mới",
                    message = "Một báo cáo hồ sơ mới vừa được gửi và cần điều hành viên xử lý.",
                    toastType = "warning"
                }, cancellationToken);

                return RedirectToProfileReportReturn(
                    model.ReturnUrl,
                    model.ReportedUserId,
                    "success",
                    message ?? "Gửi khiếu nại hồ sơ thành công.");
            }

            return RedirectToProfileReportReturn(
                model.ReturnUrl,
                model.ReportedUserId,
                "error",
                message ?? "Không thể gửi khiếu nại hồ sơ.");
        }
        catch (Exception)
        {
            return RedirectToProfileReportReturn(
                model.ReturnUrl,
                model.ReportedUserId,
                "error",
                "Không thể gửi khiếu nại hồ sơ.");
        }
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> UploadReportImages(List<IFormFile>? files, CancellationToken cancellationToken)
        => await UploadReportMediaCore(files, BlobMediaType.Image, cancellationToken);

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> UploadReportVideos(List<IFormFile>? files, CancellationToken cancellationToken)
        => await UploadReportMediaCore(files, BlobMediaType.Video, cancellationToken);

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> UploadReportMedia(List<IFormFile>? files, [FromQuery] string? mediaType, CancellationToken cancellationToken)
    {
        var normalized = mediaType?.Trim().ToLowerInvariant();
        if (normalized is not ("image" or "video"))
        {
            return Json(new { success = false, message = "Loại phương tiện không hợp lệ. Chỉ hỗ trợ ảnh/video." });
        }

        var type = normalized == "video" ? BlobMediaType.Video : BlobMediaType.Image;
        return await UploadReportMediaCore(files, type, cancellationToken);
    }

    private async Task<IActionResult> UploadReportMediaCore(List<IFormFile>? files, BlobMediaType mediaType, CancellationToken cancellationToken)
    {
        var mediaLabel = mediaType == BlobMediaType.Video ? "video" : "ảnh";

        if (files == null || files.Count == 0)
            return Json(new { success = false, message = $"Vui lòng chọn ít nhất một {mediaLabel}." });

        try
        {
            var uploadedUrls = await _blobStorageService.UploadMediaAsync(
                files,
                BlobStorageContainerKind.ReportMedia,
                mediaType,
                cancellationToken);

            if (uploadedUrls.Count == 0)
                return Json(new { success = false, message = $"Không có {mediaLabel} hợp lệ để tải lên." });

            return Json(new
            {
                success = true,
                message = uploadedUrls.Count == 1
                    ? $"Đã tải lên {mediaLabel} thành công."
                    : $"Đã tải lên các tệp {mediaLabel} thành công.",
                data = new { urls = uploadedUrls }
            });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"Không thể tải lên {mediaLabel} khiếu nại: {ex.Message}" });
        }
    }

    [Authorize(Roles = "Moderator")]
    [HttpGet("/Moderator/ManageReport")]
    public async Task<IActionResult> ManageReport(
        string? search = null,
        int? status = null,
        string? entityType = null,
        int page = 1)
    {
        ViewBag.Search = search;
        ViewBag.Status = status?.ToString() ?? "";
        ViewBag.EntityType = entityType ?? "";

        var qs = new List<string> { $"page={page}", "pageSize=10" };
        if (status.HasValue) qs.Add($"status={status.Value}");
        if (!string.IsNullOrWhiteSpace(entityType)) qs.Add($"entityType={Uri.EscapeDataString(entityType)}");
        if (!string.IsNullOrWhiteSpace(search)) qs.Add($"search={Uri.EscapeDataString(search)}");

        var token = HttpContext.Session.GetString("AccessToken");
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/reports/moderator-view-report-list?{string.Join("&", qs)}");
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            var response = await _http.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResult<ModeratorReportListResponse>>(json, JsonOpts);
            return View("~/Views/Moderator/Report/ManageReport.cshtml", result?.Data ?? new ModeratorReportListResponse());
        }
        catch
        {
            TempData["Error"] = "Không thể tải danh sách report.";
            return View("~/Views/Moderator/Report/ManageReport.cshtml", new ModeratorReportListResponse());
        }
    }

    [Authorize(Roles = "Moderator")]
    [HttpGet("/Moderator/ViewReportDetail/{id:guid}")]
    public async Task<IActionResult> ViewReportDetail(Guid id)
    {
        var detail = await FetchReportDetailAsync(id);
        if (detail == null)
        {
            TempData["Error"] = "Không tìm thấy report.";
            return RedirectToAction(nameof(ManageReport));
        }

        var pageModel = new ModeratorReportDetailPageModel
        {
            Detail = detail,
            Form = new ModeratorResolveReportRequest
            {
                Resolution = detail.Resolution ?? string.Empty,
                ActionTaken = detail.ActionTaken ?? string.Empty,
                OffenderNotificationMessage = string.Empty
            }
        };
        return View("~/Views/Moderator/Report/ViewReportDetail.cshtml", pageModel);
    }

    [Authorize(Roles = "Moderator")]
    [HttpPost("/Moderator/ViewReportDetail/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ViewReportDetail(Guid id, [Bind(Prefix = "Form")] ModeratorResolveReportRequest form)
    {
        var detail = await FetchReportDetailAsync(id);
        if (detail == null)
        {
            TempData["Error"] = "Không tìm thấy report.";
            return RedirectToAction(nameof(ManageReport));
        }

        if (detail.Status == 1)
        {
            TempData["Error"] = "Report đã completed. Không thể chỉnh sửa.";
            var lockedModel = new ModeratorReportDetailPageModel
            {
                Detail = detail,
                Form = new ModeratorResolveReportRequest
                {
                    Resolution = detail.Resolution ?? string.Empty,
                    ActionTaken = detail.ActionTaken ?? string.Empty,
                    OffenderNotificationMessage = string.Empty
                }
            };
            return View("~/Views/Moderator/Report/ViewReportDetail.cshtml", lockedModel);
        }

        if (!ModelState.IsValid)
        {
            var invalidModel = new ModeratorReportDetailPageModel
            {
                Detail = detail,
                Form = form
            };
            return View("~/Views/Moderator/Report/ViewReportDetail.cshtml", invalidModel);
        }

        var token = HttpContext.Session.GetString("AccessToken");
        var body = JsonSerializer.Serialize(new
        {
            resolution = form.Resolution?.Trim(),
            actionTaken = form.ActionTaken?.Trim(),
            offenderNotificationMessage = string.IsNullOrWhiteSpace(form.OffenderNotificationMessage)
                ? null
                : form.OffenderNotificationMessage.Trim()
        });

        var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/reports/moderator-resolve-report/{id}")
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
                await _notificationHub.Clients.Group($"user:{detail.ReporterUserId}").SendAsync("notification:new", new
                {
                    type = "report-resolved",
                    title = "Thông báo từ Moderator",
                    message = "Chúng tôi đã xử lý yêu cầu phản ánh của bạn",
                    toastType = "success"
                });

                if (!string.IsNullOrWhiteSpace(form.OffenderNotificationMessage)
                    && detail.OffenderUserId.HasValue
                    && detail.OffenderUserId.Value != Guid.Empty)
                {
                    await _notificationHub.Clients.Group($"user:{detail.OffenderUserId.Value}").SendAsync("notification:new", new
                    {
                        type = "report-reviewed",
                        title = "Thông báo từ Moderator",
                        message = form.OffenderNotificationMessage.Trim(),
                        toastType = "info"
                    });
                }

                var listUrl = Url.Action(nameof(ManageReport), "Report") ?? "/Moderator/ManageReport";
                var toastMessage = Uri.EscapeDataString("Bạn đã xử lý yêu cầu phản ánh thành công");
                return Redirect($"{listUrl}?toastType=success&toastMessage={toastMessage}");
            }

            TempData["Error"] = result?.Message ?? "Không thể xử lý report.";
            var failedModel = new ModeratorReportDetailPageModel
            {
                Detail = detail,
                Form = form
            };
            return View("~/Views/Moderator/Report/ViewReportDetail.cshtml", failedModel);
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Lỗi kết nối: {ex.Message}";
            var failedModel = new ModeratorReportDetailPageModel
            {
                Detail = detail,
                Form = form
            };
            return View("~/Views/Moderator/Report/ViewReportDetail.cshtml", failedModel);
        }
    }

    [Authorize(Roles = "Moderator")]
    [HttpGet("/Moderator/ViewReportedJobPostingDetail")]
    public async Task<IActionResult> ViewReportedJobPostingDetail(Guid jobPostingId, Guid? reportId = null)
    {
        if (jobPostingId == Guid.Empty)
            return RedirectToAction(nameof(ManageReport), new { toastType = "error", toastMessage = "Không tìm thấy bài đăng." });

        var token = HttpContext.Session.GetString("AccessToken");
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            var response = await _http.GetAsync($"/api/job-postings/moderator-view-job-detail/{jobPostingId}");
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResult<JobPostingDetailResponse>>(json, JsonOpts);

            if (result?.Success != true || result.Data == null)
            {
                return RedirectToAction(nameof(ManageReport), new
                {
                    toastType = "error",
                    toastMessage = result?.Message ?? "Không thể tải chi tiết bài đăng bị phản ánh."
                });
            }

            var model = new ModeratorReportedJobPostingDetailPageModel
            {
                ReportId = reportId,
                JobPosting = result.Data
            };

            return View("~/Views/Moderator/Report/ViewReportedJobPostingDetail.cshtml", model);
        }
        catch
        {
            return RedirectToAction(nameof(ManageReport), new
            {
                toastType = "error",
                toastMessage = "Không thể tải chi tiết bài đăng bị phản ánh."
            });
        }
    }

    [Authorize(Roles = "Moderator")]
    [HttpPost("/Moderator/DeactivateReportedJobPosting")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeactivateReportedJobPosting(Guid jobPostingId, Guid? reportId = null)
    {
        if (jobPostingId == Guid.Empty)
            return RedirectToAction(nameof(ManageReport), new { toastType = "error", toastMessage = "Không tìm thấy bài đăng." });

        var token = HttpContext.Session.GetString("AccessToken");
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            var response = await _http.PatchAsJsonAsync($"/api/job-postings/moderator-deactivate-job-posting/{jobPostingId}", new { });
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResult>(json, JsonOpts);

            if (result?.Success == true)
            {
                if (reportId.HasValue && reportId.Value != Guid.Empty)
                {
                    return RedirectToAction(nameof(ViewReportDetail), new
                    {
                        id = reportId.Value,
                        toastType = "success",
                        toastMessage = "Đã vô hiệu hóa bài đăng"
                    });
                }

                return RedirectToAction(nameof(ManageReport), new
                {
                    toastType = "success",
                    toastMessage = "Đã vô hiệu hóa bài đăng"
                });
            }

            return RedirectToAction(nameof(ViewReportedJobPostingDetail), new
            {
                jobPostingId,
                reportId,
                toastType = "error",
                toastMessage = result?.Message ?? "Không thể vô hiệu hóa bài đăng."
            });
        }
        catch
        {
            return RedirectToAction(nameof(ViewReportedJobPostingDetail), new
            {
                jobPostingId,
                reportId,
                toastType = "error",
                toastMessage = "Không thể vô hiệu hóa bài đăng."
            });
        }
    }

    [Authorize(Roles = "Moderator")]
    [HttpGet("/Moderator/ViewReportedProfileDetail")]
    public async Task<IActionResult> ViewReportedProfileDetail(Guid userId, Guid? reportId = null)
    {
        if (userId == Guid.Empty)
            return RedirectToAction(nameof(ManageReport), new { toastType = "error", toastMessage = "Không tìm thấy hồ sơ." });

        try
        {
            var token = HttpContext.Session.GetString("AccessToken");
            if (string.IsNullOrWhiteSpace(token))
            {
                return RedirectToAction("Login", "Auth", new
                {
                    toastType = "warning",
                    toastMessage = "Phiên đăng nhập đã hết hạn."
                });
            }

            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await _http.GetAsync($"/api/profile/public/{userId}");

            if (!response.IsSuccessStatusCode)
            {
                return RedirectToAction(nameof(ManageReport), new
                {
                    toastType = "error",
                    toastMessage = "Không thể tải hồ sơ bị phản ánh."
                });
            }

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResult<PersonalProfileViewModel>>(json, JsonOpts);
            if (result?.Success != true || result.Data == null)
            {
                return RedirectToAction(nameof(ManageReport), new
                {
                    toastType = "error",
                    toastMessage = result?.Message ?? "Không thể tải hồ sơ bị phản ánh."
                });
            }

            result.Data.IsReadOnlyView = true;

            var model = new ModeratorReportedProfileDetailPageModel
            {
                ReportId = reportId,
                Profile = result.Data
            };

            return View("~/Views/Moderator/Report/ViewReportedProfileDetail.cshtml", model);
        }
        catch
        {
            return RedirectToAction(nameof(ManageReport), new
            {
                toastType = "error",
                toastMessage = "Không thể tải hồ sơ bị phản ánh."
            });
        }
    }

    [Authorize(Roles = "Moderator")]
    [HttpPost("/Moderator/ToggleReportStatus")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleReportStatus(Guid id, bool isActive)
    {
        var token = HttpContext.Session.GetString("AccessToken");
        var body = JsonSerializer.Serialize(new { isActive });
        var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/reports/moderator-toggle-report-status/{id}")
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
            var isSuccess = result?.Success ?? false;

            return Json(new
            {
                success = isSuccess,
                message = isSuccess
                    ? (isActive
                        ? "Bạn đã kích hoạt phản ánh thành công"
                        : "Bạn đã vô hiệu hóa phản ánh thành công")
                    : (result?.Message ?? "Thao tac that bai.")
            });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"Lỗi kết nối: {ex.Message}" });
        }
    }

    private async Task<ModeratorReportDetailDto?> FetchReportDetailAsync(Guid id)
    {
        var token = HttpContext.Session.GetString("AccessToken");
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/reports/moderator-view-report-detail/{id}");
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            var response = await _http.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResult<ModeratorReportDetailDto>>(json, JsonOpts);
            return result?.Success == true ? result.Data : null;
        }
        catch
        {
            return null;
        }
    }

    private void SetAuthHeader()
    {
        var token = HttpContext.Session.GetString("AccessToken");
        if (!string.IsNullOrWhiteSpace(token))
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        else
            _http.DefaultRequestHeaders.Authorization = null;
    }

    private static string ExtractPlainText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        var noTags = Regex.Replace(html, "<.*?>", " ");
        var decoded = WebUtility.HtmlDecode(noTags);
        return Regex.Replace(decoded ?? string.Empty, @"\s+", " ").Trim();
    }

    private static string? NormalizeEvidence(string? rawEvidence)
    {
        if (string.IsNullOrWhiteSpace(rawEvidence))
            return null;

        var normalized = rawEvidence.Trim();
        var hasMediaTag =
            normalized.Contains("<img", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("<video", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("<iframe", StringComparison.OrdinalIgnoreCase);

        var plain = ExtractPlainText(normalized);
        if (string.IsNullOrWhiteSpace(plain) && !hasMediaTag)
            return null;

        return normalized;
    }

    private static string? TryExtractMessage(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("message", out var messageElement) &&
                messageElement.ValueKind == JsonValueKind.String)
            {
                return messageElement.GetString();
            }

            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("title", out var titleElement) &&
                titleElement.ValueKind == JsonValueKind.String)
            {
                return titleElement.GetString();
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private static bool? TryExtractSuccessFlag(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("success", out var successElement) &&
                (successElement.ValueKind == JsonValueKind.True || successElement.ValueKind == JsonValueKind.False))
            {
                return successElement.GetBoolean();
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private IActionResult RedirectToProfileReportReturn(
        string? returnUrl,
        Guid complainedUserId,
        string toastType,
        string toastMessage)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            var redirectUrl = BuildToastRedirectUrl(returnUrl, toastType, toastMessage);
            return LocalRedirect(redirectUrl);
        }

        return RedirectToAction("ViewUser", "Profile", new
        {
            userId = complainedUserId,
            toastType,
            toastMessage
        });
    }

    private static string BuildToastRedirectUrl(string returnUrl, string toastType, string toastMessage)
    {
        var sanitizedUrl = returnUrl.Trim();
        var hashIndex = sanitizedUrl.IndexOf('#');
        var hash = string.Empty;
        if (hashIndex >= 0)
        {
            hash = sanitizedUrl[hashIndex..];
            sanitizedUrl = sanitizedUrl[..hashIndex];
        }

        var path = sanitizedUrl;
        var query = string.Empty;
        var queryIndex = sanitizedUrl.IndexOf('?');
        if (queryIndex >= 0)
        {
            path = sanitizedUrl[..queryIndex];
            query = sanitizedUrl[(queryIndex + 1)..];
        }

        var preserved = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(query))
        {
            var existing = QueryHelpers.ParseQuery(query);
            foreach (var item in existing)
            {
                if (item.Key.Equals("toastType", StringComparison.OrdinalIgnoreCase) ||
                    item.Key.Equals("toastMessage", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                preserved[item.Key] = item.Value.ToString();
            }
        }

        var resultUrl = preserved.Count > 0
            ? QueryHelpers.AddQueryString(path, preserved)
            : path;

        resultUrl = QueryHelpers.AddQueryString(
            resultUrl,
            new Dictionary<string, string?>
            {
                ["toastType"] = toastType,
                ["toastMessage"] = toastMessage
            });

        return string.IsNullOrEmpty(hash) ? resultUrl : $"{resultUrl}{hash}";
    }
}
