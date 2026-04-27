using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Controllers;

[ApiController]
[Route("api/Admin")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly IExportService _exportService;

    public AdminController(IExportService exportService)
    {
        _exportService = exportService;
    }

    [HttpGet("export-system-data")]
    public async Task<IActionResult> ExportSystemData()
    {
        var fileContents = await _exportService.ExportSystemDataToExcelAsync();
        return File(
            fileContents,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"NannyMatch_SystemData_{DateTime.Now:yyyyMMdd}.xlsx");
    }
}
