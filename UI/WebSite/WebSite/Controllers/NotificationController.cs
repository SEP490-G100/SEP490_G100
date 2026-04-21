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
    public IActionResult Center(Guid? notificationId = null)
    {
        ViewData["NotificationId"] = notificationId?.ToString() ?? string.Empty;
        return View();
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
        var token = HttpContext.Session.GetString("AccessToken");
        if (string.IsNullOrWhiteSpace(token))
        {
            return Json(new
            {
                success = true,
                data = new { unreadCount = 0 }
            });
        }

        setAuthHeader();
        var result = await proxy(() => _http.GetAsync("/api/notifications/unread-count"));

        // Keep navbar polling resilient when backend token is expired/invalid.
        // Returning zero prevents noisy 401 logs while user remains on current page.
        if (result is ContentResult { StatusCode: 401 })
        {
            return Json(new
            {
                success = true,
                data = new { unreadCount = 0 }
            });
        }

        return result;
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

