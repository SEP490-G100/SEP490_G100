using Moq;
using FluentAssertions;
using System.Text.Json;
using Nanny_BackEnd.DTOs.Recommendation;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services;

namespace Nanny_BackEnd.Tests;

public class GetTopJobsForNannyTests
{
    private readonly Mock<IRecommendationRepository> _mockRepo;
    private readonly Mock<IRecommendationConfigRepository> _mockConfig;
    private readonly RecommendationService _sut;

    private static readonly ScoringWeights DefaultWeights =
        new(Semantic: 0.80, Salary: 0.12, Distance: 0.08, ColdStart: 0.75);

    public GetTopJobsForNannyTests()
    {
        var b = RecommendationServiceTestBuilder.Create();
        _mockRepo = b.Repo;
        _mockConfig = b.Config;
        _sut = b.Sut;
    }

    // ── Helpers ──────────────────────────────────────────────────────────
    private static JobCandidate MakeJob(
        string?  embeddingJson = null,
        decimal? lat           = null,
        decimal? lng           = null,
        decimal? salaryMin     = null,
        decimal? salaryMax     = null,
        bool     negotiable    = false) => new()
    {
        JobId            = Guid.NewGuid(),
        Title            = "Test Job",
        Embedding        = embeddingJson,
        Latitude         = lat,
        Longitude        = lng,
        SalaryMin        = salaryMin,
        SalaryMax        = salaryMax,
        SalaryNegotiable = negotiable,
        RequiredSkills   = new()
    };

    private static NannyReadModelDto MakeNannyModel(
        string?  embeddingJson = null,
        decimal? lat           = null,
        decimal? lng           = null,
        int?     maxTravel     = null,
        decimal? salaryMin     = null,
        decimal? salaryMax     = null) => new()
    {
        NannyProfileId    = Guid.NewGuid(),
        Embedding         = embeddingJson,
        Latitude          = lat,
        Longitude         = lng,
        MaxTravelDistance = maxTravel,
        ExpectedSalaryMin = salaryMin,
        ExpectedSalaryMax = salaryMax
    };

    private static string EmbedJson(params float[] values) =>
        JsonSerializer.Serialize(values);

    // ── TC1: Không có job ứng viên → trả về danh sách rỗng ngay lập tức ─
    [Fact]
    public async Task NoCandidates()
    {
        var nannyId = Guid.NewGuid();
        _mockRepo.Setup(r => r.GetJobCandidatesAsync(nannyId))
                 .ReturnsAsync(new List<JobCandidate>());

        var result = await _sut.GetTopJobsForNannyAsync(nannyId);

        result.Should().BeEmpty();

        // Không cần lấy NannyReadModel hay Weights nếu không có ứng viên
        _mockRepo.Verify(r => r.GetNannyReadModelAsync(It.IsAny<Guid>()), Times.Never);
        _mockConfig.Verify(c => c.GetWeightsAsync(), Times.Never);
    }

    // ── TC2: Có embedding → dùng cosine similarity, EmbeddingWasNull = false
    [Fact]
    public async Task WithEmbedding()
    {
        var nannyId    = Guid.NewGuid();
        var embed      = EmbedJson(1f, 0f);              // vector [1, 0]
        var job        = MakeJob(embeddingJson: embed);  // same vector → cosine = 1.0
        var nannyModel = MakeNannyModel(embeddingJson: embed);

        _mockRepo.Setup(r => r.GetJobCandidatesAsync(nannyId)).ReturnsAsync([job]);
        _mockRepo.Setup(r => r.GetNannyReadModelAsync(nannyId)).ReturnsAsync(nannyModel);
        _mockConfig.Setup(c => c.GetWeightsAsync()).ReturnsAsync(DefaultWeights);

        var result = await _sut.GetTopJobsForNannyAsync(nannyId);

        result.Should().HaveCount(1);
        result[0].EmbeddingWasNull.Should().BeFalse();
        result[0].SemanticScore.Should().BeApproximately(1.0, precision: 0.001);
        result[0].FinalScore.Should().BeGreaterThan(0);
    }

