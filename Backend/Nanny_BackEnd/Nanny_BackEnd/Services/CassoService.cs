using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Nanny_BackEnd.Services;

public class CassoService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;
    private readonly CassoOptions _options;

    public CassoService(IHttpClientFactory httpFactory, IOptions<CassoOptions> options)
    {
        _http = httpFactory.CreateClient("Casso");
        _options = options.Value;
    }

    public bool isWebhookAuthorized(string? secureToken)
    {
        if (string.IsNullOrWhiteSpace(_options.WebhookSecureToken))
            return false;

        return string.Equals(_options.WebhookSecureToken, secureToken, StringComparison.Ordinal);
    }

    public async Task syncTransactions()
    {
        validateSyncOptions();

        using var request = new HttpRequestMessage(HttpMethod.Post, "sync");
        request.Headers.Authorization = new AuthenticationHeaderValue("Apikey", _options.ApiKey);
        request.Content = new StringContent(
            JsonSerializer.Serialize(new { bank_acc_id = _options.BankAccountId }),
            Encoding.UTF8,
            "application/json");

        using var response = await _http.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<CassoApiResponse>(json, JsonOptions);

        if (!response.IsSuccessStatusCode || result == null || result.Error != 0)
            throw new InvalidOperationException(result?.Message ?? "Khong the dong bo giao dich moi tu Casso.");
    }

    private void validateSyncOptions()
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey) || string.IsNullOrWhiteSpace(_options.BankAccountId))
            throw new InvalidOperationException("Vui long cau hinh Casso ApiKey va BankAccountId.");
    }

    private sealed class CassoApiResponse
    {
        public int Error { get; set; }
        public string Message { get; set; } = "";
    }
}
