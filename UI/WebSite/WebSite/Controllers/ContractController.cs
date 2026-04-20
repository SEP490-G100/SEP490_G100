using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebSite.Models.Contract;
using System.Text;

namespace WebSite.Controllers;

[Authorize]
[Route("Contract")]
public class ContractController : Controller
{
    private readonly HttpClient _http;

    public ContractController(IHttpClientFactory httpFactory)
    {
        _http = httpFactory.CreateClient("BackendApi");
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        return View();
    }

    [HttpGet("Detail/{id:guid}")]
    public IActionResult Detail(Guid id)
    {
        return View("ViewContractDetail", new ContractDetailPageViewModel { ContractId = id });
    }

    [HttpGet("Api/List")]
    public async Task<IActionResult> ListApi()
    {
        SetBearerToken();
        var response = await _http.GetAsync("/api/contracts");
        return await ToJsonProxy(response);
    }

    [HttpGet("Api/Detail/{id:guid}")]
    public async Task<IActionResult> DetailApi(Guid id)
    {
        SetBearerToken();
        var response = await _http.GetAsync($"/api/contracts/{id}");
        return await ToJsonProxy(response);
    }

    [HttpPatch("Api/Detail/{id:guid}/DraftParent")]
    public async Task<IActionResult> DraftParent(Guid id)
    {
        SetBearerToken();
        var body = await ReadBodyAsync();
        var response = await _http.PatchAsync(
            $"/api/contracts/{id}/draft-parent",
            JsonContent(body));
        return await ToJsonProxy(response);
    }

    [HttpPatch("Api/Detail/{id:guid}/FillNanny")]
    public async Task<IActionResult> FillNanny(Guid id)
    {
        SetBearerToken();
        var body = await ReadBodyAsync();
        var response = await _http.PatchAsync(
            $"/api/contracts/{id}/fill-nanny",
            JsonContent(body));
        return await ToJsonProxy(response);
    }

    [HttpPost("Api/Detail/{id:guid}/FinalConfirmParent")]
    public async Task<IActionResult> FinalConfirmParent(Guid id)
    {
        SetBearerToken();
        var response = await _http.PostAsync(
            $"/api/contracts/{id}/final-confirm-parent",
            JsonContent("{}"));
        return await ToJsonProxy(response);
    }

    private void SetBearerToken()
    {
        var token = HttpContext.Session.GetString("AccessToken");
        _http.DefaultRequestHeaders.Authorization = string.IsNullOrWhiteSpace(token)
            ? null
            : new AuthenticationHeaderValue("Bearer", token);
    }

    private static async Task<IActionResult> ToJsonProxy(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        return new ContentResult
        {
            Content = string.IsNullOrWhiteSpace(body) ? "{}" : body,
            ContentType = "application/json",
            StatusCode = (int)response.StatusCode
        };
    }

    private async Task<string> ReadBodyAsync()
    {
        using var reader = new StreamReader(Request.Body);
        return await reader.ReadToEndAsync();
    }

    private static StringContent JsonContent(string body) =>
        new(string.IsNullOrWhiteSpace(body) ? "{}" : body, Encoding.UTF8, "application/json");
}

