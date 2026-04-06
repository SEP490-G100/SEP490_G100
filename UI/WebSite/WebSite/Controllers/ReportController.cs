using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using WebSite.Hubs;
using WebSite.Models.Search;
using WebSite.Services;

namespace WebSite.Controllers;

[Authorize]
public class ReportController : Controller
{
    private readonly HttpClient _http;
    private readonly IHubContext<NotificationHub> _notificationHub;
    private readonly IAzureBlobStorageService _blobStorageService;

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
                toastMessage = "Khong tim thay bai dang can phan nan."
            });
        }

        var reason = ExtractPlainText(model.Reason);
        if (reason.Length < 5 || reason.Length > 500)
        {
            return RedirectToAction("Index", "Search", new
            {
                toastType = "warning",
                toastMessage = "Ly do phan nan phai tu 5 den 500 ky tu."
            });
        }

        var evidence = NormalizeEvidence(model.Evidence);
        if (!string.IsNullOrEmpty(evidence) && evidence.Length > 2000)
        {
            return RedirectToAction("Index", "Search", new
            {
                toastType = "warning",
                toastMessage = "Bang chung khong duoc vuot qua 2000 ky tu."
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

            if (response.IsSuccessStatusCode)
            {
                await _notificationHub.Clients.Group("role:Moderator").SendAsync("notification:new", new
                {
                    type = "report-submitted",
                    title = "Co bao cao bai dang moi",
                    message = "Mot bao cao job posting moi vua duoc gui va can moderator xu ly.",
                    toastType = "warning"
                }, cancellationToken);

                return RedirectToAction("Index", "Search", new
                {
                    toastType = "success",
                    toastMessage = message ?? "Gui phan nan thanh cong."
                });
            }

            return RedirectToAction("Index", "Search", new
            {
                toastType = "error",
                toastMessage = message ?? "Khong the gui phan nan bai dang."
            });
        }
        catch (Exception)
        {
            return RedirectToAction("Index", "Search", new
            {
                toastType = "error",
                toastMessage = "Khong the gui phan nan bai dang."
            });
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
            return Json(new { success = false, message = "Loai media khong hop le. Chi ho tro image/video." });
        }

        var type = normalized == "video" ? BlobMediaType.Video : BlobMediaType.Image;
        return await UploadReportMediaCore(files, type, cancellationToken);
    }

    private async Task<IActionResult> UploadReportMediaCore(List<IFormFile>? files, BlobMediaType mediaType, CancellationToken cancellationToken)
    {
        var mediaLabel = mediaType == BlobMediaType.Video ? "video" : "anh";

        if (files == null || files.Count == 0)
            return Json(new { success = false, message = $"Vui long chon it nhat mot {mediaLabel}." });

        try
        {
            var uploadedUrls = await _blobStorageService.UploadMediaAsync(
                files,
                BlobStorageContainerKind.ReportMedia,
                mediaType,
                cancellationToken);

            if (uploadedUrls.Count == 0)
                return Json(new { success = false, message = $"Khong co {mediaLabel} hop le de upload." });

            return Json(new
            {
                success = true,
                message = uploadedUrls.Count == 1 ? $"Upload {mediaLabel} thanh cong." : $"Upload cac {mediaLabel} thanh cong.",
                data = new { urls = uploadedUrls }
            });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"Khong the upload {mediaLabel} report: {ex.Message}" });
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
}
