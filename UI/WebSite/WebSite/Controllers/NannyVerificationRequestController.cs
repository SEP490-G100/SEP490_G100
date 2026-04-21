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

[Authorize(Roles = "Nanny")]
[Route("[controller]")]
public class NannyVerificationRequestController : Controller
{
    private readonly HttpClient _http;
    private readonly IAzureBlobStorageService _storageService;
    private readonly IHubContext<NotificationHub> _notificationHub;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly HashSet<string> AllowedDocumentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp",
        ".pdf"
    };
    private const long MaxDocumentSizeInBytes = 5 * 1024 * 1024;

    public NannyVerificationRequestController(
        IHttpClientFactory httpFactory,
        IAzureBlobStorageService storageService,
        IHubContext<NotificationHub> notificationHub)
    {
        _http = httpFactory.CreateClient("BackendApi");
        _storageService = storageService;
        _notificationHub = notificationHub;
    }

    private void AddAuthHeader()
    {
        var token = HttpContext.Session.GetString("AccessToken");
        if (!string.IsNullOrEmpty(token))
        {
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }

    [HttpGet("NannyGetVerificationRequestList")]
    public async Task<IActionResult> NannyGetVerificationRequestList(int? status = null, int page = 1)
    {
        ViewBag.Status = status;
        AddAuthHeader();

        var queryParts = new List<string> { $"page={page}", "pageSize=3" };
        if (status.HasValue)
        {
            queryParts.Add($"status={status.Value}");
        }

        var response = await _http.GetAsync($"/api/NannyVerificationRequest/nanny-view-verification-list?{string.Join("&", queryParts)}");
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

    [HttpGet("NannyViewVerificationRequestDetail/{id:guid}")]
    public async Task<IActionResult> NannyViewVerificationRequestDetail(Guid id)
    {
        AddAuthHeader();

        var response = await _http.GetAsync($"/api/NannyVerificationRequest/nanny-view-verification-detail/{id}");
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

        return View("~/Views/NannyVerificationRequest/NannyViewVerificationRequestDetail.cshtml", apiResult.Data);
    }

    [HttpGet("NannySubmitVerificationRequest")]
    [HttpGet("verifyNanny")]
    [HttpGet("vetifyNanny")]
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

    [HttpPost("NannySubmitProfileVerificationRequest")]
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
            "Bạn phải tải ảnh cho mục căn cước công dân.",
            isRequired: true);
        ValidateUploadSection(
            model.CertificateFiles,
            nameof(model.CertificateFiles),
            requiredMessage: null,
            isRequired: false);

        if (!ModelState.IsValid)
        {
            return View("NannySubmitVerificationRequest", model);
        }

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

    [HttpPost("NannySubmitHealthCertificateRequest")]
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

            return await SubmitVerificationPayloadAsync(payload, "Bạn đã gửi yêu cầu giấy khám sức khỏe thành công.");
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
        var response = await _http.PostAsync("/api/NannyVerificationRequest/nanny-submit-verification", jsonContent);

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
            toastMessage = "Bạn đã gửi yêu cầu xác minh thành công."
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
            {
                ModelState.AddModelError(fieldName, requiredMessage);
            }

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
