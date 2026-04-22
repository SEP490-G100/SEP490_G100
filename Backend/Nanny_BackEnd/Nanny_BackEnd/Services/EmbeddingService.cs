using System.Text;
using System.Text.Json;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Options;
using Nanny_BackEnd.DTOs.Recommendation;
using Nanny_BackEnd.Helpers;
using Nanny_BackEnd.Repositories;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services.Interfaces;
using OpenAI.Embeddings;

namespace Nanny_BackEnd.Services;

public class EmbeddingService : IEmbeddingService
{
    private readonly AzureOpenAIClient _azureClient;
    private readonly AzureOpenAIOptions _options;
    private readonly IRecommendationRepository _repo;
    private readonly ILogger<EmbeddingService> _logger;

    private static readonly string[] EducationLabels =
        { "Trung học", "Cao đẳng", "Đại học", "Thạc sĩ", "Khác" };

    private static readonly Dictionary<byte, string> ChildAgeGroupLabels = new()
    {
        { 1, "trẻ sơ sinh (0-1 tuổi)" },
        { 2, "trẻ chập chững (1-3 tuổi)" },
        { 3, "trẻ mầm non (3-6 tuổi)" },
        { 4, "học sinh tiểu học (6-12 tuổi)" }
    };

    public EmbeddingService(
        IOptions<AzureOpenAIOptions> options,
        IRecommendationRepository repo,
        ILogger<EmbeddingService> logger)
    {
        _options = options.Value;
        _repo = repo;
        _logger = logger;
        _azureClient = new AzureOpenAIClient(
            new Uri(_options.Endpoint),
            new Azure.AzureKeyCredential(_options.ApiKey));
    }

    // Public API

    public async Task EmbedNannyAsync(Guid nannyProfileId)
    {
        var model = await _repo.GetNannyReadModelAsync(nannyProfileId);
        if (model == null)
        {
            _logger.LogWarning("EmbedNannyAsync: NannyProfile {Id} not found.", nannyProfileId);
            return;
        }

        var text = BuildNannyText(model);
        var vector = await GetEmbeddingVectorAsync(text);
        var json = JsonSerializer.Serialize(vector);
        await _repo.SaveNannyEmbeddingAsync(nannyProfileId, json);
        _logger.LogInformation("Embedded nanny {Id} ({Chars} chars).", nannyProfileId, text.Length);
    }

    public async Task EmbedJobAsync(Guid jobId)
    {
        var model = await _repo.GetJobReadModelAsync(jobId);
        if (model == null)
        {
            _logger.LogWarning("EmbedJobAsync: JobPosting {Id} not found.", jobId);
            return;
        }

        var text = BuildJobText(model);
        var vector = await GetEmbeddingVectorAsync(text);
        var json = JsonSerializer.Serialize(vector);
        await _repo.SaveJobEmbeddingAsync(jobId, json);
        _logger.LogInformation("Embedded job {Id} ({Chars} chars).", jobId, text.Length);
    }

    public async Task<int> EmbedAllPendingNanniesAsync()
        => await EmbedNanniesAsync(await _repo.GetPendingEmbedNanniesAsync());

    public async Task<int> EmbedAllPendingJobsAsync()
        => await EmbedJobsAsync(await _repo.GetPendingEmbedJobsAsync());

    /// <summary>Force re-embed tất cả nanny, bất kể đã có embedding hay chưa.</summary>
    public async Task<int> EmbedAllNanniesForceAsync()
        => await EmbedNanniesAsync(await _repo.GetAllNannyReadModelsAsync());

    /// <summary>Force re-embed tất cả job, bất kể đã có embedding hay chưa.</summary>
    public async Task<int> EmbedAllJobsForceAsync()
        => await EmbedJobsAsync(await _repo.GetAllJobReadModelsAsync());

    private async Task<int> EmbedNanniesAsync(IEnumerable<NannyReadModelDto> list)
    {
        int count = 0;
        foreach (var m in list)
        {
            try
            {
                var text = BuildNannyText(m);
                var vector = await GetEmbeddingVectorAsync(text);
                var json = JsonSerializer.Serialize(vector);
                await _repo.SaveNannyEmbeddingAsync(m.NannyProfileId, json);
                count++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to embed nanny {Id}.", m.NannyProfileId);
            }
        }
        return count;
    }

    private async Task<int> EmbedJobsAsync(IEnumerable<JobReadModelDto> list)
    {
        int count = 0;
        foreach (var m in list)
        {
            try
            {
                var text = BuildJobText(m);
                var vector = await GetEmbeddingVectorAsync(text);
                var json = JsonSerializer.Serialize(vector);
                await _repo.SaveJobEmbeddingAsync(m.JobId, json);
                count++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to embed job {Id}.", m.JobId);
            }
        }
        return count;
    }

