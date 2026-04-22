using System.Text.Json;
using Moq;
using Microsoft.Extensions.Logging.Abstractions;
using Nanny_BackEnd.DTOs.Recommendation;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services;
using Nanny_BackEnd.Services.Interfaces;

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
        var b = CreateSut();
        _mockRepo = b.Repo;
        _mockConfig = b.Config;
        _sut = b.Sut;
    }

    private static (
        Mock<IRecommendationRepository> Repo,
        Mock<IRecommendationConfigRepository> Config,
        RecommendationService Sut) CreateSut()
    {
        var mockRepo   = new Mock<IRecommendationRepository>();
        var mockConfig = new Mock<IRecommendationConfigRepository>();
        var mockParent = new Mock<IParentRepository>();
        var mockNanny  = new Mock<INannyProfileRepository>();
        var mockJob    = new Mock<IJobRepository>();
        var mockSub    = new Mock<ISubscriptionService>();
        var sut = new RecommendationService(
            mockRepo.Object,
            mockConfig.Object,
            mockParent.Object,
            mockNanny.Object,
            mockJob.Object,
            mockSub.Object,
            NullLogger<RecommendationService>.Instance);
        return (mockRepo, mockConfig, sut);
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

        Assert.Empty(result);

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

        Assert.Single(result);
        Assert.False(result[0].EmbeddingWasNull);
        Assert.InRange(result[0].SemanticScore, 0.999, 1.001);
        Assert.True(result[0].FinalScore > 0);
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

        Assert.Single(result);
        Assert.True(result[0].EmbeddingWasNull);
        Assert.InRange(result[0].SemanticScore, DefaultWeights.ColdStart - 0.001, DefaultWeights.ColdStart + 0.001);
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

        Assert.Equal(2, result.Count);
        Assert.True(result[0].FinalScore > result[1].FinalScore);
        Assert.Equal(nannyA.NannyProfileId, result[0].NannyProfileId);
        Assert.Equal(nannyB.NannyProfileId, result[1].NannyProfileId);
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

        Assert.True(withOverride[0].DistanceScore > withoutOverride[0].DistanceScore);
    }
}
