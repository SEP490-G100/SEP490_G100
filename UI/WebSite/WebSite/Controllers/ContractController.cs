using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebSite.Models.Contract;
using WebSite.Services;

namespace WebSite.Controllers;

[Authorize]
[Route("Contract")]
public class ContractController : Controller
{
    private readonly HttpClient _http;
    private readonly IAzureBlobStorageService _blobStorage;

    public ContractController(IHttpClientFactory httpFactory, IAzureBlobStorageService blobStorage)
    {
        _http = httpFactory.CreateClient("BackendApi");
        _blobStorage = blobStorage;
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        return View();
    }

    [HttpGet("Api/List")]
    public async Task<IActionResult> ListApi()
    {
        SetBearerToken();
        var response = await _http.GetAsync("/api/contracts");
        return await ToJsonProxy(response);
    }

    [HttpPost("Api/{id:guid}/UploadStorageFile")]
    [RequestSizeLimit(15 * 1024 * 1024)]
    public async Task<IActionResult> UploadStorageFile(Guid id, IFormFile? contractFile)
    {
        SetBearerToken();

        if (contractFile == null || contractFile.Length == 0)
            return BadRequest(new { success = false, message = "Vui long chon file PDF hop dong de upload." });

        try
        {
            var fileUrl = await _blobStorage.UploadContractPdfAsync(contractFile);
            var response = await _http.PatchAsJsonAsync($"/api/contracts/{id}/storage-file", new
            {
                pdfUrl = fileUrl
            });

            return await ToJsonProxy(response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception)
        {
            return StatusCode(500, new { success = false, message = "Khong the upload file PDF hop dong luc nay." });
        }
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
