using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Nanny_BackEnd.Controllers;

/// <summary>
/// Gợi ý địa chỉ và lấy tọa độ cho địa điểm tại Việt Nam (dùng Nominatim/OSM).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class AddressController : ControllerBase
{
    private static readonly HttpClient Http = new()
    {
        DefaultRequestHeaders = { { "User-Agent", "SEP490-NannyApp/1.0 (Vietnam address suggest)" } },
        Timeout = TimeSpan.FromSeconds(10)
    };

    private const string NominatimSearchUrl = "https://nominatim.openstreetmap.org/search";

    /// <summary>
    /// Gợi ý địa điểm trong lãnh thổ Việt Nam. Trả về danh sách kèm tọa độ (Latitude, Longitude).
    /// </summary>
    [HttpGet("suggest")]
    public async Task<IActionResult> Suggest([FromQuery] string? q, [FromQuery] int limit = 8)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
            return Ok(new List<AddressSuggestionDto>());

        var query = Uri.EscapeDataString(q.Trim());
        var url = $"{NominatimSearchUrl}?q={query}&countrycodes=vn&format=json&addressdetails=1&limit={Math.Min(limit, 10)}";

        try
        {
            var response = await Http.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            var raw = JsonSerializer.Deserialize<List<NominatimItem>>(json);
            var list = (raw ?? new List<NominatimItem>())
                .Select(MapToSuggestion)
                .Where(x => x != null)
                .Cast<AddressSuggestionDto>()
                .ToList();
            return Ok(list);
        }
        catch (Exception)
        {
            return Ok(new List<AddressSuggestionDto>());
        }
    }

    private static AddressSuggestionDto? MapToSuggestion(NominatimItem item)
    {
        if (string.IsNullOrEmpty(item.Lat) || string.IsNullOrEmpty(item.Lon))
            return null;
        if (!decimal.TryParse(item.Lat, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var lat))
            return null;
        if (!decimal.TryParse(item.Lon, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var lng))
            return null;

        var addr = item.Address ?? new Dictionary<string, JsonElement>();
        string? Get(string key) => addr.TryGetValue(key, out var el) ? el.GetString() : null;

        // Vietnam: state ~ tỉnh/thành, city/municipality ~ quận/huyện, suburb/village/town ~ phường/xã
        var city = Get("state") ?? Get("city") ?? Get("province");
        var district = Get("municipality") ?? Get("city") ?? Get("county") ?? Get("district");
        var ward = Get("suburb") ?? Get("village") ?? Get("town") ?? Get("ward");

        return new AddressSuggestionDto
        {
            DisplayName = item.DisplayName ?? "",
            Latitude = lat,
            Longitude = lng,
            City = city,
            District = district,
            Ward = ward
        };
    }

    private class NominatimItem
    {
        [JsonPropertyName("lat")]
        public string? Lat { get; set; }

        [JsonPropertyName("lon")]
        public string? Lon { get; set; }

        [JsonPropertyName("display_name")]
        public string? DisplayName { get; set; }

        [JsonPropertyName("address")]
        public Dictionary<string, JsonElement>? Address { get; set; }
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
