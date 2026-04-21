using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebSite.Controllers;

[Authorize]
public class CommunicationController : Controller
{
    private readonly HttpClient _http;
    private readonly string _apiBaseUrl;

    public CommunicationController(IHttpClientFactory httpFactory, IConfiguration config)
    {
        _http = httpFactory.CreateClient("BackendApi");
        _apiBaseUrl = (config["ApiSettings:BaseUrl"] ?? "").TrimEnd('/');
    }

    // GET /Communication  â€” Trang chat chÃ­nh
    [HttpGet]
    public IActionResult Index(Guid? conversationId = null)
    {
        ViewBag.ApiBaseUrl = _apiBaseUrl;
        ViewBag.InitialConversationId = conversationId;
        ViewBag.AccessToken = HttpContext.Session.GetString("AccessToken") ?? "";
        return View();
    }

    // GET /Communication/Conversations â€” proxy API
    [HttpGet]
    public async Task<IActionResult> Conversations()
    {
        setAuthHeader();
        return await proxy(() => _http.GetAsync("/api/communication/conversations"));
    }

    /// <summary>GET /Communication/UnreadCount — badge tin nhắn trên header (giống Notification/UnreadCount).</summary>
    [HttpGet]
    public async Task<IActionResult> UnreadCount()
    {
        var token = HttpContext.Session.GetString("AccessToken");
        if (string.IsNullOrWhiteSpace(token))
        {
            return Json(new { success = true, data = new { unreadCount = 0 } });
        }

        setAuthHeader();
        var result = await proxy(() => _http.GetAsync("/api/communication/unread-count"));
        if (result is ContentResult { StatusCode: 401 })
        {
            return Json(new { success = true, data = new { unreadCount = 0 } });
        }

        return result;
    }

    // GET /Communication/Messages?conversationId=...&page=...&pageSize=...
    [HttpGet]
    public async Task<IActionResult> Messages(Guid conversationId, int page = 1, int pageSize = 30)
    {
        setAuthHeader();
        return await proxy(() =>
            _http.GetAsync($"/api/communication/conversations/{conversationId}/messages?page={page}&pageSize={pageSize}"));
    }

    /// <summary>POST /Communication/MarkRead?conversationId= — đánh dấu đã đọc mọi tin trong hội thoại (đồng bộ badge).</summary>
    [HttpPost]
    public async Task<IActionResult> MarkRead([FromQuery] Guid conversationId)
    {
        setAuthHeader();
        return await proxy(() =>
            _http.PostAsync($"/api/communication/conversations/{conversationId}/mark-read", null));
    }

    // POST /Communication/GetOrCreate
    [HttpPost]
    public async Task<IActionResult> GetOrCreate([FromBody] object dto)
    {
        setAuthHeader();
        var content = new StringContent(JsonSerializer.Serialize(dto), Encoding.UTF8, "application/json");
        return await proxy(() => _http.PostAsync("/api/communication/conversations", content));
    }

    // POST /Communication/SendMessage?conversationId=...
    [HttpPost]
    public async Task<IActionResult> SendMessage(Guid conversationId, [FromBody] object dto)
    {
        setAuthHeader();
        var content = new StringContent(JsonSerializer.Serialize(dto), Encoding.UTF8, "application/json");
        return await proxy(() =>
            _http.PostAsync($"/api/communication/conversations/{conversationId}/messages", content));
    }

    // DELETE /Communication/DeleteMessage/{id}
    [HttpDelete]
    public async Task<IActionResult> DeleteMessage(Guid id)
    {
        setAuthHeader();
        return await proxy(() => _http.DeleteAsync($"/api/communication/messages/{id}"));
    }

    // POST /Communication/ReportMessage/{id}
    [HttpPost]
    public async Task<IActionResult> ReportMessage(Guid id, [FromBody] object dto)
    {
        setAuthHeader();
        var content = new StringContent(JsonSerializer.Serialize(dto), Encoding.UTF8, "application/json");
        return await proxy(() => _http.PostAsync($"/api/communication/messages/{id}/report", content));
    }

    // PATCH /Communication/UpdateStatus/{id}
    [HttpPost]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] object dto)
    {
        setAuthHeader();
        var req = new HttpRequestMessage(HttpMethod.Patch, $"/api/communication/conversations/{id}/status")
        {
            Content = new StringContent(JsonSerializer.Serialize(dto), Encoding.UTF8, "application/json")
        };
        return await proxy(() => _http.SendAsync(req));
    }

    // â”€â”€â”€ Helpers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private void setAuthHeader()
    {
        var token = HttpContext.Session.GetString("AccessToken");
        _http.DefaultRequestHeaders.Authorization = string.IsNullOrWhiteSpace(token)
            ? null
            : new AuthenticationHeaderValue("Bearer", token);
    }

    private static async Task<IActionResult> proxy(Func<Task<HttpResponseMessage>> action)
    {
        try
        {
            var response = await action();
            var json = await response.Content.ReadAsStringAsync();
            return new ContentResult
            {
                Content = string.IsNullOrWhiteSpace(json) ? "{}" : json,
                ContentType = "application/json",
                StatusCode = (int)response.StatusCode
            };
        }
        catch (Exception)
        {
            return new JsonResult(new { success = false, message = "Không thể kết nối máy chủ lúc này. Vui lòng thử lại." }) { StatusCode = 500 };
        }
    }
}
