using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebSite.Models.Contract;

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
        return View(new ContractDetailPageViewModel { ContractId = id });
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
}

