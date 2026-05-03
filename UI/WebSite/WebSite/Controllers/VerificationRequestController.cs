using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Azure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using WebSite.Enums;
using WebSite.Hubs;
using WebSite.Models;
using WebSite.Models.Profile;
using WebSite.Models.Verification;
using WebSite.Services;

namespace WebSite.Controllers;

public class VerificationRequestController : Controller
{
    private readonly HttpClient _http;
    private readonly IAzureBlobStorageService _storageService;
    private readonly IHubContext<NotificationHub> _notificationHub;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };
    private static readonly HashSet<string> AllowedDocumentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp",
        ".pdf"
    };
    private const long MaxDocumentSizeInBytes = 5 * 1024 * 1024;

    public VerificationRequestController(
        IHttpClientFactory httpFactory,
        IAzureBlobStorageService storageService,
        IHubContext<NotificationHub> notificationHub)
    {
        _http = httpFactory.CreateClient("BackendApi");
        _storageService = storageService;
        _notificationHub = notificationHub;
    }

    [Authorize(Roles = "Moderator")]
    [HttpGet("/Moderator/ManageNannyVerification")]
    public async Task<IActionResult> ManageNannyVerification(string? search = null, int? status = null, int? requestType = null, int page = 1)
    {
        ViewBag.Search = search;
        ViewBag.Status = status;
        ViewBag.RequestType = requestType;

        var qs = new List<string> { $"page={page}", "pageSize=10" };
        if (status.HasValue) qs.Add($"status={status.Value}");
        if (requestType.HasValue) qs.Add($"requestType={requestType.Value}");
        if (!string.IsNullOrWhiteSpace(search)) qs.Add($"search={Uri.EscapeDataString(search)}");

        var token = HttpContext.Session.GetString("AccessToken");
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"/api/VerificationRequest/moderator-view-verification-list?{string.Join("&", qs)}");

        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            var response = await _http.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResult<VerificationRequestListResponse>>(json, JsonOpts);

            return View(
                "~/Views/Moderator/NannyVerification/ManageNannyVerification.cshtml",
                result?.Data ?? new VerificationRequestListResponse());
        }
        catch
        {
            TempData["Error"] = "Không thể tải danh sách xác minh.";
            return View(
                "~/Views/Moderator/NannyVerification/ManageNannyVerification.cshtml",
                new VerificationRequestListResponse());
        }
    }

    [Authorize(Roles = "Moderator")]
    [HttpGet("/Moderator/ViewNannyVerificationDetail/{id:guid}")]
    public async Task<IActionResult> ViewNannyVerificationDetail(Guid id)
    {
        var token = HttpContext.Session.GetString("AccessToken");
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/VerificationRequest/moderator-view-verification-detail/{id}");

        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            var response = await _http.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResult<VerificationRequestDetailDto>>(json, JsonOpts);

            if (result?.Success != true || result.Data == null)
            {
            TempData["Error"] = "Không tìm thấy yêu cầu xác minh.";
                return RedirectToAction(nameof(ManageNannyVerification));
            }

            return View("~/Views/Moderator/NannyVerification/ViewNannyVerificationDetail.cshtml", result.Data);
        }
        catch
        {
                TempData["Error"] = "Lỗi kết nối đến API.";
            return RedirectToAction(nameof(ManageNannyVerification));
        }
    }

    [Authorize(Roles = "Moderator")]
    [HttpPost("/Moderator/ReviewVerification/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReviewVerification(
        Guid id,
        int action,
        string? rejectionReason,
        Guid? nannyUserId = null)
    {
        var body = JsonSerializer.Serialize(new
        {
            action,
            rejectionReason = string.IsNullOrWhiteSpace(rejectionReason) ? null : rejectionReason.Trim()
        });

        var token = HttpContext.Session.GetString("AccessToken");
        var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/VerificationRequest/moderator-review-verification/{id}")
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
                if (nannyUserId.HasValue && nannyUserId.Value != Guid.Empty)
                {
                    await _notificationHub.Clients.Group($"user:{nannyUserId.Value}").SendAsync("notification:new", new
                    {
                        type = action == 2 ? "verification-approved" : "verification-rejected",
                    title = action == 2 ? "Yêu cầu xác minh đã được chấp thuận" : "Yêu cầu xác minh đã bị từ chối",
                        message = action == 2
                        ? "Yêu cầu xác minh của bạn đã được chấp thuận."
                        : "Yêu cầu xác minh của bạn đã bị từ chối.",
                        toastType = action == 2 ? "success" : "warning"
                    });
                }

                var listUrl = "/Moderator/ManageNannyVerification";
                var toastMessage = Uri.EscapeDataString("Bạn đã xử lý yêu cầu xác minh thành công");
                return Redirect($"{listUrl}?toastType=success&toastMessage={toastMessage}");
            }

            TempData["Error"] = result?.Message ?? "Xử lý thất bại.";
            return RedirectToAction(nameof(ManageNannyVerification));
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Lỗi kết nối: {ex.Message}";
            return RedirectToAction(nameof(ManageNannyVerification));
        }
    }

    [Authorize(Roles = "Nanny")]
    [HttpGet("VerificationRequest/NannyGetVerificationRequestList")]
    public async Task<IActionResult> NannyGetVerificationRequestList(int? status = null, int page = 1)
    {
        ViewBag.Status = status;
        AddAuthHeader();

        var queryParts = new List<string> { $"page={page}", "pageSize=10" };
        if (status.HasValue)
            queryParts.Add($"status={status.Value}");

        var response = await _http.GetAsync($"/api/VerificationRequest/nanny-view-verification-list?{string.Join("&", queryParts)}");
        if (!response.IsSuccessStatusCode)
        {
            return View(new VerificationRequestListResponse
            {
                Page = page,
                PageSize = 3
            });
        }

        var json = await response.Content.ReadAsStringAsync();
        var apiResult = JsonSerializer.Deserialize<ApiResult<VerificationRequestListResponse>>(json, JsonOptions);

        return View(apiResult?.Data ?? new VerificationRequestListResponse
        {
            Page = page,
            PageSize = 3
        });
    }

    [Authorize(Roles = "Nanny")]
    [HttpGet("VerificationRequest/NannyViewVerificationRequestDetail/{id:guid}")]
    public async Task<IActionResult> NannyViewVerificationRequestDetail(Guid id)
    {
        AddAuthHeader();

        var response = await _http.GetAsync($"/api/VerificationRequest/nanny-view-verification-detail/{id}");
        if (!response.IsSuccessStatusCode)
        {
            return RedirectToAction(nameof(NannyGetVerificationRequestList), new
            {
                toastType = "error",
                    toastMessage = "Không tìm thấy chi tiết yêu cầu xác minh."
            });
        }

        var json = await response.Content.ReadAsStringAsync();
        var apiResult = JsonSerializer.Deserialize<ApiResult<VerificationRequestDetailDto>>(json, JsonOptions);
        if (apiResult?.Success != true || apiResult.Data == null)
        {
            return RedirectToAction(nameof(NannyGetVerificationRequestList), new
            {
                toastType = "error",
                toastMessage = apiResult?.Message ?? "Không tìm thấy chi tiết yêu cầu xác minh."
            });
        }

        return View("~/Views/VerificationRequest/NannyViewVerificationRequestDetail.cshtml", apiResult.Data);
    }

    [Authorize(Roles = "Nanny")]
    [HttpGet("VerificationRequest/NannySubmitVerificationRequest")]
    public async Task<IActionResult> NannySubmitVerificationRequest()
    {
        var model = new SubmitVerificationRequestViewModel();
        if (!await PopulateProfileInfoAsync(model))
        {
            return RedirectToAction("Index", "Profile", new
            {
                toastType = "error",
                    toastMessage = "Vui lòng cập nhật hồ sơ cá nhân trước khi gửi yêu cầu."
            });
        }

        return View(model);
    }

    [Authorize(Roles = "Nanny")]
    [HttpPost("VerificationRequest/NannySubmitProfileVerificationRequest")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NannySubmitProfileVerificationRequest(SubmitVerificationRequestViewModel model)
    {
        if (!await PopulateProfileInfoAsync(model))
        {
            return RedirectToAction("Index", "Profile", new
            {
                toastType = "error",
                toastMessage = "Vui lòng cập nhật hồ sơ cá nhân trước khi gửi yêu cầu."
            });
        }

        ValidateUploadSection(
            model.IdentityCardFiles,
            nameof(model.IdentityCardFiles),
            "Ban phai tai anh cho muc can cuoc cong dan.",
            isRequired: true);
        ValidateIssueDate(
            model.IdentityCardIssueDate,
            nameof(model.IdentityCardIssueDate),
            "căn cước công dân");

        if (!ModelState.IsValid)
        {
            ViewData["ActiveTab"] = "identity-card-panel";
            return View("NannySubmitVerificationRequest", model);
        }

        try
        {
            var payload = await BuildSubmissionPayloadAsync(
                requestType: 1,
                files: model.IdentityCardFiles,
                documentType: VerificationDocumentType.IdentityCard,
                issueDate: model.IdentityCardIssueDate);

            return await SubmitVerificationPayloadAsync(payload, "Bạn đã gửi yêu cầu xác minh căn cước công dân thành công.");
        }
        catch (InvalidOperationException ex)
        {
            return RedirectToAction(nameof(NannySubmitVerificationRequest), new
            {
                toastType = "error",
                toastMessage = ex.Message,
                tab = "identity-card-panel"
            });
        }
        catch (RequestFailedException)
        {
            return RedirectToAction(nameof(NannySubmitVerificationRequest), new
            {
                toastType = "error",
                toastMessage = "Không thể upload tài liệu lên Azure Blob Storage. Vui lòng thử lại sau.",
                tab = "identity-card-panel"
            });
        }
    }

    [Authorize(Roles = "Nanny")]
    [HttpPost("VerificationRequest/NannySubmitDegreeCertificateRequest")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NannySubmitDegreeCertificateRequest(SubmitVerificationRequestViewModel model)
    {
        if (!await PopulateProfileInfoAsync(model))
        {
            return RedirectToAction("Index", "Profile", new
            {
                toastType = "error",
                toastMessage = "Vui lòng cập nhật hồ sơ cá nhân trước khi gửi yêu cầu."
            });
        }

        ValidateUploadSection(
            model.CertificateFiles,
            nameof(model.CertificateFiles),
            "Nếu muốn gửi yêu cầu bằng cấp/chứng chỉ, bạn phải tải lên ít nhất một file.",
            isRequired: true);
        ValidateIssueDate(
            model.CertificateIssueDate,
            nameof(model.CertificateIssueDate),
            "bằng cấp/chứng chỉ");

        if (!ModelState.IsValid)
        {
            ViewData["ActiveTab"] = "degree-certificate-panel";
            return View("NannySubmitVerificationRequest", model);
        }

        try
        {
            var payload = await BuildSubmissionPayloadAsync(
                requestType: 3,
                files: model.CertificateFiles,
                documentType: VerificationDocumentType.DegreeCertificate,
                issueDate: model.CertificateIssueDate);

            return await SubmitVerificationPayloadAsync(payload, "Bạn đã gửi yêu cầu xác minh bằng cấp/chứng chỉ thành công.");
        }
        catch (InvalidOperationException ex)
        {
            return RedirectToAction(nameof(NannySubmitVerificationRequest), new
            {
                toastType = "error",
                toastMessage = ex.Message,
                tab = "degree-certificate-panel"
            });
        }
        catch (RequestFailedException)
        {
            return RedirectToAction(nameof(NannySubmitVerificationRequest), new
            {
                toastType = "error",
                toastMessage = "Không thể upload tài liệu lên Azure Blob Storage. Vui lòng thử lại sau.",
                tab = "degree-certificate-panel"
            });
        }
    }

    [Authorize(Roles = "Nanny")]
    [HttpPost("VerificationRequest/NannySubmitHealthCertificateRequest")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NannySubmitHealthCertificateRequest(SubmitVerificationRequestViewModel model)
    {
        if (!await PopulateProfileInfoAsync(model))
        {
            return RedirectToAction("Index", "Profile", new
            {
                toastType = "error",
                toastMessage = "Vui lòng cập nhật profile cá nhân truoc khi gui yêu cầu."
            });
        }

        ValidateUploadSection(
            model.HealthCertificateFiles,
            nameof(model.HealthCertificateFiles),
            "Bạn phải upload ảnh cho mục giấy khám sức khỏe.",
            isRequired: true);
        ValidateIssueDate(
            model.HealthCertificateIssueDate,
            nameof(model.HealthCertificateIssueDate),
            "giấy khám sức khỏe",
            earliestAllowedDate: DateTime.UtcNow.Date.AddMonths(-12),
            earliestDateValidationMessage: "Ngày cấp của giấy khám sức khỏe phải trong vòng 12 tháng gần nhất.");

        if (!ModelState.IsValid)
        {
            ViewData["ActiveTab"] = "health-certificate-panel";
            return View("NannySubmitVerificationRequest", model);
        }

        try
        {
            var payload = await BuildSubmissionPayloadAsync(
                requestType: 2,
                files: model.HealthCertificateFiles,
                documentType: VerificationDocumentType.HealthCertificate,
                issueDate: model.HealthCertificateIssueDate);

            return await SubmitVerificationPayloadAsync(payload, "Bạn đã gửi yêu cầu giấy khám sức khỏe thành công.");
        }
        catch (InvalidOperationException ex)
        {
            return RedirectToAction(nameof(NannySubmitVerificationRequest), new
            {
                toastType = "error",
                toastMessage = ex.Message,
                tab = "health-certificate-panel"
            });
        }
        catch (RequestFailedException)
        {
            return RedirectToAction(nameof(NannySubmitVerificationRequest), new
            {
                toastType = "error",
                toastMessage = "Không thể tải tài liệu lên. Vui lòng thử lại sau.",
                tab = "health-certificate-panel"
            });
        }
    }

    private void AddAuthHeader()
    {
        var token = HttpContext.Session.GetString("AccessToken");
        if (!string.IsNullOrEmpty(token))
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private async Task<object> BuildSubmissionPayloadAsync(
        int requestType,
        List<IFormFile>? files,
        VerificationDocumentType documentType,
        DateTime? issueDate)
    {
        var documents = new List<object>();
        await AddDocumentsAsync(files, documentType, documents, issueDate);

        return new
        {
            RequestType = requestType,
            Documents = documents
        };
    }

    private async Task<IActionResult> SubmitVerificationPayloadAsync(object payload, string successMessage)
    {
        AddAuthHeader();
        var payloadJson = JsonSerializer.Serialize(payload);
        var jsonContent = new StringContent(payloadJson, Encoding.UTF8, "application/json");
        var response = await _http.PostAsync("/api/VerificationRequest/nanny-submit-verification", jsonContent);

        var json = await response.Content.ReadAsStringAsync();
        ApiResult? result;
        try
        {
            result = JsonSerializer.Deserialize<ApiResult>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return RedirectToAction(nameof(NannySubmitVerificationRequest), new
            {
                toastType = "error",
                toastMessage = "Lỗi hệ thống từ máy chủ. Vui lòng thử lại sau."
            });
        }

        if (!response.IsSuccessStatusCode || result == null || !result.Success)
        {
            return RedirectToAction(nameof(NannySubmitVerificationRequest), new
            {
                toastType = "error",
                toastMessage = result?.Message ?? "Gửi yêu cầu thất bại."
            });
        }

        await _notificationHub.Clients.Group("role:Moderator").SendAsync("notification:new", new
        {
            type = "verification-request-submitted",
            title = "Có yêu cầu xác minh mới",
            message = "Một bảo mẫu vừa gửi yêu cầu xác minh mới.",
            toastType = "info"
        });

        return RedirectToAction(nameof(NannyGetVerificationRequestList), new
        {
            toastType = "success",
            toastMessage = successMessage
        });
    }

    private async Task<bool> PopulateProfileInfoAsync(SubmitVerificationRequestViewModel model)
    {
        AddAuthHeader();
        var response = await _http.GetAsync("/api/profile");
        if (!response.IsSuccessStatusCode)
        {
            return false;
        }

        var json = await response.Content.ReadAsStringAsync();
        var profileResult = JsonSerializer.Deserialize<ApiResult<PersonalProfileViewModel>>(json, JsonOptions);
        var profile = profileResult?.Data;
        if (profile == null)
        {
            return false;
        }

        model.NannyFirstName = profile.FirstName;
        model.NannyLastName = profile.LastName;
        model.NannyEmail = profile.Email;
        model.NannyAvatarUrl = profile.AvatarUrl;
        model.NannyCity = profile.City;
        model.NannyAddress = profile.Address;
        model.PhoneNumber = profile.PhoneNumber;
        return true;
    }

    private void ValidateUploadSection(
        List<IFormFile>? files,
        string fieldName,
        string? requiredMessage,
        bool isRequired)
    {
        if (files == null || files.Count == 0)
        {
            if (isRequired && !string.IsNullOrWhiteSpace(requiredMessage))
                ModelState.AddModelError(fieldName, requiredMessage);

            return;
        }

        foreach (var file in files)
        {
            if (!IsSupportedDocument(file))
            {
                ModelState.AddModelError(fieldName, "Bạn phải tải lên tệp định dạng ảnh hoặc PDF.");
                return;
            }

            if (file.Length > MaxDocumentSizeInBytes)
            {
                ModelState.AddModelError(fieldName, "Bạn chỉ được tải lên tệp có dung lượng tối đa 5MB.");
                return;
            }
        }
    }

    private void ValidateIssueDate(
        DateTime? issueDate,
        string fieldName,
        string documentLabel,
        DateTime? earliestAllowedDate = null,
        string? earliestDateValidationMessage = null)
    {
        if (!issueDate.HasValue)
        {
            ModelState.AddModelError(fieldName, $"Bạn phải chọn ngày cấp cho {documentLabel}.");
            return;
        }

        var date = issueDate.Value.Date;
        if (date > DateTime.UtcNow.Date)
        {
            ModelState.AddModelError(fieldName, $"Ngày cấp của {documentLabel} không được lớn hơn ngày hiện tại.");
            return;
        }

        var minAllowedDate = earliestAllowedDate?.Date ?? new DateTime(1900, 1, 1);
        if (date < minAllowedDate)
        {
            ModelState.AddModelError(
                fieldName,
                earliestDateValidationMessage ?? $"Ngày cấp của {documentLabel} không hợp lệ.");
        }
    }

    private static bool IsSupportedDocument(IFormFile file)
    {
        if (file == null || string.IsNullOrWhiteSpace(file.FileName))
        {
            return false;
        }

        var extension = Path.GetExtension(file.FileName);
        if (!AllowedDocumentExtensions.Contains(extension))
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(file.ContentType)
            || file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
            || string.Equals(file.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase);
    }

    private async Task AddDocumentsAsync(
        List<IFormFile>? files,
        VerificationDocumentType documentType,
        List<object> documents,
        DateTime? issueDate = null)
    {
        if (files == null || files.Count == 0)
        {
            return;
        }

        foreach (var file in files)
        {
            var documentUrl = await _storageService.UploadVerificationDocumentAsync(file, documentType);

            documents.Add(new
            {
                DocumentType = (int)documentType,
                DocumentUrl = documentUrl,
                FileName = file.FileName,
                FileSize = (int)file.Length,
                ExpiryDate = issueDate
            });
        }
    }
}
