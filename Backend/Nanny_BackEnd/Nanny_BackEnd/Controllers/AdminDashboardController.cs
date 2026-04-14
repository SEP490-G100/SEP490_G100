using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nanny_BackEnd.Services;

namespace Nanny_BackEnd.Controllers;

[ApiController]
[Route("api/admin/dashboard")]
[Authorize(Roles = "Admin")]
public class AdminDashboardController : ControllerBase
{
    private readonly AdminDashboardService _adminDashboardService;

    public AdminDashboardController(AdminDashboardService adminDashboardService)
    {
        _adminDashboardService = adminDashboardService;
    }

    [HttpGet]
    public async Task<IActionResult> GetDashboard()
    {
        var stats = await _adminDashboardService.GetDashboardStatsAsync();
        return Ok(new { success = true, data = stats });
    }
}
