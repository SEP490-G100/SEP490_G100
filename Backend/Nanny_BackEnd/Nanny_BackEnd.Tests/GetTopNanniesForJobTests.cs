using Moq;
using FluentAssertions;
using System.Text.Json;
using Nanny_BackEnd.DTOs.Recommendation;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services;

namespace Nanny_BackEnd.Tests;

public class GetTopNanniesForJobTests
{
    private readonly Mock<IRecommendationRepository> _mockRepo;
    private readonly Mock<IRecommendationConfigRepository> _mockConfig;
    private readonly RecommendationService _sut;

    // Weights chuẩn dùng trong mọi TC
    private static readonly ScoringWeights DefaultWeights =
        new(Semantic: 0.80, Salary: 0.12, Distance: 0.08, ColdStart: 0.75);

    public GetTopNanniesForJobTests()
    {
        var b = RecommendationServiceTestBuilder.Create();
        _mockRepo = b.Repo;
        _mockConfig = b.Config;
        _sut = b.Sut;
    }

    // ── Helper: tạo NannyCandidate tối giản ─────────────────────────────
    private static NannyCandidate MakeCandidate(
        string?  embeddingJson   = null,
        decimal? rating          = null,
        decimal? lat             = null,
        decimal? lng             = null,
        int?     maxTravel       = null,
        decimal? salaryMin       = null,
        decimal? salaryMax       = null) => new()
    {
        NannyProfileId = Guid.NewGuid(),
        FullName       = "Test Nanny",
        Embedding      = embeddingJson,
        AverageRating  = rating,
        Latitude       = lat,
        Longitude      = lng,
        MaxTravelDistance = maxTravel,
        ExpectedSalaryMin = salaryMin,
        ExpectedSalaryMax = salaryMax,
        Skills         = new()
    };

    // ── Helper: tạo JobReadModelDto tối giản ────────────────────────────
    private static JobReadModelDto MakeJobModel(
        string?  embeddingJson  = null,
        decimal? lat            = null,
        decimal? lng            = null,
        decimal? salaryMin      = null,
        decimal? salaryMax      = null,
        bool     negotiable     = false) => new()
    {
        JobId             = Guid.NewGuid(),
        Embedding         = embeddingJson,
        Latitude          = lat,
        Longitude         = lng,
        SalaryMin         = salaryMin,
        SalaryMax         = salaryMax,
        SalaryNegotiable  = negotiable,
        RequiredSkillIds  = new()
    };

    // ── Helper: serialize float[] → JSON string (dùng làm embedding giả) ─
    private static string EmbedJson(params float[] values) =>
        JsonSerializer.Serialize(values);

    // ── TC1: Không có ứng viên → trả về danh sách rỗng ngay lập tức ─────
    [Fact]
    public async Task NoCandidates()
    {
        var jobId = Guid.NewGuid();
        _mockRepo.Setup(r => r.GetNannyCandidatesAsync(jobId))
                 .ReturnsAsync(new List<NannyCandidate>());

        var result = await _sut.GetTopNanniesForJobAsync(jobId);

        result.Should().BeEmpty();

        // GetJobReadModelAsync và GetWeightsAsync không nên được gọi
        _mockRepo.Verify(r => r.GetJobReadModelAsync(It.IsAny<Guid>()), Times.Never);
        _mockConfig.Verify(c => c.GetWeightsAsync(), Times.Never);
    }

    // ── TC2: Có embedding → dùng cosine similarity, EmbeddingWasNull = false
    [Fact]
    public async Task WithEmbedding()
    {
        var jobId     = Guid.NewGuid();
        var embed     = EmbedJson(1f, 0f);                     // vector [1, 0]
        var candidate = MakeCandidate(embeddingJson: embed);   // same vector → cosine = 1.0
        var jobModel  = MakeJobModel(embeddingJson: embed);

        _mockRepo.Setup(r => r.GetNannyCandidatesAsync(jobId)).ReturnsAsync([candidate]);
        _mockRepo.Setup(r => r.GetJobReadModelAsync(jobId)).ReturnsAsync(jobModel);
        _mockConfig.Setup(c => c.GetWeightsAsync()).ReturnsAsync(DefaultWeights);

        var result = await _sut.GetTopNanniesForJobAsync(jobId);

        result.Should().HaveCount(1);
        result[0].EmbeddingWasNull.Should().BeFalse();
        result[0].SemanticScore.Should().BeApproximately(1.0, precision: 0.001);
        result[0].FinalScore.Should().BeGreaterThan(0);
    }

