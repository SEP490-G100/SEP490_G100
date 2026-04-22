using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Controllers;

[ApiController]
[Route("api/moderator/dashboard")]
[Authorize(Roles = "Moderator")]
public class ModeratorDashboardController : ControllerBase
{
    private readonly IModeratorDashboardService _moderatorDashboardService;

    public ModeratorDashboardController(IModeratorDashboardService moderatorDashboardService)
    {
        _moderatorDashboardService = moderatorDashboardService;
    }

    [HttpGet]
    public async Task<IActionResult> GetDashboard()
    {
        var stats = await _moderatorDashboardService.GetDashboardStatsAsync();
        return Ok(new { success = true, data = stats });
    }
}
