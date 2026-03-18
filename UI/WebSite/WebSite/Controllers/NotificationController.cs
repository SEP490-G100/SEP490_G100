using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebSite.Controllers;

[Authorize]
public class NotificationController : Controller
{
    private readonly HttpClient _http;

    public NotificationController(IHttpClientFactory httpFactory)
    {
        _http = httpFactory.CreateClient("BackendApi");
    }

    [HttpGet]
    public async Task<IActionResult> My(int page = 1, int pageSize = 8)
    {
        setAuthHeader();
        return await proxy(() => _http.GetAsync($"/api/notifications/me?page={page}&pageSize={pageSize}"));
    }

    [HttpGet]
    public async Task<IActionResult> UnreadCount()
    {
        setAuthHeader();
        return await proxy(() => _http.GetAsync("/api/notifications/unread-count"));
    }

    [HttpPost]
    public async Task<IActionResult> MarkRead(Guid id)
    {
        setAuthHeader();
        return await proxy(() => _http.PostAsync($"/api/notifications/{id}/mark-read", null));
    }

    [HttpPost]
    public async Task<IActionResult> MarkAllRead()
    {
        setAuthHeader();
        return await proxy(() => _http.PostAsync("/api/notifications/mark-all-read", null));
    }

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
            return new JsonResult(new { success = false, message = ex.Message })
            {
                StatusCode = 500
            };
        }
    }
}
