using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nanny_BackEnd.DTOs.Nanny;
using Nanny_BackEnd.Services;

namespace Nanny_BackEnd.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NanniesController : ControllerBase
{
    private readonly NannyService _nannyService;

    public NanniesController(NannyService nannyService)
    {
        _nannyService = nannyService;
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> GetNannies([FromQuery] NannyListRequest request)
    {
        try
        {
            var result = await _nannyService.GetListAsync(request);
            return Ok(new
            {
                success = true,
                data = result.Items,
                totalCount = result.TotalCount,
                page = result.Page,
                pageSize = result.PageSize
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, Fail(ex.Message));
        }
    }

    [AllowAnonymous]
    [HttpGet("{nannyProfileId:guid}")]
    public async Task<IActionResult> GetNannyDetail(Guid nannyProfileId)
    {
        try
        {
            var detail = await _nannyService.GetDetailAsync(nannyProfileId);
            return Ok(new { success = true, data = detail });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(Fail(ex.Message));
        }
        catch (Exception ex)
        {
            return StatusCode(500, Fail(ex.Message));
        }
    }

    private static object Fail(string message) => new { success = false, message };
}