    // ── TC3: Embedding null → dùng ColdStart thay cosine ────────────────
    [Fact]
    public async Task ColdStart()
    {
        var jobId     = Guid.NewGuid();
        var candidate = MakeCandidate(embeddingJson: null);    // không có embedding
        var jobModel  = MakeJobModel(embeddingJson: null);

        _mockRepo.Setup(r => r.GetNannyCandidatesAsync(jobId)).ReturnsAsync([candidate]);
        _mockRepo.Setup(r => r.GetJobReadModelAsync(jobId)).ReturnsAsync(jobModel);
        _mockConfig.Setup(c => c.GetWeightsAsync()).ReturnsAsync(DefaultWeights);

        var result = await _sut.GetTopNanniesForJobAsync(jobId);

        result.Should().HaveCount(1);
        result[0].EmbeddingWasNull.Should().BeTrue();
        result[0].SemanticScore.Should().BeApproximately(DefaultWeights.ColdStart, precision: 0.001);
    }

    // ── TC4: topK giới hạn kết quả, kết quả sắp xếp theo FinalScore ─────
    // 3 ứng viên khác nhau rating → boost khác nhau → FinalScore khác nhau
    [Fact]
    public async Task TopKOrdering()
    {
        var jobId = Guid.NewGuid();

        // Tất cả null embedding → cùng SemanticScore = ColdStart
        // Tất cả null location/salary → distanceScore=0.8, salaryScore=0.8
        // Chỉ khác nhau ở rating → boost khác nhau → FinalScore khác nhau
        var nannyA = MakeCandidate(rating: 5.0m);  // boost ≈ 1.15  (FinalScore cao nhất)
        var nannyB = MakeCandidate(rating: 4.0m);  // boost ≈ 1.017 (FinalScore giữa)
        var nannyC = MakeCandidate(rating: null);  // boost = 1.0   (FinalScore thấp nhất)

        _mockRepo.Setup(r => r.GetNannyCandidatesAsync(jobId))
                 .ReturnsAsync([nannyA, nannyB, nannyC]);
        _mockRepo.Setup(r => r.GetJobReadModelAsync(jobId))
                 .ReturnsAsync(MakeJobModel());
        _mockConfig.Setup(c => c.GetWeightsAsync()).ReturnsAsync(DefaultWeights);

        var result = await _sut.GetTopNanniesForJobAsync(jobId, topK: 2);

        result.Should().HaveCount(2);

        // Thứ tự: FinalScore giảm dần
        result[0].FinalScore.Should().BeGreaterThan(result[1].FinalScore);

        // Top 1 phải là nannyA (rating 5.0 → boost cao nhất)
        result[0].NannyProfileId.Should().Be(nannyA.NannyProfileId);

        // Top 2 phải là nannyB (rating 4.0 > null)
        result[1].NannyProfileId.Should().Be(nannyB.NannyProfileId);
    }

    // ── TC5: overrideLat/Lng từ client → thay tọa độ job trong DB ────────
    // Không override: job ở Hà Nội, nanny ở HCM → xa → distanceScore thấp
    // Có override: dùng tọa độ HCM → gần nanny → distanceScore cao
    [Fact]
    public async Task OverrideCoordinates()
    {
        var jobId = Guid.NewGuid();

        // Nanny ở HCM
        var candidate = MakeCandidate(
            lat:       10.7m,
            lng:       106.7m,
            maxTravel: 10);      // bán kính 10km

        // Job model lưu tọa độ Hà Nội (xa ~1100km)
        var jobModel = MakeJobModel(lat: 21.0m, lng: 105.8m);

        _mockRepo.Setup(r => r.GetNannyCandidatesAsync(jobId)).ReturnsAsync([candidate]);
        _mockRepo.Setup(r => r.GetJobReadModelAsync(jobId)).ReturnsAsync(jobModel);
        _mockConfig.Setup(c => c.GetWeightsAsync()).ReturnsAsync(DefaultWeights);

        // Gọi không override → job tọa độ Hà Nội → nanny ở HCM rất xa → score thấp (≈ 0.1)
        var withoutOverride = await _sut.GetTopNanniesForJobAsync(jobId);

        // Gọi có override → tọa độ HCM → gần nanny → score cao (≈ 1.0)
        var withOverride = await _sut.GetTopNanniesForJobAsync(
            jobId,
            overrideLat: 10.7,
            overrideLng: 106.7);

        withOverride[0].DistanceScore.Should().BeGreaterThan(
            withoutOverride[0].DistanceScore,
            because: "overriding to nanny's own location should give a much higher distance score");
    }
}
