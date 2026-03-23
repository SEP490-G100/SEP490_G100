using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebSite.Models;
using WebSite.Models.Profile;
using WebSite.Models.Verification;

namespace WebSite.Controllers;

[Authorize(Roles = "Nanny")]
public class NannyVerificationRequestController : Controller
{
    private readonly HttpClient _http;
    private readonly Microsoft.AspNetCore.Hosting.IWebHostEnvironment _env;
    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public NannyVerificationRequestController(IHttpClientFactory httpFactory, Microsoft.AspNetCore.Hosting.IWebHostEnvironment env)
    {
        _http = httpFactory.CreateClient("BackendApi");
        _env = env;
    }

    private void AddAuthHeader()
    {
        var token = HttpContext.Session.GetString("AccessToken");
        if (!string.IsNullOrEmpty(token))
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
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
        var apiResult = JsonSerializer.Deserialize<ApiResult<List<VerificationRequestListViewModel>>>(json, _jsonOptions);

        return View(apiResult?.Data ?? new List<VerificationRequestListViewModel>());
    }

    [HttpGet]
    public async Task<IActionResult> Submit()
    {
        AddAuthHeader();
        var response = await _http.GetAsync("/api/profile");
        if (!response.IsSuccessStatusCode)
        {
            TempData["Error"] = "Vui lòng cập nhật hồ sơ cá nhân trước khi gửi yêu cầu.";
            return RedirectToAction("Index", "Profile");
        }

        var json = await response.Content.ReadAsStringAsync();
        var profileResult = JsonSerializer.Deserialize<ApiResult<PersonalProfileViewModel>>(json, _jsonOptions);
        var profile = profileResult?.Data;

        if (profile == null)
            return RedirectToAction("Index", "Profile");

        var model = new SubmitVerificationRequestViewModel
        {
            NannyFirstName = profile.FirstName,
            NannyLastName = profile.LastName,
            NannyEmail = profile.Email,
            NannyAvatarUrl = profile.AvatarUrl,
            NannyCity = profile.City,
            NannyAddress = profile.Address,
            PhoneNumber = profile.PhoneNumber
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(SubmitVerificationRequestViewModel model)
    {
        AddAuthHeader();

        if (model.Files == null || !model.Files.Any())
        {
            ModelState.AddModelError("", "Bạn phải tải lên ít nhất một tài liệu.");
            return View(model); // Pre-fill lost, ideally we'd re-fetch profile to keep read-only data
        }

        var uploadsFolder = Path.Combine(_env.WebRootPath, "img", "verificationRequest");
        Directory.CreateDirectory(uploadsFolder);

        var docs = new List<object>();

        foreach (var file in model.Files)
        {
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            var newFileName = $"{Guid.NewGuid()}{ext}";
            var filePath = Path.Combine(uploadsFolder, newFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            docs.Add(new {
                DocumentUrl = $"/img/verificationRequest/{newFileName}",
                FileName = file.FileName,
                FileSize = (int)file.Length
            });
        }

        var jsonContent = new StringContent(JsonSerializer.Serialize(new { Documents = docs }), System.Text.Encoding.UTF8, "application/json");
        var response = await _http.PostAsync("/api/NannyVerificationRequest/submit", jsonContent);
        
        var json = await response.Content.ReadAsStringAsync();
        ApiResult result = null;
        try
        {
            result = JsonSerializer.Deserialize<ApiResult>(json, _jsonOptions);
        }
        catch (JsonException)
        {
            // Log the actual response for debugging if needed, but show user-friendly message
            TempData["Error"] = "Lỗi hệ thống từ server. Vui lòng thử lại sau.";
            return RedirectToAction("Submit");
        }

        if (!response.IsSuccessStatusCode || result == null || !result.Success)
        {
            TempData["Error"] = result?.Message ?? "Gửi yêu cầu thất bại.";
            return RedirectToAction("Submit"); // Redirect to reload profile data
        }

        TempData["Success"] = "Gửi yêu cầu xác minh thành công.";
        return RedirectToAction("Index");
    }
}