    // ── TC3: Embedding null → dùng ColdStart thay cosine ────────────────
    [Fact]
    public async Task ColdStart()
    {
        var nannyId    = Guid.NewGuid();
        var job        = MakeJob(embeddingJson: null);
        var nannyModel = MakeNannyModel(embeddingJson: null);

        _mockRepo.Setup(r => r.GetJobCandidatesAsync(nannyId)).ReturnsAsync([job]);
        _mockRepo.Setup(r => r.GetNannyReadModelAsync(nannyId)).ReturnsAsync(nannyModel);
        _mockConfig.Setup(c => c.GetWeightsAsync()).ReturnsAsync(DefaultWeights);

        var result = await _sut.GetTopJobsForNannyAsync(nannyId);

        result.Should().HaveCount(1);
        result[0].EmbeddingWasNull.Should().BeTrue();
        result[0].SemanticScore.Should().BeApproximately(DefaultWeights.ColdStart, precision: 0.001);
    }

    // ── TC4: topK giới hạn kết quả, sắp xếp theo FinalScore ─────────────
    // Job không có rating → boost luôn = 1.0
    // Phân biệt bằng độ tương đồng embedding: 1.0 / 0.75 (ColdStart) / 0.0
    [Fact]
    public async Task TopKOrdering()
    {
        var nannyId    = Guid.NewGuid();
        var nannyEmbed = EmbedJson(1f, 0f);              // nanny vector [1, 0]

        var jobHigh = MakeJob(embeddingJson: EmbedJson(1f, 0f));  // cosine = 1.0
        var jobMid  = MakeJob(embeddingJson: null);               // ColdStart = 0.75
        var jobLow  = MakeJob(embeddingJson: EmbedJson(0f, 1f));  // cosine = 0.0

        _mockRepo.Setup(r => r.GetJobCandidatesAsync(nannyId))
                 .ReturnsAsync([jobHigh, jobMid, jobLow]);
        _mockRepo.Setup(r => r.GetNannyReadModelAsync(nannyId))
                 .ReturnsAsync(MakeNannyModel(embeddingJson: nannyEmbed));
        _mockConfig.Setup(c => c.GetWeightsAsync()).ReturnsAsync(DefaultWeights);

        var result = await _sut.GetTopJobsForNannyAsync(nannyId, topK: 2);

        result.Should().HaveCount(2);

        // Thứ tự FinalScore giảm dần
        result[0].FinalScore.Should().BeGreaterThan(result[1].FinalScore);

        // Top 1: jobHigh (cosine = 1.0)
        result[0].JobId.Should().Be(jobHigh.JobId);

        // Top 2: jobMid (ColdStart = 0.75 > cosine 0.0)
        result[1].JobId.Should().Be(jobMid.JobId);
    }

    // ── TC5: Có tọa độ cả 2 phía → distKm được tính, DistanceScore < 0.8 ─
    // Khi không có tọa độ → distanceScore mặc định = 0.8
    // Khi có tọa độ nhưng nanny và job ở xa → distanceScore < 0.8
    [Fact]
    public async Task WithLocation_DistanceScoreCalculated()
    {
        var nannyId = Guid.NewGuid();

        // Nanny ở HCM, job ở Hà Nội (~1100km), MaxTravelDistance = 10km
        var job = MakeJob(lat: 21.0m, lng: 105.8m);
        var nannyModel = MakeNannyModel(
            lat:       10.7m,
            lng:       106.7m,
            maxTravel: 10);

        _mockRepo.Setup(r => r.GetJobCandidatesAsync(nannyId)).ReturnsAsync([job]);
        _mockRepo.Setup(r => r.GetNannyReadModelAsync(nannyId)).ReturnsAsync(nannyModel);
        _mockConfig.Setup(c => c.GetWeightsAsync()).ReturnsAsync(DefaultWeights);

        var result = await _sut.GetTopJobsForNannyAsync(nannyId);

        result.Should().HaveCount(1);

        // Khoảng cách ~1100km >> maxTravel 10km → distanceScore chạm sàn 0.1
        result[0].DistanceKm.Should().BeGreaterThan(1000);
        result[0].DistanceScore.Should().BeApproximately(0.1, precision: 0.01);
    }
}
