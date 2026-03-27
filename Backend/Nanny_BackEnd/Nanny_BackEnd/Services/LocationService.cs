using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Nanny_BackEnd.DTOs.Address;

namespace Nanny_BackEnd.Services;

public class LocationService
{
    private const string CacheKey = "address-location-tree";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IWebHostEnvironment _env;
    private readonly IMemoryCache _cache;
    private readonly ILogger<LocationService> _logger;
    private static readonly Dictionary<string, string> ProvinceRenameMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Thừa Thiên Huế", "Thành phố Huế" }
    };
    private static readonly Dictionary<string, string[]> ProvinceAliasMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Thành phố Huế", ["Thừa Thiên Huế"] }
    };

    public LocationService(IWebHostEnvironment env, IMemoryCache cache, ILogger<LocationService> logger)
    {
        _env = env;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ProvinceLocationDto>> GetLocationTreeAsync(CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(CacheKey, out IReadOnlyList<ProvinceLocationDto>? cached) && cached is not null)
            return cached;

        var path = Path.Combine(_env.ContentRootPath, "Data", "vn-addresses.json");
        if (!File.Exists(path))
        {
            _logger.LogWarning("Location JSON file not found at {Path}", path);
            return [];
        }

        try
        {
            await using var stream = File.OpenRead(path);
            var raw = await JsonSerializer.DeserializeAsync<List<ProvinceLocationDto>>(stream, JsonOptions, cancellationToken);
            var result = normalizeTree(raw ?? []);

            _cache.Set(CacheKey, result, TimeSpan.FromHours(24));
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load location tree from local JSON file.");
            return [];
        }
    }

    public async Task<List<LocationApproxSuggestion>> SuggestApproximateAsync(string query, int limit, CancellationToken cancellationToken = default)
    {
        var tree = await GetLocationTreeAsync(cancellationToken);
        if (tree.Count == 0) return [];

        var parsed = parseAddressTokens(query);
        var cityToken = parsed.CityToken;
        var districtToken = parsed.DistrictToken;
        var wardToken = parsed.WardToken;

        var province = findBestProvince(tree, cityToken, parsed.RawNormalized)
            ?? tree.FirstOrDefault();
        if (province is null) return [];

        var district = findBestDistrict(province, districtToken, parsed.RawNormalized)
            ?? province.Districts.FirstOrDefault();
        var ward = district is null ? null : findBestWard(district, wardToken, parsed.RawNormalized) ?? district.Wards.FirstOrDefault();

        var lat = ward?.Latitude ?? district?.Latitude ?? province.Latitude;
        var lng = ward?.Longitude ?? district?.Longitude ?? province.Longitude;
        if (lat is null || lng is null) return [];

        var item = new LocationApproxSuggestion
        {
            DisplayName = buildDisplayName(ward?.Name, district?.Name, province.Name),
            Latitude = lat.Value,
            Longitude = lng.Value,
            City = province.Name,
            District = district?.Name,
            Ward = ward?.Name
        };

        return [item];
    }

    private static IReadOnlyList<ProvinceLocationDto> normalizeTree(List<ProvinceLocationDto> source)
    {
        foreach (var province in source)
        {
            province.Name = (province.Name ?? "").Trim();
            if (ProvinceRenameMap.TryGetValue(province.Name, out var renamed) && !string.IsNullOrWhiteSpace(renamed))
                province.Name = renamed;
            province.Districts ??= [];
            foreach (var district in province.Districts)
            {
                district.Name = (district.Name ?? "").Trim();
                district.Wards ??= [];
                foreach (var ward in district.Wards)
                    ward.Name = (ward.Name ?? "").Trim();
            }
        }
        return source.AsReadOnly();
    }

    private static string buildDisplayName(string? ward, string? district, string city)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(ward)) parts.Add(ward!);
        if (!string.IsNullOrWhiteSpace(district)) parts.Add(district!);
        parts.Add(city);
        return string.Join(", ", parts);
    }

    private static (string RawNormalized, string? CityToken, string? DistrictToken, string? WardToken) parseAddressTokens(string raw)
    {
        var parts = (raw ?? "")
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(normalizeText)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();

        var city = parts.Count >= 1 ? parts[^1] : null;
        var district = parts.Count >= 2 ? parts[^2] : null;
        var ward = parts.Count >= 3 ? parts[^3] : null;

        return (normalizeText(raw ?? ""), city, district, ward);
    }

    private static ProvinceLocationDto? findBestProvince(IEnumerable<ProvinceLocationDto> tree, string? cityToken, string fullQuery)
    {
        if (!string.IsNullOrWhiteSpace(cityToken))
        {
            var exact = tree.FirstOrDefault(p => normalizeText(p.Name) == cityToken);
            if (exact is not null) return exact;

            var aliasMatched = tree.FirstOrDefault(p => MatchProvinceAlias(p.Name, cityToken));
            if (aliasMatched is not null) return aliasMatched;

            var contains = tree.FirstOrDefault(p => normalizeText(p.Name).Contains(cityToken));
            if (contains is not null) return contains;
        }

        return tree.FirstOrDefault(p =>
            fullQuery.Contains(normalizeText(p.Name)) ||
            MatchProvinceAlias(p.Name, fullQuery));
    }

    private static bool MatchProvinceAlias(string provinceName, string token)
    {
        if (string.IsNullOrWhiteSpace(provinceName) || string.IsNullOrWhiteSpace(token))
            return false;

        var normalizedToken = normalizeText(token);
        if (ProvinceAliasMap.TryGetValue(provinceName, out var aliases))
        {
            return aliases.Any(alias =>
            {
                var normalizedAlias = normalizeText(alias);
                return normalizedAlias == normalizedToken
                       || normalizedAlias.Contains(normalizedToken)
                       || normalizedToken.Contains(normalizedAlias);
            });
        }

        return false;
    }

    private static DistrictLocationDto? findBestDistrict(ProvinceLocationDto province, string? districtToken, string fullQuery)
    {
        if (province.Districts.Count == 0) return null;
        if (!string.IsNullOrWhiteSpace(districtToken))
        {
            var exact = province.Districts.FirstOrDefault(d => normalizeText(d.Name) == districtToken);
            if (exact is not null) return exact;
            var contains = province.Districts.FirstOrDefault(d => normalizeText(d.Name).Contains(districtToken));
            if (contains is not null) return contains;
        }

        return province.Districts.FirstOrDefault(d => fullQuery.Contains(normalizeText(d.Name)));
    }

    private static WardLocationDto? findBestWard(DistrictLocationDto district, string? wardToken, string fullQuery)
    {
        if (district.Wards.Count == 0) return null;
        if (!string.IsNullOrWhiteSpace(wardToken))
        {
            var exact = district.Wards.FirstOrDefault(w => normalizeText(w.Name) == wardToken);
            if (exact is not null) return exact;
            var contains = district.Wards.FirstOrDefault(w => normalizeText(w.Name).Contains(wardToken));
            if (contains is not null) return contains;
        }

        return district.Wards.FirstOrDefault(w => fullQuery.Contains(normalizeText(w.Name)));
    }

    private static string normalizeText(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        var normalized = input.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }

        return sb.ToString().Normalize(NormalizationForm.FormC).Replace("đ", "d");
    }

    public class LocationApproxSuggestion
    {
        public string DisplayName { get; set; } = "";
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public string? City { get; set; }
        public string? District { get; set; }
        public string? Ward { get; set; }
    }
}
