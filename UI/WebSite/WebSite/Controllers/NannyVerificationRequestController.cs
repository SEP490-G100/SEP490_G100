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

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> NannyGetVerificationRequests(int? status = null, int page = 1)
    {
        ViewBag.Status = status;
        AddAuthHeader();

        var queryParts = new List<string> { $"page={page}", "pageSize=3" };
        if (status.HasValue)
        {
            queryParts.Add($"status={status.Value}");
        }

        var response = await _http.GetAsync($"/api/NannyVerificationRequest/nanny-verification-requests?{string.Join("&", queryParts)}");
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

    [HttpGet("NannyGetVerificationRequestDetail/{id:guid}")]
    public async Task<IActionResult> NannyGetVerificationRequestDetail(Guid id)
    {
        AddAuthHeader();

        var response = await _http.GetAsync($"/api/NannyVerificationRequest/nanny-verification-requests/{id}");
        if (!response.IsSuccessStatusCode)
        {
            return RedirectToAction(nameof(NannyGetVerificationRequests), new
            {
                toastType = "error",
                toastMessage = "Khong tim thay chi tiet yeu cau xac minh."
            });
        }

        var json = await response.Content.ReadAsStringAsync();
        var apiResult = JsonSerializer.Deserialize<ApiResult<VerificationRequestDetailDto>>(json, JsonOptions);
        if (apiResult?.Success != true || apiResult.Data == null)
        {
            return RedirectToAction(nameof(NannyGetVerificationRequests), new
            {
                toastType = "error",
                toastMessage = apiResult?.Message ?? "Khong tim thay chi tiet yeu cau xac minh."
            });
        }

        return View("~/Views/NannyVerificationRequest/NannyViewVerificationDetail.cshtml", apiResult.Data);
    }

    [HttpGet("NannySubmitVerificationRequest")]
    [HttpGet("Submit")]
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

    [HttpPost("NannySubmitVerificationRequest")]
    [HttpPost("Submit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NannySubmitVerificationRequest(SubmitVerificationRequestViewModel model)
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
            "Ban phai upload anh cho muc can cuoc cong dan.",
            isRequired: true);
        ValidateUploadSection(
            model.HealthCertificateFiles,
            nameof(model.HealthCertificateFiles),
            "Ban phai upload anh cho muc giay kham suc khoe.",
            isRequired: true);
        ValidateUploadSection(
            model.CertificateFiles,
            nameof(model.CertificateFiles),
            requiredMessage: null,
            isRequired: false);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        AddAuthHeader();

        var documents = new List<object>();
        try
        {
            await AddDocumentsAsync(model.IdentityCardFiles, VerificationDocumentType.IdentityCard, documents);
            await AddDocumentsAsync(model.HealthCertificateFiles, VerificationDocumentType.HealthCertificate, documents);
            await AddDocumentsAsync(model.CertificateFiles, VerificationDocumentType.DegreeCertificate, documents);
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
                toastMessage = "Khong the upload tai lieu len Azure Blob Storage. Vui long thu lai sau."
            });
        }

        var payload = JsonSerializer.Serialize(new { Documents = documents });
        var jsonContent = new StringContent(payload, Encoding.UTF8, "application/json");
        var response = await _http.PostAsync("/api/NannyVerificationRequest/nanny-submit-verification-request", jsonContent);

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
                toastMessage = "Loi he thong tu server. Vui long thu lai sau."
            });
        }

        if (!response.IsSuccessStatusCode || result == null || !result.Success)
        {
            return RedirectToAction(nameof(NannySubmitVerificationRequest), new
            {
                toastType = "error",
                toastMessage = result?.Message ?? "Gui yeu cau that bai."
            });
        }

        await _notificationHub.Clients.Group("role:Moderator").SendAsync("notification:new", new
        {
            type = "verification-request-submitted",
            title = "Co yeu cau xac minh moi",
            message = "Mot nanny vua gui yeu cau xac minh moi.",
            toastType = "info"
        });

        return RedirectToAction(nameof(NannyGetVerificationRequests), new
        {
            toastType = "success",
            toastMessage = "Ban da gui yeu cau xac minh thanh cong."
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
                ModelState.AddModelError(fieldName, "Ban phai upload file dinh dang anh hoac pdf.");
                return;
            }

            if (file.Length > MaxDocumentSizeInBytes)
            {
                ModelState.AddModelError(fieldName, "Ban chi duoc upload file kich thuoc toi da 5mb.");
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
        List<object> documents)
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
                FileSize = (int)file.Length
            });
        }
    }
}
