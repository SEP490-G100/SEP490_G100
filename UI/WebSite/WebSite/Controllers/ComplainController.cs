using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.WebUtilities;
using WebSite.Hubs;
using WebSite.Models.Search;
using WebSite.Services;

namespace WebSite.Controllers;

[Authorize]
public class ComplainController : Controller
{
    private readonly HttpClient _http;
    private readonly IHubContext<NotificationHub> _notificationHub;
    private readonly IAzureBlobStorageService _blobStorageService;

    public ComplainController(
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
    public async Task<IActionResult> ComplainJobPosting(JobPostingComplainFormModel model, CancellationToken cancellationToken)
    {
        if (model.JobPostingId == Guid.Empty)
        {
            return RedirectToAction("Index", "Search", new
            {
                toastType = "error",
                toastMessage = "Không t?m th?y bài đăng can phàn nàn."
            });
        }

        var reason = ExtractPlainText(model.Reason);
        if (reason.Length < 5 || reason.Length > 500)
        {
            return RedirectToAction("Index", "Search", new
            {
                toastType = "warning",
                toastMessage = "Ly do phàn nàn phai tu 5 den 500 ky tu."
            });
        }

        var evidence = NormalizeEvidence(model.Evidence);
        if (!string.IsNullOrEmpty(evidence) && evidence.Length > 2000)
        {
            return RedirectToAction("Index", "Search", new
            {
                toastType = "warning",
                toastMessage = "Bang chung không được vuot qua 2000 ky tu."
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
                    type = "complain-submitted",
                    title = "Co bao cao bài đăng mới",
                    message = "Mot bao cao job posting mới vua được gui va can moderator xu ly.",
                    toastType = "warning"
                }, cancellationToken);

                return RedirectToAction("Index", "Search", new
                {
                    toastType = "success",
                    toastMessage = message ?? "Gui phàn nàn thành công."
                });
            }

            return RedirectToAction("Index", "Search", new
            {
                toastType = "error",
                toastMessage = message ?? "Không th? gui phàn nàn bai dang."
            });
        }
        catch (Exception)
        {
            return RedirectToAction("Index", "Search", new
            {
                toastType = "error",
                toastMessage = "Không th? gui phàn nàn bai dang."
            });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ComplainProfile(ProfileComplainFormModel model, CancellationToken cancellationToken)
    {
        if (model.ComplainedUserId == Guid.Empty)
        {
            return RedirectToProfileComplainReturn(
                model.ReturnUrl,
                model.ComplainedUserId,
                "error",
                "Không t?m th?y profile can phàn nàn.");
        }

        var currentUserIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(currentUserIdValue, out var currentUserId) && currentUserId == model.ComplainedUserId)
        {
            return RedirectToProfileComplainReturn(
                model.ReturnUrl,
                model.ComplainedUserId,
                "warning",
                "Ban không the phàn nàn chinh profile cua minh.");
        }

        var reason = ExtractPlainText(model.Reason);
        if (reason.Length < 5 || reason.Length > 500)
        {
            return RedirectToProfileComplainReturn(
                model.ReturnUrl,
                model.ComplainedUserId,
                "warning",
                "Ly do phàn nàn phai tu 5 den 500 ky tu.");
        }

        var evidence = NormalizeEvidence(model.Evidence);
        if (!string.IsNullOrEmpty(evidence) && evidence.Length > 2000)
        {
            return RedirectToProfileComplainReturn(
                model.ReturnUrl,
                model.ComplainedUserId,
                "warning",
                "Bang chung không được vuot qua 2000 ky tu.");
        }

        SetAuthHeader();
        try
        {
            var response = await _http.PostAsJsonAsync(
                $"/api/reports/profiles/{model.ComplainedUserId}",
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
                    type = "complain-submitted",
                    title = "Co bao cao profile mới",
                    message = "Mot bao cao profile mới vua được gui va can moderator xu ly.",
                    toastType = "warning"
                }, cancellationToken);

                return RedirectToProfileComplainReturn(
                    model.ReturnUrl,
                    model.ComplainedUserId,
                    "success",
                    message ?? "Gui phàn nàn profile thành công.");
            }

            return RedirectToProfileComplainReturn(
                model.ReturnUrl,
                model.ComplainedUserId,
                "error",
                message ?? "Không th? gui phàn nàn profile.");
        }
        catch (Exception)
        {
            return RedirectToProfileComplainReturn(
                model.ReturnUrl,
                model.ComplainedUserId,
                "error",
                "Không th? gui phàn nàn profile.");
        }
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> UploadComplainImages(List<IFormFile>? files, CancellationToken cancellationToken)
        => await UploadComplainMediaCore(files, BlobMediaType.Image, cancellationToken);

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> UploadComplainVideos(List<IFormFile>? files, CancellationToken cancellationToken)
        => await UploadComplainMediaCore(files, BlobMediaType.Video, cancellationToken);

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> UploadComplainMedia(List<IFormFile>? files, [FromQuery] string? mediaType, CancellationToken cancellationToken)
    {
        var normalized = mediaType?.Trim().ToLowerInvariant();
        if (normalized is not ("image" or "video"))
        {
            return Json(new { success = false, message = "Lo?i media không h?p l?. Ch? h? tr? image/video." });
        }

        var type = normalized == "video" ? BlobMediaType.Video : BlobMediaType.Image;
        return await UploadComplainMediaCore(files, type, cancellationToken);
    }

    private async Task<IActionResult> UploadComplainMediaCore(List<IFormFile>? files, BlobMediaType mediaType, CancellationToken cancellationToken)
    {
        var mediaLabel = mediaType == BlobMediaType.Video ? "video" : "anh";

        if (files == null || files.Count == 0)
            return Json(new { success = false, message = $"Vui lòng chon it nhat mot {mediaLabel}." });

        try
        {
            var uploadedUrls = await _blobStorageService.UploadMediaAsync(
                files,
                BlobStorageContainerKind.ReportMedia,
                mediaType,
                cancellationToken);

            if (uploadedUrls.Count == 0)
                return Json(new { success = false, message = $"Không c? {mediaLabel} hop le ?? upload." });

            return Json(new
            {
                success = true,
                message = uploadedUrls.Count == 1 ? $"Upload {mediaLabel} thành công." : $"Upload cac {mediaLabel} thành công.",
                data = new { urls = uploadedUrls }
            });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"Không th? upload {mediaLabel} complain: {ex.Message}" });
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

    private IActionResult RedirectToProfileComplainReturn(
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



