using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebSite.Models.Review;

namespace WebSite.Controllers;

[Authorize]
public class ReviewController : Controller
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public ReviewController(IHttpClientFactory httpFactory)
    {
        _http = httpFactory.CreateClient("BackendApi");
    }

    private void SetAuthHeader()
    {
        var token = HttpContext.Session.GetString("AccessToken");
        _http.DefaultRequestHeaders.Authorization = !string.IsNullOrEmpty(token)
            ? new AuthenticationHeaderValue("Bearer", token)
            : null;
    }

    // GET /Review/Create/{hiringRecordId}
    [HttpGet]
    public async Task<IActionResult> Create(Guid hiringRecordId)
    {
        SetAuthHeader();

        var resp = await _http.GetAsync("/api/reviews/reviewable");
        if (!resp.IsSuccessStatusCode)
            return RedirectToAction("Index", "Home");

        var json = await resp.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ApiResult<List<ReviewableHiringRecordViewModel>>>(json, JsonOpts);
        var record = result?.Data?.FirstOrDefault(r => r.HiringRecordId == hiringRecordId);
        if (record is null)
            return NotFound();

        var vm = new CreateReviewViewModel
        {
            HiringRecordId = hiringRecordId,
            NannyName = record.NannyName,
            NannyAvatarUrl = record.NannyAvatarUrl,
            StartDate = record.StartDate,
            EndDate = record.EndDate,
        };
        return View(vm);
    }

    // POST /Review/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateReviewViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        SetAuthHeader();
        var payload = JsonSerializer.Serialize(new
        {
            hiringRecordId = model.HiringRecordId,
            rating = model.Rating,
            comment = model.Comment
        });
        var resp = await _http.PostAsync("/api/reviews",
            new StringContent(payload, Encoding.UTF8, "application/json"));

        if (resp.IsSuccessStatusCode)
        {
            TempData["Success"] = "Đánh giá của bạn đã được ghi nhận!";
            return RedirectToAction(nameof(History));
        }

        ModelState.AddModelError("", "Không thể gửi đánh giá. Vui lòng thử lại.");
        return View(model);
    }

    // GET /Review/History
    [HttpGet]
    public async Task<IActionResult> History()
    {
        SetAuthHeader();
        var reviewable = new List<ReviewableHiringRecordViewModel>();

        var resp = await _http.GetAsync("/api/reviews/reviewable");
        if (resp.IsSuccessStatusCode)
        {
            var json = await resp.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResult<List<ReviewableHiringRecordViewModel>>>(json, JsonOpts);
            reviewable = result?.Data ?? [];
        }

        return View(new ReviewHistoryViewModel { Reviewable = reviewable });
    }

    private sealed class ApiResult<T>
    {
        public T? Data { get; set; }
    }
}
