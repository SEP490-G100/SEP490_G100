using System.Text.Json;
using System.Net.Http.Headers;
using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Services;

/// <summary>
/// Geocoding tự động: chuyển địa chỉ (location, city, district) → lat/lng
/// Dùng OpenStreetMap Nominatim (miễn phí, không cần API key).
/// Rate limit: 1 req/giây — đủ dùng cho tạo/cập nhật bài đăng.
/// </summary>
public class GeocodingService : IGeocodingService
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
    public virtual async Task<(decimal Lat, decimal Lng)?> geocode(
        string? location, string? city, string? district)
    {
        var normalizedLocation = location?.Trim();
        var normalizedCity = city?.Trim();
        var normalizedDistrict = district?.Trim();
        var expectedCityToken = NormalizeAdministrativeName(normalizedCity);

        // Ưu tiên query đầy đủ để tránh nhầm địa chỉ cùng tên ở tỉnh/thành khác.
        var queries = new List<string>();
        var dedupe = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddQuery(string? query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return;

            var value = query.Trim();
            if (dedupe.Add(value))
                queries.Add(value);
        }

        if (!string.IsNullOrWhiteSpace(normalizedLocation) &&
            !string.IsNullOrWhiteSpace(normalizedDistrict) &&
            !string.IsNullOrWhiteSpace(normalizedCity))
        {
            AddQuery($"{normalizedLocation}, {normalizedDistrict}, {normalizedCity}, Vietnam");
        }

        if (!string.IsNullOrWhiteSpace(normalizedLocation) &&
            !string.IsNullOrWhiteSpace(normalizedCity))
        {
            AddQuery($"{normalizedLocation}, {normalizedCity}, Vietnam");
        }

        if (!string.IsNullOrWhiteSpace(normalizedDistrict) &&
            !string.IsNullOrWhiteSpace(normalizedCity))
        {
            AddQuery($"{normalizedDistrict}, {normalizedCity}, Vietnam");
        }

        if (!string.IsNullOrWhiteSpace(normalizedCity))
            AddQuery($"{normalizedCity}, Vietnam");

        if (!string.IsNullOrWhiteSpace(normalizedLocation))
            AddQuery($"{normalizedLocation}, Vietnam");

        foreach (var q in queries)
        {
            var result = await tryGeocode(q, expectedCityToken);
            if (result.HasValue) return result;
        }

        return null;
    }

    private async Task<(decimal Lat, decimal Lng)?> tryGeocode(string query, string expectedCityToken)
    {
        try
        {
            var url = $"https://nominatim.openstreetmap.org/search?q={Uri.EscapeDataString(query)}&format=json&limit=5&countrycodes=vn&addressdetails=1";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            if (!LooksLikeJsonArray(json))
                return null;

            var arr  = JsonSerializer.Deserialize<NominatimResult[]>(json, Opts);

            if (arr == null || arr.Length == 0) return null;

            var candidate = arr.FirstOrDefault(item =>
                string.IsNullOrWhiteSpace(expectedCityToken) ||
                IsExpectedCityMatch(item.DisplayName, expectedCityToken));

            if (candidate == null)
                return null;

            if (decimal.TryParse(candidate.Lat, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var lat) &&
                decimal.TryParse(candidate.Lon, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var lng))
            {
                return (lat, lng);
            }
        }
        catch { /* silent — geocoding failure should not block job creation */ }

        return null;
    }

    private static bool LooksLikeJsonArray(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        foreach (var ch in raw)
        {
            if (!char.IsWhiteSpace(ch))
                return ch == '[';
        }

        return false;
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

    private static bool IsExpectedCityMatch(string? displayName, string expectedCityToken)
    {
        if (string.IsNullOrWhiteSpace(expectedCityToken))
            return true;

        var normalizedDisplayName = NormalizeText(displayName);
        if (string.IsNullOrWhiteSpace(normalizedDisplayName))
            return false;

        return normalizedDisplayName.Contains(expectedCityToken, StringComparison.Ordinal);
    }

    private static string NormalizeAdministrativeName(string? value)
    {
        var normalized = NormalizeText(value)
            .Replace("tp.", "tp ", StringComparison.Ordinal)
            .Trim();

        if (normalized.StartsWith("thanh pho ", StringComparison.Ordinal))
            normalized = normalized["thanh pho ".Length..];
        else if (normalized.StartsWith("tp ", StringComparison.Ordinal))
            normalized = normalized["tp ".Length..];
        else if (normalized.StartsWith("tinh ", StringComparison.Ordinal))
            normalized = normalized["tinh ".Length..];

        return normalized.Trim();
    }

    private static string NormalizeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var source = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(source.Length);

        foreach (var ch in source)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        }

        return sb
            .ToString()
            .Normalize(NormalizationForm.FormC)
            .Replace("đ", "d", StringComparison.Ordinal);
    }

    private class NominatimResult
    {
        [JsonPropertyName("display_name")]
        public string? DisplayName { get; set; }

        public string Lat { get; set; } = "";
        public string Lon { get; set; } = "";
    }
}
