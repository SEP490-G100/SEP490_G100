using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebSite.Models.Profile;

namespace WebSite.Controllers;

[Authorize]
public class ProfileController : Controller
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public ProfileController(IHttpClientFactory httpFactory)
    {
        _http = httpFactory.CreateClient("BackendApi");
    }

    // Helper method to get token from session
    private string? GetTokenFromSession()
    {
        return HttpContext.Session.GetString("AccessToken");
    }

    // Get and display personal profile
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        try
        {
            var token = GetTokenFromSession();
            if (string.IsNullOrEmpty(token))
                return RedirectToAction("Login", "Auth");

            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _http.GetAsync("/api/profile");
            if (!response.IsSuccessStatusCode)
                return RedirectToAction("Login", "Auth");

            var content = await response.Content.ReadAsStringAsync();
            var apiResult = JsonSerializer.Deserialize<ApiResultDto>(content, JsonOpts);

            if (apiResult == null || !apiResult.Success)
                return BadRequest(apiResult?.Message ?? "Không thể tải hồ sơ.");

            var profile = JsonSerializer.Deserialize<PersonalProfileViewModel>(
                JsonSerializer.Serialize(apiResult.Data), JsonOpts);

            return View(profile);
        }
        catch (Exception ex)
        {
            TempData["Error"] = "Lỗi khi tải hồ sơ: " + ex.Message;
            return RedirectToAction("Index", "Home");
        }
    }

    // Edit personal information
    [HttpGet]
    public async Task<IActionResult> Edit()
    {
        try
        {
            var token = GetTokenFromSession();
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _http.GetAsync("/api/profile");
            if (!response.IsSuccessStatusCode)
                return RedirectToAction("Login", "Auth");

            var content = await response.Content.ReadAsStringAsync();
            var apiResult = JsonSerializer.Deserialize<ApiResultDto>(content, JsonOpts);

            var profile = JsonSerializer.Deserialize<EditPersonalInfoViewModel>(
                JsonSerializer.Serialize(apiResult?.Data), JsonOpts);

            return View(profile);
        }
        catch (Exception ex)
        {
            TempData["Error"] = "Lỗi khi tải hồ sơ: " + ex.Message;
            return RedirectToAction("Index");
        }
    }

