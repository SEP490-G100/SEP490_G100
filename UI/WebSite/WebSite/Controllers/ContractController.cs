using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebSite.Models;
using WebSite.Models.Contract;
using WebSite.Services;

namespace WebSite.Controllers;

[Authorize]
[Route("Contract")]
public class ContractController : Controller
{
    private readonly HttpClient _http;
    private readonly IAzureBlobStorageService _blobStorage;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

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

    [HttpGet("ViewContractDetail")]
    public async Task<IActionResult> ViewContractDetail([FromQuery] Guid? contractId, [FromQuery] Guid? hiringRecordId)
    {
        if (!contractId.HasValue && !hiringRecordId.HasValue)
            return RedirectToAction("ViewHiringHistory", "Hiring");

        SetBearerToken();
        var query = new List<string>();
        if (contractId.HasValue && contractId.Value != Guid.Empty)
            query.Add($"contractId={contractId.Value}");
        if (hiringRecordId.HasValue && hiringRecordId.Value != Guid.Empty)
            query.Add($"hiringRecordId={hiringRecordId.Value}");

        var response = await _http.GetAsync("/api/contracts/detail" + (query.Count > 0 ? "?" + string.Join("&", query) : string.Empty));
        if (!response.IsSuccessStatusCode)
        {
            TempData["Error"] = "Không thể tải chi tiết hợp đồng.";
            return RedirectToAction("ViewHiringHistory", "Hiring");
        }

        var body = await response.Content.ReadAsStringAsync();
        var api = JsonSerializer.Deserialize<ApiResult<ContractDetailViewModel>>(body, JsonOpts);
        if (api?.Success != true || api.Data == null)
        {
            TempData["Error"] = api?.Message ?? "Không thể tải chi tiết hợp đồng.";
            return RedirectToAction("ViewHiringHistory", "Hiring");
        }

        return View("~/Views/Contract/ViewContractDetail.cshtml", api.Data);
    }

    [HttpGet("Api/List")]
    public async Task<IActionResult> ListApi()
    {
        SetBearerToken();
        var response = await _http.GetAsync("/api/contracts");
        return await ToJsonProxy(response);
    }

    [HttpGet("Api/Detail")]
    public async Task<IActionResult> DetailApi([FromQuery] Guid? contractId, [FromQuery] Guid? hiringRecordId)
    {
        SetBearerToken();
        var query = new List<string>();
        if (contractId.HasValue && contractId.Value != Guid.Empty)
            query.Add($"contractId={contractId.Value}");
        if (hiringRecordId.HasValue && hiringRecordId.Value != Guid.Empty)
            query.Add($"hiringRecordId={hiringRecordId.Value}");

        var response = await _http.GetAsync("/api/contracts/detail" + (query.Count > 0 ? "?" + string.Join("&", query) : string.Empty));
        return await ToJsonProxy(response);
    }

    [HttpPatch("Api/{id:guid}/ParentConfirmInfo")]
    public async Task<IActionResult> ParentConfirmInfo(Guid id, [FromBody] ContractParentFillRequestViewModel request)
    {
        SetBearerToken();
        var response = await _http.PatchAsJsonAsync($"/api/contracts/{id}/parent-confirm-info", request);
        return await ToJsonProxy(response);
    }

    [HttpPatch("Api/{id:guid}/NannyConfirmInfo")]
    public async Task<IActionResult> NannyConfirmInfo(Guid id, [FromBody] ContractNannyFillRequestViewModel request)
    {
        SetBearerToken();
        var response = await _http.PatchAsJsonAsync($"/api/contracts/{id}/nanny-confirm-info", request);
        return await ToJsonProxy(response);
    }

    [HttpPost("Api/{id:guid}/ParentFinalConfirm")]
    public async Task<IActionResult> ParentFinalConfirm(Guid id)
    {
        SetBearerToken();
        var response = await _http.PostAsync($"/api/contracts/{id}/parent-final-confirm", new StringContent("{}", Encoding.UTF8, "application/json"));
        return await ToJsonProxy(response);
    }

    [HttpGet("Api/{id:guid}/Download")]
    public async Task<IActionResult> Download(Guid id)
    {
        SetBearerToken();
        var response = await _http.GetAsync($"/api/contracts/{id}/download");
        if (!response.IsSuccessStatusCode)
            return await ToJsonProxy(response);

        var content = await response.Content.ReadAsByteArrayAsync();
        var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
            ?? response.Content.Headers.ContentDisposition?.FileName
            ?? "HopDong.pdf";

        fileName = fileName.Trim('"');
        return File(content, "application/pdf", fileName);
    }

    [HttpPost("Api/{id:guid}/UploadStorageFile")]
    [RequestSizeLimit(15 * 1024 * 1024)]
    public async Task<IActionResult> UploadStorageFile(Guid id, IFormFile? contractFile)
    {
        SetBearerToken();

        if (contractFile == null || contractFile.Length == 0)
            return BadRequest(new { success = false, message = "Vui lòng chọn file PDF hợp đồng để tải lên." });

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
            return StatusCode(500, new { success = false, message = "Không thể tải lên file PDF hợp đồng lúc này." });
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
