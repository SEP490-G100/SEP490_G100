using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebSite.Controllers;

[Authorize]
[Route("Hiring")]
public class HiringController : Controller
{
    private readonly HttpClient _http;

    public HiringController(IHttpClientFactory httpFactory)
    {
        _http = httpFactory.CreateClient("BackendApi");
    }

    [HttpGet("{jobPostingId:guid}/Applicants")]
    public async Task<IActionResult> Applicants(Guid jobPostingId)
    {
        SetBearerToken();
        return await Proxy(() => _http.GetAsync($"/api/hiring/{jobPostingId}/applicants"));
    }

    [HttpPost("{jobPostingId:guid}/Applicants/{jobAppId:guid}/Approve")]
    public async Task<IActionResult> Approve(Guid jobPostingId, Guid jobAppId)
    {
        SetBearerToken();
        return await Proxy(() => _http.PostAsync(
            $"/api/hiring/{jobPostingId}/applicants/{jobAppId}/approve",
            EmptyJson()));
    }

    [HttpGet("{jobPostingId:guid}/Applicants/{jobAppId:guid}/NannyContext")]
    public async Task<IActionResult> NannyContext(Guid jobPostingId, Guid jobAppId)
    {
        SetBearerToken();
        return await Proxy(() => _http.GetAsync($"/api/hiring/{jobPostingId}/applicants/{jobAppId}/nanny-context"));
    }

    [HttpPost("{jobPostingId:guid}/Applicants/{jobAppId:guid}/Hire")]
    public async Task<IActionResult> Hire(Guid jobPostingId, Guid jobAppId)
    {
        SetBearerToken();
        var body = await ReadBodyAsync();
        return await Proxy(() => _http.PostAsync(
            $"/api/hiring/{jobPostingId}/applicants/{jobAppId}/hire",
            JsonContent(body)));
    }

    [HttpPost("ContactRequests/{contactRequestId:guid}/Hire")]
    public async Task<IActionResult> HireFromContactRequest(Guid contactRequestId)
    {
        SetBearerToken();
        var body = await ReadBodyAsync();
        return await Proxy(() => _http.PostAsync(
            $"/api/hiring/contact-requests/{contactRequestId}/hire",
            JsonContent(body)));
    }

    [HttpGet("Record")]
    public async Task<IActionResult> Record([FromQuery] Guid hiringRecordId)
    {
        SetBearerToken();
        return await Proxy(() => _http.GetAsync($"/api/hiring/records/{hiringRecordId}"));
    }

    [HttpPost("Records/{hiringRecordId:guid}/Respond")]
    public async Task<IActionResult> Respond(Guid hiringRecordId)
    {
        SetBearerToken();
        var body = await ReadBodyAsync();
        return await Proxy(() => _http.PostAsync(
            $"/api/hiring/records/{hiringRecordId}/respond",
            JsonContent(body)));
    }

    [HttpPost("Records/{hiringRecordId:guid}/Complete")]
    public async Task<IActionResult> Complete(Guid hiringRecordId)
    {
        SetBearerToken();
        return await Proxy(() => _http.PostAsync(
            $"/api/hiring/records/{hiringRecordId}/complete",
            EmptyJson()));
    }

    [HttpGet("Templates")]
    public async Task<IActionResult> Templates()
    {
        SetBearerToken();
        return await Proxy(() => _http.GetAsync("/api/hiring/templates"));
    }

    private void SetBearerToken()
    {
        var token = HttpContext.Session.GetString("AccessToken");
        _http.DefaultRequestHeaders.Authorization = string.IsNullOrWhiteSpace(token)
            ? null
            : new AuthenticationHeaderValue("Bearer", token);
    }

    private async Task<string> ReadBodyAsync()
    {
        using var reader = new System.IO.StreamReader(Request.Body);
        return await reader.ReadToEndAsync();
    }

    private static StringContent EmptyJson() => new("{}", Encoding.UTF8, "application/json");
    private static StringContent JsonContent(string body) =>
        new(string.IsNullOrWhiteSpace(body) ? "{}" : body, Encoding.UTF8, "application/json");

    private static async Task<IActionResult> Proxy(Func<Task<HttpResponseMessage>> action)
    {
        try
        {
            var response = await action();
            var body = await response.Content.ReadAsStringAsync();
            return new ContentResult
            {
                Content = string.IsNullOrWhiteSpace(body) ? "{}" : body,
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

