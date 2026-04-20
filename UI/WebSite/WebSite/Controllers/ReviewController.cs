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

    // GET /Review/Create?hiringRecordId={id}
    [HttpGet]
    public async Task<IActionResult> Create(Guid hiringRecordId)
    {
        if (hiringRecordId == Guid.Empty)
            return RedirectToAction(nameof(History));

        SetAuthHeader();

        // Lay thong tin hiring record ?? hien thi form
        var resp = await _http.GetAsync($"/api/hiring/records/{hiringRecordId}");
        if (!resp.IsSuccessStatusCode)
            return RedirectToAction(nameof(History));

        var json = await resp.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<HiringRecordApiResult>(json, JsonOpts);
        var d = result?.Data;
        if (d is null)
            return RedirectToAction(nameof(History));

        // Chi cho phep review khi hop dong da hoan thanh (Status = 4)
        if (d.Status != 4)
        {
            TempData["Error"] = "Chỉ có thể đánh giá sau khi hợp đồng hoàn thành.";
            return RedirectToAction(nameof(History));
        }

        DateOnly.TryParse(d.StartDate, out var startDate);
        DateOnly.TryParse(d.EndDate,   out var endDateParsed);
        DateOnly? endDate = string.IsNullOrEmpty(d.EndDate) ? null : endDateParsed;

        var vm = new CreateReviewViewModel
        {
            HiringRecordId = hiringRecordId,
            NannyName      = d.NannyName ?? "",
            NannyAvatarUrl = null,
            StartDate      = startDate,
            EndDate        = endDate,
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

        // Đọc thông báo lỗi từ backend để hiển thị đúng nguyên nhân
        string errorMsg = "Không thể gửi đánh giá. Vui lòng thử lại.";
        try
        {
            var errJson = await resp.Content.ReadAsStringAsync();
            var errObj  = JsonSerializer.Deserialize<JsonElement>(errJson);
            if (errObj.TryGetProperty("message", out var msg) && msg.GetString() is { Length: > 0 } m)
                errorMsg = m;
        }
        catch { /* bỏ qua nếu body không phải JSON */ }

        ModelState.AddModelError("", errorMsg);
        return View(model);
    }

    // GET /Review/History
    [HttpGet]
    public async Task<IActionResult> History()
    {
        SetAuthHeader();
        var reviewable = new List<ReviewableHiringRecordViewModel>();
        var submitted = new List<SubmittedReviewViewModel>();

        var reviewableResp = await _http.GetAsync("/api/reviews/reviewable");
        if (reviewableResp.IsSuccessStatusCode)
        {
            var json = await reviewableResp.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResult<List<ReviewableHiringRecordViewModel>>>(json, JsonOpts);
            reviewable = result?.Data ?? [];
        }

        var mineResp = await _http.GetAsync("/api/reviews/mine");
        if (mineResp.IsSuccessStatusCode)
        {
            var json = await mineResp.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResult<List<SubmittedReviewViewModel>>>(json, JsonOpts);
            submitted = result?.Data ?? [];
        }

        return View(new ReviewHistoryViewModel { Reviewable = reviewable, Submitted = submitted });
    }

    private sealed class ApiResult<T>
    {
        public T? Data { get; set; }
    }

    // DTO noi bo: map response cua GET /api/hiring/records/{id}
    private sealed class HiringRecordApiResult
    {
        public HiringRecordData? Data { get; set; }
    }

    private sealed class HiringRecordData
    {
        public string NannyName { get; set; } = "";
        public int Status { get; set; }
        public string StartDate { get; set; } = "";   // nhan duoi ?ang string "yyyy-MM-dd"
        public string? EndDate { get; set; }
    }
}
