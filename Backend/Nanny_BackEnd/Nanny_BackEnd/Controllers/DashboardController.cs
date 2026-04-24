using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("admin/dashboard")]
    public async Task<IActionResult> AdminDashboard()
    {
        var stats = await _dashboardService.GetAdminDashboardStatsAsync();
        return Ok(new { success = true, data = stats });
    }

    [Authorize(Roles = "Moderator")]
    [HttpGet("moderator/dashboard")]
    public async Task<IActionResult> ModeratorDashboard()
    {
        var stats = await _dashboardService.GetModeratorDashboardStatsAsync();
        return Ok(new { success = true, data = stats });
    }
}