[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Edit(EditPersonalInfoViewModel model)
{
    if (!ModelState.IsValid)
        return View(model);

    try
    {
        var token = GetTokenFromSession();
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var updateRequest = new
            {
                model.FirstName,
                model.LastName,
                model.PhoneNumber,
                model.AvatarUrl,
                model.DateOfBirth,
                model.Gender,
                model.Address,
                model.City,
                model.District,
                model.Ward,
                model.Latitude,
                model.Longitude
            };

            var response = await _http.PutAsJsonAsync("/api/profile", updateRequest);
            var content = await response.Content.ReadAsStringAsync();
            var apiResult = JsonSerializer.Deserialize<ApiResultDto>(content, JsonOpts);

            if (apiResult == null || !apiResult.Success)
            {
                ModelState.AddModelError("", apiResult?.Message ?? "Cập nhật thất bại.");
                return View(model);
            }

            TempData["Success"] = "Cập nhật thông tin cá nhân thành công.";
            return RedirectToAction("Index");
        }
        catch (Exception ex)
        {
            TempData["Error"] = "Lỗi khi cập nhật: " + ex.Message;
            return View(model);
        }
    }

    // View child profiles
    [HttpGet]
    public async Task<IActionResult> Children()
    {
        try
        {
            var token = GetTokenFromSession();
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _http.GetAsync("/api/profile/children");
            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                TempData["Error"] = "Bạn không có quyền xem danh sách con em.";
                return RedirectToAction("Index");
            }

            if (!response.IsSuccessStatusCode)
                return BadRequest();

            var content = await response.Content.ReadAsStringAsync();
            var apiResult = JsonSerializer.Deserialize<ApiResultDto>(content, JsonOpts);

            var children = new List<ChildProfileViewModel>();
            if (apiResult?.Data is System.Text.Json.JsonElement element && element.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                children = JsonSerializer.Deserialize<List<ChildProfileViewModel>>(element.GetRawText(), JsonOpts) ?? new();
            }

            return View(children);
        }
        catch (Exception ex)
        {
            TempData["Error"] = "Lỗi khi tải danh sách con em: " + ex.Message;
            return RedirectToAction("Index");
        }
    }

    // Add child profile
    [HttpGet]
    public IActionResult AddChild()
    {
        return View(new CreateChildProfileViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddChild(CreateChildProfileViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        try
        {
            var token = GetTokenFromSession();
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _http.PostAsJsonAsync("/api/profile/children", model);
            var content = await response.Content.ReadAsStringAsync();
            var apiResult = JsonSerializer.Deserialize<ApiResultDto>(content, JsonOpts);

            if (apiResult == null || !apiResult.Success)
            {
                ModelState.AddModelError("", apiResult?.Message ?? "Thêm con em thất bại.");
                return View(model);
            }

            TempData["Success"] = "Thêm con em thành công.";
            return RedirectToAction("Children");
        }
        catch (Exception ex)
        {
            TempData["Error"] = "Lỗi khi thêm con em: " + ex.Message;
            return View(model);
        }
    }

    // Edit child profile
    [HttpGet]
    public async Task<IActionResult> EditChild(Guid childId)
    {
        try
        {
            var token = GetTokenFromSession();
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _http.GetAsync("/api/profile/children");
            if (!response.IsSuccessStatusCode)
                return NotFound();

            var content = await response.Content.ReadAsStringAsync();
            var apiResult = JsonSerializer.Deserialize<ApiResultDto>(content, JsonOpts);

            var children = JsonSerializer.Deserialize<List<ChildProfileViewModel>>(
                JsonSerializer.Serialize(apiResult?.Data), JsonOpts) ?? new();

            var child = children.FirstOrDefault(c => c.Id == childId);
            if (child == null)
                return NotFound();

            var viewModel = new UpdateChildProfileViewModel
            {
                Id = child.Id,
                Name = child.Name,
                DateOfBirth = child.DateOfBirth,
                Gender = child.Gender,
                SpecialNeeds = child.SpecialNeeds,
                Allergies = child.Allergies,
                Notes = child.Notes
            };

            return View(viewModel);
        }
        catch (Exception ex)
        {
            TempData["Error"] = "Lỗi khi tải thông tin con em: " + ex.Message;
            return RedirectToAction("Children");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditChild(UpdateChildProfileViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        try
        {
            var token = GetTokenFromSession();
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var updateRequest = new
            {
                model.Name,
                model.DateOfBirth,
                model.Gender,
                model.SpecialNeeds,
                model.Allergies,
                model.Notes
            };

            var response = await _http.PutAsJsonAsync($"/api/profile/children/{model.Id}", updateRequest);
            var content = await response.Content.ReadAsStringAsync();
            var apiResult = JsonSerializer.Deserialize<ApiResultDto>(content, JsonOpts);

            if (apiResult == null || !apiResult.Success)
            {
                ModelState.AddModelError("", apiResult?.Message ?? "Cập nhật thất bại.");
                return View(model);
            }

            TempData["Success"] = "Cập nhật thông tin con em thành công.";
            return RedirectToAction("Children");
        }
        catch (Exception ex)
        {
            TempData["Error"] = "Lỗi khi cập nhật: " + ex.Message;
            return View(model);
        }
    }

    // Delete child profile
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteChild(Guid childId)
    {
        try
        {
            var token = GetTokenFromSession();
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _http.DeleteAsync($"/api/profile/children/{childId}");
            var content = await response.Content.ReadAsStringAsync();
            var apiResult = JsonSerializer.Deserialize<ApiResultDto>(content, JsonOpts);

            if (apiResult == null || !apiResult.Success)
            {
                TempData["Error"] = apiResult?.Message ?? "Xóa thất bại.";
                return RedirectToAction("Children");
            }

            TempData["Success"] = "Xóa con em thành công.";
            return RedirectToAction("Children");
        }
        catch (Exception ex)
        {
            TempData["Error"] = "Lỗi khi xóa: " + ex.Message;
            return RedirectToAction("Children");
        }
    }
}

// View Models









public class ApiResultDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public object? Data { get; set; }
}
