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

    // GET /Communication  — Trang chat chính
    [HttpGet]
    public IActionResult Index(Guid? conversationId = null)
    {
        ViewBag.ApiBaseUrl = _apiBaseUrl;
        ViewBag.InitialConversationId = conversationId;
        ViewBag.AccessToken = HttpContext.Session.GetString("AccessToken") ?? "";
        return View();
    }

    // GET /Communication/Conversations — proxy API
    [HttpGet]
    public async Task<IActionResult> Conversations()
    {
        setAuthHeader();
        return await proxy(() => _http.GetAsync("/api/communication/conversations"));
    }

    // GET /Communication/Messages?conversationId=...&page=...&pageSize=...
    [HttpGet]
    public async Task<IActionResult> Messages(Guid conversationId, int page = 1, int pageSize = 30)
    {
        setAuthHeader();
        return await proxy(() =>
            _http.GetAsync($"/api/communication/conversations/{conversationId}/messages?page={page}&pageSize={pageSize}"));
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

    // ─── Helpers ─────────────────────────────────────────────────────────────

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
        catch (Exception ex)
        {
            return new JsonResult(new { success = false, message = ex.Message }) { StatusCode = 500 };
        }
    }
}