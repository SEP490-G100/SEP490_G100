using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nanny_BackEnd.Helpers;

public static class GeoHelper
{
    // Dùng Nominatim (OpenStreetMap) để lấy toạ độ theo địa chỉ đầy đủ tại Việt Nam
    private static readonly HttpClient Http = new()
    {
        DefaultRequestHeaders =
        {
            { "User-Agent", "SEP490-NannyApp/1.0 (server-side geocoding)" }
        },
        Timeout = TimeSpan.FromSeconds(10)
    };

    private const string NominatimSearchUrl = "https://nominatim.openstreetmap.org/search";

    public static async Task<(decimal? lat, decimal? lng)> ResolveLatLngAsync(
        string? addressDetail,
        string? city,
        string? district,
        string? ward)
    {
        // Cần tối thiểu City + District + Ward để xác định vị trí đủ chính xác
        if (string.IsNullOrWhiteSpace(city) ||
            string.IsNullOrWhiteSpace(district) ||
            string.IsNullOrWhiteSpace(ward))
            return (null, null);

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(addressDetail)) parts.Add(addressDetail.Trim());
        parts.Add(ward!.Trim());
        parts.Add(district!.Trim());
        parts.Add(city!.Trim());

        var query = string.Join(", ", parts);
        if (string.IsNullOrWhiteSpace(query))
            return (null, null);

        var url = $"{NominatimSearchUrl}?q={Uri.EscapeDataString(query)}&countrycodes=vn&format=json&limit=1";

        try
        {
            var response = await Http.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            var items = JsonSerializer.Deserialize<List<NominatimItem>>(json);
            var first = items?.FirstOrDefault();
            if (first == null || string.IsNullOrWhiteSpace(first.Lat) || string.IsNullOrWhiteSpace(first.Lon))
                return (null, null);

            if (!decimal.TryParse(first.Lat, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var lat))
                return (null, null);
            if (!decimal.TryParse(first.Lon, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var lng))
                return (null, null);

            return (lat, lng);
        }
        catch
        {
            // Nếu lỗi (mạng, API, timeout, ...) thì không set toạ độ
            return (null, null);
        }
    }

    private class NominatimItem
    {
        [JsonPropertyName("lat")]
        public string? Lat { get; set; }

        [JsonPropertyName("lon")]
        public string? Lon { get; set; }

        [JsonPropertyName("display_name")]
        public string? DisplayName { get; set; }
    }
}


