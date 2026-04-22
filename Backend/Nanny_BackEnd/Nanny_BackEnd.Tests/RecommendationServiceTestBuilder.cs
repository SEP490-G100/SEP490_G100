using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Tests;

/// <summary>Giữ ctor <see cref="RecommendationService"/> tại một chỗ khi thêm DI.</summary>
internal static class RecommendationServiceTestBuilder
{
    public static (
        Mock<IRecommendationRepository> Repo,
        Mock<IRecommendationConfigRepository> Config,
        Mock<IParentRepository> Parent,
        Mock<INannyProfileRepository> Nanny,
        Mock<IJobRepository> Job,
        Mock<ISubscriptionService> Sub,
        RecommendationService Sut) Create()
    {
        var mockRepo = new Mock<IRecommendationRepository>();
        var mockConfig = new Mock<IRecommendationConfigRepository>();
        var mockParent = new Mock<IParentRepository>();
        var mockNanny = new Mock<INannyProfileRepository>();
        var mockJob = new Mock<IJobRepository>();
        var mockSub = new Mock<ISubscriptionService>();
        var sut = new RecommendationService(
            mockRepo.Object,
            mockConfig.Object,
            mockParent.Object,
            mockNanny.Object,
            mockJob.Object,
            mockSub.Object,
            NullLogger<RecommendationService>.Instance);
        return (mockRepo, mockConfig, mockParent, mockNanny, mockJob, mockSub, sut);
    }
}
