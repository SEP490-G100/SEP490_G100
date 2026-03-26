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
    private readonly IVerificationDocumentStorageService _storageService;
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
        IVerificationDocumentStorageService storageService,
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

    [HttpGet]
    public async Task<IActionResult> Index(int? status = null, int page = 1)
    {
        ViewBag.Status = status;
        AddAuthHeader();

        var queryParts = new List<string> { $"page={page}", "pageSize=3" };
        if (status.HasValue)
        {
            queryParts.Add($"status={status.Value}");
        }

        var response = await _http.GetAsync($"/api/NannyVerificationRequest/nanny-requests?{string.Join("&", queryParts)}");
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

    [HttpGet]
    public async Task<IActionResult> NannyViewVerificationDetail(Guid id)
    {
        AddAuthHeader();

        var response = await _http.GetAsync($"/api/NannyVerificationRequest/nanny-requests/{id}");
        if (!response.IsSuccessStatusCode)
        {
            return RedirectToAction(nameof(Index), new
            {
                toastType = "error",
                toastMessage = "Khong tim thay chi tiet yeu cau xac minh."
            });
        }

        var json = await response.Content.ReadAsStringAsync();
        var apiResult = JsonSerializer.Deserialize<ApiResult<VerificationRequestDetailDto>>(json, JsonOptions);
        if (apiResult?.Success != true || apiResult.Data == null)
        {
            return RedirectToAction(nameof(Index), new
            {
                toastType = "error",
                toastMessage = apiResult?.Message ?? "Khong tim thay chi tiet yeu cau xac minh."
            });
        }

        return View("~/Views/NannyVerificationRequest/NannyViewVerificationDetail.cshtml", apiResult.Data);
    }

    [HttpGet]
    public async Task<IActionResult> Submit()
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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(SubmitVerificationRequestViewModel model)
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
            model.CertificateFiles,
            nameof(model.CertificateFiles),
            "Ban phai upload anh cho muc bang cap va chung chi.",
            isRequired: true);
        ValidateUploadSection(
            model.HealthCertificateFiles,
            nameof(model.HealthCertificateFiles),
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
            await AddDocumentsAsync(model.CertificateFiles, VerificationDocumentType.DegreeCertificate, documents);
            await AddDocumentsAsync(model.HealthCertificateFiles, VerificationDocumentType.HealthCertificate, documents);
        }
        catch (InvalidOperationException ex)
        {
            return RedirectToAction(nameof(Submit), new
            {
                toastType = "error",
                toastMessage = ex.Message
            });
        }
        catch (RequestFailedException)
        {
            return RedirectToAction(nameof(Submit), new
            {
                toastType = "error",
                toastMessage = "Khong the upload tai lieu len Azure Blob Storage. Vui long thu lai sau."
            });
        }

        var payload = JsonSerializer.Serialize(new { Documents = documents });
        var jsonContent = new StringContent(payload, Encoding.UTF8, "application/json");
        var response = await _http.PostAsync("/api/NannyVerificationRequest/submit", jsonContent);

        var json = await response.Content.ReadAsStringAsync();
        ApiResult? result;
        try
        {
            result = JsonSerializer.Deserialize<ApiResult>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return RedirectToAction(nameof(Submit), new
            {
                toastType = "error",
                toastMessage = "Loi he thong tu server. Vui long thu lai sau."
            });
        }

        if (!response.IsSuccessStatusCode || result == null || !result.Success)
        {
            return RedirectToAction(nameof(Submit), new
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

        var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrWhiteSpace(currentUserId))
        {
            await _notificationHub.Clients.Group($"user:{currentUserId}").SendAsync("notification:new", new
            {
                type = "verification-request-created",
                title = "Ban vua gui yeu cau xac minh thanh cong",
                message = "Ban vua gui yeu cau xac minh thanh cong.",
                toastType = "success"
            });
        }

        return RedirectToAction(nameof(Index), new
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
            var documentUrl = await _storageService.UploadAsync(file, documentType);

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
