using System.Text.Json;

namespace Nanny_BackEnd.Services;

/// <summary>
/// Geocoding tự động: chuyển địa chỉ (location, city, district) → lat/lng
/// Dùng OpenStreetMap Nominatim (miễn phí, không cần API key).
/// Rate limit: 1 req/giây — đủ dùng cho tạo/cập nhật bài đăng.
/// </summary>
public class GeocodingService
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions Opts = new() { PropertyNameCaseInsensitive = true };

    public GeocodingService(IHttpClientFactory factory)
    {
        _http = factory.CreateClient("Nominatim");
    }

    /// <summary>
    /// Geocode địa chỉ → (latitude, longitude). Trả null nếu không tìm thấy.
    /// Ưu tiên: Location đầy đủ → City+District → City.
    /// </summary>
    public async Task<(decimal Lat, decimal Lng)?> geocode(
        string? location, string? city, string? district)
    {
        // Ưu tiên địa chỉ đầy đủ nhất
        var queries = new List<string>();

        if (!string.IsNullOrWhiteSpace(location))
            queries.Add(location.Trim());

        if (!string.IsNullOrWhiteSpace(district) && !string.IsNullOrWhiteSpace(city))
            queries.Add($"{district.Trim()}, {city.Trim()}, Vietnam");

        if (!string.IsNullOrWhiteSpace(city))
            queries.Add($"{city.Trim()}, Vietnam");

        foreach (var q in queries)
        {
            var result = await tryGeocode(q);
            if (result.HasValue) return result;
        }

        return null;
    }

    private async Task<(decimal Lat, decimal Lng)?> tryGeocode(string query)
    {
        try
        {
            var url = $"https://nominatim.openstreetmap.org/search?q={Uri.EscapeDataString(query)}&format=json&limit=1&countrycodes=vn";
            var response = await _http.GetAsync(url);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            var arr  = JsonSerializer.Deserialize<NominatimResult[]>(json, Opts);

            if (arr == null || arr.Length == 0) return null;

            if (decimal.TryParse(arr[0].Lat, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var lat) &&
                decimal.TryParse(arr[0].Lon, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var lng))
            {
                return (lat, lng);
            }
        }
        catch { /* silent — geocoding failure should not block job creation */ }

        return null;
    }

    // ── Haversine distance (km) ─────────────────────────────────────────
    /// <summary>
    /// Tính khoảng cách (km) giữa 2 điểm theo công thức Haversine.
    /// </summary>
    public static double CalculateDistanceKm(
        double lat1, double lng1, double lat2, double lng2)
    {
        const double R = 6371; // Earth radius km
        var dLat = ToRad(lat2 - lat1);
        var dLng = ToRad(lng2 - lng1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2))
              * Math.Sin(dLng / 2)   * Math.Sin(dLng / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return Math.Round(R * c, 1);
    }

    private static double ToRad(double deg) => deg * Math.PI / 180;

    private class NominatimResult
    {
        public string Lat { get; set; } = "";
        public string Lon { get; set; } = "";
    }
}
