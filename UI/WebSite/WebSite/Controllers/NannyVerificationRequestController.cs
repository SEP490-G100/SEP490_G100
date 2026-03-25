using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Azure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebSite.Enums;
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
        IVerificationDocumentStorageService storageService)
    {
        _http = httpFactory.CreateClient("BackendApi");
        _storageService = storageService;
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
    public async Task<IActionResult> Index()
    {
        AddAuthHeader();
        var response = await _http.GetAsync("/api/NannyVerificationRequest/nanny-requests");
        if (!response.IsSuccessStatusCode)
        {
            TempData["Error"] = "Không thể lấy danh sách yêu cầu xác minh.";
            return View(new List<VerificationRequestListViewModel>());
        }

        var json = await response.Content.ReadAsStringAsync();
        var apiResult = JsonSerializer.Deserialize<ApiResult<List<VerificationRequestListViewModel>>>(json, JsonOptions);

        return View(apiResult?.Data ?? new List<VerificationRequestListViewModel>());
    }

    [HttpGet]
    public async Task<IActionResult> Submit()
    {
        var model = new SubmitVerificationRequestViewModel();
        if (!await PopulateProfileInfoAsync(model))
        {
            TempData["Error"] = "Vui lòng cập nhật hồ sơ cá nhân trước khi gửi yêu cầu.";
            return RedirectToAction("Index", "Profile");
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(SubmitVerificationRequestViewModel model)
    {
        if (!await PopulateProfileInfoAsync(model))
        {
            TempData["Error"] = "Vui lòng cập nhật hồ sơ cá nhân trước khi gửi yêu cầu.";
            return RedirectToAction("Index", "Profile");
        }

        ValidateUploadSection(
            model.IdentityCardFiles,
            nameof(model.IdentityCardFiles),
            "Bạn phải upload ảnh cho mục căn cước công dân.",
            isRequired: true);
        ValidateUploadSection(
            model.CertificateFiles,
            nameof(model.CertificateFiles),
            "Bạn phải upload ảnh cho mục bằng cấp và chứng chỉ.",
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

        var docs = new List<object>();
        try
        {
            await AddDocumentsAsync(model.IdentityCardFiles, VerificationDocumentType.IdentityCard, docs);
            await AddDocumentsAsync(model.CertificateFiles, VerificationDocumentType.DegreeCertificate, docs);
            await AddDocumentsAsync(model.HealthCertificateFiles, VerificationDocumentType.HealthCertificate, docs);
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction("Submit");
        }
        catch (RequestFailedException)
        {
            TempData["Error"] = "Không thể upload tài liệu lên Azure Blob Storage. Vui lòng thử lại sau.";
            return RedirectToAction("Submit");
        }

        var payload = JsonSerializer.Serialize(new { Documents = docs });
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
            TempData["Error"] = "Lỗi hệ thống từ server. Vui lòng thử lại sau.";
            return RedirectToAction("Submit");
        }

        if (!response.IsSuccessStatusCode || result == null || !result.Success)
        {
            TempData["Error"] = result?.Message ?? "Gửi yêu cầu thất bại.";
            return RedirectToAction("Submit");
        }

        TempData["Success"] = "Gửi yêu cầu xác minh thành công.";
        return RedirectToAction("Index");
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
                ModelState.AddModelError(fieldName, "Bạn phải upload file định dạng ảnh hoặc pdf.");
                return;
            }

            if (file.Length > MaxDocumentSizeInBytes)
            {
                ModelState.AddModelError(fieldName, "Bạn chỉ đc upload file kích thước tối đa 5mb.");
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
        List<object> docs)
    {
        if (files == null || files.Count == 0)
        {
            return;
        }

        foreach (var file in files)
        {
            var documentUrl = await _storageService.UploadAsync(file, documentType);

            docs.Add(new
            {
                DocumentType = (int)documentType,
                DocumentUrl = documentUrl,
                FileName = file.FileName,
                FileSize = (int)file.Length
            });
        }
    }
}
