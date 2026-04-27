using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Controllers;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class AddressController : ControllerBase
{
    private readonly ILocationService _locationService;

    public AddressController(ILocationService locationService)
    {
        _locationService = locationService;
    }

    [HttpGet("location-tree")]
    public async Task<IActionResult> GetLocationTree(CancellationToken cancellationToken)
    {
        var data = await _locationService.GetLocationTreeAsync(cancellationToken);
        if (data.Count == 0)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = "Khong tai duoc danh sach tinh thanh."
            });
        }

        return Ok(data);
    }

    [HttpGet("suggest")]
    public async Task<IActionResult> Suggest([FromQuery] string? q, [FromQuery] int limit = 8, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
            return Ok(new List<AddressSuggestionDto>());

        var items = await _locationService.SuggestApproximateAsync(q.Trim(), Math.Clamp(limit, 1, 10), cancellationToken);
        var list = items.Select(x => new AddressSuggestionDto
        {
            DisplayName = x.DisplayName,
            Latitude = x.Latitude,
            Longitude = x.Longitude,
            City = x.City,
            District = x.District,
            Ward = x.Ward
        }).ToList();

        return Ok(list);
    }
}

public class AddressSuggestionDto
{
    public string DisplayName { get; set; } = "";
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public string? City { get; set; }
    public string? District { get; set; }
    public string? Ward { get; set; }
}
