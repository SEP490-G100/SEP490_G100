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
            TempData["Error"] = "Khong the tai danh sach xac minh.";
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
                TempData["Error"] = "Khong tim thay yeu cau xac minh.";
                return RedirectToAction(nameof(ManageNannyVerification));
            }

            return View("~/Views/Moderator/NannyVerification/ViewNannyVerificationDetail.cshtml", result.Data);
        }
        catch
        {
            TempData["Error"] = "Loi ket noi den API.";
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
                        title = action == 2 ? "Yeu cau xac minh da duoc chap thuan" : "Yeu cau xac minh da bi tu choi",
                        message = action == 2
                            ? "Yeu cau xac minh cua ban da duoc chap thuan."
                            : "Yeu cau xac minh cua ban da bi tu choi.",
                        toastType = action == 2 ? "success" : "warning"
                    });
                }

                var listUrl = "/Moderator/ManageNannyVerification";
                var toastMessage = Uri.EscapeDataString("Ban da xu ly yeu cau xac minh thanh cong");
                return Redirect($"{listUrl}?toastType=success&toastMessage={toastMessage}");
            }

            TempData["Error"] = result?.Message ?? "Xu ly that bai.";
            return RedirectToAction(nameof(ManageNannyVerification));
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Loi ket noi: {ex.Message}";
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
                toastMessage = "Khong tim thay chi tiet yeu cau xac minh."
            });
        }

        var json = await response.Content.ReadAsStringAsync();
        var apiResult = JsonSerializer.Deserialize<ApiResult<VerificationRequestDetailDto>>(json, JsonOptions);
        if (apiResult?.Success != true || apiResult.Data == null)
        {
            return RedirectToAction(nameof(NannyGetVerificationRequestList), new
            {
                toastType = "error",
                toastMessage = apiResult?.Message ?? "Khong tim thay chi tiet yeu cau xac minh."
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
                toastMessage = "Vui long cap nhat ho so ca nhan truoc khi gui yeu cau."
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
                toastMessage = "Vui long cap nhat ho so ca nhan truoc khi gui yeu cau."
            });
        }

        ValidateUploadSection(
            model.IdentityCardFiles,
            nameof(model.IdentityCardFiles),
            "Ban phai tai anh cho muc can cuoc cong dan.",
            isRequired: true);
        ValidateUploadSection(
            model.CertificateFiles,
            nameof(model.CertificateFiles),
            requiredMessage: null,
            isRequired: false);

        if (!ModelState.IsValid)
            return View("NannySubmitVerificationRequest", model);

        try
        {
            var payload = await BuildSubmissionPayloadAsync(
                model,
                requestType: 1,
                includeIdentity: true,
                includeHealthCertificate: false,
                includeDegreeCertificate: true);

            return await SubmitVerificationPayloadAsync(payload, "Bạn đã gửi yêu cầu xác minh hồ sơ thành công.");
        }
        catch (InvalidOperationException ex)
        {
            return RedirectToAction(nameof(NannySubmitVerificationRequest), new
            {
                toastType = "error",
                toastMessage = ex.Message
            });
        }
        catch (RequestFailedException)
        {
            return RedirectToAction(nameof(NannySubmitVerificationRequest), new
            {
                toastType = "error",
                toastMessage = "Không thể upload tài liệu lên Azure Blob Storage. Vui lòng thử lại sau."
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

        if (!model.HealthCertificateExpiryDate.HasValue)
        {
            ModelState.AddModelError(nameof(model.HealthCertificateExpiryDate), "Bạn phải chọn ngày hết hạn cho giấy khám sức khỏe.");
        }
        else if (model.HealthCertificateExpiryDate.Value.Date <= DateTime.UtcNow.Date)
        {
            ModelState.AddModelError(nameof(model.HealthCertificateExpiryDate), "Ngày hết hạn phải lớn hơn ngày hiện tại.");
        }

        if (!ModelState.IsValid)
        {
            return View("NannySubmitVerificationRequest", model);
        }

        try
        {
            var payload = await BuildSubmissionPayloadAsync(
                model,
                requestType: 2,
                includeIdentity: false,
                includeHealthCertificate: true,
                includeDegreeCertificate: false);

            return await SubmitVerificationPayloadAsync(payload, "Ban da gui yeu cau giay kham suc khoe thanh cong.");
        }
        catch (InvalidOperationException ex)
        {
            return RedirectToAction(nameof(NannySubmitVerificationRequest), new
            {
                toastType = "error",
                toastMessage = ex.Message
            });
        }
        catch (RequestFailedException)
        {
            return RedirectToAction(nameof(NannySubmitVerificationRequest), new
            {
                toastType = "error",
                toastMessage = "Không thể tải tài liệu lên. Vui lòng thử lại sau."
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
        SubmitVerificationRequestViewModel model,
        int requestType,
        bool includeIdentity,
        bool includeHealthCertificate,
        bool includeDegreeCertificate)
    {
        var documents = new List<object>();

        if (includeIdentity)
        {
            await AddDocumentsAsync(model.IdentityCardFiles, VerificationDocumentType.IdentityCard, documents);
        }

        if (includeHealthCertificate)
        {
            await AddDocumentsAsync(model.HealthCertificateFiles, VerificationDocumentType.HealthCertificate, documents, model.HealthCertificateExpiryDate);
        }

        if (includeDegreeCertificate)
        {
            await AddDocumentsAsync(model.CertificateFiles, VerificationDocumentType.DegreeCertificate, documents);
        }

        return new
        {
            RequestType = requestType,
            HealthCertificateExpiryDate = model.HealthCertificateExpiryDate,
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
        DateTime? expiryDate = null)
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
                ExpiryDate = expiryDate
            });
        }
    }
}