    // Template builders

    /// <summary>
    /// Nanny template:
    /// "Nanny {YearsOfExp} năm kinh nghiệm, {EducationLevel}.
    ///  {Bio}.
    ///  Kỹ năng: {skill1}, {skill2}, ..."
    /// </summary>
    public static string BuildNannyText(NannyReadModelDto m)
    {
        var sb = new StringBuilder();

        //  kinh nghiệm + học vấn
        var years = m.YearsOfExperience ?? 0;
        var edu = m.EducationLevel.HasValue && m.EducationLevel.Value >= 0 && m.EducationLevel.Value < EducationLabels.Length
            ? EducationLabels[m.EducationLevel.Value]
            : null;

        var line1 = edu != null
            ? $"Nanny {years} năm kinh nghiệm, {edu}."
            : $"Nanny {years} năm kinh nghiệm.";
        sb.AppendLine(line1);

        //  Skills (nếu có)
        if (m.SkillNames.Count > 0)
            sb.AppendLine("Kỹ năng: " + string.Join(", ", m.SkillNames) + ".");

        //  Bio (nếu có)
        if (!string.IsNullOrWhiteSpace(m.Bio))
            sb.AppendLine(m.Bio.Trim() + ".");

        return sb.ToString().Trim();
    }

    /// <summary>
    /// Job template:
    /// "{Title}.
    ///  [Cần nanny có kinh nghiệm chăm sóc {ChildAgeGroup}.] ← chỉ khi có
    ///  {Description}.
    ///  [{Characteristic}.] ← chỉ khi có
    ///  [{SpecialNeeds}.] ← chỉ khi có
    ///  Kỹ năng cần: {skill1}, {skill2}, ..."
    /// </summary>
    public static string BuildJobText(JobReadModelDto m)
    {
        var sb = new StringBuilder();

        // Dòng 1: Title — tóm tắt ngắn gọn nhất của job
        if (!string.IsNullOrWhiteSpace(m.Title))
            sb.AppendLine(m.Title.Trim() + ".");

        // Dòng 2: ChildAgeGroup [+ NumberOfChildren] (nếu có)
        if (m.ChildAgeGroup.HasValue && ChildAgeGroupLabels.TryGetValue(m.ChildAgeGroup.Value, out var ageLabel))
        {
            var ageLine = m.NumberOfChildren.HasValue
                ? $"Chăm {ageLabel}, {m.NumberOfChildren} bé."
                : $"Chăm {ageLabel}.";
            sb.AppendLine(ageLine);
        }

        // Dòng 3: Skills (nếu có) — trước description để nhấn mạnh yêu cầu kỹ năng
        if (m.RequiredSkillNames.Count > 0)
            sb.AppendLine("Yêu cầu kỹ năng: " + string.Join(", ", m.RequiredSkillNames) + ".");

        // Dòng 4: Description
        if (!string.IsNullOrWhiteSpace(m.Description))
            sb.AppendLine(m.Description.Trim() + ".");

        // Dòng 5: Characteristic (nếu có)
        if (!string.IsNullOrWhiteSpace(m.Characteristic))
        {
            var chars = ParseCharacteristic(m.Characteristic);
            if (chars.Count > 0)
                sb.AppendLine(string.Join(", ", chars) + ".");
        }

        // Dòng 6: SpecialNeeds (nếu có)
        if (!string.IsNullOrWhiteSpace(m.SpecialNeeds))
            sb.AppendLine(m.SpecialNeeds.Trim() + ".");

        return sb.ToString().Trim();
    }

    // Azure OpenAI call

    private async Task<float[]> GetEmbeddingVectorAsync(string text)
    {
        var embeddingClient = _azureClient.GetEmbeddingClient(_options.EmbeddingDeploymentName);
        var result = await embeddingClient.GenerateEmbeddingAsync(text);
        return result.Value.ToFloats().ToArray();
    }

    // Helpers

    /// <summary>
    /// Parse Characteristic field — hỗ trợ cả JSON array ["A","B"] và plain text "A, B".
    /// </summary>
    private static List<string> ParseCharacteristic(string raw)
    {
        raw = raw.Trim();
        if (raw.StartsWith('['))
        {
            try
            {
                var arr = JsonSerializer.Deserialize<List<string>>(raw);
                if (arr != null) return arr.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
            }
            catch { /* fallthrough */ }
        }
        // plain text
        return new List<string> { raw };
    }

    /// <summary>
    /// Deserialise embedding JSON từ DB thành float[].
    /// Trả về null nếu không có hoặc parse lỗi.
    /// </summary>
    public static float[]? DeserializeEmbedding(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<float[]>(json); }
        catch { return null; }
    }
}
