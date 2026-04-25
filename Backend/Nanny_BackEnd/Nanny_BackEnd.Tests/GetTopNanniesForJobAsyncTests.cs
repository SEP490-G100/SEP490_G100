using System.Text.Json;
using Moq;
using Microsoft.Extensions.Logging.Abstractions;
using Nanny_BackEnd.DTOs.Recommendation;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Tests;

public class GetTopNanniesForJobAsyncTests
{
    private readonly Mock<IRecommendationRepository> _mockRepo;
    private readonly Mock<IRecommendationConfigRepository> _mockConfig;
    private readonly RecommendationService _sut;

    private static readonly ScoringWeights DefaultWeights =
        new(Semantic: 0.80, Salary: 0.12, Distance: 0.08, ColdStart: 0.75);

    public GetTopNanniesForJobAsyncTests()
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

    // -- Helper: t?o NannyCandidate t?i gi?n -----------------------------
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

    // -- Helper: t?o JobReadModelDto t?i gi?n ----------------------------
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

    private static string EmbedJson(params float[] values) =>
        JsonSerializer.Serialize(values);

    [Fact]
    public async Task NoCandidates()
    {
        var jobId = Guid.NewGuid();
        _mockRepo.Setup(r => r.GetNannyCandidatesAsync(jobId))
                 .ReturnsAsync(new List<NannyCandidate>());

        var result = await _sut.GetTopNanniesForJobAsync(jobId);

        Assert.Empty(result);
    }

    [Fact]
    public async Task WithEmbedding()
    {
        var jobId     = Guid.NewGuid();
        var embed     = EmbedJson(1f, 0f);                     // vector [1, 0]
        var candidate = MakeCandidate(embeddingJson: embed);   // same vector ? cosine = 1.0
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

    [Fact]
    public async Task ColdStart()
    {
        var jobId     = Guid.NewGuid();
        var candidate = MakeCandidate(embeddingJson: null);        var jobModel  = MakeJobModel(embeddingJson: null);

        _mockRepo.Setup(r => r.GetNannyCandidatesAsync(jobId)).ReturnsAsync([candidate]);
        _mockRepo.Setup(r => r.GetJobReadModelAsync(jobId)).ReturnsAsync(jobModel);
        _mockConfig.Setup(c => c.GetWeightsAsync()).ReturnsAsync(DefaultWeights);

        var result = await _sut.GetTopNanniesForJobAsync(jobId);

        Assert.Single(result);
        Assert.True(result[0].EmbeddingWasNull);
        Assert.InRange(result[0].SemanticScore, DefaultWeights.ColdStart - 0.001, DefaultWeights.ColdStart + 0.001);
    }

    // -- TC4: topK gi?i h?n k?t qu?, k?t qu? s?p x?p theo FinalScore -----
    [Fact]
    public async Task TopKOrdering()
    {
        var jobId = Guid.NewGuid();

        // T?t c? null location/salary ? distanceScore=0.8, salaryScore=0.8
        var nannyA = MakeCandidate(rating: 5.0m);        var nannyB = MakeCandidate(rating: 4.0m);        var nannyC = MakeCandidate(rating: null);  // boost = 1.0   (FinalScore th?p nh?t)

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

    // -- TC5: overrideLat/Lng t? client ? thay t?a d? job trong DB --------
    [Fact]
    public async Task OverrideCoordinates()
    {
        var jobId = Guid.NewGuid();

        // Nanny ? HCM
        var candidate = MakeCandidate(
            lat:       10.7m,
            lng:       106.7m,
            maxTravel: 10);
        var jobModel = MakeJobModel(lat: 21.0m, lng: 105.8m);

        _mockRepo.Setup(r => r.GetNannyCandidatesAsync(jobId)).ReturnsAsync([candidate]);
        _mockRepo.Setup(r => r.GetJobReadModelAsync(jobId)).ReturnsAsync(jobModel);
        _mockConfig.Setup(c => c.GetWeightsAsync()).ReturnsAsync(DefaultWeights);

        var withoutOverride = await _sut.GetTopNanniesForJobAsync(jobId);

        var withOverride = await _sut.GetTopNanniesForJobAsync(
            jobId,
            overrideLat: 10.7,
            overrideLng: 106.7);

        Assert.True(withOverride[0].DistanceScore > withoutOverride[0].DistanceScore);
    }
}
